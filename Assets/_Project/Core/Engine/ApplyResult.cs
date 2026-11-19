using System;
using System.Collections.Generic;
using LiarsBar.Core.Events;

namespace LiarsBar.Core.Engine
{
    public enum ActionErrorCode
    {
        None,
        NullAction,
        UnknownAction,
        MatchNotInProgress,
        UnknownSeat,
        PlayerEliminated,
        NotYourTurn,
        MustCallLiar,
        CannotCallLiarOnFirstTurn,
        InvalidCardCount,
        DuplicateCards,
        CardsNotInHand,
        InvalidSettings,
        NotEnoughContent
    }

    /// <summary>Почему действие отклонено. Сообщение — для логов, UI показывает текст по коду.</summary>
    public sealed class ActionError
    {
        public ActionErrorCode Code { get; }
        public string Message { get; }

        public ActionError(ActionErrorCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"{Code}: {Message}";
    }

    /// <summary>Итог Start/Apply: либо успех со списком событий, либо ошибка валидации (состояние не изменилось).</summary>
    public sealed class ApplyResult
    {
        public bool IsSuccess => Error == null;
        public IReadOnlyList<GameEvent> Events { get; }
        public ActionError Error { get; }

        private ApplyResult(IReadOnlyList<GameEvent> events, ActionError error)
        {
            Events = events;
            Error = error;
        }

        public static ApplyResult Success(IReadOnlyList<GameEvent> events) =>
            new ApplyResult(events ?? throw new ArgumentNullException(nameof(events)), null);

        public static ApplyResult Fail(ActionErrorCode code, string message) =>
            new ApplyResult(Array.Empty<GameEvent>(), new ActionError(code, message));

        public static ApplyResult Fail(ActionError error) =>
            new ApplyResult(Array.Empty<GameEvent>(), error ?? throw new ArgumentNullException(nameof(error)));

        public override string ToString() => IsSuccess ? $"OK ({Events.Count} events)" : $"FAIL {Error}";
    }
}
