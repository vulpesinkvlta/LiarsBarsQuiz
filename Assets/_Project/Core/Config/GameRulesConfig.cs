using System;
using System.Collections.Generic;

namespace LiarsBar.Core.Config
{
    /// <summary>
    /// Все числа правил в одном месте (спецификация MVP, раздел 2).
    /// Обычный C#-класс: ScriptableObject-обёртка появится в Infrastructure.
    /// </summary>
    [Serializable]
    public sealed class GameRulesConfig
    {
        public int MinPlayers = 2;
        public int MaxPlayers = 6;

        /// <summary>Камор в револьвере, одна из них с пулей.</summary>
        public int RevolverChambers = 6;

        public int MinCardsPerPlay = 1;
        public int MaxCardsPerPlay = 2;

        /// <summary>Минимум ✓ и ✗ в каждой руке.</summary>
        public int MinTrueInHand = 1;
        public int MinFalseInHand = 1;

        /// <summary>Сколько раз пересдавать руку, прежде чем признать предмет непригодным.</summary>
        public int MaxDealAttempts = 100;

        /// <summary>Размер руки: 2–3 игрока → 5, 4 → 4, 5–6 → 3.</summary>
        public int HandSizeForSmallTable = 5;
        public int HandSizeForFour = 4;
        public int HandSizeForLargeTable = 3;

        /// <summary>Таймер хода онлайн, секунды (0 — без таймера).</summary>
        public float OnlineTurnSeconds = 30f;

        public float BotDelayMinSeconds = 2.2f;
        public float BotDelayMaxSeconds = 3.7f;

        public static GameRulesConfig Default => new GameRulesConfig();

        public int HandSizeFor(int playerCount)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), $"Players must be {MinPlayers}-{MaxPlayers}, got {playerCount}.");
            if (playerCount <= 3) return HandSizeForSmallTable;
            if (playerCount == 4) return HandSizeForFour;
            return HandSizeForLargeTable;
        }

        /// <summary>Список ошибок конфигурации; пустой — конфиг корректен.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (MinPlayers < 2) errors.Add("MinPlayers must be >= 2.");
            if (MaxPlayers < MinPlayers) errors.Add("MaxPlayers must be >= MinPlayers.");
            if (RevolverChambers < 1) errors.Add("RevolverChambers must be >= 1.");
            if (MinCardsPerPlay < 1) errors.Add("MinCardsPerPlay must be >= 1.");
            if (MaxCardsPerPlay < MinCardsPerPlay) errors.Add("MaxCardsPerPlay must be >= MinCardsPerPlay.");
            if (MinTrueInHand < 0 || MinFalseInHand < 0) errors.Add("MinTrueInHand/MinFalseInHand must be >= 0.");
            if (MaxDealAttempts < 1) errors.Add("MaxDealAttempts must be >= 1.");

            foreach (var size in new[] { HandSizeForSmallTable, HandSizeForFour, HandSizeForLargeTable })
            {
                if (size < MinTrueInHand + MinFalseInHand) errors.Add($"Hand size {size} can't fit the required ✓/✗ mix.");
                if (size < MinCardsPerPlay) errors.Add($"Hand size {size} is smaller than MinCardsPerPlay.");
            }

            if (OnlineTurnSeconds < 0) errors.Add("OnlineTurnSeconds must be >= 0.");
            if (BotDelayMinSeconds < 0 || BotDelayMaxSeconds < BotDelayMinSeconds) errors.Add("Bot delay range is invalid.");
            return errors;
        }
    }
}
