using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    // Authoring catalog for project-owned training assets. Runtime startup loads
    // the generated presets; it never falls back to these authoring recipes.
    public static class TrainingScenarioCatalogV1
    {
        public const string SecondTouchNetOwnSide =
            "second-touch-net-own-side";
        public const string ThirdTouchNetOwnSide =
            "third-touch-net-own-side";
        public const string ThirdTouchNetCross =
            "third-touch-net-cross";
        public const string PostBlockNet =
            "post-block-net";
        public const string NetDeflectionSingleDispatch =
            "net-deflection-single-dispatch";
        public const string SecondTouchNetUnreachable =
            "second-touch-net-own-side-unreachable";

        public static IReadOnlyList<string> ScenarioIds { get; } =
            new[]
            {
                SecondTouchNetOwnSide,
                ThirdTouchNetOwnSide,
                ThirdTouchNetCross,
                PostBlockNet,
                NetDeflectionSingleDispatch,
                SecondTouchNetUnreachable
            };

        public static TrainingScenarioV1 Create(string scenarioId)
        {
            return TrainingScenarioValidatorV1.Build(CreateDraft(scenarioId));
        }

        public static TrainingScenarioDraftV1 CreateDraft(string scenarioId)
        {
            var context =
                FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            var draft = BaseDraft(scenarioId, context);
            switch (scenarioId)
            {
                case SecondTouchNetOwnSide:
                    Configure(
                        draft,
                        "Second touch: net rebound to own side",
                        RallyStartRecipeV3.AfterSet,
                        TeamSide.Home,
                        context.Home.Players[0].PlayerId,
                        new SimVector3(0f, 2f, -.42f),
                        new SimVector3(0f, 0f, 5f));
                    break;
                case ThirdTouchNetOwnSide:
                    Configure(
                        draft,
                        "Third touch: net rebound to own side",
                        RallyStartRecipeV3.AfterAttack,
                        TeamSide.Home,
                        context.Home.Players[1].PlayerId,
                        new SimVector3(0f, 2f, -.42f),
                        new SimVector3(0f, 0f, 5f));
                    break;
                case ThirdTouchNetCross:
                    Configure(
                        draft,
                        "Third touch: top tape deflection crosses",
                        RallyStartRecipeV3.AfterAttack,
                        TeamSide.Home,
                        context.Home.Players[1].PlayerId,
                        new SimVector3(0f, 2.72f, -.42f),
                        new SimVector3(0f, -1f, 5f));
                    break;
                case PostBlockNet:
                    Configure(
                        draft,
                        "Accepted block: rebound contacts net",
                        RallyStartRecipeV3.AfterAcceptedBlock,
                        TeamSide.Home,
                        context.Home.Players[1].PlayerId,
                        new SimVector3(0f, 2f, -.42f),
                        new SimVector3(0f, 0f, 5f));
                    break;
                case NetDeflectionSingleDispatch:
                    Configure(
                        draft,
                        "One flight: repeated net contacts dispatch once",
                        RallyStartRecipeV3.AfterReceive,
                        TeamSide.Home,
                        context.Home.Players[0].PlayerId,
                        new SimVector3(0f, 2.72f, -.15f),
                        new SimVector3(0f, -1f, .2f));
                    break;
                case SecondTouchNetUnreachable:
                    Configure(
                        draft,
                        "Second touch: net rebound is explicitly unreachable",
                        RallyStartRecipeV3.AfterSet,
                        TeamSide.Home,
                        context.Home.Players[0].PlayerId,
                        new SimVector3(0f, .52f, -.32f),
                        new SimVector3(0f, -1f, 10f));
                    MoveHomePlayersOutOfReach(draft, context.Home);
                    ResetToLegalServePoses(draft, draft.Context.Home, TeamSide.Home);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(scenarioId),
                        scenarioId,
                        "Unknown formal training scenario.");
            }

            return draft;
        }

        private static TrainingScenarioDraftV1 BaseDraft(
            string id,
            MatchContextV4 context)
        {
            var draft = new TrainingScenarioDraftV1
            {
                ScenarioId = TrainingScenarioV1.ScenarioIdPrefix + id,
                DisplayName = id,
                Source = "project-catalog",
                Context = context,
                FirstServingSide = TeamSide.Home,
                HomeTactics = Tactic(-1f),
                AwayTactics = Tactic(1f),
                Ai = new FormalMatchAiInputV4(),
                AccessLevel = TrainingScenarioAccessLevelV1.Developer
            };
            AddPoses(draft, context.Home, TeamSide.Home);
            AddPoses(draft, context.Away, TeamSide.Away);
            draft.HomeRotation.AddRange(context.Home.RotationOrder.Select(value => value.PlayerId));
            draft.AwayRotation.AddRange(context.Away.RotationOrder.Select(value => value.PlayerId));
            draft.RotationLocked = true;
            return draft;
        }

        private static void Configure(
            TrainingScenarioDraftV1 draft,
            string displayName,
            RallyStartRecipeV3 recipe,
            TeamSide sourceTeam,
            StablePlayerId lastActor,
            SimVector3 ballPosition,
            SimVector3 ballVelocity)
        {
            draft.DisplayName = displayName;
            // TrainingLab V2 always begins at an actual serve. The historical
            // net-continuation recipes remain catalog names only, not runtime injection points.
            draft.StartRecipe = RallyStartRecipeV3.ServeFlight;
            draft.SourceTeam = draft.FirstServingSide;
            draft.LastLegalActor = null;
            var sign = draft.FirstServingSide == TeamSide.Home ? -1f : 1f;
            draft.BallPosition = new SimVector3(0f, 2.2f,
                sign * (CourtBuilder.FormalHalfLength + .2f));
            draft.BallVelocity = new SimVector3(0f, 2.5f, -sign * 10f);
            ResetToLegalServePoses(draft, draft.Context.Home, TeamSide.Home);
            ResetToLegalServePoses(draft, draft.Context.Away, TeamSide.Away);
        }

        private static void ResetToLegalServePoses(TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 team, TeamSide side)
        {
            for (var index = 0; index < team.RotationOrder.Count; index++)
            {
                var player = draft.Players.Single(value =>
                    value.PlayerId.Equals(team.RotationOrder[index].PlayerId));
                var position = PhysicalMatchConfiguration.FormalIndoorSixVsSix
                    .PositionFor(side, index + 1);
                player.Position = new SimVector3(position.x, position.y, position.z);
            }
        }

        private static void AddPoses(
            TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 team,
            TeamSide side)
        {
            for (var index = 0; index < team.Players.Count; index++)
            {
                var position =
                    PhysicalMatchConfiguration.FormalIndoorSixVsSix
                        .PositionFor(side, index + 1);
                draft.Players.Add(new TrainingPlayerPoseDraftV1
                {
                    PlayerId = team.Players[index].PlayerId,
                    Position = new SimVector3(
                        position.x,
                        position.y,
                        position.z),
                    Forward = side == TeamSide.Home
                        ? new SimVector3(0f, 0f, 1f)
                        : new SimVector3(0f, 0f, -1f),
                    Pose = StickFigurePose.Ready
                });
            }
        }

        private static void MoveHomePlayersOutOfReach(
            TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 home)
        {
            var positions = new[]
            {
                new SimVector3(-4f, 0f, -8f),
                new SimVector3(4f, 0f, -8f),
                new SimVector3(-4f, 0f, -5f),
                new SimVector3(4f, 0f, -5f),
                new SimVector3(-4f, 0f, -2f),
                new SimVector3(4f, 0f, -2f)
            };
            for (var index = 0; index < home.Players.Count; index++)
            {
                var player = draft.Players.Find(value =>
                    value.PlayerId.Equals(home.Players[index].PlayerId));
                player.Position = positions[index];
            }
        }

        private static FormalMatchTacticInputV4 Tactic(float sideSign)
        {
            return new FormalMatchTacticInputV4
            {
                SetRoute = SetRoute.LeftPin,
                SpikeRoute = SpikeRoute.CrossCourt,
                SetterX = 0f,
                SetterZ = 2.5f * sideSign,
                AttackerX = 2.4f,
                AttackerZ = 1.8f * sideSign,
                DefenderX = -2.2f,
                DefenderZ = 5.4f * sideSign,
                Blocker = PlayerRole.OutsideHitter,
                BlockX = 2.2f,
                BlockZ = .55f * sideSign,
                CoverReceiver = PlayerRole.Setter,
                CoverX = 0f,
                CoverZ = 1.6f * sideSign,
                SetRhythm = SetRhythm.FastPin,
                AttackFlightSeconds = .45f
            };
        }
    }
}
