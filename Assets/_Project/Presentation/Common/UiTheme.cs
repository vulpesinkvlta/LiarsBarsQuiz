using TMPro;
using UnityEngine;

namespace LiarsBar.Presentation.Common
{
    [CreateAssetMenu(menuName = "Liars Bar/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Sprite backdrop, felt, card, portrait, revolver, shotFlash;
        public Sprite cardBack;
        public AudioClip click, cards, liar, spin, empty, shot, victory;
        [Header("Стол и жесты")]
        [Range(.12f,.45f)] public float journalSlideSeconds = .24f;
        [Range(.04f,.24f)] public float playDragScreenFraction = .10f;
        [Range(.22f,.65f)] public float drumScreenFraction = .43f;
        [Range(0f,.8f)] public float rouletteDim = .43f;
        public Color Background = new Color(0.16f, 0.16f, 0.17f);
        public Color PanelColor = new Color(0.26f, 0.26f, 0.28f);
        public Color PanelLight = new Color(0.36f, 0.36f, 0.38f);
        public Color Highlight = new Color(0.55f, 0.47f, 0.22f);
        public Color Selected = new Color(0.30f, 0.45f, 0.65f);
        public Color Dead = new Color(0.12f, 0.12f, 0.12f);
        public Color Text = new Color(0.93f, 0.93f, 0.93f);
        public Color TextDim = new Color(0.6f, 0.6f, 0.6f);
        public Color Overlay = new Color(0f, 0f, 0f, 0.75f);
    }
}
