using System;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Seats
{
    /// <summary>
    /// Место человека на этом устройстве. Сам ничего не решает: запоминает запрос сессии
    /// и ждёт, пока UI через IMatchSession.Submit передаст действие.
    /// </summary>
    public sealed class LocalHumanSeat : ISeatController
    {
        private Action<IGameAction> _pending;

        public int SeatId { get; }
        public string Name { get; }

        /// <summary>Сессия ждёт от этого места действие.</summary>
        public bool IsWaiting => _pending != null;

        /// <summary>Сессия попросила ход (пригодится hot-seat для заставки «передай устройство»).</summary>
        public event Action<PlayerView> ActionRequested;

        public LocalHumanSeat(int seatId, string name)
        {
            SeatId = seatId;
            Name = name;
        }

        public void RequestAction(PlayerView view, Action<IGameAction> submit)
        {
            _pending = submit ?? throw new ArgumentNullException(nameof(submit));
            ActionRequested?.Invoke(view);
        }

        /// <summary>Передать действие из UI. false — от этого места сейчас ничего не ждут.</summary>
        public bool TrySubmit(IGameAction action)
        {
            if (action == null || action.SeatId != SeatId || _pending == null) return false;
            var submit = _pending;
            _pending = null;
            submit(action);
            return true;
        }

        public void Cancel() => _pending = null;
    }
}
