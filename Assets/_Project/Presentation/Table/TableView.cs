using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;
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
        private GameObject _passDevice;
        private TextMeshProUGUI _passTitle;
        private TextMeshProUGUI _passButtonLabel;
        private int _passSeat;
        public event Action<int> PassConfirmed;

        private Image _effect;
        private TextMeshProUGUI _effectTitle;
        private RectTransform _token;
        private TextMeshProUGUI _symbol;
        private readonly List<Image> _chambers = new List<Image>();
        public bool IsPlayingEvent { get; internal set; }

        private GameObject _reveal;
        private TextMeshProUGUI _revealText;
        private float _revealStartedAt;
        private const float RevealDuration = 0.28f;
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

            var passPanel = Ui.Panel("PassDeviceView", root, Ui.PanelColor);
            Ui.Anchor(passPanel.rectTransform, 0.02f, 0.10f, 0.70f, 0.46f);
            _passDevice = passPanel.gameObject;
            _passTitle = Ui.Label("Recipient", passPanel.transform, string.Empty, 32);
            Ui.Anchor(_passTitle.rectTransform, 0.03f, 0.53f, 0.97f, 0.95f);
            var confirm = Ui.Button("Confirm", passPanel.transform, string.Empty, 30, out _passButtonLabel);
            Ui.Anchor((RectTransform)confirm.transform, 0.08f, 0.08f, 0.92f, 0.50f);
            confirm.onClick.AddListener(() => PassConfirmed?.Invoke(_passSeat));
            _passDevice.SetActive(false);

            _effect = Ui.Panel("TableAnimation", root, Ui.PanelColor);
            Ui.Anchor(_effect.rectTransform, 0.02f, 0.46f, 0.70f, 0.75f);
            _effectTitle = Ui.Label("Event", _effect.transform, string.Empty, 35);
            Ui.Anchor(_effectTitle.rectTransform, 0.02f, 0.02f, 0.98f, 0.40f);
            _token = Ui.Rect("Token", _effect.transform);
            _token.anchorMin = _token.anchorMax = new Vector2(0.5f, 0.70f);
            _token.sizeDelta = new Vector2(90, 90);
            _symbol = Ui.Label("Card", _token, "?", 46);
            Ui.Stretch(_symbol.rectTransform);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                var chamber = Ui.Panel("Chamber", _token, Ui.TextDim);
                var rt = chamber.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(15, 15);
                rt.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 29f;
                _chambers.Add(chamber);
            }
            _effect.gameObject.SetActive(false);

            _reveal = BuildModal(root, "Reveal", "Далее", () => RevealClosed?.Invoke(), out _revealText);
            _gameOver = BuildModal(root, "GameOver", "Сыграть ещё", () => RestartClicked?.Invoke(), out _gameOverText);
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
            _effect.gameObject.SetActive(false);
            foreach (var seat in _seats) seat.transform.localRotation = Quaternion.identity;
            IsPlayingEvent = false;
            _reveal.SetActive(false);
            _gameOver.SetActive(false);
            _passDevice.SetActive(false);
            _logLines.Clear();
            _log.text = string.Empty;
            _hint.text = string.Empty;
            _playCaption.text = string.Empty;
            _tableCards.Show(Array.Empty<CardModel>());
            _hand.Show(Array.Empty<CardModel>());
        }

        public void SetPassDevice(int seat, string name)
        {
            _passSeat = seat;
            if (seat >= 0)
            {
                _passTitle.text = $"Передайте устройство: {name}";
                _passButtonLabel.text = $"Я {name}, показать карты";
            }
            _passDevice.SetActive(seat >= 0);
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

        internal IEnumerator Animate(GameEvent evt, PlayerView state)
        {
            if (!(evt is RoundStarted || evt is CardsPlayed || evt is LiarCalled || evt is RevolverShooted))
                yield break;

            _effect.gameObject.SetActive(true);
            _effect.color = Ui.PanelColor;
            _token.localRotation = Quaternion.identity;
            _token.anchoredPosition = Vector2.zero;
            bool wheel = evt is RevolverShooted;
            _symbol.gameObject.SetActive(!wheel);
            foreach (var chamber in _chambers) chamber.gameObject.SetActive(wheel);
            Vector3 start = _token.position;
            Vector3 end = start;

            Vector3 SeatPosition(int seat)
            {
                // Seats use an evenly spaced horizontal row, including during the first deal.
                var seats = state.Seats.ToList();
                int index = seats.FindIndex(s => s.SeatId == seat);
                var rect = _seatsRow.rect;
                return _seatsRow.TransformPoint(new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, (index + 0.5f) / seats.Count), rect.center.y));
            }

            if (evt is RoundStarted round)
            {
                _effectTitle.text = round.SubjectTitle;
                _symbol.text = "▣";
                foreach (var seat in round.HandSizes.Keys)
                    yield return Fly(start, SeatPosition(seat), 0.16f);
            }
            else if (evt is CardsPlayed play)
            {
                _effectTitle.text = state.GetSeat(play.SeatId).Name;
                _symbol.text = play.Cards.Count == 1 ? "▣" : "▣ ▣";
                yield return Fly(SeatPosition(play.SeatId), end, 0.35f);
            }
            else if (evt is LiarCalled call)
            {
                _symbol.text = "!";
                _effectTitle.text = state.GetSeat(call.AccuserSeat).Name + " — Лжец!";
                var seat = _seats.ElementAtOrDefault(state.Seats.ToList().FindIndex(s => s.SeatId == call.AccuserSeat));
                for (float t = 0; t < 0.48f; t += Time.unscaledDeltaTime)
                {
                    if (seat != null) seat.transform.localRotation = Quaternion.Euler(0, 0, 4f * Mathf.Sin(t * 58));
                    yield return null;
                }
                if (seat != null) seat.transform.localRotation = Quaternion.identity;
            }
            else if (evt is RevolverShooted shot)
            {
                _effectTitle.text = state.GetSeat(shot.SeatId).Name + " · рулетка";
                for (float t = 0; t < 0.55f; t += Time.unscaledDeltaTime)
                {
                    _token.localRotation = Quaternion.Euler(0, 0, 720 * Mathf.SmoothStep(0, 1, t / 0.55f));
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(1.2f);
                _effectTitle.text = shot.Hit ? "БАХ! " + state.GetSeat(shot.SeatId).Name + " выбывает" :
                    $"Щёлк… пусто. {shot.ShotsTaken}/{shot.Chambers}";
                if (shot.Hit)
                {
                    _effect.color = new Color(0.72f, 0.22f, 0.12f);
                    yield return ShakeTable();
                    _effect.color = Ui.PanelColor;
                }
                yield return new WaitForSecondsRealtime(0.8f);
            }
            _effect.gameObject.SetActive(false);
        }

        internal IEnumerator FlipTableCards(FactsRevealed revealed)
        {
            foreach (var fact in revealed.Facts)
                yield return _tableCards.FlipRevealed(new CardModel(fact.Id, fact.Text, fact.IsTrue, false, false));
            yield return new WaitForSecondsRealtime(0.35f);
        }

        private IEnumerator ShakeTable()
        {
            var panels = new List<RectTransform>();
            var origins = new List<Vector2>();
            foreach (RectTransform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                panels.Add(child);
                origins.Add(child.anchoredPosition);
            }
            try
            {
                const float duration = 0.22f;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    float strength = 9f * (1f - t / duration);
                    var offset = strength * new Vector2(Mathf.Sin(t * 110f), Mathf.Sin(t * 72f));
                    for (int i = 0; i < panels.Count; i++) panels[i].anchoredPosition = origins[i] + offset;
                    yield return null;
                }
            }
            finally
            {
                for (int i = 0; i < panels.Count; i++)
                    if (panels[i] != null) panels[i].anchoredPosition = origins[i];
            }
        }

        private IEnumerator Fly(Vector3 from, Vector3 to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                _token.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            _token.position = to;
        }

        public void ShowReveal(string text)
        {
            _revealText.text = text;
            _revealStartedAt = Time.unscaledTime;
            _revealText.rectTransform.localScale = new Vector3(0f, 1f, 1f);
            _reveal.SetActive(true);
            _reveal.transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (_reveal == null || !_reveal.activeSelf || _revealText.rectTransform.localScale.x >= 1f) return;
            var progress = Mathf.Clamp01((Time.unscaledTime - _revealStartedAt) / RevealDuration);
            _revealText.rectTransform.localScale = new Vector3(Mathf.Sin(progress * Mathf.PI * 0.5f), 1f, 1f);
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
