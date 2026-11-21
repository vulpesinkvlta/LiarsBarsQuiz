using System;

namespace LiarsBar.Application.Timing
{
    /// <summary>
    /// Отложенный вызов. Ядро и Application не знают про корутины и Update:
    /// в игре это UnityTimerService, в тестах — ручной таймер.
    /// </summary>
    public interface ITimerService
    {
        double Now { get; }

        /// <summary>Вызвать callback через delaySeconds. Dispose у результата отменяет вызов.</summary>
        IDisposable Schedule(float delaySeconds, Action callback);
    }
}
