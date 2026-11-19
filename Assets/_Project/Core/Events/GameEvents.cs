using System.Collections.Generic;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Views;

namespace LiarsBar.Core.Events
{
    /// <summary>
    /// Событие партии. События публичны: их видят все места (UI, сеть, боты),
    /// поэтому скрытой информации (чужие руки, пометки несыгранных карт, позиция пули) в них нет.
    /// </summary>
    public abstract class GameEvent
    {
    }

    /// <summary>Начался раунд: новый предмет, карты розданы живым игрокам.</summary>
    public sealed class RoundStarted : GameEvent
    {
        public int RoundNumber { get; }
        public string SubjectId { get; }
        public string SubjectTitle { get; }
        public int StarterSeat { get; }

        /// <summary>Сколько карт получил каждый живой игрок (SeatId → число карт).</summary>
        public IReadOnlyDictionary<int, int> HandSizes { get; }

        public RoundStarted(int roundNumber, string subjectId, string subjectTitle, int starterSeat, IReadOnlyDictionary<int, int> handSizes)
        {
            RoundNumber = roundNumber;
            SubjectId = subjectId;
            SubjectTitle = subjectTitle;
            StarterSeat = starterSeat;
            HandSizes = handSizes;
        }

        public override string ToString() => $"RoundStarted #{RoundNumber} {SubjectId}, starter #{StarterSeat}";
    }

    /// <summary>Игрок положил карты на стол (текстом вверх, без пометок).</summary>
    public sealed class CardsPlayed : GameEvent
    {
        public int SeatId { get; }
        public IReadOnlyList<CardFace> Cards { get; }
        public int CardsLeftInHand { get; }

        public CardsPlayed(int seatId, IReadOnlyList<CardFace> cards, int cardsLeftInHand)
        {
            SeatId = seatId;
            Cards = cards;
            CardsLeftInHand = cardsLeftInHand;
        }

        public override string ToString() => $"CardsPlayed #{SeatId} x{Cards.Count}, left {CardsLeftInHand}";
    }

    /// <summary>Кто-то крикнул «Лжец!» на ход другого игрока.</summary>
    public sealed class LiarCalled : GameEvent
    {
        public int AccuserSeat { get; }
        public int AccusedSeat { get; }

        /// <summary>Обвинение было принудительным (карты остались только у сыгравшего).</summary>
        public bool WasForced { get; }

        public LiarCalled(int accuserSeat, int accusedSeat, bool wasForced)
        {
            AccuserSeat = accuserSeat;
            AccusedSeat = accusedSeat;
            WasForced = wasForced;
        }

        public override string ToString() => $"LiarCalled #{AccuserSeat} -> #{AccusedSeat}{(WasForced ? " (forced)" : "")}";
    }

    /// <summary>Карты последнего хода вскрыты: теперь видны пометки ✓/✗ и пояснения.</summary>
    public sealed class FactsRevealed : GameEvent
    {
        public int PlayedBySeat { get; }
        public IReadOnlyList<Fact> Facts { get; }
        public bool LieFound { get; }

        /// <summary>Кто идёт играть в рулетку: сыгравший (ложь найдена) или обвинивший.</summary>
        public int ShooterSeat { get; }

        public FactsRevealed(int playedBySeat, IReadOnlyList<Fact> facts, bool lieFound, int shooterSeat)
        {
            PlayedBySeat = playedBySeat;
            Facts = facts;
            LieFound = lieFound;
            ShooterSeat = shooterSeat;
        }

        public override string ToString() => $"FactsRevealed #{PlayedBySeat} lie={LieFound}, shooter #{ShooterSeat}";
    }

    /// <summary>Игрок нажал на спуск.</summary>
    public sealed class RevolverShooted : GameEvent
    {
        public int SeatId { get; }

        /// <summary>Номер каморы, которую проверили (0-based).</summary>
        public int ChamberIndex { get; }

        /// <summary>Сколько всего раз игрок нажимал на спуск за партию (включая этот).</summary>
        public int ShotsTaken { get; }

        public int Chambers { get; }
        public bool Hit { get; }

        public RevolverShooted(int seatId, int chamberIndex, int shotsTaken, int chambers, bool hit)
        {
            SeatId = seatId;
            ChamberIndex = chamberIndex;
            ShotsTaken = shotsTaken;
            Chambers = chambers;
            Hit = hit;
        }

        public override string ToString() => $"RevolverShooted #{SeatId} {ShotsTaken}/{Chambers} {(Hit ? "BANG" : "click")}";
    }

    /// <summary>Игрок выбыл из партии.</summary>
    public sealed class PlayerEliminated : GameEvent
    {
        public int SeatId { get; }

        public PlayerEliminated(int seatId) => SeatId = seatId;

        public override string ToString() => $"PlayerEliminated #{SeatId}";
    }

    /// <summary>Ход перешёл к игроку.</summary>
    public sealed class TurnChanged : GameEvent
    {
        public int SeatId { get; }

        /// <summary>Игрок обязан крикнуть «Лжец!».</summary>
        public bool ForcedAccusation { get; }

        public TurnChanged(int seatId, bool forcedAccusation)
        {
            SeatId = seatId;
            ForcedAccusation = forcedAccusation;
        }

        public override string ToString() => $"TurnChanged #{SeatId}{(ForcedAccusation ? " (forced)" : "")}";
    }

    /// <summary>Живым остался один игрок.</summary>
    public sealed class MatchEnded : GameEvent
    {
        public int WinnerSeat { get; }
        public int RoundsPlayed { get; }

        public MatchEnded(int winnerSeat, int roundsPlayed)
        {
            WinnerSeat = winnerSeat;
            RoundsPlayed = roundsPlayed;
        }

        public override string ToString() => $"MatchEnded winner #{WinnerSeat} after {RoundsPlayed} rounds";
    }
}
