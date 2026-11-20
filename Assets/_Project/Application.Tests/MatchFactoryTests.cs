using System;
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
        public void InvalidSettings_Throw()
        {
            var factory = Factory(new ManualTimer());
            Assert.Throws<ArgumentException>(() => factory.Create(GameModeSettings.VsBots("A", 0)));
            Assert.Throws<ArgumentException>(() => factory.Create(GameModeSettings.VsBots("A", 6)));
            Assert.Throws<NotSupportedException>(() =>
                factory.Create(new GameModeSettings(GameMode.HotSeat, new[] { PlayerSlot.Human("A"), PlayerSlot.Human("B") })));
        }
    }
}
