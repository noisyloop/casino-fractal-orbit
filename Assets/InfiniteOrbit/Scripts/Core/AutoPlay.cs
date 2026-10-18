namespace InfiniteOrbit.Core
{
    public enum AutoPlayStopReason
    {
        None,
        Completed,
        PlayerStopped,
        FeatureTriggered,
        SingleWinLimit,
        LossLimit,
        BalanceIncreaseLimit,
        InsufficientCredits,
        Tilt,
        BetChanged,
    }

    /// <summary>Auto-play request with its stop conditions. A limit of 0 disables that condition.</summary>
    public sealed class AutoPlaySettings
    {
        public int Rounds { get; set; } = 25;
        public bool StopOnFeature { get; set; } = true;
        /// <summary>Stop after any single round paying at least this many credits.</summary>
        public long StopOnSingleWinAtLeast { get; set; }
        /// <summary>Stop once net loss since auto-play started reaches this many credits.</summary>
        public long LossLimit { get; set; }
        /// <summary>Stop once the balance has grown by at least this many credits since auto-play started.</summary>
        public long StopOnBalanceIncreaseOf { get; set; }
    }

    /// <summary>Tracks one auto-play session. Pure bookkeeping: the session decides when to call it.</summary>
    public sealed class AutoPlay
    {
        public AutoPlaySettings Settings { get; private set; }
        public bool Active { get; private set; }
        public int RoundsRemaining { get; private set; }
        public int RoundsPlayed { get; private set; }
        public long StartCredits { get; private set; }
        public AutoPlayStopReason LastStopReason { get; private set; }

        public void Start(AutoPlaySettings settings, long credits)
        {
            Settings = settings;
            Active = settings.Rounds > 0;
            RoundsRemaining = settings.Rounds;
            RoundsPlayed = 0;
            StartCredits = credits;
            LastStopReason = AutoPlayStopReason.None;
        }

        public void Stop(AutoPlayStopReason reason)
        {
            if (!Active) return;
            Active = false;
            LastStopReason = reason;
        }

        /// <summary>Called after a round settles. Returns true if auto-play should continue.</summary>
        public bool OnRoundSettled(long roundWin, bool featureTriggered, long creditsNow, long nextBet)
        {
            if (!Active) return false;
            RoundsPlayed++;
            RoundsRemaining--;

            var s = Settings;
            if (s.StopOnFeature && featureTriggered) Stop(AutoPlayStopReason.FeatureTriggered);
            else if (s.StopOnSingleWinAtLeast > 0 && roundWin >= s.StopOnSingleWinAtLeast) Stop(AutoPlayStopReason.SingleWinLimit);
            else if (s.LossLimit > 0 && StartCredits - creditsNow >= s.LossLimit) Stop(AutoPlayStopReason.LossLimit);
            else if (s.StopOnBalanceIncreaseOf > 0 && creditsNow - StartCredits >= s.StopOnBalanceIncreaseOf) Stop(AutoPlayStopReason.BalanceIncreaseLimit);
            else if (RoundsRemaining <= 0) Stop(AutoPlayStopReason.Completed);
            else if (creditsNow < nextBet) Stop(AutoPlayStopReason.InsufficientCredits);
            return Active;
        }
    }
}
