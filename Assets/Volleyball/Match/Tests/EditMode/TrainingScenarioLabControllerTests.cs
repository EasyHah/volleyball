using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingScenarioLabControllerTests
    {
        [Test]
        public void Store_LoadAndResetNeverMutateBuiltInDraft()
        {
            var store = Store();
            var first = store.Load("builtin:first");
            var original = first.BallPosition;
            first.BallPosition = new SimVector3(4f, 4f, 4f);

            Assert.That(
                store.Load("builtin:first").BallPosition,
                Is.EqualTo(original));
        }

        [Test]
        public void InvalidEdit_StaysEditingAndFocusesStableIssuePath()
        {
            var runtime = new FakeSimulation();
            using var controller =
                new TrainingScenarioLabController(Store(), runtime);
            controller.SetBallPosition(new SimVector3(float.NaN, 2f, 0f));

            Assert.That(controller.Validate(), Is.False);
            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Editing));
            var index = IndexOf(
                controller.Validation,
                TrainingScenarioIssueCodesV1.NonFiniteValue);
            controller.FocusIssue(index);
            Assert.That(controller.SelectedObjectId, Is.EqualTo("ball"));
            Assert.That(controller.SelectedPropertyPath,
                Is.EqualTo("ball"));
            Assert.That(runtime.Starts, Is.Empty);
        }

        [Test]
        public void Run_FreezesValidatedScenarioAndLocksEveryEditCommand()
        {
            var runtime = new FakeSimulation();
            using var controller =
                new TrainingScenarioLabController(Store(), runtime);
            var draftPosition = controller.Draft.BallPosition;

            Assert.That(controller.Validate(), Is.True);
            Assert.That(controller.Run(), Is.True);
            Assert.That(runtime.Starts, Has.Count.EqualTo(1));
            Assert.That(
                runtime.Starts[0].BallPosition,
                Is.EqualTo(draftPosition));
            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Running));
            Assert.That(
                () => controller.SetBallPosition(SimVector3.Zero),
                Throws.InvalidOperationException);
            Assert.That(
                () => controller.SelectDraftEntry("builtin:first"),
                Throws.InvalidOperationException);
        }

        [Test]
        public void RotationMustBeConfirmedBeforePositioningAndRun()
        {
            using var controller = new TrainingScenarioLabController(Store(), new FakeSimulation());

            controller.ReopenRotation();
            Assert.That(controller.CurrentStep, Is.EqualTo(TrainingLabStepV1.Rotation));
            Assert.That(controller.Validate(), Is.False);
            controller.ConfirmRotation();

            Assert.That(controller.CurrentStep, Is.EqualTo(TrainingLabStepV1.Positioning));
            controller.GoToPositioning();
            Assert.That(controller.CurrentStep, Is.EqualTo(TrainingLabStepV1.Positioning));
            controller.SelectServeTool(TrainingServeToolV1.AdjustVelocity);
            Assert.That(controller.ServeTool, Is.EqualTo(TrainingServeToolV1.AdjustVelocity));
            Assert.That(controller.CurrentStep, Is.EqualTo(TrainingLabStepV1.ServeBall));
        }

        [Test]
        public void PositionFault_BlocksOnlyEntryIntoServeSetup()
        {
            using var controller = new TrainingScenarioLabController(
                Store(), new FakeSimulation());
            var slotFour = controller.Draft.HomeRotation[3];
            controller.SetPlayerPosition(slotFour,
                new SimVector3(-3f, 0f, -7f));

            Assert.That(controller.PositionFaultPreview, Is.Not.Empty);
            Assert.That(controller.CanEnterServeSetup, Is.False);
            Assert.That(() => controller.SelectServeTool(
                    TrainingServeToolV1.MoveBall),
                Throws.InvalidOperationException.With.Message.Contains(
                    "position fault"));

            controller.ResetDraft();

            Assert.That(controller.CanEnterServeSetup, Is.True);
        }

        [Test]
        public void ServeSetup_ClampsTheBallToTheCurrentServingBand()
        {
            using var controller = new TrainingScenarioLabController(
                Store(), new FakeSimulation());
            controller.SelectServeTool(TrainingServeToolV1.MoveBall);
            controller.SetBallPosition(new SimVector3(8f, 2f, 4f));

            Assert.That(controller.Draft.BallPosition,
                Is.EqualTo(new SimVector3(4.5f, 2f, -9f)));
        }

        [Test]
        public void CameraBookmarks_StoreTheCurrentFreeCameraWithoutChangingMatchContext()
        {
            using var controller = new TrainingScenarioLabController(Store(), new FakeSimulation());
            var contextHash = controller.Draft.Context.ContextHash;

            controller.SaveCameraBookmark("观察机位",
                new SimVector3(2f, 8f, -12f),
                new SimVector3(0f, -.35f, 1f),
                7.5f, false);

            Assert.That(controller.Draft.CameraBookmarks, Has.Count.EqualTo(1));
            Assert.That(controller.Draft.CameraBookmarks[0].Position,
                Is.EqualTo(new SimVector3(2f, 8f, -12f)));
            Assert.That(controller.Draft.Context.ContextHash,
                Is.EqualTo(contextHash));
        }

        [Test]
        public void PauseStepResume_UsesOnlyTheRuntimeLifecycleSurface()
        {
            var runtime = new FakeSimulation();
            using var controller =
                new TrainingScenarioLabController(Store(), runtime);
            controller.Run();

            controller.Pause();
            controller.Step();
            controller.Resume();

            Assert.That(runtime.PauseCalls, Is.EqualTo(1));
            Assert.That(runtime.StepCalls, Is.EqualTo(1));
            Assert.That(runtime.ResumeCalls, Is.EqualTo(1));
            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Running));
        }

        [Test]
        public void SameSeedRerun_ReusesFrozenHashAndReportsExactAgreement()
        {
            var runtime = new FakeSimulation();
            using var controller =
                new TrainingScenarioLabController(Store(), runtime);
            controller.Run();
            runtime.RaiseCompleted(Evidence(runtime.Starts[0]));

            controller.RerunSameSeed();
            Assert.That(runtime.Starts, Has.Count.EqualTo(2));
            Assert.That(
                runtime.Starts[1].ContentHash,
                Is.EqualTo(runtime.Starts[0].ContentHash));
            Assert.That(
                runtime.Starts[1].Context.Seed,
                Is.EqualTo(runtime.Starts[0].Context.Seed));
            runtime.RaiseCompleted(Evidence(runtime.Starts[1]));

            Assert.That(
                controller.RunComparisonSummary,
                Is.EqualTo("同 seed 双跑一致"));
        }

        [Test]
        public void V5RotationDrop_SwapsOnlyCardsFromTheSameTeam()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            var homeBefore = setup.HomeRotation.ToArray();
            var awayBefore = setup.AwayRotation.ToArray();

            Assert.That(controller.TryDropRotationCard(
                TeamSide.Home, 1, TeamSide.Home, 4), Is.True);
            Assert.That(setup.HomeRotation[0], Is.EqualTo(homeBefore[3]));
            Assert.That(setup.HomeRotation[3], Is.EqualTo(homeBefore[0]));

            var afterSwap = setup.HomeRotation.ToArray();
            Assert.That(controller.TryDropRotationCard(
                TeamSide.Home, 1, TeamSide.Away, 1), Is.False);
            Assert.That(controller.TryDropRotationCard(
                TeamSide.Home, 1, null, null), Is.False);
            Assert.That(setup.HomeRotation, Is.EqualTo(afterSwap));
            Assert.That(setup.AwayRotation, Is.EqualTo(awayBefore));
        }

        [Test]
        public void V5RotationConfirmAndReopen_PreservePosesAndRecomputeFaults()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            var player = setup.HomeRotation[3];
            var editor = new MatchSetupEditorV1(setup);
            var moved = editor.SetPlayerPosition(player,
                new SimVector3(-3.5f, 0f, -7f));

            controller.ConfirmRotation();
            Assert.That(setup.RotationLocked, Is.True);
            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Positioning));
            Assert.That(controller.PositionFaults, Is.Not.Empty);

            controller.ReopenRotation();
            Assert.That(setup.RotationLocked, Is.False);
            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Rotation));
            Assert.That(setup.Players.Single(value =>
                value.PlayerId.Equals(player)).Position, Is.EqualTo(moved));

            controller.ConfirmRotation();
            Assert.That(controller.PositionFaults, Is.Not.Empty);
            Assert.That(() => controller.ExchangeRotation(
                    TeamSide.Home, 1, 2),
                Throws.InvalidOperationException);
        }

        [Test]
        public void V5Positioning_CourtAndRulerCommandsShareAxesAndSnap()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            controller.ConfirmRotation();
            var player = setup.AwayRotation[0];
            var board = new UnityEngine.Rect(0f, 0f, 900f, 450f);
            var fromCourt = controller.SetPlayerPositionFromCourt(
                player, board, new UnityEngine.Vector2(650f, 100f));
            controller.SetPlayerPosition(player,
                new SimVector3(fromCourt.X, 0f, 2f));

            var fromDepth = controller.SetPlayerDepthFromHorizontalRuler(
                player, board, 650f);
            Assert.That(fromDepth, Is.EqualTo(fromCourt));

            controller.SetPlayerPosition(player,
                new SimVector3(1f, 0f, fromCourt.Z));
            var fromLateral = controller.SetPlayerLateralFromVerticalRuler(
                player, board, 100f);
            Assert.That(fromLateral, Is.EqualTo(fromCourt));
        }

        [Test]
        public void V5PositionFaultFocus_SelectsExactlyParticipantsAndBlocksServe()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            controller.ConfirmRotation();
            var violating = setup.HomeRotation[3];
            controller.SetPlayerPosition(violating,
                new SimVector3(-3.5f, 0f, -7f));
            var fault = controller.PositionFaults[0];
            var hashBefore = new MatchSetupEditorV1(setup).Freeze().SetupHash;

            controller.FocusPositionFault(0);

            Assert.That(controller.FocusedPlayerIds,
                Is.EquivalentTo(new[]
                {
                    fault.RequiredAheadOrLeft.PlayerId,
                    fault.ViolatingBehindOrRight.PlayerId
                }));
            Assert.That(controller.SelectedObjectId,
                Is.EqualTo(fault.ViolatingBehindOrRight.PlayerId.Value));
            Assert.That(controller.CanEnterServeSetup, Is.False);
            Assert.That(controller.ServeSetupBlockReason,
                Does.Contain("all position faults"));
            Assert.That(() => controller.ContinueToServeSetup(),
                Throws.InvalidOperationException);
            Assert.That(new MatchSetupEditorV1(setup).Freeze().SetupHash,
                Is.EqualTo(hashBefore));
        }

        private static TrainingScenarioDraftStoreV1 Store()
        {
            return new TrainingScenarioDraftStoreV1(
                new Dictionary<string, TrainingScenarioDraftV1>
                {
                    ["builtin:first"] =
                        TrainingScenarioCatalogV1.CreateDraft(
                            TrainingScenarioCatalogV1.SecondTouchNetOwnSide),
                    ["builtin:second"] =
                        TrainingScenarioCatalogV1.CreateDraft(
                            TrainingScenarioCatalogV1.ThirdTouchNetCross)
                });
        }

        private static MatchSetupDraftV1 V5Setup()
        {
            return MatchSetupDraftV1.CreateDefault(
                FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5(),
                TeamSide.Home);
        }

        private static int IndexOf(
            TrainingScenarioValidationResultV1 result,
            string code)
        {
            for (var index = 0; index < result.Issues.Count; index++)
            {
                if (result.Issues[index].Code == code) return index;
            }

            return -1;
        }

        private static TrainingRunEvidenceV1 Evidence(
            TrainingScenarioV1 scenario)
        {
            return new TrainingRunEvidenceV1(
                scenario.ScenarioId,
                scenario.ContentHash,
                scenario.Context.Seed,
                Array.Empty<TrainingTimelineEventV1>(),
                Array.Empty<DecisionSnapshotV1>(),
                Array.Empty<SetterTargetSnapshotV1>(),
                true,
                Volleyball.Domain.Prototype.TeamId.Blue,
                "test-resolution");
        }

        private sealed class FakeSimulation :
            ITrainingSimulationControllerV1
        {
            public event Action<TrainingRunEvidenceV1> Completed;
            public event Action<string> Faulted;

            public readonly List<TrainingScenarioV1> Starts =
                new List<TrainingScenarioV1>();
            public int PauseCalls { get; private set; }
            public int ResumeCalls { get; private set; }
            public int StepCalls { get; private set; }
            public bool HasRuntime { get; private set; }
            public bool IsPaused { get; private set; }
            public TrainingRunEvidenceV1 CurrentEvidence { get; private set; }

            public void Start(TrainingScenarioV1 scenario)
            {
                Starts.Add(scenario);
                HasRuntime = true;
                IsPaused = false;
                CurrentEvidence = null;
            }

            public void Pause()
            {
                PauseCalls++;
                IsPaused = true;
            }

            public void Resume()
            {
                ResumeCalls++;
                IsPaused = false;
            }

            public void Step()
            {
                StepCalls++;
            }

            public void Reset()
            {
                HasRuntime = false;
                IsPaused = false;
                CurrentEvidence = null;
            }

            public void RaiseCompleted(TrainingRunEvidenceV1 evidence)
            {
                CurrentEvidence = evidence;
                Completed?.Invoke(evidence);
            }

            public void RaiseFault(string message)
            {
                Faulted?.Invoke(message);
            }
        }
    }
}
