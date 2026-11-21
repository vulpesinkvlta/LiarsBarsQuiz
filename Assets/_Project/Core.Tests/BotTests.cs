using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;
using NUnit.Framework;
using static LiarsBar.Core.Tests.EngineTestKit;

namespace LiarsBar.Core.Tests
{
    public class BotProfileTests
    {
        [Test]
        public void Defaults_AreValid_AndMatchSpec()
        {
            foreach (var p in BotProfile.Defaults) Assert.IsEmpty(p.Validate(), p.Id);

            var c = BotProfile.Cautious;
            Assert.AreEqual((0.70f, 0.45f, 0.20f, 0.70f, 0.10f), (c.KnowEasy, c.KnowMedium, c.KnowHard, c.AccuseThreshold, c.MixLieChance));
            var b = BotProfile.Brazen;
            Assert.AreEqual((0.60f, 0.35f, 0.15f, 0.45f, 0.24f), (b.KnowEasy, b.KnowMedium, b.KnowHard, b.AccuseThreshold, b.MixLieChance));
            var e = BotProfile.Expert;
            Assert.AreEqual((0.90f, 0.70f, 0.45f, 0.55f, 0.20f), (e.KnowEasy, e.KnowMedium, e.KnowHard, e.AccuseThreshold, e.MixLieChance));

            Assert.AreEqual(3, BotProfile.Defaults.Select(p => p.Id).Distinct().Count());
        }

        [Test]
        public void Threshold_GrowsByFivePercentPerShot()
        {
            var p = BotProfile.Expert;
            Assert.AreEqual(0.55, p.ThresholdAfterShots(0), 1e-6);
            Assert.AreEqual(0.70, p.ThresholdAfterShots(3), 1e-6);
        }

        [Test]
        public void Validate_CatchesBadValues()
        {
            var p = BotProfile.Brazen;
            p.KnowHard = 1.5f;
            p.ThresholdPerShot = -0.1f;
            Assert.AreEqual(2, p.Validate().Count);
        }
    }

    public class BotRecallTests
    {
        [Test]
        public void Roll_IsDeterministic_AndInRange()
        {
            for (var i = 0; i < 200; i++)
            {
                var r = BotRecall.Roll(i, $"fact_{i}");
                Assert.That(r, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
                Assert.AreEqual(r, BotRecall.Roll(i, $"fact_{i}"));
            }
        }

        [Test]
        public void Roll_IsRoughlyUniform_AndDiffersBetweenBots()
        {
            const int n = 5000;
            var below = Enumerable.Range(0, n).Count(i => BotRecall.Roll(BotRecall.BotKey(1, 0), $"f{i}") < 0.45);
            Assert.AreEqual(0.45, (double)below / n, 0.03);

            var same = Enumerable.Range(0, n).Count(i =>
                BotRecall.Roll(BotRecall.BotKey(1, 0), $"f{i}") < 0.5 == BotRecall.Roll(BotRecall.BotKey(1, 1), $"f{i}") < 0.5);
            Assert.AreEqual(0.5, (double)same / n, 0.04, "two bots must know independent sets of facts");
        }
    }

    public class HeuristicBotStrategyTests
    {
        private const int Me = 0;
        private const int Other = 1;

        private static readonly Fact T1 = new Fact("t1", "true 1", true, "", Difficulty.Easy);
        private static readonly Fact T3 = new Fact("t3", "true 3", true, "", Difficulty.Hard);
        private static readonly Fact F1 = new Fact("f1", "false 1", false, "", Difficulty.Easy);
        private static readonly Fact F2 = new Fact("f2", "false 2", false, "", Difficulty.Medium);
        private static readonly Fact F3 = new Fact("f3", "false 3", false, "", Difficulty.Hard);

        private static readonly IFactKnowledge Catalog =
            new FactCatalog(new[] { new Subject("s", "S", new[] { T1, T3, F1, F2, F3 }) });

        private static HeuristicBotStrategy Strategy() => new HeuristicBotStrategy(Catalog);

        private static BotProfile KnowsAll(float know = 1f) => new BotProfile
        {
            Id = "test", KnowEasy = know, KnowMedium = know, KnowHard = know,
            AccuseThreshold = 0.5f, MixLieChance = 0f
        };

        private static PlayerView View(IEnumerable<Fact> myHand, IEnumerable<Fact> table = null, bool forced = false,
            int myShots = 0, int otherCards = 3)
        {
            var hand = (myHand ?? Enumerable.Empty<Fact>()).Select(HandCard.From).ToList();
            var seats = new List<SeatInfo>
            {
                new SeatInfo(Me, "me", true, hand.Count, myShots, 6, null),
                new SeatInfo(Other, "other", true, otherCards, 0, 6, null)
            };
            var play = table == null ? null : new TablePlayView(Other, table.Select(CardFace.From).ToList());
            return new PlayerView(Me, MatchPhase.AwaitingAction, 1, "s", "S", Me, forced, -1, seats, play, hand);
        }

        private static IGameAction Decide(PlayerView view, BotProfile profile, int seed = 1) =>
            Strategy().Decide(view, profile, new SeededRandom(seed));

        [Test]
        public void ForcedAccusation_AlwaysCallsLiar()
        {
            var action = Decide(View(new Fact[0], new[] { T1 }, forced: true), KnowsAll());
            Assert.IsInstanceOf<CallLiarAction>(action);
            Assert.AreEqual(Me, action.SeatId);
        }

        [Test]
        public void FirstTurnOfRound_NeverCallsLiar()
        {
            for (var seed = 1; seed <= 50; seed++)
                Assert.IsInstanceOf<PlayCardsAction>(Decide(View(new[] { T1, F1 }), BotProfile.Brazen, seed));
        }

        [Test]
        public void KnownLie_IsCalled_KnownTruth_IsNot()
        {
            Assert.IsInstanceOf<CallLiarAction>(Decide(View(new[] { T3 }, new[] { F2 }), KnowsAll()));
            Assert.IsInstanceOf<PlayCardsAction>(Decide(View(new[] { T3 }, new[] { T1 }), KnowsAll()));
        }

        [Test]
        public void UnknownCards_UseBasePriorAndModifiers()
        {
            var s = Strategy();
            var p = KnowsAll(0f);

            Assert.AreEqual(0.35, s.EstimateLieProbability(View(new[] { T3 }, new[] { T1 }), p), 1e-6);
            // 2 карты: база 0,35 + 0,10 = 0,45 на карту → 1 − 0,55² = 0,6975.
            Assert.AreEqual(0.6975, s.EstimateLieProbability(View(new[] { T3 }, new[] { T1, F1 }), p), 1e-6);
            // У сыгравшего осталась 1 карта: + 0,15 → 0,50.
            Assert.AreEqual(0.50, s.EstimateLieProbability(View(new[] { T3 }, new[] { T1 }, otherCards: 1), p), 1e-6);
            Assert.AreEqual(0, s.EstimateLieProbability(View(new[] { T3 }), p));
        }

        [Test]
        public void Threshold_RisesWithShotsTaken()
        {
            var p = KnowsAll(0f);
            p.AccuseThreshold = 0.55f; // оценка хода из 2 карт — 0,6975

            Assert.IsInstanceOf<CallLiarAction>(Decide(View(new[] { T3 }, new[] { T1, F1 }, myShots: 0), p));
            Assert.IsInstanceOf<CallLiarAction>(Decide(View(new[] { T3 }, new[] { T1, F1 }, myShots: 2), p)); // 0,65
            Assert.IsInstanceOf<PlayCardsAction>(Decide(View(new[] { T3 }, new[] { T1, F1 }, myShots: 3), p)); // 0,70
        }

        [Test]
        public void HasTrue_PlaysOneTrue_WithoutMixing()
        {
            for (var seed = 1; seed <= 30; seed++)
            {
                var play = (PlayCardsAction)Decide(View(new[] { T1, T3, F1, F3 }), KnowsAll(), seed);
                Assert.AreEqual(1, play.CardIds.Count);
                Assert.That(play.CardIds[0], Is.EqualTo("t1").Or.EqualTo("t3"));
            }
        }

        [Test]
        public void MixLie_AddsHardestFalseToTrue()
        {
            var p = KnowsAll();
            p.MixLieChance = 1f;
            var play = (PlayCardsAction)Decide(View(new[] { T1, F1, F3, F2 }), p);
            CollectionAssert.AreEqual(new[] { "t1", "f3" }, play.CardIds);
        }

        [Test]
        public void MixLie_RespectsMaxCardsPerPlay()
        {
            var p = KnowsAll();
            p.MixLieChance = 1f;
            var play = (PlayCardsAction)new HeuristicBotStrategy(Catalog, maxCardsPerPlay: 1)
                .Decide(View(new[] { T1, F3 }), p, new SeededRandom(1));
            CollectionAssert.AreEqual(new[] { "t1" }, play.CardIds);
        }

        [Test]
        public void OnlyFalses_PlaysHardestOne()
        {
            var play = (PlayCardsAction)Decide(View(new[] { F1, F3, F2 }), BotProfile.Brazen);
            CollectionAssert.AreEqual(new[] { "f3" }, play.CardIds);
        }

        [Test]
        public void Knowledge_IsStableForTheSameBot()
        {
            var s = Strategy();
            var p = KnowsAll(0.5f);
            foreach (var f in new[] { T1, T3, F1, F2, F3 })
            {
                var first = s.Knows(Me, f, p);
                for (var i = 0; i < 10; i++) Assert.AreEqual(first, s.Knows(Me, f, p));
            }
        }

        [Test]
        public void NotMyTurn_Throws()
        {
            var view = View(new[] { T1 });
            var notMine = new PlayerView(Me, MatchPhase.AwaitingAction, 1, "s", "S", Other, false, -1, view.Seats, null, view.MyHand);
            Assert.Throws<System.InvalidOperationException>(() => Decide(notMine, KnowsAll()));
        }
    }

    /// <summary>Боты доигрывают партии на синтетическом контенте при любом числе игроков.</summary>
    public class BotSimulationTests
    {
        [Test]
        public void Bots_FinishMatches([Values(2, 3, 4, 5, 6)] int players)
        {
            var result = BotSimulation.Run(Subjects(6), BotProfile.Defaults, matches: 40, players: players, seed: players);
            Assert.AreEqual(40, result.Wins.Values.Sum());
            Assert.AreEqual(40 * players, result.SeatsPlayed.Values.Sum());
            Assert.Greater(result.Accusations, 0);
            Assert.GreaterOrEqual(result.MinRounds, players - 1, "each round eliminates at most one player");
            TestContext.WriteLine(result.ToReport());
        }

        [Test]
        public void SameSeed_GivesSameResult()
        {
            string Run(int seed) => BotSimulation.Run(Subjects(6), BotProfile.Defaults, 20, 4, seed).ToReport();
            Assert.AreEqual(Run(5), Run(5));
            Assert.AreNotEqual(Run(5), Run(6));
        }
    }
}
