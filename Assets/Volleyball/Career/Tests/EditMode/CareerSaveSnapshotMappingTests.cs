using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveSnapshotMappingTests
    {
        [TestCase(CareerProgressionKind.CareerCreated)]
        [TestCase(CareerProgressionKind.Tryout)]
        [TestCase(CareerProgressionKind.Planning)]
        [TestCase(CareerProgressionKind.Planned)]
        [TestCase(CareerProgressionKind.AwaitingEventChoice)]
        public void RoundTrip_PreservesEverySupportedAuthorityState(
            CareerProgressionKind kind)
        {
            var source = CreateSnapshot(kind);

            var document = CareerSaveSnapshotMapper.ToDocument(source);
            var restored = CareerSaveSnapshotMapper.ToDomain(document);

            AssertEquivalent(source, restored);
            Assert.That(document.progression.kind, Is.EqualTo(ProgressionName(kind)));
        }

        [TestCase(CareerProgressionKind.AwaitingEventChoice, 1)]
        [TestCase(CareerProgressionKind.Planned, 2)]
        public void RoundTrip_PreservesExecutedTrainingEmphasisContributions(
            CareerProgressionKind kind,
            int expectedCount)
        {
            var source = CreateSnapshot(kind, useTrainingPlan: true);

            var document = CareerSaveSnapshotMapper.ToDocument(source);
            var restored = CareerSaveSnapshotMapper.ToDomain(document);

            AssertEquivalent(source, restored);
            Assert.That(document.trainingEmphases, Has.Length.EqualTo(expectedCount));
            Assert.That(restored.TrainingEmphases.Contributions, Has.Count.EqualTo(expectedCount));
            Assert.That(restored.TrainingEmphases.Contributions[0].Direction,
                Is.EqualTo(CareerTrainingDirection.Spike));
            if (expectedCount == 2)
            {
                Assert.That(restored.TrainingEmphases.Contributions[1].Direction,
                    Is.EqualTo(CareerTrainingDirection.Jump));
            }
        }

        [Test]
        public void RoundTrip_PreservesIncompleteAndCompletedOnboarding()
        {
            var incomplete = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(
                    CreateSnapshot(CareerProgressionKind.Tryout)));
            var complete = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(
                    CreateSnapshot(CareerProgressionKind.Planning)));

            Assert.That(incomplete.Onboarding.NextStageNumber, Is.EqualTo(2));
            Assert.That(incomplete.Onboarding.IsFormallyEnrolled, Is.False);
            Assert.That(incomplete.Onboarding.Stages[0].ChoiceId, Is.EqualTo("stage-1-choice"));
            Assert.That(incomplete.Onboarding.Stages[0].ResolvedOutputs.Count, Is.EqualTo(2));
            Assert.That(
                incomplete.Onboarding.Stages[0].ResolvedOutputs[0].OutputId,
                Is.EqualTo("stage-1-primary"));
            Assert.That(incomplete.Onboarding.Stages[0].ResolvedOutputs[1].Perturbation, Is.EqualTo(-5));
            Assert.That(incomplete.Onboarding.Stages[1].ChoiceId, Is.Null);
            Assert.That(incomplete.Onboarding.Stages[1].ResolvedOutputs, Is.Empty);

            Assert.That(complete.Onboarding.IsComplete, Is.True);
            Assert.That(complete.Onboarding.NextStageNumber, Is.Zero);
            Assert.That(complete.Onboarding.Stages, Has.Count.EqualTo(3));
            Assert.That(complete.Onboarding.Stages[2].ChoiceId, Is.EqualTo("stage-3-choice"));
        }

        [Test]
        public void RoundTrip_PreservesRestoredSourceFullVersionToken()
        {
            var source = CreateSnapshot(CareerProgressionKind.Planning);
            var restoredFrom = new CareerVersionToken(
                new LineageId(Guid.NewGuid()),
                source.Identity.Revision - 1,
                new Sha256Digest(new string('e', 64)));
            var identity = new CareerSaveIdentity(
                source.Identity.ProfileId,
                source.Identity.SaveId,
                source.Identity.LineageId,
                source.Identity.Revision,
                source.Identity.CreatedAtUtcMs,
                source.Identity.UpdatedAtUtcMs,
                source.Identity.SnapshotHash,
                restoredFrom);
            var restoredSource = new CareerSaveSnapshot(
                source.Versions,
                identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                source.Progression,
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                source.OperationReceipts);

            var restored = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(restoredSource));

            Assert.That(restored.Identity.RestoredFromVersionToken, Is.EqualTo(restoredFrom));
        }

        [Test]
        public void RoundTrip_PreservesPlanningEmptySlotAndReservedMatchSlot()
        {
            var restored = CareerSaveSnapshotMapper.ToDomain(
                Document(CareerProgressionKind.Planning));

            Assert.That(restored.Progression.WeekPlan.IsConfirmed, Is.False);
            Assert.That(restored.Progression.WeekPlan.Slots[0].Kind,
                Is.EqualTo(CareerWeekActionKind.TeamPractice));
            Assert.That(restored.Progression.WeekPlan.Slots[1], Is.Null);
            Assert.That(restored.Progression.WeekPlan.Slots[2].Kind,
                Is.EqualTo(CareerWeekActionKind.Match));
        }

        [Test]
        public void RoundTrip_PreservesFullPendingEventEffects()
        {
            var restored = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(
                    CreateSnapshot(CareerProgressionKind.AwaitingEventChoice)));
            var pendingEvent = restored.Progression.PendingEvent;

            Assert.That(pendingEvent.SourceWeekPlanId, Is.EqualTo(restored.Progression.WeekPlan.PlanId));
            Assert.That(
                pendingEvent.SourceSlotActionId,
                Is.EqualTo(restored.Progression.WeekPlan.Slots[0].SlotActionId));
            Assert.That(
                pendingEvent.SourceActionOccurrenceId,
                Is.EqualTo(restored.Progression.WeekPlan.Slots[0].OccurrenceId));
            Assert.That(pendingEvent.EventId, Is.EqualTo("social.first-week"));
            Assert.That(pendingEvent.RandomVersion, Is.EqualTo(1));
            Assert.That(pendingEvent.ResumeAtSlotNumber, Is.EqualTo(2));
            Assert.That(pendingEvent.Options, Has.Count.EqualTo(2));
            Assert.That(pendingEvent.Options[0].OptionId, Is.EqualTo("accept"));
            Assert.That(pendingEvent.Options[0].GrowthExperienceDelta.Spike, Is.EqualTo(10));
            Assert.That(pendingEvent.Options[0].GrowthExperienceDelta.Stamina, Is.EqualTo(80));
            Assert.That(pendingEvent.Options[0].FatigueDelta, Is.EqualTo(5));
            Assert.That(pendingEvent.Options[0].MindsetDelta, Is.EqualTo(10));
            Assert.That(pendingEvent.Options[0].CoachTrustDelta, Is.EqualTo(-2));
            Assert.That(pendingEvent.Options[1].GrowthExperienceDelta.Spike, Is.EqualTo(1));
            Assert.That(pendingEvent.Options[1].GrowthExperienceDelta.Stamina, Is.EqualTo(8));
            Assert.That(pendingEvent.Options[1].FatigueDelta, Is.EqualTo(-5));
            Assert.That(pendingEvent.Options[1].MindsetDelta, Is.EqualTo(-10));
            Assert.That(pendingEvent.Options[1].CoachTrustDelta, Is.EqualTo(2));
        }

        [Test]
        public void RoundTrip_PreservesAllFiveReceiptTargetShapes()
        {
            var document = CareerSaveSnapshotMapper.ToDocument(
                CreateSnapshot(CareerProgressionKind.Planned));
            var restored = CareerSaveSnapshotMapper.ToDomain(document);

            var create = FindReceipt(restored, OperationKind.CreateCareer);
            var tryout = FindReceipt(restored, OperationKind.ConfirmTryoutStage);
            var plan = FindReceipt(restored, OperationKind.ConfirmWeekPlan);
            var action = FindReceipt(restored, OperationKind.ExecuteWeekAction);
            var careerEvent = FindReceipt(restored, OperationKind.ResolveEventChoice);

            Assert.That(create.Target.WeekPlanId, Is.Null);
            Assert.That(tryout.Target.TryoutStage, Is.EqualTo(1));
            Assert.That(tryout.Target.TryoutOccurrenceId, Is.Not.Null);
            Assert.That(tryout.Target.ChoiceId, Is.EqualTo("stage-1-choice"));
            Assert.That(plan.Target.WeekPlanId, Is.EqualTo(restored.Progression.WeekPlan.PlanId));
            Assert.That(plan.Target.SlotActionId, Is.Null);
            Assert.That(action.Target.SlotActionId, Is.Not.Null);
            Assert.That(action.Target.ActionOccurrenceId, Is.Not.Null);
            Assert.That(careerEvent.Target.EventOccurrenceId, Is.Not.Null);
            Assert.That(careerEvent.Target.OptionId, Is.EqualTo("accept"));
        }

        [Test]
        public void RoundTrip_PreservesAllFiveOutcomeSummaryShapes()
        {
            var document = Document(CareerProgressionKind.Planned);
            var restored = CareerSaveSnapshotMapper.ToDomain(document);

            var createDocument = FindReceiptDocument(document, OperationKind.CreateCareer);
            var planDocument = FindReceiptDocument(document, OperationKind.ConfirmWeekPlan);
            Assert.That(createDocument.outcomeSummary.tryoutResolvedOutputs, Is.Empty);
            Assert.That(createDocument.outcomeSummary.growthExperienceDelta, Is.Null);
            Assert.That(createDocument.outcomeSummary.fatigueDelta, Is.Null);
            Assert.That(planDocument.outcomeSummary.tryoutResolvedOutputs, Is.Empty);
            Assert.That(planDocument.outcomeSummary.growthExperienceDelta, Is.Null);
            Assert.That(planDocument.outcomeSummary.coachTrustDelta, Is.Null);

            var tryout = FindReceipt(restored, OperationKind.ConfirmTryoutStage).OutcomeSummary;
            var slot = FindReceipt(restored, OperationKind.ExecuteWeekAction).OutcomeSummary;
            var careerEvent = FindReceipt(restored, OperationKind.ResolveEventChoice).OutcomeSummary;
            Assert.That(tryout.OutcomeKind, Is.EqualTo(OperationOutcomeKind.TryoutAdvanced));
            Assert.That(tryout.TryoutResolvedOutputs, Has.Count.EqualTo(2));
            Assert.That(tryout.TryoutResolvedOutputs[0].OutputId, Is.EqualTo("stage-1-primary"));
            Assert.That(tryout.GrowthExperienceDelta, Is.Null);
            Assert.That(slot.OutcomeKind, Is.EqualTo(OperationOutcomeKind.SlotCompleted));
            Assert.That(slot.TryoutResolvedOutputs, Is.Empty);
            Assert.That(slot.GrowthExperienceDelta.Stamina, Is.EqualTo(8));
            Assert.That(slot.FatigueDelta, Is.EqualTo(4));
            Assert.That(careerEvent.OutcomeKind, Is.EqualTo(OperationOutcomeKind.EventChoiceApplied));
            Assert.That(careerEvent.GrowthExperienceDelta.Spike, Is.EqualTo(10));
            Assert.That(careerEvent.GrowthExperienceDelta.Stamina, Is.EqualTo(80));
            Assert.That(careerEvent.MindsetDelta, Is.EqualTo(10));
        }

        [Test]
        public void ToDomain_DeepCopiesOnboardingEventPlanPlayerAndReceiptTargets()
        {
            var document = CareerSaveSnapshotMapper.ToDocument(
                CreateSnapshot(CareerProgressionKind.AwaitingEventChoice));
            var originalOutput = document.onboarding.stages[0].resolvedOutputs[0].outputId;
            var originalGrowth = document.progression.pendingEvent.options[0]
                .growthExperienceDelta.spike;
            var originalTargetPlan = FindReceiptDocument(
                document,
                OperationKind.ExecuteWeekAction).target.weekPlanId;
            var originalReceiptGrowth = FindReceiptDocument(
                document,
                OperationKind.ExecuteWeekAction).outcomeSummary.growthExperienceDelta.spike;

            var restored = CareerSaveSnapshotMapper.ToDomain(document);

            document.onboarding.stages[0].resolvedOutputs[0].outputId = "changed-output";
            document.progression.weekPlan.slots[0].kind = "rest";
            document.progression.pendingEvent.options[0].growthExperienceDelta.spike = 999;
            document.player.attributes.spike.abilityBasisPoints = 9999;
            FindReceiptDocument(document, OperationKind.ExecuteWeekAction).target.weekPlanId =
                GuidValue(999).ToString("D");
            FindReceiptDocument(document, OperationKind.ExecuteWeekAction)
                .outcomeSummary.growthExperienceDelta.spike = 999;

            Assert.That(
                restored.Onboarding.Stages[0].ResolvedOutputs[0].OutputId,
                Is.EqualTo(originalOutput));
            Assert.That(
                restored.Progression.WeekPlan.Slots[0].Kind,
                Is.EqualTo(CareerWeekActionKind.TeamPractice));
            Assert.That(
                restored.Progression.PendingEvent.Options[0].GrowthExperienceDelta.Spike,
                Is.EqualTo(originalGrowth));
            Assert.That(restored.Player.Attributes.Spike.AbilityBasisPoints, Is.EqualTo(5100));
            Assert.That(
                FindReceipt(restored, OperationKind.ExecuteWeekAction).Target.WeekPlanId.Value
                    .ToString(),
                Is.EqualTo(originalTargetPlan));
            Assert.That(
                FindReceipt(restored, OperationKind.ExecuteWeekAction)
                    .OutcomeSummary.GrowthExperienceDelta.Spike,
                Is.EqualTo(originalReceiptGrowth));
        }

        [Test]
        public void ToDomain_RejectsMissingOrUnsupportedVersions()
        {
            var documents = new CareerSaveDocumentV1[5];
            for (var index = 0; index < documents.Length; index++)
            {
                documents[index] = CareerSaveSnapshotMapper.ToDocument(
                    CreateSnapshot(CareerProgressionKind.CareerCreated));
            }

            documents[0].versions = null;
            documents[1].versions.schemaVersion = 2;
            documents[2].versions.contentVersion = 2;
            documents[3].versions.rulesetVersion = 2;
            documents[4].versions.careerRandomAlgorithmVersion = 2;

            for (var index = 0; index < documents.Length; index++)
            {
                Assert.That(
                    () => CareerSaveSnapshotMapper.ToDomain(documents[index]),
                    Throws.InstanceOf<ArgumentException>());
            }
        }

        [Test]
        public void ToDomain_RejectsUnknownEnumsAndMalformedIdentityValues()
        {
            var unknownProgression = Document(CareerProgressionKind.CareerCreated);
            unknownProgression.progression.kind = "executing_slot";
            var unknownAction = Document(CareerProgressionKind.Planned);
            unknownAction.progression.weekPlan.slots[0].kind = "academics";
            var unknownReceipt = Document(CareerProgressionKind.Planned);
            unknownReceipt.operationReceipts[0].operationKind = "unknown";
            var badSeed = Document(CareerProgressionKind.CareerCreated);
            badSeed.careerSeed = new string('A', 64);
            var badHash = Document(CareerProgressionKind.CareerCreated);
            badHash.integrity.snapshotHash = "bad";
            var badGuid = Document(CareerProgressionKind.CareerCreated);
            badGuid.identity.profileId = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA";

            AssertRejected(unknownProgression);
            AssertRejected(unknownAction);
            AssertRejected(unknownReceipt);
            AssertRejected(badSeed);
            AssertRejected(badHash);
            AssertRejected(badGuid);
        }

        [Test]
        public void ToDomain_RejectsInvalidOnboardingSequencesAndOutputs()
        {
            var missingStage = Document(CareerProgressionKind.Tryout);
            missingStage.onboarding.stages = new[]
            {
                missingStage.onboarding.stages[0],
                missingStage.onboarding.stages[1]
            };

            var wrongOrder = Document(CareerProgressionKind.Tryout);
            wrongOrder.onboarding.stages[1].stageNumber = 3;

            var duplicateOccurrence = Document(CareerProgressionKind.Tryout);
            duplicateOccurrence.onboarding.stages[1].occurrenceId =
                duplicateOccurrence.onboarding.stages[0].occurrenceId;

            var outputWithoutChoice = Document(CareerProgressionKind.Tryout);
            outputWithoutChoice.onboarding.stages[1].resolvedOutputs = new[]
            {
                new TryoutResolvedOutputDocumentV1
                {
                    outputId = "unexpected",
                    perturbation = 1
                }
            };

            var duplicateOutput = Document(CareerProgressionKind.Tryout);
            duplicateOutput.onboarding.stages[0].resolvedOutputs[1].outputId =
                duplicateOutput.onboarding.stages[0].resolvedOutputs[0].outputId;

            AssertRejected(missingStage);
            AssertRejected(wrongOrder);
            AssertRejected(duplicateOccurrence);
            AssertRejected(outputWithoutChoice);
            AssertRejected(duplicateOutput);
        }

        [Test]
        public void ToDomain_RejectsIncompleteOrContradictoryPendingEvents()
        {
            var oneOption = Document(CareerProgressionKind.AwaitingEventChoice);
            oneOption.progression.pendingEvent.options = new[]
            {
                oneOption.progression.pendingEvent.options[0]
            };

            var missingGrowth = Document(CareerProgressionKind.AwaitingEventChoice);
            missingGrowth.progression.pendingEvent.options[0].growthExperienceDelta = null;

            var wrongSourcePlan = Document(CareerProgressionKind.AwaitingEventChoice);
            wrongSourcePlan.progression.pendingEvent.sourceWeekPlanId = GuidValue(998).ToString("D");

            var wrongResume = Document(CareerProgressionKind.AwaitingEventChoice);
            wrongResume.progression.pendingEvent.resumeAtSlotNumber = 3;

            var unsafeGrowth = Document(CareerProgressionKind.AwaitingEventChoice);
            unsafeGrowth.progression.pendingEvent.options[0].growthExperienceDelta.spike =
                9007199254740992L;

            AssertRejected(oneOption);
            AssertRejected(missingGrowth);
            AssertRejected(wrongSourcePlan);
            AssertRejected(wrongResume);
            AssertRejected(unsafeGrowth);
        }

        [Test]
        public void ToDomain_RejectsReceiptTargetAndRecoveryContradictions()
        {
            var missingCreate = Document(CareerProgressionKind.Planned);
            var reduced = new OperationReceiptDocumentV1[missingCreate.operationReceipts.Length - 1];
            Array.Copy(missingCreate.operationReceipts, 1, reduced, 0, reduced.Length);
            missingCreate.operationReceipts = reduced;

            var extraCreateTargetField = Document(CareerProgressionKind.CareerCreated);
            extraCreateTargetField.operationReceipts[0].target.weekPlanId = GuidValue(10).ToString("D");

            var mismatchedTryoutChoice = Document(CareerProgressionKind.Tryout);
            FindReceiptDocument(mismatchedTryoutChoice, OperationKind.ConfirmTryoutStage)
                .target.choiceId = "different-choice";

            var brokenFrontier = Document(CareerProgressionKind.Planned);
            var eventIndex = FindReceiptIndex(brokenFrontier, OperationKind.ResolveEventChoice);
            var withoutEvent = new OperationReceiptDocumentV1[
                brokenFrontier.operationReceipts.Length - 1];
            var destination = 0;
            for (var index = 0; index < brokenFrontier.operationReceipts.Length; index++)
            {
                if (index != eventIndex)
                {
                    withoutEvent[destination++] = brokenFrontier.operationReceipts[index];
                }
            }

            brokenFrontier.operationReceipts = withoutEvent;

            var duplicateTarget = Document(CareerProgressionKind.Planned);
            var firstAction = FindReceiptIndex(duplicateTarget, OperationKind.ExecuteWeekAction);
            var secondAction = FindReceiptIndex(
                duplicateTarget,
                OperationKind.ExecuteWeekAction,
                firstAction + 1);
            duplicateTarget.operationReceipts[secondAction].target.slotActionId =
                duplicateTarget.operationReceipts[firstAction].target.slotActionId;
            duplicateTarget.operationReceipts[secondAction].target.actionOccurrenceId =
                duplicateTarget.operationReceipts[firstAction].target.actionOccurrenceId;

            AssertRejected(missingCreate);
            AssertRejected(extraCreateTargetField);
            AssertRejected(mismatchedTryoutChoice);
            AssertRejected(brokenFrontier);
            AssertRejected(duplicateTarget);
        }

        [Test]
        public void ToDomain_RejectsOutcomeSummaryShapeContradictions()
        {
            var missingSummary = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(missingSummary, OperationKind.CreateCareer).outcomeSummary = null;

            var createWithOutputs = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(createWithOutputs, OperationKind.CreateCareer)
                .outcomeSummary.tryoutResolvedOutputs = new[]
                {
                    new TryoutResolvedOutputDocumentV1
                    {
                        outputId = "unexpected",
                        perturbation = 1
                    }
                };

            var tryoutWithoutOutputs = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(tryoutWithoutOutputs, OperationKind.ConfirmTryoutStage)
                .outcomeSummary.tryoutResolvedOutputs = Array.Empty<TryoutResolvedOutputDocumentV1>();

            var planWithDelta = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(planWithDelta, OperationKind.ConfirmWeekPlan)
                .outcomeSummary.growthExperienceDelta =
                new CareerAttributeGrowthDeltaDocumentV1();

            var slotMissingStatus = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(slotMissingStatus, OperationKind.ExecuteWeekAction)
                .outcomeSummary.fatigueDelta = null;

            var eventWithOutputs = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(eventWithOutputs, OperationKind.ResolveEventChoice)
                .outcomeSummary.tryoutResolvedOutputs = new[]
                {
                    new TryoutResolvedOutputDocumentV1
                    {
                        outputId = "unexpected",
                        perturbation = 1
                    }
                };

            AssertRejected(missingSummary);
            AssertRejected(createWithOutputs);
            AssertRejected(tryoutWithoutOutputs);
            AssertRejected(planWithDelta);
            AssertRejected(slotMissingStatus);
            AssertRejected(eventWithOutputs);
        }

        [Test]
        public void ToDomain_RejectsInvalidRevisionFrontiers()
        {
            var createdAheadOfBusinessFrontier = Document(CareerProgressionKind.CareerCreated);
            createdAheadOfBusinessFrontier.identity.revision = 2;

            var plannedAheadOfBusinessFrontier = Document(CareerProgressionKind.Planned);
            plannedAheadOfBusinessFrontier.identity.revision = 9;

            var nonIncreasingChain = Document(CareerProgressionKind.Planned);
            FindReceiptDocument(nonIncreasingChain, OperationKind.ResolveEventChoice)
                .appliedRevision = 9;
            nonIncreasingChain.identity.revision = 9;

            AssertRejected(createdAheadOfBusinessFrontier);
            AssertRejected(plannedAheadOfBusinessFrontier);
            AssertRejected(nonIncreasingChain);
        }

        [Test]
        public void ToDomain_RejectsGlobalOccurrenceAndRandomVersionCollisions()
        {
            var pendingCollidesWithTryout = Document(
                CareerProgressionKind.AwaitingEventChoice);
            pendingCollidesWithTryout.progression.pendingEvent.occurrenceId =
                pendingCollidesWithTryout.onboarding.stages[0].occurrenceId;

            var planCollidesWithTryout = Document(CareerProgressionKind.Planning);
            planCollidesWithTryout.progression.weekPlan.slots[0].occurrenceId =
                planCollidesWithTryout.onboarding.stages[0].occurrenceId;

            var wrongTryoutRandom = Document(CareerProgressionKind.Tryout);
            wrongTryoutRandom.onboarding.stages[1].randomVersion = 2;

            var wrongEventRandom = Document(CareerProgressionKind.AwaitingEventChoice);
            wrongEventRandom.progression.pendingEvent.randomVersion = 2;

            var draftWithoutReservedMatch = Document(CareerProgressionKind.Planning);
            draftWithoutReservedMatch.progression.weekPlan.slots[2].kind = "rest";

            AssertRejected(pendingCollidesWithTryout);
            AssertRejected(planCollidesWithTryout);
            AssertRejected(wrongTryoutRandom);
            AssertRejected(wrongEventRandom);
            AssertRejected(draftWithoutReservedMatch);
        }

        [Test]
        public void ToDomain_RejectsProgressionOnboardingAndRosterContradictions()
        {
            var createdWithCompleteOnboarding = Document(CareerProgressionKind.CareerCreated);
            var complete = Document(CareerProgressionKind.Planning);
            createdWithCompleteOnboarding.onboarding = complete.onboarding;

            var tryoutStageMismatch = Document(CareerProgressionKind.Tryout);
            tryoutStageMismatch.progression.tryoutStage = 3;

            var plannedWithoutPlayer = Document(CareerProgressionKind.Planned);
            plannedWithoutPlayer.player = null;
            plannedWithoutPlayer.teamId = null;
            plannedWithoutPlayer.potentialGrade = null;
            plannedWithoutPlayer.fatigue = null;
            plannedWithoutPlayer.mindset = null;
            plannedWithoutPlayer.coachTrust = null;

            AssertRejected(createdWithCompleteOnboarding);
            AssertRejected(tryoutStageMismatch);
            AssertRejected(plannedWithoutPlayer);
        }

        private static CareerSaveDocumentV1 Document(CareerProgressionKind kind)
        {
            return CareerSaveSnapshotMapper.ToDocument(CreateSnapshot(kind));
        }

        private static void AssertRejected(CareerSaveDocumentV1 document)
        {
            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        private static CareerSaveSnapshot CreateSnapshot(
            CareerProgressionKind kind,
            bool useTrainingPlan = false)
        {
            var lineageId = new LineageId(GuidValue(3));
            var plan = kind == CareerProgressionKind.Planning
                ? CreatePlan(false, useTrainingPlan)
                : kind == CareerProgressionKind.Planned ||
                  kind == CareerProgressionKind.AwaitingEventChoice
                    ? CreatePlan(true, useTrainingPlan)
                    : null;
            var progression = CreateProgression(kind, plan);
            var onboarding = CreateOnboarding(kind);
            var includePlayer = kind == CareerProgressionKind.Planning ||
                                kind == CareerProgressionKind.Planned ||
                                kind == CareerProgressionKind.AwaitingEventChoice;
            var draft = new CareerPlayerDraft(new PlayerId("player.alpha"), "Lin", 12);

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    new ProfileId(GuidValue(1)),
                    new SaveId(GuidValue(2)),
                    lineageId,
                    SnapshotRevision(kind),
                    1000,
                    2000,
                    new Sha256Digest(new string('a', 64))),
                new CareerSeed(CreateSeedBytes()),
                "Road to V League",
                draft,
                onboarding,
                progression,
                EmphasesFor(progression),
                includePlayer ? CreatePlayer() : null,
                includePlayer ? (TeamId?)new TeamId("team.university-a") : null,
                includePlayer ? (PotentialGrade?)PotentialGrade.B : null,
                includePlayer ? (int?)23 : null,
                includePlayer ? (int?)72 : null,
                includePlayer ? (int?)61 : null,
                CreateReceipts(onboarding, progression, lineageId));
        }

        private static CareerProgressionState CreateProgression(
            CareerProgressionKind kind,
            CareerWeekPlanState plan)
        {
            switch (kind)
            {
                case CareerProgressionKind.CareerCreated:
                    return CareerProgressionState.Created();
                case CareerProgressionKind.Tryout:
                    return CareerProgressionState.Tryout(2);
                case CareerProgressionKind.Planning:
                    return CareerProgressionState.Planning(plan);
                case CareerProgressionKind.Planned:
                    return CareerProgressionState.Planned(plan, 3);
                case CareerProgressionKind.AwaitingEventChoice:
                    return CareerProgressionState.AwaitingEventChoice(
                        plan,
                        CreatePendingEvent(plan));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static TryoutOnboardingState CreateOnboarding(CareerProgressionKind kind)
        {
            if (kind == CareerProgressionKind.CareerCreated)
            {
                return ActiveOnboarding(1);
            }

            if (kind == CareerProgressionKind.Tryout)
            {
                return ActiveOnboarding(2);
            }

            return new TryoutOnboardingState(
                new[]
                {
                    ConfirmedStage(1),
                    ConfirmedStage(2),
                    ConfirmedStage(3)
                },
                0,
                true);
        }

        private static TryoutOnboardingState ActiveOnboarding(int currentStage)
        {
            var stages = new TryoutStageState[3];
            for (var stage = 1; stage <= stages.Length; stage++)
            {
                stages[stage - 1] = stage < currentStage
                    ? ConfirmedStage(stage)
                    : UnconfirmedStage(stage);
            }

            return new TryoutOnboardingState(stages, currentStage, false);
        }

        private static TryoutStageState ConfirmedStage(int stage)
        {
            return new TryoutStageState(
                stage,
                new OccurrenceId(GuidValue(100 + stage)),
                1,
                "stage-" + stage + "-choice",
                new[]
                {
                    new TryoutResolvedOutput("stage-" + stage + "-primary", 10),
                    new TryoutResolvedOutput("stage-" + stage + "-secondary", -5)
                });
        }

        private static TryoutStageState UnconfirmedStage(int stage)
        {
            return new TryoutStageState(
                stage,
                new OccurrenceId(GuidValue(100 + stage)),
                1,
                null,
                Array.Empty<TryoutResolvedOutput>());
        }

        private static CareerWeekPlanState CreatePlan(bool confirmed, bool useTrainingPlan = false)
        {
            return new CareerWeekPlanState(
                new WeekPlanId(GuidValue(10)),
                1,
                2,
                confirmed && useTrainingPlan
                    ? new[]
                    {
                        CreateAction(11, 41, CareerWeekActionKind.SpecializedTraining),
                        CreateAction(12, 42, CareerWeekActionKind.StrengthTraining),
                        CreateAction(13, 43, CareerWeekActionKind.Match)
                    }
                    : confirmed
                    ? new[]
                    {
                        CreateAction(11, 41, CareerWeekActionKind.TeamPractice),
                        CreateAction(12, 42, CareerWeekActionKind.Rest),
                        CreateAction(13, 43, CareerWeekActionKind.Match)
                    }
                    : new CareerWeekActionState[]
                    {
                        CreateAction(11, 41, CareerWeekActionKind.TeamPractice),
                        null,
                        CreateAction(13, 43, CareerWeekActionKind.Match)
                    },
                confirmed);
        }

        private static CareerWeekActionState CreateAction(
            int actionId,
            int occurrenceId,
            CareerWeekActionKind kind)
        {
            return new CareerWeekActionState(
                new SlotActionId(GuidValue(actionId)),
                new OccurrenceId(GuidValue(occurrenceId)),
                kind,
                ContentIdFor(kind));
        }

        private static string ContentIdFor(CareerWeekActionKind kind)
        {
            switch (kind)
            {
                case CareerWeekActionKind.SpecializedTraining: return "week_action.specialized.spike";
                case CareerWeekActionKind.StrengthTraining: return "week_action.strength.jump";
                case CareerWeekActionKind.TeamPractice: return "week_action.team_practice.standard";
                case CareerWeekActionKind.Rest: return "week_action.rest.standard";
                case CareerWeekActionKind.Match: return "schedule.u1w1.match.01";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static TrainingEmphasisLedger EmphasesFor(CareerProgressionState progression)
        {
            var count = progression.Kind == CareerProgressionKind.AwaitingEventChoice
                ? 1
                : progression.Kind == CareerProgressionKind.Planned
                    ? Math.Max(0, progression.NextSlotNumber - 1)
                    : 0;
            var result = TrainingEmphasisLedger.Empty;
            var catalog = CareerWeekActionCatalogV1.Create();
            for (var index = 0; index < count && index < 2; index++)
            {
                var action = progression.WeekPlan.Slots[index];
                var definition = catalog.Find(action.ContentId);
                if (definition.Direction.HasValue)
                {
                    result = result.AddExecutedTraining(action, catalog);
                }
            }

            return result;
        }

        private static PendingCareerEvent CreatePendingEvent(CareerWeekPlanState plan)
        {
            var source = plan.Slots[0];
            return new PendingCareerEvent(
                plan.PlanId,
                source.SlotActionId,
                source.OccurrenceId,
                "social.first-week",
                new OccurrenceId(GuidValue(50)),
                1,
                new CareerEventOptionEffect(
                    "accept",
                    new CareerAttributeGrowthDelta(10, 20, 30, 40, 50, 60, 70, 80),
                    5,
                    10,
                    -2),
                new CareerEventOptionEffect(
                    "decline",
                    new CareerAttributeGrowthDelta(1, 2, 3, 4, 5, 6, 7, 8),
                    -5,
                    -10,
                    2));
        }

        private static IReadOnlyList<OperationReceipt> CreateReceipts(
            TryoutOnboardingState onboarding,
            CareerProgressionState progression,
            LineageId lineageId)
        {
            var receipts = new List<OperationReceipt>
            {
                Receipt(
                    200,
                    1,
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    lineageId)
            };

            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                var stage = onboarding.Stages[index];
                if (stage.IsConfirmed)
                {
                    receipts.Add(
                        Receipt(
                            200 + stage.StageNumber,
                            1 + stage.StageNumber,
                            OperationKind.ConfirmTryoutStage,
                            OperationReceiptTarget.ForTryoutStage(
                                stage.StageNumber,
                                stage.OccurrenceId,
                                stage.ChoiceId),
                            lineageId));
                }
            }

            var plan = progression.WeekPlan;
            if (plan == null || !plan.IsConfirmed)
            {
                return receipts;
            }

            receipts.Add(
                Receipt(
                    210,
                    5,
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(plan.PlanId),
                    lineageId));
            if (progression.Kind == CareerProgressionKind.Planned ||
                progression.Kind == CareerProgressionKind.AwaitingEventChoice)
            {
                receipts.Add(ActionReceipt(211, 6, plan, 0, lineageId));
            }

            if (progression.Kind == CareerProgressionKind.Planned &&
                progression.NextSlotNumber >= 2)
            {
                var source = plan.Slots[0];
                receipts.Add(
                    Receipt(
                        212,
                        7,
                        OperationKind.ResolveEventChoice,
                        OperationReceiptTarget.ForEventChoice(
                            plan.PlanId,
                            source.SlotActionId,
                            source.OccurrenceId,
                            new OccurrenceId(GuidValue(50)),
                            "accept"),
                        lineageId));
            }

            if (progression.Kind == CareerProgressionKind.Planned &&
                progression.NextSlotNumber >= 3)
            {
                receipts.Add(ActionReceipt(213, 8, plan, 1, lineageId));
            }

            return receipts;
        }

        private static OperationReceipt ActionReceipt(
            int operationId,
            long revision,
            CareerWeekPlanState plan,
            int slotIndex,
            LineageId lineageId)
        {
            var action = plan.Slots[slotIndex];
            return Receipt(
                operationId,
                revision,
                OperationKind.ExecuteWeekAction,
                OperationReceiptTarget.ForWeekAction(
                    plan.PlanId,
                    action.SlotActionId,
                    action.OccurrenceId),
                lineageId);
        }

        private static OperationReceipt Receipt(
            int operationId,
            long revision,
            OperationKind kind,
            OperationReceiptTarget target,
            LineageId lineageId)
        {
            return new OperationReceipt(
                new OperationId(GuidValue(operationId)),
                kind,
                target,
                new Sha256Digest(new string((char)('a' + operationId % 6), 64)),
                lineageId,
                revision,
                3000 + operationId,
                OutcomeFor(kind),
                SummaryFor(kind, target));
        }

        private static OperationOutcomeSummary SummaryFor(
            OperationKind kind,
            OperationReceiptTarget target)
        {
            switch (kind)
            {
                case OperationKind.CreateCareer:
                    return OperationOutcomeSummary.ForCareerCreated();
                case OperationKind.ConfirmTryoutStage:
                    return OperationOutcomeSummary.ForTryoutAdvanced(
                        ConfirmedStage(target.TryoutStage).ResolvedOutputs);
                case OperationKind.ConfirmWeekPlan:
                    return OperationOutcomeSummary.ForWeekPlanConfirmed();
                case OperationKind.ExecuteWeekAction:
                    return OperationOutcomeSummary.ForSlotCompleted(
                        new CareerAttributeGrowthDelta(1, 2, 3, 4, 5, 6, 7, 8),
                        4,
                        1,
                        2);
                case OperationKind.ResolveEventChoice:
                    return OperationOutcomeSummary.ForEventChoiceApplied(
                        new CareerAttributeGrowthDelta(10, 20, 30, 40, 50, 60, 70, 80),
                        5,
                        10,
                        -2);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static long SnapshotRevision(CareerProgressionKind kind)
        {
            switch (kind)
            {
                case CareerProgressionKind.CareerCreated:
                    return 1;
                case CareerProgressionKind.Tryout:
                    return 2;
                case CareerProgressionKind.Planning:
                    return 5;
                case CareerProgressionKind.Planned:
                    return 8;
                case CareerProgressionKind.AwaitingEventChoice:
                    return 6;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static OperationOutcomeKind OutcomeFor(OperationKind kind)
        {
            switch (kind)
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
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static CareerPlayerRecord CreatePlayer()
        {
            return new CareerPlayerRecord(
                new PlayerId("player.alpha"),
                "Lin",
                12,
                new CareerPlayerAttributes(
                    new CareerAttributeProgress(5100, 101),
                    new CareerAttributeProgress(5200, 102),
                    new CareerAttributeProgress(5300, 103),
                    new CareerAttributeProgress(5400, 104),
                    new CareerAttributeProgress(5500, 105),
                    new CareerAttributeProgress(5600, 106),
                    new CareerAttributeProgress(5700, 107),
                    new CareerAttributeProgress(5800, 108)));
        }

        private static byte[] CreateSeedBytes()
        {
            var seed = new byte[CareerSeed.ByteLength];
            for (var index = 0; index < seed.Length; index++)
            {
                seed[index] = (byte)index;
            }

            return seed;
        }

        private static Guid GuidValue(int value)
        {
            return Guid.Parse("00000000-0000-0000-0000-" + value.ToString("D12"));
        }

        private static OperationReceipt FindReceipt(
            CareerSaveSnapshot snapshot,
            OperationKind kind)
        {
            for (var index = 0; index < snapshot.OperationReceipts.Count; index++)
            {
                if (snapshot.OperationReceipts[index].OperationKind == kind)
                {
                    return snapshot.OperationReceipts[index];
                }
            }

            throw new AssertionException("Receipt not found: " + kind);
        }

        private static OperationReceiptDocumentV1 FindReceiptDocument(
            CareerSaveDocumentV1 document,
            OperationKind kind)
        {
            return document.operationReceipts[FindReceiptIndex(document, kind)];
        }

        private static int FindReceiptIndex(
            CareerSaveDocumentV1 document,
            OperationKind kind,
            int startIndex = 0)
        {
            var name = OperationName(kind);
            for (var index = startIndex; index < document.operationReceipts.Length; index++)
            {
                if (document.operationReceipts[index].operationKind == name)
                {
                    return index;
                }
            }

            throw new AssertionException("Receipt document not found: " + kind);
        }

        private static void AssertEquivalent(CareerSaveSnapshot expected, CareerSaveSnapshot actual)
        {
            Assert.That(actual.Versions.SchemaVersion, Is.EqualTo(expected.Versions.SchemaVersion));
            Assert.That(actual.Versions.ContentVersion, Is.EqualTo(expected.Versions.ContentVersion));
            Assert.That(actual.Versions.RulesetVersion, Is.EqualTo(expected.Versions.RulesetVersion));
            Assert.That(actual.Versions.CareerRandomAlgorithmVersion,
                Is.EqualTo(expected.Versions.CareerRandomAlgorithmVersion));
            Assert.That(actual.Identity.ProfileId, Is.EqualTo(expected.Identity.ProfileId));
            Assert.That(actual.Identity.SaveId, Is.EqualTo(expected.Identity.SaveId));
            Assert.That(actual.Identity.LineageId, Is.EqualTo(expected.Identity.LineageId));
            Assert.That(actual.Identity.Revision, Is.EqualTo(expected.Identity.Revision));
            Assert.That(actual.Identity.CreatedAtUtcMs, Is.EqualTo(expected.Identity.CreatedAtUtcMs));
            Assert.That(actual.Identity.UpdatedAtUtcMs, Is.EqualTo(expected.Identity.UpdatedAtUtcMs));
            Assert.That(actual.Identity.SnapshotHash, Is.EqualTo(expected.Identity.SnapshotHash));
            Assert.That(
                actual.Identity.RestoredFromVersionToken,
                Is.EqualTo(expected.Identity.RestoredFromVersionToken));
            Assert.That(actual.CareerSeed, Is.EqualTo(expected.CareerSeed));
            Assert.That(actual.CareerName, Is.EqualTo(expected.CareerName));
            Assert.That(actual.PlayerDraft.PlayerId, Is.EqualTo(expected.PlayerDraft.PlayerId));
            Assert.That(actual.PlayerDraft.DisplayName, Is.EqualTo(expected.PlayerDraft.DisplayName));
            Assert.That(actual.PlayerDraft.JerseyNumber, Is.EqualTo(expected.PlayerDraft.JerseyNumber));
            AssertOnboardingEquivalent(expected.Onboarding, actual.Onboarding);
            AssertProgressionEquivalent(expected.Progression, actual.Progression);
            Assert.That(actual.TrainingEmphases.Contributions.Count,
                Is.EqualTo(expected.TrainingEmphases.Contributions.Count));
            for (var index = 0; index < expected.TrainingEmphases.Contributions.Count; index++)
            {
                Assert.That(actual.TrainingEmphases.Contributions[index].SourceSlotActionId,
                    Is.EqualTo(expected.TrainingEmphases.Contributions[index].SourceSlotActionId));
                Assert.That(actual.TrainingEmphases.Contributions[index].Direction,
                    Is.EqualTo(expected.TrainingEmphases.Contributions[index].Direction));
                Assert.That(actual.TrainingEmphases.Contributions[index].BonusBasisPoints,
                    Is.EqualTo(expected.TrainingEmphases.Contributions[index].BonusBasisPoints));
            }
            AssertPlayerEquivalent(expected.Player, actual.Player);
            Assert.That(actual.TeamId, Is.EqualTo(expected.TeamId));
            Assert.That(actual.PotentialGrade, Is.EqualTo(expected.PotentialGrade));
            Assert.That(actual.Fatigue, Is.EqualTo(expected.Fatigue));
            Assert.That(actual.Mindset, Is.EqualTo(expected.Mindset));
            Assert.That(actual.CoachTrust, Is.EqualTo(expected.CoachTrust));
            Assert.That(actual.OperationReceipts.Count, Is.EqualTo(expected.OperationReceipts.Count));
            for (var index = 0; index < expected.OperationReceipts.Count; index++)
            {
                AssertReceiptEquivalent(expected.OperationReceipts[index], actual.OperationReceipts[index]);
            }
        }

        private static void AssertOnboardingEquivalent(
            TryoutOnboardingState expected,
            TryoutOnboardingState actual)
        {
            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.NextStageNumber, Is.EqualTo(expected.NextStageNumber));
            Assert.That(actual.IsFormallyEnrolled, Is.EqualTo(expected.IsFormallyEnrolled));
            Assert.That(actual.Stages.Count, Is.EqualTo(expected.Stages.Count));
            for (var stageIndex = 0; stageIndex < expected.Stages.Count; stageIndex++)
            {
                var expectedStage = expected.Stages[stageIndex];
                var actualStage = actual.Stages[stageIndex];
                Assert.That(actualStage.StageNumber, Is.EqualTo(expectedStage.StageNumber));
                Assert.That(actualStage.OccurrenceId, Is.EqualTo(expectedStage.OccurrenceId));
                Assert.That(actualStage.RandomVersion, Is.EqualTo(expectedStage.RandomVersion));
                Assert.That(actualStage.ChoiceId, Is.EqualTo(expectedStage.ChoiceId));
                Assert.That(actualStage.ResolvedOutputs.Count,
                    Is.EqualTo(expectedStage.ResolvedOutputs.Count));
                for (var outputIndex = 0;
                     outputIndex < expectedStage.ResolvedOutputs.Count;
                     outputIndex++)
                {
                    Assert.That(actualStage.ResolvedOutputs[outputIndex].OutputId,
                        Is.EqualTo(expectedStage.ResolvedOutputs[outputIndex].OutputId));
                    Assert.That(actualStage.ResolvedOutputs[outputIndex].Perturbation,
                        Is.EqualTo(expectedStage.ResolvedOutputs[outputIndex].Perturbation));
                }
            }
        }

        private static void AssertProgressionEquivalent(
            CareerProgressionState expected,
            CareerProgressionState actual)
        {
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.TryoutStage, Is.EqualTo(expected.TryoutStage));
            Assert.That(actual.NextSlotNumber, Is.EqualTo(expected.NextSlotNumber));
            AssertPlanEquivalent(expected.WeekPlan, actual.WeekPlan);
            AssertPendingEventEquivalent(expected.PendingEvent, actual.PendingEvent);
        }

        private static void AssertPlanEquivalent(
            CareerWeekPlanState expected,
            CareerWeekPlanState actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.PlanId, Is.EqualTo(expected.PlanId));
            Assert.That(actual.Season, Is.EqualTo(expected.Season));
            Assert.That(actual.Week, Is.EqualTo(expected.Week));
            Assert.That(actual.IsConfirmed, Is.EqualTo(expected.IsConfirmed));
            for (var index = 0; index < expected.Slots.Count; index++)
            {
                var expectedSlot = expected.Slots[index];
                var actualSlot = actual.Slots[index];
                if (expectedSlot == null)
                {
                    Assert.That(actualSlot, Is.Null);
                }
                else
                {
                    Assert.That(actualSlot.SlotActionId, Is.EqualTo(expectedSlot.SlotActionId));
                    Assert.That(actualSlot.OccurrenceId, Is.EqualTo(expectedSlot.OccurrenceId));
                    Assert.That(actualSlot.Kind, Is.EqualTo(expectedSlot.Kind));
                    Assert.That(actualSlot.ContentId, Is.EqualTo(expectedSlot.ContentId));
                }
            }
        }

        private static void AssertPendingEventEquivalent(
            PendingCareerEvent expected,
            PendingCareerEvent actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual.SourceWeekPlanId, Is.EqualTo(expected.SourceWeekPlanId));
            Assert.That(actual.SourceSlotActionId, Is.EqualTo(expected.SourceSlotActionId));
            Assert.That(actual.SourceActionOccurrenceId, Is.EqualTo(expected.SourceActionOccurrenceId));
            Assert.That(actual.EventId, Is.EqualTo(expected.EventId));
            Assert.That(actual.OccurrenceId, Is.EqualTo(expected.OccurrenceId));
            Assert.That(actual.RandomVersion, Is.EqualTo(expected.RandomVersion));
            Assert.That(actual.ResumeAtSlotNumber, Is.EqualTo(expected.ResumeAtSlotNumber));
            Assert.That(actual.Options.Count, Is.EqualTo(expected.Options.Count));
            for (var index = 0; index < expected.Options.Count; index++)
            {
                AssertEffectEquivalent(expected.Options[index], actual.Options[index]);
            }
        }

        private static void AssertEffectEquivalent(
            CareerEventOptionEffect expected,
            CareerEventOptionEffect actual)
        {
            Assert.That(actual.OptionId, Is.EqualTo(expected.OptionId));
            Assert.That(actual.GrowthExperienceDelta.Spike,
                Is.EqualTo(expected.GrowthExperienceDelta.Spike));
            Assert.That(actual.GrowthExperienceDelta.Serve,
                Is.EqualTo(expected.GrowthExperienceDelta.Serve));
            Assert.That(actual.GrowthExperienceDelta.Reception,
                Is.EqualTo(expected.GrowthExperienceDelta.Reception));
            Assert.That(actual.GrowthExperienceDelta.Defense,
                Is.EqualTo(expected.GrowthExperienceDelta.Defense));
            Assert.That(actual.GrowthExperienceDelta.Block,
                Is.EqualTo(expected.GrowthExperienceDelta.Block));
            Assert.That(actual.GrowthExperienceDelta.Movement,
                Is.EqualTo(expected.GrowthExperienceDelta.Movement));
            Assert.That(actual.GrowthExperienceDelta.Jump,
                Is.EqualTo(expected.GrowthExperienceDelta.Jump));
            Assert.That(actual.GrowthExperienceDelta.Stamina,
                Is.EqualTo(expected.GrowthExperienceDelta.Stamina));
            Assert.That(actual.FatigueDelta, Is.EqualTo(expected.FatigueDelta));
            Assert.That(actual.MindsetDelta, Is.EqualTo(expected.MindsetDelta));
            Assert.That(actual.CoachTrustDelta, Is.EqualTo(expected.CoachTrustDelta));
        }

        private static void AssertPlayerEquivalent(
            CareerPlayerRecord expected,
            CareerPlayerRecord actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.PlayerId, Is.EqualTo(expected.PlayerId));
            Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName));
            Assert.That(actual.JerseyNumber, Is.EqualTo(expected.JerseyNumber));
            Assert.That(actual.Attributes, Is.EqualTo(expected.Attributes));
        }

        private static void AssertReceiptEquivalent(OperationReceipt expected, OperationReceipt actual)
        {
            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.OperationId, Is.EqualTo(expected.OperationId));
            Assert.That(actual.OperationKind, Is.EqualTo(expected.OperationKind));
            Assert.That(actual.InputFingerprint, Is.EqualTo(expected.InputFingerprint));
            Assert.That(actual.AppliedLineageId, Is.EqualTo(expected.AppliedLineageId));
            Assert.That(actual.AppliedRevision, Is.EqualTo(expected.AppliedRevision));
            Assert.That(actual.CompletedAtUtcMs, Is.EqualTo(expected.CompletedAtUtcMs));
            Assert.That(actual.OutcomeKind, Is.EqualTo(expected.OutcomeKind));
            Assert.That(actual.Target.TryoutStage, Is.EqualTo(expected.Target.TryoutStage));
            Assert.That(actual.Target.TryoutOccurrenceId, Is.EqualTo(expected.Target.TryoutOccurrenceId));
            Assert.That(actual.Target.ChoiceId, Is.EqualTo(expected.Target.ChoiceId));
            Assert.That(actual.Target.WeekPlanId, Is.EqualTo(expected.Target.WeekPlanId));
            Assert.That(actual.Target.SlotActionId, Is.EqualTo(expected.Target.SlotActionId));
            Assert.That(actual.Target.ActionOccurrenceId, Is.EqualTo(expected.Target.ActionOccurrenceId));
            Assert.That(actual.Target.EventOccurrenceId, Is.EqualTo(expected.Target.EventOccurrenceId));
            Assert.That(actual.Target.OptionId, Is.EqualTo(expected.Target.OptionId));
            AssertSummaryEquivalent(expected.OutcomeSummary, actual.OutcomeSummary);
        }

        private static void AssertSummaryEquivalent(
            OperationOutcomeSummary expected,
            OperationOutcomeSummary actual)
        {
            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.OutcomeKind, Is.EqualTo(expected.OutcomeKind));
            Assert.That(actual.TryoutResolvedOutputs.Count,
                Is.EqualTo(expected.TryoutResolvedOutputs.Count));
            for (var index = 0; index < expected.TryoutResolvedOutputs.Count; index++)
            {
                Assert.That(actual.TryoutResolvedOutputs[index].OutputId,
                    Is.EqualTo(expected.TryoutResolvedOutputs[index].OutputId));
                Assert.That(actual.TryoutResolvedOutputs[index].Perturbation,
                    Is.EqualTo(expected.TryoutResolvedOutputs[index].Perturbation));
            }

            if (expected.GrowthExperienceDelta == null)
            {
                Assert.That(actual.GrowthExperienceDelta, Is.Null);
            }
            else
            {
                Assert.That(actual.GrowthExperienceDelta.Spike,
                    Is.EqualTo(expected.GrowthExperienceDelta.Spike));
                Assert.That(actual.GrowthExperienceDelta.Serve,
                    Is.EqualTo(expected.GrowthExperienceDelta.Serve));
                Assert.That(actual.GrowthExperienceDelta.Reception,
                    Is.EqualTo(expected.GrowthExperienceDelta.Reception));
                Assert.That(actual.GrowthExperienceDelta.Defense,
                    Is.EqualTo(expected.GrowthExperienceDelta.Defense));
                Assert.That(actual.GrowthExperienceDelta.Block,
                    Is.EqualTo(expected.GrowthExperienceDelta.Block));
                Assert.That(actual.GrowthExperienceDelta.Movement,
                    Is.EqualTo(expected.GrowthExperienceDelta.Movement));
                Assert.That(actual.GrowthExperienceDelta.Jump,
                    Is.EqualTo(expected.GrowthExperienceDelta.Jump));
                Assert.That(actual.GrowthExperienceDelta.Stamina,
                    Is.EqualTo(expected.GrowthExperienceDelta.Stamina));
            }

            Assert.That(actual.FatigueDelta, Is.EqualTo(expected.FatigueDelta));
            Assert.That(actual.MindsetDelta, Is.EqualTo(expected.MindsetDelta));
            Assert.That(actual.CoachTrustDelta, Is.EqualTo(expected.CoachTrustDelta));
        }

        private static string ProgressionName(CareerProgressionKind kind)
        {
            switch (kind)
            {
                case CareerProgressionKind.CareerCreated:
                    return "career_created";
                case CareerProgressionKind.Tryout:
                    return "tryout";
                case CareerProgressionKind.Planning:
                    return "planning";
                case CareerProgressionKind.Planned:
                    return "planned";
                case CareerProgressionKind.AwaitingEventChoice:
                    return "awaiting_event_choice";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static string OperationName(OperationKind kind)
        {
            switch (kind)
            {
                case OperationKind.CreateCareer:
                    return "create_career";
                case OperationKind.ConfirmTryoutStage:
                    return "confirm_tryout_stage";
                case OperationKind.ConfirmWeekPlan:
                    return "confirm_week_plan";
                case OperationKind.ExecuteWeekAction:
                    return "execute_week_action";
                case OperationKind.ResolveEventChoice:
                    return "resolve_event_choice";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
