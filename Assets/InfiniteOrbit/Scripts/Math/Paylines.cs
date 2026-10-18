using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>A fixed payline: the row (0 = top, 1 = middle, 2 = bottom) used on each reel, read left to right.</summary>
    public sealed class Payline
    {
        private readonly int[] _rows;

        public int Index { get; }
        public IReadOnlyList<int> Rows => _rows;

        public Payline(int index, int[] rows)
        {
            Index = index;
            _rows = (int[])rows.Clone();
        }

        public int CellAt(int reel) => Grid.CellIndex(reel, _rows[reel]);
    }

    public static class StandardPaylines
    {
        /// <summary>The 25 fixed lines, numbered 1..25 on the cabinet (index 0..24 in code).</summary>
        public static readonly int[][] Rows =
        {
            new[] { 1, 1, 1, 1, 1 },
            new[] { 0, 0, 0, 0, 0 },
            new[] { 2, 2, 2, 2, 2 },
            new[] { 0, 1, 2, 1, 0 },
            new[] { 2, 1, 0, 1, 2 },
            new[] { 0, 0, 1, 2, 2 },
            new[] { 2, 2, 1, 0, 0 },
            new[] { 1, 0, 0, 0, 1 },
            new[] { 1, 2, 2, 2, 1 },
            new[] { 1, 0, 1, 2, 1 },
            new[] { 1, 2, 1, 0, 1 },
            new[] { 0, 1, 0, 1, 0 },
            new[] { 2, 1, 2, 1, 2 },
            new[] { 1, 1, 0, 1, 1 },
            new[] { 1, 1, 2, 1, 1 },
            new[] { 0, 1, 1, 1, 0 },
            new[] { 2, 1, 1, 1, 2 },
            new[] { 0, 0, 2, 0, 0 },
            new[] { 2, 2, 0, 2, 2 },
            new[] { 0, 2, 0, 2, 0 },
            new[] { 2, 0, 2, 0, 2 },
            new[] { 1, 0, 2, 0, 1 },
            new[] { 1, 2, 0, 2, 1 },
            new[] { 0, 2, 2, 2, 0 },
            new[] { 2, 0, 0, 0, 2 },
        };

        public static Payline[] Create()
        {
            var lines = new Payline[Rows.Length];
            for (int i = 0; i < Rows.Length; i++) lines[i] = new Payline(i, Rows[i]);
            return lines;
        }
    }
}
