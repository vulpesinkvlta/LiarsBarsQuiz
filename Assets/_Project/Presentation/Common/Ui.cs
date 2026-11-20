using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiarsBar.Presentation.Common
{
    /// <summary>
    /// Сборка простого uGUI из кода: серые прямоугольники этапа 6 без префабов.
    /// На этапе 11 вью получат настоящий стиль, а API вью останется тем же.
    /// </summary>
    public static class Ui
    {
        public static Color Background => Theme.Background;
        public static Color PanelColor => Theme.PanelColor;
        public static Color PanelLight => Theme.PanelLight;
        public static Color Highlight => Theme.Highlight;
        public static Color Selected => Theme.Selected;
        public static Color Dead => Theme.Dead;
        public static Color Text => Theme.Text;
        public static Color TextDim => Theme.TextDim;
        public static Color Overlay => Theme.Overlay;

        private static UiTheme _theme;
        public static UiTheme Theme => _theme != null ? _theme : (_theme = ScriptableObject.CreateInstance<UiTheme>());
        public static ISoundPlayer Sounds { get; private set; }

        public static void Configure(UiTheme theme, ISoundPlayer sounds)
        {
            _theme = theme;
            Sounds = sounds;
        }

        public static TMP_FontAsset Font { get; set; }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var rt = Rect(name, parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? Text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        public static Button Button(string name, Transform parent, string text, float fontSize, out TextMeshProUGUI label)
        {
            var image = Panel(name, parent, PanelLight);
            var button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Sounds?.Play(Theme.click));
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            label = Label("Label", image.transform, text, fontSize);
            Stretch(label.rectTransform, 8);
            return button;
        }

        public static void Stretch(RectTransform rt, float padding = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Привязать к доле родителя: anchors в долях (0..1).</summary>
        public static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        public static LayoutElement Size(Component c, float width, float height)
        {
            if (!c.gameObject.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            return le;
        }
    }
}
