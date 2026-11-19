using System;
using LiarsBar.Core.Domain;
using NUnit.Framework;

namespace LiarsBar.Core.Tests
{
    public class HandTests
    {
        private static Hand MakeHand() => new Hand(new[]
        {
            TestFacts.True("a"), TestFacts.False("b"), TestFacts.True("c")
        });

        [Test]
        public void Remove_ReturnsCardsInRequestOrder_AndShrinksHand()
        {
            var hand = MakeHand();

            var removed = hand.Remove(new[] { "c", "a" });

            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual("c", removed[0].Id);
            Assert.AreEqual("a", removed[1].Id);
            Assert.AreEqual(1, hand.Count);
            Assert.IsTrue(hand.Contains("b"));
        }

        [Test]
        public void Remove_ForeignCard_DoesNotTouchHand()
        {
            var hand = MakeHand();

            Assert.IsNull(hand.Remove(new[] { "a", "someone-else" }));
            Assert.AreEqual(3, hand.Count, "Hand must stay unchanged when any id is foreign.");
            Assert.IsTrue(hand.Contains("a"));
        }

        [Test]
        public void Remove_DuplicateIds_Rejected()
        {
            var hand = MakeHand();

            Assert.IsNull(hand.Remove(new[] { "a", "a" }));
            Assert.AreEqual(3, hand.Count);
        }

        [Test]
        public void Remove_Empty_Rejected()
        {
            var hand = MakeHand();

            Assert.IsNull(hand.Remove(Array.Empty<string>()));
            Assert.IsNull(hand.Remove(null));
            Assert.AreEqual(3, hand.Count);
        }

        [Test]
        public void Remove_All_MakesHandEmpty()
        {
            var hand = MakeHand();
            hand.Remove(new[] { "a", "b", "c" });
            Assert.IsTrue(hand.IsEmpty);
        }

        [Test]
        public void Constructor_RejectsDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new Hand(new[] { TestFacts.True("a"), TestFacts.True("a") }));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var hand = MakeHand();
            var clone = hand.Clone();
            clone.Remove(new[] { "a" });

            Assert.AreEqual(3, hand.Count);
            Assert.AreEqual(2, clone.Count);
        }
    }
}
