using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar.Core.Domain
{
    public enum MatchPhase
    {
        /// <summary>Партия создана, раздачи ещё не было.</summary>
        NotStarted,

        /// <summary>Идёт раунд: текущий игрок играет карты или кричит «Лжец!».</summary>
        AwaitingAction,

        /// <summary>Обвинение разрешено, кто-то играет в русскую рулетку.</summary>
        Resolving,

        /// <summary>Живым остался один игрок.</summary>
        Ended
    }

    /// <summary>
    /// Полное состояние партии (истина, видимая только движку и хосту).
    /// Меняет его только MatchEngine (этап 3); UI, сеть и боты получают проекцию PlayerView.
    /// </summary>
    public sealed class MatchState
    {
        private readonly List<PlayerState> _players;
        private readonly HashSet<string> _usedSubjectIds;

        public IReadOnlyList<PlayerState> Players => _players;
        public IReadOnlyCollection<string> UsedSubjectIds => _usedSubjectIds;

        public MatchPhase Phase { get; set; } = MatchPhase.NotStarted;
        public int RoundNumber { get; set; }
        public Subject CurrentSubject { get; set; }

        /// <summary>Чей ход (SeatId); -1, если ход не определён.</summary>
        public int CurrentSeat { get; set; } = -1;

        /// <summary>Последний ход на столе в текущем раунде; null — первый ход раунда.</summary>
        public TablePlay LastPlay { get; set; }

        /// <summary>Текущий игрок обязан крикнуть «Лжец!»: карты остались только у сыгравшего.</summary>
        public bool ForcedAccusation { get; set; }

        /// <summary>Победитель (SeatId) после окончания партии, иначе -1.</summary>
        public int WinnerSeat { get; set; } = -1;

        public bool IsFirstTurnOfRound => LastPlay == null;
        public IEnumerable<PlayerState> AlivePlayers => _players.Where(p => p.IsAlive);
        public int AliveCount => _players.Count(p => p.IsAlive);

        public MatchState(IEnumerable<PlayerState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            _players = players.OrderBy(p => p.SeatId).ToList();
            if (_players.Select(p => p.SeatId).Distinct().Count() != _players.Count)
                throw new ArgumentException("Duplicate seat ids.", nameof(players));
            _usedSubjectIds = new HashSet<string>();
        }

        public PlayerState GetPlayer(int seatId)
        {
            var player = _players.FirstOrDefault(p => p.SeatId == seatId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(seatId), $"No player at seat {seatId}.");
            return player;
        }

        public bool TryGetPlayer(int seatId, out PlayerState player)
        {
            player = _players.FirstOrDefault(p => p.SeatId == seatId);
            return player != null;
        }

        public void MarkSubjectUsed(string subjectId) => _usedSubjectIds.Add(subjectId);
        public void ResetUsedSubjects() => _usedSubjectIds.Clear();

        /// <summary>Глубокая копия: нужна движку, чтобы невалидное действие не меняло состояние, и тестам.</summary>
        public MatchState Clone()
        {
            var copy = new MatchState(_players.Select(p => p.Clone()))
            {
                Phase = Phase,
                RoundNumber = RoundNumber,
                CurrentSubject = CurrentSubject,
                CurrentSeat = CurrentSeat,
                LastPlay = LastPlay,
                ForcedAccusation = ForcedAccusation,
                WinnerSeat = WinnerSeat
            };
            foreach (var id in _usedSubjectIds) copy._usedSubjectIds.Add(id);
            return copy;
        }
    }
}
