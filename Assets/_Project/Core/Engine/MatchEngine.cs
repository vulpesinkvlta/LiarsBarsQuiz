using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine.Rules;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;

namespace LiarsBar.Core.Engine
{
    /// <summary>
    /// Единственная точка входа в правила: Start(settings) и Apply(action).
    /// Один движок для всех режимов (боты, hot-seat, хост онлайн).
    /// Действие сначала проверяется правилами, затем применяется к копии состояния;
    /// копия заменяет текущее состояние только при успехе, так что ошибка ничего не меняет.
    /// </summary>
    public sealed class MatchEngine
    {
        private readonly GameRulesConfig _config;
        private readonly ISubjectProvider _subjects;
        private readonly IRandom _random;
        private readonly IReadOnlyList<IActionRule> _rules;
        private readonly Dealer _dealer;

        /// <summary>Текущее полное состояние (null до Start). Наружу отдавайте SnapshotFor, а не его.</summary>
        public MatchState State { get; private set; }

        public GameRulesConfig Config => _config;

        public MatchEngine(GameRulesConfig config, ISubjectProvider subjects, IRandom random, IReadOnlyList<IActionRule> rules = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _rules = rules ?? ActionRules.Default(config);
            _dealer = new Dealer(config, random);
        }

        public PlayerView SnapshotFor(int seatId) =>
            (State ?? throw new InvalidOperationException("Match is not started.")).SnapshotFor(seatId);

        public ApplyResult Start(MatchSettings settings)
        {
            if (settings == null) return ApplyResult.Fail(ActionErrorCode.InvalidSettings, "Settings are null.");
            if (State != null) return ApplyResult.Fail(ActionErrorCode.InvalidSettings, "Match is already started.");

            var configErrors = _config.Validate();
            if (configErrors.Count > 0) return ApplyResult.Fail(ActionErrorCode.InvalidSettings, string.Join(" ", configErrors));

            var seats = settings.Seats;
            if (seats.Count < _config.MinPlayers || seats.Count > _config.MaxPlayers)
                return ApplyResult.Fail(ActionErrorCode.InvalidSettings, $"Players must be {_config.MinPlayers}-{_config.MaxPlayers}, got {seats.Count}.");
            if (seats.Any(s => s == null || s.SeatId < 0))
                return ApplyResult.Fail(ActionErrorCode.InvalidSettings, "Seat ids must be >= 0.");
            if (seats.Select(s => s.SeatId).Distinct().Count() != seats.Count)
                return ApplyResult.Fail(ActionErrorCode.InvalidSettings, "Duplicate seat ids.");
            if (seats.Any(s => s.BulletIndex.HasValue && (s.BulletIndex < 0 || s.BulletIndex >= _config.RevolverChambers)))
                return ApplyResult.Fail(ActionErrorCode.InvalidSettings, "Bullet index is out of range.");
            if (settings.FirstSeat.HasValue && seats.All(s => s.SeatId != settings.FirstSeat.Value))
                return ApplyResult.Fail(ActionErrorCode.InvalidSettings, $"First seat {settings.FirstSeat} is not at the table.");

            // Предмет должен раздаваться на любое число живых: от полного стола до двух игроков.
            for (var count = _config.MinPlayers; count <= seats.Count; count++)
            {
                var playable = count;
                if (!(_subjects.GetSubjects() ?? Array.Empty<Subject>()).Any(s => _dealer.CanDeal(s, playable)))
                    return ApplyResult.Fail(ActionErrorCode.NotEnoughContent, $"No subject can be dealt to {count} players.");
            }

            var players = seats.Select(s => new PlayerState(s.SeatId, s.Name,
                s.BulletIndex.HasValue
                    ? new Revolver(s.BulletIndex.Value, _config.RevolverChambers)
                    : Revolver.CreateRandom(_random, _config.RevolverChambers)));

            var state = new MatchState(players);
            var first = settings.FirstSeat ?? _random.Pick(state.Players).SeatId;
            var events = new List<GameEvent>();
            StartRound(state, first, events);

            State = state;
            return ApplyResult.Success(events.AsReadOnly());
        }

        public ApplyResult Apply(IGameAction action)
        {
            if (action == null) return ApplyResult.Fail(ActionErrorCode.NullAction, "Action is null.");
            if (State == null) return ApplyResult.Fail(ActionErrorCode.MatchNotInProgress, "Match is not started.");
            if (!(action is PlayCardsAction) && !(action is CallLiarAction))
                return ApplyResult.Fail(ActionErrorCode.UnknownAction, $"Unknown action {action.GetType().Name}.");

            foreach (var rule in _rules)
            {
                var error = rule.Validate(State, action);
                if (error != null) return ApplyResult.Fail(error);
            }

            var next = State.Clone();
            var events = new List<GameEvent>();
            switch (action)
            {
                case PlayCardsAction play:
                    ApplyPlay(next, play, events);
                    break;
                case CallLiarAction call:
                    ApplyCallLiar(next, call, events);
                    break;
            }

            State = next;
            return ApplyResult.Success(events.AsReadOnly());
        }

        private void ApplyPlay(MatchState state, PlayCardsAction action, List<GameEvent> events)
        {
            var player = state.GetPlayer(action.SeatId);

            // Предыдущий ход пропустили без обвинения: его ложь сошла с рук.
            if (state.LastPlay != null)
                state.GetPlayer(state.LastPlay.SeatId).Stats.UncaughtLies += state.LastPlay.LieCount;

            var cards = player.Hand.Remove(action.CardIds)
                        ?? throw new InvalidOperationException("Validated cards are missing from the hand.");
            player.Stats.CardsPlayed += cards.Count;
            state.LastPlay = new TablePlay(player.SeatId, cards);

            events.Add(new CardsPlayed(player.SeatId, cards.Select(CardFace.From).ToList().AsReadOnly(), player.Hand.Count));

            var turn = TurnOrder.AfterPlay(state, player.SeatId);
            SetTurn(state, turn.SeatId, turn.Forced, events);
        }

        private void ApplyCallLiar(MatchState state, CallLiarAction action, List<GameEvent> events)
        {
            var accused = state.LastPlay;
            var accuser = state.GetPlayer(action.SeatId);
            events.Add(new LiarCalled(accuser.SeatId, accused.SeatId, state.ForcedAccusation));

            var verdict = AccusationResolver.Resolve(accused, accuser.SeatId);
            if (verdict.LieFound) accuser.Stats.CorrectAccusations++;
            else accuser.Stats.WrongAccusations++;

            events.Add(new FactsRevealed(accused.SeatId, accused.Cards, verdict.LieFound, verdict.ShooterSeat));

            state.Phase = MatchPhase.Resolving;
            state.ForcedAccusation = false;
            state.CurrentSeat = -1;

            var shooter = state.GetPlayer(verdict.ShooterSeat);
            var chamber = shooter.Revolver.Shooted;
            var hit = shooter.Revolver.Shot();
            events.Add(new RevolverShooted(shooter.SeatId, chamber, shooter.Revolver.Shooted, shooter.Revolver.Chambers, hit));

            if (hit)
            {
                shooter.IsAlive = false;
                shooter.Hand = new Hand();
                events.Add(new PlayerEliminated(shooter.SeatId));
            }
            else
            {
                shooter.Stats.ShotsSurvived++;
            }

            if (state.AliveCount <= 1)
            {
                var winner = state.AlivePlayers.Single();
                state.Phase = MatchPhase.Ended;
                state.WinnerSeat = winner.SeatId;
                state.LastPlay = null;
                foreach (var p in state.Players) p.Hand = new Hand();
                events.Add(new MatchEnded(winner.SeatId, state.RoundNumber));
                return;
            }

            StartRound(state, TurnOrder.RoundStarter(state, shooter.SeatId), events);
        }

        private void StartRound(MatchState state, int starterSeat, List<GameEvent> events)
        {
            var alive = state.AlivePlayers.Select(p => p.SeatId).ToList();
            var subject = PickSubject(state, alive.Count);
            var hands = _dealer.Deal(subject, alive);

            foreach (var player in state.Players)
                player.Hand = hands.TryGetValue(player.SeatId, out var hand) ? hand : new Hand();

            state.RoundNumber++;
            state.CurrentSubject = subject;
            state.MarkSubjectUsed(subject.Id);
            state.LastPlay = null;
            state.Phase = MatchPhase.AwaitingAction;

            var sizes = hands.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
            events.Add(new RoundStarted(state.RoundNumber, subject.Id, subject.Title, starterSeat, sizes));
            SetTurn(state, starterSeat, false, events);
        }

        /// <summary>Случайный подходящий предмет без повторов; когда пул исчерпан, повторы разрешаются заново.</summary>
        private Subject PickSubject(MatchState state, int playerCount)
        {
            var playable = (_subjects.GetSubjects() ?? Array.Empty<Subject>())
                .Where(s => _dealer.CanDeal(s, playerCount))
                .ToList();
            if (playable.Count == 0)
                throw new InvalidOperationException($"No subject can be dealt to {playerCount} players.");

            var fresh = playable.Where(s => !state.UsedSubjectIds.Contains(s.Id)).ToList();
            if (fresh.Count == 0)
            {
                state.ResetUsedSubjects();
                // Не повторяем только что сыгранный предмет, если есть выбор.
                fresh = playable.Where(s => s.Id != state.CurrentSubject?.Id).ToList();
                if (fresh.Count == 0) fresh = playable;
            }
            return _random.Pick(fresh);
        }

        private static void SetTurn(MatchState state, int seatId, bool forced, List<GameEvent> events)
        {
            state.CurrentSeat = seatId;
            state.ForcedAccusation = forced;
            events.Add(new TurnChanged(seatId, forced));
        }
    }
}
