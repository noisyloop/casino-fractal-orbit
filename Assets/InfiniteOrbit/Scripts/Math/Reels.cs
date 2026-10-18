using System;
using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>A single physical reel strip. Every stop is equally likely; symbol weights come from repetition on the strip.</summary>
    public sealed class ReelStrip
    {
        private readonly Symbol[] _symbols;

        public ReelStrip(Symbol[] symbols)
        {
            if (symbols == null || symbols.Length < GameConfig.Rows)
                throw new ArgumentException("Reel strip must be at least as long as the visible window.");
            _symbols = (Symbol[])symbols.Clone();
        }

        public int Length => _symbols.Length;

        /// <summary>Symbol at a strip index, wrapping cyclically.</summary>
        public Symbol this[int index] => _symbols[((index % _symbols.Length) + _symbols.Length) % _symbols.Length];

        public IReadOnlyList<Symbol> Symbols => _symbols;

        public int CountOf(Symbol s)
        {
            int n = 0;
            for (int i = 0; i < _symbols.Length; i++)
                if (_symbols[i] == s) n++;
            return n;
        }
    }

    /// <summary>The five reel strips used for one game mode (base game or free spins).</summary>
    public sealed class ReelSet
    {
        private readonly ReelStrip[] _reels;

        public string Name { get; }

        public ReelSet(string name, ReelStrip[] reels)
        {
            if (reels == null || reels.Length != GameConfig.Reels)
                throw new ArgumentException($"A reel set needs exactly {GameConfig.Reels} strips.");
            Name = name;
            _reels = (ReelStrip[])reels.Clone();
        }

        public ReelStrip this[int reel] => _reels[reel];
        public int Count => _reels.Length;
    }

    /// <summary>
    /// Lays out a reel strip from per-symbol counts. The layout is a seeded shuffle followed by a deterministic
    /// repair pass, so the same counts and seed always yield the same strip. Constraints:
    /// <list type="bullet">
    /// <item>Scatters are at least <c>minScatterSpacing</c> stops apart (cyclically), so at most one shows per reel.</item>
    /// <item>Orbs are laid out in the requested stack sizes so 6+ orb screens are reachable.</item>
    /// </list>
    /// </summary>
    public static class ReelStripBuilder
    {
        public static ReelStrip Build(IReadOnlyList<(Symbol symbol, int count)> counts, ulong seed, int minScatterSpacing = 3, int orbStack = 1)
        {
            var rng = new Xoshiro256StarStar(seed);

            // Units are placed as blocks: orbs may be stacked, everything else is a single stop.
            var units = new List<Symbol[]>();
            foreach (var (symbol, count) in counts)
            {
                if (count < 0) throw new ArgumentException("Negative symbol count.");
                if (symbol == Symbol.Orb && orbStack > 1)
                {
                    int remaining = count;
                    while (remaining > 0)
                    {
                        int size = Math.Min(orbStack, remaining);
                        var block = new Symbol[size];
                        for (int i = 0; i < size; i++) block[i] = Symbol.Orb;
                        units.Add(block);
                        remaining -= size;
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++) units.Add(new[] { symbol });
                }
            }

            for (int attempt = 0; attempt < 10000; attempt++)
            {
                for (int i = units.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(i + 1);
                    var tmp = units[i];
                    units[i] = units[j];
                    units[j] = tmp;
                }

                var strip = new List<Symbol>();
                foreach (var u in units) strip.AddRange(u);
                if (SatisfiesConstraints(strip, minScatterSpacing))
                    return new ReelStrip(strip.ToArray());
            }

            throw new InvalidOperationException("Could not lay out reel strip under the given constraints.");
        }

        private static bool SatisfiesConstraints(List<Symbol> strip, int minScatterSpacing)
        {
            int n = strip.Count;
            var positions = new List<int>();
            for (int i = 0; i < n; i++)
                if (strip[i] == Symbol.Scatter) positions.Add(i);
            for (int i = 0; i < positions.Count; i++)
            {
                int next = positions[(i + 1) % positions.Count];
                int gap = (next - positions[i] + n) % n;
                if (positions.Count > 1 && gap < minScatterSpacing) return false;
            }
            return true;
        }
    }
}
