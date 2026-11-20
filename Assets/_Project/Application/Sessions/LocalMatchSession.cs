using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Seats;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Sessions
{
    /// <summary>
    /// Партия, у которой движок живёт на этом устройстве: офлайн с ботами, hot-seat и хост онлайн.
    /// Держит MatchEngine и контроллеры мест; по очереди просит действие у того, чей ход,
    /// применяет его и рассылает события и снимок зрителя.
    /// </summary>
    public sealed class LocalMatchSession : IMatchSession
    {
        private readonly MatchEngine _engine;
        private readonly MatchSettings _settings;
        private readonly Dictionary<int, ISeatController> _controllers;
        private readonly Queue<Submission> _inbox = new Queue<Submission>();

        private int _requestId;
        private bool _processing;
        private bool _busy;
        private bool _requestDeferred;
        private bool _started;
        private bool _disposed;

        public event Action<PlayerView> ViewChanged;
        public event Action<IReadOnlyList<GameEvent>> EventsRaised;
        public event Action<ActionError> ActionRejected;

        /// <summary>Действие бота или удалённого игрока отклонено и заменено запасным (для логов).</summary>
        public event Action<int, ActionError> ControllerActionReplaced;

        private readonly bool _followHumanTurn;
        public int ViewerSeat { get; private set; }
        public PlayerView CurrentView { get; private set; }

        /// <summary>Полное состояние — только для Application (хост, тесты). UI его не получает.</summary>
        public MatchState State => _engine.State;

        public IReadOnlyDictionary<int, ISeatController> Controllers => _controllers;

        public bool IsEnded => _engine.State?.Phase == MatchPhase.Ended;

        public LocalMatchSession(MatchEngine engine, MatchSettings settings,
            IReadOnlyDictionary<int, ISeatController> controllers, int viewerSeat, bool followHumanTurn = false)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (controllers == null) throw new ArgumentNullException(nameof(controllers));

            var seatIds = settings.Seats.Select(s => s.SeatId).ToList();
            var missing = seatIds.Where(id => !controllers.ContainsKey(id) || controllers[id] == null).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"No controller for seats: {string.Join(", ", missing)}.", nameof(controllers));
            if (viewerSeat >= 0 && !seatIds.Contains(viewerSeat))
                throw new ArgumentException($"Viewer seat {viewerSeat} is not at the table.", nameof(viewerSeat));

            _controllers = controllers.ToDictionary(kv => kv.Key, kv => kv.Value);
            _followHumanTurn = followHumanTurn;
            ViewerSeat = followHumanTurn ? -1 : viewerSeat;
        }

        public bool ConfirmHumanTurn(int seatId)
        {
            if (!_followHumanTurn || _disposed || !_started || _busy || ViewerSeat >= 0 ||
                _engine.State.Phase != MatchPhase.AwaitingAction || _engine.State.CurrentSeat != seatId ||
                !(_controllers[seatId] is LocalHumanSeat)) return false;

            ViewerSeat = seatId;
            CurrentView = _engine.SnapshotFor(seatId);
            ViewChanged?.Invoke(CurrentView);
            return true;
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (_started) throw new InvalidOperationException("Session is already started.");
            _started = true;

            var result = _engine.Start(_settings);
            if (!result.IsSuccess) throw new InvalidOperationException($"Match failed to start: {result.Error}");
            Publish(result.Events);
        }

        public void Submit(IGameAction action)
        {
            if (_disposed || !_started) return;
            if (action == null)
            {
                Reject(new ActionError(ActionErrorCode.NullAction, "Action is null."));
                return;
            }

            // С этого устройства можно ходить только за своих людей и только когда их ждут.
            if (!_controllers.TryGetValue(action.SeatId, out var controller) || !(controller is LocalHumanSeat human))
            {
                Reject(new ActionError(ActionErrorCode.NotYourTurn, $"Seat {action.SeatId} is not controlled on this device."));
                return;
            }

            if ((_followHumanTurn && ViewerSeat != action.SeatId) || !human.TrySubmit(action))
                Reject(new ActionError(ActionErrorCode.NotYourTurn, $"Seat {action.SeatId} is not expected to act now."));
        }

        public void SetPresentationBusy(bool busy)
        {
            _busy = busy;
            if (!busy && _requestDeferred && !_disposed)
            {
                _requestDeferred = false;
                RequestCurrent();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requestId++;
            _inbox.Clear();
            foreach (var controller in _controllers.Values) controller.Cancel();
            ViewChanged = null;
            EventsRaised = null;
            ActionRejected = null;
            ControllerActionReplaced = null;
        }

        /// <summary>
        /// Заменить контроллер места (например, ушедшего гостя — ботом). Если сейчас ход этого места,
        /// действие запрашивается у нового контроллера.
        /// </summary>
        public void ReplaceController(int seatId, ISeatController controller)
        {
            ThrowIfDisposed();
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (!_controllers.TryGetValue(seatId, out var old))
                throw new ArgumentException($"Seat {seatId} is not at the table.", nameof(seatId));

            old.Cancel();
            _controllers[seatId] = controller;
            if (_started && _engine.State.Phase == MatchPhase.AwaitingAction && _engine.State.CurrentSeat == seatId)
                RequestCurrent();
        }

        private void RequestCurrent()
        {
            var state = _engine.State;
            if (_disposed || state == null || state.Phase != MatchPhase.AwaitingAction) return;
            if (_busy)
            {
                _requestDeferred = true;
                return;
            }

            var seat = state.CurrentSeat;
            var id = ++_requestId;
            _controllers[seat].RequestAction(_engine.SnapshotFor(seat), action => Enqueue(new Submission(id, seat, action)));
        }

        /// <summary>Действия обрабатываются по очереди, без рекурсии, даже если контроллер ответил синхронно.</summary>
        private void Enqueue(Submission submission)
        {
            if (_disposed) return;
            _inbox.Enqueue(submission);
            if (_processing) return;

            _processing = true;
            try
            {
                while (_inbox.Count > 0 && !_disposed) Process(_inbox.Dequeue());
            }
            finally
            {
                _processing = false;
            }
        }

        private void Process(Submission submission)
        {
            if (submission.RequestId != _requestId) return; // поздний или повторный ответ

            var controller = _controllers[submission.SeatId];
            var action = submission.Action;
            var result = action != null && action.SeatId == submission.SeatId
                ? _engine.Apply(action)
                : ApplyResult.Fail(ActionErrorCode.NotYourTurn, $"Controller of seat {submission.SeatId} submitted {action?.ToString() ?? "null"}.");

            if (!result.IsSuccess)
            {
                if (controller is LocalHumanSeat)
                {
                    Reject(result.Error);
                    RequestCurrent(); // снова ждём человека
                    return;
                }

                // Бот или удалённый игрок не должен ломать партию: ходим за него простым допустимым ходом.
                ControllerActionReplaced?.Invoke(submission.SeatId, result.Error);
                result = _engine.Apply(FallbackAction(_engine.SnapshotFor(submission.SeatId)));
                if (!result.IsSuccess) throw new InvalidOperationException($"Fallback action failed: {result.Error}");
            }

            Publish(result.Events);
        }

        private void Publish(IReadOnlyList<GameEvent> events)
        {
            _requestId++; // все старые запросы больше не актуальны
            if (_followHumanTurn)
            {
                ViewerSeat = -1;
                CurrentView = _engine.SnapshotFor(ViewerSeat);
            }
            EventsRaised?.Invoke(events);
            if (_disposed) return;

            if (!_followHumanTurn) CurrentView = _engine.SnapshotFor(ViewerSeat);
            ViewChanged?.Invoke(CurrentView);
            if (_disposed) return;

            RequestCurrent();
        }

        private void Reject(ActionError error) => ActionRejected?.Invoke(error);

        /// <summary>Самый простой допустимый ход: обязан обвинить — обвиняет, иначе кладёт первую карту.</summary>
        public static IGameAction FallbackAction(PlayerView view)
        {
            if (view.CanCallLiar && (view.ForcedAccusation || !view.CanPlayCards)) return new CallLiarAction(view.ViewerSeat);
            if (view.CanPlayCards) return new PlayCardsAction(view.ViewerSeat, view.MyHand[0].Id);
            return new CallLiarAction(view.ViewerSeat);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LocalMatchSession));
        }

        private readonly struct Submission
        {
            public readonly int RequestId;
            public readonly int SeatId;
            public readonly IGameAction Action;

            public Submission(int requestId, int seatId, IGameAction action)
            {
                RequestId = requestId;
                SeatId = seatId;
                Action = action;
            }
        }
    }
}
