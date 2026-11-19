using System.IO;
using System.Linq;
using System.Text;
using LiarsBar.Core.Content;
using LiarsBar.Infrastructure.Content;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LiarsBar.Editor
{
    /// <summary>
    /// Статический атлас TMP: латиница, кириллица, типографика игры и все символы из Resources/Content/*.json.
    /// Пересобирать после каждого изменения контента (LiarsBar → Content → Rebuild Font Atlas);
    /// тест ShippedContentTests упадёт, если в атласе не хватает символа.
    /// GUID ассета при пересборке сохраняется, ссылки на шрифт не ломаются.
    /// </summary>
    public static class FontAtlasBuilder
    {
        public const string SourceFontPath = "Assets/_Project/Content/Fonts/Inter-Regular.ttf";
        public const string FontAssetPath = "Assets/_Project/Content/Fonts/Inter SDF.asset";

        /// <summary>Символы интерфейса, которых может не быть в контенте.</summary>
        public const string UiCharacters = "«»„“”‘’—–…✓✗№°×·•−";

        private const int SamplingPointSize = 56;
        private const int Padding = 6;
        private const int AtlasSize = 1024;

        public static string BaseCharacters()
        {
            var sb = new StringBuilder();
            for (var c = (char)0x20; c <= 0x7E; c++) sb.Append(c);       // ASCII
            for (var c = (char)0x0410; c <= 0x044F; c++) sb.Append(c);   // А–я
            sb.Append("Ёё").Append(UiCharacters);
            return sb.ToString();
        }

        public static string AllCharacters()
        {
            var content = ContentValidator.CollectCharacters(JsonSubjectProvider.FromResources().GetSubjects());
            return new string(BaseCharacters().Concat(content).Distinct().OrderBy(c => c).ToArray());
        }

        [MenuItem("LiarsBar/Content/Rebuild Font Atlas")]
        public static void Rebuild()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[FontAtlas] Source font not found: {SourceFontPath}");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) ?? CreatePersistent(font);

            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.ClearFontAssetData(true);
            var chars = AllCharacters();
            asset.TryAddCharacters(chars, out var missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var msg = $"[FontAtlas] {FontAssetPath}: {asset.characterTable.Count}/{chars.Length} characters, atlas {asset.atlasWidth}×{asset.atlasHeight}";
            if (string.IsNullOrEmpty(missing)) Debug.Log(msg);
            else Debug.LogError($"{msg}. Missing: '{missing}'");
            File.WriteAllText("Temp/liarsbar_font.txt", msg + (string.IsNullOrEmpty(missing) ? "" : $" MISSING '{missing}'"));
        }

        private static TMP_FontAsset CreatePersistent(Font font)
        {
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            var name = Path.GetFileNameWithoutExtension(FontAssetPath);
            asset.name = name;

            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }
    }
}
