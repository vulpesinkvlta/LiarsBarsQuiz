using System;
using System.Collections.Generic;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Как рисовать карту. Mark: null — без пометки (чужие карты на столе), true — ✓, false — ✗.</summary>
    public sealed class CardModel
    {
        public string Id { get; }
        public string Text { get; }
        public bool? Mark { get; }
        public bool Selected { get; }
        public bool Interactable { get; }

        public CardModel(string id, string text, bool? mark, bool selected, bool interactable)
        {
            Id = id;
            Text = text;
            Mark = mark;
            Selected = selected;
            Interactable = interactable;
        }
    }

    /// <summary>Место за столом: имя, карты, выстрелы x/6, жив/выбыл, чей ход.</summary>
    public sealed class SeatModel
    {
        public int SeatId { get; }
        public string Name { get; }
        public int CardCount { get; }
        public int ShotsTaken { get; }
        public int Chambers { get; }
        public bool IsAlive { get; }
        public bool IsCurrent { get; }
        public bool IsViewer { get; }

        public SeatModel(int seatId, string name, int cardCount, int shotsTaken, int chambers, bool isAlive, bool isCurrent, bool isViewer)
        {
            SeatId = seatId;
            Name = name;
            CardCount = cardCount;
            ShotsTaken = shotsTaken;
            Chambers = chambers;
            IsAlive = isAlive;
            IsCurrent = isCurrent;
            IsViewer = isViewer;
        }
    }

    /// <summary>Что умеет серый стол. View без логики: только показывает и сообщает о нажатиях.</summary>
    public interface ITableView
    {
        event Action PlayClicked;
        event Action LiarClicked;
        event Action<string> CardClicked;
        event Action RevealClosed;

        void SetHeader(string text);
        void SetSeats(IReadOnlyList<SeatModel> seats);
        void SetTablePlay(string caption, IReadOnlyList<CardModel> cards);
        void SetHand(IReadOnlyList<CardModel> cards);
        void SetActions(bool canPlay, bool canCallLiar, bool visible);
        void SetHint(string text);
        void AppendLog(string line);
        void ShowReveal(string text);
        void HideReveal();
        void ShowGameOver(string text);
    }
}
