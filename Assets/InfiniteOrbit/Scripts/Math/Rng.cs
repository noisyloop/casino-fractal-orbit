using System;
using System.Collections.Generic;

namespace InfiniteOrbit.GameMath
{
    /// <summary>
    /// Random source used by the math module. The math only ever asks for a uniform integer in [0, max),
    /// which keeps the RNG contract small and makes outcomes easy to script in tests.
    /// </summary>
    public interface IRng
    {
        int NextInt(int maxExclusive);
    }

    /// <summary>
    /// xoshiro256** PRNG (Blackman/Vigna) seeded via SplitMix64. Fast, high quality, fully deterministic
    /// for a given seed. Range reduction uses Lemire's unbiased multiply-and-reject method.
    /// </summary>
    public sealed class Xoshiro256StarStar : IRng
    {
        private ulong _s0, _s1, _s2, _s3;

        public ulong Seed { get; }

        public Xoshiro256StarStar(ulong seed)
        {
            Seed = seed;
            ulong sm = seed;
            _s0 = SplitMix64(ref sm);
            _s1 = SplitMix64(ref sm);
            _s2 = SplitMix64(ref sm);
            _s3 = SplitMix64(ref sm);
        }

        /// <summary>Derives an independent stream seed from a base seed and a stream index (used for parallel simulation chunks).</summary>
        public static ulong DeriveSeed(ulong baseSeed, ulong streamIndex)
        {
            ulong sm = baseSeed ^ (0x9E3779B97F4A7C15UL * (streamIndex + 1));
            return SplitMix64(ref sm);
        }

        private static ulong SplitMix64(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextULong()
        {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint range = (uint)maxExclusive;
            ulong m = (ulong)NextUInt() * range;
            uint low = (uint)m;
            if (low < range)
            {
                uint threshold = (uint)(-(int)range) % range;
                while (low < threshold)
                {
                    m = (ulong)NextUInt() * range;
                    low = (uint)m;
                }
            }
            return (int)(m >> 32);
        }
    }

    /// <summary>Plays back a fixed list of values. Used by tests and replay tooling to force exact outcomes.</summary>
    public sealed class ScriptedRng : IRng
    {
        private readonly Queue<int> _values;

        public ScriptedRng(IEnumerable<int> values)
        {
            _values = new Queue<int>(values);
        }

        public int Remaining => _values.Count;

        public int NextInt(int maxExclusive)
        {
            if (_values.Count == 0)
                throw new InvalidOperationException("ScriptedRng ran out of values.");
            int v = _values.Dequeue();
            if (v < 0 || v >= maxExclusive)
                throw new InvalidOperationException($"Scripted value {v} is outside [0, {maxExclusive}).");
            return v;
        }
    }
}
