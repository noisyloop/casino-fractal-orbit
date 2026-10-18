using System;
using System.Text;

namespace InfiniteOrbit.GameMath
{
    /// <summary>The visible 5x3 window. Cells are indexed reel-major: cell = reel * 3 + row.</summary>
    public sealed class Grid
    {
        private readonly Symbol[] _cells = new Symbol[GameConfig.Cells];

        public static int CellIndex(int reel, int row) => reel * GameConfig.Rows + row;
        public static int ReelOf(int cell) => cell / GameConfig.Rows;
        public static int RowOf(int cell) => cell % GameConfig.Rows;

        public Symbol this[int cell]
        {
            get => _cells[cell];
            set => _cells[cell] = value;
        }

        public Symbol this[int reel, int row]
        {
            get => _cells[CellIndex(reel, row)];
            set => _cells[CellIndex(reel, row)] = value;
        }

        /// <summary>Fills the window from reel strips; the stop is the strip index shown on the top row.</summary>
        public static Grid FromStops(ReelSet reels, int[] stops)
        {
            if (stops.Length != GameConfig.Reels) throw new ArgumentException("Need one stop per reel.");
            var g = new Grid();
            for (int r = 0; r < GameConfig.Reels; r++)
                for (int row = 0; row < GameConfig.Rows; row++)
                    g[r, row] = reels[r][stops[r] + row];
            return g;
        }

        /// <summary>Builds a grid from rows written top to bottom, e.g. { {A,A,A,K,Q}, {...}, {...} }.</summary>
        public static Grid FromRows(Symbol[,] rows)
        {
            var g = new Grid();
            for (int row = 0; row < GameConfig.Rows; row++)
                for (int r = 0; r < GameConfig.Reels; r++)
                    g[r, row] = rows[row, r];
            return g;
        }

        public int Count(Symbol s)
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i] == s) n++;
            return n;
        }

        public Symbol[] ToArray() => (Symbol[])_cells.Clone();

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int row = 0; row < GameConfig.Rows; row++)
            {
                for (int r = 0; r < GameConfig.Reels; r++)
                    sb.Append(this[r, row].ToString().PadRight(10));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
