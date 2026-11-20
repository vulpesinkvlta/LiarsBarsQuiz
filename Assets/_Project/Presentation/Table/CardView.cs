using System;
using LiarsBar.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Карта-факт: текст и (только в своей руке) пометка ✓/✗. Нажатие выбирает карту.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Width = 230f;
        public const float Height = 190f;

        private Image _background;
        private Button _button;
        private TextMeshProUGUI _text;
        private TextMeshProUGUI _mark;

        public string Id { get; private set; }
        public event Action<string> Clicked;

        public static CardView Create(Transform parent)
        {
            var bg = Ui.Panel("Card", parent, Ui.PanelLight);
            bg.sprite = Ui.Theme.card;
            Ui.Size(bg, Width, Height);
            var view = bg.gameObject.AddComponent<CardView>();
            view._background = bg;
            view._button = bg.gameObject.AddComponent<Button>();
            view._button.transition = Selectable.Transition.None;
            view._button.onClick.AddListener(() => view.Clicked?.Invoke(view.Id));

            view._mark = Ui.Label("Mark", bg.transform, string.Empty, 30, TextAlignmentOptions.TopRight);
            Ui.Anchor(view._mark.rectTransform, 0.6f, 0.78f, 0.97f, 0.98f);

            view._text = Ui.Label("Text", bg.transform, string.Empty, 22);
            view._text.enableAutoSizing = true;
            view._text.fontSizeMin = 14;
            view._text.fontSizeMax = 24;
            Ui.Anchor(view._text.rectTransform, 0.06f, 0.06f, 0.94f, 0.8f);
            return view;
        }

        public void Bind(CardModel model)
        {
            Id = model.Id;
            _text.text = model.Text;
            _mark.text = model.Mark == null ? string.Empty : model.Mark.Value ? "<color=#3FA34D>✓</color>" : "<color=#D9534F>✗</color>";
            _background.color = model.Selected ? Ui.Selected : Ui.PanelLight;
            _button.interactable = model.Interactable;
            _background.raycastTarget = model.Interactable;

            // Выбранная карта приподнимается, чтобы выбор был заметен без цвета.
            var le = GetComponent<LayoutElement>();
            le.preferredHeight = Height;
            transform.localScale = model.Selected ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        }
    }
}
