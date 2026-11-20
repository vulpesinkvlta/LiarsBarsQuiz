using System;
using LiarsBar.Application.Sessions;
using LiarsBar.Presentation.Table;
using UnityEngine;

namespace LiarsBar.Presentation.Common
{
    public sealed class ScreenRouter : MonoBehaviour
    {
        private enum Screen { Splash, Menu, Rules, Setup, Table, ConfirmExit, GameOver }

        private const string RulesSeen = "LiarsBar.RulesSeen";
        private const string SoundEnabled = "LiarsBar.SoundEnabled";
        private static readonly string[] Rules =
        {
            "Крупье даёт предмет и факты.\nСреди карт в руке есть и правда, и ложь.",
            "Ходи или кричи «Лжец!».\nСыграй одну или две карты либо оспорь последний ход.",
            "Ошибся — играй в рулетку.\nЕсли среди сыгранных карт есть ложь, рискует сыгравший. Если все правдивы, рискует обвинитель."
        };

        private Screen _screen;
        private RectTransform _page;
        private TableView _table;
        private Action<GameModeSettings> _start;
        private Action _leave;
        private GameModeSettings _lastMatch;
        private int _ruleIndex;
        private string _defaultName;
        private int _defaultBots;

        public void Initialize(TableView table, Action<GameModeSettings> start, Action leave, string defaultName, int defaultBots)
        {
            _table = table;
            _start = start;
            _leave = leave;
            _defaultName = defaultName;
            _defaultBots = defaultBots;
            _table.ExitClicked += RequestExit;
            _table.RestartClicked += Restart;
            _table.GameOverShown += OnGameOver;
            AudioListener.volume = PlayerPrefs.GetInt(SoundEnabled, 1);
            Page(Screen.Splash, "Бар лжецов");
            Button("Войти в бар", 0.44f, () =>
            {
                if (PlayerPrefs.GetInt(RulesSeen, 0) == 0) OpenRules();
                else Menu();
            });
        }

        private void Page(Screen next, string title)
        {
            ClearPage();
            _screen = next;
            _table.gameObject.SetActive(false);
            _page = Ui.Panel(next.ToString(), transform, Ui.Background).rectTransform;
            Ui.Stretch(_page);
            var heading = Ui.Label("Title", _page, title, 50);
            Ui.Anchor(heading.rectTransform, 0.05f, 0.82f, 0.95f, 0.97f);
        }

        private void ClearPage()
        {
            if (_page == null) return;
            _page.gameObject.SetActive(false);
            Destroy(_page.gameObject);
            _page = null;
        }

        private UnityEngine.UI.Button Button(string caption, float y, Action click)
        {
            var button = Ui.Button(caption, _page, caption, 30, out _);
            Ui.Anchor((RectTransform)button.transform, 0.18f, y, 0.82f, y + 0.095f);
            button.interactable = click != null;
            if (click != null) button.onClick.AddListener(() => click());
            return button;
        }

        private void Menu()
        {
            Page(Screen.Menu, "Бар лжецов");
            Button("Играть с ботами", 0.68f, () => Setup(GameMode.VsBots));
            Button("На одном устройстве", 0.55f, () => Setup(GameMode.HotSeat));
            Button("Онлайн · скоро", 0.42f, null);
            Button("Правила", 0.29f, OpenRules);
            Button(AudioListener.volume > 0 ? "Звук включён" : "Звук выключен", 0.16f, () =>
            {
                int enabled = AudioListener.volume > 0 ? 0 : 1;
                AudioListener.volume = enabled;
                PlayerPrefs.SetInt(SoundEnabled, enabled);
                PlayerPrefs.Save();
                Menu();
            });
        }

        private void OpenRules()
        {
            _ruleIndex = 0;
            ShowRule();
        }

        private void ShowRule()
        {
            Page(Screen.Rules, $"Правила · {_ruleIndex + 1} / {Rules.Length}");
            var card = Ui.Panel("Card", _page, Ui.PanelColor);
            Ui.Anchor(card.rectTransform, 0.08f, 0.43f, 0.92f, 0.80f);
            var text = Ui.Label("Rule", card.transform, Rules[_ruleIndex], 32);
            Ui.Stretch(text.rectTransform, 18);
            card.gameObject.AddComponent<RuleSwipe>().Swiped = direction =>
            {
                int next = Mathf.Clamp(_ruleIndex + direction, 0, Rules.Length - 1);
                if (next == _ruleIndex) return;
                _ruleIndex = next;
                ShowRule();
            };
            Button(_ruleIndex == Rules.Length - 1 ? "Готово" : "Далее", 0.30f, () =>
            {
                if (_ruleIndex == Rules.Length - 1) CloseRules();
                else { _ruleIndex++; ShowRule(); }
            });
            if (_ruleIndex > 0) Button("Назад", 0.18f, () => { _ruleIndex--; ShowRule(); });
            Button("В меню", 0.06f, CloseRules);
        }

        private void CloseRules()
        {
            PlayerPrefs.SetInt(RulesSeen, 1);
            PlayerPrefs.Save();
            Menu();
        }

        private void Setup(GameMode mode)
        {
            Page(Screen.Setup, mode == GameMode.VsBots ? "Игра с ботами" : "На одном устройстве");
            var setup = new MatchSetupView(_page, mode, _defaultName, _defaultBots);
            Button("Начать игру", 0.15f, () => StartTable(setup.Settings()));
            Button("В меню", 0.035f, Menu);
        }

        private void StartTable(GameModeSettings settings)
        {
            _lastMatch = settings;
            ClearPage();
            _screen = Screen.Table;
            _table.gameObject.SetActive(true);
            _start(settings);
        }

        private void Restart()
        {
            if (_screen == Screen.GameOver) StartTable(_lastMatch);
        }

        private void OnGameOver()
        {
            ClearPage();
            _screen = Screen.GameOver;
            _table.gameObject.SetActive(true);
        }

        private void RequestExit()
        {
            if (_screen == Screen.GameOver) { Leave(); return; }
            if (_screen != Screen.Table) return;
            Page(Screen.ConfirmExit, "Выйти из партии?");
            Button("Продолжить игру", 0.52f, () =>
            {
                ClearPage();
                _screen = Screen.Table;
                _table.gameObject.SetActive(true);
            });
            Button("Выйти в меню", 0.37f, Leave);
        }

        private void Leave()
        {
            _leave();
            Menu();
        }
    }
}
