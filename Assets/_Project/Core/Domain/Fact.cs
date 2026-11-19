using System;

namespace LiarsBar.Core.Domain
{
    /// <summary>Сложность факта: насколько его трудно распознать. Используется ботами и балансом контента.</summary>
    public enum Difficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3
    }

    /// <summary>
    /// Утверждение о предмете: одна карта в игре. Неизменяемый объект.
    /// Id карты равен Id факта: в одной партии факт встречается не больше одного раза.
    /// </summary>
    public sealed class Fact
    {
        public string Id { get; }
        public string Text { get; }
        public bool IsTrue { get; }
        public string Explanation { get; }
        public Difficulty Difficulty { get; }

        public Fact(string id, string text, bool isTrue, string explanation, Difficulty difficulty = Difficulty.Medium)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Fact id is empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Fact text is empty.", nameof(text));

            Id = id;
            Text = text;
            IsTrue = isTrue;
            Explanation = explanation ?? string.Empty;
            Difficulty = difficulty;
        }

        public override string ToString() => $"{Id} [{(IsTrue ? "✓" : "✗")}] {Text}";
    }
}
