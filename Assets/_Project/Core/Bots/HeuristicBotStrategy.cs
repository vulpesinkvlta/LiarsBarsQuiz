using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;

namespace LiarsBar.Core.Bots
{
    /// <summary>
    /// Эвристика из документа MVP (раздел 6).
    /// «Лжец!»: принудительно → обвиняет; иначе оценивает P(ложь в ходе) = 1 − П(1 − pᵢ) и сравнивает
    /// с порогом профиля (+ThresholdPerShot за каждый уже сделанный выстрел).
    /// Ход: есть ✓ → одна ✓ (+ самая сложная ✗ с шансом MixLieChance); только ✗ → самая сложная ✗.
    /// </summary>
    public sealed class HeuristicBotStrategy : IBotStrategy
    {
        private readonly IFactKnowledge _knowledge;
        private readonly int _salt;
        private readonly int _maxCardsPerPlay;

        /// <param name="knowledge">Справочник фактов («эрудиция»).</param>
        /// <param name="salt">Соль знаний: разная в разных партиях, чтобы бот на месте N не знал всегда одно и то же.</param>
        /// <param name="maxCardsPerPlay">Сколько карт можно положить за ход (GameRulesConfig.MaxCardsPerPlay).</param>
        public HeuristicBotStrategy(IFactKnowledge knowledge, int salt = 0, int maxCardsPerPlay = 2)
        {
            _knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
            _salt = salt;
            _maxCardsPerPlay = Math.Max(1, maxCardsPerPlay);
        }

        public IGameAction Decide(PlayerView view, BotProfile profile, IRandom random)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!view.IsMyTurn) throw new InvalidOperationException($"Seat {view.ViewerSeat}: not this bot's turn.");

            if (view.CanCallLiar)
            {
                if (view.ForcedAccusation || !view.CanPlayCards) return new CallLiarAction(view.ViewerSeat);
                if (ShouldCallLiar(view, profile)) return new CallLiarAction(view.ViewerSeat);
            }

            if (!view.CanPlayCards) throw new InvalidOperationException($"Seat {view.ViewerSeat}: no legal action.");
            return new PlayCardsAction(view.ViewerSeat, ChooseCards(view.MyHand, profile, random));
        }

        public bool ShouldCallLiar(PlayerView view, BotProfile profile)
        {
            var shots = view.GetSeat(view.ViewerSeat)?.ShotsTaken ?? 0;
            return EstimateLieProbability(view, profile) > profile.ThresholdAfterShots(shots);
        }

        /// <summary>Оценка «в последнем ходе есть ложь» глазами этого бота; 0, если на столе пусто.</summary>
        public double EstimateLieProbability(PlayerView view, BotProfile profile)
        {
            var play = view.LastPlay;
            if (play == null || play.Cards.Count == 0) return 0;

            var basePrior = (double)profile.UnknownLieChance;
            var accused = view.GetSeat(play.SeatId);
            if (accused != null && accused.CardCount <= profile.LowCardsLimit) basePrior += profile.LowCardsBonus;
            if (play.Cards.Count >= 2) basePrior += profile.TwoCardsBonus;
            basePrior = Clamp01(basePrior);

            var allTrue = 1.0;
            foreach (var card in play.Cards)
            {
                var p = basePrior;
                if (_knowledge.TryGetFact(card.Id, out var fact) && Knows(view.ViewerSeat, fact, profile))
                    p = fact.IsTrue ? 0 : 1;
                allTrue *= 1 - p;
            }

            return 1 - allTrue;
        }

        /// <summary>Знает ли бот на месте seatId этот факт. Детерминировано по (соль, место, факт).</summary>
        public bool Knows(int seatId, Fact fact, BotProfile profile) =>
            BotRecall.Roll(BotRecall.BotKey(_salt, seatId), fact.Id) < profile.KnowChance(fact.Difficulty);

        private IReadOnlyList<string> ChooseCards(IReadOnlyList<HandCard> hand, BotProfile profile, IRandom random)
        {
            var trues = hand.Where(c => c.IsTrue).ToList();
            var falses = hand.Where(c => !c.IsTrue).ToList();

            if (trues.Count == 0) return new[] { Hardest(falses, random).Id };

            var cards = new List<string> { random.Pick(trues).Id };
            if (_maxCardsPerPlay >= 2 && falses.Count > 0 && random.Chance(profile.MixLieChance))
                cards.Add(Hardest(falses, random).Id);
            return cards;
        }

        /// <summary>Карта с самой высокой сложностью (её труднее разоблачить); при равенстве — случайная.</summary>
        private static HandCard Hardest(IReadOnlyList<HandCard> cards, IRandom random)
        {
            var max = cards.Max(c => c.Difficulty);
            return random.Pick(cards.Where(c => c.Difficulty == max).ToList());
        }

        private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }
}
