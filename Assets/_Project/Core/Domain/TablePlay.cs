using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Domain
{
    /// <summary>Ход на столе: кто и какие карты положил. Пометки ✓/✗ видны только после обвинения.</summary>
    public sealed class TablePlay
    {
        public int SeatId { get; }
        public IReadOnlyList<Fact> Cards { get; }

        public bool HasLie => Cards.Any(c => !c.IsTrue);
        public int LieCount => Cards.Count(c => !c.IsTrue);

        public TablePlay(int seatId, IEnumerable<Fact> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var list = cards.ToList();
            if (list.Count == 0) throw new ArgumentException("A play must contain at least one card.", nameof(cards));

            SeatId = seatId;
            Cards = list.AsReadOnly();
        }

        public override string ToString() => $"#{SeatId} played {string.Join(", ", Cards.Select(c => c.Id))}";
    }
}
