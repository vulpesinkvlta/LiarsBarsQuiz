using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using NUnit.Framework;

namespace LiarsBar.Core.Tests
{
    /// <summary>Хелперы для тестов движка.</summary>
    internal static class EngineTestKit
    {
        public static ISubjectProvider Subjects(int count = 4) =>
            new InMemorySubjectProvider(Enumerable.Range(0, count).Select(i => TestFacts.Subject($"s{i}")));

        public static MatchEngine NewEngine(int seed = 1, ISubjectProvider subjects = null, GameRulesConfig config = null) =>
            new MatchEngine(config ?? GameRulesConfig.Default, subjects ?? Subjects(), new SeededRandom(seed));

        /// <summary>Партия на count мест с фиксированными пулями и первым ходящим.</summary>
        public static MatchEngine Started(int count, int firstSeat = 0, int bulletIndex = 5, int seed = 1)
        {
            var engine = NewEngine(seed);
            var settings = new MatchSettings(Enumerable.Range(0, count)
                .Select(i => new SeatSetup(i, $"P{i}") { BulletIndex = bulletIndex }))
            {
                FirstSeat = firstSeat
            };
            var result = engine.Start(settings);
            Assert.IsTrue(result.IsSuccess, result.ToString());
            return engine;
        }

        public static PlayerState Current(MatchEngine engine) => engine.State.GetPlayer(engine.State.CurrentSeat);

        public static ApplyResult Ok(MatchEngine engine, IGameAction action)
        {
            var result = engine.Apply(action);
            Assert.IsTrue(result.IsSuccess, $"{action}: {result}");
            return result;
        }

        /// <summary>Текущий игрок кладёт n первых карт руки.</summary>
        public static ApplyResult PlayFirst(MatchEngine engine, int n)
        {
            var player = Current(engine);
            return Ok(engine, new PlayCardsAction(player.SeatId, player.Hand.Cards.Take(n).Select(c => c.Id)));
        }

        /// <summary>Текущий игрок кладёт одну карту нужной правдивости.</summary>
        public static ApplyResult PlayOne(MatchEngine engine, bool isTrue)
        {
            var player = Current(engine);
            var card = player.Hand.Cards.First(c => c.IsTrue == isTrue);
            return Ok(engine, new PlayCardsAction(player.SeatId, card.Id));
        }

        public static ApplyResult CallLiar(MatchEngine engine) => Ok(engine, new CallLiarAction(engine.State.CurrentSeat));

        public static T Single<T>(ApplyResult result) where T : GameEvent => result.Events.OfType<T>().Single();

        /// <summary>Отпечаток состояния: если он не изменился, состояние не менялось.</summary>
        public static string Fingerprint(MatchState s)
        {
            var sb = new StringBuilder();
            sb.Append($"{s.Phase}|{s.RoundNumber}|{s.CurrentSubject?.Id}|{s.CurrentSeat}|{s.ForcedAccusation}|{s.WinnerSeat}|");
            sb.Append(s.LastPlay == null ? "-" : s.LastPlay.ToString()).Append('|');
            sb.Append(string.Join(",", s.UsedSubjectIds.OrderBy(x => x))).Append('|');
            foreach (var p in s.Players)
            {
                var st = p.Stats;
                sb.Append($"[{p.SeatId}:{p.IsAlive}:{p.Revolver.Shooted}:{p.Revolver.IsFired}:{p.Hand}:" +
                          $"{st.UncaughtLies},{st.CorrectAccusations},{st.WrongAccusations},{st.ShotsSurvived},{st.CardsPlayed}]");
            }
            return sb.ToString();
        }

        public static IReadOnlyList<string> AllHandIds(MatchState s) =>
            s.Players.SelectMany(p => p.Hand.Cards).Select(c => c.Id).ToList();
    }
}
