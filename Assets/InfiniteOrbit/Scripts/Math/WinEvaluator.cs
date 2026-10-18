using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>
    /// Stateless win evaluation. Rules:
    /// <list type="bullet">
    /// <item>Line wins pay left to right from reel 1, consecutive matches only; only the highest win per line pays.</item>
    /// <item>Wild substitutes for every paying symbol; it never substitutes for Scatter or Orb.</item>
    /// <item>Scatters pay anywhere on the screen, in multiples of total bet.</item>
    /// <item>Orbs have no line or scatter pay; they only drive hold-and-spin.</item>
    /// </list>
    /// </summary>
    public sealed class WinEvaluator
    {
        private readonly GameConfig _config;

        public WinEvaluator(GameConfig config)
        {
            _config = config;
        }

        private static bool IsSubstitutable(Symbol s) => s != Symbol.Scatter && s != Symbol.Orb;

        /// <summary>Evaluates one payline. Returns null when it does not pay.</summary>
        public LineWin EvaluateLine(Grid grid, Payline line, long lineBet)
        {
            var symbols = new Symbol[GameConfig.Reels];
            for (int r = 0; r < GameConfig.Reels; r++) symbols[r] = grid[line.CellAt(r)];
            int pay = EvaluateSymbols(symbols, out Symbol paidSymbol, out int paidCount);
            if (pay <= 0) return null;

            var cells = new int[paidCount];
            for (int r = 0; r < paidCount; r++) cells[r] = line.CellAt(r);
            return new LineWin(line.Index, paidSymbol, paidCount, pay * lineBet, cells);
        }

        /// <summary>
        /// Core line rule on the five symbols of a line, reel 1 first. Returns the pay in line-bet multiples
        /// (0 if none) and the symbol/count that pays. Shared by the engine and the analytic PAR calculator.
        /// </summary>
        public int EvaluateSymbols(Symbol[] line, out Symbol paidSymbol, out int paidCount)
        {
            paidSymbol = line[0];
            paidCount = 0;
            if (!IsSubstitutable(line[0])) return 0;

            // Leading wilds pay on their own (if wild has a pay) or substitute for the first real symbol.
            int wildRun = 0;
            while (wildRun < GameConfig.Reels && line[wildRun] == Symbol.Wild) wildRun++;

            Symbol target = wildRun < GameConfig.Reels ? line[wildRun] : Symbol.Wild;
            int count = wildRun;
            if (target != Symbol.Wild && IsSubstitutable(target))
            {
                while (count < GameConfig.Reels && (line[count] == target || line[count] == Symbol.Wild)) count++;
            }

            int wildPay = _config.Info(Symbol.Wild).Pays[wildRun];
            int targetPay = IsSubstitutable(target) ? _config.Info(target).Pays[count] : 0;

            if (wildPay > targetPay)
            {
                paidSymbol = Symbol.Wild;
                paidCount = wildRun;
                return wildPay;
            }
            paidSymbol = target;
            paidCount = count;
            return targetPay;
        }

        public long EvaluateLines(Grid grid, long lineBet, List<LineWin> into)
        {
            long total = 0;
            var lines = _config.Paylines;
            for (int i = 0; i < lines.Length; i++)
            {
                var win = EvaluateLine(grid, lines[i], lineBet);
                if (win == null) continue;
                into?.Add(win);
                total += win.Credits;
            }
            return total;
        }

        /// <summary>Counts scatters, records their cells and returns the scatter pay.</summary>
        public long EvaluateScatter(Grid grid, long totalBet, List<int> cells, out int count)
        {
            count = 0;
            for (int c = 0; c < GameConfig.Cells; c++)
            {
                if (grid[c] != Symbol.Scatter) continue;
                count++;
                cells?.Add(c);
            }
            int capped = count > GameConfig.Reels ? GameConfig.Reels : count;
            return _config.ScatterPays[capped] * totalBet;
        }
    }
}
