using System;
using System.Text;
using InfiniteOrbit.GameMath;

namespace InfiniteOrbit.Simulator
{
    /// <summary>Derived figures from one simulation run plus the exact analytic cross-checks.</summary>
    public sealed class Report
    {
        public GameConfig Config { get; }
        public SimulationStats Stats { get; }
        public ulong Seed { get; }
        public int BetLevel { get; }
        public TimeSpan Elapsed { get; }

        public double ExactLineRtp { get; }
        public double ExactScatterRtp { get; }
        public double[] ScatterDistribution { get; }
        public double[] OrbDistribution { get; }
        public double ExactFreeSpinTrigger { get; }
        public double ExactHoldAndSpinTrigger { get; }
        public double MeanOrbPrize { get; }
        public double FreeSpinReelLineRtp { get; }

        public Report(GameConfig config, SimulationStats stats, ulong seed, int betLevel, TimeSpan elapsed)
        {
            Config = config;
            Stats = stats;
            Seed = seed;
            BetLevel = betLevel;
            Elapsed = elapsed;
            ExactLineRtp = Analytics.LineRtp(config, config.BaseReels);
            ExactScatterRtp = Analytics.ScatterRtp(config, config.BaseReels);
            ScatterDistribution = Analytics.ScreenCountDistribution(config.BaseReels, Symbol.Scatter);
            OrbDistribution = Analytics.ScreenCountDistribution(config.BaseReels, Symbol.Orb);
            ExactFreeSpinTrigger = Analytics.ProbabilityAtLeast(ScatterDistribution, 3);
            ExactHoldAndSpinTrigger = Analytics.ProbabilityAtLeast(OrbDistribution, config.HoldAndSpin.TriggerCount);
            MeanOrbPrize = Analytics.MeanOrbPrize(config);
            FreeSpinReelLineRtp = Analytics.LineRtp(config, config.FreeSpinReels);
        }

        public long Bet => Config.TotalBetForLevel(BetLevel);
        public double PerRound(long n) => (double)n / Stats.Rounds;
        public double PerTrigger(double n, long triggers) => triggers == 0 ? 0 : n / triggers;
        public double X(long credits) => (double)credits / Bet;

        public double FreeSpinAverageAward => PerTrigger(X(Stats.FreeSpinsWin), Stats.FreeSpinTriggers);
        public double FreeSpinAverageSpins => PerTrigger(Stats.FreeSpinsPlayed, Stats.FreeSpinTriggers);
        public double HoldAndSpinAverageAward => PerTrigger(X(Stats.HoldAndSpinWin), Stats.HoldAndSpinTriggers);
        public double HoldAndSpinAverageOrbs => PerTrigger(Stats.HoldAndSpinFinalOrbs, Stats.HoldAndSpinTriggers);
        public double HoldAndSpinAverageRespins => PerTrigger(Stats.HoldAndSpinRespins, Stats.HoldAndSpinTriggers);
        public double VolatilityIndex90 => 1.645 * Stats.StdDev;

        public static string OneIn(double p) => p <= 0 ? "never" : $"1 in {1 / p:N1}";

        public string ConsoleSummary()
        {
            var s = Stats;
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Simulated {s.Rounds:N0} rounds in {Elapsed.TotalSeconds:F1}s ({s.Rounds / Elapsed.TotalSeconds / 1e6:F2}M rounds/s)");
            sb.AppendLine();
            sb.AppendLine("RETURN TO PLAYER");
            sb.AppendLine($"  Total RTP .................. {s.Rtp:P3}  (95% CI ± {s.Rtp95HalfWidth:P3})");
            sb.AppendLine($"    Base game lines .......... {s.RtpOf(s.BaseLineWin):P3}  (exact {ExactLineRtp:P3})");
            sb.AppendLine($"    Base game scatter ........ {s.RtpOf(s.BaseScatterWin):P3}  (exact {ExactScatterRtp:P3})");
            sb.AppendLine($"    Free spins ............... {s.RtpOf(s.FreeSpinsWin):P3}");
            sb.AppendLine($"    Hold & spin (Orbit Lock) . {s.RtpOf(s.HoldAndSpinWin):P3}  (jackpots {s.RtpOf(s.HoldAndSpinJackpotWin):P3})");
            sb.AppendLine();
            sb.AppendLine("FREQUENCIES");
            sb.AppendLine($"  Hit frequency (any win) .... {s.HitFrequency:P3}  ({OneIn(s.HitFrequency)})");
            sb.AppendLine($"  Free spins trigger ......... {SimulationStats.OneIn(s.FreeSpinTriggers, s.Rounds)}  (exact {OneIn(ExactFreeSpinTrigger)})");
            sb.AppendLine($"  Hold & spin trigger ........ {SimulationStats.OneIn(s.HoldAndSpinTriggers, s.Rounds)}  (exact {OneIn(ExactHoldAndSpinTrigger)})");
            sb.AppendLine($"  Grand jackpot .............. {SimulationStats.OneIn(s.JackpotHits[(int)OrbPrizeKind.Grand], s.Rounds)}");
            sb.AppendLine();
            sb.AppendLine("WINS (x total bet)");
            sb.AppendLine($"  Average win per round ...... {s.MeanMultiple:F3}x");
            sb.AppendLine($"  Average win when hit ....... {s.AverageWinMultipleWhenHit:F3}x");
            sb.AppendLine($"  Max win .................... {s.MaxWinMultiple:N1}x  (round #{s.MaxWinRoundIndex:N0})");
            sb.AppendLine($"  Std deviation (volatility) . {s.StdDev:F2}   VI(90%) {VolatilityIndex90:F2}");
            sb.AppendLine($"  Free spins avg award ....... {FreeSpinAverageAward:F2}x over {FreeSpinAverageSpins:F2} spins");
            sb.AppendLine($"  Hold & spin avg award ...... {HoldAndSpinAverageAward:F2}x with {HoldAndSpinAverageOrbs:F2} orbs");
            sb.AppendLine();
            sb.AppendLine("WIN DISTRIBUTION");
            double maxP = 0;
            for (int i = 1; i < s.BucketCounts.Length; i++) maxP = Math.Max(maxP, PerRound(s.BucketCounts[i]));
            for (int i = 0; i < s.BucketCounts.Length; i++)
            {
                double p = PerRound(s.BucketCounts[i]);
                int bar = i == 0 || p == 0 ? 0 : Math.Max(1, (int)Math.Round(40 * Math.Sqrt(p / maxP)));
                sb.AppendLine($"  {SimulationStats.BucketLabel(i),-18} {s.BucketCounts[i],12:N0}  {p,9:P4}  RTP {s.BucketWin[i] / s.Rounds,8:P3}  {new string('#', bar)}");
            }
            sb.AppendLine("  (bar length ~ sqrt(probability), excluding the no-win bucket)");
            return sb.ToString().Replace(" %", "%");
        }
    }
}
