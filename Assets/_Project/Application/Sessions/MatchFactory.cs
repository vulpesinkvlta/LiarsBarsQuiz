using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Timing;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Randomness;

namespace LiarsBar.Application.Sessions
{
    /// <summary>
    /// Собирает сессию по GameModeSettings: движок, места, контроллеры (человек / бот), зрителя.
    /// Режим отличается только набором контроллеров — правила одни.
    /// </summary>
    public sealed class MatchFactory
    {
        public const string DefaultHumanName = "Игрок";

        private readonly GameRulesConfig _config;
        private readonly ISubjectProvider _subjects;
        private readonly IFactKnowledge _knowledge;
        private readonly IReadOnlyList<BotProfile> _profiles;
        private readonly ITimerService _timer;

        public MatchFactory(GameRulesConfig config, ISubjectProvider subjects, IReadOnlyList<BotProfile> botProfiles,
            ITimerService timer, IFactKnowledge knowledge = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _profiles = (botProfiles ?? BotProfile.Defaults).Where(p => p != null).ToList().AsReadOnly();
            if (_profiles.Count == 0) _profiles = BotProfile.Defaults;
            _knowledge = knowledge ?? new FactCatalog(subjects);
        }

        public LocalMatchSession Create(GameModeSettings settings)
            => Create(settings, null);

        // Seat zero belongs to the host. Guest actors follow in roster order.
        public LocalMatchSession CreateHost(IReadOnlyList<KeyValuePair<int, string>> actors, int botCount)
        {
            if (actors == null || actors.Count == 0 || actors.Select(p => p.Key).Distinct().Count() != actors.Count)
                throw new ArgumentException("Expected a host and unique actor numbers.", nameof(actors));
            if (botCount < 0 || botCount > _config.MaxPlayers - actors.Count)
                throw new ArgumentOutOfRangeException(nameof(botCount));
            var slots = actors.Select(p => PlayerSlot.Human(p.Value))
                .Concat(Enumerable.Range(0, botCount).Select(_ => PlayerSlot.Bot()));
            return Create(new GameModeSettings(GameMode.Online, slots), actors);
        }

        private LocalMatchSession Create(GameModeSettings settings, IReadOnlyList<KeyValuePair<int, string>> actors)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Mode != GameMode.VsBots && settings.Mode != GameMode.HotSeat && actors == null)
                throw new NotSupportedException($"Mode {settings.Mode} is not supported yet.");

            var slots = settings.Slots;
            if (slots.Count < _config.MinPlayers || slots.Count > _config.MaxPlayers)
                throw new ArgumentException($"Players must be {_config.MinPlayers}-{_config.MaxPlayers}, got {slots.Count}.", nameof(settings));
            if (slots.Any(s => s == null) || slots.All(s => s.IsBot))
                throw new ArgumentException("At least one human is required.", nameof(settings));
            if (settings.Mode == GameMode.VsBots && slots.Count(s => !s.IsBot) != 1)
                throw new ArgumentException("Mode VsBots needs exactly one human.", nameof(settings));

            var seed = settings.Seed ?? SeededRandom.CreateUnseeded().Next(int.MaxValue);
            var setupRandom = new SeededRandom(seed);
            var strategy = new HeuristicBotStrategy(_knowledge, seed, _config.MaxCardsPerPlay);

            // Профили по кругу в случайном порядке: при 1–3 ботах все разные.
            var profileOrder = _profiles.ToList();
            setupRandom.Shuffle(profileOrder);

            var seats = new List<SeatSetup>();
            var controllers = new Dictionary<int, ISeatController>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var viewer = -1;
            var botIndex = 0;

            for (var seatId = 0; seatId < slots.Count; seatId++)
            {
                var slot = slots[seatId];
                if (slot.IsBot)
                {
                    var profile = (slot.BotProfile ?? profileOrder[botIndex % profileOrder.Count]).Clone();
                    var name = UniqueName(string.IsNullOrWhiteSpace(slot.Name) ? profile.DisplayName : slot.Name.Trim(), names);
                    seats.Add(new SeatSetup(seatId, name));
                    controllers[seatId] = new BotSeat(seatId, strategy, profile, new SeededRandom(seed ^ (0x5BD1E995 * (seatId + 1))),
                        _timer, _config.BotDelayMinSeconds, _config.BotDelayMaxSeconds);
                    botIndex++;
                }
                else
                {
                    var name = UniqueName(string.IsNullOrWhiteSpace(slot.Name) ? DefaultHumanName : slot.Name.Trim(), names);
                    seats.Add(new SeatSetup(seatId, name));
                    if (actors != null && seatId > 0)
                        controllers[seatId] = new RemoteSeat(actors[seatId].Key, seatId, name);
                    else
                        controllers[seatId] = new LocalHumanSeat(seatId, name);
                    if (viewer < 0) viewer = seatId;
                }
            }

            var engine = new MatchEngine(_config, _subjects, new SeededRandom(seed * 31 + 7));
            var turnTimer = actors == null ? null : new TurnTimer(_timer, _config.OnlineTurnSeconds,
                view => strategy.Decide(view, profileOrder[0], setupRandom));
            return new LocalMatchSession(engine, new MatchSettings(seats), controllers, viewer,
                followHumanTurn: settings.Mode == GameMode.HotSeat, turnTimer: turnTimer);
        }

        public BotSeat CreateReplacementBot(int seatId)
        {
            var random = SeededRandom.CreateUnseeded();
            var strategy = new HeuristicBotStrategy(_knowledge, random.Next(int.MaxValue), _config.MaxCardsPerPlay);
            return new BotSeat(seatId, strategy, _profiles[random.Next(_profiles.Count)].Clone(), random,
                _timer, _config.BotDelayMinSeconds, _config.BotDelayMaxSeconds);
        }

        private static string UniqueName(string name, HashSet<string> taken)
        {
            var result = name;
            for (var i = 2; !taken.Add(result); i++) result = $"{name} {i}";
            return result;
        }
    }
}
