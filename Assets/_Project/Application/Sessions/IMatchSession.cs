using System;
using System.Collections.Generic;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Sessions
{
    /// <summary>
    /// Партия глазами одного устройства. Presenter знает только этот интерфейс и не знает режима:
    /// офлайн, hot-seat, хост и гость онлайн взаимозаменяемы.
    /// </summary>
    public interface IMatchSession : IDisposable
    {
        /// <summary>Новый снимок для текущего зрителя. Приходит после EventsRaised.</summary>
        event Action<PlayerView> ViewChanged;

        /// <summary>Публичные события последнего действия (или старта партии).</summary>
        event Action<IReadOnlyList<GameEvent>> EventsRaised;

        /// <summary>Действие с этого устройства отклонено правилами; состояние не изменилось.</summary>
        event Action<ActionError> ActionRejected;

        /// <summary>Чьими глазами смотрит устройство (-1 — зритель).</summary>
        int ViewerSeat { get; }

        /// <summary>Последний снимок для зрителя (null до старта).</summary>
        PlayerView CurrentView { get; }

        /// <summary>Начать партию: разослать события старта и первый снимок.</summary>
        void Start();

        /// <summary>Действие человека на этом устройстве.</summary>
        void Submit(IGameAction action);

        /// <summary>
        /// UI что-то показывает (окно проверки, анимации): следующее действие не запрашивается,
        /// пока не придёт false. Так боты не ходят «за кадром».
        /// </summary>
        void SetPresentationBusy(bool busy);
    }
}
