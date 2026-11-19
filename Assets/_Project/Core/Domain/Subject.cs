using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Domain
{
    /// <summary>Предмет раунда (тема) с набором фактов. Неизменяемый объект.</summary>
    public sealed class Subject
    {
        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<Fact> Facts { get; }

        public int TrueCount => Facts.Count(f => f.IsTrue);
        public int FalseCount => Facts.Count - TrueCount;

        public Subject(string id, string title, IEnumerable<Fact> facts)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Subject id is empty.", nameof(id));
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            var list = facts.ToList();
            if (list.Any(f => f == null)) throw new ArgumentException("Subject contains a null fact.", nameof(facts));

            var duplicate = list.GroupBy(f => f.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new ArgumentException($"Duplicate fact id '{duplicate.Key}' in subject '{id}'.", nameof(facts));

            Id = id;
            Title = title ?? string.Empty;
            Facts = list.AsReadOnly();
        }

        public override string ToString() => $"{Id} «{Title}» ({TrueCount}✓/{FalseCount}✗)";
    }
}
