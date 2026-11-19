using System;
using System.Linq;
using LiarsBar.Core.Config;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Randomness;
using NUnit.Framework;

namespace LiarsBar.Core.Tests
{
    public class SubjectAndPlayTests
    {
        [Test]
        public void Subject_CountsTrueAndFalse()
        {
            var subject = TestFacts.Subject(trueCount: 10, falseCount: 10);
            Assert.AreEqual(10, subject.TrueCount);
            Assert.AreEqual(10, subject.FalseCount);
        }

        [Test]
        public void Subject_RejectsDuplicateFactIds()
        {
            Assert.Throws<ArgumentException>(() =>
                new Subject("s", "S", new[] { TestFacts.True("x"), TestFacts.False("x") }));
        }

        [Test]
        public void TablePlay_DetectsLie()
        {
            Assert.IsFalse(new TablePlay(0, new[] { TestFacts.True("a"), TestFacts.True("b") }).HasLie);
            Assert.IsTrue(new TablePlay(0, new[] { TestFacts.True("a"), TestFacts.False("b") }).HasLie);
        }

        [Test]
        public void TablePlay_RejectsEmpty()
        {
            Assert.Throws<ArgumentException>(() => new TablePlay(0, Array.Empty<Fact>()));
        }
    }

    public class MatchStateTests
    {
        private static MatchState MakeState(int players)
        {
            var random = new SeededRandom(1);
            return new MatchState(Enumerable.Range(0, players)
                .Select(i => new PlayerState(i, $"P{i}", Revolver.CreateRandom(random))));
        }

        [Test]
        public void NewState_IsNotStarted_AndAllAlive()
        {
            var state = MakeState(4);
            Assert.AreEqual(MatchPhase.NotStarted, state.Phase);
            Assert.AreEqual(4, state.AliveCount);
            Assert.IsTrue(state.IsFirstTurnOfRound);
        }

        [Test]
        public void Constructor_RejectsDuplicateSeats()
        {
            var r = new Revolver(0);
            Assert.Throws<ArgumentException>(() =>
                new MatchState(new[] { new PlayerState(0, "a", r), new PlayerState(0, "b", r) }));
        }

        [Test]
        public void Clone_IsDeep()
        {
            var state = MakeState(3);
            state.GetPlayer(0).Hand = new Hand(new[] { TestFacts.True("a"), TestFacts.False("b") });
            state.MarkSubjectUsed("s1");

            var clone = state.Clone();
            clone.GetPlayer(0).Hand.Remove(new[] { "a" });
            clone.GetPlayer(1).IsAlive = false;
            clone.GetPlayer(2).Stats.ShotsSurvived++;
            clone.GetPlayer(2).Revolver.Shot();
            clone.MarkSubjectUsed("s2");

            Assert.AreEqual(2, state.GetPlayer(0).Hand.Count);
            Assert.IsTrue(state.GetPlayer(1).IsAlive);
            Assert.AreEqual(0, state.GetPlayer(2).Stats.ShotsSurvived);
            Assert.AreEqual(0, state.GetPlayer(2).Revolver.Shooted);
            Assert.AreEqual(1, state.UsedSubjectIds.Count);
        }

        [Test]
        public void GetPlayer_UnknownSeat_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MakeState(2).GetPlayer(5));
        }
    }

    public class GameRulesConfigTests
    {
        [TestCase(2, 5)]
        [TestCase(3, 5)]
        [TestCase(4, 4)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        public void HandSize_MatchesSpec(int players, int expected)
        {
            Assert.AreEqual(expected, GameRulesConfig.Default.HandSizeFor(players));
        }

        [TestCase(1)]
        [TestCase(7)]
        public void HandSize_OutOfRange_Throws(int players)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRulesConfig.Default.HandSizeFor(players));
        }

        [Test]
        public void Default_IsValid()
        {
            CollectionAssert.IsEmpty(GameRulesConfig.Default.Validate());
        }

        [Test]
        public void Validate_CatchesBrokenConfig()
        {
            var config = new GameRulesConfig { MaxCardsPerPlay = 0, HandSizeForLargeTable = 1 };
            Assert.GreaterOrEqual(config.Validate().Count, 2);
        }
    }

    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRandom(123);
            var b = new SeededRandom(123);
            for (var i = 0; i < 100; i++) Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        [Test]
        public void DifferentSeeds_DifferentSequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            var same = Enumerable.Range(0, 20).All(_ => a.Next(1_000_000) == b.Next(1_000_000));
            Assert.IsFalse(same);
        }

        [Test]
        public void Next_StaysInRange()
        {
            var r = new SeededRandom(7);
            for (var i = 0; i < 10_000; i++)
            {
                var v = r.Next(3, 9);
                Assert.That(v, Is.InRange(3, 8));
                var d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void Shuffle_KeepsElements()
        {
            var list = Enumerable.Range(0, 20).ToList();
            new SeededRandom(5).Shuffle(list);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), list);
        }
    }
}
