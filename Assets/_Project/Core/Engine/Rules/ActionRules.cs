using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Engine.Rules
{
    /// <summary>Валидатор хода. Возвращает null, если действие допустимо. Новое правило = новый класс (OCP).</summary>
    public interface IActionRule
    {
        ActionError Validate(MatchState state, IGameAction action);
    }

    /// <summary>Действует живой игрок, сейчас его ход и партия идёт.</summary>
    public sealed class IsPlayersTurnRule : IActionRule
    {
        public ActionError Validate(MatchState state, IGameAction action)
        {
            if (state.Phase != MatchPhase.AwaitingAction)
                return new ActionError(ActionErrorCode.MatchNotInProgress, $"Phase is {state.Phase}.");
            if (!state.TryGetPlayer(action.SeatId, out var player))
                return new ActionError(ActionErrorCode.UnknownSeat, $"No player at seat {action.SeatId}.");
            if (!player.IsAlive)
                return new ActionError(ActionErrorCode.PlayerEliminated, $"Seat {action.SeatId} is eliminated.");
            if (state.CurrentSeat != action.SeatId)
                return new ActionError(ActionErrorCode.NotYourTurn, $"Current seat is {state.CurrentSeat}, not {action.SeatId}.");
            return null;
        }
    }

    /// <summary>При принудительном обвинении можно только крикнуть «Лжец!».</summary>
    public sealed class ForcedAccusationRule : IActionRule
    {
        public ActionError Validate(MatchState state, IGameAction action)
        {
            if (state.ForcedAccusation && action is PlayCardsAction)
                return new ActionError(ActionErrorCode.MustCallLiar, "Cards are left only at the last player: you must call liar.");
            return null;
        }
    }

    /// <summary>На первом ходу раунда обвинять некого.</summary>
    public sealed class CanCallLiarRule : IActionRule
    {
        public ActionError Validate(MatchState state, IGameAction action)
        {
            if (action is CallLiarAction && state.LastPlay == null)
                return new ActionError(ActionErrorCode.CannotCallLiarOnFirstTurn, "Nothing to accuse on the first turn of a round.");
            return null;
        }
    }

    /// <summary>За ход кладут от MinCardsPerPlay до MaxCardsPerPlay карт.</summary>
    public sealed class CardCountInRangeRule : IActionRule
    {
        private readonly GameRulesConfig _config;

        public CardCountInRangeRule(GameRulesConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

        public ActionError Validate(MatchState state, IGameAction action)
        {
            if (!(action is PlayCardsAction play)) return null;
            var count = play.CardIds.Count;
            if (count < _config.MinCardsPerPlay || count > _config.MaxCardsPerPlay)
                return new ActionError(ActionErrorCode.InvalidCardCount,
                    $"Play {_config.MinCardsPerPlay}-{_config.MaxCardsPerPlay} cards, got {count}.");
            return null;
        }
    }

    /// <summary>Все карты есть в руке, без повторов.</summary>
    public sealed class CardsInHandRule : IActionRule
    {
        public ActionError Validate(MatchState state, IGameAction action)
        {
            if (!(action is PlayCardsAction play)) return null;
            if (play.CardIds.Distinct().Count() != play.CardIds.Count)
                return new ActionError(ActionErrorCode.DuplicateCards, "The same card is played twice.");

            var hand = state.GetPlayer(play.SeatId).Hand;
            var missing = play.CardIds.Where(id => !hand.Contains(id)).ToList();
            if (missing.Count > 0)
                return new ActionError(ActionErrorCode.CardsNotInHand, $"Not in hand: {string.Join(", ", missing)}.");
            return null;
        }
    }

    public static class ActionRules
    {
        /// <summary>Набор правил MVP. Порядок важен: сначала «чей ход», потом детали действия.</summary>
        public static IReadOnlyList<IActionRule> Default(GameRulesConfig config) => new IActionRule[]
        {
            new IsPlayersTurnRule(),
            new ForcedAccusationRule(),
            new CanCallLiarRule(),
            new CardCountInRangeRule(config),
            new CardsInHandRule()
        };
    }
}
