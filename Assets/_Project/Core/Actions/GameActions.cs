using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Actions
{
    /// <summary>Действие игрока за столом. Единственный способ изменить партию — MatchEngine.Apply(action).</summary>
    public interface IGameAction
    {
        /// <summary>Место игрока, который совершает действие.</summary>
        int SeatId { get; }
    }

    /// <summary>Положить на стол 1–2 карты из своей руки.</summary>
    public sealed class PlayCardsAction : IGameAction
    {
        public int SeatId { get; }
        public IReadOnlyList<string> CardIds { get; }

        public PlayCardsAction(int seatId, IEnumerable<string> cardIds)
        {
            SeatId = seatId;
            CardIds = (cardIds ?? throw new ArgumentNullException(nameof(cardIds))).ToList().AsReadOnly();
        }

        public PlayCardsAction(int seatId, params string[] cardIds) : this(seatId, (IEnumerable<string>)cardIds) { }

        public override string ToString() => $"#{SeatId} plays [{string.Join(", ", CardIds)}]";
    }

    /// <summary>Крикнуть «Лжец!» на последний ход на столе.</summary>
    public sealed class CallLiarAction : IGameAction
    {
        public int SeatId { get; }

        public CallLiarAction(int seatId) => SeatId = seatId;

        public override string ToString() => $"#{SeatId} calls liar";
    }
}
