using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application.Sessions;
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
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private BotProfileAsset[] _botProfiles;
        [SerializeField] private string _playerName = "Игрок";
        [SerializeField, Range(1, 5)] private int _botCount = 3;

        private GameRulesConfig _config;
        private MatchFactory _factory;
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
            _view = TableView.Create(_font);
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
            _presenter = new TablePresenter(_session, _view, _config.MinCardsPerPlay, _config.MaxCardsPerPlay);
            _session.Start();
        }

        private IReadOnlyList<BotProfile> LoadBotProfiles()
        {
            var profiles = (_botProfiles ?? new BotProfileAsset[0]).Where(a => a != null).Select(a => a.Profile).ToList();
            return profiles.Count > 0 ? profiles : BotProfile.Defaults;
        }

        private void DisposeMatch()
        {
            _presenter?.Dispose();
            _presenter = null;
            _session?.Dispose();
            _session = null;
        }

        private void OnDestroy() => DisposeMatch();
    }
}
