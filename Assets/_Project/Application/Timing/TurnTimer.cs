using System;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Timing
{
    public sealed class TurnTimer : IDisposable
    {
        private readonly ITimerService _clock;
        private readonly float _seconds;
        private readonly Func<PlayerView, IGameAction> _decide;
        private IDisposable _pending;
        private double _deadline;
        private Action _onExpiry;

        public TurnTimer(ITimerService clock, float seconds, Func<PlayerView, IGameAction> decide)
        {
            _clock = clock;
            _seconds = seconds;
            _decide = decide;
        }

        public int? RemainingSeconds => _pending == null ? (int?)null :
            (int)Math.Ceiling(Math.Max(0, _deadline - _clock.Now));

        public void Start(PlayerView view, Action<IGameAction> submit)
        {
            Dispose();
            _deadline = _clock.Now + _seconds;
            _onExpiry = () => submit(_decide(view));
            _pending = _clock.Schedule(_seconds, Expire);
        }

        public bool ExpireIfDue()
        {
            if (_pending == null || _clock.Now < _deadline) return false;
            Expire();
            return true;
        }

        private void Expire()
        {
            var callback = _onExpiry;
            Dispose();
            callback?.Invoke();
        }

        public void Dispose()
        {
            _pending?.Dispose();
            _pending = null;
            _onExpiry = null;
        }
    }
}
