using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>
    /// The certified math definition for "Infinite Orbit". Change numbers here only; everything else
    /// (engine, simulator, PAR sheet, in-game help) derives from <see cref="CreateConfig"/>.
    /// </summary>
    public static class InfiniteOrbitMath
    {
        public const string MathVersion = "IO-1.0";

        /// <summary>Seed for the deterministic reel-strip layout. Changing it changes the strips and therefore the math.</summary>
        public const ulong StripLayoutSeed = 0x1F0_0B17UL;

        /// <summary>Orbs are stacked in blocks of this size on base reels.</summary>
        public const int OrbStackSize = 3;

        // Symbol order for the count tables below.
        public static readonly Symbol[] StripSymbols =
        {
            Symbol.Ten, Symbol.Jack, Symbol.Queen, Symbol.King, Symbol.Ace,
            Symbol.Comet, Symbol.Nebula, Symbol.Ringworld, Symbol.Pulsar,
            Symbol.Wild, Symbol.Scatter, Symbol.Orb,
        };

        /// <summary>Base game symbol counts per reel: [reel][symbol in <see cref="StripSymbols"/> order].</summary>
        public static readonly int[][] BaseCounts =
        {
            //          10   J   Q   K   A  Com Neb Rng Pul  W  Sc  Orb
            new[] {      9,  9,  8,  8,  7,  5,  4,  4,  3,  0,  2,  4 },
            new[] {      9,  9,  8,  8,  7,  5,  4,  4,  3,  4,  2,  4 },
            new[] {      9,  9,  8,  8,  7,  5,  4,  4,  3,  4,  2,  4 },
            new[] {      9,  9,  8,  8,  7,  5,  4,  4,  3,  4,  2,  4 },
            new[] {      9,  9,  8,  8,  7,  5,  4,  4,  3,  0,  2,  4 },
        };

        /// <summary>Free spin symbol counts per reel. No orbs: hold-and-spin is a base-game feature.</summary>
        public static readonly int[][] FreeSpinCounts =
        {
            //          10   J   Q   K   A  Com Neb Rng Pul  W  Sc  Orb
            new[] {      7,  7,  7,  6,  6,  5,  5,  4,  4,  0,  2,  0 },
            new[] {      7,  7,  7,  6,  6,  5,  5,  4,  4,  8,  2,  0 },
            new[] {      7,  7,  7,  6,  6,  5,  5,  4,  4,  8,  2,  0 },
            new[] {      7,  7,  7,  6,  6,  5,  5,  4,  4,  8,  2,  0 },
            new[] {      7,  7,  7,  6,  6,  5,  5,  4,  4,  0,  2,  0 },
        };

        public static SymbolInfo[] CreateSymbols()
        {
            return new[]
            {
                new SymbolInfo(Symbol.Ten,       "10", "Ten",       SymbolKind.Low,   new[] { 0, 0, 0,  5, 20,  75 }, "Crystalline ten glyph."),
                new SymbolInfo(Symbol.Jack,      "J",  "Jack",      SymbolKind.Low,   new[] { 0, 0, 0,  5, 20,  75 }, "Crystalline jack glyph."),
                new SymbolInfo(Symbol.Queen,     "Q",  "Queen",     SymbolKind.Low,   new[] { 0, 0, 0,  5, 25, 100 }, "Crystalline queen glyph."),
                new SymbolInfo(Symbol.King,      "K",  "King",      SymbolKind.Low,   new[] { 0, 0, 0, 10, 30, 125 }, "Crystalline king glyph."),
                new SymbolInfo(Symbol.Ace,       "A",  "Ace",       SymbolKind.Low,   new[] { 0, 0, 0, 10, 40, 150 }, "Crystalline ace glyph."),
                new SymbolInfo(Symbol.Comet,     "COM", "Comet",     SymbolKind.High,  new[] { 0, 0, 0, 25, 100, 400 }, "A comet trailing fractal dust."),
                new SymbolInfo(Symbol.Nebula,    "NEB", "Nebula",    SymbolKind.High,  new[] { 0, 0, 0, 30, 125, 500 }, "A spiral nebula."),
                new SymbolInfo(Symbol.Ringworld, "RNG", "Ringworld", SymbolKind.High,  new[] { 0, 0, 0, 40, 200, 750 }, "A ringed world."),
                new SymbolInfo(Symbol.Pulsar,    "PUL", "Pulsar",    SymbolKind.High,  new[] { 0, 0, 0, 75, 300, 2000 }, "A pulsar beam: the top symbol."),
                new SymbolInfo(Symbol.Wild,      "W",  "Singularity", SymbolKind.Wild, new[] { 0, 0, 0, 0, 0, 0 }, "Wild. Appears on reels 2, 3 and 4. Substitutes for all symbols except Fractal Gate and Energy Orb."),
                new SymbolInfo(Symbol.Scatter,   "SC", "Fractal Gate", SymbolKind.Scatter, new[] { 0, 0, 0, 0, 0, 0 }, "Scatter. Pays anywhere. 3, 4 or 5 trigger free spins."),
                new SymbolInfo(Symbol.Orb,       "ORB", "Energy Orb", SymbolKind.Bonus, new[] { 0, 0, 0, 0, 0, 0 }, "Carries a credit prize or a jackpot. 6 or more trigger Orbit Lock respins."),
            };
        }

        public static WeightedTable<OrbPrize> CreateOrbPrizes()
        {
            // Credit prizes are in line-bet multiples (25 line bets = 1x total bet).
            return new WeightedTable<OrbPrize>(new List<(OrbPrize, int)>
            {
                (OrbPrize.Credits(25), 200),
                (OrbPrize.Credits(50), 250),
                (OrbPrize.Credits(75), 150),
                (OrbPrize.Credits(125), 80),
                (OrbPrize.Credits(250), 50),
                (OrbPrize.Credits(500), 16),
                (OrbPrize.Jackpot(OrbPrizeKind.Mini), 26),
                (OrbPrize.Jackpot(OrbPrizeKind.Minor), 6),
                (OrbPrize.Jackpot(OrbPrizeKind.Major), 1),
            });
        }

        public static ReelSet BuildReelSet(string name, int[][] counts, ulong seedOffset, int orbStack)
        {
            var strips = new ReelStrip[GameConfig.Reels];
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                var entries = new List<(Symbol, int)>();
                for (int s = 0; s < StripSymbols.Length; s++)
                    if (counts[r][s] > 0) entries.Add((StripSymbols[s], counts[r][s]));
                strips[r] = ReelStripBuilder.Build(entries, StripLayoutSeed + seedOffset + (ulong)r, 3, orbStack);
            }
            return new ReelSet(name, strips);
        }

        public static GameConfig CreateConfig()
        {
            var config = new GameConfig
            {
                Version = MathVersion,
                Symbols = CreateSymbols(),
                Paylines = StandardPaylines.Create(),
                BaseReels = BuildReelSet("Base", BaseCounts, 0, OrbStackSize),
                FreeSpinReels = BuildReelSet("Free Spins", FreeSpinCounts, 100, 1),
                ScatterPays = new[] { 0, 0, 0, 2, 10, 50 },
                FreeSpins = new FreeSpinsConfig
                {
                    Awards = new[] { 0, 0, 0, 8, 12, 20 },
                    StartMultiplier = 1,
                    MultiplierStep = 1,
                    MaxMultiplier = 10,
                    MaxTotalSpins = 1000,
                },
                HoldAndSpin = new HoldAndSpinConfig
                {
                    TriggerCount = 6,
                    Respins = 3,
                    OrbWeight = 1,
                    BlankWeight = 20,
                    Prizes = CreateOrbPrizes(),
                    MiniMultiple = 10,
                    MinorMultiple = 25,
                    MajorMultiple = 100,
                    GrandMultiple = 1000,
                    GrandOnFullScreen = true,
                },
                LineBets = new long[] { 1, 2, 4, 10, 20 },
            };
            config.Validate();
            return config;
        }
    }
}
