using System;
using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>Discrete weighted distribution. Picks consume exactly one RNG draw in [0, TotalWeight).</summary>
    public sealed class WeightedTable<T>
    {
        private readonly T[] _items;
        private readonly int[] _weights;
        private readonly int[] _cumulative;

        public int TotalWeight { get; }
        public int Count => _items.Length;

        public WeightedTable(IReadOnlyList<(T item, int weight)> entries)
        {
            if (entries == null || entries.Count == 0) throw new ArgumentException("Weighted table needs entries.");
            _items = new T[entries.Count];
            _weights = new int[entries.Count];
            _cumulative = new int[entries.Count];
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].weight <= 0) throw new ArgumentException("Weights must be positive.");
                _items[i] = entries[i].item;
                _weights[i] = entries[i].weight;
                total += entries[i].weight;
                _cumulative[i] = total;
            }
            TotalWeight = total;
        }

        public T this[int index] => _items[index];
        public int WeightAt(int index) => _weights[index];
        public double ProbabilityAt(int index) => (double)_weights[index] / TotalWeight;

        public T Pick(IRng rng)
        {
            int r = rng.NextInt(TotalWeight);
            // Tables are tiny (< 20 entries); linear scan beats binary search here.
            for (int i = 0; i < _cumulative.Length; i++)
                if (r < _cumulative[i]) return _items[i];
            return _items[_items.Length - 1];
        }
    }
}
