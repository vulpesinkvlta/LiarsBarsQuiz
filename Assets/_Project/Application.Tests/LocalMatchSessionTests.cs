using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Config;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;
using NUnit.Framework;
using static LiarsBar.Application.Tests.SessionTestKit;

namespace LiarsBar.Application.Tests
{
    public class LocalMatchSessionTests
    {
        [Test]
        public void RemoteTurn_OnlyOwnerCanAct_AndCanRetryAfterInvalidPlay()
        {
            var seats = Enumerable.Range(0, 3).ToDictionary(i => i,
                i => (ISeatController)new RemoteSeat(10 + i, i, $"Guest {i}"));
            var engine = new MatchEngine(GameRulesConfig.Default, Subjects(), new SeededRandom(9));
            using (var session = new LocalMatchSession(engine, MatchSettings.ForPlayers(3), seats, 0))
            {
                session.Start();
                var id = session.State.CurrentSeat;
                var remote = (RemoteSeat)seats[id];
                var valid = LocalMatchSession.FallbackAction(engine.SnapshotFor(id));
                int batches = 0;
                session.EventsRaised += _ => batches++;
                Assert.IsFalse(remote.TrySubmit(99, valid));
                Assert.IsFalse(remote.TrySubmit(10 + id, new CallLiarAction((id + 1) % 3)));
                Assert.IsTrue(remote.TrySubmit(10 + id, new PlayCardsAction(id, new[] { "missing" })));
                Assert.AreEqual(0, batches);
                Assert.AreEqual(id, session.State.CurrentSeat);
                Assert.IsTrue(remote.TrySubmit(10 + id, valid));
                Assert.AreEqual(1, batches);
                Assert.IsFalse(remote.TrySubmit(10 + id, valid));
            }
        }

        private static LocalMatchSession VsBots(ManualTimer timer, int bots = 3, int seed = 7)
        {
            var settings = GameModeSettings.VsBots("Лена", bots);
            settings.Seed = seed;
            return Factory(timer).Create(settings);
        }

        [Test]
        public void Start_PublishesEventsThenViewForViewer()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer);
            var log = new List<string>();
            session.EventsRaised += events => log.Add("events:" + string.Join(",", events.Select(e => e.GetType().Name)));
            session.ViewChanged += view => log.Add("view:" + view.ViewerSeat);

            session.Start();

            Assert.AreEqual(2, log.Count);
            StringAssert.StartsWith("events:RoundStarted,TurnChanged", log[0]);
            Assert.AreEqual("view:0", log[1]);
            Assert.AreEqual(0, session.ViewerSeat);
            Assert.AreEqual(4, session.CurrentView.MyHand.Count, "4 игрока → по 4 карты");
        }

        [Test]
        public void Start_Twice_Throws()
        {
            var session = VsBots(new ManualTimer());
            session.Start();
            Assert.Throws<System.InvalidOperationException>(() => session.Start());
        }

        [Test]
        public void ViewerSnapshot_NeverContainsOtherHands()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 3);
            var checkedViews = 0;
            session.ViewChanged += view =>
            {
                // В момент рассылки состояние совпадает со снимком: рука в снимке — ровно рука места 0.
                Assert.AreEqual(0, view.ViewerSeat);
                CollectionAssert.AreEqual(session.State.GetPlayer(0).Hand.Cards.Select(c => c.Id).ToList(),
                    view.MyHand.Select(c => c.Id).ToList());
                checkedViews++;
            };
            session.Start();
            PlayToEnd(session, timer);

            Assert.That(checkedViews, Is.GreaterThan(3));
        }

        [Test]
        public void HumanSubmit_OnTurn_IsApplied()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 11);
            session.Start();
            AdvanceUntilHumanTurn(session, timer);

            var before = Snapshot(session);
            var rejected = new List<ActionError>();
            session.ActionRejected += rejected.Add;
            var raised = 0;
            session.EventsRaised += _ => raised++;

            session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));

            Assert.IsEmpty(rejected);
            Assert.AreEqual(1, raised);
            Assert.AreNotEqual(before, Snapshot(session));
        }

        [Test]
        public void HumanSubmit_NotOnTurn_IsRejected_AndStateUnchanged()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 11);
            session.Start();
            AdvanceUntilBotTurn(session, timer);

            var before = Snapshot(session);
            var rejected = new List<ActionError>();
            session.ActionRejected += rejected.Add;

            session.Submit(new CallLiarAction(0));

            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual(ActionErrorCode.NotYourTurn, rejected[0].Code);
            Assert.AreEqual(before, Snapshot(session));
        }

        [Test]
        public void HumanCannotActForBotSeat()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 11);
            session.Start();
            AdvanceUntilBotTurn(session, timer);

            var bot = session.State.CurrentSeat;
            var rejected = new List<ActionError>();
            session.ActionRejected += rejected.Add;
            var before = Snapshot(session);

            session.Submit(new PlayCardsAction(bot, session.State.GetPlayer(bot).Hand.Cards[0].Id));

            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual(before, Snapshot(session));
        }

        [Test]
        public void HumanInvalidAction_IsRejected_AndHumanIsAskedAgain()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 5);
            session.Start();
            AdvanceUntilHumanTurn(session, timer);

            var rejected = new List<ActionError>();
            session.ActionRejected += rejected.Add;
            var before = Snapshot(session);

            session.Submit(new PlayCardsAction(0, "no_such_card"));

            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual(ActionErrorCode.CardsNotInHand, rejected[0].Code);
            Assert.AreEqual(before, Snapshot(session));
            var human = (LocalHumanSeat)session.Controllers[0];
            Assert.IsTrue(human.IsWaiting, "после ошибки человек снова может ходить");

            session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));
            Assert.AreEqual(1, rejected.Count);
        }

        [Test]
        public void BotDelay_IsWithinConfiguredRange()
        {
            var timer = new ManualTimer();
            var session = BotsOnly(4, timer, seed: 21);
            session.Start();
            PlayToEnd(session, timer);

            var config = GameRulesConfig.Default;
            Assert.That(timer.RequestedDelays.Count, Is.GreaterThan(10));
            foreach (var d in timer.RequestedDelays)
                Assert.That(d, Is.InRange(config.BotDelayMinSeconds, config.BotDelayMaxSeconds));
        }

        [Test]
        public void BotDoesNotActBeforeDelay()
        {
            var timer = new ManualTimer();
            var session = BotsOnly(3, timer, seed: 2);
            var actions = 0;
            session.EventsRaised += _ => actions++;
            session.Start();
            actions = 0;

            timer.Advance(GameRulesConfig.Default.BotDelayMinSeconds - 0.01);
            Assert.AreEqual(0, actions);
            Assert.IsTrue(timer.Step());
            Assert.AreEqual(1, actions);
            Assert.That(timer.Now, Is.LessThanOrEqualTo(GameRulesConfig.Default.BotDelayMaxSeconds + 1e-6));
        }

        [Test]
        public void PresentationBusy_DefersNextRequest()
        {
            var timer = new ManualTimer();
            var session = BotsOnly(3, timer, seed: 4);
            session.Start();
            session.SetPresentationBusy(true);

            Assert.IsTrue(timer.Step(), "первый запрос сделан до занятости");
            Assert.AreEqual(0, timer.Pending, "пока UI занят, следующий ход не запрашивается");
            Assert.IsFalse(timer.Step());

            session.SetPresentationBusy(false);
            Assert.AreEqual(1, timer.Pending);
        }

        [Test]
        public void Dispose_CancelsPendingBotAndIgnoresLateSubmits()
        {
            var timer = new ManualTimer();
            var session = BotsOnly(3, timer, seed: 4);
            var raised = 0;
            session.Start();
            session.EventsRaised += _ => raised++;
            session.Dispose();

            timer.Advance(10);
            Assert.AreEqual(0, raised);
            Assert.AreEqual(0, timer.Pending);
        }

        [Test]
        public void BrokenController_IsReplacedByFallback_AndMatchContinues()
        {
            var timer = new ManualTimer();
            var config = GameRulesConfig.Default;
            var subjects = Subjects();
            var controllers = new Dictionary<int, ISeatController>
            {
                [0] = new CheatingSeat(),
                [1] = new BotSeat(1, new HeuristicBotStrategy(new FactCatalog(subjects)), BotProfile.Expert, new SeededRandom(1), timer, 1, 2),
                [2] = new BotSeat(2, new HeuristicBotStrategy(new FactCatalog(subjects)), BotProfile.Brazen, new SeededRandom(2), timer, 1, 2)
            };
            var session = new LocalMatchSession(new MatchEngine(config, subjects, new SeededRandom(9)),
                MatchSettings.ForPlayers(3), controllers, -1);
            var replaced = 0;
            session.ControllerActionReplaced += (_, __) => replaced++;

            session.Start();
            PlayToEnd(session, timer);

            Assert.That(replaced, Is.GreaterThan(0));
            Assert.IsTrue(session.IsEnded);
        }

        [Test]
        public void ReplaceController_MidTurn_AsksNewController()
        {
            var timer = new ManualTimer();
            var session = VsBots(timer, seed: 11);
            session.Start();
            AdvanceUntilHumanTurn(session, timer);

            var bot = new BotSeat(0, new HeuristicBotStrategy(new FactCatalog(Subjects())), BotProfile.Cautious,
                new SeededRandom(3), timer, 1, 2);
            session.ReplaceController(0, bot);
            Assert.AreEqual(1, timer.Pending);

            for (var step = 0; step < 5000 && !session.IsEnded; step++)
                Assert.IsTrue(timer.Step(), "Every remaining turn is controlled by a bot.");
            Assert.IsTrue(session.IsEnded);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void BotsOnly_MatchesFinish(int players)
        {
            for (var seed = 1; seed <= 15; seed++)
            {
                var timer = new ManualTimer();
                var session = BotsOnly(players, timer, seed);
                MatchEnded ended = null;
                session.EventsRaised += events => ended = ended ?? events.OfType<MatchEnded>().FirstOrDefault();
                session.Start();
                PlayToEnd(session, timer);

                Assert.IsNotNull(ended, $"seed {seed}");
                Assert.AreEqual(ended.WinnerSeat, session.CurrentView.WinnerSeat);
                Assert.AreEqual(0, timer.Pending, "после конца партии никто не ходит");
            }
        }

        [Test]
        public void HumanVsThreeBots_FullMatchFinishes()
        {
            for (var seed = 1; seed <= 20; seed++)
            {
                var timer = new ManualTimer();
                var session = VsBots(timer, 3, seed);
                session.Start();
                PlayToEnd(session, timer);
                Assert.AreEqual(MatchPhase.Ended, session.CurrentView.Phase, $"seed {seed}");
            }
        }

        private static void AdvanceUntilHumanTurn(LocalMatchSession session, ManualTimer timer)
        {
            for (var i = 0; i < 200 && !session.CurrentView.IsMyTurn; i++)
                Assert.IsTrue(timer.Step(), "человек так и не получил ход");
            Assert.IsTrue(session.CurrentView.IsMyTurn);
        }

        private static void AdvanceUntilBotTurn(LocalMatchSession session, ManualTimer timer)
        {
            for (var i = 0; i < 200 && session.CurrentView.IsMyTurn; i++)
                session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));
            Assert.IsFalse(session.CurrentView.IsMyTurn);
            Assert.AreEqual(MatchPhase.AwaitingAction, session.CurrentView.Phase);
        }

        private static string Snapshot(LocalMatchSession session)
        {
            var s = session.State;
            return $"{s.Phase}|{s.RoundNumber}|{s.CurrentSeat}|{s.LastPlay?.SeatId}|" +
                   string.Join(";", s.Players.Select(p => $"{p.SeatId}:{p.IsAlive}:{p.Revolver.Shooted}:{string.Join(",", p.Hand.Cards.Select(c => c.Id))}"));
        }

        /// <summary>Контроллер, который всегда пытается сыграть чужую карту.</summary>
        private sealed class CheatingSeat : ISeatController
        {
            public void RequestAction(PlayerView view, System.Action<IGameAction> submit) =>
                submit(new PlayCardsAction(view.ViewerSeat, "stolen_card"));

            public void Cancel() { }
        }
    }
}
