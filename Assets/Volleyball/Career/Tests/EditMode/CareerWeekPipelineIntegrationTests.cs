using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekPipelineIntegrationTests
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";
        private const string SeedHex =
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

        private static readonly ProfileId Profile =
            new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        private static readonly SaveId Save =
            new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private static readonly LineageId Lineage =
            new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333"));

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "career-week-pipeline-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_root) &&
                _root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void RealRepository_FirstWeekPipelineReloadsRevisionFourThroughEight()
        {
            var paths = new CareerStoragePaths(_root);
            var repository = new LocalCareerSaveRepository(
                paths,
                new SystemAtomicFileSystem());
            var revision4 = CompleteOnboarding(repository, out var onboardingCommands);
            Assert.That(revision4.Identity.Revision, Is.EqualTo(4));
            Assert.That(revision4.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planning));
            Assert.That(revision4.PotentialGrade, Is.EqualTo(PotentialGrade.B));
            Assert.That(AllGrowth(revision4.Player.Attributes), Is.All.Zero);
            AssertSealedOnDisk(paths, revision4);

            var match = revision4.Progression.WeekPlan.Slots[2];
            var plan = new CareerWeekPlanState(
                revision4.Progression.WeekPlan.PlanId,
                1,
                1,
                new[]
                {
                    new CareerWeekActionState(
                        Slot(101),
                        Occurrence(102),
                        CareerWeekActionKind.SpecializedTraining,
                        "week_action.specialized.spike"),
                    new CareerWeekActionState(
                        Slot(103),
                        Occurrence(104),
                        CareerWeekActionKind.SpecializedTraining,
                        "week_action.specialized.spike"),
                    match
                },
                true);
            var random = new RecordingRandom(new CareerDeterministicRandom());
            var service = new CareerWeekCommandService(repository, random);
            var confirm = new ConfirmWeekPlanCommand(
                Profile,
                Save,
                revision4.Identity.VersionToken,
                Operation(30),
                2000,
                plan);
            Assert.That(service.ConfirmWeekPlan(confirm).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var revision5 = Reload(repository);
            Assert.That(revision5.Identity.Revision, Is.EqualTo(5));
            Assert.That(revision5.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
            Assert.That(revision5.Progression.NextSlotNumber, Is.EqualTo(1));
            AssertPlayerAndStatusEqual(revision4, revision5);
            AssertSealedOnDisk(paths, revision5);
            AssertExistingRetryPreservesDiskBytes(
                paths,
                revision5,
                () => service.ConfirmWeekPlan(confirm),
                random,
                0);

            var slot1 = new ExecuteWeekActionCommand(
                Profile,
                Save,
                revision5.Identity.VersionToken,
                Operation(31),
                2001,
                plan.PlanId,
                1,
                plan.Slots[0].SlotActionId,
                plan.Slots[0].OccurrenceId,
                plan.Slots[0].ContentId,
                EventOccurrence());
            Assert.That(service.ExecuteWeekAction(slot1).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var revision6 = Reload(repository);
            Assert.That(revision6.Identity.Revision, Is.EqualTo(6));
            Assert.That(revision6.Progression.Kind,
                Is.EqualTo(CareerProgressionKind.AwaitingEventChoice));
            Assert.That(revision6.Progression.PendingEvent, Is.Not.Null);
            AssertAbilityBasisPointsEqual(revision4, revision6);
            Assert.That(revision6.Player.Attributes.Spike.GrowthExperience, Is.EqualTo(120));
            Assert.That(AllGrowth(revision6.Player.Attributes).Skip(1), Is.All.Zero);
            Assert.That(revision6.Fatigue.Value, Is.EqualTo(revision5.Fatigue.Value + 8));
            Assert.That(revision6.Mindset, Is.EqualTo(revision5.Mindset));
            Assert.That(revision6.CoachTrust, Is.EqualTo(revision5.CoachTrust));
            Assert.That(revision6.TrainingEmphases.Contributions, Has.Count.EqualTo(1));
            Assert.That(revision6.TrainingEmphases.Contributions[0].SourceSlotActionId,
                Is.EqualTo(plan.Slots[0].SlotActionId));
            Assert.That(revision6.TrainingEmphases.Contributions[0].Direction,
                Is.EqualTo(CareerTrainingDirection.Spike));
            Assert.That(revision6.TrainingEmphases.Contributions[0].BonusBasisPoints,
                Is.EqualTo(1000));
            var pending = revision6.Progression.PendingEvent;
            Assert.That(pending.Options.Select(option => option.OptionId), Is.EqualTo(new[]
            {
                "event.team_meal.option.attend",
                "event.team_meal.option.extra_practice"
            }));
            AssertEffect(pending.Options[0], 0, 4, 6, 3);
            AssertEffect(pending.Options[1], 80, 10, -2, 6);
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
            AssertSealedOnDisk(paths, revision6);
            AssertExistingRetryPreservesDiskBytes(
                paths,
                revision6,
                () => service.ExecuteWeekAction(slot1),
                random,
                2);

            var resolve = new ResolveEventChoiceCommand(
                Profile,
                Save,
                revision6.Identity.VersionToken,
                Operation(32),
                2002,
                plan.PlanId,
                plan.Slots[0].SlotActionId,
                plan.Slots[0].OccurrenceId,
                "event.team_meal",
                EventOccurrence(),
                "event.team_meal.option.extra_practice");
            Assert.That(service.ResolveEventChoice(resolve).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var revision7 = Reload(repository);
            Assert.That(revision7.Identity.Revision, Is.EqualTo(7));
            Assert.That(revision7.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
            Assert.That(revision7.Progression.NextSlotNumber, Is.EqualTo(2));
            Assert.That(revision7.Progression.PendingEvent, Is.Null);
            AssertAbilityBasisPointsEqual(revision4, revision7);
            Assert.That(revision7.Player.Attributes.Spike.GrowthExperience, Is.EqualTo(200));
            Assert.That(AllGrowth(revision7.Player.Attributes).Skip(1), Is.All.Zero);
            Assert.That(revision7.Fatigue.Value, Is.EqualTo(revision6.Fatigue.Value + 10));
            Assert.That(revision7.Mindset.Value, Is.EqualTo(revision6.Mindset.Value - 2));
            Assert.That(revision7.CoachTrust.Value, Is.EqualTo(revision6.CoachTrust.Value + 6));
            Assert.That(revision7.TrainingEmphases.Contributions, Has.Count.EqualTo(1));
            Assert.That(revision7.TrainingEmphases.Contributions[0].SourceSlotActionId,
                Is.EqualTo(plan.Slots[0].SlotActionId));
            Assert.That(revision7.TrainingEmphases.Contributions[0].BonusBasisPoints,
                Is.EqualTo(1000));
            AssertSealedOnDisk(paths, revision7);
            AssertExistingRetryPreservesDiskBytes(
                paths,
                revision7,
                () => service.ResolveEventChoice(resolve),
                random,
                2);

            var slot2 = new ExecuteWeekActionCommand(
                Profile,
                Save,
                revision7.Identity.VersionToken,
                Operation(33),
                2003,
                plan.PlanId,
                2,
                plan.Slots[1].SlotActionId,
                plan.Slots[1].OccurrenceId,
                plan.Slots[1].ContentId,
                null);
            Assert.That(service.ExecuteWeekAction(slot2).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var revision8 = Reload(repository);
            Assert.That(revision8.Identity.Revision, Is.EqualTo(8));
            Assert.That(revision8.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
            Assert.That(revision8.Progression.NextSlotNumber, Is.EqualTo(3));
            Assert.That(revision8.Progression.PendingEvent, Is.Null);
            AssertAbilityBasisPointsEqual(revision4, revision8);
            Assert.That(revision8.Player.Attributes.Spike.GrowthExperience, Is.EqualTo(320));
            Assert.That(AllGrowth(revision8.Player.Attributes).Skip(1), Is.All.Zero);
            Assert.That(revision8.Fatigue.Value, Is.EqualTo(revision7.Fatigue.Value + 8));
            Assert.That(revision8.Mindset, Is.EqualTo(revision7.Mindset));
            Assert.That(revision8.CoachTrust, Is.EqualTo(revision7.CoachTrust));
            Assert.That(revision8.TrainingEmphases.Contributions, Has.Count.EqualTo(2));
            Assert.That(revision8.TrainingEmphases.Contributions[0].SourceSlotActionId,
                Is.EqualTo(plan.Slots[0].SlotActionId));
            Assert.That(revision8.TrainingEmphases.Contributions[0].BonusBasisPoints,
                Is.EqualTo(1000));
            Assert.That(revision8.TrainingEmphases.Contributions[1].SourceSlotActionId,
                Is.EqualTo(plan.Slots[1].SlotActionId));
            Assert.That(revision8.TrainingEmphases.Contributions[1].BonusBasisPoints,
                Is.EqualTo(500));
            Assert.That(revision8.TrainingEmphases.Freeze(), Has.Count.EqualTo(1));
            Assert.That(revision8.TrainingEmphases.Freeze()[0].TotalBonusBasisPoints,
                Is.EqualTo(1500));
            AssertSealedOnDisk(paths, revision8);
            AssertExistingRetryPreservesDiskBytes(
                paths,
                revision8,
                () => service.ExecuteWeekAction(slot2),
                random,
                2);

            AssertExactReceipts(
                revision8,
                onboardingCommands,
                confirm,
                slot1,
                resolve,
                slot2);
            AssertPendingMatchRuntimeSurfaceIsDomainOnly();
        }

        private static void AssertExactReceipts(
            CareerSaveSnapshot final,
            OnboardingCommands onboarding,
            ConfirmWeekPlanCommand confirm,
            ExecuteWeekActionCommand slot1,
            ResolveEventChoiceCommand resolve,
            ExecuteWeekActionCommand slot2)
        {
            Assert.That(final.OperationReceipts, Has.Count.EqualTo(8));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.AppliedRevision),
                Is.EqualTo(new long[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.AppliedLineageId),
                Is.All.EqualTo(Lineage));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.CompletedAtUtcMs),
                Is.EqualTo(new long[] { 1000, 1001, 1002, 1003, 2000, 2001, 2002, 2003 }));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.OperationKind),
                Is.EqualTo(new[]
                {
                    OperationKind.CreateCareer,
                    OperationKind.ConfirmTryoutStage,
                    OperationKind.ConfirmTryoutStage,
                    OperationKind.ConfirmTryoutStage,
                    OperationKind.ConfirmWeekPlan,
                    OperationKind.ExecuteWeekAction,
                    OperationKind.ResolveEventChoice,
                    OperationKind.ExecuteWeekAction
                }));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.OutcomeKind),
                Is.EqualTo(new[]
                {
                    OperationOutcomeKind.CareerCreated,
                    OperationOutcomeKind.TryoutAdvanced,
                    OperationOutcomeKind.TryoutAdvanced,
                    OperationOutcomeKind.TryoutAdvanced,
                    OperationOutcomeKind.WeekPlanConfirmed,
                    OperationOutcomeKind.SlotCompleted,
                    OperationOutcomeKind.EventChoiceApplied,
                    OperationOutcomeKind.SlotCompleted
                }));
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.OperationId).Distinct().Count(),
                Is.EqualTo(8));

            var expectedFingerprints = new[]
            {
                CareerOperationFingerprintV2.Hash(onboarding.Create),
                CareerOperationFingerprintV2.Hash(
                    onboarding.First,
                    onboarding.Create.TryoutOccurrenceIds[0]),
                CareerOperationFingerprintV2.Hash(
                    onboarding.Second,
                    onboarding.Create.TryoutOccurrenceIds[1]),
                CareerOperationFingerprintV2.Hash(
                    onboarding.Third,
                    onboarding.Create.TryoutOccurrenceIds[2]),
                CareerOperationFingerprintV2.Hash(confirm),
                CareerOperationFingerprintV2.Hash(slot1),
                CareerOperationFingerprintV2.Hash(resolve),
                CareerOperationFingerprintV2.Hash(slot2)
            };
            Assert.That(
                final.OperationReceipts.Select(receipt => receipt.InputFingerprint),
                Is.EqualTo(expectedFingerprints));

            var create = final.OperationReceipts[0];
            Assert.That(create.Target.OperationKind, Is.EqualTo(OperationKind.CreateCareer));
            for (var index = 0; index < 3; index++)
            {
                var command = index == 0
                    ? onboarding.First
                    : index == 1
                        ? onboarding.Second
                        : onboarding.Third;
                var receipt = final.OperationReceipts[index + 1];
                Assert.That(receipt.Target.TryoutStage, Is.EqualTo(index + 1));
                Assert.That(receipt.Target.TryoutOccurrenceId,
                    Is.EqualTo(onboarding.Create.TryoutOccurrenceIds[index]));
                Assert.That(receipt.Target.ChoiceId, Is.EqualTo(command.ChoiceId));
            }

            var confirmation = final.OperationReceipts[4];
            Assert.That(confirmation.Target.WeekPlanId, Is.EqualTo(confirm.CandidatePlan.PlanId));
            Assert.That(confirmation.Target.SlotActionId, Is.Null);
            var firstAction = final.OperationReceipts[5];
            AssertActionTarget(firstAction, slot1);
            AssertAppliedDeltas(firstAction, 120, 8, 0, 0);
            var eventChoice = final.OperationReceipts[6];
            Assert.That(eventChoice.Target.WeekPlanId, Is.EqualTo(resolve.WeekPlanId));
            Assert.That(eventChoice.Target.SlotActionId, Is.EqualTo(resolve.SourceSlotActionId));
            Assert.That(eventChoice.Target.ActionOccurrenceId,
                Is.EqualTo(resolve.SourceActionOccurrenceId));
            Assert.That(eventChoice.Target.EventOccurrenceId,
                Is.EqualTo(resolve.EventOccurrenceId));
            Assert.That(eventChoice.Target.OptionId, Is.EqualTo(resolve.OptionId));
            AssertAppliedDeltas(eventChoice, 80, 10, -2, 6);
            var secondAction = final.OperationReceipts[7];
            AssertActionTarget(secondAction, slot2);
            AssertAppliedDeltas(secondAction, 120, 8, 0, 0);
        }

        private static void AssertActionTarget(
            OperationReceipt receipt,
            ExecuteWeekActionCommand command)
        {
            Assert.That(receipt.Target.WeekPlanId, Is.EqualTo(command.WeekPlanId));
            Assert.That(receipt.Target.SlotActionId, Is.EqualTo(command.SlotActionId));
            Assert.That(receipt.Target.ActionOccurrenceId, Is.EqualTo(command.ActionOccurrenceId));
            Assert.That(receipt.Target.EventOccurrenceId, Is.Null);
            Assert.That(receipt.Target.OptionId, Is.Null);
        }

        private static void AssertAppliedDeltas(
            OperationReceipt receipt,
            long spikeGrowth,
            int fatigue,
            int mindset,
            int trust)
        {
            Assert.That(receipt.OutcomeSummary.GrowthExperienceDelta.Spike,
                Is.EqualTo(spikeGrowth));
            Assert.That(receipt.OutcomeSummary.GrowthExperienceDelta.Total,
                Is.EqualTo(spikeGrowth));
            Assert.That(receipt.OutcomeSummary.FatigueDelta, Is.EqualTo(fatigue));
            Assert.That(receipt.OutcomeSummary.MindsetDelta, Is.EqualTo(mindset));
            Assert.That(receipt.OutcomeSummary.CoachTrustDelta, Is.EqualTo(trust));
        }

        private static void AssertPendingMatchRuntimeSurfaceIsDomainOnly()
        {
            Assert.That(
                typeof(CareerSaveSnapshot).GetProperty("PendingMatch"),
                Is.Not.Null);
            Assert.That(
                typeof(PendingCareerMatch).Namespace,
                Is.EqualTo("Volleyball.Career.Domain"));
        }

        private static void AssertSealedOnDisk(
            CareerStoragePaths paths,
            CareerSaveSnapshot expected)
        {
            var bytes = File.ReadAllBytes(paths.CareerPath(Profile, Save));
            var onDisk = CareerSaveJsonCodec.Deserialize(bytes);
            Assert.That(expected.Identity.SnapshotHash.Value, Is.Not.EqualTo(ZeroHash));
            Assert.That(
                expected.Identity.SnapshotHash,
                Is.EqualTo(CareerSaveJsonCodec.ComputeSnapshotHash(expected)));
            Assert.That(onDisk.Identity.VersionToken, Is.EqualTo(expected.Identity.VersionToken));
            Assert.That(onDisk.Identity.SnapshotHash, Is.EqualTo(expected.Identity.SnapshotHash));
            CollectionAssert.AreEqual(CareerSaveJsonCodec.Serialize(expected), bytes);
        }

        private static void AssertPlayerAndStatusEqual(
            CareerSaveSnapshot expected,
            CareerSaveSnapshot actual)
        {
            AssertAbilityBasisPointsEqual(expected, actual);
            CollectionAssert.AreEqual(
                AllGrowth(expected.Player.Attributes),
                AllGrowth(actual.Player.Attributes));
            Assert.That(actual.Fatigue, Is.EqualTo(expected.Fatigue));
            Assert.That(actual.Mindset, Is.EqualTo(expected.Mindset));
            Assert.That(actual.CoachTrust, Is.EqualTo(expected.CoachTrust));
        }

        private static void AssertAbilityBasisPointsEqual(
            CareerSaveSnapshot expected,
            CareerSaveSnapshot actual)
        {
            Assert.That(
                AllAttributes(actual.Player.Attributes).Select(value => value.AbilityBasisPoints),
                Is.EqualTo(AllAttributes(expected.Player.Attributes)
                    .Select(value => value.AbilityBasisPoints)));
        }

        private static IEnumerable<CareerAttributeProgress> AllAttributes(
            CareerPlayerAttributes attributes)
        {
            return new[]
            {
                attributes.Spike,
                attributes.Serve,
                attributes.Reception,
                attributes.Defense,
                attributes.Block,
                attributes.Movement,
                attributes.Jump,
                attributes.Stamina
            };
        }

        private static IEnumerable<long> AllGrowth(CareerPlayerAttributes attributes)
        {
            return AllAttributes(attributes).Select(value => value.GrowthExperience);
        }

        private static void AssertEffect(
            CareerEventOptionEffect effect,
            long spikeGrowth,
            int fatigue,
            int mindset,
            int trust)
        {
            Assert.That(effect.GrowthExperienceDelta.Spike, Is.EqualTo(spikeGrowth));
            Assert.That(effect.GrowthExperienceDelta.Total, Is.EqualTo(spikeGrowth));
            Assert.That(effect.FatigueDelta, Is.EqualTo(fatigue));
            Assert.That(effect.MindsetDelta, Is.EqualTo(mindset));
            Assert.That(effect.CoachTrustDelta, Is.EqualTo(trust));
        }

        private static void AssertRandomCall(
            RandomCall call,
            string expectedOptionId,
            string expectedDigest,
            long expectedRoll)
        {
            Assert.That(call.Request.AlgorithmVersion, Is.EqualTo(1));
            Assert.That(call.Request.Seed.ToHex(), Is.EqualTo(SeedHex));
            Assert.That(call.Request.StreamId, Is.EqualTo("event"));
            Assert.That(call.Request.Season, Is.EqualTo(1));
            Assert.That(call.Request.Week, Is.EqualTo(1));
            Assert.That(call.Request.EntityStableId, Is.EqualTo(expectedOptionId));
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

        private static void AssertExistingRetryPreservesDiskBytes(
            CareerStoragePaths paths,
            CareerSaveSnapshot expected,
            Func<CareerWeekCommandResult> retry,
            RecordingRandom random,
            int expectedRandomCalls)
        {
            var before = File.ReadAllBytes(paths.CareerPath(Profile, Save));

            var result = retry();

            Assert.That(result.Status, Is.EqualTo(CareerApplicationStatus.Existing));
            Assert.That(result.Snapshot.Identity.VersionToken,
                Is.EqualTo(expected.Identity.VersionToken));
            CollectionAssert.AreEqual(
                before,
                File.ReadAllBytes(paths.CareerPath(Profile, Save)));
            Assert.That(random.CallCount, Is.EqualTo(expectedRandomCalls));
        }

        private static CareerSaveSnapshot CompleteOnboarding(
            ICareerSaveRepository repository,
            out OnboardingCommands commands)
        {
            var service = new CareerOnboardingService(
                repository,
                new FixedSeedSource(),
                new CareerDeterministicRandom(),
                TryoutCatalogV1.Create());
            var create = new CreateCareerCommand(
                Profile,
                Save,
                Lineage,
                "player.pipeline",
                "Pipeline Career",
                "Pipeline Player",
                7,
                new[] { Occurrence(1), Occurrence(2), Occurrence(3) },
                Operation(20),
                1000);
            Assert.That(service.CreateCareer(create).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            var current = Reload(repository);

            var first = new ConfirmTryoutStageCommand(
                Profile,
                Save,
                current.Identity.VersionToken,
                Operation(21),
                1001,
                1,
                "tryout.attack.choice.power");
            Assert.That(service.ConfirmTryoutStage(first).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            current = Reload(repository);

            var second = new ConfirmTryoutStageCommand(
                Profile,
                Save,
                current.Identity.VersionToken,
                Operation(22),
                1002,
                2,
                "tryout.reception_defense.choice.first_touch");
            Assert.That(service.ConfirmTryoutStage(second).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            current = Reload(repository);

            var third = new ConfirmTryoutStageCommand(
                Profile,
                Save,
                current.Identity.VersionToken,
                Operation(23),
                1003,
                3,
                "tryout.scrimmage.choice.endurance",
                new TryoutEnrollmentIds(
                    new WeekPlanId(Guid.Parse("00000000-0000-0000-0000-000000000010")),
                    Slot(11),
                    Occurrence(12)));
            Assert.That(service.ConfirmTryoutStage(third).Status,
                Is.EqualTo(CareerApplicationStatus.Applied));
            commands = new OnboardingCommands(create, first, second, third);
            return Reload(repository);
        }

        private static CareerSaveSnapshot Reload(ICareerSaveRepository repository)
        {
            var loaded = repository.Load(Profile, Save);
            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Snapshot, Is.Not.Null);
            return loaded.Snapshot;
        }

        private static SlotActionId Slot(int suffix)
        {
            return new SlotActionId(Guid.Parse(
                "40000000-0000-0000-0000-" + suffix.ToString("D12")));
        }

        private static OccurrenceId Occurrence(int suffix)
        {
            return new OccurrenceId(Guid.Parse(
                "50000000-0000-0000-0000-" + suffix.ToString("D12")));
        }

        private static OccurrenceId EventOccurrence()
        {
            return new OccurrenceId(
                Guid.Parse("00000000-0000-0000-0000-000000000003"));
        }

        private static OperationId Operation(int suffix)
        {
            return new OperationId(Guid.Parse(
                "60000000-0000-0000-0000-" + suffix.ToString("D12")));
        }

        private sealed class FixedSeedSource : ICareerSeedSource
        {
            public CareerSeed GenerateSeed()
            {
                return CareerSeed.Parse(SeedHex);
            }
        }

        private sealed class OnboardingCommands
        {
            public OnboardingCommands(
                CreateCareerCommand create,
                ConfirmTryoutStageCommand first,
                ConfirmTryoutStageCommand second,
                ConfirmTryoutStageCommand third)
            {
                Create = create;
                First = first;
                Second = second;
                Third = third;
            }

            public CreateCareerCommand Create { get; }
            public ConfirmTryoutStageCommand First { get; }
            public ConfirmTryoutStageCommand Second { get; }
            public ConfirmTryoutStageCommand Third { get; }
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

            public int CallCount => Calls.Count;

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
    }
}
