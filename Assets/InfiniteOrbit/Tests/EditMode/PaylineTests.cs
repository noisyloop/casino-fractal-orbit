using System.Collections.Generic;
using System.Linq;
using InfiniteOrbit.GameMath;
using NUnit.Framework;
using static InfiniteOrbit.Tests.TestUtil;

namespace InfiniteOrbit.Tests
{
    public class PaylineTests
    {
        private GameConfig _config;
        private WinEvaluator _eval;

        [SetUp]
        public void SetUp()
        {
            _config = Config();
            _eval = new WinEvaluator(_config);
        }

        [Test]
        public void TwentyFiveUniqueLinesWithValidRows()
        {
            Assert.AreEqual(25, _config.Paylines.Length);
            var keys = _config.Paylines.Select(l => string.Join(",", l.Rows)).ToList();
            Assert.AreEqual(25, keys.Distinct().Count(), "Paylines must be unique.");
            foreach (var line in _config.Paylines)
            {
                Assert.AreEqual(5, line.Rows.Count);
                Assert.That(line.Rows, Is.All.InRange(0, 2));
            }
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1 }, _config.Paylines[0].Rows, "Line 1 is the middle row.");
        }

        [Test]
        public void FilterScreenHasNoWins()
        {
            var grid = Grid.FromRows(Filler());
            Assert.AreEqual(0, _eval.EvaluateLines(grid, 1, null));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void NOfAKindOnMiddleLinePaysTable(int count)
        {
            var rows = Filler();
            for (int r = 0; r < count; r++) rows[1, r] = P;
            if (count < 5) rows[1, count] = T; // break the run
            var win = _eval.EvaluateLine(Grid.FromRows(rows), _config.Paylines[0], 1);
            Assert.IsNotNull(win);
            Assert.AreEqual(P, win.Symbol);
            Assert.AreEqual(count, win.Count);
            Assert.AreEqual(_config.Info(P).Pays[count], win.Credits);
            CollectionAssert.AreEqual(Enumerable.Range(0, count).Select(r => Grid.CellIndex(r, 1)), win.Cells);
        }

        [Test]
        public void TwoOfAKindDoesNotPay()
        {
            var rows = Filler();
            rows[1, 0] = P; rows[1, 1] = P; rows[1, 2] = T;
            Assert.IsNull(_eval.EvaluateLine(Grid.FromRows(rows), _config.Paylines[0], 1));
        }

        [Test]
        public void WinsMustStartOnReelOne()
        {
            var rows = Filler();
            rows[1, 0] = K;
            for (int r = 1; r < 5; r++) rows[1, r] = P;
            Assert.IsNull(_eval.EvaluateLine(Grid.FromRows(rows), _config.Paylines[0], 1), "Right-aligned Pulsars must not pay.");
        }

        [Test]
        public void LinePayScalesWithLineBet()
        {
            var rows = Filler();
            for (int r = 0; r < 3; r++) rows[1, r] = A;
            var grid = Grid.FromRows(rows);
            long one = _eval.EvaluateLine(grid, _config.Paylines[0], 1).Credits;
            long twenty = _eval.EvaluateLine(grid, _config.Paylines[0], 20).Credits;
            Assert.AreEqual(one * 20, twenty);
        }

        [Test]
        public void VShapedLineFollowsItsRows()
        {
            // Line 4 is rows 0,1,2,1,0.
            var rows = Filler();
            rows[0, 0] = C; rows[1, 1] = C; rows[2, 2] = C; rows[1, 3] = C; rows[0, 4] = C;
            var win = _eval.EvaluateLine(Grid.FromRows(rows), _config.Paylines[3], 1);
            Assert.IsNotNull(win);
            Assert.AreEqual(5, win.Count);
            Assert.AreEqual(C, win.Symbol);
        }

        [Test]
        public void FullScreenOfOneSymbolPaysEveryLine()
        {
            var rows = new Symbol[3, 5];
            for (int row = 0; row < 3; row++)
                for (int r = 0; r < 5; r++) rows[row, r] = A;
            var wins = new List<LineWin>();
            long total = _eval.EvaluateLines(Grid.FromRows(rows), 2, wins);
            Assert.AreEqual(25, wins.Count);
            Assert.AreEqual(25 * _config.Info(A).Pays[5] * 2, total);
        }

        [Test]
        public void OnlyHighestWinPaysPerLine()
        {
            // W on reels 2-3 then Ace: Ace x4 via substitution, never also a separate 3-of-a-kind.
            var rows = Filler();
            rows[1, 0] = A; rows[1, 1] = W; rows[1, 2] = W; rows[1, 3] = A; rows[1, 4] = Q;
            var wins = new List<LineWin>();
            _eval.EvaluateLines(Grid.FromRows(rows), 1, wins);
            Assert.AreEqual(1, wins.Count(w => w.LineIndex == 0));
            Assert.AreEqual(4, wins.Single(w => w.LineIndex == 0).Count);
        }
    }
}
