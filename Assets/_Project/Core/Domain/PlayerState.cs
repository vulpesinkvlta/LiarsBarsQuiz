using System;

namespace LiarsBar.Core.Domain
{
    /// <summary>Состояние игрока за столом. Место (SeatId) не меняется всю партию.</summary>
    public sealed class PlayerState
    {
        public int SeatId { get; }
        public string Name { get; }
        public Hand Hand { get; set; }
        public Revolver Revolver { get; }
        public bool IsAlive { get; set; } = true;
        public PlayerStats Stats { get; }

        public bool HasCards => !Hand.IsEmpty;

        public PlayerState(int seatId, string name, Revolver revolver)
            : this(seatId, name, new Hand(), revolver, true, new PlayerStats()) { }

        public PlayerState(int seatId, string name, Hand hand, Revolver revolver, bool isAlive, PlayerStats stats)
        {
            if (seatId < 0) throw new ArgumentOutOfRangeException(nameof(seatId));
            SeatId = seatId;
            Name = string.IsNullOrWhiteSpace(name) ? $"Player {seatId + 1}" : name;
            Hand = hand ?? throw new ArgumentNullException(nameof(hand));
            Revolver = revolver ?? throw new ArgumentNullException(nameof(revolver));
            IsAlive = isAlive;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        public PlayerState Clone() => new PlayerState(SeatId, Name, Hand.Clone(), Revolver.Clone(), IsAlive, Stats.Clone());

        public override string ToString() => $"#{SeatId} {Name}{(IsAlive ? "" : " (out)")} {Hand}";
    }
}
