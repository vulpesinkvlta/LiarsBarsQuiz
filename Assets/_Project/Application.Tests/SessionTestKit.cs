using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using LiarsBar.Application.Timing;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Randomness;

namespace LiarsBar.Application.Tests
{
    /// <summary>Таймер, который двигается только вручную: тесты не ждут реальных секунд.</summary>
    internal sealed class ManualTimer : ITimerService
    {
        private sealed class Entry : IDisposable
        {
            public double Due;
            public long Order;
            public Action Callback;
            public bool Cancelled;
            public void Dispose() => Cancelled = true;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private long _order;

        public double Now { get; private set; }
        public int Pending => _entries.Count(e => !e.Cancelled);
        public List<float> RequestedDelays { get; } = new List<float>();

        public IDisposable Schedule(float delaySeconds, Action callback)
        {
            RequestedDelays.Add(delaySeconds);
            var entry = new Entry { Due = Now + delaySeconds, Order = _order++, Callback = callback };
            _entries.Add(entry);
            return entry;
        }

        /// <summary>Сдвинуть время и вызвать всё, что успело наступить.</summary>
        public void Advance(double seconds)
        {
            var target = Now + seconds;
            while (true)
            {
                _entries.RemoveAll(e => e.Cancelled);
                var next = _entries.Where(e => e.Due <= target).OrderBy(e => e.Due).ThenBy(e => e.Order).FirstOrDefault();
                if (next == null) break;
                _entries.Remove(next);
                Now = Math.Max(Now, next.Due);
                next.Callback();
            }
            Now = target;
        }

        /// <summary>Вызвать ближайший отложенный вызов. false — ничего не запланировано.</summary>
        public bool Step()
        {
            _entries.RemoveAll(e => e.Cancelled);
            if (_entries.Count == 0) return false;
            var next = _entries.OrderBy(e => e.Due).ThenBy(e => e.Order).First();
            Advance(next.Due - Now);
            return true;
        }
    }

    internal static class SessionTestKit
    {
        public static Fact True(string id) => new Fact(id, $"true {id}", true, "because", Difficulty.Easy);
        public static Fact False(string id) => new Fact(id, $"false {id}", false, "because", Difficulty.Hard);

        public static Subject Subject(string id) =>
            new Subject(id, $"Subject {id}",
                Enumerable.Range(0, 10).Select(i => True($"{id}_t{i}"))
                    .Concat(Enumerable.Range(0, 10).Select(i => False($"{id}_f{i}"))));

        public static ISubjectProvider Subjects(int count = 4) =>
            new InMemorySubjectProvider(Enumerable.Range(0, count).Select(i => Subject($"s{i}")));

        public static MatchFactory Factory(ManualTimer timer, GameRulesConfig config = null) =>
            new MatchFactory(config ?? GameRulesConfig.Default, Subjects(), BotProfile.Defaults, timer);

        /// <summary>Сессия только из ботов (зритель -1): удобно гонять партии целиком.</summary>
        public static LocalMatchSession BotsOnly(int players, ManualTimer timer, int seed)
        {
            var config = GameRulesConfig.Default;
            var subjects = Subjects();
            var strategy = new HeuristicBotStrategy(new FactCatalog(subjects), seed);
            var profiles = BotProfile.Defaults;
            var controllers = new Dictionary<int, ISeatController>();
            for (var i = 0; i < players; i++)
                controllers[i] = new BotSeat(i, strategy, profiles[i % profiles.Count], new SeededRandom(seed + i), timer,
                    config.BotDelayMinSeconds, config.BotDelayMaxSeconds);

            return new LocalMatchSession(new MatchEngine(config, subjects, new SeededRandom(seed)),
                MatchSettings.ForPlayers(players), controllers, -1);
        }

        /// <summary>Крутить таймер и ходить за человека простым допустимым ходом, пока партия не кончится.</summary>
        public static int PlayToEnd(LocalMatchSession session, ManualTimer timer, int maxSteps = 5000)
        {
            var steps = 0;
            while (!session.IsEnded)
            {
                if (++steps > maxSteps) throw new InvalidOperationException("Match did not finish.");
                var view = session.CurrentView;
                if (view.IsMyTurn)
                {
                    session.Submit(LocalMatchSession.FallbackAction(view));
                    continue;
                }
                if (!timer.Step()) throw new InvalidOperationException($"Session is stuck: seat {view.CurrentSeat}, phase {view.Phase}.");
            }
            return steps;
        }
    }
}
