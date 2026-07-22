using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekCommandServiceTests
    {
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

            var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(command);

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
                var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(command);

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
                var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(command);

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
                var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(testCase.Command);

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
            var service = new CareerWeekCommandService(repository);
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
            var service = new CareerWeekCommandService(repository);
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
            var service = new CareerWeekCommandService(repository);
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
                    12,
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
            var notFound = new CareerWeekCommandService(notFoundRepository)
                .ConfirmWeekPlan(Command(prior));
            Assert.That(notFound.Status, Is.EqualTo(CareerApplicationStatus.NotFound));
            Assert.That(notFound.PersistenceKind, Is.EqualTo(PersistenceResultKind.NotFound));
            Assert.That(notFound.Snapshot, Is.Null);

            var loadFailureRepository = new MemoryRepository(prior)
            {
                LoadFailure = PersistenceResultKind.IoFailure
            };
            var loadFailure = new CareerWeekCommandService(loadFailureRepository)
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
                var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(Command(prior));

                Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
                Assert.That(result.PersistenceKind, Is.EqualTo(failureKind));
                Assert.That(result.Snapshot, Is.SameAs(prior));
                Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(4));
                Assert.That(result.OutcomeSummary, Is.Null);
                Assert.That(repository.Snapshot, Is.SameAs(prior));
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

            var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(Command(prior));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(result.PersistenceKind, Is.EqualTo(PersistenceResultKind.BackupDegraded));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(5));
            Assert.That(result.OutcomeSummary.OutcomeKind, Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
        }

        [Test]
        public void ConfirmWeekPlan_ServiceSurfaceHasNoRandomDependencyOrFutureCommandShells()
        {
            var constructors = typeof(CareerWeekCommandService).GetConstructors();
            var methods = typeof(CareerWeekCommandService).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var fields = typeof(CareerWeekCommandService).GetFields(
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(ICareerSaveRepository) }));
            Assert.That(methods.Select(method => method.Name), Is.EqualTo(new[] { "ConfirmWeekPlan" }));
            Assert.That(
                fields.Any(field => field.FieldType == typeof(IDeterministicCareerRandom)),
                Is.False);
        }

        private static void AssertRace(
            ConfirmWeekPlanCommand command,
            CareerSaveSnapshot prior,
            CareerSaveSnapshot winner,
            CareerApplicationStatus expectedStatus,
            bool hasConflict)
        {
            var repository = new MemoryRepository(prior) { CommitRaceWinner = winner };

            var result = new CareerWeekCommandService(repository).ConfirmWeekPlan(command);

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Snapshot, Is.SameAs(winner));
            Assert.That(result.ConflictingReceipt != null, Is.EqualTo(hasConflict));
            Assert.That(result.OutcomeSummary != null, Is.EqualTo(expectedStatus == CareerApplicationStatus.Existing));
            Assert.That(repository.CommitCount, Is.EqualTo(1));
            Assert.That(repository.LoadCount, Is.EqualTo(2));
        }

        private static CareerSaveSnapshot PlanningSnapshot()
        {
            return CareerPersistenceTestData.PlanningSnapshot(
                Profile,
                Save,
                Lineage,
                revision: 4,
                updatedAtUtcMs: 10);
        }

        private static ConfirmWeekPlanCommand Command(
            CareerSaveSnapshot prior,
            CareerWeekPlanState candidate = null,
            ProfileId? profileId = null,
            SaveId? saveId = null,
            CareerVersionToken? expectedVersionToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 11,
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

        private static void AssertUnchanged(CareerSaveSnapshot prior, CareerSaveSnapshot next)
        {
            Assert.That(next.Identity.ProfileId, Is.EqualTo(prior.Identity.ProfileId));
            Assert.That(next.Identity.SaveId, Is.EqualTo(prior.Identity.SaveId));
            Assert.That(next.Identity.LineageId, Is.EqualTo(prior.Identity.LineageId));
            Assert.That(next.Identity.CreatedAtUtcMs, Is.EqualTo(prior.Identity.CreatedAtUtcMs));
            Assert.That(next.Identity.RestoredFromVersionToken, Is.EqualTo(prior.Identity.RestoredFromVersionToken));
            Assert.That(next.Versions, Is.EqualTo(prior.Versions));
            Assert.That(next.CareerSeed, Is.EqualTo(prior.CareerSeed));
            Assert.That(next.CareerName, Is.EqualTo(prior.CareerName));
            Assert.That(next.PlayerDraft.PlayerId, Is.EqualTo(prior.PlayerDraft.PlayerId));
            Assert.That(next.PlayerDraft.DisplayName, Is.EqualTo(prior.PlayerDraft.DisplayName));
            Assert.That(next.PlayerDraft.JerseyNumber, Is.EqualTo(prior.PlayerDraft.JerseyNumber));
            Assert.That(next.Onboarding.IsComplete, Is.EqualTo(prior.Onboarding.IsComplete));
            Assert.That(next.Onboarding.IsFormallyEnrolled, Is.EqualTo(prior.Onboarding.IsFormallyEnrolled));
            Assert.That(next.Onboarding.Stages.Select(stage => stage.OccurrenceId),
                Is.EqualTo(prior.Onboarding.Stages.Select(stage => stage.OccurrenceId)));
            Assert.That(next.Player.PlayerId, Is.EqualTo(prior.Player.PlayerId));
            Assert.That(next.Player.DisplayName, Is.EqualTo(prior.Player.DisplayName));
            Assert.That(next.Player.JerseyNumber, Is.EqualTo(prior.Player.JerseyNumber));
            Assert.That(next.Player.Attributes, Is.EqualTo(prior.Player.Attributes));
            Assert.That(next.TeamId, Is.EqualTo(prior.TeamId));
            Assert.That(next.PotentialGrade, Is.EqualTo(prior.PotentialGrade));
            Assert.That(next.Fatigue, Is.EqualTo(prior.Fatigue));
            Assert.That(next.Mindset, Is.EqualTo(prior.Mindset));
            Assert.That(next.CoachTrust, Is.EqualTo(prior.CoachTrust));
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

            public PersistenceResultKind SuccessfulCommitKind { get; set; } =
                PersistenceResultKind.Committed;

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
                if (LoadFailure.HasValue)
                {
                    return new CareerPersistenceResult(LoadFailure.Value);
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

                Snapshot = nextSnapshot;
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
