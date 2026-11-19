namespace LiarsBar.Core.Domain
{
    /// <summary>Статистика игрока за партию: основа для номинаций на финальном экране.</summary>
    public sealed class PlayerStats
    {
        /// <summary>Сыгранные ✗-карты, на которых игрока не поймали («Лучший лжец»).</summary>
        public int UncaughtLies { get; set; }

        /// <summary>Обвинения, в которых сыгравший действительно соврал («Детектор лжи»).</summary>
        public int CorrectAccusations { get; set; }

        public int WrongAccusations { get; set; }

        /// <summary>Сколько раз игрок жал на спуск и остался жив («Борис хрен попадешь»).</summary>
        public int ShotsSurvived { get; set; }

        public int CardsPlayed { get; set; }

        public PlayerStats Clone() => (PlayerStats)MemberwiseClone();
    }
}
