using System;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Engine
{
    /// <summary>Судит обвинение: хотя бы одна ✗ → в рулетку играет сыгравший, иначе — обвинивший.</summary>
    public static class AccusationResolver
    {
        public readonly struct Verdict
        {
            public bool LieFound { get; }
            public int ShooterSeat { get; }

            public Verdict(bool lieFound, int shooterSeat)
            {
                LieFound = lieFound;
                ShooterSeat = shooterSeat;
            }
        }

        public static Verdict Resolve(TablePlay accusedPlay, int accuserSeat)
        {
            if (accusedPlay == null) throw new ArgumentNullException(nameof(accusedPlay));
            return accusedPlay.HasLie
                ? new Verdict(true, accusedPlay.SeatId)
                : new Verdict(false, accuserSeat);
        }
    }
}
