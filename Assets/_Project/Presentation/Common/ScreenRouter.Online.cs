using System;
using LiarsBar.Application;
using TMPro;
using UnityEngine;

namespace LiarsBar.Presentation.Common
{
    public sealed partial class ScreenRouter
    {
        private IRoomService _rooms;
        private int _onlineBots;
        private string _onlineName;
        private string _codeInput = "";
        public event Func<int, bool> OnlineStartRequested;

        private void StartOnline()
        {
            if (_screen != Screen.Lobby) return;
            _table.gameObject.SetActive(true);
            bool started = false;
            try
            {
                started = OnlineStartRequested?.Invoke(_onlineBots) == true;
            }
            finally
            {
                if (!started) _table.gameObject.SetActive(false);
            }
            if (!started) return;
            _lastMatch = null;
            ClearPage();
            _screen = Screen.Table;
        }

        private void OpenOnline()
        {
            _onlineName ??= _defaultName;
            _onlineBots = _defaultBots;
            Page(Screen.Online, "Онлайн");
            RefreshOnline();
        }

        private void RefreshOnline()
        {
            if (_screen != Screen.Online && _screen != Screen.Lobby) return;
            if (_rooms.Code != null)
            {
                Page(Screen.Lobby, _rooms.Code);
                Button("Скопировать код", .71f, () => CopyRoomCode(_rooms.Code));
                var players = _rooms.Players;
                var roster = Ui.Label("Players", _page, string.Join("\n", players), 26);
                roster.richText = false;
                Ui.Anchor(roster.rectTransform, .05f, .40f, .95f, .70f);
                if (_rooms.IsHost)
                {
                    int max = 6 - players.Count;
                    _onlineBots = Mathf.Clamp(_onlineBots, 0, max);
                    Button($"Боты: {_onlineBots}    +", .29f, () =>
                    {
                        _onlineBots = (_onlineBots + 1) % (max + 1);
                        RefreshOnline();
                    }).interactable = max > 0;
                    Button("Начать", .17f, StartOnline)
                        .interactable = OnlineStartRequested != null && players.Count + _onlineBots >= 2;
                }
            }
            else
            {
                Page(Screen.Online, "Онлайн");
                OnlineInput("Имя", _onlineName, .67f, 24, s => _onlineName = s);
                Button("Создать комнату", .55f, () => _rooms.Create(_onlineName)).interactable = !_rooms.Busy;
                OnlineInput("Код", _codeInput, .41f, 4, s => _codeInput = s);
                Button("Войти по коду", .29f, () => _rooms.Join(_onlineName, _codeInput)).interactable = !_rooms.Busy;
                var status = Ui.Label("Status", _page, _rooms.Status ?? "", 24);
                Ui.Anchor(status.rectTransform, .03f, .16f, .97f, .28f);
            }
            Button("В меню", .04f, () => { Menu(); _rooms.Close(); });
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal", EntryPoint = "LiarsBarCopyRoomCode")]
        private static extern void CopyRoomCode(string code);
#else
        private static void CopyRoomCode(string code) => GUIUtility.systemCopyBuffer = code;
#endif

        private void OnlineInput(string caption, string value, float y, int limit, Action<string> change)
        {
            var label = Ui.Label(caption, _page, caption, 28);
            Ui.Anchor(label.rectTransform, .18f, y, .36f, y + .09f);
            var box = Ui.Panel(caption + "Input", _page, Ui.PanelColor);
            box.gameObject.SetActive(false);
            Ui.Anchor(box.rectTransform, .38f, y, .82f, y + .09f);
            var viewport = Ui.Rect("Viewport", box.transform);
            Ui.Stretch(viewport, 5);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Ui.Label("Text", viewport, "", 28);
            Ui.Stretch(text.rectTransform);
            text.richText = false;
            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.targetGraphic = box;
            field.richText = false;
            field.characterLimit = limit;
            field.text = value;
            field.onValueChanged.AddListener(change.Invoke);
            box.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (_rooms != null) _rooms.Changed -= RefreshOnline;
        }
    }
}
