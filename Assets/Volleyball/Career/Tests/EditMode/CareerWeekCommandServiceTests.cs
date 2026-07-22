using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekCommandServiceTests
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        private static readonly ProfileId Profile =
            new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private static readonly SaveId Save =
            new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private static readonly LineageId Lineage =
            new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        private static readonly OperationId Operation =
            new OperationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        [Test]
        public void ConfirmWeekPlan_AppliesExactlyOneCompleteRevisionFiveSnapshot()
        {
            var prior = PlanningSnapshot();
            var command = Command(prior);
            var repository = new MemoryRepository(prior);

            var result = Service(repository).ConfirmWeekPlan(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(result.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(result.OutcomeSummary, Is.Not.Null);
            Assert.That(result.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));

            var next = result.Snapshot;
            Assert.That(next.Identity.Revision, Is.EqualTo(5));
            Assert.That(next.Identity.UpdatedAtUtcMs, Is.EqualTo(command.CompletedAtUtcMs));
            Assert.That(next.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
            Assert.That(next.Progression.NextSlotNumber, Is.EqualTo(1));
            AssertPlanEqual(command.CandidatePlan, next.Progression.WeekPlan);
            Assert.That(next.TrainingEmphases.Contributions, Is.Empty);
            AssertUnchanged(prior, next);
            Assert.That(next.OperationReceipts, Has.Count.EqualTo(prior.OperationReceipts.Count + 1));
            CollectionAssert.AreEqual(
                prior.OperationReceipts.Select(receipt => receipt.OperationId),
                next.OperationReceipts.Take(prior.OperationReceipts.Count).Select(receipt => receipt.OperationId));

            var receipt = next.OperationReceipts[next.OperationReceipts.Count - 1];
            Assert.That(receipt.OperationId, Is.EqualTo(command.OperationId));
            Assert.That(receipt.OperationKind, Is.EqualTo(OperationKind.ConfirmWeekPlan));
            Assert.That(receipt.Target.WeekPlanId, Is.EqualTo(command.CandidatePlan.PlanId));
            Assert.That(receipt.InputFingerprint, Is.EqualTo(CareerOperationFingerprintV1.Hash(command)));
            Assert.That(receipt.AppliedLineageId, Is.EqualTo(Lineage));
            Assert.That(receipt.AppliedRevision, Is.EqualTo(5));
            Assert.That(receipt.CompletedAtUtcMs, Is.EqualTo(command.CompletedAtUtcMs));
            Assert.That(receipt.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
            Assert.That(receipt.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
            Assert.That(result.OutcomeSummary.OutcomeKind, Is.EqualTo(receipt.OutcomeSummary.OutcomeKind));
        }

        [Test]
        public void ConfirmWeekPlan_RejectsMalformedCommandShapeBeforeLoading()
        {
            var prior = PlanningSnapshot();
            var malformedCandidate = Command(prior);
            CorruptSlot(malformedCandidate.CandidatePlan, 0, null);
            var incompleteCandidate = new CareerWeekPlanState(
                prior.Progression.WeekPlan.PlanId,
                1,
                1,
                new CareerWeekActionState[] { null, null, prior.Progression.WeekPlan.Slots[2] },
                false);
            var commands = new ConfirmWeekPlanCommand[]
            {
                null,
                Command(prior, profileId: new ProfileId?(default(ProfileId))),
                Command(prior, saveId: new SaveId?(default(SaveId))),
                Command(prior, expectedVersionToken: new CareerVersionToken?(default(CareerVersionToken))),
                Command(prior, operationId: new OperationId?(default(OperationId))),
                Command(prior, completedAtUtcMs: -1),
                Command(prior, completedAtUtcMs: 9007199254740992L),
                Command(prior, candidate: null, useNullCandidate: true),
                Command(prior, candidate: incompleteCandidate),
                malformedCandidate
            };

            foreach (var command in commands)
            {
                var repository = new MemoryRepository(prior);
                var result = Service(repository).ConfirmWeekPlan(command);

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
                Assert.That(result.Snapshot, Is.Null);
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.LoadCount, Is.Zero);
                Assert.That(repository.CommitCount, Is.Zero);
            }
        }

        [Test]
        public void ConfirmWeekPlan_RejectsInvalidCandidateIdentityContentLayoutAndCollisions()
        {
            var prior = PlanningSnapshot();
            var unconfirmed = Candidate(prior, isConfirmed: false);
            var duplicateSlot = Command(prior);
            CorruptSlot(duplicateSlot.CandidatePlan, 1, duplicateSlot.CandidatePlan.Slots[0]);
            var matchInFreeSlot = Command(prior);
            CorruptSlot(matchInFreeSlot.CandidatePlan, 0, matchInFreeSlot.CandidatePlan.Slots[2]);
            var cases = new[]
            {
                Command(prior, candidate: unconfirmed),
                Command(prior, candidate: Candidate(prior, planId: WeekPlan(90))),
                Command(prior, candidate: Candidate(prior, season: 2)),
                Command(prior, candidate: Candidate(prior, week: 2)),
                Command(prior, candidate: Candidate(prior, matchSlotActionId: Slot(91))),
                Command(prior, candidate: Candidate(prior, matchOccurrenceId: Occurrence(92))),
                Command(prior, candidate: Candidate(prior, matchContentId: "schedule.u1w1.match.changed")),
                Command(prior, corruptAfterCopy: true),
                Command(prior, candidate: Candidate(prior, firstContentId: "week_action.unknown")),
                Command(prior, candidate: Candidate(
                    prior,
                    firstKind: CareerWeekActionKind.Rest,
                    firstContentId: "week_action.specialized.spike")),
                duplicateSlot,
                matchInFreeSlot,
                Command(prior, candidate: Candidate(
                    prior,
                    firstOccurrenceId: prior.Onboarding.Stages[0].OccurrenceId))
            };

            foreach (var command in cases)
            {
                var repository = new MemoryRepository(prior);
                var result = Service(repository).ConfirmWeekPlan(command);

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
                Assert.That(result.Snapshot, Is.SameAs(prior));
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.CommitCount, Is.Zero);
            }
        }

        [Test]
        public void ConfirmWeekPlan_RejectsWrongOwnershipStateVersionsAndTime()
        {
            var prior = PlanningSnapshot();
            var applied = AppliedSnapshot(prior, Command(prior));
            var unsupported = CopySnapshot(
                prior,
                versions: new CareerSaveVersions(2, 1, 1, 1));
            var cases = new[]
            {
                new InvalidCase(prior, Command(prior, profileId: new ProfileId(Guid.Parse("abababab-abab-abab-abab-abababababab")))),
                new InvalidCase(prior, Command(prior, saveId: new SaveId(Guid.Parse("acacacac-acac-acac-acac-acacacacacac")))),
                new InvalidCase(
                    applied,
                    Command(
                        applied,
                        candidate: applied.Progression.WeekPlan,
                        operationId: new OperationId(Guid.Parse("adadadad-adad-adad-adad-adadadadadad")))),
                new InvalidCase(unsupported, Command(unsupported)),
                new InvalidCase(prior, Command(prior, completedAtUtcMs: prior.Identity.UpdatedAtUtcMs - 1))
            };

            foreach (var testCase in cases)
            {
                var repository = new MemoryRepository(testCase.Snapshot);
                var result = Service(repository).ConfirmWeekPlan(testCase.Command);

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
                Assert.That(result.Snapshot, Is.SameAs(testCase.Snapshot));
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.CommitCount, Is.Zero);
            }
        }

        [Test]
        public void ConfirmWeekPlan_ExactRetryAndCompletionTimeOnlyRetryReturnExistingSummary()
        {
            var prior = PlanningSnapshot();
            var command = Command(prior);
            var repository = new MemoryRepository(prior);
            var service = Service(repository);
            var applied = service.ConfirmWeekPlan(command);
            var changedTime = Command(
                prior,
                completedAtUtcMs: command.CompletedAtUtcMs + 1000);

            var exact = service.ConfirmWeekPlan(command);
            var metadataOnly = service.ConfirmWeekPlan(changedTime);

            Assert.That(applied.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(exact.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(metadataOnly.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(exact.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(metadataOnly.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(exact.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
            Assert.That(metadataOnly.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmWeekPlan_SameOperationDifferentBusinessInputConflicts()
        {
            var prior = PlanningSnapshot();
            var original = Command(prior);
            var repository = new MemoryRepository(prior);
            var service = Service(repository);
            service.ConfirmWeekPlan(original);
            var changed = Command(
                prior,
                candidate: Candidate(prior, firstContentId: "week_action.specialized.serve"));

            var result = service.ConfirmWeekPlan(changed);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.OperationConflict));
            Assert.That(result.ConflictingReceipt, Is.Not.Null);
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmWeekPlan_StaleDifferentOperationReturnsAuthoritativeVersionConflict()
        {
            var prior = PlanningSnapshot();
            var repository = new MemoryRepository(prior);
            var service = Service(repository);
            service.ConfirmWeekPlan(Command(prior));
            var stale = Command(
                prior,
                operationId: new OperationId(Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd")));

            var result = service.ConfirmWeekPlan(stale);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.VersionConflict));
            Assert.That(result.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmWeekPlan_CommitRaceRechecksExactConflictingAndMissingReceipts()
        {
            var prior = PlanningSnapshot();
            var command = Command(prior);
            var exact = AppliedSnapshot(prior, command);
            var conflictingCommand = Command(
                prior,
                candidate: Candidate(prior, firstContentId: "week_action.specialized.serve"));
            var conflicting = AppliedSnapshot(prior, conflictingCommand);
            var missing = CopySnapshot(
                prior,
                identity: new CareerSaveIdentity(
                    Profile,
                    Save,
                    Lineage,
                    5,
                    prior.Identity.CreatedAtUtcMs,
                    202,
                    prior.Identity.SnapshotHash));

            AssertRace(command, prior, exact, CareerApplicationStatus.Existing, hasConflict: false);
            AssertRace(command, prior, conflicting, CareerApplicationStatus.OperationConflict, hasConflict: true);
            AssertRace(command, prior, missing, CareerApplicationStatus.VersionConflict, hasConflict: false);
        }

        [Test]
        public void ConfirmWeekPlan_NotFoundLoadAndCommitFailuresNeverExposeSpeculativeRevisionFive()
        {
            var prior = PlanningSnapshot();
            var notFoundRepository = new MemoryRepository(null);
            var notFound = Service(notFoundRepository)
                .ConfirmWeekPlan(Command(prior));
            Assert.That(notFound.Status, Is.EqualTo(CareerApplicationStatus.NotFound));
            Assert.That(notFound.PersistenceKind, Is.EqualTo(PersistenceResultKind.NotFound));
            Assert.That(notFound.Snapshot, Is.Null);

            var loadFailureRepository = new MemoryRepository(prior)
            {
                LoadFailure = PersistenceResultKind.IoFailure
            };
            var loadFailure = Service(loadFailureRepository)
                .ConfirmWeekPlan(Command(prior));
            Assert.That(loadFailure.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(loadFailure.PersistenceKind, Is.EqualTo(PersistenceResultKind.IoFailure));
            Assert.That(loadFailure.Snapshot, Is.Null);

            foreach (var failureKind in new[]
                     {
                         PersistenceResultKind.NotCommitted,
                         PersistenceResultKind.IoFailure,
                         PersistenceResultKind.LockUnavailable
                     })
            {
                var repository = new MemoryRepository(prior) { CommitFailure = failureKind };
                var result = Service(repository).ConfirmWeekPlan(Command(prior));

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
                Assert.That(result.PersistenceKind, Is.EqualTo(failureKind));
                Assert.That(result.Snapshot, Is.SameAs(prior));
                Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(4));
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.Snapshot, Is.SameAs(prior));
                Assert.That(repository.CommitCount, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfirmWeekPlan_InitialLoadNullOrThrowReturnsPersistenceFailureWithoutSnapshot(
            bool throws)
        {
            var prior = PlanningSnapshot();
            var repository = new MemoryRepository(prior)
            {
                NullLoadCallNumber = throws ? (int?)null : 1,
                ThrowLoadCallNumber = throws ? 1 : (int?)null
            };
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() =>
                result = Service(repository)
                    .ConfirmWeekPlan(Command(prior)));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.PersistenceKind, Is.Null);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfirmWeekPlan_CommitNullOrThrowReturnsPriorAuthoritativeSnapshot(bool throws)
        {
            var prior = PlanningSnapshot();
            var repository = new MemoryRepository(prior)
            {
                ReturnNullCommit = !throws,
                ThrowCommit = throws
            };
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() =>
                result = Service(repository)
                    .ConfirmWeekPlan(Command(prior)));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.PersistenceKind, Is.Null);
            Assert.That(result.Snapshot, Is.SameAs(prior));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(4));
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.Snapshot, Is.SameAs(prior));
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfirmWeekPlan_CommitRaceReloadNullOrThrowReturnsPersistenceFailureWithoutRevisionFive(
            bool throws)
        {
            var prior = PlanningSnapshot();
            var winner = AppliedSnapshot(prior, Command(prior));
            var repository = new MemoryRepository(prior)
            {
                CommitRaceWinner = winner,
                NullLoadCallNumber = throws ? (int?)null : 2,
                ThrowLoadCallNumber = throws ? 2 : (int?)null
            };
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() =>
                result = Service(repository)
                    .ConfirmWeekPlan(Command(prior)));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.PersistenceKind, Is.Null);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.LoadCount, Is.EqualTo(2));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmWeekPlan_SuccessKindWithoutMatchingReceiptReturnsPersistenceFailure()
        {
            var prior = PlanningSnapshot();
            var missingReceiptPayload = AppliedSnapshot(
                prior,
                Command(
                    prior,
                    operationId: new OperationId(
                        Guid.Parse("edededed-eded-eded-eded-edededededed"))));
            foreach (var successKind in new[]
                     {
                         PersistenceResultKind.Committed,
                         PersistenceResultKind.BackupDegraded
                     })
            {
                var repository = new MemoryRepository(prior)
                {
                    SuccessfulCommitKind = successKind,
                    SuccessfulCommitPayload = missingReceiptPayload
                };

                CareerWeekCommandResult result = null;
                Assert.DoesNotThrow(() =>
                    result = Service(repository)
                        .ConfirmWeekPlan(Command(prior)));

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
                Assert.That(result.PersistenceKind, Is.EqualTo(successKind));
                Assert.That(result.Snapshot, Is.SameAs(prior));
                Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(4));
                Assert.That(result.ConflictingReceipt, Is.Null);
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.LoadCount, Is.EqualTo(1));
                Assert.That(repository.CommitCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ConfirmWeekPlan_BackupDegradedCommitIsAppliedWithPersistedSummary()
        {
            var prior = PlanningSnapshot();
            var repository = new MemoryRepository(prior)
            {
                SuccessfulCommitKind = PersistenceResultKind.BackupDegraded
            };

            var result = Service(repository).ConfirmWeekPlan(Command(prior));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.BackupDegraded));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(5));
            Assert.That(result.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
        }

        [Test]
        public void ExecuteWeekAction_SlotOneAppliesActionAndFreezesExactEventWithoutApplyingIt()
        {
            var prior = ConfirmedSnapshot();
            var command = ExecuteCommand(prior, 1);
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var repository = new MemoryRepository(prior);

            var result = Service(repository, random).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(6));
            Assert.That(result.Snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.AwaitingEventChoice));
            Assert.That(result.Snapshot.Progression.NextSlotNumber, Is.Zero);
            Assert.That(result.Snapshot.Player.Attributes.Spike.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Spike.GrowthExperience + 132));
            Assert.That(result.Snapshot.Fatigue.Value, Is.EqualTo(31));
            Assert.That(result.Snapshot.Mindset.Value, Is.EqualTo(47));
            Assert.That(result.Snapshot.CoachTrust.Value, Is.EqualTo(71));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(1));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[0].SourceSlotActionId,
                Is.EqualTo(command.SlotActionId));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[0].Direction,
                Is.EqualTo(CareerTrainingDirection.Spike));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[0].BonusBasisPoints,
                Is.EqualTo(1000));

            Assert.That(random.Calls, Has.Count.EqualTo(2));
            AssertRandomCall(
                random.Calls[0],
                "event.team_meal.option.attend",
                "498a5ad33f7737a79b2d489870aa5b9c32a287a44c5af6d94bad45877cd9de8d",
                6791);
            AssertRandomCall(
                random.Calls[1],
                "event.team_meal.option.extra_practice",
                "505b9fbea8a2ef2df237fdd58b1e2fe36c81a22af753e6314214795ad0d98318",
                7549);

            var pending = result.Snapshot.Progression.PendingEvent;
            Assert.That(pending.SourceWeekPlanId, Is.EqualTo(command.WeekPlanId));
            Assert.That(pending.SourceSlotActionId, Is.EqualTo(command.SlotActionId));
            Assert.That(pending.SourceActionOccurrenceId, Is.EqualTo(command.ActionOccurrenceId));
            Assert.That(pending.EventId, Is.EqualTo("event.team_meal"));
            Assert.That(pending.OccurrenceId, Is.EqualTo(command.TriggeredEventOccurrenceId.Value));
            Assert.That(pending.RandomVersion, Is.EqualTo(1));
            Assert.That(pending.ResumeAtSlotNumber, Is.EqualTo(2));
            Assert.That(pending.Options.Select(option => option.OptionId), Is.EqualTo(new[]
            {
                "event.team_meal.option.attend",
                "event.team_meal.option.extra_practice"
            }));
            AssertEffect(pending.Options[0], 0, 4, 6, 3);
            AssertEffect(pending.Options[1], 88, 10, -2, 6);

            var receipt = result.Snapshot.OperationReceipts.Last();
            Assert.That(receipt.OperationId, Is.EqualTo(command.OperationId));
            Assert.That(receipt.OperationKind, Is.EqualTo(OperationKind.ExecuteWeekAction));
            Assert.That(receipt.Target.WeekPlanId, Is.EqualTo(command.WeekPlanId));
            Assert.That(receipt.Target.SlotActionId, Is.EqualTo(command.SlotActionId));
            Assert.That(receipt.Target.ActionOccurrenceId, Is.EqualTo(command.ActionOccurrenceId));
            Assert.That(receipt.InputFingerprint, Is.EqualTo(CareerOperationFingerprintV1.Hash(command)));
            Assert.That(receipt.AppliedRevision, Is.EqualTo(6));
            Assert.That(receipt.OutcomeKind, Is.EqualTo(OperationOutcomeKind.SlotCompleted));
            Assert.That(receipt.OutcomeSummary.GrowthExperienceDelta.Spike, Is.EqualTo(132));
            Assert.That(receipt.OutcomeSummary.FatigueDelta, Is.EqualTo(8));
            Assert.That(receipt.OutcomeSummary.MindsetDelta, Is.Zero);
            Assert.That(receipt.OutcomeSummary.CoachTrustDelta, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Spike, Is.EqualTo(132));
            AssertExecutionContextPreserved(prior, result.Snapshot);
            Assert.That(result.Snapshot.Player.Attributes.Serve, Is.EqualTo(prior.Player.Attributes.Serve));
            Assert.That(result.Snapshot.Player.Attributes.Reception, Is.EqualTo(prior.Player.Attributes.Reception));
            Assert.That(result.Snapshot.Player.Attributes.Defense, Is.EqualTo(prior.Player.Attributes.Defense));
            Assert.That(result.Snapshot.Player.Attributes.Block, Is.EqualTo(prior.Player.Attributes.Block));
            Assert.That(result.Snapshot.Player.Attributes.Movement, Is.EqualTo(prior.Player.Attributes.Movement));
            Assert.That(result.Snapshot.Player.Attributes.Jump, Is.EqualTo(prior.Player.Attributes.Jump));
            Assert.That(result.Snapshot.Player.Attributes.Stamina, Is.EqualTo(prior.Player.Attributes.Stamina));
        }

        [TestCase(CareerWeekActionKind.SpecializedTraining, "week_action.specialized.serve", 132, 8, 0, 0, 1)]
        [TestCase(CareerWeekActionKind.StrengthTraining, "week_action.strength.jump", 110, 12, 0, 0, 1)]
        [TestCase(CareerWeekActionKind.TeamPractice, "week_action.team_practice.standard", 22, 6, 0, 5, 0)]
        [TestCase(CareerWeekActionKind.Rest, "week_action.rest.standard", 0, -18, 3, 0, 0)]
        public void ExecuteWeekAction_SlotOneUsesClosedActionConsequencesAndEmphasisKinds(
            CareerWeekActionKind kind,
            string contentId,
            long growth,
            int fatigue,
            int mindset,
            int trust,
            int emphasisCount)
        {
            var prior = ConfirmedSnapshot(kind, contentId);
            var command = ExecuteCommand(prior, 1);
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(new MemoryRepository(prior), random).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.OutcomeSummary.FatigueDelta, Is.EqualTo(fatigue));
            Assert.That(result.OutcomeSummary.MindsetDelta, Is.EqualTo(mindset));
            Assert.That(result.OutcomeSummary.CoachTrustDelta, Is.EqualTo(trust));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(emphasisCount));
            if (kind == CareerWeekActionKind.TeamPractice)
            {
                Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Spike, Is.EqualTo(growth));
                Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Stamina, Is.EqualTo(growth));
            }
            else
            {
                Assert.That(
                    TotalGrowth(result.OutcomeSummary.GrowthExperienceDelta),
                    Is.EqualTo(growth));
            }

            Assert.That(random.Calls, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExecuteWeekAction_SlotTwoAdvancesRevisionSevenToEightWithoutRandomOrPendingMatch()
        {
            var prior = CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var command = ExecuteCommand(prior, 2);
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var repository = new MemoryRepository(prior);

            var result = Service(repository, random).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(8));
            Assert.That(result.Snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
            Assert.That(result.Snapshot.Progression.NextSlotNumber, Is.EqualTo(3));
            Assert.That(result.Snapshot.Progression.PendingEvent, Is.Null);
            Assert.That(result.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(2));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[1].Direction,
                Is.EqualTo(CareerTrainingDirection.Jump));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[1].BonusBasisPoints,
                Is.EqualTo(1000));
            Assert.That(prior.Player.Attributes.Spike.GrowthExperience, Is.EqualTo(130));
            Assert.That(prior.Player.Attributes.Serve.GrowthExperience, Is.EqualTo(21));
            Assert.That(prior.Player.Attributes.Reception.GrowthExperience, Is.EqualTo(32));
            Assert.That(prior.Player.Attributes.Defense.GrowthExperience, Is.EqualTo(43));
            Assert.That(prior.Player.Attributes.Block.GrowthExperience, Is.EqualTo(54));
            Assert.That(prior.Player.Attributes.Movement.GrowthExperience, Is.EqualTo(65));
            Assert.That(prior.Player.Attributes.Jump.GrowthExperience, Is.EqualTo(76));
            Assert.That(prior.Player.Attributes.Stamina.GrowthExperience, Is.EqualTo(87));
            Assert.That(prior.Fatigue.Value, Is.EqualTo(32));
            Assert.That(prior.Mindset.Value, Is.EqualTo(56));
            Assert.That(prior.CoachTrust.Value, Is.EqualTo(63));
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Spike, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Serve, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Reception, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Defense, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Block, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Movement, Is.Zero);
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Jump, Is.EqualTo(100));
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Stamina, Is.Zero);
            Assert.That(result.OutcomeSummary.FatigueDelta, Is.EqualTo(12));
            Assert.That(
                result.Snapshot.Player.Attributes.Spike.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Spike.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Spike));
            Assert.That(
                result.Snapshot.Player.Attributes.Serve.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Serve.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Serve));
            Assert.That(
                result.Snapshot.Player.Attributes.Reception.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Reception.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Reception));
            Assert.That(
                result.Snapshot.Player.Attributes.Defense.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Defense.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Defense));
            Assert.That(
                result.Snapshot.Player.Attributes.Block.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Block.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Block));
            Assert.That(
                result.Snapshot.Player.Attributes.Movement.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Movement.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Movement));
            Assert.That(
                result.Snapshot.Player.Attributes.Jump.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Jump.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Jump));
            Assert.That(
                result.Snapshot.Player.Attributes.Stamina.GrowthExperience,
                Is.EqualTo(prior.Player.Attributes.Stamina.GrowthExperience +
                    result.OutcomeSummary.GrowthExperienceDelta.Stamina));
            Assert.That(result.Snapshot.Fatigue.Value, Is.EqualTo(prior.Fatigue.Value + 12));
            Assert.That(result.Snapshot.Mindset.Value, Is.EqualTo(prior.Mindset.Value));
            Assert.That(result.Snapshot.CoachTrust.Value, Is.EqualTo(prior.CoachTrust.Value));
            AssertAbilityBasisPointsEqual(prior.Player.Attributes, result.Snapshot.Player.Attributes);
            AssertContributionEqual(
                prior.TrainingEmphases.Contributions[0],
                result.Snapshot.TrainingEmphases.Contributions[0]);
            Assert.That(random.Calls, Is.Empty);
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(typeof(CareerProgressionState).Assembly.GetType(
                "Volleyball.Career.Domain.PendingMatch"), Is.Null);
            AssertExecutionContextPreserved(prior, result.Snapshot);
        }

        [Test]
        public void ExecuteWeekAction_AcceptsImmediateRestoreOfSlotOneFrontier()
        {
            var original = ConfirmedSnapshot();
            var restored = ImmediateRestore(
                original,
                new LineageId(Guid.Parse("41414141-4141-4141-4141-414141414141")));
            var command = ExecuteCommand(restored, 1, completedAtUtcMs: restored.Identity.UpdatedAtUtcMs + 1);
            var repository = new MemoryRepository(restored);

            var result = Service(repository).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(7));
            Assert.That(result.Snapshot.Identity.LineageId, Is.EqualTo(restored.Identity.LineageId));
            Assert.That(
                result.Snapshot.Identity.RestoredFromVersionToken,
                Is.EqualTo(new CareerVersionToken? (original.Identity.VersionToken)));
            var receipt = result.Snapshot.OperationReceipts.Last();
            Assert.That(receipt.AppliedLineageId, Is.EqualTo(restored.Identity.LineageId));
            Assert.That(receipt.AppliedRevision, Is.EqualTo(7));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteWeekAction_RejectsMaximumSafeRevisionBeforeRandomOrCommit()
        {
            var prior = UnadvanceableConfirmedSnapshot();
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var repository = new MemoryRepository(prior);
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() => result = Service(repository, random)
                .ExecuteWeekAction(ExecuteCommand(prior, 1)));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(result.Snapshot, Is.SameAs(prior));
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(random.Calls, Is.Empty);
            Assert.That(repository.CommitCount, Is.Zero);
        }

        [Test]
        public void ExecuteWeekAction_AcceptsImmediateRestoreOfSlotTwoFrontier()
        {
            var original = CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var restored = ImmediateRestore(
                original,
                new LineageId(Guid.Parse("42424242-4242-4242-4242-424242424242")));
            var command = ExecuteCommand(restored, 2, completedAtUtcMs: restored.Identity.UpdatedAtUtcMs + 1);
            var repository = new MemoryRepository(restored);

            var result = Service(repository).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(9));
            Assert.That(result.Snapshot.Identity.LineageId, Is.EqualTo(restored.Identity.LineageId));
            Assert.That(
                result.Snapshot.Identity.RestoredFromVersionToken,
                Is.EqualTo(new CareerVersionToken?(original.Identity.VersionToken)));
            var receipt = result.Snapshot.OperationReceipts.Last();
            Assert.That(receipt.AppliedLineageId, Is.EqualTo(restored.Identity.LineageId));
            Assert.That(receipt.AppliedRevision, Is.EqualTo(9));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExecuteWeekAction_RandomDependencyExceptionIsPersistenceFailure(bool invalidOperation)
        {
            var prior = ConfirmedSnapshot();
            var random = new ThrowingRandom(invalidOperation);
            var repository = new MemoryRepository(prior);
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() => result = Service(repository, random)
                .ExecuteWeekAction(ExecuteCommand(prior, 1)));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.PersistenceKind, Is.Null);
            Assert.That(result.Snapshot, Is.SameAs(prior));
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(repository.CommitCount, Is.Zero);
            Assert.That(random.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteWeekAction_FrozenOptionsUseSameClampedPostActionBasis()
        {
            var prior = BoundaryConfirmedSnapshot();
            var command = ExecuteCommand(prior, 1);
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(new MemoryRepository(prior), random).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(
                result.Snapshot.Player.Attributes.Spike.GrowthExperience,
                Is.EqualTo(CareerAttributeProgress.MaximumGrowthExperience));
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Spike, Is.EqualTo(100));
            Assert.That(result.Snapshot.Fatigue.Value, Is.EqualTo(98));
            Assert.That(result.Snapshot.Mindset.Value, Is.EqualTo(94));
            Assert.That(result.Snapshot.CoachTrust.Value, Is.EqualTo(97));
            var options = result.Snapshot.Progression.PendingEvent.Options;
            AssertEffect(options[0], 0, 2, 6, 3);
            AssertEffect(options[1], 0, 2, -2, 3);
            Assert.That(random.Calls.Select(call => call.Result), Is.EqualTo(new long[] { 6791, 7549 }));
        }

        [Test]
        public void ExecuteWeekAction_InvalidShapesNeverLoadCommitOrCallRandom()
        {
            var prior = ConfirmedSnapshot();
            var invalid = new ExecuteWeekActionCommand[]
            {
                null,
                ExecuteCommand(prior, 1, profileId: default(ProfileId)),
                ExecuteCommand(prior, 1, saveId: default(SaveId)),
                ExecuteCommand(prior, 1, expectedToken: default(CareerVersionToken)),
                ExecuteCommand(prior, 1, operationId: default(OperationId)),
                ExecuteCommand(prior, 1, completedAtUtcMs: -1),
                ExecuteCommand(prior, 1, completedAtUtcMs: 9007199254740992L),
                ExecuteCommand(prior, 1, weekPlanId: default(WeekPlanId)),
                ExecuteCommand(prior, 0),
                ExecuteCommand(prior, 3),
                ExecuteCommand(prior, 1, slotActionId: default(SlotActionId)),
                ExecuteCommand(prior, 1, actionOccurrenceId: default(OccurrenceId)),
                ExecuteCommand(prior, 1, contentId: " "),
                ExecuteCommand(prior, 1, contentId: "\ud800"),
                ExecuteCommand(prior, 1, eventOccurrenceId: null, useDefaultEventOccurrence: false),
                ExecuteCommand(prior, 1, eventOccurrenceId: default(OccurrenceId)),
                ExecuteCommand(CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage), 2,
                    eventOccurrenceId: EventOccurrence())
            };

            foreach (var command in invalid)
            {
                var repository = new MemoryRepository(prior);
                var random = new RecordingRandom(new CareerDeterministicRandom());
                var result = Service(repository, random).ExecuteWeekAction(command);

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
                Assert.That(result.Snapshot, Is.Null);
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.LoadCount, Is.Zero);
                Assert.That(repository.CommitCount, Is.Zero);
                Assert.That(random.Calls, Is.Empty);
            }
        }

        [Test]
        public void ExecuteWeekAction_ExactRetryAndCompletionTimeRetryDoNotDuplicateOrReroll()
        {
            var prior = ConfirmedSnapshot();
            var command = ExecuteCommand(prior, 1);
            var changedTime = ExecuteCommand(prior, 1, completedAtUtcMs: command.CompletedAtUtcMs + 1);
            var repository = new MemoryRepository(prior);
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var service = Service(repository, random);

            var applied = service.ExecuteWeekAction(command);
            var exact = service.ExecuteWeekAction(command);
            var metadataOnly = service.ExecuteWeekAction(changedTime);

            Assert.That(applied.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(exact.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(metadataOnly.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Has.Count.EqualTo(2));
            Assert.That(repository.Snapshot.OperationReceipts.Count(
                receipt => receipt.OperationId.Equals(command.OperationId)), Is.EqualTo(1));
            Assert.That(repository.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(1));
            Assert.That(repository.Snapshot.Progression.PendingEvent.Options, Has.Count.EqualTo(2));
        }

        [Test]
        public void ExecuteWeekAction_RejectsAuthoritativeMismatchesWithoutRandomOrCommit()
        {
            var prior = ConfirmedSnapshot();
            var unsupported = CopySnapshot(
                prior,
                versions: new CareerSaveVersions(1, 2, 1, 1));
            var wrongCalendar = WithPlanCalendar(prior, 2, 1);
            var cases = new[]
            {
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, profileId: new ProfileId(Guid.Parse("abababab-abab-abab-abab-abababababab")))),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, saveId: new SaveId(Guid.Parse("acacacac-acac-acac-acac-acacacacacac")))),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, completedAtUtcMs: prior.Identity.UpdatedAtUtcMs - 1)),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, weekPlanId: WeekPlan(90))),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, slotActionId: Slot(90))),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, actionOccurrenceId: Occurrence(90))),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, contentId: "week_action.specialized.serve")),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 2, eventOccurrenceId: null, useDefaultEventOccurrence: false)),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, eventOccurrenceId: prior.Onboarding.Stages[0].OccurrenceId)),
                new ExecuteInvalidCase(prior, ExecuteCommand(
                    prior, 1, eventOccurrenceId: prior.Progression.WeekPlan.Slots[2].OccurrenceId)),
                new ExecuteInvalidCase(unsupported, ExecuteCommand(unsupported, 1)),
                new ExecuteInvalidCase(wrongCalendar, ExecuteCommand(wrongCalendar, 1)),
                new ExecuteInvalidCase(PlanningSnapshot(), ExecuteCommandForPlanning(PlanningSnapshot()))
            };

            foreach (var testCase in cases)
            {
                var repository = new MemoryRepository(testCase.Snapshot);
                var random = new RecordingRandom(new CareerDeterministicRandom());
                var result = Service(repository, random).ExecuteWeekAction(testCase.Command);

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
                Assert.That(result.Snapshot, Is.SameAs(testCase.Snapshot));
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.CommitCount, Is.Zero);
                Assert.That(random.Calls, Is.Empty);
            }
        }

        [Test]
        public void ExecuteWeekAction_OperationConflictAndStaleVersionDoNotCalculateOrCommit()
        {
            var prior = ConfirmedSnapshot();
            var command = ExecuteCommand(prior, 1);
            var repository = new MemoryRepository(prior);
            var initialRandom = new RecordingRandom(new CareerDeterministicRandom());
            Assert.That(Service(repository, initialRandom).ExecuteWeekAction(command).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var conflict = ExecuteCommand(
                prior,
                1,
                eventOccurrenceId: new OccurrenceId(
                    Guid.Parse("00000000-0000-0000-0000-000000000099")));
            var stale = ExecuteCommand(
                prior,
                1,
                operationId: OperationFor(21));

            var conflictResult = Service(repository, random).ExecuteWeekAction(conflict);
            var staleResult = Service(repository, random).ExecuteWeekAction(stale);

            Assert.That(conflictResult.Status, Is.EqualTo(CareerApplicationStatus.OperationConflict));
            Assert.That(conflictResult.ConflictingReceipt.OperationId, Is.EqualTo(command.OperationId));
            Assert.That(conflictResult.OutcomeSummary, Is.Null);
            Assert.That(staleResult.Status, Is.EqualTo(CareerApplicationStatus.VersionConflict));
            Assert.That(staleResult.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(staleResult.OutcomeSummary, Is.Null);
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExecuteWeekAction_CommitRaceRechecksExactConflictingAndMissingReceipts(int slotNumber)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var command = ExecuteCommand(prior, slotNumber);
            var winnerRandom = new RecordingRandom(new CareerDeterministicRandom());
            var winnerRepository = new MemoryRepository(prior);
            var winner = Service(winnerRepository, winnerRandom).ExecuteWeekAction(command).Snapshot;
            var conflicting = WithLastReceiptFingerprint(winner, Hash('e'));
            var missingCommand = ExecuteCommand(prior, slotNumber, operationId: OperationFor(22));
            var missingRepository = new MemoryRepository(prior);
            var missingWinner = Service(missingRepository, new RecordingRandom(new CareerDeterministicRandom()))
                .ExecuteWeekAction(missingCommand).Snapshot;

            AssertExecuteRace(
                command, prior, winner, CareerApplicationStatus.Existing, false, slotNumber == 1 ? 2 : 0);
            AssertExecuteRace(
                command, prior, conflicting, CareerApplicationStatus.OperationConflict, true, slotNumber == 1 ? 2 : 0);
            AssertExecuteRace(
                command, prior, missingWinner, CareerApplicationStatus.VersionConflict, false, slotNumber == 1 ? 2 : 0);
        }

        [TestCase(1, PersistenceResultKind.NotCommitted)]
        [TestCase(1, PersistenceResultKind.IoFailure)]
        [TestCase(1, PersistenceResultKind.LockUnavailable)]
        [TestCase(2, PersistenceResultKind.NotCommitted)]
        [TestCase(2, PersistenceResultKind.IoFailure)]
        [TestCase(2, PersistenceResultKind.LockUnavailable)]
        public void ExecuteWeekAction_CommitFailuresExposeOnlyPriorAuthoritativeState(
            int slotNumber,
            PersistenceResultKind failureKind)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var repository = new MemoryRepository(prior) { CommitFailure = failureKind };
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(repository, random).ExecuteWeekAction(ExecuteCommand(prior, slotNumber));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.PersistenceKind, Is.EqualTo(failureKind));
            Assert.That(result.Snapshot, Is.SameAs(prior));
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(result.ConflictingReceipt, Is.Null);
            Assert.That(repository.Snapshot, Is.SameAs(prior));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void ExecuteWeekAction_RepositoryNullOrThrowNeverEscapesOrExposesSpeculation(
            int slotNumber,
            bool throws)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var initial = new MemoryRepository(prior)
            {
                NullLoadCallNumber = throws ? (int?)null : 1,
                ThrowLoadCallNumber = throws ? 1 : (int?)null
            };
            CareerWeekCommandResult initialResult = null;
            Assert.DoesNotThrow(() => initialResult = Service(initial).ExecuteWeekAction(
                ExecuteCommand(prior, slotNumber)));
            Assert.That(initialResult.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(initialResult.Snapshot, Is.Null);
            Assert.That(initialResult.OutcomeSummary, Is.Null);
            Assert.That(initial.CommitCount, Is.Zero);

            var commit = new MemoryRepository(prior)
            {
                ReturnNullCommit = !throws,
                ThrowCommit = throws
            };
            var random = new RecordingRandom(new CareerDeterministicRandom());
            CareerWeekCommandResult commitResult = null;
            Assert.DoesNotThrow(() => commitResult = Service(commit, random).ExecuteWeekAction(
                ExecuteCommand(prior, slotNumber)));
            Assert.That(commitResult.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(commitResult.Snapshot, Is.SameAs(prior));
            Assert.That(commitResult.OutcomeSummary, Is.Null);
            Assert.That(commit.Snapshot, Is.SameAs(prior));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExecuteWeekAction_MalformedLoadAndSuccessPayloadsAreStructuredPersistenceFailures(
            int slotNumber)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var malformedLoadRepository = new MemoryRepository(prior)
            {
                MalformedLoadWithoutSnapshot = true
            };
            CareerWeekCommandResult malformedLoad = null;

            Assert.DoesNotThrow(() => malformedLoad = Service(malformedLoadRepository)
                .ExecuteWeekAction(ExecuteCommand(prior, slotNumber)));
            Assert.That(malformedLoad.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(malformedLoad.Snapshot, Is.Null);
            Assert.That(malformedLoad.OutcomeSummary, Is.Null);
            Assert.That(malformedLoadRepository.CommitCount, Is.Zero);

            var malformedSuccessRepository = new MemoryRepository(prior)
            {
                MalformedSuccessWithoutSnapshot = true
            };
            var random = new RecordingRandom(new CareerDeterministicRandom());
            CareerWeekCommandResult malformedSuccess = null;
            Assert.DoesNotThrow(() => malformedSuccess = Service(malformedSuccessRepository, random)
                .ExecuteWeekAction(ExecuteCommand(prior, slotNumber)));
            Assert.That(malformedSuccess.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(malformedSuccess.Snapshot, Is.SameAs(prior));
            Assert.That(malformedSuccess.OutcomeSummary, Is.Null);
            Assert.That(malformedSuccessRepository.Snapshot, Is.SameAs(prior));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [Test]
        public void ExecuteWeekAction_InterleavedUnrelatedKeyDoesNotChangeFrozenOptionEffects()
        {
            var prior = ConfirmedSnapshot();
            var command = ExecuteCommand(prior, 1);
            var baseline = Service(
                new MemoryRepository(prior),
                new RecordingRandom(new CareerDeterministicRandom())).ExecuteWeekAction(command);
            var interleavedRandom = new InterleavingRandom(new CareerDeterministicRandom());
            var interleaved = Service(
                new MemoryRepository(prior),
                interleavedRandom).ExecuteWeekAction(command);

            Assert.That(interleaved.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            for (var index = 0; index < 2; index++)
            {
                AssertEffectEqual(
                    baseline.Snapshot.Progression.PendingEvent.Options[index],
                    interleaved.Snapshot.Progression.PendingEvent.Options[index]);
            }

            Assert.That(interleavedRandom.RequestedCalls, Is.EqualTo(2));
            Assert.That(interleavedRandom.UnrelatedCalls, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteWeekAction_SameDirectionSecondTrainingAddsOnlyFiveHundredBasisPoints()
        {
            var prior = SameDirectionSlotTwoPrior();
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(new MemoryRepository(prior), random)
                .ExecuteWeekAction(ExecuteCommand(prior, 2));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.OutcomeSummary.GrowthExperienceDelta.Spike, Is.EqualTo(120));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(2));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[0].BonusBasisPoints, Is.EqualTo(1000));
            Assert.That(result.Snapshot.TrainingEmphases.Contributions[1].BonusBasisPoints, Is.EqualTo(500));
            Assert.That(result.Snapshot.TrainingEmphases.Freeze()[0].TotalBonusBasisPoints, Is.EqualTo(1500));
            Assert.That(random.Calls, Is.Empty);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExecuteWeekAction_BackupDegradedReturnsAppliedPersistedSummary(int slotNumber)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var repository = new MemoryRepository(prior)
            {
                SuccessfulCommitKind = PersistenceResultKind.BackupDegraded
            };
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(repository, random).ExecuteWeekAction(ExecuteCommand(prior, slotNumber));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.BackupDegraded));
            Assert.That(result.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.SlotCompleted));
            Assert.That(result.Snapshot.OperationReceipts.Last().OutcomeSummary.OutcomeKind,
                Is.EqualTo(result.OutcomeSummary.OutcomeKind));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExecuteWeekAction_SuccessPayloadWithoutMatchingReceiptIsPersistenceFailure(int slotNumber)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var repository = new MemoryRepository(prior)
            {
                SuccessfulCommitPayload = prior
            };
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(repository, random).ExecuteWeekAction(ExecuteCommand(prior, slotNumber));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.Snapshot, Is.SameAs(prior));
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void ExecuteWeekAction_CommitRaceReloadNullOrThrowIsPersistenceFailure(
            int slotNumber,
            bool throws)
        {
            var prior = slotNumber == 1
                ? ConfirmedSnapshot()
                : CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var command = ExecuteCommand(prior, slotNumber);
            var winner = Service(
                    new MemoryRepository(prior),
                    new RecordingRandom(new CareerDeterministicRandom()))
                .ExecuteWeekAction(command).Snapshot;
            var repository = new MemoryRepository(prior)
            {
                CommitRaceWinner = winner,
                NullLoadCallNumber = throws ? (int?)null : 2,
                ThrowLoadCallNumber = throws ? 2 : (int?)null
            };
            var random = new RecordingRandom(new CareerDeterministicRandom());
            CareerWeekCommandResult result = null;

            Assert.DoesNotThrow(() => result = Service(repository, random).ExecuteWeekAction(command));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.OutcomeSummary, Is.Null);
            Assert.That(repository.LoadCount, Is.EqualTo(2));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Has.Count.EqualTo(slotNumber == 1 ? 2 : 0));
        }

        [Test]
        public void ConfirmWeekPlan_ServiceSurfaceHasFinalRandomDependencyAndNoFutureCommandShells()
        {
            var constructors = typeof(CareerWeekCommandService).GetConstructors();
            var methods = typeof(CareerWeekCommandService).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var fields = typeof(CareerWeekCommandService).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(ICareerSaveRepository), typeof(IDeterministicCareerRandom) }));
            Assert.That(methods.Select(method => method.Name),
                Is.EqualTo(new[] { "ConfirmWeekPlan", "ExecuteWeekAction" }));
            Assert.That(
                fields.Any(field => field.FieldType == typeof(IDeterministicCareerRandom)),
                Is.True);

            var prior = PlanningSnapshot();
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var result = Service(new MemoryRepository(prior), random).ConfirmWeekPlan(Command(prior));
            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(random.Calls, Is.Empty);
            Assert.That(typeof(CareerWeekCommandService).GetMethod("ResolveEventChoice"), Is.Null);
        }

        private static CareerWeekCommandService Service(
            MemoryRepository repository,
            IDeterministicCareerRandom random = null)
        {
            return new CareerWeekCommandService(
                repository,
                random ?? new RecordingRandom(new CareerDeterministicRandom()));
        }

        private static ExecuteWeekActionCommand ExecuteCommandForPlanning(
            CareerSaveSnapshot planning)
        {
            return new ExecuteWeekActionCommand(
                planning.Identity.ProfileId,
                planning.Identity.SaveId,
                planning.Identity.VersionToken,
                OperationFor(20),
                planning.Identity.UpdatedAtUtcMs + 1,
                planning.Progression.WeekPlan.PlanId,
                1,
                Slot(1),
                Occurrence(2),
                "week_action.specialized.spike",
                EventOccurrence());
        }

        private static CareerSaveSnapshot SameDirectionSlotTwoPrior()
        {
            var source = CareerPersistenceTestData.PlannedAfterFirstTraining(Profile, Save, Lineage);
            var oldPlan = source.Progression.WeekPlan;
            var plan = new CareerWeekPlanState(
                oldPlan.PlanId,
                oldPlan.Season,
                oldPlan.Week,
                new[]
                {
                    oldPlan.Slots[0],
                    new CareerWeekActionState(
                        oldPlan.Slots[1].SlotActionId,
                        oldPlan.Slots[1].OccurrenceId,
                        CareerWeekActionKind.SpecializedTraining,
                        "week_action.specialized.spike"),
                    oldPlan.Slots[2]
                },
                true);
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                CareerProgressionState.Planned(plan, 2),
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                source.OperationReceipts);
        }

        private static CareerSaveSnapshot WithPlanCalendar(
            CareerSaveSnapshot source,
            int season,
            int week)
        {
            var oldPlan = source.Progression.WeekPlan;
            var plan = new CareerWeekPlanState(
                oldPlan.PlanId,
                season,
                week,
                oldPlan.Slots,
                true);
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                CareerProgressionState.Planned(plan, source.Progression.NextSlotNumber),
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                source.OperationReceipts);
        }

        private static CareerSaveSnapshot WithLastReceiptFingerprint(
            CareerSaveSnapshot source,
            Sha256Digest fingerprint)
        {
            var receipts = new List<OperationReceipt>(source.OperationReceipts);
            var last = receipts[receipts.Count - 1];
            receipts[receipts.Count - 1] = new OperationReceipt(
                last.OperationId,
                last.OperationKind,
                last.Target,
                fingerprint,
                last.AppliedLineageId,
                last.AppliedRevision,
                last.CompletedAtUtcMs,
                last.OutcomeKind,
                last.OutcomeSummary);
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
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
                receipts);
        }

        private static void AssertExecuteRace(
            ExecuteWeekActionCommand command,
            CareerSaveSnapshot prior,
            CareerSaveSnapshot winner,
            CareerApplicationStatus status,
            bool hasConflict,
            int expectedRandomCalls)
        {
            var repository = new MemoryRepository(prior) { CommitRaceWinner = winner };
            var random = new RecordingRandom(new CareerDeterministicRandom());

            var result = Service(repository, random).ExecuteWeekAction(command);

            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Snapshot, Is.SameAs(winner));
            Assert.That(result.ConflictingReceipt != null, Is.EqualTo(hasConflict));
            Assert.That(result.OutcomeSummary != null, Is.EqualTo(status == CareerApplicationStatus.Existing));
            Assert.That(repository.LoadCount, Is.EqualTo(2));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(random.Calls, Has.Count.EqualTo(expectedRandomCalls));
        }

        private static void AssertEffectEqual(
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

        private static void AssertAbilityBasisPointsEqual(
            CareerPlayerAttributes expected,
            CareerPlayerAttributes actual)
        {
            Assert.That(actual.Spike.AbilityBasisPoints, Is.EqualTo(expected.Spike.AbilityBasisPoints));
            Assert.That(actual.Serve.AbilityBasisPoints, Is.EqualTo(expected.Serve.AbilityBasisPoints));
            Assert.That(actual.Reception.AbilityBasisPoints, Is.EqualTo(expected.Reception.AbilityBasisPoints));
            Assert.That(actual.Defense.AbilityBasisPoints, Is.EqualTo(expected.Defense.AbilityBasisPoints));
            Assert.That(actual.Block.AbilityBasisPoints, Is.EqualTo(expected.Block.AbilityBasisPoints));
            Assert.That(actual.Movement.AbilityBasisPoints, Is.EqualTo(expected.Movement.AbilityBasisPoints));
            Assert.That(actual.Jump.AbilityBasisPoints, Is.EqualTo(expected.Jump.AbilityBasisPoints));
            Assert.That(actual.Stamina.AbilityBasisPoints, Is.EqualTo(expected.Stamina.AbilityBasisPoints));
        }

        private static void AssertContributionEqual(
            TrainingEmphasisContribution expected,
            TrainingEmphasisContribution actual)
        {
            Assert.That(actual.SourceSlotActionId, Is.EqualTo(expected.SourceSlotActionId));
            Assert.That(actual.Direction, Is.EqualTo(expected.Direction));
            Assert.That(actual.BonusBasisPoints, Is.EqualTo(expected.BonusBasisPoints));
        }

        private static void AssertExecutionContextPreserved(
            CareerSaveSnapshot prior,
            CareerSaveSnapshot next)
        {
            Assert.That(next.Identity.ProfileId, Is.EqualTo(prior.Identity.ProfileId));
            Assert.That(next.Identity.SaveId, Is.EqualTo(prior.Identity.SaveId));
            Assert.That(next.Identity.LineageId, Is.EqualTo(prior.Identity.LineageId));
            Assert.That(next.Identity.CreatedAtUtcMs, Is.EqualTo(prior.Identity.CreatedAtUtcMs));
            Assert.That(next.Identity.RestoredFromVersionToken,
                Is.EqualTo(prior.Identity.RestoredFromVersionToken));
            Assert.That(next.Identity.SnapshotHash.Value, Is.EqualTo(ZeroHash));
            Assert.That(next.Versions.SchemaVersion, Is.EqualTo(prior.Versions.SchemaVersion));
            Assert.That(next.Versions.ContentVersion, Is.EqualTo(prior.Versions.ContentVersion));
            Assert.That(next.Versions.RulesetVersion, Is.EqualTo(prior.Versions.RulesetVersion));
            Assert.That(next.Versions.CareerRandomAlgorithmVersion,
                Is.EqualTo(prior.Versions.CareerRandomAlgorithmVersion));
            CollectionAssert.AreEqual(prior.CareerSeed.ToBytes(), next.CareerSeed.ToBytes());
            Assert.That(next.CareerName, Is.EqualTo(prior.CareerName));
            Assert.That(next.PlayerDraft.PlayerId, Is.EqualTo(prior.PlayerDraft.PlayerId));
            Assert.That(next.PlayerDraft.DisplayName, Is.EqualTo(prior.PlayerDraft.DisplayName));
            Assert.That(next.PlayerDraft.JerseyNumber, Is.EqualTo(prior.PlayerDraft.JerseyNumber));
            Assert.That(next.Player.PlayerId, Is.EqualTo(prior.Player.PlayerId));
            Assert.That(next.Player.DisplayName, Is.EqualTo(prior.Player.DisplayName));
            Assert.That(next.Player.JerseyNumber, Is.EqualTo(prior.Player.JerseyNumber));
            Assert.That(next.TeamId, Is.EqualTo(prior.TeamId));
            Assert.That(next.PotentialGrade, Is.EqualTo(prior.PotentialGrade));
            AssertPlanEqual(prior.Progression.WeekPlan, next.Progression.WeekPlan);
            Assert.That(next.Onboarding.CurrentStageNumber,
                Is.EqualTo(prior.Onboarding.CurrentStageNumber));
            Assert.That(next.Onboarding.IsComplete, Is.EqualTo(prior.Onboarding.IsComplete));
            Assert.That(next.Onboarding.IsFormallyEnrolled,
                Is.EqualTo(prior.Onboarding.IsFormallyEnrolled));
            Assert.That(next.Onboarding.Stages, Has.Count.EqualTo(prior.Onboarding.Stages.Count));
            for (var index = 0; index < prior.Onboarding.Stages.Count; index++)
            {
                Assert.That(next.Onboarding.Stages[index].StageNumber,
                    Is.EqualTo(prior.Onboarding.Stages[index].StageNumber));
                Assert.That(next.Onboarding.Stages[index].OccurrenceId,
                    Is.EqualTo(prior.Onboarding.Stages[index].OccurrenceId));
                Assert.That(next.Onboarding.Stages[index].ChoiceId,
                    Is.EqualTo(prior.Onboarding.Stages[index].ChoiceId));
                AssertResolvedOutputs(
                    prior.Onboarding.Stages[index].ResolvedOutputs,
                    next.Onboarding.Stages[index].ResolvedOutputs);
            }

            for (var index = 0; index < prior.OperationReceipts.Count; index++)
            {
                AssertReceipt(prior.OperationReceipts[index], next.OperationReceipts[index]);
            }
        }

        private static CareerSaveSnapshot ConfirmedSnapshot(
            CareerWeekActionKind firstKind = CareerWeekActionKind.SpecializedTraining,
            string firstContentId = "week_action.specialized.spike")
        {
            var planning = PlanningSnapshot();
            var confirmation = Command(
                planning,
                candidate: Candidate(
                    planning,
                    firstKind: firstKind,
                    firstContentId: firstContentId));
            var confirmed = AppliedSnapshot(planning, confirmation);
            var seed = new byte[CareerSeed.ByteLength];
            for (var index = 0; index < seed.Length; index++)
            {
                seed[index] = (byte)index;
            }

            return new CareerSaveSnapshot(
                confirmed.Versions,
                confirmed.Identity,
                new CareerSeed(seed),
                confirmed.CareerName,
                confirmed.PlayerDraft,
                confirmed.Onboarding,
                confirmed.Progression,
                confirmed.TrainingEmphases,
                confirmed.Player,
                confirmed.TeamId,
                confirmed.PotentialGrade,
                confirmed.Fatigue,
                confirmed.Mindset,
                confirmed.CoachTrust,
                confirmed.OperationReceipts);
        }

        private static CareerSaveSnapshot BoundaryConfirmedSnapshot()
        {
            var source = ConfirmedSnapshot();
            var attributes = source.Player.Attributes;
            var player = new CareerPlayerRecord(
                source.Player.PlayerId,
                source.Player.DisplayName,
                source.Player.JerseyNumber,
                new CareerPlayerAttributes(
                    new CareerAttributeProgress(
                        attributes.Spike.AbilityBasisPoints,
                        CareerAttributeProgress.MaximumGrowthExperience - 100),
                    attributes.Serve,
                    attributes.Reception,
                    attributes.Defense,
                    attributes.Block,
                    attributes.Movement,
                    attributes.Jump,
                    attributes.Stamina));
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                source.Progression,
                source.TrainingEmphases,
                player,
                source.TeamId,
                source.PotentialGrade,
                90,
                94,
                97,
                source.OperationReceipts);
        }

        private static CareerSaveSnapshot UnadvanceableConfirmedSnapshot()
        {
            var source = ConfirmedSnapshot();
            var maximumSafeRevision = CareerAttributeProgress.MaximumGrowthExperience;
            var restoredLineage = new LineageId(
                Guid.Parse("43434343-4343-4343-4343-434343434343"));
            var receipts = source.OperationReceipts.Select(receipt => new OperationReceipt(
                receipt.OperationId,
                receipt.OperationKind,
                receipt.Target,
                receipt.InputFingerprint,
                restoredLineage,
                receipt.AppliedRevision,
                receipt.CompletedAtUtcMs,
                receipt.OutcomeKind,
                receipt.OutcomeSummary)).ToArray();
            return new CareerSaveSnapshot(
                source.Versions,
                new CareerSaveIdentity(
                    source.Identity.ProfileId,
                    source.Identity.SaveId,
                    restoredLineage,
                    maximumSafeRevision,
                    source.Identity.CreatedAtUtcMs,
                    source.Identity.UpdatedAtUtcMs + 1,
                    Hash('8'),
                    new CareerVersionToken(
                        source.Identity.LineageId,
                        maximumSafeRevision - 1,
                        source.Identity.SnapshotHash)),
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
                receipts);
        }

        private static CareerSaveSnapshot ImmediateRestore(
            CareerSaveSnapshot source,
            LineageId newLineageId)
        {
            var receipts = source.OperationReceipts.Select(receipt => new OperationReceipt(
                receipt.OperationId,
                receipt.OperationKind,
                receipt.Target,
                receipt.InputFingerprint,
                newLineageId,
                receipt.AppliedRevision,
                receipt.CompletedAtUtcMs,
                receipt.OutcomeKind,
                receipt.OutcomeSummary)).ToArray();
            return new CareerSaveSnapshot(
                source.Versions,
                new CareerSaveIdentity(
                    source.Identity.ProfileId,
                    source.Identity.SaveId,
                    newLineageId,
                    source.Identity.Revision + 1,
                    source.Identity.CreatedAtUtcMs,
                    source.Identity.UpdatedAtUtcMs + 1,
                    Hash('7'),
                    source.Identity.VersionToken),
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
                receipts);
        }

        private static ExecuteWeekActionCommand ExecuteCommand(
            CareerSaveSnapshot prior,
            int slotNumber,
            ProfileId? profileId = null,
            SaveId? saveId = null,
            CareerVersionToken? expectedToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 300,
            WeekPlanId? weekPlanId = null,
            SlotActionId? slotActionId = null,
            OccurrenceId? actionOccurrenceId = null,
            string contentId = null,
            OccurrenceId? eventOccurrenceId = null,
            bool useDefaultEventOccurrence = true)
        {
            var plan = prior.Progression.WeekPlan;
            var action = slotNumber >= 1 && slotNumber <= plan.Slots.Count
                ? plan.Slots[slotNumber - 1]
                : plan.Slots[0];
            return new ExecuteWeekActionCommand(
                profileId ?? Profile,
                saveId ?? Save,
                expectedToken ?? prior.Identity.VersionToken,
                operationId ?? OperationFor(20),
                completedAtUtcMs,
                weekPlanId ?? plan.PlanId,
                slotNumber,
                slotActionId ?? action.SlotActionId,
                actionOccurrenceId ?? action.OccurrenceId,
                contentId ?? action.ContentId,
                useDefaultEventOccurrence && slotNumber == 1 && !eventOccurrenceId.HasValue
                    ? new OccurrenceId?(EventOccurrence())
                    : eventOccurrenceId);
        }

        private static OccurrenceId EventOccurrence()
        {
            return new OccurrenceId(Guid.Parse("00000000-0000-0000-0000-000000000003"));
        }

        private static long TotalGrowth(CareerAttributeGrowthDelta delta)
        {
            return delta.Spike + delta.Serve + delta.Reception + delta.Defense +
                   delta.Block + delta.Movement + delta.Jump + delta.Stamina;
        }

        private static void AssertEffect(
            CareerEventOptionEffect effect,
            long spikeGrowth,
            int fatigue,
            int mindset,
            int trust)
        {
            Assert.That(effect.GrowthExperienceDelta.Spike, Is.EqualTo(spikeGrowth));
            Assert.That(TotalGrowth(effect.GrowthExperienceDelta), Is.EqualTo(spikeGrowth));
            Assert.That(effect.FatigueDelta, Is.EqualTo(fatigue));
            Assert.That(effect.MindsetDelta, Is.EqualTo(mindset));
            Assert.That(effect.CoachTrustDelta, Is.EqualTo(trust));
        }

        private static void AssertRandomCall(
            RandomCall call,
            string optionId,
            string expectedDigest,
            long expectedRoll)
        {
            Assert.That(call.Request.AlgorithmVersion, Is.EqualTo(1));
            Assert.That(call.Request.StreamId, Is.EqualTo("event"));
            Assert.That(call.Request.Season, Is.EqualTo(1));
            Assert.That(call.Request.Week, Is.EqualTo(1));
            Assert.That(call.Request.EntityStableId, Is.EqualTo(optionId));
            Assert.That(call.Request.OccurrenceId, Is.EqualTo(EventOccurrence()));
            Assert.That(call.Request.DrawIndex, Is.Zero);
            Assert.That(call.Minimum, Is.Zero);
            Assert.That(call.Maximum, Is.EqualTo(10000));
            Assert.That(call.Result, Is.EqualTo(expectedRoll));
            Assert.That(
                Hex(new CareerDeterministicRandom().ComputeDigest(call.Request, 0)),
                Is.EqualTo(expectedDigest));
        }

        private static string Hex(byte[] bytes)
        {
            const string alphabet = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = alphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = alphabet[bytes[index] & 15];
            }

            return new string(characters);
        }

        private static void AssertRace(
            ConfirmWeekPlanCommand command,
            CareerSaveSnapshot prior,
            CareerSaveSnapshot winner,
            CareerApplicationStatus expectedStatus,
            bool hasConflict)
        {
            var repository = new MemoryRepository(prior) { CommitRaceWinner = winner };

            var result = Service(repository).ConfirmWeekPlan(command);

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Snapshot, Is.SameAs(winner));
            Assert.That(result.ConflictingReceipt != null, Is.EqualTo(hasConflict));
            Assert.That(result.OutcomeSummary != null, Is.EqualTo(expectedStatus == CareerApplicationStatus.Existing));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(repository.LoadCount, Is.EqualTo(2));
        }

        private static CareerSaveSnapshot PlanningSnapshot()
        {
            var stages = new[]
            {
                ConfirmedStage(1, 101, 11),
                ConfirmedStage(2, 102, 21),
                ConfirmedStage(3, 103, 31)
            };
            var onboarding = new TryoutOnboardingState(stages, 0, true);
            var match = new CareerWeekActionState(
                Slot(50),
                Occurrence(50),
                CareerWeekActionKind.Match,
                "schedule.u1w1.match.01");
            var plan = new CareerWeekPlanState(
                WeekPlan(50),
                1,
                1,
                new CareerWeekActionState[] { null, null, match },
                false);
            var receipts = new List<OperationReceipt>
            {
                Receipt(
                    1,
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    OperationOutcomeSummary.ForCareerCreated(),
                    61)
            };
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                receipts.Add(Receipt(
                    index + 2,
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        stage.StageNumber,
                        stage.OccurrenceId,
                        stage.ChoiceId),
                    OperationOutcomeSummary.ForTryoutAdvanced(stage.ResolvedOutputs),
                    62 + index));
            }

            var attributes = new CareerPlayerAttributes(
                new CareerAttributeProgress(1100, 11),
                new CareerAttributeProgress(2200, 22),
                new CareerAttributeProgress(3300, 33),
                new CareerAttributeProgress(4400, 44),
                new CareerAttributeProgress(5500, 55),
                new CareerAttributeProgress(6600, 66),
                new CareerAttributeProgress(7700, 77),
                new CareerAttributeProgress(8800, 88));
            var draft = new CareerPlayerDraft(
                new PlayerId("distinct-player"),
                "Distinct Player",
                27);
            var restoredFrom = new CareerVersionToken(
                new LineageId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                2,
                Hash('8'));
            var seedBytes = new byte[CareerSeed.ByteLength];
            for (var index = 0; index < seedBytes.Length; index++)
            {
                seedBytes[index] = (byte)(index + 1);
            }

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    Profile,
                    Save,
                    Lineage,
                    4,
                    50,
                    200,
                    Hash('9'),
                    restoredFrom),
                new CareerSeed(seedBytes),
                "Distinct Career",
                draft,
                onboarding,
                CareerProgressionState.Planning(plan),
                TrainingEmphasisLedger.Empty,
                new CareerPlayerRecord(
                    draft.PlayerId,
                    draft.DisplayName,
                    draft.JerseyNumber,
                    attributes),
                new TeamId("university-distinct"),
                PotentialGrade.A,
                23,
                47,
                71,
                receipts);
        }

        private static ConfirmWeekPlanCommand Command(
            CareerSaveSnapshot prior,
            CareerWeekPlanState candidate = null,
            ProfileId? profileId = null,
            SaveId? saveId = null,
            CareerVersionToken? expectedVersionToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 201,
            bool useNullCandidate = false,
            bool corruptAfterCopy = false)
        {
            var command = new ConfirmWeekPlanCommand(
                profileId ?? Profile,
                saveId ?? Save,
                expectedVersionToken ?? prior.Identity.VersionToken,
                operationId ?? Operation,
                completedAtUtcMs,
                useNullCandidate ? null : candidate ?? Candidate(prior));
            if (corruptAfterCopy)
            {
                CorruptSlot(
                    command.CandidatePlan,
                    0,
                    new CareerWeekActionState(
                        Slot(1),
                        Occurrence(2),
                        CareerWeekActionKind.Match,
                        "schedule.u1w1.match.01"));
            }

            return command;
        }

        private static CareerWeekPlanState Candidate(
            CareerSaveSnapshot prior,
            WeekPlanId? planId = null,
            int season = 1,
            int week = 1,
            SlotActionId? firstSlotActionId = null,
            OccurrenceId? firstOccurrenceId = null,
            CareerWeekActionKind firstKind = CareerWeekActionKind.SpecializedTraining,
            string firstContentId = "week_action.specialized.spike",
            SlotActionId? secondSlotActionId = null,
            OccurrenceId? secondOccurrenceId = null,
            CareerWeekActionKind secondKind = CareerWeekActionKind.Rest,
            string secondContentId = "week_action.rest.standard",
            SlotActionId? matchSlotActionId = null,
            OccurrenceId? matchOccurrenceId = null,
            string matchContentId = "schedule.u1w1.match.01",
            bool isConfirmed = true)
        {
            var authoritativeMatch = prior.Progression.WeekPlan.Slots[2];
            return new CareerWeekPlanState(
                planId ?? prior.Progression.WeekPlan.PlanId,
                season,
                week,
                new[]
                {
                    new CareerWeekActionState(
                        firstSlotActionId ?? Slot(1),
                        firstOccurrenceId ?? Occurrence(2),
                        firstKind,
                        firstContentId),
                    new CareerWeekActionState(
                        secondSlotActionId ?? Slot(3),
                        secondOccurrenceId ?? Occurrence(4),
                        secondKind,
                        secondContentId),
                    new CareerWeekActionState(
                        matchSlotActionId ?? authoritativeMatch.SlotActionId,
                        matchOccurrenceId ?? authoritativeMatch.OccurrenceId,
                        CareerWeekActionKind.Match,
                        matchContentId)
                },
                isConfirmed);
        }

        private static CareerSaveSnapshot AppliedSnapshot(
            CareerSaveSnapshot prior,
            ConfirmWeekPlanCommand command)
        {
            var summary = OperationOutcomeSummary.ForWeekPlanConfirmed();
            var receipts = new List<OperationReceipt>(prior.OperationReceipts)
            {
                new OperationReceipt(
                    command.OperationId,
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(command.CandidatePlan.PlanId),
                    CareerOperationFingerprintV1.Hash(command),
                    prior.Identity.LineageId,
                    prior.Identity.Revision + 1,
                    command.CompletedAtUtcMs,
                    OperationOutcomeKind.WeekPlanConfirmed,
                    summary)
            };
            return new CareerSaveSnapshot(
                prior.Versions,
                new CareerSaveIdentity(
                    prior.Identity.ProfileId,
                    prior.Identity.SaveId,
                    prior.Identity.LineageId,
                    prior.Identity.Revision + 1,
                    prior.Identity.CreatedAtUtcMs,
                    command.CompletedAtUtcMs,
                    prior.Identity.SnapshotHash,
                    prior.Identity.RestoredFromVersionToken),
                prior.CareerSeed,
                prior.CareerName,
                prior.PlayerDraft,
                prior.Onboarding,
                CareerProgressionState.Planned(command.CandidatePlan, 1),
                prior.TrainingEmphases,
                prior.Player,
                prior.TeamId,
                prior.PotentialGrade,
                prior.Fatigue,
                prior.Mindset,
                prior.CoachTrust,
                receipts);
        }

        private static CareerSaveSnapshot CopySnapshot(
            CareerSaveSnapshot source,
            CareerSaveVersions versions = null,
            CareerSaveIdentity identity = null)
        {
            return new CareerSaveSnapshot(
                versions ?? source.Versions,
                identity ?? source.Identity,
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
        }

        private static TryoutStageState ConfirmedStage(
            int stageNumber,
            int occurrenceValue,
            int perturbationBase)
        {
            return new TryoutStageState(
                stageNumber,
                Occurrence(occurrenceValue),
                CareerSaveVersions.CurrentCareerRandomAlgorithmVersion,
                "tryout.stage." + stageNumber + ".choice",
                new[]
                {
                    new TryoutResolvedOutput(
                        "stage." + stageNumber + ".output.primary",
                        perturbationBase),
                    new TryoutResolvedOutput(
                        "stage." + stageNumber + ".output.secondary",
                        perturbationBase + 1)
                });
        }

        private static OperationReceipt Receipt(
            int revision,
            OperationKind kind,
            OperationReceiptTarget target,
            OperationOutcomeSummary summary,
            long completedAtUtcMs)
        {
            return new OperationReceipt(
                OperationFor(revision),
                kind,
                target,
                Hash((char)('a' + revision)),
                Lineage,
                revision,
                completedAtUtcMs,
                summary.OutcomeKind,
                summary);
        }

        private static void AssertUnchanged(CareerSaveSnapshot prior, CareerSaveSnapshot next)
        {
            Assert.That(next.Identity.ProfileId, Is.EqualTo(prior.Identity.ProfileId));
            Assert.That(next.Identity.SaveId, Is.EqualTo(prior.Identity.SaveId));
            Assert.That(next.Identity.LineageId, Is.EqualTo(prior.Identity.LineageId));
            Assert.That(next.Identity.CreatedAtUtcMs, Is.EqualTo(prior.Identity.CreatedAtUtcMs));
            Assert.That(prior.Identity.SnapshotHash.Value, Is.Not.EqualTo(ZeroHash));
            Assert.That(next.Identity.SnapshotHash.Value, Is.EqualTo(ZeroHash));
            Assert.That(prior.Identity.RestoredFromVersionToken.HasValue, Is.True);
            Assert.That(next.Identity.RestoredFromVersionToken.HasValue, Is.True);
            Assert.That(
                next.Identity.RestoredFromVersionToken.Value.LineageId,
                Is.EqualTo(prior.Identity.RestoredFromVersionToken.Value.LineageId));
            Assert.That(
                next.Identity.RestoredFromVersionToken.Value.Revision,
                Is.EqualTo(prior.Identity.RestoredFromVersionToken.Value.Revision));
            Assert.That(
                next.Identity.RestoredFromVersionToken.Value.SnapshotHash,
                Is.EqualTo(prior.Identity.RestoredFromVersionToken.Value.SnapshotHash));
            Assert.That(next.Versions.SchemaVersion, Is.EqualTo(prior.Versions.SchemaVersion));
            Assert.That(next.Versions.ContentVersion, Is.EqualTo(prior.Versions.ContentVersion));
            Assert.That(next.Versions.RulesetVersion, Is.EqualTo(prior.Versions.RulesetVersion));
            Assert.That(
                next.Versions.CareerRandomAlgorithmVersion,
                Is.EqualTo(prior.Versions.CareerRandomAlgorithmVersion));
            CollectionAssert.AreEqual(prior.CareerSeed.ToBytes(), next.CareerSeed.ToBytes());
            Assert.That(next.CareerName, Is.EqualTo(prior.CareerName));
            Assert.That(next.PlayerDraft.PlayerId, Is.EqualTo(prior.PlayerDraft.PlayerId));
            Assert.That(next.PlayerDraft.DisplayName, Is.EqualTo(prior.PlayerDraft.DisplayName));
            Assert.That(next.PlayerDraft.JerseyNumber, Is.EqualTo(prior.PlayerDraft.JerseyNumber));
            Assert.That(
                next.Onboarding.CurrentStageNumber,
                Is.EqualTo(prior.Onboarding.CurrentStageNumber));
            Assert.That(next.Onboarding.IsComplete, Is.EqualTo(prior.Onboarding.IsComplete));
            Assert.That(next.Onboarding.IsFormallyEnrolled, Is.EqualTo(prior.Onboarding.IsFormallyEnrolled));
            Assert.That(next.Onboarding.Stages, Has.Count.EqualTo(prior.Onboarding.Stages.Count));
            for (var stageIndex = 0; stageIndex < prior.Onboarding.Stages.Count; stageIndex++)
            {
                var expectedStage = prior.Onboarding.Stages[stageIndex];
                var actualStage = next.Onboarding.Stages[stageIndex];
                Assert.That(actualStage.StageNumber, Is.EqualTo(expectedStage.StageNumber));
                Assert.That(actualStage.OccurrenceId, Is.EqualTo(expectedStage.OccurrenceId));
                Assert.That(actualStage.RandomVersion, Is.EqualTo(expectedStage.RandomVersion));
                Assert.That(actualStage.ChoiceId, Is.EqualTo(expectedStage.ChoiceId));
                Assert.That(actualStage.IsConfirmed, Is.EqualTo(expectedStage.IsConfirmed));
                AssertResolvedOutputs(expectedStage.ResolvedOutputs, actualStage.ResolvedOutputs);
            }

            Assert.That(next.Player.PlayerId, Is.EqualTo(prior.Player.PlayerId));
            Assert.That(next.Player.DisplayName, Is.EqualTo(prior.Player.DisplayName));
            Assert.That(next.Player.JerseyNumber, Is.EqualTo(prior.Player.JerseyNumber));
            AssertAttributes(prior.Player.Attributes, next.Player.Attributes);
            Assert.That(next.TeamId, Is.EqualTo(prior.TeamId));
            Assert.That(next.PotentialGrade, Is.EqualTo(prior.PotentialGrade));
            Assert.That(next.Fatigue, Is.EqualTo(prior.Fatigue));
            Assert.That(next.Mindset, Is.EqualTo(prior.Mindset));
            Assert.That(next.CoachTrust, Is.EqualTo(prior.CoachTrust));
            for (var receiptIndex = 0; receiptIndex < prior.OperationReceipts.Count; receiptIndex++)
            {
                AssertReceipt(
                    prior.OperationReceipts[receiptIndex],
                    next.OperationReceipts[receiptIndex]);
            }
        }

        private static void AssertAttributes(
            CareerPlayerAttributes expected,
            CareerPlayerAttributes actual)
        {
            Assert.That(actual.Spike, Is.EqualTo(expected.Spike));
            Assert.That(actual.Serve, Is.EqualTo(expected.Serve));
            Assert.That(actual.Reception, Is.EqualTo(expected.Reception));
            Assert.That(actual.Defense, Is.EqualTo(expected.Defense));
            Assert.That(actual.Block, Is.EqualTo(expected.Block));
            Assert.That(actual.Movement, Is.EqualTo(expected.Movement));
            Assert.That(actual.Jump, Is.EqualTo(expected.Jump));
            Assert.That(actual.Stamina, Is.EqualTo(expected.Stamina));
        }

        private static void AssertReceipt(OperationReceipt expected, OperationReceipt actual)
        {
            Assert.That(actual.OperationId, Is.EqualTo(expected.OperationId));
            Assert.That(actual.OperationKind, Is.EqualTo(expected.OperationKind));
            Assert.That(actual.Target.OperationKind, Is.EqualTo(expected.Target.OperationKind));
            Assert.That(actual.Target.TryoutStage, Is.EqualTo(expected.Target.TryoutStage));
            Assert.That(actual.Target.TryoutOccurrenceId, Is.EqualTo(expected.Target.TryoutOccurrenceId));
            Assert.That(actual.Target.ChoiceId, Is.EqualTo(expected.Target.ChoiceId));
            Assert.That(actual.Target.WeekPlanId, Is.EqualTo(expected.Target.WeekPlanId));
            Assert.That(actual.Target.SlotActionId, Is.EqualTo(expected.Target.SlotActionId));
            Assert.That(actual.Target.ActionOccurrenceId, Is.EqualTo(expected.Target.ActionOccurrenceId));
            Assert.That(actual.Target.EventOccurrenceId, Is.EqualTo(expected.Target.EventOccurrenceId));
            Assert.That(actual.Target.OptionId, Is.EqualTo(expected.Target.OptionId));
            Assert.That(actual.InputFingerprint, Is.EqualTo(expected.InputFingerprint));
            Assert.That(actual.AppliedLineageId, Is.EqualTo(expected.AppliedLineageId));
            Assert.That(actual.AppliedRevision, Is.EqualTo(expected.AppliedRevision));
            Assert.That(actual.CompletedAtUtcMs, Is.EqualTo(expected.CompletedAtUtcMs));
            Assert.That(actual.OutcomeKind, Is.EqualTo(expected.OutcomeKind));
            AssertOutcomeSummary(expected.OutcomeSummary, actual.OutcomeSummary);
        }

        private static void AssertOutcomeSummary(
            OperationOutcomeSummary expected,
            OperationOutcomeSummary actual)
        {
            Assert.That(actual.OutcomeKind, Is.EqualTo(expected.OutcomeKind));
            AssertResolvedOutputs(
                expected.TryoutResolvedOutputs,
                actual.TryoutResolvedOutputs);
            if (expected.GrowthExperienceDelta == null)
            {
                Assert.That(actual.GrowthExperienceDelta, Is.Null);
            }
            else
            {
                Assert.That(actual.GrowthExperienceDelta, Is.Not.Null);
                Assert.That(actual.GrowthExperienceDelta.Spike, Is.EqualTo(expected.GrowthExperienceDelta.Spike));
                Assert.That(actual.GrowthExperienceDelta.Serve, Is.EqualTo(expected.GrowthExperienceDelta.Serve));
                Assert.That(actual.GrowthExperienceDelta.Reception, Is.EqualTo(expected.GrowthExperienceDelta.Reception));
                Assert.That(actual.GrowthExperienceDelta.Defense, Is.EqualTo(expected.GrowthExperienceDelta.Defense));
                Assert.That(actual.GrowthExperienceDelta.Block, Is.EqualTo(expected.GrowthExperienceDelta.Block));
                Assert.That(actual.GrowthExperienceDelta.Movement, Is.EqualTo(expected.GrowthExperienceDelta.Movement));
                Assert.That(actual.GrowthExperienceDelta.Jump, Is.EqualTo(expected.GrowthExperienceDelta.Jump));
                Assert.That(actual.GrowthExperienceDelta.Stamina, Is.EqualTo(expected.GrowthExperienceDelta.Stamina));
            }

            Assert.That(actual.FatigueDelta, Is.EqualTo(expected.FatigueDelta));
            Assert.That(actual.MindsetDelta, Is.EqualTo(expected.MindsetDelta));
            Assert.That(actual.CoachTrustDelta, Is.EqualTo(expected.CoachTrustDelta));
        }

        private static void AssertResolvedOutputs(
            IReadOnlyList<TryoutResolvedOutput> expected,
            IReadOnlyList<TryoutResolvedOutput> actual)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].OutputId, Is.EqualTo(expected[index].OutputId));
                Assert.That(actual[index].Perturbation, Is.EqualTo(expected[index].Perturbation));
            }
        }

        private static void AssertPlanEqual(CareerWeekPlanState expected, CareerWeekPlanState actual)
        {
            Assert.That(actual.PlanId, Is.EqualTo(expected.PlanId));
            Assert.That(actual.Season, Is.EqualTo(expected.Season));
            Assert.That(actual.Week, Is.EqualTo(expected.Week));
            Assert.That(actual.IsConfirmed, Is.EqualTo(expected.IsConfirmed));
            for (var index = 0; index < expected.Slots.Count; index++)
            {
                Assert.That(actual.Slots[index].SlotActionId, Is.EqualTo(expected.Slots[index].SlotActionId));
                Assert.That(actual.Slots[index].OccurrenceId, Is.EqualTo(expected.Slots[index].OccurrenceId));
                Assert.That(actual.Slots[index].Kind, Is.EqualTo(expected.Slots[index].Kind));
                Assert.That(actual.Slots[index].ContentId, Is.EqualTo(expected.Slots[index].ContentId));
            }
        }

        private static void CorruptSlot(
            CareerWeekPlanState plan,
            int index,
            CareerWeekActionState replacement)
        {
            var field = typeof(CareerWeekPlanState).GetField(
                "_slots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var slots = (CareerWeekActionState[])field.GetValue(plan);
            slots[index] = replacement;
        }

        private static SlotActionId Slot(int value)
        {
            return new SlotActionId(Guid.Parse("00000000-0000-0000-0000-" + value.ToString("D12")));
        }

        private static OccurrenceId Occurrence(int value)
        {
            return new OccurrenceId(Guid.Parse("10000000-0000-0000-0000-" + value.ToString("D12")));
        }

        private static WeekPlanId WeekPlan(int value)
        {
            return new WeekPlanId(Guid.Parse("20000000-0000-0000-0000-" + value.ToString("D12")));
        }

        private static OperationId OperationFor(int value)
        {
            return new OperationId(Guid.Parse("30000000-0000-0000-0000-" + value.ToString("D12")));
        }

        private static Sha256Digest Hash(char value)
        {
            return new Sha256Digest(new string(value, 64));
        }

        private readonly struct InvalidCase
        {
            public InvalidCase(CareerSaveSnapshot snapshot, ConfirmWeekPlanCommand command)
            {
                Snapshot = snapshot;
                Command = command;
            }

            public CareerSaveSnapshot Snapshot { get; }

            public ConfirmWeekPlanCommand Command { get; }
        }

        private readonly struct ExecuteInvalidCase
        {
            public ExecuteInvalidCase(
                CareerSaveSnapshot snapshot,
                ExecuteWeekActionCommand command)
            {
                Snapshot = snapshot;
                Command = command;
            }

            public CareerSaveSnapshot Snapshot { get; }
            public ExecuteWeekActionCommand Command { get; }
        }

        private sealed class RandomCall
        {
            public RandomCall(
                CareerRandomRequest request,
                long minimum,
                long maximum,
                long result)
            {
                Request = request;
                Minimum = minimum;
                Maximum = maximum;
                Result = result;
            }

            public CareerRandomRequest Request { get; }
            public long Minimum { get; }
            public long Maximum { get; }
            public long Result { get; }
        }

        private sealed class RecordingRandom : IDeterministicCareerRandom
        {
            private readonly IDeterministicCareerRandom _inner;

            public RecordingRandom(IDeterministicCareerRandom inner)
            {
                _inner = inner;
            }

            public List<RandomCall> Calls { get; } = new List<RandomCall>();

            public long NextInt64(
                CareerRandomRequest request,
                long minInclusive,
                long maxExclusive)
            {
                var result = _inner.NextInt64(request, minInclusive, maxExclusive);
                Calls.Add(new RandomCall(request, minInclusive, maxExclusive, result));
                return result;
            }
        }

        private sealed class InterleavingRandom : IDeterministicCareerRandom
        {
            private readonly IDeterministicCareerRandom _inner;

            public InterleavingRandom(IDeterministicCareerRandom inner)
            {
                _inner = inner;
            }

            public int RequestedCalls { get; private set; }
            public int UnrelatedCalls { get; private set; }

            public long NextInt64(
                CareerRandomRequest request,
                long minInclusive,
                long maxExclusive)
            {
                var unrelated = new CareerRandomRequest(
                    request.AlgorithmVersion,
                    request.Seed,
                    "event",
                    request.Season,
                    request.Week,
                    "event.team_meal.option.attend",
                    new OccurrenceId(
                        Guid.Parse("00000000-0000-0000-0000-000000000077")),
                    0);
                _inner.NextInt64(unrelated, 0, 10000);
                UnrelatedCalls++;
                RequestedCalls++;
                return _inner.NextInt64(request, minInclusive, maxExclusive);
            }
        }

        private sealed class ThrowingRandom : IDeterministicCareerRandom
        {
            private readonly bool _invalidOperation;

            public ThrowingRandom(bool invalidOperation)
            {
                _invalidOperation = invalidOperation;
            }

            public int CallCount { get; private set; }

            public long NextInt64(
                CareerRandomRequest request,
                long minInclusive,
                long maxExclusive)
            {
                CallCount++;
                if (_invalidOperation)
                {
                    throw new InvalidOperationException("Injected random dependency failure.");
                }

                throw new IOException("Injected random dependency failure.");
            }
        }

        private sealed class MemoryRepository : ICareerSaveRepository
        {
            public MemoryRepository(CareerSaveSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public CareerSaveSnapshot Snapshot { get; private set; }

            public int LoadCount { get; private set; }

            public int CommitCount { get; private set; }

            public PersistenceResultKind? LoadFailure { get; set; }

            public PersistenceResultKind? CommitFailure { get; set; }

            public int? NullLoadCallNumber { get; set; }

            public int? ThrowLoadCallNumber { get; set; }

            public bool ReturnNullCommit { get; set; }

            public bool ThrowCommit { get; set; }

            public bool MalformedLoadWithoutSnapshot { get; set; }

            public bool MalformedSuccessWithoutSnapshot { get; set; }

            public PersistenceResultKind SuccessfulCommitKind { get; set; } =
                PersistenceResultKind.Committed;

            public CareerSaveSnapshot SuccessfulCommitPayload { get; set; }

            public CareerSaveSnapshot CommitRaceWinner { get; set; }

            public CareerPersistenceResult Create(
                CareerSaveSnapshot initialSnapshot,
                OperationId operationId)
            {
                throw new NotSupportedException();
            }

            public CareerPersistenceResult Load(ProfileId profileId, SaveId saveId)
            {
                LoadCount++;
                if (NullLoadCallNumber == LoadCount)
                {
                    return null;
                }

                if (ThrowLoadCallNumber == LoadCount)
                {
                    throw new IOException("Injected repository load failure.");
                }

                if (LoadFailure.HasValue)
                {
                    return new CareerPersistenceResult(LoadFailure.Value);
                }

                if (MalformedLoadWithoutSnapshot)
                {
                    return new CareerPersistenceResult(PersistenceResultKind.Loaded);
                }

                return Snapshot == null
                    ? new CareerPersistenceResult(PersistenceResultKind.NotFound)
                    : new CareerPersistenceResult(PersistenceResultKind.Loaded, Snapshot);
            }

            public CareerPersistenceResult Commit(
                ProfileId profileId,
                SaveId saveId,
                CareerVersionToken expectedVersionToken,
                CareerSaveSnapshot nextSnapshot,
                OperationId operationId)
            {
                CommitCount++;
                if (ReturnNullCommit)
                {
                    return null;
                }

                if (ThrowCommit)
                {
                    throw new IOException("Injected repository commit failure.");
                }

                if (MalformedSuccessWithoutSnapshot)
                {
                    return new CareerPersistenceResult(SuccessfulCommitKind);
                }

                if (CommitRaceWinner != null)
                {
                    Snapshot = CommitRaceWinner;
                    CommitRaceWinner = null;
                    return new CareerPersistenceResult(PersistenceResultKind.VersionConflict);
                }

                if (CommitFailure.HasValue)
                {
                    return new CareerPersistenceResult(CommitFailure.Value);
                }

                if (Snapshot == null || !Snapshot.Identity.VersionToken.Equals(expectedVersionToken))
                {
                    return new CareerPersistenceResult(PersistenceResultKind.VersionConflict);
                }

                Snapshot = SuccessfulCommitPayload ?? nextSnapshot;
                return new CareerPersistenceResult(SuccessfulCommitKind, Snapshot);
            }

            public CareerPersistenceResult RecoverFromBackup(
                ProfileId profileId,
                SaveId saveId,
                CareerVersionToken confirmedBackupVersionToken,
                Sha256Digest? confirmedCorruptMainFingerprint,
                OperationId operationId,
                long recoveredAtUtcMs,
                LineageId newLineageId)
            {
                throw new NotSupportedException();
            }
        }
    }
}
