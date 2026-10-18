using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteOrbit.Core;
using InfiniteOrbit.GameMath;
using NUnit.Framework;

namespace InfiniteOrbit.Tests
{
    public class StateMachineTests
    {
        [Test]
        public void StartsIdle()
        {
            Assert.AreEqual(GameState.Idle, new GameStateMachine().Current);
        }

        [Test]
        public void IllegalTransitionsThrow()
        {
            var m = new GameStateMachine();
            Assert.Throws<InvalidOperationException>(() => m.TransitionTo(GameState.FreeSpins));
            Assert.Throws<InvalidOperationException>(() => m.TransitionTo(GameState.WinPresentation));
            m.TransitionTo(GameState.Spinning);
            Assert.Throws<InvalidOperationException>(() => m.TransitionTo(GameState.Spinning));
        }

        [Test]
        public void AnyStateCanTiltAndOnlyResumesWhereItWas()
        {
            var m = new GameStateMachine();
            m.TransitionTo(GameState.Spinning);
            m.TransitionTo(GameState.BonusIntro);
            m.TransitionTo(GameState.Error);
            Assert.AreEqual(GameState.BonusIntro, m.ResumeState);
            Assert.IsFalse(m.CanTransition(GameState.Idle));
            Assert.IsFalse(m.CanTransition(GameState.Error));
            m.TransitionTo(GameState.BonusIntro);
            Assert.AreEqual(GameState.BonusIntro, m.Current);
        }

        [Test]
        public void ChangedEventReportsFromAndTo()
        {
            var m = new GameStateMachine();
            var seen = new List<string>();
            m.Changed += (a, b) => seen.Add($"{a}>{b}");
            m.TransitionTo(GameState.Spinning);
            m.TransitionTo(GameState.Idle);
            CollectionAssert.AreEqual(new[] { "Idle>Spinning", "Spinning>Idle" }, seen);
        }
    }

    public class SessionTests
    {
        private static readonly GameConfig Config = InfiniteOrbitMath.CreateConfig();
        private static readonly DateTime FixedTime = new DateTime(2026, 1, 1, 12, 0, 0);

        /// <summary>Finds a seed whose first round at the given bet level satisfies the predicate.</summary>
        private static ulong FindSeed(Func<RoundResult, bool> predicate, int betLevel = 0)
        {
            var engine = new SlotEngine(Config);
            for (ulong seed = 1; seed < 2_000_000; seed++)
                if (predicate(engine.PlayRound(new Xoshiro256StarStar(seed), betLevel))) return seed;
            throw new InvalidOperationException("No seed found.");
        }

        private static GameSession NewSession(ulong seed, long credits = 10_000) =>
            new GameSession(Config, new Xoshiro256StarStar(seed), credits, () => FixedTime);

        /// <summary>Plays the current round through every phase, returning the visited states.</summary>
        private static List<GameState> PlayThrough(GameSession s)
        {
            var visited = new List<GameState> { s.State };
            int guard = 0;
            while (s.State != GameState.Idle && guard++ < 20)
            {
                s.Advance();
                visited.Add(s.State);
            }
            return visited;
        }

        [Test]
        public void LosingRoundGoesSpinningToIdleAndDebitsBet()
        {
            var s = NewSession(FindSeed(r => r.TotalWin == 0));
            Assert.AreEqual(SpinRejection.None, s.RequestSpin());
            Assert.AreEqual(10_000 - 25, s.Credits, "Bet is debited at spin start.");
            CollectionAssert.AreEqual(new[] { GameState.Spinning, GameState.Idle }, PlayThrough(s));
            Assert.AreEqual(10_000 - 25, s.Credits);
            Assert.AreEqual(0, s.WinMeter);
        }

        [Test]
        public void SmallWinShowsWinPresentationThenCredits()
        {
            var s = NewSession(FindSeed(r => r.TotalWin > 0 && r.BigWinTier == BigWinTier.None && r.FreeSpins == null && r.HoldAndSpin == null));
            s.RequestSpin();
            long win = s.CurrentRound.TotalWin;
            Assert.AreEqual(10_000 - 25, s.Credits, "Win is not credited before the presentation finishes.");
            CollectionAssert.AreEqual(new[] { GameState.Spinning, GameState.WinPresentation, GameState.Idle }, PlayThrough(s));
            Assert.AreEqual(10_000 - 25 + win, s.Credits);
            Assert.AreEqual(win, s.WinMeter);
        }

        [Test]
        public void FreeSpinsRoundVisitsBonusIntroAndFreeSpins()
        {
            var s = NewSession(FindSeed(r => r.FreeSpins != null && r.HoldAndSpin == null));
            s.RequestSpin();
            s.Advance();
            Assert.AreEqual(GameState.BonusIntro, s.State);
            Assert.AreEqual(FeatureKind.FreeSpins, s.ActiveFeature);
            s.Advance();
            Assert.AreEqual(GameState.FreeSpins, s.State);
            var rest = PlayThrough(s);
            Assert.AreEqual(GameState.Idle, rest.Last());
            Assert.IsTrue(rest.Contains(GameState.WinPresentation));
        }

        [Test]
        public void HoldAndSpinRoundVisitsBonusIntroAndHoldAndSpin()
        {
            var s = NewSession(FindSeed(r => r.HoldAndSpin != null && r.FreeSpins == null));
            s.RequestSpin();
            var visited = PlayThrough(s);
            int intro = visited.IndexOf(GameState.BonusIntro);
            Assert.GreaterOrEqual(intro, 0);
            Assert.AreEqual(GameState.HoldAndSpin, visited[intro + 1]);
        }

        [Test]
        public void BigWinPhasePrecedesWinPresentation()
        {
            var s = NewSession(FindSeed(r => r.BigWinTier != BigWinTier.None));
            s.RequestSpin();
            var visited = PlayThrough(s);
            int big = visited.IndexOf(GameState.BigWin);
            Assert.Greater(big, 0);
            Assert.AreEqual(GameState.WinPresentation, visited[big + 1]);
        }

        [Test]
        public void SpinRejectedWhenBusyOrBroke()
        {
            var s = NewSession(1, credits: 30);
            Assert.AreEqual(SpinRejection.None, s.RequestSpin());
            Assert.AreEqual(SpinRejection.NotIdle, s.RequestSpin());
            PlayThrough(s);
            if (s.Credits < 25) Assert.AreEqual(SpinRejection.InsufficientCredits, s.RequestSpin());

            var broke = NewSession(1, credits: 10);
            Assert.AreEqual(SpinRejection.InsufficientCredits, broke.RequestSpin());
            Assert.AreEqual(GameState.Idle, broke.State);
        }

        [Test]
        public void BetLevelsAndMaxBet()
        {
            var s = NewSession(FindSeed(r => r.TotalWin == 0, betLevel: 4));
            Assert.AreEqual(25, s.TotalBet);
            Assert.IsTrue(s.BetUp());
            Assert.AreEqual(50, s.TotalBet);
            Assert.IsTrue(s.BetDown());
            Assert.IsFalse(s.BetDown(), "Cannot go below level 0.");
            Assert.AreEqual(SpinRejection.None, s.MaxBetSpin());
            Assert.AreEqual(500, s.TotalBet);
            Assert.AreEqual(10_000 - 500, s.Credits);
            Assert.IsFalse(s.BetDown(), "Bet is locked while a round is in play.");
        }

        [Test]
        public void HistoryKeepsLast100RoundsNewestFirst()
        {
            var s = NewSession(77, credits: 1_000_000);
            for (int i = 0; i < 130; i++)
            {
                Assert.AreEqual(SpinRejection.None, s.RequestSpin());
                PlayThrough(s);
            }
            Assert.AreEqual(100, s.History.Count);
            Assert.AreEqual(130, s.History[0].RoundNumber);
            Assert.AreEqual(31, s.History[99].RoundNumber);
            CollectionAssert.AreEqual(Enumerable.Range(31, 100).Reverse().Select(i => (long)i), s.History.Select(h => h.RoundNumber));

            var last = s.History[0];
            Assert.AreEqual(25, last.TotalBet);
            Assert.AreEqual(5, last.BaseStops.Length);
            Assert.AreEqual(last.CreditsBefore - 25 + last.TotalWin, last.CreditsAfter);
            Assert.AreEqual(FixedTime, last.Time);
            Assert.AreSame(s.CurrentRound, last.Result);
        }

        [Test]
        public void HistoryRecordsFeatureState()
        {
            var s = NewSession(FindSeed(r => r.FreeSpins != null && r.HoldAndSpin == null));
            s.RequestSpin();
            PlayThrough(s);
            var e = s.History[0];
            Assert.Greater(e.FreeSpinsPlayed, 0);
            Assert.Greater(e.FreeSpinFinalMultiplier, 0);
            StringAssert.Contains("FS", e.FeatureSummary);
        }

        [Test]
        public void MetersReconcileOverManyRounds()
        {
            var s = NewSession(5, credits: 100_000);
            s.AddCredits(5_000);
            for (int i = 0; i < 2000 && s.CanSpin; i++)
            {
                s.RequestSpin();
                PlayThrough(s);
                Assert.IsTrue(s.MetersReconcile());
            }
            Assert.AreEqual(s.StartingCredits + s.CreditsAdded + s.TotalWon - s.TotalWagered, s.Credits);
        }

        [Test]
        public void TiltFreezesRoundAndClearResumesIt()
        {
            var s = NewSession(FindSeed(r => r.FreeSpins != null));
            s.RequestSpin();
            var round = s.CurrentRound;
            s.Advance(); // into a feature phase
            var before = s.State;

            s.Tilt("DOOR OPEN");
            Assert.AreEqual(GameState.Error, s.State);
            Assert.AreEqual("DOOR OPEN", s.TiltMessage);
            s.Advance();
            Assert.AreEqual(GameState.Error, s.State, "Advance is ignored while tilted.");
            Assert.AreEqual(SpinRejection.NotIdle, s.RequestSpin());

            Assert.IsTrue(s.ClearTilt());
            Assert.AreEqual(before, s.State);
            Assert.AreSame(round, s.CurrentRound, "The outcome is never re-drawn after a tilt.");
            PlayThrough(s);
            Assert.AreEqual(round.TotalWin, s.WinMeter);
        }

        [Test]
        public void AutoPlayRunsRequestedRoundsThenStops()
        {
            var s = NewSession(9, credits: 1_000_000);
            var stops = new List<AutoPlayStopReason>();
            s.AutoPlayStopped += r => stops.Add(r);
            Assert.IsTrue(s.StartAutoPlay(new AutoPlaySettings { Rounds = 10, StopOnFeature = false }));
            int rounds = 0;
            while (s.AutoPlayWantsSpin && rounds < 100)
            {
                s.RequestSpin();
                PlayThrough(s);
                rounds++;
            }
            Assert.AreEqual(10, rounds);
            CollectionAssert.AreEqual(new[] { AutoPlayStopReason.Completed }, stops);
            Assert.IsTrue(s.History.Take(10).All(h => h.AutoPlay));
        }

        [Test]
        public void AutoPlayStopsOnFeature()
        {
            var a = new AutoPlay();
            a.Start(new AutoPlaySettings { Rounds = 50, StopOnFeature = true }, 1000);
            Assert.IsTrue(a.OnRoundSettled(0, false, 975, 25));
            Assert.IsFalse(a.OnRoundSettled(400, true, 1350, 25));
            Assert.AreEqual(AutoPlayStopReason.FeatureTriggered, a.LastStopReason);
        }

        [TestCase(0L, 0L, 100L, 0L, 900L, AutoPlayStopReason.LossLimit)]
        [TestCase(500L, 0L, 0L, 600L, 1575L, AutoPlayStopReason.SingleWinLimit)]
        [TestCase(0L, 300L, 0L, 400L, 1375L, AutoPlayStopReason.BalanceIncreaseLimit)]
        [TestCase(0L, 0L, 0L, 0L, 10L, AutoPlayStopReason.InsufficientCredits)]
        public void AutoPlayStopConditions(long singleWin, long increase, long loss, long roundWin, long creditsAfter, AutoPlayStopReason expected)
        {
            var a = new AutoPlay();
            a.Start(new AutoPlaySettings { Rounds = 50, StopOnFeature = false, StopOnSingleWinAtLeast = singleWin, StopOnBalanceIncreaseOf = increase, LossLimit = loss }, 1000);
            Assert.IsFalse(a.OnRoundSettled(roundWin, false, creditsAfter, 25));
            Assert.AreEqual(expected, a.LastStopReason);
        }

        [Test]
        public void TiltStopsAutoPlayAndBetIsLockedDuringAutoPlay()
        {
            var s = NewSession(3, credits: 100_000);
            s.StartAutoPlay(new AutoPlaySettings { Rounds = 5 });
            Assert.IsFalse(s.BetUp());
            s.RequestSpin();
            s.Tilt("TEST");
            Assert.IsFalse(s.AutoPlay.Active);
            Assert.AreEqual(AutoPlayStopReason.Tilt, s.AutoPlay.LastStopReason);
        }

        [Test]
        public void SessionOutcomesMatchEngineForSameSeed()
        {
            // The session adds no randomness of its own: it replays the engine's sequence exactly.
            var engine = new SlotEngine(Config);
            var rng = new Xoshiro256StarStar(31337);
            var s = NewSession(31337, credits: 1_000_000);
            for (int i = 0; i < 300; i++)
            {
                var expected = engine.PlayRound(rng, 0);
                s.RequestSpin();
                Assert.AreEqual(string.Join(",", expected.BaseSpin.Stops), string.Join(",", s.CurrentRound.BaseSpin.Stops));
                Assert.AreEqual(expected.TotalWin, s.CurrentRound.TotalWin);
                PlayThrough(s);
            }
        }
    }
}
