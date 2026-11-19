using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;
using NUnit.Framework;
using static LiarsBar.Core.Tests.EngineTestKit;

namespace LiarsBar.Core.Tests
{
    public class DealerTests
    {
        [TestCase(2, 5)]
        [TestCase(3, 5)]
        [TestCase(4, 4)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        public void Deal_HandSizeAndMix(int players, int expectedSize)
        {
            var subject = TestFacts.Subject();
            for (var seed = 0; seed < 100; seed++)
            {
                var dealer = new Dealer(GameRulesConfig.Default, new SeededRandom(seed));
                var hands = dealer.Deal(subject, Enumerable.Range(0, players).ToList());

                Assert.AreEqual(players, hands.Count);
                foreach (var hand in hands.Values)
                {
                    Assert.AreEqual(expectedSize, hand.Count);
                    Assert.GreaterOrEqual(hand.Cards.Count(c => c.IsTrue), 1);
                    Assert.GreaterOrEqual(hand.Cards.Count(c => !c.IsTrue), 1);
                }

                var ids = hands.Values.SelectMany(h => h.Cards).Select(c => c.Id).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count(), "a card was dealt twice");
                Assert.IsTrue(ids.All(id => subject.Facts.Any(f => f.Id == id)));
            }
        }

        [Test]
        public void Deal_TightSubject_StillSatisfiesMix()
        {
            // 6 игроков × 3 карты, ровно 6 ✓: каждой руке нужна ровно одна ✓ — случайной пересдачей почти не выпадает.
            var subject = TestFacts.Subject(trueCount: 6, falseCount: 12);
            var config = new GameRulesConfig { MaxDealAttempts = 1 };
            for (var seed = 0; seed < 50; seed++)
            {
                var hands = new Dealer(config, new SeededRandom(seed)).Deal(subject, Enumerable.Range(0, 6).ToList());
                Assert.IsTrue(hands.Values.All(h => h.Cards.Count(c => c.IsTrue) == 1));
            }
        }

        [Test]
        public void CanDeal_RejectsTooSmallSubject()
        {
            var dealer = new Dealer(GameRulesConfig.Default, new SeededRandom(1));
            Assert.IsFalse(dealer.CanDeal(TestFacts.Subject(trueCount: 3, falseCount: 10), 6));
            Assert.IsFalse(dealer.CanDeal(TestFacts.Subject(trueCount: 5, falseCount: 4), 2));
            Assert.IsTrue(dealer.CanDeal(TestFacts.Subject(trueCount: 5, falseCount: 5), 2));
        }
    }

    public class MatchStartTests
    {
        [Test]
        public void Start_DealsAndGivesTurnToFirstSeat()
        {
            var engine = NewEngine();
            var settings = MatchSettings.ForPlayers(4);
            settings.FirstSeat = 2;
            var result = engine.Start(settings);

            Assert.IsTrue(result.IsSuccess);
            var round = Single<RoundStarted>(result);
            Assert.AreEqual(1, round.RoundNumber);
            Assert.AreEqual(2, round.StarterSeat);
            Assert.IsTrue(round.HandSizes.Values.All(n => n == 4));
            Assert.AreEqual(2, Single<TurnChanged>(result).SeatId);
            Assert.AreEqual(MatchPhase.AwaitingAction, engine.State.Phase);
            Assert.IsTrue(engine.State.IsFirstTurnOfRound);
        }

        [Test]
        public void Start_RandomFirstSeat_IsAtTable()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var engine = NewEngine(seed);
                engine.Start(MatchSettings.ForPlayers(5));
                Assert.That(engine.State.CurrentSeat, Is.InRange(0, 4));
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        public void Start_RejectsWrongPlayerCount(int count)
        {
            var result = NewEngine().Start(MatchSettings.ForPlayers(count));
            Assert.AreEqual(ActionErrorCode.InvalidSettings, result.Error.Code);
        }

        [Test]
        public void Start_FailsWithoutPlayableContent()
        {
            var subjects = new InMemorySubjectProvider(new[] { TestFacts.Subject(trueCount: 3, falseCount: 3) });
            var result = NewEngine(subjects: subjects).Start(MatchSettings.ForPlayers(4));
            Assert.AreEqual(ActionErrorCode.NotEnoughContent, result.Error.Code);
        }

        [Test]
        public void Start_Twice_Fails()
        {
            var engine = Started(3);
            Assert.IsFalse(engine.Start(MatchSettings.ForPlayers(3)).IsSuccess);
        }

        [Test]
        public void Subjects_DoNotRepeatUntilPoolIsExhausted()
        {
            var engine = Started(2, bulletIndex: 5);
            var seen = new System.Collections.Generic.List<string> { engine.State.CurrentSubject.Id };
            for (var i = 0; i < 3; i++)
            {
                PlayOne(engine, true);
                CallLiar(engine); // все ✓ → стреляет обвинивший, пуля в 6-й каморе
                seen.Add(engine.State.CurrentSubject.Id);
            }
            Assert.AreEqual(4, seen.Distinct().Count(), string.Join(",", seen));
        }
    }

    public class MatchTurnTests
    {
        [Test]
        public void Play_MovesTurnToNextSeat_AndHidesMarks()
        {
            var engine = Started(3);
            var result = PlayFirst(engine, 2);

            var played = Single<CardsPlayed>(result);
            Assert.AreEqual(0, played.SeatId);
            Assert.AreEqual(2, played.Cards.Count);
            Assert.AreEqual(3, played.CardsLeftInHand);
            Assert.AreEqual(1, engine.State.CurrentSeat);
            Assert.IsFalse(engine.State.ForcedAccusation);
            Assert.AreEqual(2, engine.State.GetPlayer(0).Stats.CardsPlayed);
        }

        [Test]
        public void Turn_SkipsPlayersWithEmptyHand()
        {
            var engine = Started(3); // по 5 карт
            PlayFirst(engine, 2); // 0: 3
            PlayFirst(engine, 2); // 1: 3
            PlayFirst(engine, 2); // 2: 3
            PlayFirst(engine, 2); // 0: 1
            PlayFirst(engine, 1); // 1: 2
            PlayFirst(engine, 1); // 2: 2
            PlayFirst(engine, 1); // 0: 0
            PlayFirst(engine, 1); // 1: 1
            PlayFirst(engine, 1); // 2: 1
            Assert.AreEqual(1, engine.State.CurrentSeat, "seat 0 has no cards and is skipped");
            Assert.IsFalse(engine.State.ForcedAccusation);
            PlayFirst(engine, 1); // 1: 0
            Assert.AreEqual(2, engine.State.CurrentSeat);
            PlayFirst(engine, 1); // 2: 0 → карт нет ни у кого
            Assert.AreEqual(0, engine.State.CurrentSeat, "next alive must accuse even with an empty hand");
            Assert.IsTrue(engine.State.ForcedAccusation);
        }

        [Test]
        public void ForcedAccusation_WhenOnlyThePlayerHasCards()
        {
            var engine = Started(2); // по 5 карт
            PlayFirst(engine, 2); // 0: 3
            PlayFirst(engine, 1); // 1: 4
            PlayFirst(engine, 2); // 0: 1
            PlayFirst(engine, 1); // 1: 3
            var lastOfZero = PlayFirst(engine, 1); // 0: 0
            Assert.IsFalse(Single<TurnChanged>(lastOfZero).ForcedAccusation, "seat 1 still has cards");

            var result = PlayFirst(engine, 1); // 1: 2, у 0 карт нет
            var turn = Single<TurnChanged>(result);
            Assert.AreEqual(0, turn.SeatId);
            Assert.IsTrue(turn.ForcedAccusation);

            var view = engine.SnapshotFor(0);
            Assert.IsTrue(view.CanCallLiar);
            Assert.IsFalse(view.CanPlayCards);

            var before = Fingerprint(engine.State);
            var bad = engine.Apply(new PlayCardsAction(0, "anything"));
            Assert.AreEqual(ActionErrorCode.MustCallLiar, bad.Error.Code);
            Assert.AreEqual(before, Fingerprint(engine.State));

            var call = CallLiar(engine);
            Assert.IsTrue(Single<LiarCalled>(call).WasForced);
        }

        [Test]
        public void PassingOverALie_CountsAsUncaughtLie()
        {
            var engine = Started(3);
            PlayOne(engine, false); // 0 соврал
            PlayOne(engine, true);  // 1 не стал обвинять
            Assert.AreEqual(1, engine.State.GetPlayer(0).Stats.UncaughtLies);
            Assert.AreEqual(0, engine.State.GetPlayer(1).Stats.UncaughtLies);
        }
    }

    public class AccusationTests
    {
        [Test]
        public void CorrectAccusation_PlayerShoots()
        {
            var engine = Started(3, bulletIndex: 5);
            PlayOne(engine, false);
            var result = CallLiar(engine);

            var called = Single<LiarCalled>(result);
            Assert.AreEqual(1, called.AccuserSeat);
            Assert.AreEqual(0, called.AccusedSeat);

            var revealed = Single<FactsRevealed>(result);
            Assert.IsTrue(revealed.LieFound);
            Assert.AreEqual(0, revealed.ShooterSeat);
            Assert.IsFalse(revealed.Facts.Single().IsTrue);

            var shot = Single<RevolverShooted>(result);
            Assert.AreEqual(0, shot.SeatId);
            Assert.AreEqual(0, shot.ChamberIndex);
            Assert.AreEqual(1, shot.ShotsTaken);
            Assert.IsFalse(shot.Hit);

            Assert.AreEqual(1, engine.State.GetPlayer(1).Stats.CorrectAccusations);
            Assert.AreEqual(1, engine.State.GetPlayer(0).Stats.ShotsSurvived);
            Assert.AreEqual(0, engine.State.GetPlayer(0).Stats.UncaughtLies);
        }

        [Test]
        public void WrongAccusation_AccuserShoots()
        {
            var engine = Started(3, bulletIndex: 5);
            PlayOne(engine, true);
            var result = CallLiar(engine);

            var revealed = Single<FactsRevealed>(result);
            Assert.IsFalse(revealed.LieFound);
            Assert.AreEqual(1, revealed.ShooterSeat);
            Assert.AreEqual(1, Single<RevolverShooted>(result).SeatId);
            Assert.AreEqual(1, engine.State.GetPlayer(1).Stats.WrongAccusations);
        }

        [Test]
        public void MixedPlay_WithOneLie_IsCaught()
        {
            var engine = Started(2, bulletIndex: 5);
            var player = Current(engine);
            var t = player.Hand.Cards.First(c => c.IsTrue).Id;
            var f = player.Hand.Cards.First(c => !c.IsTrue).Id;
            Ok(engine, new PlayCardsAction(0, t, f));
            Assert.AreEqual(0, Single<FactsRevealed>(CallLiar(engine)).ShooterSeat);
        }

        [Test]
        public void SurvivedShooter_StartsNextRound()
        {
            var engine = Started(4, bulletIndex: 5);
            PlayOne(engine, true);          // 0
            var result = CallLiar(engine);  // 1 ошибся и стреляет

            var round = Single<RoundStarted>(result);
            Assert.AreEqual(2, round.RoundNumber);
            Assert.AreEqual(1, round.StarterSeat);
            Assert.AreEqual(1, engine.State.CurrentSeat);
            Assert.IsNull(engine.State.LastPlay);
            Assert.IsTrue(engine.State.Players.All(p => p.Hand.Count == 4), "everyone is re-dealt");
        }

        [Test]
        public void Elimination_NextAliveStarts_AndIsSkippedAfterwards()
        {
            var engine = Started(3, bulletIndex: 0); // первый же выстрел смертелен
            PlayOne(engine, true);           // 0
            PlayOne(engine, false);          // 1 соврал
            var result = CallLiar(engine);   // 2 ловит, стреляет 1

            Assert.IsTrue(Single<RevolverShooted>(result).Hit);
            Assert.AreEqual(1, Single<PlayerEliminated>(result).SeatId);
            Assert.IsFalse(engine.State.GetPlayer(1).IsAlive);
            Assert.IsTrue(engine.State.GetPlayer(1).Hand.IsEmpty);

            var round = Single<RoundStarted>(result);
            Assert.AreEqual(2, round.StarterSeat, "eliminated shooter → next alive starts");
            Assert.IsFalse(round.HandSizes.ContainsKey(1));
            Assert.IsTrue(round.HandSizes.Values.All(n => n == 5), "2 alive players → 5 cards");

            PlayFirst(engine, 1); // 2
            Assert.AreEqual(0, engine.State.CurrentSeat, "seat 1 is skipped");

            var before = Fingerprint(engine.State);
            Assert.AreEqual(ActionErrorCode.PlayerEliminated, engine.Apply(new CallLiarAction(1)).Error.Code);
            Assert.AreEqual(before, Fingerprint(engine.State));
        }

        [Test]
        public void LastSurvivor_EndsMatch()
        {
            var engine = Started(2, bulletIndex: 0);
            PlayOne(engine, false);
            var result = CallLiar(engine);

            var ended = Single<MatchEnded>(result);
            Assert.AreEqual(1, ended.WinnerSeat);
            Assert.AreEqual(1, ended.RoundsPlayed);
            Assert.IsFalse(result.Events.OfType<RoundStarted>().Any());
            Assert.AreEqual(MatchPhase.Ended, engine.State.Phase);
            Assert.AreEqual(1, engine.State.WinnerSeat);
            Assert.AreEqual(-1, engine.State.CurrentSeat);

            var view = engine.SnapshotFor(1);
            Assert.AreEqual(1, view.WinnerSeat);
            Assert.IsFalse(view.IsMyTurn);
            Assert.IsNotNull(view.GetSeat(1).Stats, "stats are revealed after the match");
            Assert.AreEqual(1, view.GetSeat(1).Stats.CorrectAccusations);

            Assert.AreEqual(ActionErrorCode.MatchNotInProgress, engine.Apply(new CallLiarAction(1)).Error.Code);
        }
    }

    public class ValidationTests
    {
        private static void AssertRejected(MatchEngine engine, IGameAction action, ActionErrorCode code)
        {
            var before = Fingerprint(engine.State);
            var result = engine.Apply(action);
            Assert.IsFalse(result.IsSuccess, action?.ToString());
            Assert.AreEqual(code, result.Error.Code, result.Error.Message);
            Assert.IsEmpty(result.Events);
            Assert.AreEqual(before, Fingerprint(engine.State), "invalid action changed the state");
        }

        [Test]
        public void InvalidActions_DoNotChangeState()
        {
            var engine = Started(3);
            var hand0 = engine.State.GetPlayer(0).Hand.Cards.Select(c => c.Id).ToList();
            var hand1 = engine.State.GetPlayer(1).Hand.Cards.Select(c => c.Id).ToList();

            AssertRejected(engine, null, ActionErrorCode.NullAction);
            AssertRejected(engine, new CallLiarAction(0), ActionErrorCode.CannotCallLiarOnFirstTurn);
            AssertRejected(engine, new PlayCardsAction(1, hand1[0]), ActionErrorCode.NotYourTurn);
            AssertRejected(engine, new PlayCardsAction(9, hand0[0]), ActionErrorCode.UnknownSeat);
            AssertRejected(engine, new PlayCardsAction(0), ActionErrorCode.InvalidCardCount);
            AssertRejected(engine, new PlayCardsAction(0, hand0.Take(3)), ActionErrorCode.InvalidCardCount);
            AssertRejected(engine, new PlayCardsAction(0, hand0[0], hand0[0]), ActionErrorCode.DuplicateCards);
            AssertRejected(engine, new PlayCardsAction(0, hand0[0], hand1[0]), ActionErrorCode.CardsNotInHand);
            AssertRejected(engine, new PlayCardsAction(0, "no-such-card"), ActionErrorCode.CardsNotInHand);
        }

        [Test]
        public void Apply_BeforeStart_Fails()
        {
            Assert.AreEqual(ActionErrorCode.MatchNotInProgress, NewEngine().Apply(new CallLiarAction(0)).Error.Code);
        }
    }

    public class PlayerViewTests
    {
        [Test]
        public void Snapshot_ShowsOnlyOwnHand()
        {
            var engine = Started(4);
            PlayFirst(engine, 1);

            foreach (var player in engine.State.Players)
            {
                var view = engine.SnapshotFor(player.SeatId);
                Assert.AreEqual(player.SeatId, view.ViewerSeat);
                CollectionAssert.AreEqual(player.Hand.Cards.Select(c => c.Id), view.MyHand.Select(c => c.Id));
                CollectionAssert.AreEqual(player.Hand.Cards.Select(c => c.IsTrue), view.MyHand.Select(c => c.IsTrue));

                var others = engine.State.Players.Where(p => p.SeatId != player.SeatId)
                    .SelectMany(p => p.Hand.Cards).Select(c => c.Id);
                Assert.IsFalse(view.MyHand.Any(c => others.Contains(c.Id)), "another hand leaked");

                foreach (var seat in view.Seats)
                {
                    Assert.AreEqual(engine.State.GetPlayer(seat.SeatId).Hand.Count, seat.CardCount);
                    Assert.IsNull(seat.Stats, "stats stay hidden during the match");
                }
            }
        }

        [Test]
        public void Snapshot_LastPlayHasNoMarks()
        {
            var engine = Started(3);
            PlayFirst(engine, 2);
            var view = engine.SnapshotFor(1);

            Assert.AreEqual(0, view.LastPlay.SeatId);
            Assert.AreEqual(2, view.LastPlay.Cards.Count);
            // CardFace несёт только Id и Text — пометки ✓/✗ в публичной части нет.
            var faceProps = typeof(CardFace).GetProperties().Select(p => p.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Id", "Text" }, faceProps);
            Assert.IsTrue(view.CanCallLiar);
            Assert.IsTrue(view.CanPlayCards);
            Assert.IsFalse(engine.SnapshotFor(0).IsMyTurn);
        }

        [Test]
        public void Snapshot_PublicTypes_DoNotExposeHiddenData()
        {
            var publicTypes = new[] { typeof(PlayerView), typeof(SeatInfo), typeof(TablePlayView), typeof(CardFace) };
            foreach (var type in publicTypes)
            foreach (var prop in type.GetProperties())
            {
                Assert.AreNotEqual(typeof(Fact), prop.PropertyType, $"{type.Name}.{prop.Name}");
                Assert.AreNotEqual(typeof(Hand), prop.PropertyType, $"{type.Name}.{prop.Name}");
                Assert.AreNotEqual(typeof(Revolver), prop.PropertyType, $"{type.Name}.{prop.Name}");
                StringAssert.DoesNotContain("Bullet", prop.Name, $"{type.Name}.{prop.Name}");
            }
        }

        [Test]
        public void Snapshot_Spectator_HasNoHand()
        {
            var engine = Started(3);
            var view = engine.SnapshotFor(-1);
            Assert.AreEqual(-1, view.ViewerSeat);
            Assert.IsEmpty(view.MyHand);
            Assert.IsFalse(view.IsMyTurn);
        }
    }
}
