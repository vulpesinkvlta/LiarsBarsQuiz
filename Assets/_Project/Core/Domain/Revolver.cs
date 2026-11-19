using System;
using LiarsBar.Core.Randomness;

namespace LiarsBar.Core.Domain
{
    /// <summary>
    /// Револьвер для русской рулетки: пуля в одной из камор, позиция скрыта от игроков.
    /// Каждое нажатие проверяет очередную камору, поэтому выстрел гарантирован не позже последней каморы.
    /// </summary>
    public sealed class Revolver
    {
        public const int DefaultChambers = 6;

        public int Chambers { get; }
        public int BulletIndex { get; }

        /// <summary>Сколько раз уже нажали на спуск (и номер следующей каморы).</summary>
        public int Shooted { get; private set; }

        /// <summary>Пуля уже вылетела: дальше стрелять нельзя.</summary>
        public bool IsFired { get; private set; }

        public int RemainingChambers => Chambers - Shooted;

        public Revolver(int bulletIndex, int chambers = DefaultChambers, int shooted = 0)
        {
            if (chambers < 1) throw new ArgumentOutOfRangeException(nameof(chambers));
            if (bulletIndex < 0 || bulletIndex >= chambers) throw new ArgumentOutOfRangeException(nameof(bulletIndex));
            if (shooted < 0 || shooted > bulletIndex) throw new ArgumentOutOfRangeException(nameof(shooted));

            Chambers = chambers;
            BulletIndex = bulletIndex;
            Shooted = shooted;
        }

        public static Revolver CreateRandom(IRandom random, int chambers = DefaultChambers)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return new Revolver(random.Next(chambers), chambers);
        }

        /// <summary>Нажать на спуск. Возвращает true, если в этой каморе была пуля.</summary>
        public bool Shot()
        {
            if (IsFired) throw new InvalidOperationException("Revolver has already fired.");

            var hit = Shooted == BulletIndex;
            Shooted++;
            IsFired = hit;
            return hit;
        }

        public Revolver Clone()
        {
            var copy = new Revolver(BulletIndex, Chambers, IsFired ? BulletIndex : Shooted);
            if (IsFired) copy.Shot();
            return copy;
        }
    }
}
