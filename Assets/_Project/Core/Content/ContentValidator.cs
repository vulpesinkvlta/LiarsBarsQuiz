using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Content
{
    /// <summary>Требования к контенту из спецификации (раздел 6 первого документа).</summary>
    public sealed class ContentRules
    {
        public int FactsPerSubject { get; set; } = 20;
        public int TrueFactsPerSubject { get; set; } = 10;
        public int MaxTextLength { get; set; } = 90;
        public int MaxExplanationLength { get; set; } = 120;

        public static ContentRules Default => new ContentRules();
    }

    /// <summary>
    /// Проверка предметов на соответствие <see cref="ContentRules"/>.
    /// Возвращает список понятных ошибок; пустой список — контент годен.
    /// </summary>
    public static class ContentValidator
    {
        public static IReadOnlyList<string> Validate(IEnumerable<Subject> subjects, ContentRules rules = null)
        {
            rules = rules ?? ContentRules.Default;
            var errors = new List<string>();
            var list = (subjects ?? throw new ArgumentNullException(nameof(subjects))).ToList();

            if (list.Count == 0) errors.Add("No subjects.");

            foreach (var dup in list.GroupBy(s => s.Id).Where(g => g.Count() > 1))
                errors.Add($"Duplicate subject id '{dup.Key}'.");

            foreach (var dup in list.SelectMany(s => s.Facts).GroupBy(f => f.Id).Where(g => g.Count() > 1))
                errors.Add($"Fact id '{dup.Key}' is used {dup.Count()} times.");

            foreach (var subject in list) ValidateSubject(subject, rules, errors);
            return errors;
        }

        private static void ValidateSubject(Subject s, ContentRules rules, List<string> errors)
        {
            var at = $"[{s.Id}]";
            if (string.IsNullOrWhiteSpace(s.Title)) errors.Add($"{at} title is empty.");
            if (s.Facts.Count != rules.FactsPerSubject)
                errors.Add($"{at} has {s.Facts.Count} facts, expected {rules.FactsPerSubject}.");
            if (s.TrueCount != rules.TrueFactsPerSubject)
                errors.Add($"{at} has {s.TrueCount} true facts, expected {rules.TrueFactsPerSubject}.");
            var falseExpected = rules.FactsPerSubject - rules.TrueFactsPerSubject;
            if (s.FalseCount != falseExpected)
                errors.Add($"{at} has {s.FalseCount} false facts, expected {falseExpected}.");

            foreach (var dup in s.Facts.GroupBy(f => Normalize(f.Text)).Where(g => g.Count() > 1))
                errors.Add($"{at} repeated fact text: «{dup.First().Text}».");

            foreach (var f in s.Facts)
            {
                var fat = $"[{s.Id}/{f.Id}]";
                if (!f.Id.StartsWith(s.Id + "_", StringComparison.Ordinal))
                    errors.Add($"{fat} fact id must start with '{s.Id}_'.");
                if (f.Text.Length > rules.MaxTextLength)
                    errors.Add($"{fat} text is {f.Text.Length} chars, max {rules.MaxTextLength}.");
                if (string.IsNullOrWhiteSpace(f.Explanation))
                    errors.Add($"{fat} explanation is empty.");
                else if (f.Explanation.Length > rules.MaxExplanationLength)
                    errors.Add($"{fat} explanation is {f.Explanation.Length} chars, max {rules.MaxExplanationLength}.");
                if (!Enum.IsDefined(typeof(Difficulty), f.Difficulty))
                    errors.Add($"{fat} difficulty {(int)f.Difficulty} is out of range 1–3.");
                if (f.Text != f.Text.Trim() || f.Explanation != f.Explanation.Trim())
                    errors.Add($"{fat} has leading or trailing spaces.");
            }
        }

        private static string Normalize(string text) => text.Trim().ToLowerInvariant();

        /// <summary>Все символы, которые встречаются в тексте контента: для проверки атласа шрифта.</summary>
        public static ISet<char> CollectCharacters(IEnumerable<Subject> subjects)
        {
            var set = new SortedSet<char>();
            foreach (var s in subjects)
            {
                AddChars(set, s.Title);
                foreach (var f in s.Facts)
                {
                    AddChars(set, f.Text);
                    AddChars(set, f.Explanation);
                }
            }
            return set;
        }

        private static void AddChars(ISet<char> set, string text)
        {
            if (text == null) return;
            foreach (var c in text)
                if (!char.IsControl(c)) set.Add(c);
        }
    }
}
