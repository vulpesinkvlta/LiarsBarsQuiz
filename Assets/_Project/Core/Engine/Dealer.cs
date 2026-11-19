using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Config;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Randomness;

namespace LiarsBar.Core.Engine
{
    /// <summary>
    /// Раздаёт карты предмета живым игрокам. В каждой руке минимум MinTrueInHand ✓ и MinFalseInHand ✗.
    /// Сначала честная пересдача (до MaxDealAttempts), затем гарантированная сборка руки,
    /// чтобы раздача не падала на неудачном seed.
    /// </summary>
    public sealed class Dealer
    {
        private readonly GameRulesConfig _config;
        private readonly IRandom _random;

        public Dealer(GameRulesConfig config, IRandom random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Хватит ли фактов предмета, чтобы раздать playerCount игрокам с соблюдением состава руки.</summary>
        public bool CanDeal(Subject subject, int playerCount)
        {
            if (subject == null || playerCount < 1) return false;
            var handSize = _config.HandSizeFor(playerCount);
            return subject.Facts.Count >= handSize * playerCount
                   && subject.TrueCount >= _config.MinTrueInHand * playerCount
                   && subject.FalseCount >= _config.MinFalseInHand * playerCount;
        }

        /// <summary>Руки по местам. Порядок seatIds сохраняется в ключах словаря.</summary>
        public IReadOnlyDictionary<int, Hand> Deal(Subject subject, IReadOnlyList<int> seatIds)
        {
            if (seatIds == null || seatIds.Count == 0) throw new ArgumentException("No seats to deal.", nameof(seatIds));
            if (!CanDeal(subject, seatIds.Count))
                throw new InvalidOperationException($"Subject '{subject?.Id}' can't be dealt to {seatIds.Count} players.");

            var handSize = _config.HandSizeFor(seatIds.Count);

            for (var attempt = 0; attempt < _config.MaxDealAttempts; attempt++)
            {
                var deck = subject.Facts.ToList();
                _random.Shuffle(deck);
                var hands = Split(deck, seatIds.Count, handSize);
                if (hands.All(IsValidHand)) return ToResult(seatIds, hands);
            }

            return ToResult(seatIds, BuildGuaranteed(subject, seatIds.Count, handSize));
        }

        public bool IsValidHand(IReadOnlyCollection<Fact> cards) =>
            cards.Count(c => c.IsTrue) >= _config.MinTrueInHand && cards.Count(c => !c.IsTrue) >= _config.MinFalseInHand;

        private static List<List<Fact>> Split(List<Fact> deck, int players, int handSize) =>
            Enumerable.Range(0, players).Select(i => deck.Skip(i * handSize).Take(handSize).ToList()).ToList();

        private List<List<Fact>> BuildGuaranteed(Subject subject, int players, int handSize)
        {
            var trues = subject.Facts.Where(f => f.IsTrue).ToList();
            var falses = subject.Facts.Where(f => !f.IsTrue).ToList();
            _random.Shuffle(trues);
            _random.Shuffle(falses);

            var hands = Enumerable.Range(0, players).Select(_ => new List<Fact>()).ToList();
            foreach (var hand in hands)
            {
                hand.AddRange(Take(trues, _config.MinTrueInHand));
                hand.AddRange(Take(falses, _config.MinFalseInHand));
            }

            var rest = trues.Concat(falses).ToList();
            _random.Shuffle(rest);
            foreach (var hand in hands)
            {
                hand.AddRange(Take(rest, handSize - hand.Count));
                _random.Shuffle(hand);
            }
            return hands;
        }

        private static List<Fact> Take(List<Fact> source, int count)
        {
            var taken = source.Take(count).ToList();
            source.RemoveRange(0, taken.Count);
            return taken;
        }

        private static IReadOnlyDictionary<int, Hand> ToResult(IReadOnlyList<int> seatIds, List<List<Fact>> hands)
        {
            var result = new Dictionary<int, Hand>();
            for (var i = 0; i < seatIds.Count; i++) result[seatIds[i]] = new Hand(hands[i]);
            return result;
        }
    }
}
