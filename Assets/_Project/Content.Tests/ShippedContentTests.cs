using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;
using LiarsBar.Editor;
using LiarsBar.Infrastructure.Content;
using NUnit.Framework;
using TMPro;
using UnityEditor;

namespace LiarsBar.Content.Tests
{
    /// <summary>
    /// «Готово, когда» этапа 4: настоящий контент из Resources/Content проходит валидатор,
    /// все его символы есть в атласе шрифта, и движок доигрывает на нём партии.
    /// </summary>
    public class ShippedContentTests
    {
        private const int MinSubjects = 4;

        private static System.Collections.Generic.IReadOnlyList<Subject> Load() =>
            JsonSubjectProvider.FromResources().GetSubjects();

        [Test]
        public void Content_LoadsFromResources()
        {
            var subjects = Load();
            Assert.GreaterOrEqual(subjects.Count, MinSubjects, "expected at least the 4 subjects of content v1");
            foreach (var id in new[] { "octopus", "eiffel_tower", "coffee", "moon" })
                Assert.IsTrue(subjects.Any(s => s.Id == id), $"subject '{id}' is missing");
        }

        [Test]
        public void Content_PassesValidator()
        {
            var errors = ContentValidator.Validate(Load());
            Assert.IsEmpty(errors, "Content errors:\n" + string.Join("\n", errors));
        }

        [Test]
        public void Content_EverySubjectCanBeDealtToSixPlayers()
        {
            var dealer = new Dealer(GameRulesConfig.Default, new SeededRandom(1));
            foreach (var s in Load())
                for (var players = GameRulesConfig.Default.MinPlayers; players <= GameRulesConfig.Default.MaxPlayers; players++)
                    Assert.IsTrue(dealer.CanDeal(s, players), $"{s.Id} cannot be dealt to {players} players");
        }

        [Test]
        public void FontAtlas_ContainsAllContentAndUiCharacters()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAtlasBuilder.FontAssetPath);
            Assert.IsNotNull(font, $"font asset not found: {FontAtlasBuilder.FontAssetPath}. Run LiarsBar → Content → Rebuild Font Atlas");
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, "atlas must be static for WebGL");

            var required = ContentValidator.CollectCharacters(Load())
                .Concat(FontAtlasBuilder.BaseCharacters())
                .Where(c => !char.IsWhiteSpace(c))
                .Distinct()
                .ToArray();

            var missing = required.Where(c => !font.characterLookupTable.ContainsKey(c)).ToArray();
            Assert.IsEmpty(missing, $"missing in atlas: '{new string(missing)}'. Run LiarsBar → Content → Rebuild Font Atlas");
        }

        [Test]
        public void Engine_PlaysFullMatchesOnRealContent([Values(2, 4, 6)] int players)
        {
            var provider = JsonSubjectProvider.FromResources();
            for (var seed = 1; seed <= 20; seed++)
            {
                var engine = new MatchEngine(GameRulesConfig.Default, provider, new SeededRandom(seed));
                var start = engine.Start(MatchSettings.ForPlayers(players));
                Assert.IsTrue(start.IsSuccess, start.ToString());

                var random = new SeededRandom(seed * 31 + 7);
                for (var step = 0; step < 5000 && engine.State.Phase != MatchPhase.Ended; step++)
                {
                    var view = engine.SnapshotFor(engine.State.CurrentSeat);
                    var result = engine.Apply(RandomAction(view, random));
                    Assert.IsTrue(result.IsSuccess, $"players={players} seed={seed} step={step}: {result}");
                }

                Assert.AreEqual(MatchPhase.Ended, engine.State.Phase, $"players={players} seed={seed}: match did not end");
                Assert.AreEqual(1, engine.State.AliveCount);
            }
        }

        private static IGameAction RandomAction(PlayerView view, IRandom random)
        {
            if (view.CanCallLiar && (!view.CanPlayCards || random.Chance(0.35)))
                return new CallLiarAction(view.ViewerSeat);

            var cards = view.MyHand.Select(c => c.Id).ToList();
            random.Shuffle(cards);
            return new PlayCardsAction(view.ViewerSeat, cards.Take(random.Next(1, System.Math.Min(2, cards.Count) + 1)));
        }
    }
}
