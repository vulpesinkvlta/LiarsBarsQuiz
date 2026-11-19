using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;

namespace LiarsBar.Core.Bots
{
    /// <summary>Итог прогона «боты против ботов» для проверки баланса.</summary>
    public sealed class BotSimulationResult
    {
        public int Matches { get; internal set; }
        public int Players { get; internal set; }
        public int TotalRounds { get; internal set; }
        public int MinRounds { get; internal set; } = int.MaxValue;
        public int MaxRounds { get; internal set; }
        public int Accusations { get; internal set; }
        public int CorrectAccusations { get; internal set; }
        public int ForcedAccusations { get; internal set; }
        public int CardsPlayed { get; internal set; }
        public int LiesPlayed { get; internal set; }

        /// <summary>Победы по Id профиля.</summary>
        public Dictionary<string, int> Wins { get; } = new Dictionary<string, int>();

        /// <summary>Сколько мест занимал профиль за все партии.</summary>
        public Dictionary<string, int> SeatsPlayed { get; } = new Dictionary<string, int>();

        public double AverageRounds => Matches == 0 ? 0 : (double)TotalRounds / Matches;

        /// <summary>Доля верных обвинений (в ходе была ✗) среди всех, включая принудительные.</summary>
        public double CorrectAccusationRate => Accusations == 0 ? 0 : (double)CorrectAccusations / Accusations;

        /// <summary>Доля партий, выигранных профилем.</summary>
        public double WinRate(string profileId) =>
            Matches == 0 ? 0 : (double)Wins.GetValueOrDefault(profileId) / Matches;

        /// <summary>Победы на одно занятое место: честное сравнение, когда профиль сидит за столом дважды.</summary>
        public double WinRatePerSeat(string profileId)
        {
            var seats = SeatsPlayed.GetValueOrDefault(profileId);
            return seats == 0 ? 0 : (double)Wins.GetValueOrDefault(profileId) / seats;
        }

        public string ToReport()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(ci, "Bots: {0} matches x {1} players", Matches, Players));
            sb.AppendLine(string.Format(ci, "Rounds: avg {0:0.00}, min {1}, max {2}", AverageRounds, MinRounds, MaxRounds));
            sb.AppendLine(string.Format(ci, "Accusations: {0}, correct {1:P1}, forced {2}", Accusations, CorrectAccusationRate, ForcedAccusations));
            sb.AppendLine(string.Format(ci, "Cards played: {0}, lies {1:P1}", CardsPlayed, CardsPlayed == 0 ? 0 : (double)LiesPlayed / CardsPlayed));
            foreach (var id in SeatsPlayed.Keys.OrderBy(k => k, StringComparer.Ordinal))
                sb.AppendLine(string.Format(ci, "  {0,-10} wins {1,4} ({2:P1} of matches, {3:P1} per seat, seats {4})",
                    id, Wins.GetValueOrDefault(id), WinRate(id), WinRatePerSeat(id), SeatsPlayed[id]));
            return sb.ToString();
        }

        public override string ToString() => ToReport();
    }

    /// <summary>
    /// Прогон партий «боты против ботов» на MatchEngine. Боты видят только свой PlayerView.
    /// Профили садятся по кругу со сдвигом на каждую партию: profiles[(seat + match) % count].
    /// </summary>
    public static class BotSimulation
    {
        public const int MaxStepsPerMatch = 5000;

        /// <exception cref="InvalidOperationException">Партия не стартовала, бот выбрал недопустимый ход или партия не закончилась.</exception>
        public static BotSimulationResult Run(ISubjectProvider subjects, IReadOnlyList<BotProfile> profiles,
            int matches, int players, int seed = 1, GameRulesConfig config = null)
        {
            if (subjects == null) throw new ArgumentNullException(nameof(subjects));
            if (profiles == null || profiles.Count == 0) throw new ArgumentException("No bot profiles.", nameof(profiles));
            config = config ?? GameRulesConfig.Default;

            var knowledge = new FactCatalog(subjects);
            var result = new BotSimulationResult { Matches = matches, Players = players };
            foreach (var p in profiles)
            {
                result.Wins[p.Id] = 0;
                result.SeatsPlayed[p.Id] = 0;
            }

            for (var m = 0; m < matches; m++)
            {
                var matchSeed = unchecked(seed * 1000003 + m);
                var engine = new MatchEngine(config, subjects, new SeededRandom(matchSeed));
                var strategy = new HeuristicBotStrategy(knowledge, salt: matchSeed, maxCardsPerPlay: config.MaxCardsPerPlay);
                var botRandom = new SeededRandom(unchecked(matchSeed * 7919 + 17));

                var seatProfiles = Enumerable.Range(0, players).Select(i => profiles[(i + m) % profiles.Count]).ToArray();
                var settings = new MatchSettings(Enumerable.Range(0, players)
                    .Select(i => new SeatSetup(i, $"{seatProfiles[i].DisplayName} {i + 1}")));
                foreach (var p in seatProfiles) result.SeatsPlayed[p.Id]++;

                var start = engine.Start(settings);
                if (!start.IsSuccess) throw new InvalidOperationException($"match {m}: start failed: {start}");
                Collect(result, start.Events, knowledge);

                var step = 0;
                for (; step < MaxStepsPerMatch && engine.State.Phase != MatchPhase.Ended; step++)
                {
                    var seat = engine.State.CurrentSeat;
                    var view = engine.SnapshotFor(seat);
                    var action = strategy.Decide(view, seatProfiles[seat], botRandom);
                    var applied = engine.Apply(action);
                    if (!applied.IsSuccess)
                        throw new InvalidOperationException($"match {m} step {step}: bot {seatProfiles[seat]} chose {action}: {applied}");
                    Collect(result, applied.Events, knowledge);
                }

                if (engine.State.Phase != MatchPhase.Ended)
                    throw new InvalidOperationException($"match {m}: did not end in {MaxStepsPerMatch} steps");

                result.Wins[seatProfiles[engine.State.WinnerSeat].Id]++;
            }

            return result;
        }

        private static void Collect(BotSimulationResult result, IReadOnlyList<GameEvent> events, IFactKnowledge knowledge)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case CardsPlayed played:
                        result.CardsPlayed += played.Cards.Count;
                        result.LiesPlayed += played.Cards.Count(c => knowledge.TryGetFact(c.Id, out var f) && !f.IsTrue);
                        break;
                    case LiarCalled called:
                        result.Accusations++;
                        if (called.WasForced) result.ForcedAccusations++;
                        break;
                    case FactsRevealed revealed:
                        if (revealed.LieFound) result.CorrectAccusations++;
                        break;
                    case MatchEnded ended:
                        result.TotalRounds += ended.RoundsPlayed;
                        result.MinRounds = Math.Min(result.MinRounds, ended.RoundsPlayed);
                        result.MaxRounds = Math.Max(result.MaxRounds, ended.RoundsPlayed);
                        break;
                }
            }
        }
    }
}
