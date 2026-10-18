using System;

namespace InfiniteOrbit.GameMath
{
    /// <summary>
    /// Produces complete round outcomes. Draw order is fixed so a seed always replays identically:
    /// base stops (reel 1..5) → orb prizes (cell order) → hold-and-spin respins → free spins.
    /// </summary>
    public sealed class SlotEngine
    {
        private readonly GameConfig _config;
        private readonly WinEvaluator _evaluator;

        public GameConfig Config => _config;
        public WinEvaluator Evaluator => _evaluator;

        public SlotEngine(GameConfig config)
        {
            config.Validate();
            _config = config;
            _evaluator = new WinEvaluator(config);
        }

        public RoundResult PlayRound(IRng rng, int betLevel)
        {
            if (betLevel < 0 || betLevel >= _config.LineBets.Length)
                throw new ArgumentOutOfRangeException(nameof(betLevel));

            long lineBet = _config.LineBets[betLevel];
            long totalBet = lineBet * _config.LineCount;

            var round = new RoundResult { TotalBet = totalBet, LineBet = lineBet };
            round.BaseSpin = Spin(rng, SpinMode.Base, lineBet, totalBet, 1);
            long total = round.BaseSpin.TotalCredits;

            if (round.BaseSpin.TriggersHoldAndSpin)
            {
                round.HoldAndSpin = PlayHoldAndSpin(rng, round.BaseSpin, lineBet, totalBet);
                total += round.HoldAndSpin.TotalCredits;
            }

            if (round.BaseSpin.TriggersFreeSpins)
            {
                round.FreeSpins = PlayFreeSpins(rng, round.BaseSpin.FreeSpinsAwarded, lineBet, totalBet);
                total += round.FreeSpins.TotalCredits;
            }

            round.TotalWin = total;
            round.BigWinTier = _config.BigWin.Classify(total, totalBet);
            return round;
        }

        /// <summary>Spins and evaluates one screen. Wins are multiplied by <paramref name="multiplier"/>.</summary>
        public SpinResult Spin(IRng rng, SpinMode mode, long lineBet, long totalBet, int multiplier)
        {
            var reels = mode == SpinMode.Base ? _config.BaseReels : _config.FreeSpinReels;
            var stops = new int[GameConfig.Reels];
            for (int r = 0; r < GameConfig.Reels; r++) stops[r] = rng.NextInt(reels[r].Length);
            return Evaluate(rng, mode, reels, stops, lineBet, totalBet, multiplier);
        }

        /// <summary>Evaluates a screen at known stops. Orb prizes still draw from <paramref name="rng"/>.</summary>
        public SpinResult Evaluate(IRng rng, SpinMode mode, ReelSet reels, int[] stops, long lineBet, long totalBet, int multiplier)
        {
            var grid = Grid.FromStops(reels, stops);
            var spin = new SpinResult
            {
                Mode = mode,
                Stops = (int[])stops.Clone(),
                Grid = grid.ToArray(),
                Multiplier = multiplier,
            };

            spin.LineCredits = _evaluator.EvaluateLines(grid, lineBet, spin.LineWins);
            spin.ScatterCredits = _evaluator.EvaluateScatter(grid, totalBet, spin.ScatterCells, out int scatters);
            spin.ScatterCount = scatters;
            spin.TotalCredits = (spin.LineCredits + spin.ScatterCredits) * multiplier;
            spin.FreeSpinsAwarded = _config.FreeSpins.Awards[Math.Min(scatters, GameConfig.Reels)];

            for (int c = 0; c < GameConfig.Cells; c++)
            {
                if (grid[c] != Symbol.Orb) continue;
                spin.Orbs.Add(DrawOrb(rng, c, lineBet, totalBet));
            }
            spin.TriggersHoldAndSpin = mode == SpinMode.Base && spin.Orbs.Count >= _config.HoldAndSpin.TriggerCount;
            return spin;
        }

        private OrbCell DrawOrb(IRng rng, int cell, long lineBet, long totalBet)
        {
            var prize = _config.HoldAndSpin.Prizes.Pick(rng);
            long credits = prize.IsJackpot
                ? _config.HoldAndSpin.JackpotMultiple(prize.Kind) * totalBet
                : prize.LineBetMultiple * lineBet;
            return new OrbCell(cell, prize, credits);
        }

        public FreeSpinsResult PlayFreeSpins(IRng rng, int initialSpins, long lineBet, long totalBet)
        {
            var fs = _config.FreeSpins;
            var result = new FreeSpinsResult { InitialSpins = initialSpins };
            int remaining = initialSpins;
            int played = 0;
            int multiplier = fs.StartMultiplier;
            long cumulative = 0;

            while (remaining > 0)
            {
                if (played >= fs.MaxTotalSpins)
                {
                    result.HitSpinCap = true;
                    break;
                }

                remaining--;
                played++;
                var spin = Spin(rng, SpinMode.FreeSpin, lineBet, totalBet, multiplier);
                cumulative += spin.TotalCredits;

                int added = spin.FreeSpinsAwarded;
                if (added > 0)
                {
                    remaining += added;
                    result.Retriggers++;
                }

                result.Spins.Add(new FreeSpinStep
                {
                    Number = played,
                    Spin = spin,
                    Multiplier = multiplier,
                    SpinsAdded = added,
                    SpinsRemaining = remaining,
                    CumulativeCredits = cumulative,
                });

                if (spin.TotalCredits > 0)
                    multiplier = Math.Min(multiplier + fs.MultiplierStep, fs.MaxMultiplier);
            }

            result.FinalMultiplier = multiplier;
            result.TotalCredits = cumulative;
            return result;
        }

        public HoldAndSpinResult PlayHoldAndSpin(IRng rng, SpinResult trigger, long lineBet, long totalBet)
        {
            var cfg = _config.HoldAndSpin;
            var result = new HoldAndSpinResult();
            foreach (var orb in trigger.Orbs)
            {
                result.InitialOrbs.Add(orb);
                result.Board[orb.Cell] = orb;
            }

            int filled = result.InitialOrbs.Count;
            int respins = cfg.Respins;
            int number = 0;
            int landWeight = cfg.OrbWeight + cfg.BlankWeight;

            while (respins > 0 && filled < GameConfig.Cells)
            {
                number++;
                var step = new RespinStep { Number = number };
                for (int c = 0; c < GameConfig.Cells; c++)
                {
                    if (result.Board[c] != null) continue;
                    if (rng.NextInt(landWeight) >= cfg.OrbWeight) continue;
                    var orb = DrawOrb(rng, c, lineBet, totalBet);
                    result.Board[c] = orb;
                    step.NewOrbs.Add(orb);
                    filled++;
                }

                respins = step.NewOrbs.Count > 0 ? cfg.Respins : respins - 1;
                step.RespinsRemaining = filled < GameConfig.Cells ? respins : 0;
                result.Respins.Add(step);
            }

            long orbCredits = 0;
            long jackpotCredits = 0;
            foreach (var orb in result.Board)
            {
                if (orb == null) continue;
                if (orb.Prize.IsJackpot)
                {
                    result.JackpotsWon.Add(orb.Prize.Kind);
                    jackpotCredits += orb.Credits;
                }
                else
                {
                    orbCredits += orb.Credits;
                }
            }

            result.FullScreen = filled == GameConfig.Cells;
            if (result.FullScreen && cfg.GrandOnFullScreen)
            {
                result.JackpotsWon.Add(OrbPrizeKind.Grand);
                jackpotCredits += cfg.GrandMultiple * totalBet;
            }

            result.OrbCredits = orbCredits;
            result.JackpotCredits = jackpotCredits;
            result.TotalCredits = orbCredits + jackpotCredits;
            return result;
        }
    }
}
