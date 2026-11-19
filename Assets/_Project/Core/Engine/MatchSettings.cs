using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Engine
{
    /// <summary>Место за столом при старте партии.</summary>
    public sealed class SeatSetup
    {
        public int SeatId { get; }
        public string Name { get; }

        /// <summary>Фиксированная позиция пули (тесты, реплеи). null — случайная.</summary>
        public int? BulletIndex { get; set; }

        public SeatSetup(int seatId, string name)
        {
            SeatId = seatId;
            Name = name;
        }
    }

    /// <summary>Параметры партии для MatchEngine.Start. Кто управляет местом (человек/бот/сеть) ядру не важно.</summary>
    public sealed class MatchSettings
    {
        public IReadOnlyList<SeatSetup> Seats { get; }

        /// <summary>Кто ходит первым. null — случайный игрок (по спецификации).</summary>
        public int? FirstSeat { get; set; }

        public MatchSettings(IEnumerable<SeatSetup> seats)
        {
            Seats = (seats ?? throw new ArgumentNullException(nameof(seats))).ToList().AsReadOnly();
        }

        /// <summary>Места 0..count-1 с именами по умолчанию.</summary>
        public static MatchSettings ForPlayers(int count) =>
            new MatchSettings(Enumerable.Range(0, count).Select(i => new SeatSetup(i, $"Player {i + 1}")));
    }
}
