using System;
using System.Collections.Generic;
using LiarsBar.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiarsBar.Presentation.Table
{
    /// <summary>
    /// Серый стол этапа 6: места, сыгранные карты, рука, кнопки «Сыграть» / «Лжец!», журнал,
    /// текстовое окно проверки и окно конца партии. Без логики — только показ и нажатия.
    /// </summary>
    public sealed class TableView : MonoBehaviour, ITableView
    {
        private const int LogLines = 14;

        private TextMeshProUGUI _header;
        private RectTransform _seatsRow;
        private readonly List<SeatView> _seats = new List<SeatView>();
        private TextMeshProUGUI _playCaption;
        private HandView _tableCards;
        private HandView _hand;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _log;
        private readonly Queue<string> _logLines = new Queue<string>();
        private Button _playButton;
        private Button _liarButton;
        private GameObject _reveal;
        private TextMeshProUGUI _revealText;
        private GameObject _gameOver;
        private TextMeshProUGUI _gameOverText;

        public event Action PlayClicked;
        public event Action LiarClicked;
        public event Action<string> CardClicked;
        public event Action RevealClosed;

        /// <summary>«Новая партия» в окне конца партии.</summary>
        public event Action RestartClicked;
        public event Action ExitClicked;
        public event Action GameOverShown;

        public static TableView Create(TMP_FontAsset font)
        {
            Ui.Font = font;
            UiRoot.EnsureEventSystem();
            var canvas = UiRoot.CreateCanvas("Table");
            var view = canvas.gameObject.AddComponent<TableView>();
            view.Build(canvas.transform);
            return view;
        }

        private void Build(Transform root)
        {
            var bg = Ui.Panel("Background", root, Ui.Background);
            Ui.Stretch(bg.rectTransform);

            _header = Ui.Label("Header", root, string.Empty, 40);
            _header.fontStyle = FontStyles.Bold;
            Ui.Anchor(_header.rectTransform, 0.02f, 0.92f, 0.98f, 0.99f);

            _seatsRow = Ui.Rect("Seats", root);
            Ui.Anchor(_seatsRow, 0.02f, 0.76f, 0.98f, 0.92f);
            Ui.Row(_seatsRow, 16);

            // Центр стола: последний ход.
            var center = Ui.Panel("TablePlay", root, Ui.PanelColor);
            Ui.Anchor(center.rectTransform, 0.02f, 0.47f, 0.70f, 0.75f);
            _playCaption = Ui.Label("Caption", center.transform, string.Empty, 28);
            Ui.Anchor(_playCaption.rectTransform, 0.02f, 0.80f, 0.98f, 0.98f);
            var tableRow = Ui.Rect("Cards", center.transform);
            Ui.Anchor(tableRow, 0.02f, 0.04f, 0.98f, 0.80f);
            Ui.Row(tableRow, 16);
            _tableCards = tableRow.gameObject.AddComponent<HandView>();

            _hint = Ui.Label("Hint", root, string.Empty, 30, color: new Color(1f, 0.85f, 0.45f));
            Ui.Anchor(_hint.rectTransform, 0.02f, 0.38f, 0.70f, 0.46f);

            var handRow = Ui.Rect("Hand", root);
            Ui.Anchor(handRow, 0.02f, 0.10f, 0.70f, 0.37f);
            Ui.Row(handRow, 14);
            _hand = handRow.gameObject.AddComponent<HandView>();
            _hand.CardClicked += id => CardClicked?.Invoke(id);

            var logPanel = Ui.Panel("Log", root, Ui.PanelColor);
            Ui.Anchor(logPanel.rectTransform, 0.72f, 0.31f, 0.98f, 0.75f);
            _log = Ui.Label("Text", logPanel.transform, string.Empty, 20, TextAlignmentOptions.BottomLeft);
            _log.overflowMode = TextOverflowModes.Truncate;
            Ui.Stretch(_log.rectTransform, 14);

            _playButton = Ui.Button("Play", root, "Сыграть", 36, out _);
            Ui.Anchor((RectTransform)_playButton.transform, 0.72f, 0.20f, 0.98f, 0.29f);
            _playButton.onClick.AddListener(() => PlayClicked?.Invoke());

            _liarButton = Ui.Button("Liar", root, "Лжец!", 36, out _);
            Ui.Anchor((RectTransform)_liarButton.transform, 0.72f, 0.10f, 0.98f, 0.19f);
            _liarButton.onClick.AddListener(() => LiarClicked?.Invoke());

            var exit = Ui.Button("Exit", root, "Выйти в меню", 26, out _);
            Ui.Anchor((RectTransform)exit.transform, 0.72f, 0.015f, 0.98f, 0.085f);
            exit.onClick.AddListener(() => ExitClicked?.Invoke());

            _reveal = BuildModal(root, "Reveal", "Далее", () => RevealClosed?.Invoke(), out _revealText);
            _gameOver = BuildModal(root, "GameOver", "Новая партия", () => RestartClicked?.Invoke(), out _gameOverText);
            var menu = Ui.Button("Menu", _gameOver.transform, "В меню", 30, out _);
            Ui.Anchor((RectTransform)menu.transform, 0.4f, 0.012f, 0.6f, 0.09f);
            menu.onClick.AddListener(() => ExitClicked?.Invoke());
            _gameOverText.alignment = TextAlignmentOptions.Center;
            _gameOverText.fontSize = 44;
        }

        private static GameObject BuildModal(Transform root, string name, string buttonText, Action onClick, out TextMeshProUGUI text)
        {
            var overlay = Ui.Panel(name, root, Ui.Overlay);
            Ui.Stretch(overlay.rectTransform);

            var box = Ui.Panel("Box", overlay.transform, Ui.PanelColor);
            Ui.Anchor(box.rectTransform, 0.2f, 0.1f, 0.8f, 0.9f);

            text = Ui.Label("Text", box.transform, string.Empty, 28, TextAlignmentOptions.TopLeft);
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = 30;
            Ui.Anchor(text.rectTransform, 0.05f, 0.18f, 0.95f, 0.95f);

            var button = Ui.Button("Button", box.transform, buttonText, 34, out _);
            Ui.Anchor((RectTransform)button.transform, 0.3f, 0.04f, 0.7f, 0.14f);
            button.onClick.AddListener(() => onClick());

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        /// <summary>Очистить стол перед новой партией.</summary>
        public void ResetView()
        {
            _reveal.SetActive(false);
            _gameOver.SetActive(false);
            _logLines.Clear();
            _log.text = string.Empty;
            _hint.text = string.Empty;
            _playCaption.text = string.Empty;
            _tableCards.Show(Array.Empty<CardModel>());
            _hand.Show(Array.Empty<CardModel>());
        }

        public void SetHeader(string text) => _header.text = text;

        public void SetSeats(IReadOnlyList<SeatModel> seats)
        {
            while (_seats.Count < seats.Count) _seats.Add(SeatView.Create(_seatsRow));
            for (var i = 0; i < _seats.Count; i++)
            {
                var visible = i < seats.Count;
                _seats[i].gameObject.SetActive(visible);
                if (visible) _seats[i].Bind(seats[i]);
            }
        }

        public void SetTablePlay(string caption, IReadOnlyList<CardModel> cards)
        {
            _playCaption.text = caption;
            _tableCards.Show(cards);
        }

        public void SetHand(IReadOnlyList<CardModel> cards) => _hand.Show(cards);

        public void SetActions(bool canPlay, bool canCallLiar, bool visible)
        {
            _playButton.gameObject.SetActive(visible);
            _liarButton.gameObject.SetActive(visible);
            _playButton.interactable = canPlay;
            _liarButton.interactable = canCallLiar;
        }

        public void SetHint(string text) => _hint.text = text;

        public void AppendLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > LogLines) _logLines.Dequeue();
            _log.text = string.Join("\n", _logLines);
        }

        public void ShowReveal(string text)
        {
            _revealText.text = text;
            _reveal.SetActive(true);
            _reveal.transform.SetAsLastSibling();
        }

        public void HideReveal() => _reveal.SetActive(false);

        public void ShowGameOver(string text)
        {
            _gameOverText.text = text;
            _gameOver.SetActive(true);
            _gameOver.transform.SetAsLastSibling();
            GameOverShown?.Invoke();
        }
    }
}
