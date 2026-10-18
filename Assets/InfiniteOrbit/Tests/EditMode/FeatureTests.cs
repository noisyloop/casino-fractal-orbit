using System.Linq;
using InfiniteOrbit.GameMath;
using NUnit.Framework;
using static InfiniteOrbit.Tests.TestUtil;

namespace InfiniteOrbit.Tests
{
    public class FreeSpinTests
    {
        private static Symbol[,] AllOf(Symbol s)
        {
            var rows = new Symbol[3, 5];
            for (int row = 0; row < 3; row++)
                for (int r = 0; r < 5; r++) rows[row, r] = s;
            return rows;
        }

        [Test]
        public void MultiplierStepsUpAfterEachWinningSpinAndCapsAtTen()
        {
            var config = ConfigWithFreeSpinScreen(AllOf(A));
            var engine = new SlotEngine(config);
            var fs = engine.PlayFreeSpins(new ScriptedRng(Repeat(0, 5 * 12)), 12, 1, 25);

            Assert.AreEqual(12, fs.Spins.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 }, fs.Spins.Select(s => s.Multiplier));
            long basePay = 25 * config.Info(A).Pays[5];
            Assert.AreEqual(basePay * fs.Spins.Sum(s => s.Multiplier), fs.TotalCredits);
            Assert.AreEqual(fs.TotalCredits, fs.Spins.Last().CumulativeCredits);
        }

        [Test]
        public void NonWinningSpinsKeepTheMultiplier()
        {
            var config = ConfigWithFreeSpinScreen(Filler());
            var engine = new SlotEngine(config);
            var fs = engine.PlayFreeSpins(new ScriptedRng(Repeat(0, 5 * 8)), 8, 1, 25);
            Assert.IsTrue(fs.Spins.All(s => s.Multiplier == 1));
            Assert.AreEqual(0, fs.TotalCredits);
            Assert.AreEqual(1, fs.FinalMultiplier);
        }

        [Test]
        public void ThreeScattersRetriggerEightMoreSpins()
        {
            // Stop 0 shows scatters on reels 1-3; stop 3 shows none.
            var rows = Filler();
            rows[0, 0] = S; rows[1, 1] = S; rows[2, 2] = S;
            var config = Config();
            config.FreeSpinReels = ReelsShowing(rows, new[] { T, J, Q });
            var engine = new SlotEngine(config);

            var script = Repeat(0, 5).Concat(Repeat(3, 5 * 15));
            var fs = engine.PlayFreeSpins(new ScriptedRng(script), 8, 1, 25);

            Assert.AreEqual(16, fs.Spins.Count);
            Assert.AreEqual(1, fs.Retriggers);
            Assert.AreEqual(8, fs.Spins[0].SpinsAdded);
            Assert.AreEqual(15, fs.Spins[0].SpinsRemaining);
            Assert.AreEqual(0, fs.Spins.Last().SpinsRemaining);
            Assert.IsFalse(fs.HitSpinCap);
        }

        [Test]
        public void EndlessRetriggerStopsAtSafetyCap()
        {
            var rows = Filler();
            rows[0, 0] = S; rows[1, 1] = S; rows[2, 2] = S;
            var config = ConfigWithFreeSpinScreen(rows);
            config.FreeSpins.MaxTotalSpins = 50;
            var fs = new SlotEngine(config).PlayFreeSpins(new ScriptedRng(Repeat(0, 5 * 50)), 8, 1, 25);
            Assert.IsTrue(fs.HitSpinCap);
            Assert.AreEqual(50, fs.Spins.Count);
        }

        [Test]
        public void FreeSpinReelsCarryNoOrbs()
        {
            var config = Config();
            for (int r = 0; r < 5; r++) Assert.AreEqual(0, config.FreeSpinReels[r].CountOf(O));
        }
    }

    public class HoldAndSpinTests
    {
        private static Symbol[,] ScreenWithOrbs(int orbs)
        {
            var rows = Filler();
            int placed = 0;
            for (int r = 0; r < 5 && placed < orbs; r++)
                for (int row = 0; row < 3 && placed < orbs; row++, placed++)
                    rows[row, r] = O;
            return rows;
        }

        private static SpinResult TriggerSpin(SlotEngine engine, int orbs)
        {
            // Orb prize draws on the trigger screen: one draw per orb (single-entry table => 0).
            return engine.Evaluate(new ScriptedRng(Repeat(0, orbs)), SpinMode.Base, ReelsShowing(ScreenWithOrbs(orbs)), Zeros(5), 1, 25, 1);
        }

        [Test]
        public void SixOrbsTriggerFiveDoNot()
        {
            var engine = new SlotEngine(ConfigWithSingleOrbPrize(OrbPrize.Credits(10)));
            Assert.IsFalse(TriggerSpin(engine, 5).TriggersHoldAndSpin);
            Assert.IsTrue(TriggerSpin(engine, 6).TriggersHoldAndSpin);
        }

        [Test]
        public void OrbsNeverTriggerDuringFreeSpins()
        {
            var engine = new SlotEngine(ConfigWithSingleOrbPrize(OrbPrize.Credits(10)));
            var spin = engine.Evaluate(new ScriptedRng(Repeat(0, 6)), SpinMode.FreeSpin, ReelsShowing(ScreenWithOrbs(6)), Zeros(5), 1, 25, 1);
            Assert.IsFalse(spin.TriggersHoldAndSpin);
        }

        [Test]
        public void NewOrbResetsRespinsToThree()
        {
            // Land weights 1:1, so a respin draw of 0 lands an orb and 1 is blank.
            var engine = new SlotEngine(ConfigWithSingleOrbPrize(OrbPrize.Credits(10)));
            var trigger = TriggerSpin(engine, 6);

            var script = Repeat(1, 9)                                    // respin 1: all 9 blank -> 2 left
                .Concat(new[] { 0, 0 }).Concat(Repeat(1, 8))             // respin 2: one orb (land + prize) -> reset to 3
                .Concat(Repeat(1, 8)).Concat(Repeat(1, 8)).Concat(Repeat(1, 8)); // three blanks -> end
            var rng = new ScriptedRng(script);
            var hs = engine.PlayHoldAndSpin(rng, trigger, 1, 25);

            CollectionAssert.AreEqual(new[] { 2, 3, 2, 1, 0 }, hs.Respins.Select(s => s.RespinsRemaining));
            Assert.AreEqual(7, hs.OrbCount);
            Assert.AreEqual(1, hs.Respins[1].NewOrbs.Count);
            Assert.AreEqual(7 * 10, hs.TotalCredits);
            Assert.IsFalse(hs.FullScreen);
            Assert.AreEqual(0, rng.Remaining, "Every scripted draw was consumed.");
        }

        [Test]
        public void FillingAllFifteenAwardsGrand()
        {
            var engine = new SlotEngine(ConfigWithSingleOrbPrize(OrbPrize.Credits(10)));
            var trigger = TriggerSpin(engine, 6);
            var script = Enumerable.Range(0, 9).SelectMany(_ => new[] { 0, 0 }); // all 9 empty cells land
            var hs = engine.PlayHoldAndSpin(new ScriptedRng(script), trigger, 1, 25);

            Assert.IsTrue(hs.FullScreen);
            Assert.AreEqual(1, hs.Respins.Count, "Feature ends as soon as the screen is full.");
            CollectionAssert.Contains(hs.JackpotsWon, OrbPrizeKind.Grand);
            Assert.AreEqual(15 * 10 + 1000 * 25, hs.TotalCredits);
        }

        [TestCase(OrbPrizeKind.Mini, 10)]
        [TestCase(OrbPrizeKind.Minor, 25)]
        [TestCase(OrbPrizeKind.Major, 100)]
        public void JackpotOrbsPayFixedMultipleOfTotalBet(OrbPrizeKind kind, int multiple)
        {
            var engine = new SlotEngine(ConfigWithSingleOrbPrize(OrbPrize.Jackpot(kind)));
            var trigger = TriggerSpin(engine, 6);
            var hs = engine.PlayHoldAndSpin(new ScriptedRng(Repeat(1, 9 * 3)), trigger, 1, 25);
            Assert.AreEqual(6, hs.JackpotsWon.Count(j => j == kind));
            Assert.AreEqual(6 * multiple * 25, hs.JackpotCredits);
            Assert.AreEqual(0, hs.OrbCredits);
        }

        [Test]
        public void RoundTotalIncludesBaseSpinAndFeature()
        {
            var config = Config();
            var engine = new SlotEngine(config);
            var rng = new Xoshiro256StarStar(7);
            for (int i = 0; i < 200_000; i++)
            {
                var round = engine.PlayRound(rng, 1);
                if (round.HoldAndSpin == null && round.FreeSpins == null) continue;
                long expected = round.BaseSpin.TotalCredits
                    + (round.HoldAndSpin?.TotalCredits ?? 0)
                    + (round.FreeSpins?.TotalCredits ?? 0);
                Assert.AreEqual(expected, round.TotalWin);
                Assert.AreEqual(config.BigWin.Classify(round.TotalWin, round.TotalBet), round.BigWinTier);
            }
        }
    }
}
