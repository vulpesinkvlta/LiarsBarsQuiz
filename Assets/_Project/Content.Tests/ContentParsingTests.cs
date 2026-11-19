using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Infrastructure.Content;
using NUnit.Framework;

namespace LiarsBar.Content.Tests
{
    /// <summary>Разбор JSON и JsonSubjectProvider без Resources.</summary>
    public class SubjectJsonParserTests
    {
        private const string Valid = @"{
  ""id"": ""octopus"", ""title"": ""Осьминог"",
  ""facts"": [
    { ""id"": ""octopus_01"", ""text"": ""У осьминога три сердца"", ""isTrue"": true, ""difficulty"": 2, ""explanation"": ""Два — для жабр"" },
    { ""id"": ""octopus_02"", ""text"": ""Живут 30 лет"", ""isTrue"": false, ""difficulty"": 1, ""explanation"": ""1–2 года"" }
  ]
}";

        [Test]
        public void Parse_ValidJson_MapsAllFields()
        {
            var s = SubjectJsonParser.Parse(Valid);
            Assert.AreEqual("octopus", s.Id);
            Assert.AreEqual("Осьминог", s.Title);
            Assert.AreEqual(2, s.Facts.Count);

            var f = s.Facts[0];
            Assert.AreEqual("octopus_01", f.Id);
            Assert.AreEqual("У осьминога три сердца", f.Text);
            Assert.IsTrue(f.IsTrue);
            Assert.AreEqual(Difficulty.Medium, f.Difficulty);
            Assert.AreEqual("Два — для жабр", f.Explanation);
            Assert.IsFalse(s.Facts[1].IsTrue);
            Assert.AreEqual(Difficulty.Easy, s.Facts[1].Difficulty);
        }

        [TestCase("\"difficulty\": 2, ", "", "difficulty")]                 // пропущено обязательное поле
        [TestCase("\"difficulty\": 2", "\"difficulty\": 4", "difficulty")]  // сложность вне 1–3
        [TestCase("\"isTrue\": true", "\"isTrue\": true, \"extra\": 1", "extra")]  // опечатка / лишнее поле
        [TestCase("\"octopus_02\"", "\"octopus_01\"", "octopus_01")]       // повтор Id факта
        [TestCase("\"У осьминога три сердца\"", "\"\"", "text")]           // пустой текст
        public void Parse_BrokenJson_ThrowsWithSourceName(string from, string to, string expectedInMessage)
        {
            var broken = ReplaceFirst(Valid, from, to);
            var e = Assert.Throws<ContentFormatException>(() => SubjectJsonParser.Parse(broken, "octopus.json"));
            StringAssert.Contains("octopus.json", e.Message);
            StringAssert.Contains(expectedInMessage, e.Message);
        }

        [TestCase("")]
        [TestCase("{ not json")]
        [TestCase("null")]
        public void Parse_Garbage_Throws(string json)
        {
            Assert.Throws<ContentFormatException>(() => SubjectJsonParser.Parse(json, "x.json"));
        }

        [Test]
        public void Provider_SortsById_AndCachesResult()
        {
            var provider = new JsonSubjectProvider(new[]
            {
                Source("b.json", Valid.Replace("octopus", "zebra")),
                Source("a.json", Valid)
            });

            var subjects = provider.GetSubjects();
            CollectionAssert.AreEqual(new[] { "octopus", "zebra" }, subjects.Select(s => s.Id).ToArray());
            Assert.AreSame(subjects, provider.GetSubjects());
        }

        [Test]
        public void Provider_DuplicateSubjectAcrossFiles_Throws()
        {
            var provider = new JsonSubjectProvider(new[] { Source("a.json", Valid), Source("b.json", Valid) });
            var e = Assert.Throws<ContentFormatException>(() => provider.GetSubjects());
            StringAssert.Contains("octopus", e.Message);
        }

        private static KeyValuePair<string, string> Source(string name, string json) => new KeyValuePair<string, string>(name, json);

        private static string ReplaceFirst(string text, string from, string to)
        {
            var i = text.IndexOf(from, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0, $"fixture does not contain '{from}'");
            return text.Substring(0, i) + to + text.Substring(i + from.Length);
        }
    }

    /// <summary>Правила валидатора на синтетических данных.</summary>
    public class ContentValidatorTests
    {
        private static Subject Make(string id = "s", int trueCount = 10, int falseCount = 10,
            string text = null, string explanation = "Пояснение", Difficulty difficulty = Difficulty.Easy, string idPrefix = null)
        {
            var facts = Enumerable.Range(0, trueCount + falseCount).Select(i =>
                new Fact($"{idPrefix ?? id}_{i:00}", text ?? $"Факт номер {i}", i < trueCount, explanation, difficulty));
            return new Subject(id, "Предмет " + id, facts);
        }

        private static IReadOnlyList<string> Errors(params Subject[] subjects) => ContentValidator.Validate(subjects);

        [Test]
        public void ValidSubject_NoErrors()
        {
            CollectionAssert.IsEmpty(Errors(Make("a"), Make("b")));
        }

        [Test]
        public void NoSubjects_IsError() => CollectionAssert.IsNotEmpty(Errors());

        [TestCase(10, 9)]
        [TestCase(9, 11)]
        [TestCase(11, 11)]
        public void WrongFactCounts_AreErrors(int t, int f)
        {
            CollectionAssert.IsNotEmpty(Errors(Make(trueCount: t, falseCount: f)));
        }

        [Test]
        public void TextLengthLimit_Is90()
        {
            Assert.IsFalse(Errors(Make(text: null)).Any(e => e.Contains("text is")));
            var s90 = new Subject("s", "S", Enumerable.Range(0, 20).Select(i =>
                new Fact($"s_{i:00}", i.ToString("00") + new string('я', 88), i < 10, "x")));
            CollectionAssert.IsEmpty(Errors(s90));
            var s91 = new Subject("s", "S", Enumerable.Range(0, 20).Select(i =>
                new Fact($"s_{i:00}", i.ToString("00") + new string('я', 89), i < 10, "x")));
            Assert.AreEqual(20, Errors(s91).Count(e => e.Contains("text is 91")));
        }

        [Test]
        public void ExplanationLimit_Is120_AndRequired()
        {
            CollectionAssert.IsEmpty(Errors(Make(explanation: new string('я', 120))));
            Assert.IsTrue(Errors(Make(explanation: new string('я', 121))).Any(e => e.Contains("explanation is 121")));
            Assert.IsTrue(Errors(Make(explanation: " ")).Any(e => e.Contains("explanation is empty")));
        }

        [Test]
        public void DuplicateIds_AcrossSubjects_AreErrors()
        {
            Assert.IsTrue(Errors(Make("a"), Make("a")).Any(e => e.Contains("Duplicate subject id 'a'")));
            Assert.IsTrue(Errors(Make("a"), Make("b", idPrefix: "a")).Any(e => e.Contains("Fact id 'a_00'")));
        }

        [Test]
        public void FactIdWithoutSubjectPrefix_IsError()
        {
            Assert.IsTrue(Errors(Make("a", idPrefix: "x")).Any(e => e.Contains("must start with 'a_'")));
        }

        [Test]
        public void RepeatedText_IsError()
        {
            Assert.IsTrue(Errors(Make(text: "Одно и то же")).Any(e => e.Contains("repeated fact text")));
        }

        [Test]
        public void UndefinedDifficulty_IsError()
        {
            Assert.IsTrue(Errors(Make(difficulty: (Difficulty)7)).Any(e => e.Contains("difficulty 7")));
        }

        [Test]
        public void CollectCharacters_TakesTitleTextAndExplanation()
        {
            var s = new Subject("s", "Ж", new[] { new Fact("s_1", "ё", true, "✓") });
            CollectionAssert.AreEquivalent(new[] { 'Ж', 'ё', '✓' }, ContentValidator.CollectCharacters(new[] { s }));
        }
    }
}
