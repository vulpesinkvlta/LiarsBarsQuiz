using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;
using LiarsBar.Presentation.Table;
using NUnit.Framework;

namespace LiarsBar.Presentation.Tests
{
    public class TablePresenterTests
    {
        private sealed class FakeSession : IMatchSession
        {
            public event Action<PlayerView> ViewChanged;
            public event Action<IReadOnlyList<GameEvent>> EventsRaised;
            public event Action<ActionError> ActionRejected;
            public int ViewerSeat => 0;
            public PlayerView CurrentView { get; private set; }
            public readonly List<IGameAction> Submitted = new List<IGameAction>();
            public readonly List<bool> Busy = new List<bool>();

            public void Start() { }
            public void Submit(IGameAction action) => Submitted.Add(action);
            public void SetPresentationBusy(bool busy) => Busy.Add(busy);
            public void Dispose() { }

            public void Push(PlayerView view, params GameEvent[] events)
            {
                EventsRaised?.Invoke(events);
                CurrentView = view;
                ViewChanged?.Invoke(view);
            }

            public void Reject(ActionErrorCode code) => ActionRejected?.Invoke(new ActionError(code, ""));
        }

        private sealed class FakeView : ITableView
        {
            public event Action PlayClicked;
            public event Action LiarClicked;
            public event Action<string> CardClicked;
            public event Action RevealClosed;

            public bool CanPlay, CanLiar, ActionsVisible, RevealShown, GameOverShown;
            public string Hint, RevealText, Header;
            public IReadOnlyList<CardModel> Hand = Array.Empty<CardModel>();
            public IReadOnlyList<CardModel> TableCards = Array.Empty<CardModel>();
            public IReadOnlyList<SeatModel> Seats = Array.Empty<SeatModel>();
            public readonly List<string> Log = new List<string>();

            public void SetHeader(string text) => Header = text;
            public void SetSeats(IReadOnlyList<SeatModel> seats) => Seats = seats;
            public void SetTablePlay(string caption, IReadOnlyList<CardModel> cards) => TableCards = cards;
            public void SetHand(IReadOnlyList<CardModel> cards) => Hand = cards;
            public void SetActions(bool canPlay, bool canCallLiar, bool visible) { CanPlay = canPlay; CanLiar = canCallLiar; ActionsVisible = visible; }
            public void SetHint(string text) => Hint = text;
            public void AppendLog(string line) => Log.Add(line);
            public void ShowReveal(string text) { RevealShown = true; RevealText = text; }
            public void HideReveal() => RevealShown = false;
            public void ShowGameOver(string text) => GameOverShown = true;

            public void ClickCard(string id) => CardClicked?.Invoke(id);
            public void ClickPlay() => PlayClicked?.Invoke();
            public void ClickLiar() => LiarClicked?.Invoke();
            public void CloseReveal() => RevealClosed?.Invoke();
        }

        private static readonly IReadOnlyList<HandCard> Hand3 = new[]
        {
            new HandCard("a", "Факт A", true, Difficulty.Easy),
            new HandCard("b", "Факт B", false, Difficulty.Medium),
            new HandCard("c", "Факт C", true, Difficulty.Hard)
        };

        private static PlayerView View(int current, TablePlayView lastPlay = null, bool forced = false,
            MatchPhase phase = MatchPhase.AwaitingAction, IReadOnlyList<HandCard> hand = null, int winner = -1)
        {
            var seats = new[]
            {
                new SeatInfo(0, "Лена", true, 3, 0, 6, null),
                new SeatInfo(1, "Марат", true, 2, 1, 6, null)
            };
            return new PlayerView(0, phase, 1, "moon", "Луна", current, forced, winner, seats, lastPlay, hand ?? Hand3);
        }

        private static TablePlayView MaratPlayed() => new TablePlayView(1, new[] { new CardFace("x", "Факт X") });

        private (FakeSession, FakeView, TablePresenter) Setup()
        {
            var session = new FakeSession();
            var view = new FakeView();
            var presenter = new TablePresenter(session, view);
            return (session, view, presenter);
        }

        [Test]
        public void NotMyTurn_ButtonsDisabled_CardsNotSelectable()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 1, lastPlay: MaratPlayed()));

            Assert.IsFalse(view.CanPlay);
            Assert.IsFalse(view.CanLiar);
            Assert.IsTrue(view.Hand.All(c => !c.Interactable));
            StringAssert.Contains("Марат", view.Hint);

            view.ClickCard("a");
            view.ClickPlay();
            view.ClickLiar();
            Assert.IsEmpty(session.Submitted);
        }

        [Test]
        public void FirstTurn_PlayNeedsSelection_LiarDisabled()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0));

            Assert.IsFalse(view.CanPlay, "ничего не выбрано");
            Assert.IsFalse(view.CanLiar, "первый ход раунда");
            view.ClickCard("a");
            Assert.IsTrue(view.CanPlay);
            Assert.IsTrue(view.Hand.Single(c => c.Id == "a").Selected);

            view.ClickPlay();
            var play = (PlayCardsAction)session.Submitted.Single();
            CollectionAssert.AreEqual(new[] { "a" }, play.CardIds);
        }

        [Test]
        public void Selection_KeepsAtMostTwo_DropsOldest_AndToggles()
        {
            var (session, view, presenter) = Setup();
            session.Push(View(current: 0));

            view.ClickCard("a");
            view.ClickCard("b");
            view.ClickCard("c");
            CollectionAssert.AreEqual(new[] { "b", "c" }, presenter.Selection);
            view.ClickCard("b");
            CollectionAssert.AreEqual(new[] { "c" }, presenter.Selection);
        }

        [Test]
        public void ForcedAccusation_OnlyLiarEnabled()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0, lastPlay: MaratPlayed(), forced: true));

            Assert.IsTrue(view.CanLiar);
            Assert.IsFalse(view.CanPlay);
            Assert.IsTrue(view.Hand.All(c => !c.Interactable));
            StringAssert.Contains("Лжец", view.Hint);

            view.ClickLiar();
            Assert.IsInstanceOf<CallLiarAction>(session.Submitted.Single());
        }

        [Test]
        public void HandShowsMarks_TableCardsHaveNoMarks()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0, lastPlay: MaratPlayed()));

            Assert.AreEqual(true, view.Hand.Single(c => c.Id == "a").Mark);
            Assert.AreEqual(false, view.Hand.Single(c => c.Id == "b").Mark);
            Assert.IsNull(view.TableCards.Single().Mark);
        }

        [Test]
        public void Reveal_BlocksInputAndSession_UntilClosed()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0, lastPlay: MaratPlayed()));

            var fact = new Fact("x", "Факт X", false, "Пояснение");
            session.Push(View(current: 0),
                new LiarCalled(0, 1, false),
                new FactsRevealed(1, new[] { fact }, true, 1),
                new RevolverShooted(1, 1, 2, 6, false),
                new RoundStarted(2, "moon", "Луна", 1, new Dictionary<int, int>()),
                new TurnChanged(0, false));

            Assert.IsTrue(view.RevealShown);
            StringAssert.Contains("Пояснение", view.RevealText);
            StringAssert.Contains("✗", view.RevealText);
            CollectionAssert.AreEqual(new[] { true }, session.Busy);
            Assert.IsFalse(view.CanPlay);
            Assert.IsFalse(view.CanLiar);
            view.ClickCard("a");
            view.ClickPlay();
            Assert.IsEmpty(session.Submitted);

            view.CloseReveal();
            Assert.IsFalse(view.RevealShown);
            CollectionAssert.AreEqual(new[] { true, false }, session.Busy);
            view.ClickCard("a");
            Assert.IsTrue(view.CanPlay);
        }

        [Test]
        public void MatchEnd_ShowsRevealThenGameOver()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0, lastPlay: MaratPlayed()));

            var fact = new Fact("x", "Факт X", false, "Пояснение");
            session.Push(View(current: -1, phase: MatchPhase.Ended, hand: Array.Empty<HandCard>(), winner: 0),
                new LiarCalled(0, 1, false),
                new FactsRevealed(1, new[] { fact }, true, 1),
                new RevolverShooted(1, 5, 6, 6, true),
                new PlayerEliminated(1),
                new MatchEnded(0, 4));

            Assert.IsTrue(view.RevealShown);
            Assert.IsFalse(view.GameOverShown);
            Assert.IsFalse(view.ActionsVisible);
            view.CloseReveal();
            Assert.IsTrue(view.GameOverShown);
        }

        [Test]
        public void Rejection_ShowsReadableHint()
        {
            var (session, view, _) = Setup();
            session.Push(View(current: 0));
            session.Reject(ActionErrorCode.CannotCallLiarOnFirstTurn);
            StringAssert.Contains("первом ходу", view.Hint);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var (session, view, presenter) = Setup();
            presenter.Dispose();
            session.Push(View(current: 0));
            Assert.IsNull(view.Header);
        }
    }
}
