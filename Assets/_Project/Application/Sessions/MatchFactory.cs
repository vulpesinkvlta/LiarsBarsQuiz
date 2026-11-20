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
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Mode != GameMode.VsBots)
                throw new NotSupportedException($"Mode {settings.Mode} is not supported yet.");

            var slots = settings.Slots;
            if (slots.Count < _config.MinPlayers || slots.Count > _config.MaxPlayers)
                throw new ArgumentException($"Players must be {_config.MinPlayers}-{_config.MaxPlayers}, got {slots.Count}.", nameof(settings));
            if (slots.Count(s => !s.IsBot) != 1)
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
                    controllers[seatId] = new LocalHumanSeat(seatId, name);
                    viewer = seatId;
                }
            }

            var engine = new MatchEngine(_config, _subjects, new SeededRandom(seed * 31 + 7));
            return new LocalMatchSession(engine, new MatchSettings(seats), controllers, viewer);
        }

        private static string UniqueName(string name, HashSet<string> taken)
        {
            var result = name;
            for (var i = 2; !taken.Add(result); i++) result = $"{name} {i}";
            return result;
        }
    }
}
