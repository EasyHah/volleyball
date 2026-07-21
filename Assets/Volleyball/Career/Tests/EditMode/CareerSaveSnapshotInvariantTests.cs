using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveSnapshotInvariantTests
    {
        [Test]
        public void CurrentVersions_ContainOnlyFourPositiveCareerAxes()
        {
            var versions = CareerSaveVersions.Current;

            Assert.That(versions.SchemaVersion, Is.EqualTo(1));
            Assert.That(versions.ContentVersion, Is.EqualTo(1));
            Assert.That(versions.RulesetVersion, Is.EqualTo(1));
            Assert.That(versions.CareerRandomAlgorithmVersion, Is.EqualTo(1));
            Assert.That(typeof(CareerSaveVersions).GetProperty("ContractVersion"), Is.Null);
            Assert.That(typeof(CareerSaveVersions).GetProperty("MatchSimulationVersion"), Is.Null);
            Assert.That(
                () => new CareerSaveVersions(0, 1, 1, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerSaveVersions(1, 1, 1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Identity_ProducesCompleteVersionTokenAndRejectsInvalidRevisionOrTime()
        {
            var identity = CreateIdentity(revision: 3, createdAt: 10, updatedAt: 20);

            Assert.That(identity.VersionToken.LineageId, Is.EqualTo(identity.LineageId));
            Assert.That(identity.VersionToken.Revision, Is.EqualTo(3));
            Assert.That(identity.VersionToken.SnapshotHash, Is.EqualTo(identity.SnapshotHash));
            Assert.That(() => CreateIdentity(revision: 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CreateIdentity(revision: long.MaxValue),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CreateIdentity(createdAt: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CreateIdentity(updatedAt: long.MaxValue),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CreateIdentity(createdAt: 2, updatedAt: 1), Throws.ArgumentException);
        }

        [Test]
        public void Progression_HasNoPersistedExecutingSlotStateOrCommandFields()
        {
            Assert.That(
                (CareerProgressionKind[])Enum.GetValues(typeof(CareerProgressionKind)),
                Is.EqualTo(
                    new[]
                    {
                        CareerProgressionKind.CareerCreated,
                        CareerProgressionKind.Tryout,
                        CareerProgressionKind.Planning,
                        CareerProgressionKind.Planned,
                        CareerProgressionKind.AwaitingEventChoice
                    }));
            Assert.That(typeof(CareerProgressionState).GetProperty("SlotNumber"), Is.Null);
            Assert.That(typeof(CareerProgressionState).GetProperty("OperationId"), Is.Null);
            Assert.That(typeof(CareerProgressionState).GetMethod("ExecutingSlot"), Is.Null);
        }

        [Test]
        public void Snapshots_AcceptEveryAuthoritativeStateBeforeTheMatchSlot()
        {
            var created = CreateSnapshot(CareerProgressionState.Created(), false);
            var tryout = CreateSnapshot(CareerProgressionState.Tryout(2), false);
            var planning = CreateSnapshot(
                CareerProgressionState.Planning(CreateDraftPlan()),
                true);
            var planForSlotOne = CreateConfirmedPlan();
            var plannedOne = CreateSnapshot(
                CareerProgressionState.Planned(planForSlotOne, 1),
                true);
            var planForSlotTwo = CreateConfirmedPlan();
            var plannedTwo = CreateSnapshot(
                CareerProgressionState.Planned(planForSlotTwo, 2),
                true);
            var planForSlotThree = CreateConfirmedPlan();
            var plannedThree = CreateSnapshot(
                CareerProgressionState.Planned(planForSlotThree, 3),
                true);
            var awaitingPlan = CreateConfirmedPlan();
            var pending = CreatePendingEvent(awaitingPlan);
            var awaiting = CreateSnapshot(
                CareerProgressionState.AwaitingEventChoice(awaitingPlan, pending),
                true);

            Assert.That(created.HasCompletePlayer, Is.False);
            Assert.That(tryout.Onboarding.CurrentStageNumber, Is.EqualTo(2));
            Assert.That(planning.Progression.WeekPlan.IsConfirmed, Is.False);
            Assert.That(plannedOne.Progression.NextSlotNumber, Is.EqualTo(1));
            Assert.That(plannedTwo.Progression.NextSlotNumber, Is.EqualTo(2));
            Assert.That(plannedThree.Progression.NextSlotNumber, Is.EqualTo(3));
            Assert.That(awaiting.Progression.PendingEvent.ResumeAtSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void WeekPlanState_EnforcesSchemaV1MatchLayout()
        {
            var first = NewActionState(CareerWeekActionKind.Rest);
            var second = NewActionState(CareerWeekActionKind.TeamPractice);
            var match = NewActionState(CareerWeekActionKind.Match);

            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new[] { match, second, first },
                    false),
                Throws.ArgumentException.With.Message.Contains("slot 3"));
            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new[] { first, second, NewActionState(CareerWeekActionKind.Rest) },
                    true),
                Throws.ArgumentException.With.Message.Contains("only match"));
            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new CareerWeekActionState[] { first, null, match },
                    true),
                Throws.ArgumentException.With.Message.Contains("empty"));
            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new CareerWeekActionState[] { first, null, null },
                    false),
                Throws.ArgumentException.With.Message.Contains("match"));
            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new[] { first, second, NewActionState(CareerWeekActionKind.Rest) },
                    false),
                Throws.ArgumentException.With.Message.Contains("match"));

            var draft = new CareerWeekPlanState(
                NewWeekPlanId(),
                1,
                1,
                new CareerWeekActionState[] { null, null, match },
                false);
            Assert.That(draft.Slots[2].IsMatch, Is.True);
        }

        [Test]
        public void WeekPlanState_RejectsDuplicateActionAndOccurrenceIds()
        {
            var first = NewActionState(CareerWeekActionKind.Rest);
            var duplicateAction = new CareerWeekActionState(
                first.SlotActionId,
                NewOccurrenceId(),
                CareerWeekActionKind.TeamPractice);
            var duplicateOccurrence = new CareerWeekActionState(
                NewSlotActionId(),
                first.OccurrenceId,
                CareerWeekActionKind.StrengthTraining);

            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new[] { first, duplicateAction, NewActionState(CareerWeekActionKind.Match) },
                    false),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekPlanState(
                    NewWeekPlanId(),
                    1,
                    1,
                    new[] { first, duplicateOccurrence, NewActionState(CareerWeekActionKind.Match) },
                    false),
                Throws.ArgumentException);
        }

        [Test]
        public void Progression_RejectsInvalidTryoutPlanAndSlotCombinations()
        {
            Assert.That(
                () => CareerProgressionState.Tryout(0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerProgressionState.Tryout(4),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerProgressionState.Planning(CreateConfirmedPlan()),
                Throws.ArgumentException);
            Assert.That(
                () => CareerProgressionState.Planned(CreateDraftPlan(), 1),
                Throws.ArgumentException);
            Assert.That(
                () => CareerProgressionState.Planned(CreateConfirmedPlan(), 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerProgressionState.Planned(CreateConfirmedPlan(), 4),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerProgressionState.AwaitingEventChoice(CreateConfirmedPlan(), null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new CareerProgressionState(
                    (CareerProgressionKind)99,
                    CareerPhase.University,
                    0,
                    null,
                    0,
                    null),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var seasonThreeDraft = new CareerWeekPlanState(
                NewWeekPlanId(),
                3,
                1,
                new CareerWeekActionState[]
                {
                    null,
                    null,
                    NewActionState(CareerWeekActionKind.Match)
                },
                false);
            Assert.That(
                () => CareerProgressionState.Planning(seasonThreeDraft),
                Throws.ArgumentException);
        }

        [Test]
        public void TryoutStage_RequiresChoiceAndOrderedNonEmptyResolvedOutputTogether()
        {
            var occurrence = NewOccurrenceId();

            Assert.That(
                () => new TryoutStageState(
                    1,
                    occurrence,
                    1,
                    null,
                    new[] { new TryoutResolvedOutput("spike", 10) }),
                Throws.ArgumentException);
            Assert.That(
                () => new TryoutStageState(
                    1,
                    occurrence,
                    1,
                    "attack",
                    Array.Empty<TryoutResolvedOutput>()),
                Throws.ArgumentException);
            Assert.That(
                () => new TryoutResolvedOutput("spike", 101),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var stage = ConfirmedTryoutStage(1);
            Assert.That(stage.IsConfirmed, Is.True);
            Assert.That(stage.ResolvedOutputs[0].OutputId, Is.EqualTo("stage-1-primary"));
            Assert.That(stage.ResolvedOutputs[1].Perturbation, Is.EqualTo(-5));
        }

        [Test]
        public void TryoutOnboarding_EnforcesSequentialCurrentStageAndFormalEnrollment()
        {
            var stages = new[]
            {
                ConfirmedTryoutStage(1),
                UnconfirmedTryoutStage(2),
                UnconfirmedTryoutStage(3)
            };

            var active = new TryoutOnboardingState(stages, 2, false);
            Assert.That(active.CurrentStageNumber, Is.EqualTo(2));
            Assert.That(active.IsComplete, Is.False);

            Assert.That(
                () => new TryoutOnboardingState(stages, 3, false),
                Throws.ArgumentException);
            Assert.That(
                () => new TryoutOnboardingState(stages, 0, true),
                Throws.ArgumentException);
            Assert.That(
                () => new TryoutOnboardingState(
                    new[]
                    {
                        UnconfirmedTryoutStage(2),
                        UnconfirmedTryoutStage(1),
                        UnconfirmedTryoutStage(3)
                    },
                    1,
                    false),
                Throws.ArgumentException.With.Message.Contains("ordered"));

            var complete = CreateCompleteOnboarding();
            Assert.That(complete.NextStageNumber, Is.EqualTo(0));
            Assert.That(complete.IsFormallyEnrolled, Is.True);
        }

        [Test]
        public void Snapshot_RequiresProgressionToMatchOnboardingAndPlayerBoundary()
        {
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    onboarding: CreateActiveOnboarding(1)),
                Throws.ArgumentException.With.Message.Contains("current stage"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    true,
                    onboarding: CreateActiveOnboarding(1)),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(CreateDraftPlan()),
                    false,
                    onboarding: CreateCompleteOnboarding()),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(CreateDraftPlan()),
                    true,
                    onboarding: CreateActiveOnboarding(3)),
                Throws.ArgumentException.With.Message.Contains("completed"));

            var draft = CreateDraft();
            var mismatchedPlayer = CreatePlayer(
                new CareerPlayerDraft(
                    new PlayerId("different-player"),
                    draft.DisplayName,
                    draft.JerseyNumber));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(CreateDraftPlan()),
                    true,
                    draft: draft,
                    player: mismatchedPlayer),
                Throws.ArgumentException.With.Message.Contains("identity"));
        }

        [Test]
        public void Snapshot_RejectsInvalidSeedDraftAndVisibleStateRanges()
        {
            Assert.That(() => new CareerSeed(new byte[31]), Throws.ArgumentException);
            Assert.That(
                () => new CareerPlayerDraft(default, "Player", 12),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerPlayerDraft(new PlayerId("player-1"), "Player", 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var progression = CareerProgressionState.Planning(CreateDraftPlan());
            Assert.That(
                () => CreateSnapshot(progression, true, fatigue: 101),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CreateSnapshot(
                    progression,
                    true,
                    potentialGrade: (PotentialGrade)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Snapshot_RequiresRandomAlgorithmVersionForTryoutAndPendingEvent()
        {
            var mismatchedOnboarding = new TryoutOnboardingState(
                new[]
                {
                    new TryoutStageState(
                        1,
                        NewOccurrenceId(),
                        2,
                        null,
                        Array.Empty<TryoutResolvedOutput>()),
                    UnconfirmedTryoutStage(2),
                    UnconfirmedTryoutStage(3)
                },
                1,
                false);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    false,
                    onboarding: mismatchedOnboarding),
                Throws.ArgumentException.With.Message.Contains("random algorithm"));

            var plan = CreateConfirmedPlan();
            var source = plan.Slots[0];
            var mismatchedEvent = new PendingCareerEvent(
                plan.PlanId,
                source.SlotActionId,
                source.OccurrenceId,
                "social.first-week",
                NewOccurrenceId(),
                2,
                NewEventEffect("accept"),
                NewEventEffect("decline"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, mismatchedEvent),
                    true),
                Throws.ArgumentException.With.Message.Contains("random algorithm"));
        }

        [Test]
        public void Snapshot_UsesOneOccurrenceRegistryAcrossOnboardingPlanAndPendingEvent()
        {
            var onboarding = CreateCompleteOnboarding();
            var collidingPlan = new CareerWeekPlanState(
                NewWeekPlanId(),
                1,
                1,
                new[]
                {
                    new CareerWeekActionState(
                        NewSlotActionId(),
                        onboarding.Stages[0].OccurrenceId,
                        CareerWeekActionKind.SpecializedTraining),
                    NewActionState(CareerWeekActionKind.Rest),
                    NewActionState(CareerWeekActionKind.Match)
                },
                true);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(collidingPlan, 1),
                    true,
                    onboarding: onboarding),
                Throws.ArgumentException.With.Message.Contains("globally unique"));

            var plan = CreateConfirmedPlan();
            var source = plan.Slots[0];
            var collidingEvent = new PendingCareerEvent(
                plan.PlanId,
                source.SlotActionId,
                source.OccurrenceId,
                "social.first-week",
                plan.Slots[1].OccurrenceId,
                1,
                NewEventEffect("accept"),
                NewEventEffect("decline"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, collidingEvent),
                    true),
                Throws.ArgumentException.With.Message.Contains("globally unique"));
        }

        [Test]
        public void Snapshot_OnboardingRecoveryRequiresCreateAndExactSequentialStageReceipts()
        {
            var identity = CreateIdentity();
            var active = CreateActiveOnboarding(2);
            var stageOne = active.Stages[0];
            var create = NewReceipt(
                OperationKind.CreateCareer,
                OperationReceiptTarget.ForCreateCareer(),
                identity.LineageId,
                19);
            var validStageOne = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(
                    1,
                    stageOne.OccurrenceId,
                    stageOne.ChoiceId),
                identity.LineageId,
                20,
                OperationOutcomeSummary.ForTryoutAdvanced(stageOne.ResolvedOutputs));

            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { create, validStageOne },
                    onboarding: active,
                    addOnboardingReceipts: false));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { validStageOne },
                    onboarding: active,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("CreateCareer"));

            var wrongOccurrence = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(1, NewOccurrenceId(), stageOne.ChoiceId),
                identity.LineageId,
                20,
                OperationOutcomeSummary.ForTryoutAdvanced(stageOne.ResolvedOutputs));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { create, wrongOccurrence },
                    onboarding: active,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("matching"));

            var wrongSummary = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(
                    1,
                    stageOne.OccurrenceId,
                    stageOne.ChoiceId),
                identity.LineageId,
                20,
                OperationOutcomeSummary.ForTryoutAdvanced(
                    new[] { new TryoutResolvedOutput("wrong-output", 0) }));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { create, wrongSummary },
                    onboarding: active,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("resolved outputs"));

            var unconfirmedStageTwo = active.Stages[1];
            var prematureStageTwo = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(
                    2,
                    unconfirmedStageTwo.OccurrenceId,
                    "premature"),
                identity.LineageId,
                18);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { create, validStageOne, prematureStageTwo },
                    onboarding: active,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("unconfirmed"));

            var duplicateStageOne = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(1, NewOccurrenceId(), stageOne.ChoiceId),
                identity.LineageId,
                18,
                OperationOutcomeSummary.ForTryoutAdvanced(stageOne.ResolvedOutputs));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Tryout(2),
                    false,
                    identity: identity,
                    receipts: new[] { create, validStageOne, duplicateStageOne },
                    onboarding: active,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("business target"));

            var complete = CreateCompleteOnboarding();
            var incompleteReceiptSet = new List<OperationReceipt>(
                OnboardingReceipts(complete, identity.LineageId, 4));
            incompleteReceiptSet.RemoveAt(incompleteReceiptSet.Count - 1);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(CreateDraftPlan()),
                    true,
                    identity: identity,
                    receipts: incompleteReceiptSet,
                    onboarding: complete,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("matching"));
        }

        [Test]
        public void PendingEvent_FreezesSourceAndTwoResolvedEffects()
        {
            var plan = CreateConfirmedPlan();
            var eventState = CreatePendingEvent(plan);

            Assert.That(eventState.SourceWeekPlanId, Is.EqualTo(plan.PlanId));
            Assert.That(eventState.SourceSlotActionId, Is.EqualTo(plan.Slots[0].SlotActionId));
            Assert.That(eventState.SourceActionOccurrenceId, Is.EqualTo(plan.Slots[0].OccurrenceId));
            Assert.That(eventState.Options.Count, Is.EqualTo(2));
            Assert.That(eventState.Options[0].GrowthExperienceDelta.Spike, Is.EqualTo(10));
            Assert.That(eventState.ResumeAtSlotNumber, Is.EqualTo(2));
            Assert.That(
                () => new CareerAttributeGrowthDelta(-1, 0, 0, 0, 0, 0, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerAttributeGrowthDelta(
                    9007199254740992L,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => NewEventEffect("choice", fatigueDelta: 101),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new PendingCareerEvent(
                    plan.PlanId,
                    plan.Slots[0].SlotActionId,
                    plan.Slots[0].OccurrenceId,
                    "social.first-week",
                    NewOccurrenceId(),
                    1,
                    NewEventEffect("same"),
                    NewEventEffect("same")),
                Throws.ArgumentException);
        }

        [Test]
        public void OperationReceipt_RequiresTypedTargetAndMatchingKindAndOutcome()
        {
            var lineage = NewLineageId();
            var plan = CreateConfirmedPlan();
            var target = OperationReceiptTarget.ForWeekAction(
                plan.PlanId,
                plan.Slots[0].SlotActionId,
                plan.Slots[0].OccurrenceId);

            Assert.That(
                () => new OperationReceipt(
                    NewOperationId(),
                    OperationKind.ResolveEventChoice,
                    target,
                    Digest('b'),
                    lineage,
                    1,
                    0,
                    OperationOutcomeKind.EventChoiceApplied,
                    OperationOutcomeSummary.ForEventChoiceApplied(
                        ZeroGrowthDelta(),
                        0,
                        0,
                        0)),
                Throws.ArgumentException.With.Message.Contains("target"));
            Assert.That(
                () => new OperationReceipt(
                    NewOperationId(),
                    OperationKind.ExecuteWeekAction,
                    target,
                    Digest('b'),
                    lineage,
                    1,
                    0,
                    OperationOutcomeKind.EventChoiceApplied,
                    OperationOutcomeSummary.ForEventChoiceApplied(
                        ZeroGrowthDelta(),
                        0,
                        0,
                        0)),
                Throws.ArgumentException.With.Message.Contains("outcome"));
            Assert.That(
                () => NewReceipt(
                    OperationKind.ExecuteWeekAction,
                    target,
                    lineage,
                    appliedRevision: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => OperationReceiptTarget.ForTryoutStage(0, NewOccurrenceId(), "choice"),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new OperationReceipt(
                    NewOperationId(),
                    OperationKind.ExecuteWeekAction,
                    target,
                    Digest('b'),
                    lineage,
                    1,
                    0,
                    OperationOutcomeKind.SlotCompleted,
                    OperationOutcomeSummary.ForWeekPlanConfirmed()),
                Throws.ArgumentException.With.Message.Contains("summary"));
        }

        [Test]
        public void OperationOutcomeSummary_IsTypedBoundedAndDefensivelyCopied()
        {
            var outputInput = new[] { new TryoutResolvedOutput("spike", 12) };
            var tryout = OperationOutcomeSummary.ForTryoutAdvanced(outputInput);
            outputInput[0] = new TryoutResolvedOutput("mutated", -5);

            Assert.That(tryout.TryoutResolvedOutputs[0].OutputId, Is.EqualTo("spike"));
            Assert.That(tryout.GrowthExperienceDelta, Is.Null);
            Assert.That(
                () => ((IList<TryoutResolvedOutput>)tryout.TryoutResolvedOutputs)[0] =
                    outputInput[0],
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => OperationOutcomeSummary.ForTryoutAdvanced(
                    Array.Empty<TryoutResolvedOutput>()),
                Throws.ArgumentException);

            var delta = new CareerAttributeGrowthDelta(1, 2, 3, 4, 5, 6, 7, 8);
            var slot = OperationOutcomeSummary.ForSlotCompleted(delta, -10, 20, 30);
            Assert.That(slot.GrowthExperienceDelta.Spike, Is.EqualTo(1));
            Assert.That(slot.GrowthExperienceDelta, Is.Not.SameAs(delta));
            Assert.That(slot.FatigueDelta, Is.EqualTo(-10));
            Assert.That(
                () => OperationOutcomeSummary.ForEventChoiceApplied(
                    delta,
                    0,
                    101,
                    0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                OperationOutcomeSummary.ForCareerCreated().TryoutResolvedOutputs,
                Is.Empty);
            Assert.That(
                OperationOutcomeSummary.ForWeekPlanConfirmed().GrowthExperienceDelta,
                Is.Null);
        }

        [Test]
        public void PlannedRecoveryFrontier_RequiresExactCurrentPlanReceipts()
        {
            var plan = CreateConfirmedPlan();
            var identity = CreateIdentity();

            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 1),
                    true,
                    identity: identity,
                    receipts: ReceiptsForPlanned(
                        plan,
                        identity.LineageId,
                        1,
                        identity.Revision)));
            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 2),
                    true,
                    identity: identity,
                    receipts: ReceiptsForPlanned(
                        plan,
                        identity.LineageId,
                        2,
                        identity.Revision)));
            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 3),
                    true,
                    identity: identity,
                    receipts: ReceiptsForPlanned(
                        plan,
                        identity.LineageId,
                        3,
                        identity.Revision)));

            var completeOnboarding = CreateCompleteOnboarding();
            var receiptsPastSlotOneFrontier = new List<OperationReceipt>(
                OnboardingReceipts(
                    completeOnboarding,
                    identity.LineageId,
                    identity.Revision - 3));
            receiptsPastSlotOneFrontier.AddRange(
                ReceiptsForPlanned(
                    plan,
                    identity.LineageId,
                    2,
                    identity.Revision));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 1),
                    true,
                    identity: identity,
                    receipts: receiptsPastSlotOneFrontier,
                    onboarding: completeOnboarding,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("next slot 1"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 2),
                    true,
                    identity: identity,
                    receipts: new[]
                    {
                        ConfirmPlanReceipt(plan, identity.LineageId, identity.Revision - 2),
                        ExecuteReceipt(plan, 0, identity.LineageId, identity.Revision - 1)
                    }),
                Throws.ArgumentException.With.Message.Contains("next slot 2"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 3),
                    true,
                    identity: identity,
                    receipts: ReceiptsForPlanned(
                        plan,
                        identity.LineageId,
                        2,
                        identity.Revision)),
                Throws.ArgumentException.With.Message.Contains("next slot 3"));
        }

        [Test]
        public void ReceiptRevisionChain_IsStrictAndFrontierMatchesSnapshotRevision()
        {
            var plan = CreateConfirmedPlan();
            var identity = CreateIdentity();
            var reversedActionAndEvent = new[]
            {
                ConfirmPlanReceipt(plan, identity.LineageId, 18),
                ExecuteReceipt(plan, 0, identity.LineageId, 20),
                EventReceipt(plan, NewOccurrenceId(), "accept", identity.LineageId, 19)
            };
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 2),
                    true,
                    identity: identity,
                    receipts: reversedActionAndEvent),
                Throws.ArgumentException.With.Message.Contains("strictly follow"));

            var onboarding = CreateCompleteOnboarding();
            var staleFrontierReceipts = new List<OperationReceipt>(
                OnboardingReceipts(onboarding, identity.LineageId, 18))
            {
                ConfirmPlanReceipt(plan, identity.LineageId, 19)
            };
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 1),
                    true,
                    identity: identity,
                    receipts: staleFrontierReceipts,
                    onboarding: onboarding,
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("snapshot revision"));

            var createdIdentity = CreateIdentity(revision: 2);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    false,
                    identity: createdIdentity,
                    receipts: new[]
                    {
                        NewReceipt(
                            OperationKind.CreateCareer,
                            OperationReceiptTarget.ForCreateCareer(),
                            createdIdentity.LineageId,
                            1)
                    },
                    addOnboardingReceipts: false),
                Throws.ArgumentException.With.Message.Contains("snapshot revision"));

            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(CreateDraftPlan()),
                    true,
                    identity: identity));
        }

        [Test]
        public void AwaitingEventRecovery_RequiresSlotOneReceiptAndExactFrozenSource()
        {
            var plan = CreateConfirmedPlan();
            var identity = CreateIdentity();
            var pending = CreatePendingEvent(plan);
            var validReceipts = new[]
            {
                ConfirmPlanReceipt(plan, identity.LineageId, identity.Revision - 1),
                ExecuteReceipt(plan, 0, identity.LineageId, identity.Revision)
            };

            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, pending),
                    true,
                    identity: identity,
                    receipts: validReceipts));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, pending),
                    true,
                    identity: identity,
                    receipts: new[]
                    {
                        ConfirmPlanReceipt(plan, identity.LineageId, identity.Revision - 1)
                    }),
                Throws.ArgumentException.With.Message.Contains("completed slot 1"));

            var alreadyResolved = new List<OperationReceipt>(validReceipts)
            {
                EventReceipt(
                    plan,
                    pending.OccurrenceId,
                    pending.Options[0].OptionId,
                    identity.LineageId,
                    14)
            };
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, pending),
                    true,
                    identity: identity,
                    receipts: alreadyResolved),
                Throws.ArgumentException.With.Message.Contains("no resolved"));

            var wrongSource = new PendingCareerEvent(
                plan.PlanId,
                NewSlotActionId(),
                plan.Slots[0].OccurrenceId,
                "social.first-week",
                NewOccurrenceId(),
                1,
                NewEventEffect("accept"),
                NewEventEffect("decline"));
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.AwaitingEventChoice(plan, wrongSource),
                    true,
                    identity: identity,
                    receipts: validReceipts),
                Throws.ArgumentException.With.Message.Contains("exactly match"));
        }

        [Test]
        public void ConfirmedCurrentPlan_RequiresItsOwnConfirmationReceipt()
        {
            var plan = CreateConfirmedPlan();
            var identity = CreateIdentity();
            var historicalPlan = CreateConfirmedPlan();

            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 1),
                    true,
                    identity: identity,
                    receipts: new[]
                    {
                        ConfirmPlanReceipt(historicalPlan, identity.LineageId, 5)
                    }),
                Throws.ArgumentException.With.Message.Contains("confirmation"));

            var draft = CreateDraftPlan();
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planning(draft),
                    true,
                    identity: identity,
                    receipts: new[]
                    {
                        NewReceipt(
                            OperationKind.ConfirmWeekPlan,
                            OperationReceiptTarget.ForWeekPlanConfirmation(draft.PlanId),
                            identity.LineageId,
                            5)
                    }),
                Throws.ArgumentException.With.Message.Contains("unconfirmed"));
        }

        [Test]
        public void Snapshot_RejectsDuplicateActionOrEventBusinessTargets()
        {
            var plan = CreateConfirmedPlan();
            var identity = CreateIdentity();
            var duplicateActionReceipts = new[]
            {
                ConfirmPlanReceipt(plan, identity.LineageId, identity.Revision - 2),
                ExecuteReceipt(plan, 0, identity.LineageId, identity.Revision - 1),
                ExecuteReceipt(plan, 0, identity.LineageId, identity.Revision)
            };

            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 2),
                    true,
                    identity: identity,
                    receipts: duplicateActionReceipts),
                Throws.ArgumentException.With.Message.Contains("business target"));

            var eventOccurrence = NewOccurrenceId();
            var duplicateEventReceipts = new List<OperationReceipt>(
                ReceiptsForPlanned(plan, identity.LineageId, 2, identity.Revision))
            {
                EventReceipt(plan, eventOccurrence, "decline", identity.LineageId, 13)
            };
            var firstEventIndex = duplicateEventReceipts.FindIndex(
                receipt => receipt.OperationKind == OperationKind.ResolveEventChoice);
            duplicateEventReceipts[firstEventIndex] = EventReceipt(
                plan,
                eventOccurrence,
                "accept",
                identity.LineageId,
                identity.Revision);

            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(plan, 2),
                    true,
                    identity: identity,
                    receipts: duplicateEventReceipts),
                Throws.ArgumentException.With.Message.Contains("business target"));
        }

        [Test]
        public void HistoricalWeekReceipts_DoNotHaveToMatchCurrentPlan()
        {
            var current = CreateConfirmedPlan();
            var historical = CreateConfirmedPlan();
            var identity = CreateIdentity();
            var receipts = new List<OperationReceipt>(
                ReceiptsForPlanned(current, identity.LineageId, 1, identity.Revision))
            {
                ConfirmPlanReceipt(historical, identity.LineageId, 5),
                ExecuteReceipt(historical, 0, identity.LineageId, 6),
                EventReceipt(historical, NewOccurrenceId(), "accept", identity.LineageId, 7)
            };

            Assert.DoesNotThrow(
                () => CreateSnapshot(
                    CareerProgressionState.Planned(current, 1),
                    true,
                    identity: identity,
                    receipts: receipts));
        }

        [Test]
        public void Snapshot_RejectsDuplicateForeignOrFutureReceipts()
        {
            var identity = CreateIdentity(revision: 2);
            var createReceipt = NewReceipt(
                OperationKind.CreateCareer,
                OperationReceiptTarget.ForCreateCareer(),
                identity.LineageId,
                appliedRevision: 2);

            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    false,
                    identity: identity,
                    receipts: new[] { createReceipt, createReceipt },
                    addOnboardingReceipts: false),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    false,
                    identity: identity,
                    receipts: new[]
                    {
                        NewReceipt(
                            OperationKind.CreateCareer,
                            OperationReceiptTarget.ForCreateCareer(),
                            NewLineageId())
                    },
                    addOnboardingReceipts: false),
                Throws.ArgumentException);
            Assert.That(
                () => CreateSnapshot(
                    CareerProgressionState.Created(),
                    false,
                    identity: identity,
                    receipts: new[]
                    {
                        NewReceipt(
                            OperationKind.CreateCareer,
                            OperationReceiptTarget.ForCreateCareer(),
                            identity.LineageId,
                            appliedRevision: 3)
                    },
                    addOnboardingReceipts: false),
                Throws.ArgumentException);
        }

        [Test]
        public void SnapshotAndNestedStates_DefensivelyCopyInputCollections()
        {
            var outputInput = new[]
            {
                new TryoutResolvedOutput("first", 1),
                new TryoutResolvedOutput("second", 2)
            };
            var stage = new TryoutStageState(1, NewOccurrenceId(), 1, "choice", outputInput);
            outputInput[0] = new TryoutResolvedOutput("mutated", 3);
            Assert.That(stage.ResolvedOutputs[0].OutputId, Is.EqualTo("first"));
            Assert.That(
                () => ((IList<TryoutResolvedOutput>)stage.ResolvedOutputs)[0] = outputInput[0],
                Throws.TypeOf<NotSupportedException>());

            var stageInput = new[]
            {
                stage,
                ConfirmedTryoutStage(2),
                ConfirmedTryoutStage(3)
            };
            var onboarding = new TryoutOnboardingState(stageInput, 0, true);
            stageInput[0] = UnconfirmedTryoutStage(1);
            Assert.That(onboarding.Stages[0].IsConfirmed, Is.True);

            var slotInput = new CareerWeekActionState[]
            {
                NewActionState(CareerWeekActionKind.Rest),
                null,
                NewActionState(CareerWeekActionKind.Match)
            };
            var plan = new CareerWeekPlanState(NewWeekPlanId(), 1, 1, slotInput, false);
            var originalActionId = plan.Slots[0].SlotActionId;
            slotInput[0] = NewActionState(CareerWeekActionKind.TeamPractice);
            Assert.That(plan.Slots[0].SlotActionId, Is.EqualTo(originalActionId));

            var identity = CreateIdentity();
            var receiptInput = new List<OperationReceipt>(
                OnboardingReceipts(onboarding, identity.LineageId, 4)).ToArray();
            var receipt = receiptInput[0];
            var snapshot = CreateSnapshot(
                CareerProgressionState.Planning(plan),
                true,
                identity: identity,
                receipts: receiptInput,
                onboarding: onboarding,
                addOnboardingReceipts: false);
            receiptInput[0] = NewReceipt(
                OperationKind.ConfirmTryoutStage,
                OperationReceiptTarget.ForTryoutStage(1, NewOccurrenceId(), "choice"),
                identity.LineageId);

            Assert.That(snapshot.OperationReceipts[0].OperationId, Is.EqualTo(receipt.OperationId));
            Assert.That(snapshot.Onboarding, Is.Not.SameAs(onboarding));
            Assert.That(
                () => ((IList<OperationReceipt>)snapshot.OperationReceipts)[0] = receipt,
                Throws.TypeOf<NotSupportedException>());
        }

        private static CareerSaveSnapshot CreateSnapshot(
            CareerProgressionState progression,
            bool includeCompletePlayer,
            CareerSaveIdentity identity = null,
            IEnumerable<OperationReceipt> receipts = null,
            TryoutOnboardingState onboarding = null,
            CareerPlayerDraft draft = null,
            CareerPlayerRecord player = null,
            PotentialGrade potentialGrade = PotentialGrade.B,
            int fatigue = 20,
            bool addOnboardingReceipts = true)
        {
            identity = identity ?? CreateIdentity();
            draft = draft ?? CreateDraft();
            onboarding = onboarding ?? DefaultOnboardingFor(progression);
            if (includeCompletePlayer)
            {
                player = player ?? CreatePlayer(draft);
            }

            var allReceipts = new List<OperationReceipt>();
            if (addOnboardingReceipts)
            {
                allReceipts.AddRange(
                    OnboardingReceipts(
                        onboarding,
                        identity.LineageId,
                        OnboardingFrontierRevision(progression, identity.Revision)));
            }

            allReceipts.AddRange(
                receipts ?? DefaultReceiptsFor(
                    progression,
                    identity.LineageId,
                    identity.Revision));

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                identity,
                new CareerSeed(new byte[CareerSeed.ByteLength]),
                "First Career",
                draft,
                onboarding,
                progression,
                player,
                includeCompletePlayer ? new TeamId("university-blue") : (TeamId?)null,
                includeCompletePlayer ? potentialGrade : (PotentialGrade?)null,
                includeCompletePlayer ? fatigue : (int?)null,
                includeCompletePlayer ? 50 : (int?)null,
                includeCompletePlayer ? 60 : (int?)null,
                allReceipts);
        }

        private static IReadOnlyList<OperationReceipt> OnboardingReceipts(
            TryoutOnboardingState onboarding,
            LineageId lineageId,
            long frontierRevision)
        {
            var receiptCount = 1;
            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                if (onboarding.Stages[index].IsConfirmed)
                {
                    receiptCount++;
                }
            }

            var revision = frontierRevision - receiptCount + 1;
            var receipts = new List<OperationReceipt>
            {
                NewReceipt(
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    lineageId,
                    revision)
            };
            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                var stage = onboarding.Stages[index];
                if (!stage.IsConfirmed)
                {
                    continue;
                }

                revision++;
                receipts.Add(
                    NewReceipt(
                        OperationKind.ConfirmTryoutStage,
                        OperationReceiptTarget.ForTryoutStage(
                            stage.StageNumber,
                            stage.OccurrenceId,
                            stage.ChoiceId),
                        lineageId,
                        revision,
                        OperationOutcomeSummary.ForTryoutAdvanced(stage.ResolvedOutputs)));
            }

            return receipts;
        }

        private static IEnumerable<OperationReceipt> DefaultReceiptsFor(
            CareerProgressionState progression,
            LineageId lineageId,
            long frontierRevision)
        {
            if (progression.Kind == CareerProgressionKind.Planned)
            {
                return ReceiptsForPlanned(
                    progression.WeekPlan,
                    lineageId,
                    progression.NextSlotNumber,
                    frontierRevision);
            }

            if (progression.Kind == CareerProgressionKind.AwaitingEventChoice)
            {
                return new[]
                {
                    ConfirmPlanReceipt(progression.WeekPlan, lineageId, frontierRevision - 1),
                    ExecuteReceipt(progression.WeekPlan, 0, lineageId, frontierRevision)
                };
            }

            return Array.Empty<OperationReceipt>();
        }

        private static IReadOnlyList<OperationReceipt> ReceiptsForPlanned(
            CareerWeekPlanState plan,
            LineageId lineageId,
            int nextSlot,
            long frontierRevision)
        {
            var operationCount = nextSlot == 1 ? 1 : nextSlot == 2 ? 3 : 4;
            var revision = frontierRevision - operationCount + 1;
            var receipts = new List<OperationReceipt>
            {
                ConfirmPlanReceipt(plan, lineageId, revision)
            };
            if (nextSlot >= 2)
            {
                receipts.Add(ExecuteReceipt(plan, 0, lineageId, ++revision));
                receipts.Add(
                    EventReceipt(
                        plan,
                        NewOccurrenceId(),
                        "accept",
                        lineageId,
                        ++revision));
            }

            if (nextSlot >= 3)
            {
                receipts.Add(ExecuteReceipt(plan, 1, lineageId, ++revision));
            }

            return receipts;
        }

        private static OperationReceipt ConfirmPlanReceipt(
            CareerWeekPlanState plan,
            LineageId lineageId,
            long appliedRevision)
        {
            return NewReceipt(
                OperationKind.ConfirmWeekPlan,
                OperationReceiptTarget.ForWeekPlanConfirmation(plan.PlanId),
                lineageId,
                appliedRevision);
        }

        private static OperationReceipt ExecuteReceipt(
            CareerWeekPlanState plan,
            int slotIndex,
            LineageId lineageId,
            long appliedRevision)
        {
            var action = plan.Slots[slotIndex];
            return NewReceipt(
                OperationKind.ExecuteWeekAction,
                OperationReceiptTarget.ForWeekAction(
                    plan.PlanId,
                    action.SlotActionId,
                    action.OccurrenceId),
                lineageId,
                appliedRevision);
        }

        private static OperationReceipt EventReceipt(
            CareerWeekPlanState plan,
            OccurrenceId eventOccurrence,
            string optionId,
            LineageId lineageId,
            long appliedRevision)
        {
            var source = plan.Slots[0];
            return NewReceipt(
                OperationKind.ResolveEventChoice,
                OperationReceiptTarget.ForEventChoice(
                    plan.PlanId,
                    source.SlotActionId,
                    source.OccurrenceId,
                    eventOccurrence,
                    optionId),
                lineageId,
                appliedRevision);
        }

        private static OperationReceipt NewReceipt(
            OperationKind operationKind,
            OperationReceiptTarget target,
            LineageId lineageId,
            long appliedRevision = 1,
            OperationOutcomeSummary outcomeSummary = null)
        {
            var outcomeKind = OutcomeFor(operationKind);
            return new OperationReceipt(
                NewOperationId(),
                operationKind,
                target,
                Digest('b'),
                lineageId,
                appliedRevision,
                10,
                outcomeKind,
                outcomeSummary ?? DefaultSummaryFor(outcomeKind));
        }

        private static OperationOutcomeSummary DefaultSummaryFor(
            OperationOutcomeKind outcomeKind)
        {
            switch (outcomeKind)
            {
                case OperationOutcomeKind.CareerCreated:
                    return OperationOutcomeSummary.ForCareerCreated();
                case OperationOutcomeKind.TryoutAdvanced:
                    return OperationOutcomeSummary.ForTryoutAdvanced(
                        new[] { new TryoutResolvedOutput("fixture-output", 0) });
                case OperationOutcomeKind.WeekPlanConfirmed:
                    return OperationOutcomeSummary.ForWeekPlanConfirmed();
                case OperationOutcomeKind.SlotCompleted:
                    return OperationOutcomeSummary.ForSlotCompleted(ZeroGrowthDelta(), 0, 0, 0);
                case OperationOutcomeKind.EventChoiceApplied:
                    return OperationOutcomeSummary.ForEventChoiceApplied(
                        ZeroGrowthDelta(),
                        0,
                        0,
                        0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcomeKind));
            }
        }

        private static CareerAttributeGrowthDelta ZeroGrowthDelta()
        {
            return new CareerAttributeGrowthDelta(0, 0, 0, 0, 0, 0, 0, 0);
        }

        private static long OnboardingFrontierRevision(
            CareerProgressionState progression,
            long snapshotRevision)
        {
            if (progression.Kind == CareerProgressionKind.Planning)
            {
                return 4;
            }

            if (progression.Kind == CareerProgressionKind.AwaitingEventChoice)
            {
                return snapshotRevision - 2;
            }

            if (progression.Kind == CareerProgressionKind.Planned)
            {
                switch (progression.NextSlotNumber)
                {
                    case 1:
                        return snapshotRevision - 1;
                    case 2:
                        return snapshotRevision - 3;
                    case 3:
                        return snapshotRevision - 4;
                }
            }

            return snapshotRevision;
        }

        private static OperationOutcomeKind OutcomeFor(OperationKind operationKind)
        {
            switch (operationKind)
            {
                case OperationKind.CreateCareer:
                    return OperationOutcomeKind.CareerCreated;
                case OperationKind.ConfirmTryoutStage:
                    return OperationOutcomeKind.TryoutAdvanced;
                case OperationKind.ConfirmWeekPlan:
                    return OperationOutcomeKind.WeekPlanConfirmed;
                case OperationKind.ExecuteWeekAction:
                    return OperationOutcomeKind.SlotCompleted;
                case OperationKind.ResolveEventChoice:
                    return OperationOutcomeKind.EventChoiceApplied;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operationKind));
            }
        }

        private static TryoutOnboardingState DefaultOnboardingFor(
            CareerProgressionState progression)
        {
            if (progression.Kind == CareerProgressionKind.CareerCreated)
            {
                return CreateActiveOnboarding(1);
            }

            if (progression.Kind == CareerProgressionKind.Tryout)
            {
                return CreateActiveOnboarding(progression.TryoutStage);
            }

            return CreateCompleteOnboarding();
        }

        private static TryoutOnboardingState CreateActiveOnboarding(int currentStage)
        {
            var stages = new TryoutStageState[3];
            for (var stage = 1; stage <= 3; stage++)
            {
                stages[stage - 1] = stage < currentStage
                    ? ConfirmedTryoutStage(stage)
                    : UnconfirmedTryoutStage(stage);
            }

            return new TryoutOnboardingState(stages, currentStage, false);
        }

        private static TryoutOnboardingState CreateCompleteOnboarding()
        {
            return new TryoutOnboardingState(
                new[]
                {
                    ConfirmedTryoutStage(1),
                    ConfirmedTryoutStage(2),
                    ConfirmedTryoutStage(3)
                },
                0,
                true);
        }

        private static TryoutStageState ConfirmedTryoutStage(int stage)
        {
            return new TryoutStageState(
                stage,
                NewOccurrenceId(),
                1,
                "stage-" + stage + "-choice",
                new[]
                {
                    new TryoutResolvedOutput("stage-" + stage + "-primary", 10),
                    new TryoutResolvedOutput("stage-" + stage + "-secondary", -5)
                });
        }

        private static TryoutStageState UnconfirmedTryoutStage(int stage)
        {
            return new TryoutStageState(
                stage,
                NewOccurrenceId(),
                1,
                null,
                Array.Empty<TryoutResolvedOutput>());
        }

        private static PendingCareerEvent CreatePendingEvent(CareerWeekPlanState plan)
        {
            var source = plan.Slots[0];
            return new PendingCareerEvent(
                plan.PlanId,
                source.SlotActionId,
                source.OccurrenceId,
                "social.first-week",
                NewOccurrenceId(),
                1,
                NewEventEffect("accept", fatigueDelta: 5, mindsetDelta: 10),
                NewEventEffect("decline", coachTrustDelta: -5));
        }

        private static CareerEventOptionEffect NewEventEffect(
            string optionId,
            int fatigueDelta = 0,
            int mindsetDelta = 0,
            int coachTrustDelta = 0)
        {
            return new CareerEventOptionEffect(
                optionId,
                new CareerAttributeGrowthDelta(10, 20, 30, 40, 50, 60, 70, 80),
                fatigueDelta,
                mindsetDelta,
                coachTrustDelta);
        }

        private static CareerSaveIdentity CreateIdentity(
            long revision = 20,
            long createdAt = 0,
            long updatedAt = 10)
        {
            return new CareerSaveIdentity(
                NewProfileId(),
                NewSaveId(),
                NewLineageId(),
                revision,
                createdAt,
                updatedAt,
                Digest());
        }

        private static CareerPlayerDraft CreateDraft()
        {
            return new CareerPlayerDraft(new PlayerId("career-player-1"), "Player One", 12);
        }

        private static CareerPlayerRecord CreatePlayer(CareerPlayerDraft draft)
        {
            var progress = new CareerAttributeProgress(5000, 10);
            return new CareerPlayerRecord(
                draft.PlayerId,
                draft.DisplayName,
                draft.JerseyNumber,
                new CareerPlayerAttributes(
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress));
        }

        private static CareerWeekPlanState CreateDraftPlan()
        {
            return new CareerWeekPlanState(
                NewWeekPlanId(),
                1,
                1,
                new CareerWeekActionState[]
                {
                    NewActionState(CareerWeekActionKind.SpecializedTraining),
                    null,
                    NewActionState(CareerWeekActionKind.Match)
                },
                false);
        }

        private static CareerWeekPlanState CreateConfirmedPlan()
        {
            return new CareerWeekPlanState(
                NewWeekPlanId(),
                1,
                1,
                new[]
                {
                    NewActionState(CareerWeekActionKind.SpecializedTraining),
                    NewActionState(CareerWeekActionKind.Rest),
                    NewActionState(CareerWeekActionKind.Match)
                },
                true);
        }

        private static CareerWeekActionState NewActionState(CareerWeekActionKind kind)
        {
            return new CareerWeekActionState(NewSlotActionId(), NewOccurrenceId(), kind);
        }

        private static Sha256Digest Digest(char value = 'a')
        {
            return new Sha256Digest(new string(value, CareerSeed.HexLength));
        }

        private static ProfileId NewProfileId() => new ProfileId(Guid.NewGuid());
        private static SaveId NewSaveId() => new SaveId(Guid.NewGuid());
        private static LineageId NewLineageId() => new LineageId(Guid.NewGuid());
        private static OperationId NewOperationId() => new OperationId(Guid.NewGuid());
        private static WeekPlanId NewWeekPlanId() => new WeekPlanId(Guid.NewGuid());
        private static SlotActionId NewSlotActionId() => new SlotActionId(Guid.NewGuid());
        private static OccurrenceId NewOccurrenceId() => new OccurrenceId(Guid.NewGuid());
    }
}
