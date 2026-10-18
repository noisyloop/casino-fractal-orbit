using System;

namespace InfiniteOrbit.GameMath
{
    /// <summary>
    /// Exact (closed-form / full-enumeration) calculations used to cross-check the Monte Carlo simulation in the PAR sheet.
    /// Reels are independent and every stop is equally likely, so per-reel distributions can be convolved exactly.
    /// </summary>
    public static class Analytics
    {
        /// <summary>
        /// Exact expected line pay in line-bet multiples for one payline, which equals the line-pay RTP contribution
        /// (25 lines x E[pay per line] line bets / 25 line bets of stake). Each line reads one cell per reel and every
        /// cell on a reel has the strip's symbol distribution, so lines are evaluated over the product of per-reel marginals.
        /// </summary>
        public static double LineRtp(GameConfig config, ReelSet reels)
        {
            var eval = new WinEvaluator(config);
            int symbolCount = config.Symbols.Length;
            var p = new double[GameConfig.Reels][];
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                p[r] = new double[symbolCount];
                for (int s = 0; s < symbolCount; s++)
                    p[r][s] = (double)reels[r].CountOf((Symbol)s) / reels[r].Length;
            }

            double expected = 0;
            var line = new Symbol[GameConfig.Reels];
            for (int a = 0; a < symbolCount; a++)
            {
                if (p[0][a] == 0) continue;
                line[0] = (Symbol)a;
                for (int b = 0; b < symbolCount; b++)
                {
                    if (p[1][b] == 0) continue;
                    line[1] = (Symbol)b;
                    double pab = p[0][a] * p[1][b];
                    for (int c = 0; c < symbolCount; c++)
                    {
                        if (p[2][c] == 0) continue;
                        line[2] = (Symbol)c;
                        double pabc = pab * p[2][c];
                        for (int d = 0; d < symbolCount; d++)
                        {
                            if (p[3][d] == 0) continue;
                            line[3] = (Symbol)d;
                            double pabcd = pabc * p[3][d];
                            for (int e = 0; e < symbolCount; e++)
                            {
                                if (p[4][e] == 0) continue;
                                line[4] = (Symbol)e;
                                int pay = eval.EvaluateSymbols(line, out _, out _);
                                if (pay > 0) expected += pabcd * p[4][e] * pay;
                            }
                        }
                    }
                }
            }
            return expected;
        }

        /// <summary>Exact distribution of how many of <paramref name="symbol"/> are visible on the whole screen (index 0..15).</summary>
        public static double[] ScreenCountDistribution(ReelSet reels, Symbol symbol)
        {
            var dist = new double[GameConfig.Cells + 1];
            dist[0] = 1;
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                var strip = reels[r];
                var reelDist = new double[GameConfig.Rows + 1];
                for (int stop = 0; stop < strip.Length; stop++)
                {
                    int n = 0;
                    for (int row = 0; row < GameConfig.Rows; row++)
                        if (strip[stop + row] == symbol) n++;
                    reelDist[n] += 1.0 / strip.Length;
                }

                var next = new double[GameConfig.Cells + 1];
                for (int i = 0; i <= GameConfig.Cells; i++)
                {
                    if (dist[i] == 0) continue;
                    for (int k = 0; k <= GameConfig.Rows && i + k <= GameConfig.Cells; k++)
                        next[i + k] += dist[i] * reelDist[k];
                }
                dist = next;
            }
            return dist;
        }

        public static double ProbabilityAtLeast(double[] dist, int n)
        {
            double p = 0;
            for (int i = n; i < dist.Length; i++) p += dist[i];
            return p;
        }

        /// <summary>Exact scatter-pay RTP for a reel set.</summary>
        public static double ScatterRtp(GameConfig config, ReelSet reels)
        {
            var dist = ScreenCountDistribution(reels, Symbol.Scatter);
            double rtp = 0;
            for (int i = 0; i < dist.Length; i++)
                rtp += dist[i] * config.ScatterPays[Math.Min(i, GameConfig.Reels)];
            return rtp;
        }

        /// <summary>Expected value of one orb prize in x total bet.</summary>
        public static double MeanOrbPrize(GameConfig config)
        {
            var table = config.HoldAndSpin.Prizes;
            double mean = 0;
            for (int i = 0; i < table.Count; i++)
            {
                var prize = table[i];
                double x = prize.IsJackpot
                    ? config.HoldAndSpin.JackpotMultiple(prize.Kind)
                    : (double)prize.LineBetMultiple / config.LineCount;
                mean += table.ProbabilityAt(i) * x;
            }
            return mean;
        }
    }
}
