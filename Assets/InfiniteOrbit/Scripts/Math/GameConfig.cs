using System;
using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    public sealed class HoldAndSpinConfig
    {
        /// <summary>Orbs needed anywhere on a base-game spin to trigger the feature.</summary>
        public int TriggerCount { get; set; } = 6;

        /// <summary>Respins granted at the start and restored whenever a new orb lands.</summary>
        public int Respins { get; set; } = 3;

        /// <summary>
        /// During respins every empty cell spins its own single-position reel with these weights.
        /// P(orb lands in an empty cell) = OrbWeight / (OrbWeight + BlankWeight).
        /// </summary>
        public int OrbWeight { get; set; }
        public int BlankWeight { get; set; }

        /// <summary>Prize printed on each orb (base game and respins share the table).</summary>
        public WeightedTable<OrbPrize> Prizes { get; set; }

        /// <summary>Fixed jackpots in multiples of total bet.</summary>
        public int MiniMultiple { get; set; } = 10;
        public int MinorMultiple { get; set; } = 25;
        public int MajorMultiple { get; set; } = 100;
        public int GrandMultiple { get; set; } = 1000;

        /// <summary>Filling all 15 cells awards the Grand jackpot in addition to the orb prizes.</summary>
        public bool GrandOnFullScreen { get; set; } = true;

        public int JackpotMultiple(OrbPrizeKind kind)
        {
            switch (kind)
            {
                case OrbPrizeKind.Mini: return MiniMultiple;
                case OrbPrizeKind.Minor: return MinorMultiple;
                case OrbPrizeKind.Major: return MajorMultiple;
                case OrbPrizeKind.Grand: return GrandMultiple;
                default: return 0;
            }
        }
    }

    public sealed class FreeSpinsConfig
    {
        /// <summary>Spins awarded by scatter count (index = count). Same table for trigger and retrigger.</summary>
        public int[] Awards { get; set; } = { 0, 0, 0, 8, 12, 20 };

        public int StartMultiplier { get; set; } = 1;
        /// <summary>Added to the multiplier after every free spin that pays.</summary>
        public int MultiplierStep { get; set; } = 1;
        public int MaxMultiplier { get; set; } = 10;

        /// <summary>Engineering guard against unbounded retriggers. Reported in the PAR sheet; never expected to bind.</summary>
        public int MaxTotalSpins { get; set; } = 1000;
    }

    /// <summary>Win/bet thresholds for the cabinet celebration tiers.</summary>
    public sealed class BigWinConfig
    {
        public double Big { get; set; } = 15;
        public double Mega { get; set; } = 40;
        public double Epic { get; set; } = 100;

        public BigWinTier Classify(long win, long bet)
        {
            if (bet <= 0) return BigWinTier.None;
            double x = (double)win / bet;
            if (x >= Epic) return BigWinTier.Epic;
            if (x >= Mega) return BigWinTier.Mega;
            if (x >= Big) return BigWinTier.Big;
            return BigWinTier.None;
        }
    }

    /// <summary>
    /// Every number that defines the game's math. The engine, the simulator, the PAR generator and the in-game
    /// paytable all read from one instance of this class, so they can never disagree.
    /// </summary>
    public sealed class GameConfig
    {
        public const int Reels = 5;
        public const int Rows = 3;
        public const int Cells = Reels * Rows;

        public string GameName { get; set; } = "Infinite Orbit";
        public string Version { get; set; } = "1.0.0";

        public SymbolInfo[] Symbols { get; set; }
        public Payline[] Paylines { get; set; }
        public ReelSet BaseReels { get; set; }
        public ReelSet FreeSpinReels { get; set; }

        /// <summary>Scatter pays in multiples of total bet, indexed by scatter count.</summary>
        public int[] ScatterPays { get; set; } = { 0, 0, 0, 2, 10, 50 };

        public FreeSpinsConfig FreeSpins { get; set; } = new FreeSpinsConfig();
        public HoldAndSpinConfig HoldAndSpin { get; set; } = new HoldAndSpinConfig();
        public BigWinConfig BigWin { get; set; } = new BigWinConfig();

        /// <summary>Line bet options in credits. Total bet = line bet x number of lines.</summary>
        public long[] LineBets { get; set; } = { 1, 2, 4, 10, 20 };

        public int LineCount => Paylines.Length;
        public long TotalBetForLevel(int level) => LineBets[level] * LineCount;

        public SymbolInfo Info(Symbol s) => Symbols[(int)s];

        public void Validate()
        {
            if (Symbols == null || Symbols.Length != Enum.GetValues(typeof(Symbol)).Length)
                throw new InvalidOperationException("Symbol table must define every symbol.");
            for (int i = 0; i < Symbols.Length; i++)
                if ((int)Symbols[i].Id != i) throw new InvalidOperationException("Symbol table out of order.");
            if (Paylines == null || Paylines.Length == 0) throw new InvalidOperationException("No paylines.");
            if (BaseReels == null || FreeSpinReels == null) throw new InvalidOperationException("Missing reel sets.");
            if (ScatterPays.Length != Reels + 1 || FreeSpins.Awards.Length != Reels + 1)
                throw new InvalidOperationException("Scatter tables must be indexed 0..5.");
            if (HoldAndSpin.Prizes == null) throw new InvalidOperationException("Missing orb prize table.");
            for (int r = 0; r < Reels; r++)
                if (FreeSpinReels[r].CountOf(Symbol.Orb) != 0)
                    throw new InvalidOperationException("Free-spin reels must not carry orbs (hold-and-spin is base-game only).");
        }
    }
}
