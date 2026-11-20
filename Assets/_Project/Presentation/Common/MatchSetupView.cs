using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Sessions;
using TMPro;
using UnityEngine;

namespace LiarsBar.Presentation.Common
{
    internal sealed class MatchSetupView
    {
        private readonly RectTransform _parent;
        private readonly GameMode _mode;
        private readonly List<PlayerSlot> _slots = new List<PlayerSlot>();
        private int _botCount;
        private RectTransform _form;

        public MatchSetupView(RectTransform parent, GameMode mode, string name, int bots)
        {
            _parent = parent;
            _mode = mode;
            _botCount = Mathf.Clamp(bots, 1, 5);
            _slots.Add(PlayerSlot.Human(name));
            if (mode == GameMode.HotSeat) _slots.Add(PlayerSlot.Human("Игрок 2"));
            Render();
        }

        public GameModeSettings Settings() => _mode == GameMode.VsBots
            ? GameModeSettings.VsBots(_slots[0].Name, _botCount)
            : new GameModeSettings(_mode, _slots);

        private void Render()
        {
            if (_form != null)
            {
                _form.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_form.gameObject);
            }
            _form = Ui.Rect("Participants", _parent);
            Ui.Anchor(_form, 0.10f, 0.26f, 0.90f, 0.82f);
            for (int i = 0; i < _slots.Count; i++) SeatRow(i);

            if (_mode == GameMode.VsBots)
            {
                var count = Ui.Label("BotCount", _form, $"Ботов за столом: {_botCount}", 32);
                Ui.Anchor(count.rectTransform, 0.18f, 0.42f, 0.82f, 0.62f);
                Control("−", 0.02f, 0.42f, 0.18f, 0.62f, _botCount > 1, () => { _botCount--; Render(); });
                Control("+", 0.82f, 0.42f, 0.98f, 0.62f, _botCount < 5, () => { _botCount++; Render(); });
            }
            else
            {
                Control("Добавить игрока", 0, 0, 0.49f, 0.14f, _slots.Count < 6, () =>
                {
                    _slots.Add(PlayerSlot.Human($"Игрок {_slots.Count + 1}"));
                    Render();
                });
                Control("Добавить бота", 0.51f, 0, 1, 0.14f, _slots.Count < 6, () =>
                {
                    _slots.Add(PlayerSlot.Bot());
                    Render();
                });
            }
        }

        private void SeatRow(int index)
        {
            float top = 1 - index * 0.14f;
            float bottom = top - 0.12f;
            var slot = _slots[index];
            var number = Ui.Label("Seat", _form, (index + 1).ToString(), 30);
            Ui.Anchor(number.rectTransform, 0, bottom, 0.08f, top);
            var box = Ui.Panel("Name", _form, Ui.PanelColor);
            Ui.Anchor(box.rectTransform, 0.09f, bottom, 0.79f, top);
            if (slot.IsBot)
            {
                Ui.Stretch(Ui.Label("Bot", box.transform, "Бот", 30).rectTransform);
            }
            else
            {
                box.gameObject.SetActive(false);
                var viewport = Ui.Rect("Viewport", box.transform);
                Ui.Stretch(viewport, 6);
                viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                var text = Ui.Label("NameText", viewport, "", 30, TextAlignmentOptions.MidlineLeft);
                text.richText = false;
                Ui.Stretch(text.rectTransform);
                var input = box.gameObject.AddComponent<TMP_InputField>();
                input.textViewport = viewport;
                input.textComponent = text;
                input.targetGraphic = box;
                input.characterLimit = 24;
                input.richText = false;
                input.onValidateInput = (_, __, ch) => ch == '<' || ch == '>' ? '\0' : ch;
                input.text = slot.Name;
                input.onValueChanged.AddListener(value => _slots[index] = PlayerSlot.Human(value));
                box.gameObject.SetActive(true);
            }

            if (_mode == GameMode.HotSeat)
            {
                bool removable = _slots.Count > 2 && (slot.IsBot || _slots.Count(s => !s.IsBot) > 1);
                Control("Убрать", 0.80f, bottom, 1, top, removable, () => { _slots.RemoveAt(index); Render(); });
            }
        }

        private void Control(string caption, float left, float bottom, float right, float top, bool enabled, Action action)
        {
            var button = Ui.Button(caption, _form, caption, 26, out _);
            Ui.Anchor((RectTransform)button.transform, left, bottom, right, top);
            button.interactable = enabled;
            button.onClick.AddListener(() => action());
        }
    }
}
