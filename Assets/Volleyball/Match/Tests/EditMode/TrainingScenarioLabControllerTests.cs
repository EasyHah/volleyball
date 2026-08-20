using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
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
            Assert.That(controller.SelectedObjectId,
                Is.EqualTo(setup.HomeRotation[0].Value));
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

        [Test]
        public void V5ServeTopAndSide_EditOneSharedSixAxisDraft()
        {
            var setup = V5Setup();
            var controller = EnterV5Serve(setup);

            Assert.That(controller.TrySetBallFromTop(1.2f, -10.4f), Is.True);
            controller.SetServeTool(TrainingServeToolV1.AdjustVelocity);
            Assert.That(controller.TrySetVelocityFromTop(2.3f, 11.4f),
                Is.True);
            controller.SetServeView(TrainingServeViewV1.Side);
            controller.SetServeTool(TrainingServeToolV1.MoveBall);
            Assert.That(controller.TrySetBallFromSide(-10.6f, 2.8f), Is.True);
            controller.SetServeTool(TrainingServeToolV1.AdjustVelocity);
            Assert.That(controller.TrySetVelocityFromSide(12.5f, 3.6f),
                Is.True);

            Assert.That(setup.BallPosition,
                Is.EqualTo(new SimVector3(1.2f, 2.8f, -10.6f)));
            Assert.That(setup.BallVelocity,
                Is.EqualTo(new SimVector3(2.3f, 3.6f, 12.5f)));
        }

        [Test]
        public void V5ServeSideChangeMovesBandAndRejectedValueRestoresDraft()
        {
            var setup = V5Setup();
            var controller = EnterV5Serve(setup);
            controller.TrySetBallFromTop(1.1f, -10.2f);
            controller.SetFirstServingSide(TeamSide.Away);

            Assert.That(setup.BallPosition,
                Is.EqualTo(new SimVector3(1.1f, 2.2f, 10.2f)));
            controller.SetServeView(TrainingServeViewV1.Side);
            var before = setup.BallPosition;
            Assert.That(controller.TrySetBallFromSide(float.NaN, 2f),
                Is.False);
            Assert.That(setup.BallPosition, Is.EqualTo(before));
            Assert.That(controller.LastEditFailure, Is.Not.Empty);
        }

        [Test]
        public void V5TrajectoryAndReadonly3DUseFrozenMatchSetupWithoutMutation()
        {
            var setup = V5Setup();
            var controller = EnterV5Serve(setup);
            var trajectory = controller.PredictTrajectory(2);
            var replay = new BallState(setup.BallPosition,
                setup.BallVelocity, SimulatedBall.DefaultRadius);
            BallIntegrator.Step(replay, SimulatedBall.DefaultFixedStep,
                new BallSimulationParameters(-9.8f, .9995f));
            Assert.That(trajectory[1], Is.EqualTo(replay.Position));

            var frozen = new MatchSetupEditorV1(setup).Freeze();
            var preview = new TrainingLab3DPreviewWindowV1(frozen,
                TrainingServeViewV1.Side);
            preview.Orbit(20f, -5f);
            preview.Zoom(-3f);
            preview.SaveBookmark("观察");
            preview.ResetCamera();
            preview.LoadBookmark("观察");

            Assert.That(preview.Close(), Is.EqualTo(TrainingServeViewV1.Side));
            Assert.That(new MatchSetupEditorV1(setup).Freeze().SetupHash,
                Is.EqualTo(frozen.SetupHash));
        }

        [Test]
        public void V5AdministratorOverrides_EditClearResetFreezeAndReedit()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            var player = setup.HomeRotation[0];
            var baseJson = ContractJson.SerializeV5(setup.BaseContext);
            var initialHash = new MatchSetupEditorV1(setup).Freeze().SetupHash;
            foreach (TrainingPlayerAttributeFieldV2 field in Enum.GetValues(
                         typeof(TrainingPlayerAttributeFieldV2)))
            {
                if (field == TrainingPlayerAttributeFieldV2.DominantHand)
                    continue;
                controller.SetPlayerAttributeOverride(player, field,
                    field == TrainingPlayerAttributeFieldV2.Height
                        ? 2210
                        : 8765);
            }
            controller.SetPlayerDominantHandOverride(player,
                DominantHandV5.Left);

            var effective = controller.EffectiveBases(player);
            Assert.That(effective.Strength, Is.EqualTo(8765));
            Assert.That(effective.HeightMillimeters, Is.EqualTo(2210));
            Assert.That(effective.Jump, Is.EqualTo(8765));
            Assert.That(effective.Movement, Is.EqualTo(8765));
            Assert.That(effective.Reaction, Is.EqualTo(8765));
            Assert.That(effective.Coordination, Is.EqualTo(8765));
            Assert.That(effective.Attack, Is.EqualTo(8765));
            Assert.That(effective.Defense, Is.EqualTo(8765));
            Assert.That(effective.CourtIq, Is.EqualTo(8765));
            Assert.That(effective.Block, Is.EqualTo(8765));
            Assert.That(effective.Serve, Is.EqualTo(8765));
            Assert.That(effective.Set, Is.EqualTo(8765));
            Assert.That(controller.EffectiveDominantHand(player),
                Is.EqualTo(DominantHandV5.Left));
            Assert.That(new MatchSetupEditorV1(setup).Freeze().SetupHash,
                Is.Not.EqualTo(initialHash));

            controller.ClearPlayerAttributeOverride(player,
                TrainingPlayerAttributeFieldV2.Attack);
            Assert.That(setup.AttributeOverrides[player].Attack, Is.Null);
            controller.ResetPlayerAttributeOverrides(player);
            Assert.That(setup.AttributeOverrides.ContainsKey(player), Is.False);
            Assert.That(new MatchSetupEditorV1(setup).Freeze().SetupHash,
                Is.EqualTo(initialHash));
            controller.SetPlayerAttributeOverride(player,
                TrainingPlayerAttributeFieldV2.Attack, 4321);
            Assert.That(controller.EffectiveBases(player).Attack,
                Is.EqualTo(4321));
            Assert.That(ContractJson.SerializeV5(setup.BaseContext),
                Is.EqualTo(baseJson));
        }

        [Test]
        public void V5OverrideInspector_RendersAndWritesAllCanonicalFields()
        {
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            var player = setup.HomeRotation[0];
            var grid = new VisualElement();

            TrainingLabV5OverrideInspectorV2.Render(
                grid, controller, player);

            Assert.That(grid.childCount, Is.EqualTo(13));
            var attack = grid.Q<IntegerField>("v5-override-attack-input");
            Assert.That(attack, Is.Not.Null);
            controller.SetPlayerAttributeOverride(player,
                TrainingPlayerAttributeFieldV2.Attack, 9123);
            Assert.That(controller.EffectiveBases(player).Attack,
                Is.EqualTo(9123));
            TrainingLabV5OverrideInspectorV2.Render(
                grid, controller, player);
            Assert.That(grid.Q("v5-override-attack")
                .ClassListContains("explicit-override"), Is.True);
        }

        [Test]
        public void V5AutomaticPreflight_PassesExactFrozenSnapshotAndRerunsIt()
        {
            var runtime = new FakeV5Runtime();
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(
                setup, runtime);
            controller.ConfirmRotation();
            controller.ContinueToServeSetup();

            Assert.That(controller.EnterPreflight(), Is.True);
            var frozen = controller.PreflightSnapshot;
            Assert.That(frozen, Is.Not.Null);
            Assert.That(controller.Run(), Is.True);
            Assert.That(runtime.Starts, Has.Count.EqualTo(1));
            Assert.That(runtime.Starts[0], Is.SameAs(frozen));
            Assert.That(controller.RunSnapshot, Is.SameAs(frozen));
            Assert.That(() => controller.SetPlayerAttributeOverride(
                    setup.HomeRotation[0],
                    TrainingPlayerAttributeFieldV2.Attack, 1000),
                Throws.InvalidOperationException);

            runtime.Complete(frozen);
            Assert.That(controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Completed));
            controller.RerunSameSnapshot();
            Assert.That(runtime.Starts, Has.Count.EqualTo(2));
            Assert.That(runtime.Starts[1], Is.SameAs(frozen));
        }

        [Test]
        public void V5RestoredValidationPage_RecreatesAutomaticPreflight()
        {
            var setup = V5Setup();
            setup.RotationLocked = true;
            var local = TrainingLabLocalScenarioV2.Create("restored-validation",
                "Restored Validation", setup, "Validation", "Top",
                "MoveBall", "ball");
            using var controller = new TrainingLabWorkbenchControllerV2(
                new TrainingLabLocalScenarioRepositoryV2(Path.Combine(
                    Path.GetTempPath(), "training-v5-preflight-" +
                    Guid.NewGuid().ToString("N"))), local);

            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Validation));
            Assert.That(controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Ready));
            Assert.That(controller.PreflightSnapshot, Is.Not.Null);
        }

        [Test]
        public void V5EditAfterPreflight_InvalidatesAndRefreezesBeforeRun()
        {
            var runtime = new FakeV5Runtime();
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(setup,
                runtime);
            controller.ConfirmRotation();
            controller.ContinueToServeSetup();
            Assert.That(controller.EnterPreflight(), Is.True);
            var original = controller.PreflightSnapshot;

            controller.SetPlayerAttributeOverride(setup.HomeRotation[0],
                TrainingPlayerAttributeFieldV2.Attack, 1234);

            Assert.That(controller.PreflightSnapshot, Is.Null);
            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.ServeBall));
            Assert.That(controller.Run(), Is.True);
            Assert.That(runtime.Starts.Single().SetupHash,
                Is.Not.EqualTo(original.SetupHash));
        }

        [Test]
        public void V5InvalidAutomaticPreflight_DoesNotCreateRuntime()
        {
            var runtime = new FakeV5Runtime();
            var setup = V5Setup();
            var controller = new TrainingLabWorkbenchControllerV2(
                setup, runtime);
            setup.HomeRotation.RemoveAt(0);

            Assert.That(controller.EnterPreflight(), Is.False);
            Assert.That(controller.PreflightSnapshot, Is.Null);
            Assert.That(controller.PreflightError, Is.Not.Empty);
            Assert.That(runtime.Starts, Is.Empty);
        }

        [Test]
        public void V5DirtyLeave_SaveDiscardCancelAndRunningBlockUseLocalDto()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "training-v5-leave-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new TrainingLabLocalScenarioRepositoryV2(root);
                var setup = V5Setup();
                var local = TrainingLabLocalScenarioV2.Create("local-v5",
                    "Local V5", setup, "Rotation", "Top", "MoveBall",
                    "ball");
                var runtime = new FakeV5Runtime();
                using var controller = new TrainingLabWorkbenchControllerV2(
                    repository, local, runtime);

                Assert.That(controller.IsDirty, Is.True);
                Assert.That(controller.RequestLeaveToHub().RequiresDecision,
                    Is.True);
                Assert.That(controller.ResolveLeave(
                    TrainingLabLeaveDecisionV1.Cancel).CanLeave, Is.False);
                controller.RequestLeaveToHub();
                Assert.That(controller.ResolveLeave(
                    TrainingLabLeaveDecisionV1.Save).CanLeave, Is.True);
                Assert.That(controller.IsDirty, Is.False);

                var savedHash = repository.Load("local-v5").MatchSetupHash;
                controller.ConfirmRotation();
                Assert.That(controller.IsDirty, Is.True);
                controller.RequestLeaveToHub();
                controller.ResolveLeave(TrainingLabLeaveDecisionV1.Discard);
                Assert.That(controller.IsDirty, Is.False);
                Assert.That(controller.LocalScenario.MatchSetupHash,
                    Is.EqualTo(savedHash));

                controller.ConfirmRotation();
                controller.ContinueToServeSetup();
                Assert.That(controller.EnterPreflight(), Is.True);
                Assert.That(controller.Run(), Is.True);
                var blocked = controller.RequestLeaveToHub();
                Assert.That(blocked.IsBlocked, Is.True);
                Assert.That(blocked.RequiresDecision, Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
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

        private static TrainingLabWorkbenchControllerV2 EnterV5Serve(
            MatchSetupDraftV1 setup)
        {
            var controller = new TrainingLabWorkbenchControllerV2(setup);
            controller.ConfirmRotation();
            controller.ContinueToServeSetup();
            return controller;
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

        private sealed class FakeV5Runtime :
            ITrainingRallySimulationControllerV5
        {
            public event Action<TrainingRallyOutcomeV1> Completed;
            public event Action<string> Faulted;
            public readonly List<MatchSetupSnapshotV1> Starts =
                new List<MatchSetupSnapshotV1>();
            public bool HasRuntime { get; private set; }
            public bool IsPaused { get; private set; }
            public MatchSetupSnapshotV1 StartedSnapshot { get; private set; }

            public void Start(MatchSetupSnapshotV1 snapshot)
            {
                Starts.Add(snapshot);
                StartedSnapshot = snapshot;
                HasRuntime = true;
                IsPaused = false;
            }

            public void Pause() => IsPaused = true;
            public void Resume() => IsPaused = false;
            public void Step() { }
            public void Reset()
            {
                HasRuntime = false;
                IsPaused = false;
                StartedSnapshot = null;
            }

            public void Complete(MatchSetupSnapshotV1 snapshot)
            {
                Completed?.Invoke(new TrainingRallyOutcomeV1(
                    new TrainingRallyStartV5(snapshot), TeamSide.Home,
                    1, 0, "test", 1, null, null));
            }

            public void Fail(string message) => Faulted?.Invoke(message);
        }
    }
}
