using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation.TrainingLab;

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
        public void SelectServeTool_RejectsPositionFaultUntilItIsCleared()
        {
            using var controller = new TrainingScenarioLabController(
                Store(), new FakeSimulation());
            var homeSlotFour = controller.Draft.HomeRotation[3];
            controller.SetPlayerPosition(homeSlotFour,
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
                TeamId.Blue,
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
