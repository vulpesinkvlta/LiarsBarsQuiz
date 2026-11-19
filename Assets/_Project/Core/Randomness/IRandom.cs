using System.Collections.Generic;

namespace LiarsBar.Core.Randomness
{
    /// <summary>Источник случайности. В тестах подставляется SeededRandom с фиксированным seed.</summary>
    public interface IRandom
    {
        /// <summary>Целое в [0, maxExclusive).</summary>
        int Next(int maxExclusive);

        /// <summary>Целое в [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>Дробное в [0, 1).</summary>
        double NextDouble();
    }

    public static class RandomExtensions
    {
        /// <summary>Перемешивание Фишера — Йетса на месте.</summary>
        public static void Shuffle<T>(this IRandom random, IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static T Pick<T>(this IRandom random, IReadOnlyList<T> list) => list[random.Next(list.Count)];

        public static bool Chance(this IRandom random, double probability) => random.NextDouble() < probability;
    }
}
