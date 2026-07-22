using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerOnboardingServiceTests
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";
        private const string SeedHex =
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

        [Test]
        public void FingerprintV1_HasLockedCanonicalCreateAndConfirmBytesAndHashes()
        {
            var create = CreateCommand();
            var confirm = ConfirmCommand(
                create,
                Token(create.LineageId, 1),
                1,
                "tryout.attack.choice.power",
                NewOperation(21));
            const string createJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"create_career\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"lineageId\":\"33333333-3333-3333-3333-333333333333\",\"playerId\":\"player.one\",\"careerName\":\"First Career\",\"playerName\":\"Player One\",\"jerseyNumber\":7,\"tryoutOccurrenceIds\":[\"00000000-0000-0000-0000-000000000001\",\"00000000-0000-0000-0000-000000000002\",\"00000000-0000-0000-0000-000000000003\"],\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";
            const string confirmJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"confirm_tryout_stage\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"expectedLineageId\":\"33333333-3333-3333-3333-333333333333\",\"expectedRevision\":1,\"expectedSnapshotHash\":\"" + ZeroHash + "\",\"stageNumber\":1,\"choiceId\":\"tryout.attack.choice.power\",\"tryoutOccurrenceId\":\"00000000-0000-0000-0000-000000000001\",\"weekPlanId\":null,\"matchSlotActionId\":null,\"matchOccurrenceId\":null,\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";

            Assert.That(
                Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(create)),
                Is.EqualTo(createJson));
            Assert.That(
                CareerOperationFingerprintV1.Hash(create).Value,
                Is.EqualTo("85e1107c2f4cd368e83923a762162399f08a46aa5984120d60b7bf2f46f39db2"));
            Assert.That(
                Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(confirm, Occurrence(1))),
                Is.EqualTo(confirmJson));
            Assert.That(
                CareerOperationFingerprintV1.Hash(confirm, Occurrence(1)).Value,
                Is.EqualTo("5ad04826d3ff53b40e44cc7bba28c2c2c49a6a67b5bc8442deb798fb56efbff9"));
        }

        [Test]
        public void CreateCareer_CreatesRevisionOneAndRetryDoesNotConsumeAnotherSeed()
        {
            var repository = new MemoryCareerRepository();
            var seeds = new FixedSeedSource();
            var service = Service(repository, seeds);
            var command = CreateCommand();

            var applied = service.CreateCareer(command);
            var retried = service.CreateCareer(command);

            Assert.That(applied.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(retried.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(seeds.Count, Is.EqualTo(1));
            Assert.That(repository.CreateCount, Is.EqualTo(1));
            var snapshot = retried.Snapshot;
            Assert.That(snapshot.Identity.Revision, Is.EqualTo(1));
            Assert.That(snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.CareerCreated));
            Assert.That(snapshot.Onboarding.CurrentStageNumber, Is.EqualTo(1));
            Assert.That(snapshot.Onboarding.Stages.Select(x => x.OccurrenceId),
                Is.EqualTo(command.TryoutOccurrenceIds));
            Assert.That(snapshot.Onboarding.Stages.All(x => !x.IsConfirmed), Is.True);
            Assert.That(snapshot.OperationReceipts, Has.Count.EqualTo(1));
            Assert.That(snapshot.OperationReceipts[0].AppliedRevision, Is.EqualTo(1));
            Assert.That(snapshot.OperationReceipts[0].OutcomeKind,
                Is.EqualTo(OperationOutcomeKind.CareerCreated));
        }

        [Test]
        public void CreateCareer_ValidatesBeforeLoadAndReportsPersistenceFailure()
        {
            var invalid = CreateCommand(
                occurrences: new[] { Occurrence(1), Occurrence(1), Occurrence(3) });
            var invalidRepository = new MemoryCareerRepository();
            var invalidSeeds = new FixedSeedSource();

            var invalidResult = Service(invalidRepository, invalidSeeds).CreateCareer(invalid);

            Assert.That(invalidResult.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(invalidRepository.LoadCount, Is.EqualTo(0));
            Assert.That(invalidSeeds.Count, Is.EqualTo(0));

            var failingRepository = new MemoryCareerRepository
            {
                CreateFailure = PersistenceResultKind.IoFailure
            };
            var failed = Service(failingRepository).CreateCareer(CreateCommand());
            Assert.That(failed.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(failed.PersistenceKind, Is.EqualTo(PersistenceResultKind.IoFailure));
            Assert.That(failed.Snapshot, Is.Null);
        }

        [Test]
        public void CreateCareer_SameOperationWithDifferentFingerprintConflictsWithoutOverwrite()
        {
            var repository = new MemoryCareerRepository();
            var service = Service(repository);
            var original = CreateCommand();
            service.CreateCareer(original);
            var changed = CreateCommand(playerName: "Different Name");

            var result = service.CreateCareer(changed);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.OperationConflict));
            Assert.That(result.ConflictingReceipt, Is.Not.Null);
            Assert.That(repository.CreateCount, Is.EqualTo(1));
            Assert.That(repository.Snapshot.PlayerDraft.DisplayName, Is.EqualTo("Player One"));
        }

        [Test]
        public void ConfirmTryout_StrictlyAdvancesThreeStagesAndEnrollsAtomicallyAtRevisionFour()
        {
            var repository = new MemoryCareerRepository();
            var random = new CountingRandom();
            var service = Service(repository, random: random);
            var create = CreateCommand();
            var created = service.CreateCareer(create);

            var stage1 = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                NewOperation(21)));
            var stage2 = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage1.Snapshot.Identity.VersionToken,
                2,
                "tryout.reception_defense.choice.first_touch",
                NewOperation(22)));
            var enrollment = new TryoutEnrollmentIds(
                new WeekPlanId(Guid.Parse("00000000-0000-0000-0000-000000000010")),
                new SlotActionId(Guid.Parse("00000000-0000-0000-0000-000000000011")),
                Occurrence(12));
            var stage3 = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage2.Snapshot.Identity.VersionToken,
                3,
                "tryout.scrimmage.choice.endurance",
                NewOperation(23),
                enrollment));

            Assert.That(stage1.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(stage1.Snapshot.Identity.Revision, Is.EqualTo(2));
            Assert.That(stage1.Snapshot.Progression.TryoutStage, Is.EqualTo(2));
            Assert.That(stage2.Snapshot.Identity.Revision, Is.EqualTo(3));
            Assert.That(stage2.Snapshot.Progression.TryoutStage, Is.EqualTo(3));
            Assert.That(stage3.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(stage3.Snapshot.Identity.Revision, Is.EqualTo(4));
            Assert.That(stage3.Snapshot.Onboarding.IsFormallyEnrolled, Is.True);
            Assert.That(stage3.Snapshot.OperationReceipts, Has.Count.EqualTo(4));
            Assert.That(stage3.Snapshot.OperationReceipts.Select(x => x.AppliedRevision),
                Is.EqualTo(new long[] { 1, 2, 3, 4 }));
            Assert.That(stage3.Snapshot.Player.Position,
                Is.EqualTo(CareerPlayerPosition.OutsideHitter));
            Assert.That(stage3.Snapshot.TeamId.Value.Value, Is.EqualTo("team.university.first"));
            Assert.That(AllAttributes(stage3.Snapshot.Player).All(x => x.GrowthExperience == 0),
                Is.True);
            Assert.That(stage3.Snapshot.PotentialGrade,
                Is.EqualTo(ExpectedPotential(stage3.Snapshot.Player.Attributes)));
            Assert.That(stage3.Snapshot.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planning));
            var plan = stage3.Snapshot.Progression.WeekPlan;
            Assert.That(plan.Season, Is.EqualTo(1));
            Assert.That(plan.Week, Is.EqualTo(1));
            Assert.That(plan.IsConfirmed, Is.False);
            Assert.That(plan.Slots[0], Is.Null);
            Assert.That(plan.Slots[1], Is.Null);
            Assert.That(plan.Slots[2].Kind, Is.EqualTo(CareerWeekActionKind.Match));
            Assert.That(plan.Slots[2].ContentId, Is.EqualTo("schedule.u1w1.match.01"));
            Assert.That(plan.Slots[2].OccurrenceId, Is.EqualTo(enrollment.MatchOccurrenceId));
            Assert.That(stage3.Snapshot.TrainingEmphases.Contributions, Is.Empty);
            Assert.That(random.Count, Is.EqualTo(11));
            Assert.That(repository.CommitCount, Is.EqualTo(3));

            var beforeRead = repository.Snapshot.Identity.VersionToken;
            var read = repository.Load(create.ProfileId, create.SaveId);
            Assert.That(read.Snapshot.Identity.VersionToken, Is.EqualTo(beforeRead));
            Assert.That(
                typeof(CareerOnboardingService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(x => x.Name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void SameStableInputs_ProduceEquivalentNumbersAfterIrrelevantEnumeration()
        {
            var firstRepository = new MemoryCareerRepository();
            var secondRepository = new MemoryCareerRepository();
            var firstCatalog = TryoutCatalogV1.Create();
            var secondCatalog = TryoutCatalogV1.Create();
            for (var stage = 0; stage < firstCatalog.Stages.Count; stage++)
            {
                for (var choice = 0; choice < firstCatalog.Stages[stage].Choices.Count; choice++)
                {
                    Assert.That(firstCatalog.Stages[stage].Choices[choice].ChoiceId, Is.Not.Empty);
                }
            }

            for (var stage = secondCatalog.Stages.Count - 1; stage >= 0; stage--)
            {
                for (var choice = secondCatalog.Stages[stage].Choices.Count - 1;
                     choice >= 0;
                     choice--)
                {
                    Assert.That(secondCatalog.Stages[stage].Choices[choice].ChoiceId, Is.Not.Empty);
                }
            }

            var first = RunOnboarding(
                Service(
                    firstRepository,
                    random: new IrrelevantRequestRandom(false),
                    catalog: firstCatalog),
                CreateCommand());
            var second = RunOnboarding(
                Service(
                    secondRepository,
                    random: new IrrelevantRequestRandom(true),
                    catalog: secondCatalog),
                CreateCommand());

            Assert.That(
                EncodeDeterministicOutcome(second),
                Is.EqualTo(EncodeDeterministicOutcome(first)));
        }

        [Test]
        public void ControlledPerturbations_ProduceExactOrderedOutputsExplanationsAndPlayerValues()
        {
            var repository = new MemoryCareerRepository();
            var run = RunOnboarding(
                Service(repository, random: new ControlledPerturbationRandom()),
                CreateCommand());

            AssertStage(
                run.StageResults[0],
                new[]
                {
                    "tryout.output.spike",
                    "tryout.output.serve",
                    "tryout.output.jump"
                },
                new[] { -100, -1, 0 });
            AssertExplanation(run.StageResults[0].Explanations[0],
                "tryout.attack", "tryout.output.spike", 5800, -100, 5700);
            AssertExplanation(run.StageResults[0].Explanations[1],
                "tryout.attack", "tryout.output.serve", 4800, -1, 4799);
            AssertExplanation(run.StageResults[0].Explanations[2],
                "tryout.attack", "tryout.output.jump", 5600, 0, 5600);

            AssertStage(
                run.StageResults[1],
                new[]
                {
                    "tryout.output.reception",
                    "tryout.output.defense",
                    "tryout.output.block",
                    "tryout.output.movement"
                },
                new[] { 100, -100, 1, -1 });
            AssertExplanation(run.StageResults[1].Explanations[0],
                "tryout.reception_defense", "tryout.output.reception", 5800, 100, 5900);
            AssertExplanation(run.StageResults[1].Explanations[1],
                "tryout.reception_defense", "tryout.output.defense", 5200, -100, 5100);
            AssertExplanation(run.StageResults[1].Explanations[2],
                "tryout.reception_defense", "tryout.output.block", 4600, 1, 4601);
            AssertExplanation(run.StageResults[1].Explanations[3],
                "tryout.reception_defense", "tryout.output.movement", 5300, -1, 5299);

            AssertStage(
                run.StageResults[2],
                new[]
                {
                    "tryout.output.stamina",
                    "tryout.output.fatigue",
                    "tryout.output.mindset",
                    "tryout.output.coach_trust"
                },
                new[] { 100, -99, -19, 100 });
            AssertExplanation(run.StageResults[2].Explanations[0],
                "tryout.scrimmage", "tryout.output.stamina", 5800, 100, 5900);
            AssertExplanation(run.StageResults[2].Explanations[1],
                "tryout.scrimmage", "tryout.output.fatigue", 8, -4, 4);
            AssertExplanation(run.StageResults[2].Explanations[2],
                "tryout.scrimmage", "tryout.output.mindset", 52, -1, 51);
            AssertExplanation(run.StageResults[2].Explanations[3],
                "tryout.scrimmage", "tryout.output.coach_trust", 48, 10, 58);

            var attributes = AllAttributes(run.Snapshot.Player).ToArray();
            Assert.That(attributes.Select(x => x.AbilityBasisPoints),
                Is.EqualTo(new[] { 5700, 4799, 5900, 5100, 4601, 5299, 5600, 5900 }));
            Assert.That(attributes.All(x => x.GrowthExperience == 0), Is.True);
            Assert.That(run.Snapshot.PotentialGrade, Is.EqualTo(PotentialGrade.B));
            Assert.That(run.Snapshot.Fatigue, Is.EqualTo(4));
            Assert.That(run.Snapshot.Mindset, Is.EqualTo(51));
            Assert.That(run.Snapshot.CoachTrust, Is.EqualTo(58));
        }

        [Test]
        public void ConfirmTryout_PersistsRawOutputsAndIdempotentRetryNeverRedrawsOrCommits()
        {
            var repository = new MemoryCareerRepository();
            var random = new CountingRandom();
            var service = Service(repository, random: random);
            var create = CreateCommand();
            var created = service.CreateCareer(create);
            var command = ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                NewOperation(21));
            var applied = service.ConfirmTryoutStage(command);
            var drawCount = random.Count;
            var commits = repository.CommitCount;

            var existing = service.ConfirmTryoutStage(command);
            var conflict = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                command.ExpectedVersionToken,
                1,
                "tryout.attack.choice.serve",
                command.OperationId));

            Assert.That(existing.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(existing.ResolvedOutputs.Select(x => x.Perturbation),
                Is.EqualTo(applied.ResolvedOutputs.Select(x => x.Perturbation)));
            Assert.That(existing.Explanations.Select(x => x.FinalValue),
                Is.EqualTo(applied.Explanations.Select(x => x.FinalValue)));
            Assert.That(random.Count, Is.EqualTo(drawCount));
            Assert.That(repository.CommitCount, Is.EqualTo(commits));
            Assert.That(conflict.Status, Is.EqualTo(CareerApplicationStatus.OperationConflict));
            Assert.That(repository.CommitCount, Is.EqualTo(commits));
        }

        [Test]
        public void ConfirmTryout_RejectsStaleSequenceWrongChoiceAndEnrollmentMisuse()
        {
            var repository = new MemoryCareerRepository();
            var service = Service(repository);
            var create = CreateCommand();
            var created = service.CreateCareer(create);
            var wrongStage = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                2,
                "tryout.reception_defense.choice.first_touch",
                NewOperation(31)));
            var wrongChoice = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.scrimmage.choice.endurance",
                NewOperation(32)));
            var earlyEnrollment = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                NewOperation(33),
                Enrollment()));
            var applied = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                NewOperation(34)));
            var stale = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                2,
                "tryout.reception_defense.choice.first_touch",
                NewOperation(35)));
            var stage2 = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                applied.Snapshot.Identity.VersionToken,
                2,
                "tryout.reception_defense.choice.first_touch",
                NewOperation(36)));
            var missingEnrollment = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage2.Snapshot.Identity.VersionToken,
                3,
                "tryout.scrimmage.choice.endurance",
                NewOperation(37)));
            var collidingEnrollment = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage2.Snapshot.Identity.VersionToken,
                3,
                "tryout.scrimmage.choice.endurance",
                NewOperation(38),
                EnrollmentWithOccurrence(1)));
            var collidingPlan = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage2.Snapshot.Identity.VersionToken,
                3,
                "tryout.scrimmage.choice.endurance",
                NewOperation(39),
                new TryoutEnrollmentIds(
                    new WeekPlanId(Occurrence(1).Value),
                    new SlotActionId(Guid.Parse("00000000-0000-0000-0000-000000000011")),
                    Occurrence(12))));
            var collidingAction = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                stage2.Snapshot.Identity.VersionToken,
                3,
                "tryout.scrimmage.choice.endurance",
                NewOperation(40),
                new TryoutEnrollmentIds(
                    new WeekPlanId(Guid.Parse("00000000-0000-0000-0000-000000000010")),
                    new SlotActionId(Occurrence(1).Value),
                    Occurrence(12))));

            Assert.That(wrongStage.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(wrongChoice.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(earlyEnrollment.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(applied.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(stale.Status, Is.EqualTo(CareerApplicationStatus.VersionConflict));
            Assert.That(stage2.Status, Is.EqualTo(CareerApplicationStatus.Applied));
            Assert.That(missingEnrollment.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(collidingEnrollment.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(collidingPlan.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(collidingAction.Status, Is.EqualTo(CareerApplicationStatus.InvalidInputOrState));
            Assert.That(repository.CommitCount, Is.EqualTo(2));
        }

        [Test]
        public void ConfirmTryout_FailedCommitReturnsOnlyThePriorAuthoritativeSnapshot()
        {
            var repository = new MemoryCareerRepository();
            var service = Service(repository);
            var create = CreateCommand();
            var created = service.CreateCareer(create);
            repository.CommitFailure = PersistenceResultKind.IoFailure;

            var failed = service.ConfirmTryoutStage(ConfirmCommand(
                create,
                created.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                NewOperation(21)));

            Assert.That(failed.Status, Is.EqualTo(CareerApplicationStatus.PersistenceFailure));
            Assert.That(failed.PersistenceKind, Is.EqualTo(PersistenceResultKind.IoFailure));
            Assert.That(failed.Snapshot, Is.SameAs(repository.Snapshot));
            Assert.That(failed.Snapshot.Identity.Revision, Is.EqualTo(1));
            Assert.That(failed.Snapshot.Onboarding.Stages[0].IsConfirmed, Is.False);
        }

        [Test]
        public void ConfirmTryout_CommitRaceWithMatchingWinnerReturnsExistingAuthoritativeResult()
        {
            var create = CreateCommand();
            var operation = NewOperation(51);
            var winnerRepository = new MemoryCareerRepository();
            var winnerService = Service(winnerRepository);
            var winnerCreated = winnerService.CreateCareer(create);
            var command = ConfirmCommand(
                create,
                winnerCreated.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                operation);
            var winner = winnerService.ConfirmTryoutStage(command).Snapshot;

            var raceRepository = new MemoryCareerRepository();
            var random = new CountingRandom();
            var raceService = Service(raceRepository, random: random);
            raceService.CreateCareer(create);
            raceRepository.CommitRaceWinner = winner;

            var result = raceService.ConfirmTryoutStage(command);

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(result.Snapshot, Is.SameAs(winner));
            Assert.That(result.Snapshot.Identity.VersionToken,
                Is.EqualTo(winner.Identity.VersionToken));
            Assert.That(result.ResolvedOutputs.Select(x => x.Perturbation),
                Is.EqualTo(winner.Onboarding.Stages[0].ResolvedOutputs.Select(x => x.Perturbation)));
            Assert.That(result.Explanations, Has.Count.EqualTo(3));
            Assert.That(random.Count, Is.EqualTo(3));
            Assert.That(raceRepository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmTryout_CommitRaceWithConflictingWinnerReturnsOldEvidence()
        {
            var create = CreateCommand();
            var operation = NewOperation(52);
            var winnerRepository = new MemoryCareerRepository();
            var winnerService = Service(winnerRepository);
            var winnerCreated = winnerService.CreateCareer(create);
            var winner = winnerService.ConfirmTryoutStage(ConfirmCommand(
                create,
                winnerCreated.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.serve",
                operation)).Snapshot;

            var raceRepository = new MemoryCareerRepository();
            var random = new CountingRandom();
            var raceService = Service(raceRepository, random: random);
            var raceCreated = raceService.CreateCareer(create);
            raceRepository.CommitRaceWinner = winner;
            var result = raceService.ConfirmTryoutStage(ConfirmCommand(
                create,
                raceCreated.Snapshot.Identity.VersionToken,
                1,
                "tryout.attack.choice.power",
                operation));

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.OperationConflict));
            Assert.That(result.Snapshot, Is.SameAs(winner));
            Assert.That(result.ConflictingReceipt, Is.Not.Null);
            Assert.That(result.ConflictingReceipt.Target.ChoiceId,
                Is.EqualTo("tryout.attack.choice.serve"));
            Assert.That(random.Count, Is.EqualTo(3));
            Assert.That(raceRepository.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void FullOnboarding_RoundTripsThroughTheRealLocalRepositoryWithoutSchemaChange()
        {
            var root = Path.Combine(Path.GetTempPath(), "career-stage3-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new LocalCareerSaveRepository(
                    new CareerStoragePaths(root),
                    new SystemAtomicFileSystem());
                var service = Service(repository);
                var create = CreateCommand();
                var snapshot = service.CreateCareer(create).Snapshot;
                snapshot = service.ConfirmTryoutStage(ConfirmCommand(
                    create, snapshot.Identity.VersionToken, 1,
                    "tryout.attack.choice.power", NewOperation(21))).Snapshot;
                snapshot = service.ConfirmTryoutStage(ConfirmCommand(
                    create, snapshot.Identity.VersionToken, 2,
                    "tryout.reception_defense.choice.first_touch", NewOperation(22))).Snapshot;
                snapshot = service.ConfirmTryoutStage(ConfirmCommand(
                    create, snapshot.Identity.VersionToken, 3,
                    "tryout.scrimmage.choice.endurance", NewOperation(23), Enrollment())).Snapshot;

                var loaded = repository.Load(create.ProfileId, create.SaveId);

                Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
                Assert.That(loaded.Snapshot.Versions.SchemaVersion, Is.EqualTo(1));
                Assert.That(loaded.Snapshot.Identity.VersionToken,
                    Is.EqualTo(snapshot.Identity.VersionToken));
                Assert.That(loaded.Snapshot.OperationReceipts, Has.Count.EqualTo(4));
                Assert.That(loaded.Snapshot.Player.Attributes, Is.EqualTo(snapshot.Player.Attributes));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static CareerOnboardingService Service(
            ICareerSaveRepository repository,
            ICareerSeedSource seeds = null,
            IDeterministicCareerRandom random = null,
            TryoutCatalog catalog = null)
        {
            return new CareerOnboardingService(
                repository,
                seeds ?? new FixedSeedSource(),
                random ?? new CareerDeterministicRandom(),
                catalog ?? TryoutCatalogV1.Create());
        }

        private static OnboardingRun RunOnboarding(
            CareerOnboardingService service,
            CreateCareerCommand create)
        {
            var snapshot = service.CreateCareer(create).Snapshot;
            var results = new CareerApplicationResult[3];
            results[0] = service.ConfirmTryoutStage(ConfirmCommand(
                create, snapshot.Identity.VersionToken, 1,
                "tryout.attack.choice.power", NewOperation(21)));
            snapshot = results[0].Snapshot;
            results[1] = service.ConfirmTryoutStage(ConfirmCommand(
                create, snapshot.Identity.VersionToken, 2,
                "tryout.reception_defense.choice.first_touch", NewOperation(22)));
            snapshot = results[1].Snapshot;
            results[2] = service.ConfirmTryoutStage(ConfirmCommand(
                create, snapshot.Identity.VersionToken, 3,
                "tryout.scrimmage.choice.endurance", NewOperation(23), Enrollment()));
            return new OnboardingRun(results[2].Snapshot, results);
        }

        private static byte[] EncodeDeterministicOutcome(OnboardingRun run)
        {
            var builder = new StringBuilder();
            for (var stage = 0; stage < run.Snapshot.Onboarding.Stages.Count; stage++)
            {
                foreach (var output in run.Snapshot.Onboarding.Stages[stage].ResolvedOutputs)
                {
                    builder.Append(output.OutputId).Append('=').Append(
                        output.Perturbation.ToString(CultureInfo.InvariantCulture)).Append(';');
                }
            }

            foreach (var attribute in AllAttributes(run.Snapshot.Player))
            {
                builder.Append(attribute.AbilityBasisPoints.ToString(CultureInfo.InvariantCulture))
                    .Append(';');
            }

            builder.Append((int)run.Snapshot.PotentialGrade.Value).Append(';')
                .Append(run.Snapshot.Fatigue.Value.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(run.Snapshot.Mindset.Value.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(run.Snapshot.CoachTrust.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            foreach (var result in run.StageResults)
            {
                foreach (var explanation in result.Explanations)
                {
                    builder.Append(explanation.ReasonId).Append('|')
                        .Append(explanation.OutputId).Append('|')
                        .Append(explanation.BaseValue.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(explanation.AppliedDelta.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(explanation.FinalValue.ToString(CultureInfo.InvariantCulture)).Append(';');
                }
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static void AssertStage(
            CareerApplicationResult result,
            string[] expectedIds,
            int[] expectedPerturbations)
        {
            Assert.That(result.ResolvedOutputs.Select(x => x.OutputId), Is.EqualTo(expectedIds));
            Assert.That(result.ResolvedOutputs.Select(x => x.Perturbation),
                Is.EqualTo(expectedPerturbations));
        }

        private static void AssertExplanation(
            TryoutOutputExplanation explanation,
            string reason,
            string output,
            int baseValue,
            int delta,
            int finalValue)
        {
            Assert.That(explanation.ReasonId, Is.EqualTo(reason));
            Assert.That(explanation.OutputId, Is.EqualTo(output));
            Assert.That(explanation.BaseValue, Is.EqualTo(baseValue));
            Assert.That(explanation.AppliedDelta, Is.EqualTo(delta));
            Assert.That(explanation.FinalValue, Is.EqualTo(finalValue));
        }

        private static CreateCareerCommand CreateCommand(
            string playerName = "Player One",
            OccurrenceId[] occurrences = null)
        {
            return new CreateCareerCommand(
                new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                "player.one",
                "First Career",
                playerName,
                7,
                occurrences ?? new[] { Occurrence(1), Occurrence(2), Occurrence(3) },
                NewOperation(20),
                1000);
        }

        private static ConfirmTryoutStageCommand ConfirmCommand(
            CreateCareerCommand create,
            CareerVersionToken expected,
            int stage,
            string choice,
            OperationId operation,
            TryoutEnrollmentIds enrollment = null)
        {
            return new ConfirmTryoutStageCommand(
                create.ProfileId,
                create.SaveId,
                expected,
                operation,
                1000 + stage,
                stage,
                choice,
                enrollment);
        }

        private static TryoutEnrollmentIds Enrollment()
        {
            return new TryoutEnrollmentIds(
                new WeekPlanId(Guid.Parse("00000000-0000-0000-0000-000000000010")),
                new SlotActionId(Guid.Parse("00000000-0000-0000-0000-000000000011")),
                Occurrence(12));
        }

        private static CareerVersionToken Token(LineageId lineage, long revision)
        {
            return new CareerVersionToken(lineage, revision, new Sha256Digest(ZeroHash));
        }

        private static OccurrenceId Occurrence(int suffix)
        {
            return new OccurrenceId(Guid.Parse("00000000-0000-0000-0000-" + suffix.ToString("000000000000")));
        }

        private static OperationId NewOperation(int suffix)
        {
            return new OperationId(Guid.Parse("10000000-0000-0000-0000-" + suffix.ToString("000000000000")));
        }

        private static TryoutEnrollmentIds EnrollmentWithOccurrence(int suffix)
        {
            return new TryoutEnrollmentIds(
                new WeekPlanId(Guid.Parse("00000000-0000-0000-0000-000000000010")),
                new SlotActionId(Guid.Parse("00000000-0000-0000-0000-000000000011")),
                Occurrence(suffix));
        }

        private static IEnumerable<CareerAttributeProgress> AllAttributes(CareerPlayerRecord player)
        {
            return new[]
            {
                player.Attributes.Spike, player.Attributes.Serve,
                player.Attributes.Reception, player.Attributes.Defense,
                player.Attributes.Block, player.Attributes.Movement,
                player.Attributes.Jump, player.Attributes.Stamina
            };
        }

        private static PotentialGrade ExpectedPotential(CareerPlayerAttributes attributes)
        {
            var average = AllAttributes(new CareerPlayerRecord(
                new PlayerId("fixture"), "Fixture", 1, attributes))
                .Sum(x => x.AbilityBasisPoints) / 8;
            if (average < 4500) return PotentialGrade.D;
            if (average < 5000) return PotentialGrade.C;
            if (average < 5500) return PotentialGrade.B;
            if (average < 6000) return PotentialGrade.A;
            return PotentialGrade.S;
        }

        private sealed class FixedSeedSource : ICareerSeedSource
        {
            public int Count { get; private set; }

            public CareerSeed GenerateSeed()
            {
                Count++;
                return CareerSeed.Parse(SeedHex);
            }
        }

        private sealed class CountingRandom : IDeterministicCareerRandom
        {
            private readonly CareerDeterministicRandom _inner = new CareerDeterministicRandom();
            public int Count { get; private set; }

            public long NextInt64(CareerRandomRequest request, long minInclusive, long maxExclusive)
            {
                Count++;
                return _inner.NextInt64(request, minInclusive, maxExclusive);
            }
        }

        private sealed class ControlledPerturbationRandom : IDeterministicCareerRandom
        {
            public long NextInt64(
                CareerRandomRequest request,
                long minInclusive,
                long maxExclusive)
            {
                int[] perturbations;
                switch (request.EntityStableId)
                {
                    case "tryout.attack.choice.power":
                        perturbations = new[] { -100, -1, 0 };
                        break;
                    case "tryout.reception_defense.choice.first_touch":
                        perturbations = new[] { 100, -100, 1, -1 };
                        break;
                    case "tryout.scrimmage.choice.endurance":
                        perturbations = new[] { 100, -99, -19, 100 };
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected controlled random entity.");
                }

                return perturbations[checked((int)request.DrawIndex)] + 100L;
            }
        }

        private sealed class IrrelevantRequestRandom : IDeterministicCareerRandom
        {
            private readonly bool _reverse;
            private readonly CareerDeterministicRandom _inner = new CareerDeterministicRandom();

            public IrrelevantRequestRandom(bool reverse)
            {
                _reverse = reverse;
            }

            public long NextInt64(
                CareerRandomRequest request,
                long minInclusive,
                long maxExclusive)
            {
                var eventRequest = new CareerRandomRequest(
                    1,
                    request.Seed,
                    "event",
                    1,
                    1,
                    "event.irrelevant.option",
                    Occurrence(90),
                    0);
                var matchRequest = new CareerRandomRequest(
                    1,
                    request.Seed,
                    "match_seed",
                    1,
                    1,
                    "schedule.irrelevant.match",
                    Occurrence(91),
                    0);
                if (_reverse)
                {
                    _inner.NextInt64(matchRequest, 0, 4294967296L);
                    _inner.NextInt64(eventRequest, 0, 10000);
                    _inner.NextInt64(
                        new CareerRandomRequest(
                            1,
                            request.Seed,
                            "event",
                            1,
                            1,
                            "event.second_irrelevant.option",
                            Occurrence(92),
                            1),
                        0,
                        10000);
                }
                else
                {
                    _inner.NextInt64(eventRequest, 0, 10000);
                }

                return _inner.NextInt64(request, minInclusive, maxExclusive);
            }
        }

        private sealed class OnboardingRun
        {
            public OnboardingRun(
                CareerSaveSnapshot snapshot,
                CareerApplicationResult[] stageResults)
            {
                Snapshot = snapshot;
                StageResults = stageResults;
            }

            public CareerSaveSnapshot Snapshot { get; }

            public CareerApplicationResult[] StageResults { get; }
        }

        private sealed class MemoryCareerRepository : ICareerSaveRepository
        {
            public CareerSaveSnapshot Snapshot { get; private set; }
            public int LoadCount { get; private set; }
            public int CreateCount { get; private set; }
            public int CommitCount { get; private set; }
            public PersistenceResultKind? CreateFailure { get; set; }
            public PersistenceResultKind? CommitFailure { get; set; }
            public CareerSaveSnapshot CommitRaceWinner { get; set; }

            public CareerPersistenceResult Create(CareerSaveSnapshot initialSnapshot, OperationId operationId)
            {
                CreateCount++;
                if (CreateFailure.HasValue)
                {
                    return new CareerPersistenceResult(CreateFailure.Value);
                }

                if (Snapshot != null)
                {
                    return new CareerPersistenceResult(PersistenceResultKind.AlreadyExists);
                }

                Snapshot = initialSnapshot;
                return new CareerPersistenceResult(PersistenceResultKind.Created, Snapshot);
            }

            public CareerPersistenceResult Load(ProfileId profileId, SaveId saveId)
            {
                LoadCount++;
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
                return new CareerPersistenceResult(PersistenceResultKind.Committed, Snapshot);
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
