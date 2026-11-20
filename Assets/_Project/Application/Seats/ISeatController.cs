using System;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Seats
{
    /// <summary>
    /// Кто управляет местом за столом: человек на этом устройстве, бот или удалённый игрок.
    /// Сессия просит действие, контроллер когда-нибудь вызывает submit (или его отменяют).
    /// </summary>
    public interface ISeatController
    {
        /// <param name="view">Снимок глазами этого места (только своя рука).</param>
        /// <param name="submit">Куда отдать действие. Повторный или поздний вызов сессия игнорирует.</param>
        void RequestAction(PlayerView view, Action<IGameAction> submit);

        /// <summary>Запрос больше не актуален (ход сделан, партия закрыта).</summary>
        void Cancel();
    }
}
