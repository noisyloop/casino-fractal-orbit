using System;
using System.Threading.Tasks;

namespace InfiniteOrbit.GameMath
{
    /// <summary>Aggregated Monte Carlo statistics. Mergeable so chunks can run in parallel and still be deterministic.</summary>
    public sealed class SimulationStats
    {
        /// <summary>Lower edges (in x total bet) of the win-distribution buckets. Bucket 0 is "no win".</summary>
        public static readonly double[] BucketEdges = { 0, 1e-9, 1, 2, 5, 10, 20, 50, 100, 250, 500, 1000 };

        public static string BucketLabel(int i)
        {
            if (i == 0) return "0x (no win)";
            string lo = i == 1 ? "0x" : $"{BucketEdges[i]:0.##}x";
            return i == BucketEdges.Length - 1 ? $">= {lo}" : $"{(i == 1 ? "> " : "")}{lo} - <{BucketEdges[i + 1]:0.##}x";
        }

        public int MaxMultiplierCap = 10;
        public long Rounds;
        public long TotalBet;
        public long TotalWin;
        public long BaseLineWin;
        public long BaseScatterWin;
        public long HoldAndSpinWin;
        public long HoldAndSpinJackpotWin;
        public long FreeSpinsWin;

        public long HitRounds;
        public long BaseHitSpins;

        public long FreeSpinTriggers;
        public long[] FreeSpinTriggersByScatters = new long[GameConfig.Reels + 1];
        public long FreeSpinsPlayed;
        public long FreeSpinRetriggers;
        public long FreeSpinCapHits;
        public long FreeSpinMaxMultiplierReached;
        public double FreeSpinMaxWinMultiple;

        public long HoldAndSpinTriggers;
        public long HoldAndSpinFinalOrbs;
        public long HoldAndSpinRespins;
        public long HoldAndSpinFullScreens;
        public long[] JackpotHits = new long[5]; // indexed by OrbPrizeKind
        public double HoldAndSpinMaxWinMultiple;

        public long BothFeatures;

        public double SumMultiple;
        public double SumSqMultiple;
        public double MaxWinMultiple;
        public long MaxWinRoundIndex = -1;

        public long[] BigWinCounts = new long[4];
        public long[] BucketCounts = new long[BucketEdges.Length];
        public double[] BucketWin = new double[BucketEdges.Length];

        public void Record(RoundResult r, long roundIndex)
        {
            Rounds++;
            TotalBet += r.TotalBet;
            TotalWin += r.TotalWin;
            BaseLineWin += r.BaseSpin.LineCredits;
            BaseScatterWin += r.BaseSpin.ScatterCredits;
            if (r.BaseSpin.TotalCredits > 0) BaseHitSpins++;
            if (r.TotalWin > 0) HitRounds++;

            double x = (double)r.TotalWin / r.TotalBet;
            SumMultiple += x;
            SumSqMultiple += x * x;
            if (x > MaxWinMultiple)
            {
                MaxWinMultiple = x;
                MaxWinRoundIndex = roundIndex;
            }

            int bucket = 0;
            for (int i = BucketEdges.Length - 1; i >= 0; i--)
            {
                if (x >= BucketEdges[i]) { bucket = i; break; }
            }
            BucketCounts[bucket]++;
            BucketWin[bucket] += x;
            BigWinCounts[(int)r.BigWinTier]++;

            if (r.FreeSpins != null)
            {
                var fs = r.FreeSpins;
                FreeSpinTriggers++;
                FreeSpinTriggersByScatters[Math.Min(r.BaseSpin.ScatterCount, GameConfig.Reels)]++;
                FreeSpinsPlayed += fs.Spins.Count;
                FreeSpinRetriggers += fs.Retriggers;
                FreeSpinsWin += fs.TotalCredits;
                if (fs.HitSpinCap) FreeSpinCapHits++;
                double fx = (double)fs.TotalCredits / r.TotalBet;
                if (fx > FreeSpinMaxWinMultiple) FreeSpinMaxWinMultiple = fx;
                foreach (var s in fs.Spins)
                    if (s.Multiplier >= MaxMultiplierCap) { FreeSpinMaxMultiplierReached++; break; }
            }

            if (r.HoldAndSpin != null)
            {
                var hs = r.HoldAndSpin;
                HoldAndSpinTriggers++;
                HoldAndSpinFinalOrbs += hs.OrbCount;
                HoldAndSpinRespins += hs.Respins.Count;
                HoldAndSpinWin += hs.TotalCredits;
                HoldAndSpinJackpotWin += hs.JackpotCredits;
                if (hs.FullScreen) HoldAndSpinFullScreens++;
                foreach (var j in hs.JackpotsWon) JackpotHits[(int)j]++;
                double hx = (double)hs.TotalCredits / r.TotalBet;
                if (hx > HoldAndSpinMaxWinMultiple) HoldAndSpinMaxWinMultiple = hx;
            }

            if (r.FreeSpins != null && r.HoldAndSpin != null) BothFeatures++;
        }

        public void Merge(SimulationStats o, long roundOffset)
        {
            if (o.MaxWinMultiple > MaxWinMultiple)
            {
                MaxWinMultiple = o.MaxWinMultiple;
                MaxWinRoundIndex = o.MaxWinRoundIndex + roundOffset;
            }
            Rounds += o.Rounds;
            TotalBet += o.TotalBet;
            TotalWin += o.TotalWin;
            BaseLineWin += o.BaseLineWin;
            BaseScatterWin += o.BaseScatterWin;
            HoldAndSpinWin += o.HoldAndSpinWin;
            HoldAndSpinJackpotWin += o.HoldAndSpinJackpotWin;
            FreeSpinsWin += o.FreeSpinsWin;
            HitRounds += o.HitRounds;
            BaseHitSpins += o.BaseHitSpins;
            FreeSpinTriggers += o.FreeSpinTriggers;
            for (int i = 0; i < FreeSpinTriggersByScatters.Length; i++) FreeSpinTriggersByScatters[i] += o.FreeSpinTriggersByScatters[i];
            FreeSpinsPlayed += o.FreeSpinsPlayed;
            FreeSpinRetriggers += o.FreeSpinRetriggers;
            FreeSpinCapHits += o.FreeSpinCapHits;
            FreeSpinMaxMultiplierReached += o.FreeSpinMaxMultiplierReached;
            FreeSpinMaxWinMultiple = Math.Max(FreeSpinMaxWinMultiple, o.FreeSpinMaxWinMultiple);
            HoldAndSpinTriggers += o.HoldAndSpinTriggers;
            HoldAndSpinFinalOrbs += o.HoldAndSpinFinalOrbs;
            HoldAndSpinRespins += o.HoldAndSpinRespins;
            HoldAndSpinFullScreens += o.HoldAndSpinFullScreens;
            for (int i = 0; i < JackpotHits.Length; i++) JackpotHits[i] += o.JackpotHits[i];
            HoldAndSpinMaxWinMultiple = Math.Max(HoldAndSpinMaxWinMultiple, o.HoldAndSpinMaxWinMultiple);
            BothFeatures += o.BothFeatures;
            SumMultiple += o.SumMultiple;
            SumSqMultiple += o.SumSqMultiple;
            for (int i = 0; i < BigWinCounts.Length; i++) BigWinCounts[i] += o.BigWinCounts[i];
            for (int i = 0; i < BucketCounts.Length; i++)
            {
                BucketCounts[i] += o.BucketCounts[i];
                BucketWin[i] += o.BucketWin[i];
            }
        }

        public double Rtp => TotalBet == 0 ? 0 : (double)TotalWin / TotalBet;
        public double RtpOf(long part) => TotalBet == 0 ? 0 : (double)part / TotalBet;
        public double HitFrequency => Rounds == 0 ? 0 : (double)HitRounds / Rounds;
        public double MeanMultiple => SumMultiple / Rounds;

        /// <summary>Standard deviation of the per-round win in x total bet (the volatility index).</summary>
        public double StdDev
        {
            get
            {
                double mean = MeanMultiple;
                return Math.Sqrt(Math.Max(0, SumSqMultiple / Rounds - mean * mean));
            }
        }

        /// <summary>Half-width of the 95% confidence interval on RTP.</summary>
        public double Rtp95HalfWidth => 1.96 * StdDev / Math.Sqrt(Rounds);

        public double AverageWinMultipleWhenHit => HitRounds == 0 ? 0 : SumMultiple / HitRounds;

        public static string OneIn(long events, long rounds) => events == 0 ? "never" : $"1 in {(double)rounds / events:N1}";
    }

    public static class Simulation
    {
        public const int ChunkSize = 100_000;

        /// <summary>
        /// Runs <paramref name="rounds"/> paid rounds. The work is split into fixed-size chunks, each with its own
        /// RNG stream derived from <paramref name="seed"/>, so results are identical regardless of thread count.
        /// </summary>
        public static SimulationStats Run(GameConfig config, ulong seed, long rounds, int betLevel = 0, bool parallel = true, Action<long> progress = null)
        {
            var engine = new SlotEngine(config);
            int chunks = (int)((rounds + ChunkSize - 1) / ChunkSize);
            var results = new SimulationStats[chunks];
            long done = 0;

            void RunChunk(int chunk)
            {
                long start = (long)chunk * ChunkSize;
                long count = Math.Min(ChunkSize, rounds - start);
                var rng = new Xoshiro256StarStar(Xoshiro256StarStar.DeriveSeed(seed, (ulong)chunk));
                var stats = new SimulationStats { MaxMultiplierCap = config.FreeSpins.MaxMultiplier };
                for (long i = 0; i < count; i++)
                    stats.Record(engine.PlayRound(rng, betLevel), i);
                results[chunk] = stats;
                if (progress != null)
                {
                    long d = System.Threading.Interlocked.Add(ref done, count);
                    progress(d);
                }
            }

            if (parallel) Parallel.For(0, chunks, RunChunk);
            else for (int c = 0; c < chunks; c++) RunChunk(c);

            var total = new SimulationStats { MaxMultiplierCap = config.FreeSpins.MaxMultiplier };
            for (int c = 0; c < chunks; c++) total.Merge(results[c], (long)c * ChunkSize);
            return total;
        }
    }
}
