using System;
using System.Collections.Generic;
using InfiniteOrbit.GameMath;

namespace InfiniteOrbit.Core
{
    public enum FeatureKind
    {
        None,
        HoldAndSpin,
        FreeSpins,
    }

    public enum SpinRejection
    {
        None,
        NotIdle,
        InsufficientCredits,
    }

    /// <summary>
    /// The cabinet's game controller, free of Unity. It owns the meters, asks the math engine for a complete
    /// <see cref="RoundResult"/>, and walks the state machine through the presentation phases of that round.
    /// The presentation calls <see cref="Advance"/> when it finishes showing the current phase; it never feeds
    /// anything back into the outcome.
    /// </summary>
    public sealed class GameSession
    {
        private readonly SlotEngine _engine;
        private readonly IRng _rng;
        private readonly Func<DateTime> _clock;
        private readonly GameStateMachine _machine = new GameStateMachine();
        private readonly Queue<(GameState state, FeatureKind feature)> _phases = new Queue<(GameState, FeatureKind)>();

        private long _creditsBeforeRound;
        private bool _roundIsAutoPlay;

        public GameConfig Config => _engine.Config;
        public GameStateMachine Machine => _machine;
        public GameState State => _machine.Current;
        public GameHistory History { get; } = new GameHistory();
        public AutoPlay AutoPlay { get; } = new AutoPlay();

        /// <summary>Credit meter. The bet is debited when a spin starts; wins are credited when the round settles.</summary>
        public long Credits { get; private set; }

        /// <summary>Win meter: total win of the last settled round (0 while a new round is in play).</summary>
        public long WinMeter { get; private set; }

        public int BetLevel { get; private set; }
        public long TotalBet => Config.TotalBetForLevel(BetLevel);
        public long LineBet => Config.LineBets[BetLevel];
        public int MaxBetLevel => Config.LineBets.Length - 1;

        /// <summary>The round currently being presented (null when idle before the first spin).</summary>
        public RoundResult CurrentRound { get; private set; }

        /// <summary>Which feature the current BonusIntro/feature phase belongs to.</summary>
        public FeatureKind ActiveFeature { get; private set; }

        public long RoundsPlayed { get; private set; }
        public string TiltMessage { get; private set; }

        // Accounting meters used for reconciliation (like a cabinet's coin-in / coin-out).
        public long StartingCredits { get; }
        public long CreditsAdded { get; private set; }
        public long TotalWagered { get; private set; }
        public long TotalWon { get; private set; }

        public event Action<GameState, GameState> StateChanged;
        public event Action<RoundResult> RoundStarted;
        public event Action<HistoryEntry> RoundSettled;
        public event Action MetersChanged;
        public event Action<AutoPlayStopReason> AutoPlayStopped;

        public GameSession(GameConfig config, IRng rng, long startingCredits, Func<DateTime> clock = null)
        {
            _engine = new SlotEngine(config);
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _clock = clock ?? (() => DateTime.Now);
            Credits = startingCredits;
            StartingCredits = startingCredits;
            _machine.Changed += (from, to) => StateChanged?.Invoke(from, to);
        }

        public bool CanSpin => State == GameState.Idle && Credits >= TotalBet;
        public bool CanChangeBet => State == GameState.Idle && !AutoPlay.Active;

        // ---------------------------------------------------------------- bets

        public bool SetBetLevel(int level)
        {
            if (!CanChangeBet || level < 0 || level > MaxBetLevel) return false;
            BetLevel = level;
            MetersChanged?.Invoke();
            return true;
        }

        public bool BetUp() => SetBetLevel(BetLevel + 1);
        public bool BetDown() => SetBetLevel(BetLevel - 1);

        /// <summary>Selects the highest bet level and spins it, like the cabinet's MAX BET button.</summary>
        public SpinRejection MaxBetSpin()
        {
            if (State != GameState.Idle) return SpinRejection.NotIdle;
            SetBetLevel(MaxBetLevel);
            return RequestSpin();
        }

        /// <summary>Demo-only "insert credits" (fake credits).</summary>
        public void AddCredits(long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Credits += amount;
            CreditsAdded += amount;
            MetersChanged?.Invoke();
        }

        // ---------------------------------------------------------------- round flow

        public SpinRejection RequestSpin()
        {
            if (State != GameState.Idle) return SpinRejection.NotIdle;
            if (Credits < TotalBet) return SpinRejection.InsufficientCredits;

            _creditsBeforeRound = Credits;
            _roundIsAutoPlay = AutoPlay.Active;
            Credits -= TotalBet;
            TotalWagered += TotalBet;
            WinMeter = 0;

            // The entire outcome is decided here, before the state machine leaves Idle.
            CurrentRound = _engine.PlayRound(_rng, BetLevel);
            BuildPhases(CurrentRound);

            ActiveFeature = FeatureKind.None;
            MetersChanged?.Invoke();
            _machine.TransitionTo(GameState.Spinning);
            RoundStarted?.Invoke(CurrentRound);
            return SpinRejection.None;
        }

        private void BuildPhases(RoundResult r)
        {
            _phases.Clear();
            if (r.HoldAndSpin != null)
            {
                _phases.Enqueue((GameState.BonusIntro, FeatureKind.HoldAndSpin));
                _phases.Enqueue((GameState.HoldAndSpin, FeatureKind.HoldAndSpin));
            }
            if (r.FreeSpins != null)
            {
                _phases.Enqueue((GameState.BonusIntro, FeatureKind.FreeSpins));
                _phases.Enqueue((GameState.FreeSpins, FeatureKind.FreeSpins));
            }
            if (r.BigWinTier != BigWinTier.None) _phases.Enqueue((GameState.BigWin, FeatureKind.None));
            if (r.TotalWin > 0) _phases.Enqueue((GameState.WinPresentation, FeatureKind.None));
            _phases.Enqueue((GameState.Idle, FeatureKind.None));
        }

        /// <summary>Phases still to be presented after the current one (for UI previews and tests).</summary>
        public IEnumerable<GameState> UpcomingStates
        {
            get { foreach (var p in _phases) yield return p.state; }
        }

        /// <summary>Called by the presentation when it has finished showing the current phase.</summary>
        public void Advance()
        {
            if (State == GameState.Idle || State == GameState.Error) return;
            var (next, feature) = _phases.Dequeue();
            if (next != GameState.Idle) ActiveFeature = feature;

            if (next == GameState.Idle)
            {
                Settle();
                _machine.TransitionTo(GameState.Idle);
                AfterSettle();
            }
            else
            {
                _machine.TransitionTo(next);
            }
        }

        private void Settle()
        {
            var r = CurrentRound;
            Credits += r.TotalWin;
            TotalWon += r.TotalWin;
            WinMeter = r.TotalWin;
            RoundsPlayed++;
            ActiveFeature = FeatureKind.None;

            var entry = HistoryEntry.FromRound(RoundsPlayed, _clock(), _creditsBeforeRound, Credits, r, _roundIsAutoPlay);
            History.Add(entry);
            MetersChanged?.Invoke();
            RoundSettled?.Invoke(entry);
        }

        private void AfterSettle()
        {
            if (!MetersReconcile())
            {
                Tilt("METER RECONCILIATION FAILED");
                return;
            }

            if (AutoPlay.Active)
            {
                var r = CurrentRound;
                bool feature = r.FreeSpins != null || r.HoldAndSpin != null;
                if (!AutoPlay.OnRoundSettled(r.TotalWin, feature, Credits, TotalBet))
                    AutoPlayStopped?.Invoke(AutoPlay.LastStopReason);
            }
        }

        /// <summary>Credits must always equal start + added + won - wagered.</summary>
        public bool MetersReconcile() => Credits == StartingCredits + CreditsAdded + TotalWon - TotalWagered;

        // ---------------------------------------------------------------- auto-play

        public bool StartAutoPlay(AutoPlaySettings settings)
        {
            if (State != GameState.Idle || settings == null || settings.Rounds <= 0) return false;
            if (Credits < TotalBet) return false;
            AutoPlay.Start(settings, Credits);
            return true;
        }

        public void StopAutoPlay()
        {
            if (!AutoPlay.Active) return;
            AutoPlay.Stop(AutoPlayStopReason.PlayerStopped);
            AutoPlayStopped?.Invoke(AutoPlayStopReason.PlayerStopped);
        }

        /// <summary>True when auto-play wants the next round started (the presentation decides the pacing).</summary>
        public bool AutoPlayWantsSpin => AutoPlay.Active && State == GameState.Idle && Credits >= TotalBet;

        // ---------------------------------------------------------------- tilt

        /// <summary>Puts the cabinet into the Error/tilt state. The round in progress is frozen, never altered.</summary>
        public void Tilt(string message)
        {
            TiltMessage = string.IsNullOrEmpty(message) ? "TILT" : message;
            if (AutoPlay.Active)
            {
                AutoPlay.Stop(AutoPlayStopReason.Tilt);
                AutoPlayStopped?.Invoke(AutoPlayStopReason.Tilt);
            }
            if (State != GameState.Error) _machine.TransitionTo(GameState.Error);
        }

        /// <summary>Attendant reset: returns to the interrupted state so the frozen round resumes exactly where it was.</summary>
        public bool ClearTilt()
        {
            if (State != GameState.Error) return false;
            if (!MetersReconcile()) return false;
            TiltMessage = null;
            _machine.TransitionTo(_machine.ResumeState);
            return true;
        }
    }
}
