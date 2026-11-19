using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Domain
{
    /// <summary>Рука игрока: карты (факты), которые он ещё не сыграл.</summary>
    public sealed class Hand
    {
        private readonly List<Fact> _cards;

        public IReadOnlyList<Fact> Cards => _cards;
        public int Count => _cards.Count;
        public bool IsEmpty => _cards.Count == 0;

        public Hand() : this(Enumerable.Empty<Fact>()) { }

        public Hand(IEnumerable<Fact> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _cards = cards.ToList();
            if (_cards.Any(c => c == null)) throw new ArgumentException("Hand contains a null card.", nameof(cards));
            if (_cards.Select(c => c.Id).Distinct().Count() != _cards.Count)
                throw new ArgumentException("Hand contains duplicate cards.", nameof(cards));
        }

        public bool Contains(string cardId) => _cards.Any(c => c.Id == cardId);

        /// <summary>Есть ли в руке все указанные карты (без повторов в запросе).</summary>
        public bool ContainsAll(IReadOnlyCollection<string> cardIds)
        {
            if (cardIds == null || cardIds.Count == 0) return false;
            if (cardIds.Distinct().Count() != cardIds.Count) return false;
            return cardIds.All(Contains);
        }

        /// <summary>
        /// Забирает карты из руки и возвращает их в порядке запроса.
        /// Атомарно: если хотя бы одной карты нет в руке (или id повторяются), рука не меняется и возвращается null.
        /// </summary>
        public IReadOnlyList<Fact> Remove(IReadOnlyCollection<string> cardIds)
        {
            if (!ContainsAll(cardIds)) return null;

            var removed = new List<Fact>(cardIds.Count);
            foreach (var id in cardIds)
            {
                var index = _cards.FindIndex(c => c.Id == id);
                removed.Add(_cards[index]);
                _cards.RemoveAt(index);
            }
            return removed;
        }

        public Hand Clone() => new Hand(_cards);

        public override string ToString() => $"Hand({_cards.Count}): {string.Join(", ", _cards.Select(c => c.Id))}";
    }
}
