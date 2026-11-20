using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Sessions;
using LiarsBar.Application.Seats;
using LiarsBar.Core.Views;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Bots;
using LiarsBar.Core.Config;
using LiarsBar.Infrastructure.Config;
using LiarsBar.Infrastructure.Content;
using LiarsBar.Infrastructure.Timing;
using LiarsBar.Presentation.Common;
using LiarsBar.Presentation.Table;
using TMPro;
using UnityEngine;

namespace LiarsBar.Bootstrap
{
    /// <summary>
    /// Composition Root: здесь собираются все зависимости и запускается игра.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private UiTheme _theme;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private BotProfileAsset[] _botProfiles;
        [SerializeField] private string _playerName = "Игрок";
        [SerializeField, Range(1, 5)] private int _botCount = 3;

        private GameRulesConfig _config;
        private MatchFactory _factory;
        private IReadOnlyCollection<string> _usedSubjects;
        private Subject _lastSubject;
        private TableView _view;
        private LocalMatchSession _session;
        private TablePresenter _presenter;

        private void Awake()
        {
            UnityEngine.Application.runInBackground = true;

            _config = GameRulesConfig.Default;
            var subjects = JsonSubjectProvider.FromResources();
            var timer = gameObject.AddComponent<UnityTimerService>();
            _factory = new MatchFactory(_config, subjects, LoadBotProfiles(), timer);

            if (_font == null) Debug.LogWarning("[Bootstrap] Font is not assigned: Cyrillic and ✓/✗ will not render.", this);
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            Ui.Configure(_theme, new UiSoundPlayer(audioSource));
            _view = TableView.Create(Ui.Theme.font != null ? Ui.Theme.font : _font);
            _view.transform.SetParent(transform, false);
            var navigation = UiRoot.CreateCanvas("Navigation", transform, 1);
            navigation.gameObject.AddComponent<ScreenRouter>().Initialize(_view, StartMatch, DisposeMatch, _playerName, _botCount);
        }

        private void StartMatch(GameModeSettings settings)
        {
            DisposeMatch();
            _view.ResetView();

            _session = _factory.Create(settings);
            _session.ControllerActionReplaced += (seat, error) =>
                Debug.LogWarning($"[Session] Seat {seat}: action rejected ({error}), fallback move used.");
            _presenter = new TablePresenter(_session, _view, _config.MinCardsPerPlay, _config.MaxCardsPerPlay, this);
            if (settings.Mode == GameMode.HotSeat)
            {
                _session.ViewChanged += UpdatePassDevice;
                _view.PassConfirmed += ConfirmPass;
                _presenter.PresentationChanged += RefreshPassDevice;
            }
            _session.Start(_usedSubjects, _lastSubject);
        }

        private void ConfirmPass(int seat) => _session.ConfirmHumanTurn(seat);

        private void RefreshPassDevice() => UpdatePassDevice(_session.CurrentView);

        private void UpdatePassDevice(PlayerView snapshot)
        {
            var seat = snapshot.CurrentSeat;
            var waiting = !_presenter.IsAnimating && !_presenter.IsRevealOpen && snapshot.Phase == MatchPhase.AwaitingAction &&
                snapshot.ViewerSeat < 0 && _session.Controllers[seat] is LocalHumanSeat;
            _view.SetPassDevice(waiting ? seat : -1, waiting ? snapshot.GetSeat(seat).Name : null);
        }

        private IReadOnlyList<BotProfile> LoadBotProfiles()
        {
            var profiles = (_botProfiles ?? new BotProfileAsset[0]).Where(a => a != null).Select(a => a.Profile).ToList();
            return profiles.Count > 0 ? profiles : BotProfile.Defaults;
        }

        private void DisposeMatch()
        {
            if (_session != null)
            {
                if (_session.State != null)
                {
                    _usedSubjects = _session.State.UsedSubjectIds.ToArray();
                    _lastSubject = _session.State.CurrentSubject;
                }
                _session.ViewChanged -= UpdatePassDevice;
                _view.PassConfirmed -= ConfirmPass;
                _presenter.PresentationChanged -= RefreshPassDevice;
            }
            _presenter?.Dispose();
            _presenter = null;
            _session?.Dispose();
            _session = null;
        }

        private void OnDestroy() => DisposeMatch();
    }
}
