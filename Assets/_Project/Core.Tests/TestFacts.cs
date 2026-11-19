using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Tests
{
    /// <summary>Фабрика тестовых фактов и предметов.</summary>
    internal static class TestFacts
    {
        public static Fact True(string id) => new Fact(id, $"true {id}", true, "because");
        public static Fact False(string id) => new Fact(id, $"false {id}", false, "because");

        /// <summary>Предмет с trueCount ✓ (t0..) и falseCount ✗ (f0..).</summary>
        public static Subject Subject(string id = "s1", int trueCount = 10, int falseCount = 10)
        {
            var facts = new List<Fact>();
            facts.AddRange(Enumerable.Range(0, trueCount).Select(i => True($"{id}-t{i}")));
            facts.AddRange(Enumerable.Range(0, falseCount).Select(i => False($"{id}-f{i}")));
            return new Subject(id, $"Subject {id}", facts);
        }
    }
}
