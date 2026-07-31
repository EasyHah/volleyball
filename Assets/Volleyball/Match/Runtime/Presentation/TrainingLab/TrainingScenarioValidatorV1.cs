using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingScenarioIssueSeverityV1
    {
        Warning,
        Error
    }

    public static class TrainingScenarioIssueCodesV1
    {
        public const string MissingDraft = "TRAINING_DRAFT_MISSING";
        public const string UnsupportedFormat = "TRAINING_FORMAT_UNSUPPORTED";
        public const string InvalidIdentity = "TRAINING_IDENTITY_INVALID";
        public const string InvalidContext = "TRAINING_CONTEXT_INVALID";
        public const string InvalidRotation = "TRAINING_ROTATION_INVALID";
        public const string InvalidTactics = "TRAINING_TACTICS_INVALID";
        public const string InvalidAi = "TRAINING_AI_INVALID";
        public const string MissingPlayer = "TRAINING_PLAYER_MISSING";
        public const string DuplicatePlayer = "TRAINING_PLAYER_DUPLICATE";
        public const string UnexpectedPlayer = "TRAINING_PLAYER_UNEXPECTED";
        public const string InvalidPlayerPose = "TRAINING_PLAYER_POSE_INVALID";
        public const string NonFiniteValue = "TRAINING_VALUE_NON_FINITE";
        public const string PlayerOutOfBounds = "TRAINING_PLAYER_OUT_OF_BOUNDS";
        public const string BallOutOfBounds = "TRAINING_BALL_OUT_OF_BOUNDS";
        public const string PlayerOverlap = "TRAINING_PLAYER_OVERLAP";
        public const string BallPlayerOverlap = "TRAINING_BALL_PLAYER_OVERLAP";
        public const string InvalidRallyStart = "TRAINING_RALLY_START_INVALID";
        public const string InvalidAccess = "TRAINING_ACCESS_INVALID";
    }

    public sealed class TrainingScenarioIssueV1
    {
        public TrainingScenarioIssueV1(
            string code,
            string objectId,
            string propertyPath,
            string message,
            TrainingScenarioIssueSeverityV1 severity)
        {
            Code = code;
            ObjectId = objectId ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }

        public string ObjectId { get; }

        public string PropertyPath { get; }

        public string Message { get; }

        public TrainingScenarioIssueSeverityV1 Severity { get; }
    }

    public sealed class TrainingScenarioValidationResultV1
    {
        internal TrainingScenarioValidationResultV1(
            IReadOnlyList<TrainingScenarioIssueV1> issues)
        {
            Issues = new ReadOnlyCollection<TrainingScenarioIssueV1>(
                issues.ToArray());
        }

        public IReadOnlyList<TrainingScenarioIssueV1> Issues { get; }

        public bool IsValid => Issues.All(
            issue => issue.Severity != TrainingScenarioIssueSeverityV1.Error);
    }

    public static class TrainingScenarioValidatorV1
    {
        private const float PlayerBoundaryClearance =
            PrototypePlayerAgent.BoundaryClearance;
        private const float MinimumPlayerRootSeparation = .32f;
        private const float BallPlayerRootClearance =
            SimulatedBall.DefaultRadius + .18f;
        private const float MaximumBallX = CourtBuilder.HalfWidth + 1f;
        private const float MaximumBallZ = CourtBuilder.FormalHalfLength + 1f;
        private const float MaximumBallY = 12f;

        public static TrainingScenarioValidationResultV1 Validate(
            TrainingScenarioDraftV1 draft)
        {
            var issues = new List<TrainingScenarioIssueV1>();
            if (draft == null)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.MissingDraft,
                    string.Empty,
                    string.Empty,
                    "A training scenario draft is required.");
                return new TrainingScenarioValidationResultV1(issues);
            }

            ValidateIdentity(draft, issues);
            ValidateContextAndConfiguration(draft, issues);
            ValidateTactics(draft, issues);
            ValidatePlayers(draft, issues);
            ValidateBall(draft, issues);
            ValidateRallyStart(draft, issues);

            if (draft.AccessLevel != TrainingScenarioAccessLevelV1.Developer)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidAccess,
                    draft.ScenarioId,
                    "accessLevel",
                    "Only Developer access is enabled in TrainingScenarioV1.");
            }

            return new TrainingScenarioValidationResultV1(issues);
        }

        public static TrainingScenarioV1 Build(
            TrainingScenarioDraftV1 draft,
            string suppliedContentHash = null)
        {
            var result = Validate(draft);
            if (!result.IsValid)
            {
                throw new ArgumentException(
                    "Training scenario is invalid: " +
                    string.Join(
                        ", ",
                        result.Issues
                            .Where(issue =>
                                issue.Severity == TrainingScenarioIssueSeverityV1.Error)
                            .Select(issue => issue.Code)
                            .Distinct()),
                    nameof(draft));
            }

            var eligibility = CreateEligibility(draft);
            var startState = RallyStartStateV3Factory.Create(
                new RallyStartRequestV3(
                    draft.StartRecipe,
                    draft.SourceTeam,
                    draft.LastLegalActor),
                eligibility);
            var players = draft.Players
                .OrderBy(player => player.PlayerId.Value, StringComparer.Ordinal)
                .Select(player => new TrainingPlayerPoseV1(
                    player.PlayerId,
                    player.Position,
                    player.Forward,
                    player.Pose))
                .ToArray();

            return new TrainingScenarioV1(
                draft.ScenarioId,
                draft.DisplayName,
                draft.Source,
                draft.Context,
                draft.FirstServingSide,
                draft.HomeInitialRotationOffset,
                draft.AwayInitialRotationOffset,
                new TrainingTeamTacticV1(draft.HomeTactics),
                new TrainingTeamTacticV1(draft.AwayTactics),
                new TrainingAiConfigurationV1(draft.Ai),
                players,
                draft.BallPosition,
                draft.BallVelocity,
                startState,
                draft.AccessLevel,
                suppliedContentHash);
        }

        internal static OnCourtEligibilitySnapshot CreateEligibility(
            TrainingScenarioDraftV1 draft)
        {
            var set = new MatchSet(
                draft.Context,
                draft.FirstServingSide,
                MatchSetRules.FormalIndoor,
                draft.HomeInitialRotationOffset,
                draft.AwayInitialRotationOffset);
            var home = RotationFor(set, TeamSide.Home);
            var away = RotationFor(set, TeamSide.Away);
            return OnCourtLineupRulesV3.Create(
                draft.Context,
                home,
                away,
                set.ServerFor(TeamSide.Home),
                set.ServerFor(TeamSide.Away),
                Array.Empty<LiberoReplacementV3>());
        }

        private static StablePlayerId[] RotationFor(MatchSet set, TeamSide side)
        {
            var result = new StablePlayerId[6];
            for (var position = 1; position <= result.Length; position++)
            {
                result[position - 1] = set.PlayerAtRotationPosition(side, position);
            }

            return result;
        }

        private static void ValidateIdentity(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            if (draft.FormatVersion != TrainingScenarioV1.FormatVersion)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.UnsupportedFormat,
                    draft.ScenarioId,
                    "formatVersion",
                    "Training scenario format is not supported.");
            }

            if (string.IsNullOrWhiteSpace(draft.ScenarioId) ||
                !draft.ScenarioId.StartsWith(
                    TrainingScenarioV1.ScenarioIdPrefix,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidIdentity,
                    draft.ScenarioId,
                    "scenarioId",
                    "Scenario ID must use the training-v1 namespace.");
            }

            if (string.IsNullOrWhiteSpace(draft.DisplayName) ||
                string.IsNullOrWhiteSpace(draft.Source))
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidIdentity,
                    draft.ScenarioId,
                    string.IsNullOrWhiteSpace(draft.DisplayName)
                        ? "displayName"
                        : "source",
                    "Display name and source are required.");
            }
        }

        private static void ValidateContextAndConfiguration(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            if (draft.Context == null ||
                draft.Context.RulesVersion != RulesVersions.FullRallyV3 ||
                draft.Context.Home?.Players?.Count != 6 ||
                draft.Context.Away?.Players?.Count != 6)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidContext,
                    draft.ScenarioId,
                    "context",
                    "A complete formal Full Rally V3 context is required.");
            }

            if (!Enum.IsDefined(typeof(TeamSide), draft.FirstServingSide) ||
                draft.HomeInitialRotationOffset < 0 ||
                draft.HomeInitialRotationOffset >= 6 ||
                draft.AwayInitialRotationOffset < 0 ||
                draft.AwayInitialRotationOffset >= 6)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidRotation,
                    draft.ScenarioId,
                    "rotation",
                    "Serving side and both rotation offsets must be valid.");
            }
        }

        private static void ValidateTactics(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            ValidateTactic(draft.HomeTactics, "homeTactics", draft.ScenarioId, issues);
            ValidateTactic(draft.AwayTactics, "awayTactics", draft.ScenarioId, issues);
            try
            {
                if (draft.Ai == null)
                {
                    throw new ArgumentNullException(nameof(draft.Ai));
                }

                draft.Ai.Validate(nameof(draft.Ai));
            }
            catch (Exception)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidAi,
                    draft.ScenarioId,
                    "ai",
                    "Complete finite AI weights are required.");
            }
        }

        private static void ValidateTactic(
            FormalMatchTacticInputV4 tactic,
            string path,
            string objectId,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            try
            {
                if (tactic == null)
                {
                    throw new ArgumentNullException(nameof(tactic));
                }

                tactic.Validate(path);
            }
            catch (Exception)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidTactics,
                    objectId,
                    path,
                    "Complete finite formal tactics are required.");
            }
        }

        private static void ValidatePlayers(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            var expected = new HashSet<StablePlayerId>();
            if (draft.Context != null)
            {
                AddExpected(draft.Context.Home, expected);
                AddExpected(draft.Context.Away, expected);
            }

            var seen = new HashSet<StablePlayerId>();
            for (var index = 0; index < draft.Players.Count; index++)
            {
                var pose = draft.Players[index];
                var path = "players[" + index + "]";
                if (pose == null || string.IsNullOrWhiteSpace(pose.PlayerId.Value))
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.InvalidPlayerPose,
                        string.Empty,
                        path,
                        "A stable player pose is required.");
                    continue;
                }

                if (!seen.Add(pose.PlayerId))
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.DuplicatePlayer,
                        pose.PlayerId.Value,
                        path + ".playerId",
                        "Player poses must contain each on-court player once.");
                }

                if (expected.Count > 0 && !expected.Contains(pose.PlayerId))
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.UnexpectedPlayer,
                        pose.PlayerId.Value,
                        path + ".playerId",
                        "Player is not part of the match context.");
                }

                if (!pose.Position.IsFinite || !pose.Forward.IsFinite)
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.NonFiniteValue,
                        pose.PlayerId.Value,
                        path,
                        "Player position and forward vectors must be finite.");
                    continue;
                }

                if (Math.Abs(pose.Position.X) >
                    CourtBuilder.HalfWidth - PlayerBoundaryClearance ||
                    Math.Abs(pose.Position.Z) >
                    CourtBuilder.FormalHalfLength - PlayerBoundaryClearance ||
                    Math.Abs(pose.Position.Y) > .05f)
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.PlayerOutOfBounds,
                        pose.PlayerId.Value,
                        path + ".position",
                        "Player root must be on the formal playing surface.");
                }

                if (pose.Forward.SqrMagnitude < .25f ||
                    !Enum.IsDefined(typeof(StickFigurePose), pose.Pose))
                {
                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.InvalidPlayerPose,
                        pose.PlayerId.Value,
                        path,
                        "Player forward and pose must be supported.");
                }
            }

            foreach (var missing in expected
                         .Where(playerId => !seen.Contains(playerId))
                         .OrderBy(playerId => playerId.Value, StringComparer.Ordinal))
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.MissingPlayer,
                    missing.Value,
                    "players",
                    "A context player is missing from the spatial snapshot.");
            }

            ValidatePlayerOverlaps(draft.Players, issues);
        }

        private static void AddExpected(
            TeamSnapshotV4 team,
            ISet<StablePlayerId> expected)
        {
            if (team?.Players == null)
            {
                return;
            }

            for (var index = 0; index < team.Players.Count; index++)
            {
                expected.Add(team.Players[index].PlayerId);
            }
        }

        private static void ValidatePlayerOverlaps(
            IReadOnlyList<TrainingPlayerPoseDraftV1> players,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            var minimumSquared =
                MinimumPlayerRootSeparation * MinimumPlayerRootSeparation;
            for (var first = 0; first < players.Count; first++)
            {
                if (players[first] == null || !players[first].Position.IsFinite)
                {
                    continue;
                }

                for (var second = first + 1; second < players.Count; second++)
                {
                    if (players[second] == null || !players[second].Position.IsFinite)
                    {
                        continue;
                    }

                    var dx = players[first].Position.X - players[second].Position.X;
                    var dz = players[first].Position.Z - players[second].Position.Z;
                    if ((dx * dx) + (dz * dz) >= minimumSquared)
                    {
                        continue;
                    }

                    Add(
                        issues,
                        TrainingScenarioIssueCodesV1.PlayerOverlap,
                        players[first].PlayerId.Value,
                        "players[" + first + "].position",
                        "Player roots begin inside the formal separation limit.");
                }
            }
        }

        private static void ValidateBall(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            if (!draft.BallPosition.IsFinite || !draft.BallVelocity.IsFinite)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.NonFiniteValue,
                    "ball",
                    "ball",
                    "Ball position and linear velocity must be finite.");
            }

            if (draft.BallPosition.IsFinite &&
                (Math.Abs(draft.BallPosition.X) > MaximumBallX ||
                Math.Abs(draft.BallPosition.Z) > MaximumBallZ ||
                draft.BallPosition.Y < SimulatedBall.DefaultRadius ||
                draft.BallPosition.Y > MaximumBallY))
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.BallOutOfBounds,
                    "ball",
                    "ball.position",
                    "Ball must begin inside the training editor volume.");
            }

            if (!draft.BallPosition.IsFinite)
            {
                return;
            }

            var minimumSquared = BallPlayerRootClearance * BallPlayerRootClearance;
            for (var index = 0; index < draft.Players.Count; index++)
            {
                var player = draft.Players[index];
                if (player == null || !player.Position.IsFinite)
                {
                    continue;
                }

                if ((draft.BallPosition - player.Position).SqrMagnitude >= minimumSquared)
                {
                    continue;
                }

                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.BallPlayerOverlap,
                    player.PlayerId.Value,
                    "ball.position",
                    "Ball begins inside a player contact root.");
            }
        }

        private static void ValidateRallyStart(
            TrainingScenarioDraftV1 draft,
            ICollection<TrainingScenarioIssueV1> issues)
        {
            if (draft.Context == null ||
                !Enum.IsDefined(typeof(TeamSide), draft.FirstServingSide) ||
                draft.HomeInitialRotationOffset < 0 ||
                draft.HomeInitialRotationOffset >= 6 ||
                draft.AwayInitialRotationOffset < 0 ||
                draft.AwayInitialRotationOffset >= 6)
            {
                return;
            }

            try
            {
                var eligibility = CreateEligibility(draft);
                RallyStartStateV3Factory.Create(
                    new RallyStartRequestV3(
                        draft.StartRecipe,
                        draft.SourceTeam,
                        draft.LastLegalActor),
                    eligibility);
            }
            catch (Exception exception)
            {
                Add(
                    issues,
                    TrainingScenarioIssueCodesV1.InvalidRallyStart,
                    draft.LastLegalActor?.Value ?? string.Empty,
                    "rallyStart",
                    exception.Message);
            }
        }

        private static void Add(
            ICollection<TrainingScenarioIssueV1> issues,
            string code,
            string objectId,
            string propertyPath,
            string message)
        {
            issues.Add(new TrainingScenarioIssueV1(
                code,
                objectId,
                propertyPath,
                message,
                TrainingScenarioIssueSeverityV1.Error));
        }
    }
}
