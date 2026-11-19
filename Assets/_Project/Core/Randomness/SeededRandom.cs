using System;

namespace LiarsBar.Core.Randomness
{
    /// <summary>
    /// Детерминированный генератор (xoshiro128**, инициализация через SplitMix64).
    /// Собственная реализация вместо System.Random: последовательность одинакова в Mono, IL2CPP и .NET,
    /// так что seed воспроизводит партию где угодно.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        private uint _s0, _s1, _s2, _s3;

        public int Seed { get; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            var x = (ulong)(uint)seed;
            _s0 = (uint)SplitMix(ref x);
            _s1 = (uint)SplitMix(ref x);
            _s2 = (uint)SplitMix(ref x);
            _s3 = (uint)SplitMix(ref x);
        }

        /// <summary>Генератор со случайным seed (по времени и счётчику).</summary>
        public static SeededRandom CreateUnseeded() => new SeededRandom(Environment.TickCount ^ Guid.NewGuid().GetHashCode());

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            // Отбрасывание хвоста убирает смещение по модулю.
            var bound = (uint)maxExclusive;
            var threshold = (uint)(-(int)bound) % bound;
            while (true)
            {
                var r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        public double NextDouble() => (NextUInt() >> 8) * (1.0 / (1u << 24));

        private uint NextUInt()
        {
            var result = RotL(_s1 * 5, 7) * 9;
            var t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 11);
            return result;
        }

        private static uint RotL(uint x, int k) => (x << k) | (x >> (32 - k));

        private static ulong SplitMix(ref ulong x)
        {
            var z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
