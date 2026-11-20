using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Bots;

namespace LiarsBar.Application.Sessions
{
    public enum GameMode
    {
        VsBots,
        HotSeat,
        Online
    }

    /// <summary>Кто сидит на месте при сборке партии.</summary>
    public sealed class PlayerSlot
    {
        public string Name { get; }
        public bool IsBot { get; }

        /// <summary>Профиль бота; null — фабрика выберет сама.</summary>
        public BotProfile BotProfile { get; }

        private PlayerSlot(string name, bool isBot, BotProfile profile)
        {
            Name = name;
            IsBot = isBot;
            BotProfile = profile;
        }

        public static PlayerSlot Human(string name) => new PlayerSlot(name, false, null);
        public static PlayerSlot Bot(BotProfile profile = null, string name = null) => new PlayerSlot(name, true, profile);
    }

    /// <summary>Настройки режима, из которых MatchFactory собирает сессию.</summary>
    public sealed class GameModeSettings
    {
        public GameMode Mode { get; }
        public IReadOnlyList<PlayerSlot> Slots { get; }

        /// <summary>Seed партии; null — случайный.</summary>
        public int? Seed { get; set; }

        public GameModeSettings(GameMode mode, IEnumerable<PlayerSlot> slots)
        {
            Mode = mode;
            Slots = (slots ?? throw new ArgumentNullException(nameof(slots))).ToList().AsReadOnly();
        }

        /// <summary>«Играть с ботами»: человек на месте 0 и botCount ботов.</summary>
        public static GameModeSettings VsBots(string playerName, int botCount) =>
            new GameModeSettings(GameMode.VsBots,
                new[] { PlayerSlot.Human(playerName) }.Concat(Enumerable.Range(0, Math.Max(0, botCount)).Select(_ => PlayerSlot.Bot())));
    }
}
