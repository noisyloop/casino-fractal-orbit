using System.Collections.Generic;
using InfiniteOrbit.GameMath;

namespace InfiniteOrbit.Tests
{
    /// <summary>Helpers for building hand-made screens and tiny reel sets so tests can force exact outcomes.</summary>
    public static class TestUtil
    {
        public const Symbol T = Symbol.Ten;
        public const Symbol J = Symbol.Jack;
        public const Symbol Q = Symbol.Queen;
        public const Symbol K = Symbol.King;
        public const Symbol A = Symbol.Ace;
        public const Symbol C = Symbol.Comet;
        public const Symbol N = Symbol.Nebula;
        public const Symbol R = Symbol.Ringworld;
        public const Symbol P = Symbol.Pulsar;
        public const Symbol W = Symbol.Wild;
        public const Symbol S = Symbol.Scatter;
        public const Symbol O = Symbol.Orb;

        public static GameConfig Config() => InfiniteOrbitMath.CreateConfig();

        /// <summary>A screen with no line wins and no specials (reel 2 shares no symbol with reel 1); tests overwrite cells on top of it.</summary>
        public static Symbol[,] Filler()
        {
            return new[,]
            {
                { T, K, T, K, T },
                { J, A, J, A, J },
                { Q, C, Q, C, Q },
            };
        }

        /// <summary>Reel set whose window at stop 0 shows <paramref name="rows"/> (rows top to bottom), with
        /// optional extra stops appended below the window on each reel.</summary>
        public static ReelSet ReelsShowing(Symbol[,] rows, Symbol[] tail = null)
        {
            var strips = new ReelStrip[GameConfig.Reels];
            for (int r = 0; r < GameConfig.Reels; r++)
            {
                var list = new List<Symbol>();
                for (int row = 0; row < GameConfig.Rows; row++) list.Add(rows[row, r]);
                if (tail != null) list.AddRange(tail);
                strips[r] = new ReelStrip(list.ToArray());
            }
            return new ReelSet("Test", strips);
        }

        /// <summary>Default config with its free-spin reels replaced by a fixed screen.</summary>
        public static GameConfig ConfigWithFreeSpinScreen(Symbol[,] rows)
        {
            var c = Config();
            c.FreeSpinReels = ReelsShowing(rows);
            return c;
        }

        /// <summary>Default config whose orb prize table only contains one prize, so prize draws are always 0.</summary>
        public static GameConfig ConfigWithSingleOrbPrize(OrbPrize prize, int orbWeight = 1, int blankWeight = 1)
        {
            var c = Config();
            c.HoldAndSpin.Prizes = new WeightedTable<OrbPrize>(new List<(OrbPrize, int)> { (prize, 1) });
            c.HoldAndSpin.OrbWeight = orbWeight;
            c.HoldAndSpin.BlankWeight = blankWeight;
            return c;
        }

        public static int[] Zeros(int n) => new int[n];

        public static IEnumerable<int> Repeat(int value, int count)
        {
            for (int i = 0; i < count; i++) yield return value;
        }
    }
}
