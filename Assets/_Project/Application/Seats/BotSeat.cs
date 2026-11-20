using System;
using LiarsBar.Application.Timing;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Seats
{
    /// <summary>
    /// Место бота: решает через IBotStrategy по своему PlayerView и отдаёт ход
    /// после паузы 1,0–2,5 с (чтобы ход было видно). Пауза — через ITimerService, а не корутины.
    /// </summary>
    public sealed class BotSeat : ISeatController
    {
        private readonly IBotStrategy _strategy;
        private readonly IRandom _random;
        private readonly ITimerService _timer;
        private readonly float _minDelay;
        private readonly float _maxDelay;
        private IDisposable _scheduled;

        public int SeatId { get; }
        public BotProfile Profile { get; }

        /// <summary>Пауза последнего запроса, секунды (для тестов и отладки).</summary>
        public float LastDelay { get; private set; }

        public BotSeat(int seatId, IBotStrategy strategy, BotProfile profile, IRandom random, ITimerService timer,
            float minDelaySeconds, float maxDelaySeconds)
        {
            SeatId = seatId;
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _minDelay = Math.Max(0f, minDelaySeconds);
            _maxDelay = Math.Max(_minDelay, maxDelaySeconds);
        }

        public void RequestAction(PlayerView view, Action<IGameAction> submit)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (submit == null) throw new ArgumentNullException(nameof(submit));

            Cancel();
            LastDelay = _minDelay + (float)_random.NextDouble() * (_maxDelay - _minDelay);
            _scheduled = _timer.Schedule(LastDelay, () =>
            {
                _scheduled = null;
                submit(_strategy.Decide(view, Profile, _random));
            });
        }

        public void Cancel()
        {
            _scheduled?.Dispose();
            _scheduled = null;
        }
    }
}
