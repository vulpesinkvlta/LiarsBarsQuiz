using System;
using LiarsBar.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Карта-факт: текст и (только в своей руке) пометка ✓/✗. Нажатие выбирает карту.</summary>
    public sealed class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Width = 230f;
        public const float Height = 190f;

        private Image _background;
        private Button _button;
        private TextMeshProUGUI _text;
        private TextMeshProUGUI _mark;

        private bool _selected;
        private int? _pointer;
        private Vector2 _start;
        private Vector2 _origin;
        public bool Dragging => _pointer.HasValue;
        public event Action Dropped;

        public void OnBeginDrag(PointerEventData e)
        {
            e.eligibleForClick = false;
            if (Dragging || !_selected || !_button.IsInteractable() || e.button != PointerEventData.InputButton.Left) return;
            if (!LocalPoint(e, e.pressPosition, out _start)) return;
            _origin = ((RectTransform)transform).anchoredPosition;
            _pointer = e.pointerId;
        }

        private bool LocalPoint(PointerEventData e, Vector2 position, out Vector2 point) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                position, e.pressEventCamera, out point);

        public void OnDrag(PointerEventData e)
        {
            if (_pointer != e.pointerId || !LocalPoint(e, e.position, out var point)) return;
            ((RectTransform)transform).anchoredPosition = _origin + point - _start;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_pointer != e.pointerId) return;
            _pointer = null;
            if (_selected && _button.IsInteractable() &&
                e.position.y - e.pressPosition.y >= Screen.height * Ui.Theme.playDragScreenFraction)
                Dropped?.Invoke();
        }

        private void OnDisable()
        {
            _pointer = null;
            if (transform.parent != null && transform.parent.GetComponent<HandView>() != null)
                ((RectTransform)transform).anchoredPosition = Vector2.zero;
        }

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

        public void ShowBack()
        {
            _background.sprite = Ui.Theme.cardBack != null ? Ui.Theme.cardBack : Ui.Theme.card;
            _background.color = Color.white;
            _text.text = "";
            _mark.text = "";
        }

        public void Bind(CardModel model)
        {
            _background.sprite = Ui.Theme.card;
            _selected = model.Selected;
            if (Id != model.Id || !_selected || !model.Interactable) _pointer = null;
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
