using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    /// <summary>
    /// Bootstrap adapter that aggregates only facts exposed by the frozen Match
    /// event surface. Unsupported physical-load facts intentionally remain zero.
    /// </summary>
    internal sealed class MatchPerformanceAccumulatorV1 : IDisposable
    {
        private readonly MatchContextV4 _context;
        private readonly Dictionary<PlayerId, PlayerState> _players =
            new Dictionary<PlayerId, PlayerState>();
        private readonly Dictionary<PlayerId, TeamSide> _sides =
            new Dictionary<PlayerId, TeamSide>();
        private readonly Dictionary<TeamSide, PendingReception> _receptions =
            new Dictionary<TeamSide, PendingReception>();
        private readonly Dictionary<string, ActionToken> _defenseAttempts =
            new Dictionary<string, ActionToken>(StringComparer.Ordinal);
        private readonly HashSet<string> _successfulDefenseAttempts =
            new HashSet<string>(StringComparer.Ordinal);

        private PhysicalMatchRallyDirector _director;
        private PendingDirectAction _serve;
        private PendingDirectAction _attack;
        private PendingDirectAction _block;
        private PendingDirectAction _setting;
        private double? _rallyStartSeconds;
        private long _sequence;
        private bool _started;
        private bool _completed;

        internal MatchPerformanceAccumulatorV1(MatchContextV4 context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            AddTeam(context.Home, TeamSide.Home);
            AddTeam(context.Away, TeamSide.Away);
            if (_players.Count != MatchPerformanceReportV1.FormalPlayerCount)
            {
                throw new ContractValidationException(
                    "Performance aggregation requires exactly 12 context players.");
            }
        }

        internal static MatchPerformanceAccumulatorV1 Attach(
            PhysicalMatchRallyDirector director)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            var accumulator =
                new MatchPerformanceAccumulatorV1(director.MatchContext);
            accumulator._director = director;
            return accumulator;
        }

        internal void StartCapture()
        {
            if (_started || _completed)
            {
                throw new InvalidOperationException(
                    "Performance capture may only be started once.");
            }

            _started = true;
            if (_director == null)
            {
                return;
            }

            _director.ReplayServeStarted += RecordServe;
            _director.ReplayContactAccepted += RecordContact;
            _director.ReplayDefenseAttemptRecorded += RecordDefenseAttempt;
            _director.ReplayRallyResolved += RecordResolution;
        }

        internal MatchPerformanceReportV1 Complete(MatchResultV4 result)
        {
            if (!_started)
            {
                throw new InvalidOperationException(
                    "Performance capture must start before completion.");
            }

            if (_completed)
            {
                throw new InvalidOperationException(
                    "Performance capture has already completed.");
            }

            result?.ValidateAgainst(_context);
            _completed = true;
            Detach();
            var reports = _players.Values
                .Select(player => player.ToReport())
                .ToArray();
            return MatchPerformanceReportV1.Create(_context, result, reports);
        }

        public void Dispose()
        {
            Detach();
        }

        internal void RecordServe(
            PlayerId serverId,
            TeamSide side,
            double simulationTimeSeconds,
            int homeScore,
            int awayScore)
        {
            RequireCapturing();
            var player = RequirePlayer(serverId, side);
            player.ServeAttempts++;
            _serve = new PendingDirectAction(
                player,
                side,
                player.AddAction(
                    NextSequence(),
                    IsCritical(homeScore, awayScore)));
            _rallyStartSeconds = simulationTimeSeconds;
        }

        internal void RecordAcceptedContact(
            PlayerId playerId,
            TeamSide side,
            string action,
            double simulationTimeSeconds,
            int homeScore,
            int awayScore)
        {
            RequireCapturing();
            var player = RequirePlayer(playerId, side);
            if (!_rallyStartSeconds.HasValue)
            {
                _rallyStartSeconds = simulationTimeSeconds;
            }

            MarkOpponentContact(_serve, side);
            MarkFollowUpContact(_attack);
            MarkFollowUpContact(_block);
            var critical = IsCritical(homeScore, awayScore);
            switch (action)
            {
                case "Attack":
                    if (_setting != null && _setting.Side == side)
                    {
                        _setting.Player.SettingSuccessful++;
                        _setting.Token.MarkSuccess();
                        _setting = null;
                    }

                    player.SpikeAttempts++;
                    _attack = new PendingDirectAction(
                        player,
                        side,
                        player.AddAction(NextSequence(), critical));
                    break;
                case "Block":
                    // The frozen event surface cannot prove uncontacted block
                    // windows. A real accepted touch is both an attempt and an
                    // effective touch in this conservative aggregation version.
                    player.BlockAttempts++;
                    player.BlockEffectiveTouches++;
                    _block = new PendingDirectAction(
                        player,
                        side,
                        player.AddAction(NextSequence(), critical));
                    break;
                case "Set":
                    player.SettingAttempts++;
                    _setting = new PendingDirectAction(
                        player,
                        side,
                        player.AddAction(NextSequence(), critical));
                    break;
                case "Receive":
                    FinalizePendingReceptionAsNegative(side);
                    player.ReceptionAttempts++;
                    _receptions[side] = new PendingReception(
                        player,
                        player.AddAction(NextSequence(), critical));
                    break;
            }
        }

        internal void RecordReceptionQuality(
            TeamSide side,
            double horizontalDistanceMeters)
        {
            RequireCapturing();
            if (!_receptions.TryGetValue(side, out var pending))
            {
                return;
            }

            if (double.IsNaN(horizontalDistanceMeters) ||
                double.IsInfinity(horizontalDistanceMeters) ||
                horizontalDistanceMeters < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(horizontalDistanceMeters));
            }

            if (horizontalDistanceMeters <= 0.75d)
            {
                pending.Player.ReceptionPerfect++;
                pending.Token.MarkSuccess();
            }
            else if (horizontalDistanceMeters <= 1.50d)
            {
                pending.Player.ReceptionPositive++;
                pending.Token.MarkSuccess();
            }
            else if (horizontalDistanceMeters <= 2.75d)
            {
                pending.Player.ReceptionNeutral++;
                pending.Token.MarkNonError();
            }
            else
            {
                pending.Player.ReceptionNegative++;
                pending.Token.MarkNonError();
            }

            _receptions.Remove(side);
        }

        internal void RecordDefenseAttempt(
            string attemptIdentity,
            PlayerId playerId,
            TeamSide side,
            string responsibilityKind,
            string eventKind,
            int homeScore,
            int awayScore)
        {
            RequireCapturing();
            if (!string.Equals(
                    responsibilityKind,
                    "FloorDefense",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(attemptIdentity))
            {
                throw new ArgumentException(
                    "Defense attempt identity is required.",
                    nameof(attemptIdentity));
            }

            var player = RequirePlayer(playerId, side);
            if (string.Equals(
                    eventKind,
                    "DefenseAttemptOpened",
                    StringComparison.Ordinal) &&
                !_defenseAttempts.ContainsKey(attemptIdentity))
            {
                player.DefenseAttempts++;
                _defenseAttempts.Add(
                    attemptIdentity,
                    player.AddAction(
                        NextSequence(),
                        IsCritical(homeScore, awayScore)));
            }

            if (string.Equals(
                    eventKind,
                    "DefenseContactAccepted",
                    StringComparison.Ordinal) &&
                _defenseAttempts.TryGetValue(attemptIdentity, out var token) &&
                _successfulDefenseAttempts.Add(attemptIdentity))
            {
                player.DefenseSuccesses++;
                token.MarkSuccess();
            }
        }

        internal void RecordRallyResolved(
            TeamSide winningSide,
            PlayerId? errorPlayerId,
            double simulationTimeSeconds)
        {
            RequireCapturing();
            ResolveServe(winningSide);
            ResolveAttack(winningSide, errorPlayerId);
            ResolveBlock(winningSide);
            ResolveSetting(errorPlayerId);
            ResolveReceptions(winningSide, errorPlayerId);

            var durationMilliseconds = _rallyStartSeconds.HasValue
                ? Milliseconds(simulationTimeSeconds - _rallyStartSeconds.Value)
                : 0L;
            foreach (var player in _players.Values)
            {
                player.RalliesOnCourt++;
                player.ActiveDurationMilliseconds = checked(
                    player.ActiveDurationMilliseconds + durationMilliseconds);
            }

            _serve = null;
            _attack = null;
            _block = null;
            _setting = null;
            _receptions.Clear();
            _defenseAttempts.Clear();
            _successfulDefenseAttempts.Clear();
            _rallyStartSeconds = null;
        }

        private void RecordServe(ReplaySimpleEvent replayEvent)
        {
            if (!replayEvent.PlayerId.HasValue)
            {
                throw new ContractValidationException(
                    "A formal serve event must identify its server.");
            }

            RecordServe(
                replayEvent.PlayerId.Value,
                Side(replayEvent.Team.ToString()),
                replayEvent.SimulationTimeSeconds,
                _director.HomeScore,
                _director.AwayScore);
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!replayEvent.PlayerId.HasValue)
            {
                throw new ContractValidationException(
                    "An accepted formal contact must identify its actor.");
            }

            var side = Side(replayEvent.Team.ToString());
            var isServeReception =
                _serve != null &&
                _serve.Side != side &&
                !_serve.OutcomeInvalidated;
            RecordAcceptedContact(
                replayEvent.PlayerId.Value,
                side,
                NormalizeAction(
                    replayEvent.Action.ToString(),
                    replayEvent.AttackDefenseAuthority?.Kind.ToString(),
                    replayEvent.OrganizationAuthority != null,
                    isServeReception),
                replayEvent.SimulationTimeSeconds,
                _director.HomeScore,
                _director.AwayScore);
            var evidence = replayEvent.OrganizationAuthority?.Evidence;
            if (evidence?.ActualFirstPassLanding.HasValue == true)
            {
                var landing = evidence.ActualFirstPassLanding.Value;
                var target = evidence.Plan.OrganizationTarget;
                var deltaX = (double)landing.X - target.X;
                var deltaZ = (double)landing.Z - target.Z;
                RecordReceptionQuality(
                    side,
                    Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ)));
            }
        }

        private void RecordDefenseAttempt(ReplayDefenseAttemptEvent replayEvent)
        {
            RecordDefenseAttempt(
                replayEvent.AttemptIdentity,
                replayEvent.Receipt.Actor,
                Side(replayEvent.Team.ToString()),
                replayEvent.Receipt.Kind.ToString(),
                replayEvent.Kind,
                _director.HomeScore,
                _director.AwayScore);
        }

        private void RecordResolution(ReplayRallyResolvedEvent replayEvent)
        {
            RecordRallyResolved(
                Side(replayEvent.Team.ToString()),
                replayEvent.ErrorPlayerId,
                replayEvent.SimulationTimeSeconds);
        }

        private void ResolveServe(TeamSide winningSide)
        {
            if (_serve == null || _serve.OutcomeInvalidated)
            {
                return;
            }

            if (_serve.Side == winningSide)
            {
                _serve.Player.ServeAces++;
                _serve.Token.MarkSuccess();
            }
            else
            {
                _serve.Player.ServeErrors++;
                _serve.Token.MarkError();
            }
        }

        private void ResolveAttack(
            TeamSide winningSide,
            PlayerId? errorPlayerId)
        {
            if (_attack == null || _attack.OutcomeInvalidated)
            {
                return;
            }

            if (_attack.Side == winningSide)
            {
                _attack.Player.SpikePoints++;
                _attack.Token.MarkSuccess();
            }
            else if (errorPlayerId.HasValue &&
                     errorPlayerId.Value.Equals(_attack.Player.PlayerId))
            {
                _attack.Player.SpikeErrors++;
                _attack.Token.MarkError();
            }
            else
            {
                _attack.Token.MarkNonError();
            }
        }

        private void ResolveBlock(TeamSide winningSide)
        {
            if (_block == null ||
                _block.OutcomeInvalidated ||
                _block.Side != winningSide)
            {
                return;
            }

            _block.Player.BlockPoints++;
            _block.Token.MarkSuccess();
        }

        private void ResolveSetting(PlayerId? errorPlayerId)
        {
            if (_setting == null ||
                !errorPlayerId.HasValue ||
                !errorPlayerId.Value.Equals(_setting.Player.PlayerId))
            {
                return;
            }

            _setting.Player.SettingErrors++;
            _setting.Token.MarkError();
        }

        private void ResolveReceptions(
            TeamSide winningSide,
            PlayerId? errorPlayerId)
        {
            foreach (var pending in _receptions.Values)
            {
                if (errorPlayerId.HasValue &&
                    errorPlayerId.Value.Equals(pending.Player.PlayerId) &&
                    _sides[pending.Player.PlayerId] != winningSide)
                {
                    pending.Player.ReceptionErrors++;
                    pending.Token.MarkError();
                }
                else
                {
                    pending.Player.ReceptionNegative++;
                    pending.Token.MarkNonError();
                }
            }
        }

        private void FinalizePendingReceptionAsNegative(TeamSide side)
        {
            if (!_receptions.TryGetValue(side, out var pending))
            {
                return;
            }

            pending.Player.ReceptionNegative++;
            pending.Token.MarkNonError();
            _receptions.Remove(side);
        }

        private void AddTeam(TeamSnapshotV4 team, TeamSide side)
        {
            foreach (var player in team.RotationOrder)
            {
                if (_players.ContainsKey(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "Performance aggregation requires unique player IDs.");
                }

                _players.Add(player.PlayerId, new PlayerState(player.PlayerId));
                _sides.Add(player.PlayerId, side);
            }
        }

        private PlayerState RequirePlayer(PlayerId playerId, TeamSide side)
        {
            if (!_players.TryGetValue(playerId, out var player) ||
                _sides[playerId] != side)
            {
                throw new ContractValidationException(
                    "Performance event actor is outside the context team.");
            }

            return player;
        }

        private static void MarkOpponentContact(
            PendingDirectAction pending,
            TeamSide contactSide)
        {
            if (pending != null && pending.Side != contactSide)
            {
                pending.OutcomeInvalidated = true;
                pending.Token.MarkNonError();
            }
        }

        private static void MarkFollowUpContact(PendingDirectAction pending)
        {
            if (pending != null)
            {
                pending.OutcomeInvalidated = true;
                pending.Token.MarkNonError();
            }
        }

        private long NextSequence()
        {
            return checked(++_sequence);
        }

        private void RequireCapturing()
        {
            if (!_started || _completed)
            {
                throw new InvalidOperationException(
                    "Performance facts require an active capture.");
            }
        }

        private void Detach()
        {
            if (_director == null)
            {
                return;
            }

            _director.ReplayServeStarted -= RecordServe;
            _director.ReplayContactAccepted -= RecordContact;
            _director.ReplayDefenseAttemptRecorded -= RecordDefenseAttempt;
            _director.ReplayRallyResolved -= RecordResolution;
            _director = null;
        }

        private static TeamSide Side(string runtimeTeam)
        {
            if (string.Equals(runtimeTeam, "Blue", StringComparison.Ordinal))
            {
                return TeamSide.Home;
            }

            if (string.Equals(runtimeTeam, "Orange", StringComparison.Ordinal))
            {
                return TeamSide.Away;
            }

            throw new ContractValidationException(
                "Unknown formal runtime team: " + runtimeTeam + ".");
        }

        internal static string NormalizeAction(
            string action,
            string attackDefenseKind,
            bool hasOrganizationAuthority,
            bool isServeReception)
        {
            if (!string.Equals(action, "Receive", StringComparison.Ordinal))
            {
                return action;
            }

            if (string.Equals(
                    attackDefenseKind,
                    "FloorDefense",
                    StringComparison.Ordinal))
            {
                return "Defense";
            }

            return hasOrganizationAuthority && isServeReception
                ? "Receive"
                : "Other";
        }

        private static bool IsCritical(int homeScore, int awayScore)
        {
            if (homeScore < 0 || awayScore < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(homeScore),
                    "Scores must be non-negative.");
            }

            return (Math.Max(homeScore, awayScore) >= 20 &&
                    Math.Abs(homeScore - awayScore) <= 2) ||
                   WouldWin(homeScore + 1, awayScore) ||
                   WouldWin(awayScore + 1, homeScore);
        }

        private static bool WouldWin(int score, int opponentScore)
        {
            return score >= 25 && score - opponentScore >= 2;
        }

        private static long Milliseconds(double durationSeconds)
        {
            if (double.IsNaN(durationSeconds) ||
                double.IsInfinity(durationSeconds) ||
                durationSeconds <= 0d)
            {
                return 0L;
            }

            return checked((long)Math.Round(
                durationSeconds * 1000d,
                MidpointRounding.AwayFromZero));
        }

        private sealed class PendingDirectAction
        {
            public PendingDirectAction(
                PlayerState player,
                TeamSide side,
                ActionToken token)
            {
                Player = player;
                Side = side;
                Token = token;
            }

            public PlayerState Player { get; }
            public TeamSide Side { get; }
            public ActionToken Token { get; }
            public bool OutcomeInvalidated { get; set; }
        }

        private sealed class PendingReception
        {
            public PendingReception(PlayerState player, ActionToken token)
            {
                Player = player;
                Token = token;
            }

            public PlayerState Player { get; }
            public ActionToken Token { get; }
        }

        private enum ActionOutcome
        {
            Unknown,
            Success,
            NonError,
            Error
        }

        private sealed class ActionToken
        {
            public ActionToken(long sequence, bool critical)
            {
                Sequence = sequence;
                Critical = critical;
            }

            public long Sequence { get; }
            public bool Critical { get; }
            public ActionOutcome Outcome { get; private set; }

            public void MarkSuccess()
            {
                Outcome = ActionOutcome.Success;
            }

            public void MarkNonError()
            {
                if (Outcome == ActionOutcome.Unknown)
                {
                    Outcome = ActionOutcome.NonError;
                }
            }

            public void MarkError()
            {
                Outcome = ActionOutcome.Error;
            }
        }

        private sealed class PlayerState
        {
            private readonly List<ActionToken> _actions =
                new List<ActionToken>();

            public PlayerState(PlayerId playerId)
            {
                PlayerId = playerId;
            }

            public PlayerId PlayerId { get; }
            public int SpikeAttempts { get; set; }
            public int SpikePoints { get; set; }
            public int SpikeErrors { get; set; }
            public int ServeAttempts { get; set; }
            public int ServeAces { get; set; }
            public int ServeErrors { get; set; }
            public int ReceptionAttempts { get; set; }
            public int ReceptionPerfect { get; set; }
            public int ReceptionPositive { get; set; }
            public int ReceptionNeutral { get; set; }
            public int ReceptionNegative { get; set; }
            public int ReceptionErrors { get; set; }
            public int DefenseAttempts { get; set; }
            public int DefenseSuccesses { get; set; }
            public int BlockAttempts { get; set; }
            public int BlockEffectiveTouches { get; set; }
            public int BlockPoints { get; set; }
            public int SettingAttempts { get; set; }
            public int SettingSuccessful { get; set; }
            public int SettingErrors { get; set; }
            public int RalliesOnCourt { get; set; }
            public long ActiveDurationMilliseconds { get; set; }

            public ActionToken AddAction(long sequence, bool critical)
            {
                var action = new ActionToken(sequence, critical);
                _actions.Add(action);
                return action;
            }

            public PlayerPerformanceReportV1 ToReport()
            {
                var criticalActions = 0;
                var criticalSuccesses = 0;
                var criticalErrors = 0;
                var errorStreakEpisodes = 0;
                var longestErrorStreak = 0;
                var currentErrorStreak = 0;
                foreach (var action in _actions.OrderBy(value => value.Sequence))
                {
                    if (action.Critical)
                    {
                        criticalActions++;
                        if (action.Outcome == ActionOutcome.Success)
                        {
                            criticalSuccesses++;
                        }
                        else if (action.Outcome == ActionOutcome.Error)
                        {
                            criticalErrors++;
                        }
                    }

                    if (action.Outcome == ActionOutcome.Error)
                    {
                        currentErrorStreak++;
                        if (currentErrorStreak == 2)
                        {
                            errorStreakEpisodes++;
                        }

                        longestErrorStreak = Math.Max(
                            longestErrorStreak,
                            currentErrorStreak);
                    }
                    else if (action.Outcome == ActionOutcome.Success ||
                             action.Outcome == ActionOutcome.NonError)
                    {
                        currentErrorStreak = 0;
                    }
                }

                return new PlayerPerformanceReportV1(
                    PlayerId,
                    new SpikePerformanceFactsV1(
                        SpikeAttempts,
                        SpikePoints,
                        SpikeErrors),
                    new ServePerformanceFactsV1(
                        ServeAttempts,
                        ServeAces,
                        ServeErrors),
                    new ReceptionPerformanceFactsV1(
                        ReceptionAttempts,
                        ReceptionPerfect,
                        ReceptionPositive,
                        ReceptionNeutral,
                        ReceptionNegative,
                        ReceptionErrors),
                    new DefensePerformanceFactsV1(
                        DefenseAttempts,
                        DefenseSuccesses),
                    new BlockPerformanceFactsV1(
                        BlockAttempts,
                        BlockEffectiveTouches,
                        BlockPoints),
                    new SettingPerformanceFactsV1(
                        SettingAttempts,
                        SettingSuccessful,
                        SettingErrors),
                    new MatchLoadFactsV1(
                        RalliesOnCourt,
                        ActiveDurationMilliseconds,
                        0,
                        0,
                        0,
                        0,
                        0),
                    new StabilityPerformanceFactsV1(
                        criticalActions,
                        criticalSuccesses,
                        criticalErrors,
                        errorStreakEpisodes,
                        longestErrorStreak));
            }
        }
    }
}
