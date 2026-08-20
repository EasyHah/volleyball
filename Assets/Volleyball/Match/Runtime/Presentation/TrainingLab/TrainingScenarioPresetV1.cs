using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    [Serializable]
    public sealed class TrainingScenarioPresetPlayerPoseV1
    {
        [SerializeField] private string playerId;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 forward = Vector3.forward;
        [SerializeField] private StickFigurePose pose = StickFigurePose.Ready;

        public TrainingPlayerPoseDraftV1 ToDraft()
        {
            return new TrainingPlayerPoseDraftV1
            {
                PlayerId = new PlayerId(playerId),
                Position = ToSimulation(position),
                Forward = ToSimulation(forward),
                Pose = pose
            };
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }
    }

    [CreateAssetMenu(
        fileName = "TrainingScenarioV1",
        menuName = "Volleyball/Formal Training Scenario V1")]
    public sealed class TrainingScenarioPresetV1 : ScriptableObject
    {
        [SerializeField] private string scenarioId = "training-v1/new";
        [SerializeField] private string displayName = "New Training Scenario";
        [SerializeField] private string source = "project";
        [SerializeField] private int formatVersion = TrainingScenarioV1.FormatVersion;
        [TextArea(8, 30)] [SerializeField] private string matchContextJson;
        [SerializeField] private TeamSide firstServingSide = TeamSide.Home;
        [Range(0, 5)] [SerializeField] private int homeInitialRotationOffset;
        [Range(0, 5)] [SerializeField] private int awayInitialRotationOffset;
        [SerializeField] private FormalMatchTacticInputV4 homeTactics;
        [SerializeField] private FormalMatchTacticInputV4 awayTactics;
        [SerializeField] private FormalMatchAiInputV4 ai;
        [SerializeField] private List<TrainingScenarioPresetPlayerPoseV1> players =
            new List<TrainingScenarioPresetPlayerPoseV1>();
        [SerializeField] private Vector3 ballPosition = new Vector3(0f, 2f, -1f);
        [SerializeField] private Vector3 ballVelocity = new Vector3(0f, 0f, 6f);
        [SerializeField] private RallyStartRecipeV3 startRecipe =
            RallyStartRecipeV3.AfterSet;
        [SerializeField] private TeamSide sourceTeam = TeamSide.Home;
        [SerializeField] private string lastLegalActorId;
        [SerializeField] private TrainingScenarioAccessLevelV1 accessLevel =
            TrainingScenarioAccessLevelV1.Developer;
        [SerializeField] private string contentHash;

        public TrainingScenarioV1 ToDefinition()
        {
            if (string.IsNullOrWhiteSpace(contentHash))
            {
                throw new InvalidOperationException(
                    "Training scenario requires its canonical content hash.");
            }

            var legacyMidRallyAsset = startRecipe != RallyStartRecipeV3.ServeFlight;
            return TrainingScenarioValidatorV1.Build(CreateDraft(),
                legacyMidRallyAsset ? null : contentHash);
        }

        public TrainingScenarioDraftV1 CreateDraft()
        {
            if (string.IsNullOrWhiteSpace(matchContextJson))
            {
                throw new InvalidOperationException(
                    "Training scenario requires a complete canonical MatchContextV4 payload.");
            }

            var draft = new TrainingScenarioDraftV1
            {
                ScenarioId = scenarioId,
                DisplayName = displayName,
                Source = source,
                FormatVersion = formatVersion,
                Context = ContractJson.DeserializeMatchContextV4(matchContextJson),
                FirstServingSide = firstServingSide,
                HomeInitialRotationOffset = homeInitialRotationOffset,
                AwayInitialRotationOffset = awayInitialRotationOffset,
                HomeTactics = TrainingScenarioDraftV1.Clone(homeTactics),
                AwayTactics = TrainingScenarioDraftV1.Clone(awayTactics),
                Ai = TrainingScenarioDraftV1.Clone(ai),
                BallPosition = ToSimulation(ballPosition),
                BallVelocity = ToSimulation(ballVelocity),
                StartRecipe = startRecipe,
                SourceTeam = sourceTeam,
                LastLegalActor = string.IsNullOrWhiteSpace(lastLegalActorId)
                    ? null
                    : new PlayerId(lastLegalActorId),
                AccessLevel = accessLevel
            };
            for (var index = 0; index < players.Count; index++)
            {
                draft.Players.Add(players[index]?.ToDraft());
            }
            // V1 assets predate explicit rotations; their context order is the preserved legal order.
            draft.HomeRotation.AddRange(draft.Context.Home.RotationOrder.Select(value => value.PlayerId));
            draft.AwayRotation.AddRange(draft.Context.Away.RotationOrder.Select(value => value.PlayerId));
            draft.RotationLocked = true;
            // Existing assets encoded mid-rally recipes. Their authored player
            // layout is reset to a legal serve formation before serve-only startup.
            draft.Players.Clear();
            AddLegalServePoses(draft, draft.Context.Home, TeamSide.Home);
            AddLegalServePoses(draft, draft.Context.Away, TeamSide.Away);
            draft.StartRecipe = RallyStartRecipeV3.ServeFlight;
            draft.SourceTeam = draft.FirstServingSide;
            draft.LastLegalActor = null;
            var sign = draft.FirstServingSide == TeamSide.Home ? -1f : 1f;
            draft.BallPosition = new SimVector3(0f, 2.2f,
                sign * (CourtBuilder.FormalHalfLength + .2f));
            draft.BallVelocity = new SimVector3(0f, 2.5f, -sign * 10f);

            return draft;
        }

        [ContextMenu("Refresh Content Hash")]
        private void RefreshContentHash()
        {
            try
            {
                contentHash = TrainingScenarioValidatorV1.Build(CreateDraft()).ContentHash;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Training scenario hash could not be refreshed: " +
                    exception.Message,
                    this);
            }
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }

        private static void AddLegalServePoses(TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 team, TeamSide side)
        {
            for (var index = 0; index < team.RotationOrder.Count; index++)
            {
                var position = PhysicalMatchConfiguration.FormalIndoorSixVsSix
                    .PositionFor(side, index + 1);
                draft.Players.Add(new TrainingPlayerPoseDraftV1
                {
                    PlayerId = team.RotationOrder[index].PlayerId,
                    Position = new SimVector3(position.x, position.y, position.z),
                    Forward = side == TeamSide.Home
                        ? new SimVector3(0f, 0f, 1f)
                        : new SimVector3(0f, 0f, -1f),
                    Pose = StickFigurePose.Ready
                });
            }
        }
    }
}
