using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;

namespace LiarsBar.Presentation.Table
{
    /// <summary>
    /// Переводит снимки и события сессии в вью-модели серого стола, а нажатия — в действия.
    /// Не знает режима: работает с любой IMatchSession. Доступность кнопок берётся только из PlayerView.
    /// </summary>
    public sealed class TablePresenter : IDisposable
    {
        private const string TrueColor = "#3FA34D";
        private const string FalseColor = "#D9534F";

        private readonly UnityEngine.MonoBehaviour _animationHost;
        private readonly IMatchSession _session;
        private readonly ITableView _view;
        private readonly int _minCards;
        private readonly int _maxCards;
        private readonly List<string> _selection = new List<string>();
        private readonly List<GameEvent> _pendingEvents = new List<GameEvent>();

        private PlayerView _current;
        private bool _revealOpen;
        private bool _gameOverPending;
        private bool _disposed;
        private bool _animating;
        private UnityEngine.Coroutine _animation;
        public bool IsAnimating => _animating;
        public event Action PresentationChanged;

        public PlayerView Current => _current;
        public IReadOnlyList<string> Selection => _selection;
        public bool IsRevealOpen => _revealOpen;

        public TablePresenter(IMatchSession session, ITableView view, int minCardsPerPlay = 1, int maxCardsPerPlay = 2, UnityEngine.MonoBehaviour animationHost = null)
        {
            _animationHost = animationHost;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _minCards = Math.Max(1, minCardsPerPlay);
            _maxCards = Math.Max(_minCards, maxCardsPerPlay);

            _session.EventsRaised += OnEventsRaised;
            _session.ViewChanged += OnViewChanged;
            _session.ActionRejected += OnActionRejected;
            _view.PlayClicked += OnPlayClicked;
            _view.LiarClicked += OnLiarClicked;
            _view.CardClicked += OnCardClicked;
            _view.RevealClosed += OnRevealClosed;

            if (_session.CurrentView != null) OnViewChanged(_session.CurrentView);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_animation != null && _view is TableView table)
                (_animationHost ?? table).StopCoroutine(_animation);
            _session.EventsRaised -= OnEventsRaised;
            _session.ViewChanged -= OnViewChanged;
            _session.ActionRejected -= OnActionRejected;
            _view.PlayClicked -= OnPlayClicked;
            _view.LiarClicked -= OnLiarClicked;
            _view.CardClicked -= OnCardClicked;
            _view.RevealClosed -= OnRevealClosed;
        }

        // События приходят перед снимком; разбираем их вместе со снимком, чтобы знать имена мест.
        private void OnEventsRaised(IReadOnlyList<GameEvent> events) => _pendingEvents.AddRange(events);

        private void OnViewChanged(PlayerView view)
        {
            _current = view;
            var events = _pendingEvents.ToList();
            _pendingEvents.Clear();
            if (_view is TableView table && events.Count > 0)
            {
                _animating = true;
                _session.SetPresentationBusy(true);
                _view.SetHand(Array.Empty<CardModel>());
                _view.SetActions(false, false, false);
                table.IsPlayingEvent = true;
                _animation = (_animationHost ?? table).StartCoroutine(AnimateEvents(table, events));
                return;
            }
            HandleEvents(events);
            ApplySnapshot();
        }

        private System.Collections.IEnumerator AnimateEvents(TableView table, IReadOnlyList<GameEvent> events)
        {
            foreach (var e in events)
            {
                if (_disposed) yield break;
                var line = Describe(e);
                if (line != null) _view.AppendLog(line);
                if (e is FactsRevealed)
                {
                    _revealOpen = true;
                    _view.ShowReveal(RevealText(events));
                    while (_revealOpen && !_disposed) yield return null;
                }
                else
                {
                    yield return table.Animate(e, _current);
                }
            }
            if (_disposed) yield break;
            table.IsPlayingEvent = false;
            _animating = false;
            ApplySnapshot();
            if (events.OfType<MatchEnded>().Any()) ShowGameOver();
            PresentationChanged?.Invoke();
            _session.SetPresentationBusy(false);
        }

        private void ApplySnapshot()
        {
            var hand = new HashSet<string>(_current.MyHand.Select(c => c.Id));
            _selection.RemoveAll(id => !hand.Contains(id));
            Render();
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var e in events)
            {
                var line = Describe(e);
                if (line != null) _view.AppendLog(line);
            }

            var revealed = events.OfType<FactsRevealed>().FirstOrDefault();
            if (revealed != null)
            {
                _revealOpen = true;
                _gameOverPending = events.OfType<MatchEnded>().Any();
                _session.SetPresentationBusy(true); // пока игрок читает проверку, следующий ход не начинается
                _view.ShowReveal(RevealText(events));
            }
            else if (events.OfType<MatchEnded>().Any())
            {
                ShowGameOver();
            }
        }

        private void Render()
        {
            var v = _current;
            if (v == null) return;

            _view.SetHeader(v.Phase == MatchPhase.Ended
                ? $"Партия окончена · раундов: {v.RoundNumber}"
                : $"Раунд {v.RoundNumber} · Предмет: {v.SubjectTitle}");

            _view.SetSeats(v.Seats.Select(s => new SeatModel(s.SeatId, s.Name, s.CardCount, s.ShotsTaken, s.Chambers, s.IsAlive,
                v.Phase == MatchPhase.AwaitingAction && s.SeatId == v.CurrentSeat, s.SeatId == v.ViewerSeat)).ToList());

            if (v.LastPlay == null)
                _view.SetTablePlay(v.Phase == MatchPhase.AwaitingAction ? "Стол пуст — первый ход раунда" : string.Empty, Array.Empty<CardModel>());
            else
                _view.SetTablePlay($"{NameOf(v.LastPlay.SeatId)} положил(а) {CardsWord(v.LastPlay.Cards.Count)}:",
                    v.LastPlay.Cards.Select(c => new CardModel(c.Id, c.Text, null, false, false)).ToList());

            var myTurn = v.IsMyTurn && !_revealOpen;
            var canPick = myTurn && v.CanPlayCards;
            _view.SetHand(v.MyHand.Select(c => new CardModel(c.Id, c.Text, c.IsTrue, _selection.Contains(c.Id), canPick)).ToList());

            var canPlay = canPick && _selection.Count >= _minCards && _selection.Count <= _maxCards;
            var canLiar = myTurn && v.CanCallLiar;
            _view.SetActions(canPlay, canLiar, v.Phase != MatchPhase.Ended && IsViewerAlive(v));
            _view.SetHint(Hint(v, myTurn));
        }

        private string Hint(PlayerView v, bool myTurn)
        {
            if (v.Phase == MatchPhase.Ended) return $"Победитель: {NameOf(v.WinnerSeat)}";
            if (_revealOpen) return "Проверка…";
            if (v.ViewerSeat >= 0 && !IsViewerAlive(v)) return "Вы выбыли — следите за игрой";
            if (!myTurn) return $"Ходит {NameOf(v.CurrentSeat)}…";
            if (v.ForcedAccusation) return $"Карты остались только у {NameOf(v.LastPlay?.SeatId ?? -1)} — нужно крикнуть «Лжец!»";
            if (v.IsFirstTurnOfRound) return $"Ваш ход: выберите {_minCards}–{_maxCards} карты и нажмите «Сыграть»";
            return $"Ваш ход: выберите {_minCards}–{_maxCards} карты или крикните «Лжец!»";
        }

        private void OnCardClicked(string id)
        {
            var v = _current;
            if (v == null || _animating || _revealOpen || !v.CanPlayCards || v.MyHand.All(c => c.Id != id)) return;

            if (!_selection.Remove(id))
            {
                _selection.Add(id);
                while (_selection.Count > _maxCards) _selection.RemoveAt(0);
            }
            Render();
        }

        private void OnPlayClicked()
        {
            var v = _current;
            if (v == null || _animating || _revealOpen || !v.CanPlayCards) return;
            if (_selection.Count < _minCards || _selection.Count > _maxCards) return;

            var cards = _selection.ToList();
            _selection.Clear();
            _session.Submit(new PlayCardsAction(v.ViewerSeat, cards));
        }

        private void OnLiarClicked()
        {
            var v = _current;
            if (v == null || _animating || _revealOpen || !v.CanCallLiar) return;
            _selection.Clear();
            _session.Submit(new CallLiarAction(v.ViewerSeat));
        }

        private void OnRevealClosed()
        {
            if (!_revealOpen) return;
            _revealOpen = false;
            _view.HideReveal();
            if (_animating) return;
            if (_gameOverPending)
            {
                _gameOverPending = false;
                ShowGameOver();
            }
            Render();
            _session.SetPresentationBusy(false);
        }

        private void ShowGameOver()
        {
            var v = _current;
            if (v == null) return;
            _view.ShowGameOver($"Победитель: {NameOf(v.WinnerSeat)}\nРаундов сыграно: {v.RoundNumber}");
        }

        private void OnActionRejected(ActionError error)
        {
            _view.SetHint(error.Code switch
            {
                ActionErrorCode.NotYourTurn => "Сейчас не ваш ход",
                ActionErrorCode.MustCallLiar => "Нужно крикнуть «Лжец!»",
                ActionErrorCode.CannotCallLiarOnFirstTurn => "На первом ходу раунда «Лжец!» кричать нельзя",
                ActionErrorCode.InvalidCardCount => $"Нужно выбрать {_minCards}–{_maxCards} карты",
                ActionErrorCode.CardsNotInHand => "Этих карт нет в руке",
                ActionErrorCode.DuplicateCards => "Карта выбрана дважды",
                _ => "Так ходить нельзя"
            });
        }

        private string Describe(GameEvent e)
        {
            switch (e)
            {
                case RoundStarted r: return $"— Раунд {r.RoundNumber}: «{r.SubjectTitle}». Начинает {NameOf(r.StarterSeat)}";
                case CardsPlayed p: return $"{NameOf(p.SeatId)} кладёт {CardsWord(p.Cards.Count)} (в руке {p.CardsLeftInHand})";
                case LiarCalled l: return $"{NameOf(l.AccuserSeat)}: «Лжец!» → {NameOf(l.AccusedSeat)}{(l.WasForced ? " (вынужденно)" : "")}";
                case FactsRevealed f: return f.LieFound ? $"Ложь найдена у {NameOf(f.PlayedBySeat)}" : $"{NameOf(f.PlayedBySeat)} сказал(а) правду";
                case RevolverShooted s: return s.Hit ? $"{NameOf(s.SeatId)}: БАХ!" : $"{NameOf(s.SeatId)}: щёлк ({s.ShotsTaken}/{s.Chambers})";
                case PlayerEliminated x: return $"{NameOf(x.SeatId)} выбывает";
                case MatchEnded m: return $"Победитель — {NameOf(m.WinnerSeat)}!";
                default: return null;
            }
        }

        private string RevealText(IReadOnlyList<GameEvent> events)
        {
            var sb = new StringBuilder();
            var call = events.OfType<LiarCalled>().FirstOrDefault();
            var revealed = events.OfType<FactsRevealed>().First();
            if (call != null)
                sb.AppendLine($"<b>{NameOf(call.AccuserSeat)}</b>: «Лжец!» → <b>{NameOf(call.AccusedSeat)}</b>{(call.WasForced ? " (вынужденно)" : "")}").AppendLine();

            sb.AppendLine($"Карты {NameOf(revealed.PlayedBySeat)}:");
            foreach (var fact in revealed.Facts)
            {
                var mark = fact.IsTrue ? $"<color={TrueColor}>✓ правда</color>" : $"<color={FalseColor}>✗ ложь</color>";
                sb.AppendLine($"{mark} — {fact.Text}");
                sb.AppendLine($"<size=80%><i>{fact.Explanation}</i></size>");
            }

            sb.AppendLine();
            sb.AppendLine(revealed.LieFound
                ? $"Ложь найдена! В рулетку играет <b>{NameOf(revealed.ShooterSeat)}</b>."
                : $"Всё правда! В рулетку играет <b>{NameOf(revealed.ShooterSeat)}</b>.");

            return sb.ToString().TrimEnd();
        }

        private string NameOf(int seatId)
        {
            var seat = _current?.GetSeat(seatId);
            if (seat == null) return seatId < 0 ? "—" : $"Игрок {seatId + 1}";
            return seat.SeatId == _current.ViewerSeat ? $"{seat.Name} (вы)" : seat.Name;
        }

        private static bool IsViewerAlive(PlayerView v) => v.GetSeat(v.ViewerSeat)?.IsAlive ?? false;

        private static string CardsWord(int n) => n == 1 ? "1 карту" : $"{n} карты";
    }
}
