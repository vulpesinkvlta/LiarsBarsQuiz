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
        private readonly List<Action<bool>> _layouts = new List<Action<bool>>();
        private RectTransform _pile;
        private Image _subject;
        private Sprite Back => Ui.Theme.cardBack != null ? Ui.Theme.cardBack : Ui.Theme.card;
        private readonly List<CardView> _played = new List<CardView>();
        private bool _raised;
        private IReadOnlyList<CardModel> _held = Array.Empty<CardModel>();
        private Vector2 _layoutSize;

        private void Place(RectTransform rect, Rect landscape, Rect portrait)
        {
            _layouts.Add(vertical =>
            {
                var area = vertical ? portrait : landscape;
                Ui.Anchor(rect, area.xMin, area.yMin, area.xMax, area.yMax);
            });
        }

        private void ResizeTable()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size == _layoutSize || size.x <= 0 || size.y <= 0) return;
            _layoutSize = size;
            bool vertical = size.y > size.x;
            foreach (var layout in _layouts) layout(vertical);
        }

        private static readonly Vector2[] OpponentPositions =
        {
            new Vector2(.12f,.54f), new Vector2(.16f,.84f), new Vector2(.45f,.90f),
            new Vector2(.65f,.83f), new Vector2(.87f,.83f)
        };

        private const int LogLines = 14;

        private TextMeshProUGUI _header;
        private RectTransform _seatsRow;
        private readonly List<SeatView> _seats = new List<SeatView>();
        private TextMeshProUGUI _playCaption;

        private HandView _hand;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _log;
        private readonly Queue<string> _logLines = new Queue<string>();
        private RectTransform _journal;
        private bool _journalOpen;
        private float _journalProgress;
        private bool _canPlay;
        private Image _flash;
        private Button _liarButton;
        private GameObject _passDevice;
        private TextMeshProUGUI _passTitle;
        private TextMeshProUGUI _passButtonLabel;
        private int _passSeat;
        public event Action<int> PassConfirmed;

        private Image _effect;
        private TextMeshProUGUI _effectTitle;
        private RectTransform _token;
        private Image _wheel;
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
            bg.sprite = Ui.Theme.backdrop;
            Ui.Stretch(bg.rectTransform);

            _header = Ui.Label("Header", root, string.Empty, 40);
            _header.fontStyle = FontStyles.Bold;
            Ui.Anchor(_header.rectTransform, 0.02f, 0.92f, 0.98f, 0.99f);

            _seatsRow = Ui.Rect("Seats", root);
            Ui.Stretch(_seatsRow);

            // Центр стола: последний ход.
            var center = Ui.Panel("TablePlay", root, Ui.PanelColor);
            center.sprite = Ui.Theme.felt;
            Ui.Anchor(center.rectTransform,.015f,.13f,.985f,.97f);
            center.transform.SetSiblingIndex(1);
            _playCaption = Ui.Label("Caption", center.transform, "", 24);
            Ui.Anchor(_playCaption.rectTransform,.22f,.68f,.66f,.76f);
            for (int i=2;i>0;i--)
            {
                var layer = Ui.Panel("DeckEdge",center.transform,new Color(.68f,.52f,.34f));
                Ui.Anchor(layer.rectTransform,.26f,.40f,.38f,.65f);
                layer.rectTransform.anchoredPosition = new Vector2(-4*i,-4*i);
                layer.sprite = Back;
            }
            _subject = Ui.Panel("SubjectDeck", center.transform, Color.white);
            Ui.Anchor(_subject.rectTransform,.26f,.40f,.38f,.65f);
            _subject.sprite = Back;
            var unknown = Ui.Label("Placeholder", _subject.transform,"?",40);
            Ui.Stretch(unknown.rectTransform);
            _pile = Ui.Rect("PlayedPile",center.transform);
            Ui.Anchor(_pile,.42f,.38f,.64f,.66f);

            _hint = Ui.Label("Hint", root, string.Empty, 30, color: new Color(1f, 0.85f, 0.45f));
            Place(_hint.rectTransform, Rect.MinMaxRect(0.02f, 0.38f, 0.70f, 0.46f), Rect.MinMaxRect(.02f,.53f,.98f,.58f));

            var handRow = Ui.Rect("Hand", root);
            Place(handRow, Rect.MinMaxRect(.04f,0,.69f,.27f), Rect.MinMaxRect(.01f,0,.76f,.22f));

            _hand = handRow.gameObject.AddComponent<HandView>();
            _hand.Dropped += () => { if (_canPlay) PlayClicked?.Invoke(); };
            _hand.CardClicked += id => CardClicked?.Invoke(id);

            var logPanel = Ui.Panel("Log", root, new Color(.16f,.27f,.35f,.98f));
            Place(logPanel.rectTransform, Rect.MinMaxRect(.79f,.38f,.99f,.76f), Rect.MinMaxRect(.70f,.38f,.99f,.73f));
            _log = Ui.Label("Text", logPanel.transform, string.Empty, 20, TextAlignmentOptions.BottomLeft);
            _log.overflowMode = TextOverflowModes.Truncate;
            Ui.Anchor(_log.rectTransform,.04f,.03f,.96f,.77f);
            _header.transform.SetParent(logPanel.transform,false);
            Ui.Anchor(_header.rectTransform,.02f,.78f,.98f,.99f);
            _header.enableAutoSizing = true;
            _header.fontSizeMax = 26;
            _journal = logPanel.rectTransform;
            var toggle = Ui.Button("ToggleLog",_journal,"<",26,out var arrow);
            var tab = (RectTransform)toggle.transform;
            tab.anchorMin = tab.anchorMax = new Vector2(0,.5f);
            tab.pivot = new Vector2(1,.5f);
            tab.sizeDelta = new Vector2(52,72);
            toggle.onClick.AddListener(() =>
            {
                _journalOpen = !_journalOpen;
                arrow.text = _journalOpen ? ">" : "<";
            });
            var expand = Ui.Button("ExpandHand",root,"Карты",22,out _);
            Ui.Anchor((RectTransform)expand.transform,.015f,.275f,.18f,.34f);
            expand.onClick.AddListener(()=>_raised = !_raised);

            _liarButton = Ui.Button("Liar", root, "Лжец!", 36, out _);
            Place((RectTransform)_liarButton.transform, Rect.MinMaxRect(.58f,.28f,.74f,.35f), Rect.MinMaxRect(.59f,.24f,.80f,.30f));
            _liarButton.onClick.AddListener(() => LiarClicked?.Invoke());

            var exit = Ui.Button("Exit", root, "Выйти в меню", 26, out _);
            Place((RectTransform)exit.transform, Rect.MinMaxRect(.81f,.91f,.99f,.98f), Rect.MinMaxRect(.77f,.92f,.99f,.99f));
            exit.onClick.AddListener(() => ExitClicked?.Invoke());

            var passPanel = Ui.Panel("PassDeviceView", root, Ui.PanelColor);
            Place(passPanel.rectTransform, Rect.MinMaxRect(0.02f, 0.10f, 0.70f, 0.46f), Rect.MinMaxRect(.02f,.16f,.98f,.58f));
            _passDevice = passPanel.gameObject;
            _passTitle = Ui.Label("Recipient", passPanel.transform, string.Empty, 32);
            Ui.Anchor(_passTitle.rectTransform, 0.03f, 0.53f, 0.97f, 0.95f);
            var confirm = Ui.Button("Confirm", passPanel.transform, string.Empty, 30, out _passButtonLabel);
            Ui.Anchor((RectTransform)confirm.transform, 0.08f, 0.08f, 0.92f, 0.50f);
            confirm.onClick.AddListener(() => PassConfirmed?.Invoke(_passSeat));
            _passDevice.SetActive(false);

            _effect = Ui.Panel("TableAnimation", root, Ui.PanelColor);
            Ui.Stretch(_effect.rectTransform);
            _effectTitle = Ui.Label("Event", _effect.transform, string.Empty, 35);
            Ui.Anchor(_effectTitle.rectTransform, 0.12f, 0.14f, 0.88f, 0.25f);
            _token = Ui.Rect("Token", _effect.transform);
            _token.anchorMin = _token.anchorMax = new Vector2(.5f,.5f);
            _token.sizeDelta = new Vector2(90, 90);
            _wheel = _token.gameObject.AddComponent<Image>();
            _wheel.sprite = Ui.Theme.revolver;
            _wheel.preserveAspect = true;
            _wheel.raycastTarget = false;
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
            _flash = Ui.Panel("MuzzleLight", _effect.transform, Color.white);
            Ui.Stretch(_flash.rectTransform);
            _flash.sprite = Ui.Theme.shotFlash;
            _flash.raycastTarget = false;
            _flash.gameObject.SetActive(false);
            _effect.gameObject.SetActive(false);

            _reveal = BuildModal(root, "Reveal", "Далее", () => RevealClosed?.Invoke(), out _revealText);
            _gameOver = BuildModal(root, "GameOver", "Сыграть ещё", () => RestartClicked?.Invoke(), out _gameOverText);
            var menu = Ui.Button("Menu", _gameOver.transform, "В меню", 30, out _);
            Ui.Anchor((RectTransform)menu.transform, 0.4f, 0.012f, 0.6f, 0.09f);
            menu.onClick.AddListener(() => ExitClicked?.Invoke());
            _gameOverText.alignment = TextAlignmentOptions.Center;
            _gameOverText.fontSize = 44;
            ResizeTable();
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
            ClearPile();
            _held = Array.Empty<CardModel>();
            _raised = false;
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
                if (!visible) continue;
                _seats[i].Bind(seats[i]);
                int viewer = Math.Max(0,seats.ToList().FindIndex(x=>x.IsViewer));
                int relative = (i - viewer + seats.Count) % seats.Count;
                int index = relative <= 0 || seats.Count == 2 ? 2 :
                    Mathf.RoundToInt(4f*(relative-1)/(seats.Count-2));
                var point = relative == 0 ? new Vector2(.865f,.09f) : OpponentPositions[index];
                Ui.Anchor((RectTransform)_seats[i].transform,point.x-.10f,point.y-.057f,point.x+.10f,point.y+.057f);
            }
        }

        public void SetTablePlay(string caption, IReadOnlyList<CardModel> cards)
        {
            _playCaption.text = caption;
            if (cards.Count > 0 && (_played.Count == 0 || _played[_played.Count-1].Id != cards[cards.Count-1].Id))
                foreach (var card in cards) AddPlayed(card);
        }

        public void SetHand(IReadOnlyList<CardModel> cards)
        {
            _held = cards;
            _hand.Show(cards);
        }

        private void ClearPile()
        {
            foreach (var card in _played) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            _played.Clear();
        }

        private void AddPlayed(CardModel model)
        {
            if (_played.Count > 0) _played[_played.Count-1].ShowBack();
            var card = CardView.Create(_pile);
            card.Bind(model);
            Ui.Stretch((RectTransform)card.transform);
            card.transform.localRotation = Quaternion.Euler(0,0,(_played.Count % 5 - 2)*2);
            ((RectTransform)card.transform).anchoredPosition += Vector2.one * (_played.Count % 7)*2;
            _played.Add(card);
        }

        private void LayoutHand()
        {
            for (int i=0;i<_held.Count;i++)
            {
                var slot = (RectTransform)_hand.transform.GetChild(i);
                if (slot.GetComponent<CardView>().Dragging) continue;
                float u = (i+.5f)/_held.Count;
                float x = Mathf.Lerp(.08f,.92f,u);
                float width = Mathf.Min(.38f,1.12f/_held.Count);
                float target = _raised || _held[i].Selected ? .06f : -.25f;
                float y = Mathf.MoveTowards(slot.anchorMin.y,target,Time.unscaledDeltaTime*3);
                slot.anchorMin = new Vector2(x-width/2,y);
                slot.anchorMax = new Vector2(x+width/2,y+1);
                slot.sizeDelta = Vector2.zero;
                slot.anchoredPosition = Vector2.Lerp(slot.anchoredPosition,Vector2.zero,Time.unscaledDeltaTime*15);
                slot.localRotation = Quaternion.Euler(0,0,(.5f-u)*28);
            }
        }

        public void SetActions(bool canPlay, bool canCallLiar, bool visible)
        {
            _canPlay = visible && canPlay;
            _liarButton.gameObject.SetActive(visible);
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
            _effect.color = Color.clear;
            _wheel.sprite = Ui.Theme.revolver;
            _token.localRotation = Quaternion.identity;
            _token.anchoredPosition = Vector2.zero;
            bool wheel = evt is RevolverShooted;
            _flash.gameObject.SetActive(false);
            float diameter = Mathf.Min(_effect.rectTransform.rect.width,_effect.rectTransform.rect.height) * Ui.Theme.drumScreenFraction;
            _token.sizeDelta = wheel ? Vector2.one * diameter : new Vector2(90,90);
            if (wheel) _effect.color = new Color(.04f,.025f,.02f,Ui.Theme.rouletteDim);
            _wheel.enabled = wheel && _wheel.sprite != null;
            _effect.sprite = null;
            _symbol.gameObject.SetActive(!wheel);
            foreach (var chamber in _chambers) chamber.gameObject.SetActive(wheel && !_wheel.enabled);
            Vector3 start = _token.position;


            Vector3 SeatPosition(int seat)
            {
                // Seats use an evenly spaced horizontal row, including during the first deal.
                var seats = state.Seats.ToList();
                int index = seats.FindIndex(s => s.SeatId == seat);
                if (index >= 0 && index < _seats.Count) return _seats[index].transform.position;
                var rect = _seatsRow.rect;
                return _seatsRow.TransformPoint(new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, (index + 0.5f) / seats.Count), rect.center.y));
            }

            if (evt is RoundStarted round)
            {
                Ui.Sounds?.Play(Ui.Theme.cards);
                ClearPile();
                var image = Resources.Load<Sprite>("RoundSubjects/"+round.SubjectId);
                _subject.sprite = image != null ? image : Back;
                _subject.transform.GetChild(0).gameObject.SetActive(image == null);
                _header.text = $"Раунд {round.RoundNumber} · Предмет: {round.SubjectTitle}";
                _effectTitle.text = "";
                _symbol.text = "?";
                foreach (var seat in round.HandSizes.Keys)
                    yield return Fly(start, SeatPosition(seat), 0.16f);
            }
            else if (evt is CardsPlayed play)
            {
                Ui.Sounds?.Play(Ui.Theme.cards);
                _effectTitle.text = state.GetSeat(play.SeatId).Name;
                _symbol.gameObject.SetActive(false);
                _wheel.enabled = true;
                _wheel.sprite = Back;
                _wheel.color = Color.white;
                _token.sizeDelta = _pile.rect.size;
                foreach (var face in play.Cards)
                {
                    if (_played.Count > 0)
                    {
                        var top = _played[_played.Count-1];
                        for(float t=0;t<.12f;t+=Time.unscaledDeltaTime)
                        { top.transform.localScale = new Vector3(1-t/.12f,1,1); yield return null; }
                        top.ShowBack();
                        top.transform.localScale = Vector3.one;
                    }
                    yield return Fly(SeatPosition(play.SeatId),_pile.TransformPoint(_pile.rect.center),.32f);
                    AddPlayed(new CardModel(face.Id,face.Text,null,false,false));
                }
                _wheel.color = Color.white;
                _token.sizeDelta = new Vector2(90,90);
            }
            else if (evt is LiarCalled call)
            {
                Ui.Sounds?.Play(Ui.Theme.liar);
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
                Ui.Sounds?.Play(Ui.Theme.spin);
                _effectTitle.text = state.GetSeat(shot.SeatId).Name + " · рулетка";
                for (float t = 0; t < 0.55f; t += Time.unscaledDeltaTime)
                {
                    _token.localRotation = Quaternion.Euler(0, 0, 720 * Mathf.SmoothStep(0, 1, t / 0.55f));
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(1.2f);
                Ui.Sounds?.Play(shot.Hit ? Ui.Theme.shot : Ui.Theme.empty);
                _effectTitle.text = shot.Hit ? "БАХ! " + state.GetSeat(shot.SeatId).Name + " выбывает" :
                    $"Щёлк… пусто. {shot.ShotsTaken}/{shot.Chambers}";
                if (shot.Hit)
                {
                    _flash.gameObject.SetActive(true);
                    yield return ShakeTable();
                    _flash.gameObject.SetActive(false);
                }
                yield return new WaitForSecondsRealtime(0.8f);
            }
            _effect.gameObject.SetActive(false);
        }

        internal IEnumerator FlipTableCards(FactsRevealed revealed)
        {
            foreach (var fact in revealed.Facts)
                {
                var card = _played.LastOrDefault(c=>c.Id==fact.Id);
                if (card == null) continue;
                card.transform.SetAsLastSibling();
                card.Bind(new CardModel(fact.Id,fact.Text,fact.IsTrue,false,false));
                yield return new WaitForSecondsRealtime(.22f);
            }
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
                    _flash.color = new Color(1,1,1,Mathf.Pow(1-t/duration,1.5f));
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

        public bool AutoCloseReveal { get; set; }

        private void Update()
        {
            ResizeTable();
            _journalProgress = Mathf.MoveTowards(_journalProgress,_journalOpen ? 1 : 0,
                Time.unscaledDeltaTime / Mathf.Max(.01f,Ui.Theme.journalSlideSeconds));
            float travel = (1-_journal.anchorMin.x)*((RectTransform)transform).rect.width;
            _journal.anchoredPosition = new Vector2(travel * (1-Mathf.SmoothStep(0,1,_journalProgress)),0);
            LayoutHand();
            if (AutoCloseReveal && _reveal.activeSelf && Time.unscaledTime - _revealStartedAt >= 5f)
                RevealClosed?.Invoke();
            if (_reveal == null || !_reveal.activeSelf || _revealText.rectTransform.localScale.x >= 1f) return;
            var progress = Mathf.Clamp01((Time.unscaledTime - _revealStartedAt) / RevealDuration);
            _revealText.rectTransform.localScale = new Vector3(Mathf.Sin(progress * Mathf.PI * 0.5f), 1f, 1f);
        }

        public void HideReveal() => _reveal.SetActive(false);

        public void SetRematchAvailable(bool available) =>
            _gameOver.transform.Find("Box/Button").gameObject.SetActive(available);

        public void ShowGameOver(string text)
        {
            _gameOverText.text = text;
            _gameOver.SetActive(true);
            _gameOver.transform.SetAsLastSibling();
            Ui.Sounds?.Play(Ui.Theme.victory);
            GameOverShown?.Invoke();
        }
    }
}
