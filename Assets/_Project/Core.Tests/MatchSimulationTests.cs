using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;
using NUnit.Framework;
using static LiarsBar.Core.Tests.EngineTestKit;

namespace LiarsBar.Core.Tests
{
    /// <summary>«Готово, когда» этапа 3: полные партии случайными допустимыми ходами для 2–6 игроков.</summary>
    public class MatchSimulationTests
    {
        private const int MaxSteps = 5000;

        /// <summary>Случайный допустимый ход, выбранный только по PlayerView (как будет у бота).</summary>
        private static IGameAction RandomAction(PlayerView view, IRandom random)
        {
            if (view.CanCallLiar && (!view.CanPlayCards || random.Chance(0.35)))
                return new CallLiarAction(view.ViewerSeat);

            var cards = view.MyHand.Select(c => c.Id).ToList();
            random.Shuffle(cards);
            var count = random.Next(1, System.Math.Min(2, cards.Count) + 1);
            return new PlayCardsAction(view.ViewerSeat, cards.Take(count));
        }

        /// <summary>Играет партию до конца и возвращает журнал событий.</summary>
        private static List<GameEvent> PlayToEnd(int players, int seed, out MatchEngine engine)
        {
            engine = NewEngine(seed, Subjects(6));
            var log = new List<GameEvent>();
            var start = engine.Start(MatchSettings.ForPlayers(players));
            Assert.IsTrue(start.IsSuccess, start.ToString());
            log.AddRange(start.Events);

            var botRandom = new SeededRandom(seed * 7919 + 1);
            for (var step = 0; step < MaxSteps && engine.State.Phase != MatchPhase.Ended; step++)
            {
                AssertInvariants(engine.State);
                var view = engine.SnapshotFor(engine.State.CurrentSeat);
                Assert.IsTrue(view.IsMyTurn);
                Assert.IsTrue(view.CanPlayCards || view.CanCallLiar, "current player has no legal action");

                var result = engine.Apply(RandomAction(view, botRandom));
                Assert.IsTrue(result.IsSuccess, $"players={players} seed={seed} step={step}: {result}");
                log.AddRange(result.Events);
            }

            Assert.AreEqual(MatchPhase.Ended, engine.State.Phase, $"players={players} seed={seed}: match did not end in {MaxSteps} steps");
            return log;
        }

        private static void AssertInvariants(MatchState s)
        {
            Assert.AreEqual(MatchPhase.AwaitingAction, s.Phase);
            Assert.GreaterOrEqual(s.AliveCount, 2);
            var current = s.GetPlayer(s.CurrentSeat);
            Assert.IsTrue(current.IsAlive);
            Assert.IsTrue(current.HasCards || s.ForcedAccusation, "turn given to a player without cards");
            Assert.IsTrue(s.Players.Where(p => !p.IsAlive).All(p => p.Hand.IsEmpty));
            var ids = AllHandIds(s);
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void RandomMatches_EndWithSingleWinner([Values(2, 3, 4, 5, 6)] int players)
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var log = PlayToEnd(players, seed, out var engine);
                var state = engine.State;

                Assert.AreEqual(1, state.AliveCount);
                var ended = log.OfType<MatchEnded>().Single();
                Assert.AreEqual(state.AlivePlayers.Single().SeatId, ended.WinnerSeat);
                Assert.AreEqual(ended.WinnerSeat, state.WinnerSeat);
                Assert.IsInstanceOf<MatchEnded>(log.Last());

                Assert.AreEqual(players - 1, log.OfType<PlayerEliminated>().Count());
                Assert.AreEqual(log.OfType<LiarCalled>().Count(), log.OfType<RevolverShooted>().Count());
                Assert.AreEqual(log.OfType<RoundStarted>().Count(), log.OfType<LiarCalled>().Count(),
                    "every round ends with exactly one accusation");
                Assert.IsTrue(state.Players.All(p => p.Revolver.Shooted <= p.Revolver.Chambers));
            }
        }

        [Test]
        public void SameSeed_ReplaysTheSameMatch()
        {
            string Replay(int seed)
            {
                var log = PlayToEnd(4, seed, out _);
                var sb = new StringBuilder();
                foreach (var e in log) sb.AppendLine(e.ToString());
                return sb.ToString();
            }

            Assert.AreEqual(Replay(42), Replay(42));
            Assert.AreNotEqual(Replay(42), Replay(43));
        }
    }
}
