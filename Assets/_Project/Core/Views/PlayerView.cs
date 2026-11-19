using System.Collections.Generic;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Views
{
    /// <summary>Карта лицом вверх: текст без пометки ✓/✗. Так карты видят все на столе.</summary>
    public sealed class CardFace
    {
        public string Id { get; }
        public string Text { get; }

        public CardFace(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public static CardFace From(Fact fact) => new CardFace(fact.Id, fact.Text);

        public override string ToString() => Id;
    }

    /// <summary>Карта в своей руке: владелец видит пометку и сложность.</summary>
    public sealed class HandCard
    {
        public string Id { get; }
        public string Text { get; }
        public bool IsTrue { get; }
        public Difficulty Difficulty { get; }

        public HandCard(string id, string text, bool isTrue, Difficulty difficulty)
        {
            Id = id;
            Text = text;
            IsTrue = isTrue;
            Difficulty = difficulty;
        }

        public static HandCard From(Fact fact) => new HandCard(fact.Id, fact.Text, fact.IsTrue, fact.Difficulty);

        public override string ToString() => $"{Id} [{(IsTrue ? "✓" : "✗")}]";
    }

    /// <summary>Публичная информация о месте за столом.</summary>
    public sealed class SeatInfo
    {
        public int SeatId { get; }
        public string Name { get; }
        public bool IsAlive { get; }
        public int CardCount { get; }

        /// <summary>Сколько раз игрок уже нажимал на спуск (x из Chambers).</summary>
        public int ShotsTaken { get; }

        public int Chambers { get; }

        /// <summary>Статистика партии; открывается только после её окончания, иначе null.</summary>
        public PlayerStats Stats { get; }

        public SeatInfo(int seatId, string name, bool isAlive, int cardCount, int shotsTaken, int chambers, PlayerStats stats)
        {
            SeatId = seatId;
            Name = name;
            IsAlive = isAlive;
            CardCount = cardCount;
            ShotsTaken = shotsTaken;
            Chambers = chambers;
            Stats = stats;
        }
    }

    /// <summary>Ход на столе в публичном виде.</summary>
    public sealed class TablePlayView
    {
        public int SeatId { get; }
        public IReadOnlyList<CardFace> Cards { get; }

        public TablePlayView(int seatId, IReadOnlyList<CardFace> cards)
        {
            SeatId = seatId;
            Cards = cards;
        }
    }

    /// <summary>
    /// Снимок партии глазами одного места: публичное состояние + своя рука с пометками.
    /// Именно его получают UI, сеть и боты — чужие руки и позиция пули сюда не попадают.
    /// </summary>
    public sealed class PlayerView
    {
        /// <summary>Чьими глазами снимок; -1 — зритель (без руки).</summary>
        public int ViewerSeat { get; }

        public MatchPhase Phase { get; }
        public int RoundNumber { get; }
        public string SubjectId { get; }
        public string SubjectTitle { get; }
        public int CurrentSeat { get; }
        public bool ForcedAccusation { get; }
        public int WinnerSeat { get; }

        public IReadOnlyList<SeatInfo> Seats { get; }

        /// <summary>Последний ход в раунде; null — первый ход раунда.</summary>
        public TablePlayView LastPlay { get; }

        /// <summary>Своя рука (пустая у зрителя и выбывших).</summary>
        public IReadOnlyList<HandCard> MyHand { get; }

        public bool IsMyTurn => Phase == MatchPhase.AwaitingAction && ViewerSeat >= 0 && CurrentSeat == ViewerSeat;
        public bool IsFirstTurnOfRound => LastPlay == null;

        /// <summary>Можно сыграть карты (число карт за ход проверяет GameRulesConfig).</summary>
        public bool CanPlayCards => IsMyTurn && !ForcedAccusation && MyHand.Count > 0;

        public bool CanCallLiar => IsMyTurn && LastPlay != null;

        public PlayerView(int viewerSeat, MatchPhase phase, int roundNumber, string subjectId, string subjectTitle,
            int currentSeat, bool forcedAccusation, int winnerSeat, IReadOnlyList<SeatInfo> seats,
            TablePlayView lastPlay, IReadOnlyList<HandCard> myHand)
        {
            ViewerSeat = viewerSeat;
            Phase = phase;
            RoundNumber = roundNumber;
            SubjectId = subjectId;
            SubjectTitle = subjectTitle;
            CurrentSeat = currentSeat;
            ForcedAccusation = forcedAccusation;
            WinnerSeat = winnerSeat;
            Seats = seats;
            LastPlay = lastPlay;
            MyHand = myHand;
        }

        public SeatInfo GetSeat(int seatId)
        {
            foreach (var seat in Seats)
                if (seat.SeatId == seatId) return seat;
            return null;
        }
    }
}
