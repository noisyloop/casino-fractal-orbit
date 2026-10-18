using System.Linq;
using InfiniteOrbit.GameMath;
using NUnit.Framework;
using static InfiniteOrbit.Tests.TestUtil;

namespace InfiniteOrbit.Tests
{
    public class WildScatterTests
    {
        private GameConfig _config;
        private WinEvaluator _eval;

        [SetUp]
        public void SetUp()
        {
            _config = Config();
            _eval = new WinEvaluator(_config);
        }

        private int Pay(params Symbol[] line) => _eval.EvaluateSymbols(line, out _, out _);

        [Test]
        public void WildSubstitutesForPayingSymbols()
        {
            int pay = _eval.EvaluateSymbols(new[] { A, W, A, Q, K }, out var sym, out int count);
            Assert.AreEqual(A, sym);
            Assert.AreEqual(3, count);
            Assert.AreEqual(_config.Info(A).Pays[3], pay);
        }

        [Test]
        public void WildsBridgeToFiveOfAKind()
        {
            Assert.AreEqual(_config.Info(P).Pays[5], Pay(P, W, W, W, P));
        }

        [Test]
        public void LeadingWildsTakeTheFirstPayingSymbol()
        {
            _eval.EvaluateSymbols(new[] { W, W, N, N, T }, out var sym, out int count);
            Assert.AreEqual(N, sym);
            Assert.AreEqual(4, count);
        }

        [Test]
        public void WildDoesNotSubstituteForScatter()
        {
            Assert.AreEqual(0, Pay(S, W, S, S, S), "A scatter on reel 1 never starts a line win.");
            Assert.AreEqual(0, Pay(W, W, S, S, S), "Wilds next to scatters form no line win.");
        }

        [Test]
        public void WildDoesNotSubstituteForOrb()
        {
            Assert.AreEqual(0, Pay(O, W, O, O, O));
            Assert.AreEqual(0, Pay(A, O, W, A, A), "Orb interrupts the run after one Ace.");
        }

        [Test]
        public void OrbAndScatterStopARun()
        {
            Assert.AreEqual(_config.Info(K).Pays[3], Pay(K, W, K, S, K));
            Assert.AreEqual(_config.Info(K).Pays[4], Pay(K, K, W, K, O));
        }

        [Test]
        public void WildOnlyOnReelsTwoToFourInBothReelSets()
        {
            foreach (var set in new[] { _config.BaseReels, _config.FreeSpinReels })
            {
                Assert.AreEqual(0, set[0].CountOf(W), $"{set.Name} reel 1");
                Assert.AreEqual(0, set[4].CountOf(W), $"{set.Name} reel 5");
                for (int r = 1; r <= 3; r++) Assert.Greater(set[r].CountOf(W), 0, $"{set.Name} reel {r + 1}");
            }
        }

        [TestCase(2, 0)]
        [TestCase(3, 2)]
        [TestCase(4, 10)]
        [TestCase(5, 50)]
        public void ScattersPayAnywhereInMultiplesOfTotalBet(int scatters, int multiple)
        {
            var rows = Filler();
            // Scatter in a different row on each reel: position doesn't matter.
            for (int r = 0; r < scatters; r++) rows[r % 3, r] = S;
            long pay = _eval.EvaluateScatter(Grid.FromRows(rows), 50, null, out int count);
            Assert.AreEqual(scatters, count);
            Assert.AreEqual(multiple * 50, pay);
        }

        [TestCase(2, 0)]
        [TestCase(3, 8)]
        [TestCase(4, 12)]
        [TestCase(5, 20)]
        public void ScattersAwardFreeSpins(int scatters, int spins)
        {
            var rows = Filler();
            for (int r = 0; r < scatters; r++) rows[2 - (r % 3), 4 - r] = S;
            var engine = new SlotEngine(_config);
            var spin = engine.Evaluate(new ScriptedRng(new int[0]), SpinMode.Base, ReelsShowing(rows), Zeros(5), 1, 25, 1);
            Assert.AreEqual(scatters, spin.ScatterCount);
            Assert.AreEqual(spins, spin.FreeSpinsAwarded);
            Assert.AreEqual(scatters >= 3, spin.TriggersFreeSpins);
            CollectionAssert.AreEquivalent(spin.ScatterCells, Enumerable.Range(0, 15).Where(c => spin.Grid[c] == S));
        }

        [Test]
        public void ScatterAndLineWinsAddUp()
        {
            var rows = Filler();
            rows[1, 0] = P; rows[1, 1] = P; rows[1, 2] = P; rows[1, 3] = T;
            rows[0, 0] = S; rows[0, 2] = S; rows[2, 4] = S;
            var engine = new SlotEngine(_config);
            var spin = engine.Evaluate(new ScriptedRng(new int[0]), SpinMode.Base, ReelsShowing(rows), Zeros(5), 2, 50, 1);
            Assert.AreEqual(_config.Info(P).Pays[3] * 2, spin.LineCredits);
            Assert.AreEqual(2 * 50, spin.ScatterCredits);
            Assert.AreEqual(spin.LineCredits + spin.ScatterCredits, spin.TotalCredits);
        }

        [Test]
        public void BaseStripsShowAtMostOneScatterPerReel()
        {
            foreach (var set in new[] { _config.BaseReels, _config.FreeSpinReels })
                for (int r = 0; r < 5; r++)
                    for (int stop = 0; stop < set[r].Length; stop++)
                    {
                        int n = 0;
                        for (int row = 0; row < 3; row++) if (set[r][stop + row] == S) n++;
                        Assert.LessOrEqual(n, 1, $"{set.Name} reel {r + 1} stop {stop}");
                    }
        }
    }
}
