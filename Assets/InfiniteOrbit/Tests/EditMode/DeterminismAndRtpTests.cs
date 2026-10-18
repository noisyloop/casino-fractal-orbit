using System;
using System.Linq;
using InfiniteOrbit.GameMath;
using NUnit.Framework;

namespace InfiniteOrbit.Tests
{
    public class DeterminismTests
    {
        private static string Fingerprint(RoundResult r)
        {
            string fs = r.FreeSpins == null ? "-" : string.Join("|", r.FreeSpins.Spins.Select(s => string.Join(",", s.Spin.Stops) + ":" + s.Spin.TotalCredits));
            string hs = r.HoldAndSpin == null ? "-" : string.Join("|", r.HoldAndSpin.Board.Select(o => o == null ? "." : o.Prize.ToString()));
            return $"{string.Join(",", r.BaseSpin.Stops)};{r.TotalWin};{fs};{hs}";
        }

        [Test]
        public void SameSeedReplaysIdentically()
        {
            var engine = new SlotEngine(InfiniteOrbitMath.CreateConfig());
            var a = new Xoshiro256StarStar(123456789);
            var b = new Xoshiro256StarStar(123456789);
            for (int i = 0; i < 20_000; i++)
                Assert.AreEqual(Fingerprint(engine.PlayRound(a, 2)), Fingerprint(engine.PlayRound(b, 2)), $"Round {i}");
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var engine = new SlotEngine(InfiniteOrbitMath.CreateConfig());
            var a = new Xoshiro256StarStar(1);
            var b = new Xoshiro256StarStar(2);
            int same = 0;
            for (int i = 0; i < 1000; i++)
                if (Fingerprint(engine.PlayRound(a, 0)) == Fingerprint(engine.PlayRound(b, 0))) same++;
            Assert.Less(same, 10);
        }

        [Test]
        public void KnownSeedProducesKnownFirstStops()
        {
            // Golden values: if this changes, the RNG or draw order changed and every replay/log is invalidated.
            var engine = new SlotEngine(InfiniteOrbitMath.CreateConfig());
            var round = engine.PlayRound(new Xoshiro256StarStar(42), 0);
            Assert.AreEqual(GoldenStops, string.Join(",", round.BaseSpin.Stops));
        }

        public const string GoldenStops = "5,25,45,61,62";

        [Test]
        public void ParallelAndSequentialSimulationAgree()
        {
            var config = InfiniteOrbitMath.CreateConfig();
            var par = Simulation.Run(config, 99, 300_000, 0, parallel: true);
            var seq = Simulation.Run(config, 99, 300_000, 0, parallel: false);
            Assert.AreEqual(seq.TotalWin, par.TotalWin);
            Assert.AreEqual(seq.HitRounds, par.HitRounds);
            Assert.AreEqual(seq.FreeSpinTriggers, par.FreeSpinTriggers);
            Assert.AreEqual(seq.MaxWinRoundIndex, par.MaxWinRoundIndex);
        }

        [Test]
        public void RngStaysInRangeAndIsRoughlyUniform()
        {
            var rng = new Xoshiro256StarStar(5);
            var counts = new int[7];
            const int n = 700_000;
            for (int i = 0; i < n; i++) counts[rng.NextInt(7)]++;
            foreach (int c in counts) Assert.AreEqual(n / 7.0, c, n / 7.0 * 0.02);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        }
    }

    public class ConfigTests
    {
        [Test]
        public void StripsMatchDeclaredCounts()
        {
            var config = InfiniteOrbitMath.CreateConfig();
            for (int r = 0; r < 5; r++)
                for (int s = 0; s < InfiniteOrbitMath.StripSymbols.Length; s++)
                {
                    var sym = InfiniteOrbitMath.StripSymbols[s];
                    Assert.AreEqual(InfiniteOrbitMath.BaseCounts[r][s], config.BaseReels[r].CountOf(sym), $"base reel {r + 1} {sym}");
                    Assert.AreEqual(InfiniteOrbitMath.FreeSpinCounts[r][s], config.FreeSpinReels[r].CountOf(sym), $"fs reel {r + 1} {sym}");
                }
        }

        [Test]
        public void StripLayoutIsStable()
        {
            var a = InfiniteOrbitMath.CreateConfig();
            var b = InfiniteOrbitMath.CreateConfig();
            for (int r = 0; r < 5; r++)
            {
                CollectionAssert.AreEqual(a.BaseReels[r].Symbols, b.BaseReels[r].Symbols);
                CollectionAssert.AreEqual(a.FreeSpinReels[r].Symbols, b.FreeSpinReels[r].Symbols);
            }
        }

        [Test]
        public void SymbolSetMatchesDesign()
        {
            var config = InfiniteOrbitMath.CreateConfig();
            Assert.AreEqual(5, config.Symbols.Count(s => s.Kind == SymbolKind.Low));
            Assert.AreEqual(4, config.Symbols.Count(s => s.Kind == SymbolKind.High));
            Assert.AreEqual(1, config.Symbols.Count(s => s.Kind == SymbolKind.Wild));
            Assert.AreEqual(1, config.Symbols.Count(s => s.Kind == SymbolKind.Scatter));
            Assert.AreEqual(1, config.Symbols.Count(s => s.Kind == SymbolKind.Bonus));
            CollectionAssert.AreEqual(new long[] { 25, 50, 100, 250, 500 }, Enumerable.Range(0, 5).Select(config.TotalBetForLevel));
        }
    }

    public class RtpTests
    {
        public const double MinRtp = 0.94;
        public const double MaxRtp = 0.96;
        public const ulong Seed = 20261018UL;

        [Test]
        public void ExactBaseGameComponentsMatchSimulation()
        {
            var config = InfiniteOrbitMath.CreateConfig();
            var stats = Simulation.Run(config, Seed, 2_000_000);
            double exactLines = Analytics.LineRtp(config, config.BaseReels);
            double exactScatter = Analytics.ScatterRtp(config, config.BaseReels);
            double fsTrigger = Analytics.ProbabilityAtLeast(Analytics.ScreenCountDistribution(config.BaseReels, Symbol.Scatter), 3);
            double hsTrigger = Analytics.ProbabilityAtLeast(Analytics.ScreenCountDistribution(config.BaseReels, Symbol.Orb), 6);

            Assert.AreEqual(exactLines, stats.RtpOf(stats.BaseLineWin), 0.005, "line RTP");
            Assert.AreEqual(exactScatter, stats.RtpOf(stats.BaseScatterWin), 0.002, "scatter RTP");
            Assert.AreEqual(fsTrigger, (double)stats.FreeSpinTriggers / stats.Rounds, fsTrigger * 0.05, "free spins trigger rate");
            Assert.AreEqual(hsTrigger, (double)stats.HoldAndSpinTriggers / stats.Rounds, hsTrigger * 0.05, "hold-and-spin trigger rate");
        }

        [Test]
        public void SimulatedRtpIsWithinTargetBand()
        {
            var stats = Simulation.Run(InfiniteOrbitMath.CreateConfig(), Seed, 5_000_000);
            TestContext.Progress.WriteLine($"RTP {stats.Rtp:P3} ± {stats.Rtp95HalfWidth:P3} over {stats.Rounds:N0} rounds");
            Assert.That(stats.Rtp, Is.InRange(MinRtp, MaxRtp));
            Assert.Less(stats.Rtp95HalfWidth, 0.01, "Sample too small to judge RTP.");
        }
    }
}
