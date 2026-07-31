using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingScenarioAccessLevelV1
    {
        Developer,
        Player
    }

    public sealed class TrainingPlayerPoseV1
    {
        public TrainingPlayerPoseV1(
            StablePlayerId playerId,
            SimVector3 position,
            SimVector3 forward,
            StickFigurePose pose)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
            {
                throw new ArgumentException("A stable player ID is required.", nameof(playerId));
            }

            if (!position.IsFinite || !forward.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    "Player pose vectors must be finite.");
            }

            if (!Enum.IsDefined(typeof(StickFigurePose), pose))
            {
                throw new ArgumentOutOfRangeException(nameof(pose));
            }

            PlayerId = playerId;
            Position = position;
            Forward = forward;
            Pose = pose;
        }

        public StablePlayerId PlayerId { get; }

        public SimVector3 Position { get; }

        public SimVector3 Forward { get; }

        public StickFigurePose Pose { get; }
    }

    public sealed class TrainingTeamTacticV1
    {
        public TrainingTeamTacticV1(FormalMatchTacticInputV4 source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Validate(nameof(source));
            SetRoute = source.SetRoute;
            SpikeRoute = source.SpikeRoute;
            SetterX = source.SetterX;
            SetterZ = source.SetterZ;
            AttackerX = source.AttackerX;
            AttackerZ = source.AttackerZ;
            DefenderX = source.DefenderX;
            DefenderZ = source.DefenderZ;
            Blocker = source.Blocker;
            BlockX = source.BlockX;
            BlockZ = source.BlockZ;
            CoverReceiver = source.CoverReceiver;
            CoverX = source.CoverX;
            CoverZ = source.CoverZ;
            SetRhythm = source.SetRhythm;
            AttackFlightSeconds = source.AttackFlightSeconds;
        }

        public SetRoute SetRoute { get; }
        public SpikeRoute SpikeRoute { get; }
        public float SetterX { get; }
        public float SetterZ { get; }
        public float AttackerX { get; }
        public float AttackerZ { get; }
        public float DefenderX { get; }
        public float DefenderZ { get; }
        public PlayerRole Blocker { get; }
        public float BlockX { get; }
        public float BlockZ { get; }
        public PlayerRole CoverReceiver { get; }
        public float CoverX { get; }
        public float CoverZ { get; }
        public SetRhythm SetRhythm { get; }
        public float AttackFlightSeconds { get; }

        public TeamRallyTactic ToRuntime()
        {
            return ToInput().ToRuntime();
        }

        internal FormalMatchTacticInputV4 ToInput()
        {
            return new FormalMatchTacticInputV4
            {
                SetRoute = SetRoute,
                SpikeRoute = SpikeRoute,
                SetterX = SetterX,
                SetterZ = SetterZ,
                AttackerX = AttackerX,
                AttackerZ = AttackerZ,
                DefenderX = DefenderX,
                DefenderZ = DefenderZ,
                Blocker = Blocker,
                BlockX = BlockX,
                BlockZ = BlockZ,
                CoverReceiver = CoverReceiver,
                CoverX = CoverX,
                CoverZ = CoverZ,
                SetRhythm = SetRhythm,
                AttackFlightSeconds = AttackFlightSeconds
            };
        }
    }

    public sealed class TrainingAiConfigurationV1
    {
        public TrainingAiConfigurationV1(FormalMatchAiInputV4 source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Validate(nameof(source));
            RolePreference = source.RolePreference;
            Reachability = source.Reachability;
            ApproachDistance = source.ApproachDistance;
            DirectionTolerance = source.DirectionTolerance;
        }

        public float RolePreference { get; }
        public float Reachability { get; }
        public float ApproachDistance { get; }
        public float DirectionTolerance { get; }

        public RallyTacticalWeights ToRuntime()
        {
            return ToInput().ToRuntime();
        }

        internal FormalMatchAiInputV4 ToInput()
        {
            return new FormalMatchAiInputV4
            {
                RolePreference = RolePreference,
                Reachability = Reachability,
                ApproachDistance = ApproachDistance,
                DirectionTolerance = DirectionTolerance
            };
        }
    }

    public sealed class TrainingScenarioV1
    {
        public const int FormatVersion = 1;
        public const string ScenarioIdPrefix = "training-v1/";

        private readonly IReadOnlyList<TrainingPlayerPoseV1> _players;

        internal TrainingScenarioV1(
            string scenarioId,
            string displayName,
            string source,
            MatchContextV4 context,
            TeamSide firstServingSide,
            int homeInitialRotationOffset,
            int awayInitialRotationOffset,
            TrainingTeamTacticV1 homeTactics,
            TrainingTeamTacticV1 awayTactics,
            TrainingAiConfigurationV1 ai,
            IReadOnlyList<TrainingPlayerPoseV1> players,
            SimVector3 ballPosition,
            SimVector3 ballVelocity,
            RallyStartStateV3 startState,
            TrainingScenarioAccessLevelV1 accessLevel,
            string suppliedContentHash)
        {
            ScenarioId = scenarioId;
            DisplayName = displayName;
            Source = source;
            FormatVersionValue = FormatVersion;
            Context = context;
            FirstServingSide = firstServingSide;
            HomeInitialRotationOffset = homeInitialRotationOffset;
            AwayInitialRotationOffset = awayInitialRotationOffset;
            HomeTactics = homeTactics;
            AwayTactics = awayTactics;
            Ai = ai;
            _players = new ReadOnlyCollection<TrainingPlayerPoseV1>(
                CopyPlayers(players));
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            StartState = startState;
            AccessLevel = accessLevel;
            ContentHash = TrainingScenarioCanonicalizerV1.ComputeContentHash(this);
            if (!string.IsNullOrWhiteSpace(suppliedContentHash) &&
                !string.Equals(
                    suppliedContentHash,
                    ContentHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Training scenario content hash does not match its complete input.",
                    nameof(suppliedContentHash));
            }
        }

        public string ScenarioId { get; }
        public string DisplayName { get; }
        public string Source { get; }
        public int FormatVersionValue { get; }
        public MatchContextV4 Context { get; }
        public TeamSide FirstServingSide { get; }
        public int HomeInitialRotationOffset { get; }
        public int AwayInitialRotationOffset { get; }
        public TrainingTeamTacticV1 HomeTactics { get; }
        public TrainingTeamTacticV1 AwayTactics { get; }
        public TrainingAiConfigurationV1 Ai { get; }
        public IReadOnlyList<TrainingPlayerPoseV1> Players => _players;
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public RallyStartStateV3 StartState { get; }
        public TrainingScenarioAccessLevelV1 AccessLevel { get; }
        public string ContentHash { get; }

        public PhysicalRallyTactics CreateTactics()
        {
            return new PhysicalRallyTactics(
                HomeTactics.ToRuntime(),
                AwayTactics.ToRuntime());
        }

        private static TrainingPlayerPoseV1[] CopyPlayers(
            IReadOnlyList<TrainingPlayerPoseV1> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            var copy = new TrainingPlayerPoseV1[players.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = players[index] ??
                              throw new ArgumentException(
                                  "Training player poses cannot contain null.",
                                  nameof(players));
            }

            return copy;
        }
    }
}
