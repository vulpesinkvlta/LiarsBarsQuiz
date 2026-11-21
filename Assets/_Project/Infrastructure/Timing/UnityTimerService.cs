using System;
using System.Collections.Generic;
using LiarsBar.Application.Timing;
using UnityEngine;

namespace LiarsBar.Infrastructure.Timing
{
    /// <summary>ITimerService на Update: отложенные вызовы без корутин в Application и Core.</summary>
    public sealed class UnityTimerService : MonoBehaviour, ITimerService
    {
        private sealed class Entry : IDisposable
        {
            public double Due;
            public Action Callback;
            public bool Cancelled;
            public void Dispose() => Cancelled = true;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _due = new List<Entry>();
        public double Now => Time.unscaledTimeAsDouble;

        public IDisposable Schedule(float delaySeconds, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var entry = new Entry { Due = Now + Mathf.Max(0f, delaySeconds), Callback = callback };
            _entries.Add(entry);
            return entry;
        }

        private void Update()
        {
            if (_entries.Count == 0) return;

            var now = Now;
            _due.Clear();
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Cancelled)
                {
                    _entries.RemoveAt(i);
                }
                else if (e.Due <= now)
                {
                    _entries.RemoveAt(i);
                    _due.Add(e);
                }
            }

            // Вызовы могут планировать новые — они попадут в _entries и сработают в следующих кадрах.
            for (var i = _due.Count - 1; i >= 0; i--)
            {
                var e = _due[i];
                if (e.Cancelled) continue;
                try
                {
                    e.Callback();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        private void OnDestroy() => _entries.Clear();
    }
}
