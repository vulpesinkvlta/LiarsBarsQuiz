using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using NUnit.Framework;
using static LiarsBar.Application.Tests.SessionTestKit;

namespace LiarsBar.Application.Tests
{
    public class MatchFactoryTests
    {
        [Test]
        public void Host_TimeoutSurvivesInvalidInputAndIsCancelledByValidMove()
        {
            var clock = new ManualTimer();
            var roster = new[] { new KeyValuePair<int, string>(9, "Host"), new KeyValuePair<int, string>(4, "Guest") };
            using (var session = Factory(clock).CreateHost(roster, 0))
            {
                session.Start();
                int batches = 0;
                session.EventsRaised += _ => batches++;
                int first = session.State.CurrentSeat;
                Assert.AreEqual(30, session.CurrentView.RemainingTurnSeconds);
                clock.Advance(29);
                Assert.AreEqual(1, session.SnapshotFor(1).RemainingTurnSeconds);
                SubmitForCurrentSeat(session, new LiarsBar.Core.Actions.PlayCardsAction(first, "missing"));
                clock.Advance(1);
                Assert.AreEqual(1, batches);
                Assert.AreNotEqual(first, session.State.CurrentSeat);
                clock.Advance(10);
                SubmitForCurrentSeat(session, LocalMatchSession.FallbackAction(session.SnapshotFor(session.State.CurrentSeat)));
                Assert.AreEqual(2, batches);
                clock.Advance(20);
                Assert.AreEqual(2, batches);
                Assert.AreEqual(10, session.SnapshotFor(0).RemainingTurnSeconds);
            }
            Assert.AreEqual(0, clock.Pending);
        }

        private static void SubmitForCurrentSeat(LocalMatchSession session, LiarsBar.Core.Actions.IGameAction action)
        {
            if (session.Controllers[action.SeatId] is RemoteSeat remote)
                Assert.IsTrue(remote.TrySubmit(remote.ActorNumber, action));
            else session.Submit(action);
        }

        [Test]
        public void Host_MapsActorsToSeatsAndFillsSelectedBots()
        {
            var roster = new[]
            {
                new KeyValuePair<int, string>(9, "Игрок"),
                new KeyValuePair<int, string>(4, "Игрок")
            };
            using (var session = Factory(new ManualTimer()).CreateHost(roster, 2))
            {
                session.Start();
                Assert.AreEqual(0, session.ViewerSeat);
                Assert.IsInstanceOf<LocalHumanSeat>(session.Controllers[0]);
                Assert.AreEqual(4, ((RemoteSeat)session.Controllers[1]).ActorNumber);
                Assert.AreEqual(2, session.Controllers.Values.OfType<BotSeat>().Count());
                Assert.AreEqual("Игрок 2", session.Seats[1].Name);
            }
        }

        [Test]
        public void VsBots_HumanOnSeatZero_ThreeDifferentBots()
        {
            var settings = GameModeSettings.VsBots("Лена", 3);
            settings.Seed = 1;
            var session = Factory(new ManualTimer()).Create(settings);
            session.Start();

            Assert.AreEqual(0, session.ViewerSeat);
            Assert.IsInstanceOf<LocalHumanSeat>(session.Controllers[0]);
            var bots = session.Controllers.Values.OfType<BotSeat>().ToList();
            Assert.AreEqual(3, bots.Count);
            Assert.AreEqual(3, bots.Select(b => b.Profile.Id).Distinct().Count(), "при 3 ботах все профили разные");

            var names = session.CurrentView.Seats.Select(s => s.Name).ToList();
            Assert.AreEqual("Лена", names[0]);
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void VsBots_FiveBots_NamesAreUnique()
        {
            var settings = GameModeSettings.VsBots("", 5);
            settings.Seed = 2;
            var session = Factory(new ManualTimer()).Create(settings);
            session.Start();

            var names = session.CurrentView.Seats.Select(s => s.Name).ToList();
            Assert.AreEqual(6, names.Count);
            Assert.AreEqual(MatchFactory.DefaultHumanName, names[0]);
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void SameSeed_GivesSameMatch()
        {
            string Describe(int seed)
            {
                var settings = GameModeSettings.VsBots("A", 3);
                settings.Seed = seed;
                var timer = new ManualTimer();
                var session = Factory(timer).Create(settings);
                session.Start();
                PlayToEnd(session, timer);
                return $"{session.State.WinnerSeat}/{session.State.RoundNumber}";
            }

            Assert.AreEqual(Describe(42), Describe(42));
        }

        [Test]
        public void HotSeat_RequiresConfirmationAfterEveryMove()
        {
            var settings = new GameModeSettings(GameMode.HotSeat,
                new[] { PlayerSlot.Human("Лена"), PlayerSlot.Human("Иван"), PlayerSlot.Human("Анна") }) { Seed = 7 };
            using var session = Factory(new ManualTimer()).Create(settings);
            session.Start();
            var seat = session.CurrentView.CurrentSeat;
            Assert.IsEmpty(session.CurrentView.MyHand);
            Assert.IsFalse(session.ConfirmHumanTurn((seat + 1) % 3));
            Assert.IsTrue(session.ConfirmHumanTurn(seat));
            Assert.IsNotEmpty(session.CurrentView.MyHand);
            session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));
            Assert.IsEmpty(session.CurrentView.MyHand);
            Assert.AreEqual(-1, session.ViewerSeat);
        }

        [Test]
        public void HotSeat_MixedTable_PlaysToWinner()
        {
            var timer = new ManualTimer();
            var settings = new GameModeSettings(GameMode.HotSeat,
                new[] { PlayerSlot.Human("Лена"), PlayerSlot.Bot(), PlayerSlot.Human("Иван") }) { Seed = 7 };
            using var session = Factory(timer).Create(settings);
            session.Start();
            PlayToEnd(session, timer);
            Assert.IsTrue(session.IsEnded);
        }

        [Test]
        public void InvalidSettings_Throw()
        {
            var factory = Factory(new ManualTimer());
            Assert.Throws<ArgumentException>(() => factory.Create(GameModeSettings.VsBots("A", 0)));
            Assert.Throws<ArgumentException>(() => factory.Create(GameModeSettings.VsBots("A", 6)));
            Assert.Throws<NotSupportedException>(() =>
                factory.Create(new GameModeSettings(GameMode.Online, new[] { PlayerSlot.Human("A"), PlayerSlot.Human("B") })));
        }
    }
}
