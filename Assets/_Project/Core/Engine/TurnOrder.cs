using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Engine
{
    /// <summary>Кто ходит следующим. Порядок мест — по возрастанию SeatId, по кругу.</summary>
    public static class TurnOrder
    {
        public readonly struct NextTurn
        {
            public int SeatId { get; }

            /// <summary>Игрок обязан крикнуть «Лжец!».</summary>
            public bool Forced { get; }

            public NextTurn(int seatId, bool forced)
            {
                SeatId = seatId;
                Forced = forced;
            }
        }

        /// <summary>Места по кругу после fromSeat (сам fromSeat не входит).</summary>
        public static IEnumerable<PlayerState> After(MatchState state, int fromSeat)
        {
            var players = state.Players;
            var start = -1;
            for (var i = 0; i < players.Count; i++)
                if (players[i].SeatId == fromSeat) start = i;
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(fromSeat), $"No player at seat {fromSeat}.");

            for (var step = 1; step < players.Count; step++)
                yield return players[(start + step) % players.Count];
        }

        /// <summary>Следующий живой после fromSeat; -1, если других живых нет.</summary>
        public static int NextAlive(MatchState state, int fromSeat) =>
            After(state, fromSeat).FirstOrDefault(p => p.IsAlive)?.SeatId ?? -1;

        /// <summary>
        /// Ход после того, как playedSeat положил карты: следующий живой с картами.
        /// Если карты не остались ни у кого, кроме сыгравшего, следующий живой обязан крикнуть «Лжец!» —
        /// так раунд всегда заканчивается проверкой.
        /// </summary>
        public static NextTurn AfterPlay(MatchState state, int playedSeat)
        {
            var withCards = After(state, playedSeat).FirstOrDefault(p => p.IsAlive && p.HasCards);
            if (withCards != null) return new NextTurn(withCards.SeatId, false);

            var next = NextAlive(state, playedSeat);
            if (next < 0) throw new InvalidOperationException("No other alive player to accuse.");
            return new NextTurn(next, true);
        }

        /// <summary>Кто начинает новый раунд: тот, кто стрелял, а если он выбыл — следующий живой.</summary>
        public static int RoundStarter(MatchState state, int shooterSeat)
        {
            var shooter = state.GetPlayer(shooterSeat);
            return shooter.IsAlive ? shooterSeat : NextAlive(state, shooterSeat);
        }
    }
}
