using System;
using System.Collections.Generic;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Bots
{
    /// <summary>
    /// Характер бота (документ MVP, раздел 6). Обычный C#-класс: ScriptableObject-обёртка лежит в Infrastructure.
    /// Все вероятности — в долях от 0 до 1.
    /// </summary>
    [Serializable]
    public sealed class BotProfile
    {
        public string Id = "bot";
        public string DisplayName = "Бот";

        /// <summary>Вероятность знать факт сложности 1 / 2 / 3.</summary>
        public float KnowEasy = 0.6f;
        public float KnowMedium = 0.4f;
        public float KnowHard = 0.2f;

        /// <summary>Обвиняет, если оценка лжи в ходе больше порога.</summary>
        public float AccuseThreshold = 0.55f;

        /// <summary>Прибавка к порогу за каждый уже сделанный выстрел: чем ближе пуля, тем осторожнее.</summary>
        public float ThresholdPerShot = 0.05f;

        /// <summary>Шанс подмешать ✗ к ✓ в ходе из 2 карт.</summary>
        public float MixLieChance = 0.2f;

        /// <summary>Базовая вероятность лжи у незнакомой карты.</summary>
        public float UnknownLieChance = 0.35f;

        /// <summary>+бонус, если у сыгравшего осталось не больше LowCardsLimit карт (его, вероятно, заставили соврать).</summary>
        public int LowCardsLimit = 1;
        public float LowCardsBonus = 0.15f;

        /// <summary>+бонус за ход из 2 карт.</summary>
        public float TwoCardsBonus = 0.10f;

        public double KnowChance(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return KnowEasy;
                case Difficulty.Hard: return KnowHard;
                default: return KnowMedium;
            }
        }

        public double ThresholdAfterShots(int shotsTaken) => AccuseThreshold + ThresholdPerShot * Math.Max(0, shotsTaken);

        public BotProfile Clone() => (BotProfile)MemberwiseClone();

        /// <summary>Список ошибок; пустой — профиль корректен.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id)) errors.Add("Id is empty.");

            void Probability(string name, float value)
            {
                if (value < 0f || value > 1f) errors.Add($"{name} must be in [0, 1], got {value}.");
            }

            Probability(nameof(KnowEasy), KnowEasy);
            Probability(nameof(KnowMedium), KnowMedium);
            Probability(nameof(KnowHard), KnowHard);
            Probability(nameof(AccuseThreshold), AccuseThreshold);
            Probability(nameof(MixLieChance), MixLieChance);
            Probability(nameof(UnknownLieChance), UnknownLieChance);
            if (ThresholdPerShot < 0f) errors.Add("ThresholdPerShot must be >= 0.");
            if (LowCardsLimit < 0) errors.Add("LowCardsLimit must be >= 0.");
            if (LowCardsBonus < 0f || TwoCardsBonus < 0f) errors.Add("Bonuses must be >= 0.");
            return errors;
        }

        public override string ToString() => Id;

        /// <summary>Осторожный: обвиняет редко, почти не врёт.</summary>
        public static BotProfile Cautious => new BotProfile
        {
            Id = "cautious", DisplayName = "Осторожный",
            KnowEasy = 0.70f, KnowMedium = 0.45f, KnowHard = 0.20f,
            AccuseThreshold = 0.70f, MixLieChance = 0.10f
        };

        /// <summary>Наглый: обвиняет и врёт охотно.</summary>
        public static BotProfile Brazen => new BotProfile
        {
            Id = "brazen", DisplayName = "Наглый",
            KnowEasy = 0.60f, KnowMedium = 0.35f, KnowHard = 0.15f,
            AccuseThreshold = 0.45f, MixLieChance = 0.24f, UnknownLieChance = 0.41f
        };

        /// <summary>Знаток: знает больше всех.</summary>
        public static BotProfile Expert => new BotProfile
        {
            Id = "expert", DisplayName = "Знаток",
            KnowEasy = 0.90f, KnowMedium = 0.70f, KnowHard = 0.45f,
            AccuseThreshold = 0.55f, MixLieChance = 0.20f
        };

        /// <summary>Три стандартных профиля (каждый раз новые копии).</summary>
        public static IReadOnlyList<BotProfile> Defaults => new[] { Cautious, Brazen, Expert };
    }
}
