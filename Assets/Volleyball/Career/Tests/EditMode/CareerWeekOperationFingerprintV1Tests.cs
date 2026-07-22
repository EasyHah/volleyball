using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekOperationFingerprintV1Tests
    {
        private const string ZeroHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        [Test]
        public void ConfirmWeekPlanCommand_DefensivelyCopiesCandidatePlan()
        {
            var candidate = CandidatePlan();

            var command = Command(candidate: candidate);

            Assert.That(command.CandidatePlan, Is.Not.SameAs(candidate));
            Assert.That(command.CandidatePlan.PlanId, Is.EqualTo(candidate.PlanId));
            Assert.That(command.CandidatePlan.Season, Is.EqualTo(candidate.Season));
            Assert.That(command.CandidatePlan.Week, Is.EqualTo(candidate.Week));
            Assert.That(command.CandidatePlan.IsConfirmed, Is.EqualTo(candidate.IsConfirmed));
            for (var index = 0; index < candidate.Slots.Count; index++)
            {
                Assert.That(command.CandidatePlan.Slots[index], Is.Not.SameAs(candidate.Slots[index]));
                Assert.That(
                    command.CandidatePlan.Slots[index].SlotActionId,
                    Is.EqualTo(candidate.Slots[index].SlotActionId));
                Assert.That(
                    command.CandidatePlan.Slots[index].OccurrenceId,
                    Is.EqualTo(candidate.Slots[index].OccurrenceId));
                Assert.That(
                    command.CandidatePlan.Slots[index].Kind,
                    Is.EqualTo(candidate.Slots[index].Kind));
                Assert.That(
                    command.CandidatePlan.Slots[index].ContentId,
                    Is.EqualTo(candidate.Slots[index].ContentId));
            }
        }

        [Test]
        public void ConfirmWeekPlanFingerprintV1_HasLockedCanonicalBytesAndHash()
        {
            const string expectedJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"confirm_week_plan\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"expectedLineageId\":\"33333333-3333-3333-3333-333333333333\",\"expectedRevision\":4,\"expectedSnapshotHash\":\"" + ZeroHash + "\",\"planId\":\"44444444-4444-4444-4444-444444444444\",\"season\":1,\"week\":1,\"slots\":[{\"slotActionId\":\"55555555-5555-5555-5555-555555555555\",\"occurrenceId\":\"66666666-6666-6666-6666-666666666666\",\"kind\":\"specialized_training\",\"contentId\":\"week_action.specialized.spike\"},{\"slotActionId\":\"77777777-7777-7777-7777-777777777777\",\"occurrenceId\":\"88888888-8888-8888-8888-888888888888\",\"kind\":\"rest\",\"contentId\":\"week_action.rest.standard\"},{\"slotActionId\":\"99999999-9999-9999-9999-999999999999\",\"occurrenceId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"kind\":\"match\",\"contentId\":\"schedule.u1w1.match.01\"}],\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";

            var bytes = CareerOperationFingerprintV1.Encode(Command());

            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expectedJson), bytes);
            Assert.That(
                CareerOperationFingerprintV1.Hash(Command()).Value,
                Is.EqualTo("08596f8a683fea343de747058bd8d8cdbfd3d8ec34308212a70f2aaf590516fe"));
        }

        [Test]
        public void ConfirmWeekPlanFingerprintV1_UsesAllFiveFixedActionKindIds()
        {
            var specializedAndRest = Fingerprint(CandidatePlan());
            var strengthAndTeam = Fingerprint(CandidatePlan(
                firstKind: CareerWeekActionKind.StrengthTraining,
                firstContentId: "week_action.strength.jump",
                secondKind: CareerWeekActionKind.TeamPractice,
                secondContentId: "week_action.team_practice.standard"));

            Assert.That(specializedAndRest, Does.Contain("\"kind\":\"specialized_training\""));
            Assert.That(specializedAndRest, Does.Contain("\"kind\":\"rest\""));
            Assert.That(specializedAndRest, Does.Contain("\"kind\":\"match\""));
            Assert.That(strengthAndTeam, Does.Contain("\"kind\":\"strength_training\""));
            Assert.That(strengthAndTeam, Does.Contain("\"kind\":\"team_practice\""));
        }

        [Test]
        public void ConfirmWeekPlanFingerprintV1_ExcludesOperationIdAndCompletedAtUtcMs()
        {
            var baseline = Command();
            var changedOperation = Command(
                operationId: new OperationId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
            var changedCompletionTime = Command(completedAtUtcMs: 9007199254740991L);

            Assert.That(
                CareerOperationFingerprintV1.Hash(changedOperation),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
            Assert.That(
                CareerOperationFingerprintV1.Hash(changedCompletionTime),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
        }

        [Test]
        public void ConfirmWeekPlanFingerprintV1_IsSensitiveToEveryBusinessIdentityAndOrder()
        {
            var baseline = CareerOperationFingerprintV1.Hash(Command());
            var changedToken = Token(
                new LineageId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
                5,
                new Sha256Digest(new string('f', 64)));
            var changedPlan = CandidatePlan(
                planId: new WeekPlanId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")));
            var changedSlotAction = CandidatePlan(
                firstSlotActionId: new SlotActionId(Guid.Parse("12121212-1212-1212-1212-121212121212")));
            var changedOccurrence = CandidatePlan(
                secondOccurrenceId: new OccurrenceId(Guid.Parse("13131313-1313-1313-1313-131313131313")));
            var changedContent = CandidatePlan(firstContentId: "week_action.specialized.serve");
            var changedKind = CandidatePlan(
                firstKind: CareerWeekActionKind.StrengthTraining,
                firstContentId: "week_action.strength.jump");
            var changedOrder = CandidatePlan(
                firstSlotActionId: new SlotActionId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                firstOccurrenceId: new OccurrenceId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                firstKind: CareerWeekActionKind.Rest,
                firstContentId: "week_action.rest.standard",
                secondSlotActionId: new SlotActionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                secondOccurrenceId: new OccurrenceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                secondKind: CareerWeekActionKind.SpecializedTraining,
                secondContentId: "week_action.specialized.spike");

            Assert.That(CareerOperationFingerprintV1.Hash(Command(expectedToken: changedToken)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedPlan)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedSlotAction)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedOccurrence)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedContent)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedKind)), Is.Not.EqualTo(baseline));
            Assert.That(CareerOperationFingerprintV1.Hash(Command(candidate: changedOrder)), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void ConfirmWeekPlanFingerprintV1_IncludesCurrentFourVersionAxes()
        {
            var encoded = Fingerprint(CandidatePlan());

            Assert.That(
                encoded,
                Does.EndWith(
                    "\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}"));
        }

        [Test]
        public void ExecuteWeekActionCommand_HasExactImmutablePublicContract()
        {
            var type = typeof(ExecuteWeekActionCommand);
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "ProfileId", "SaveId", "ExpectedVersionToken", "OperationId",
                    "CompletedAtUtcMs", "WeekPlanId", "SlotNumber", "SlotActionId",
                    "ActionOccurrenceId", "ContentId", "TriggeredEventOccurrenceId"
                },
                properties.Select(property => property.Name));
            Assert.That(properties, Has.All.Matches<PropertyInfo>(property => !property.CanWrite));
            Assert.That(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExecuteWeekActionFingerprintV1_SlotOneHasLockedCanonicalBytesAndHash()
        {
            const string expectedJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"execute_week_action\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"expectedLineageId\":\"33333333-3333-3333-3333-333333333333\",\"expectedRevision\":5,\"expectedSnapshotHash\":\"" + ZeroHash + "\",\"weekPlanId\":\"44444444-4444-4444-4444-444444444444\",\"slotNumber\":1,\"slotActionId\":\"55555555-5555-5555-5555-555555555555\",\"actionOccurrenceId\":\"66666666-6666-6666-6666-666666666666\",\"contentId\":\"week_action.specialized.spike\",\"triggeredEventOccurrenceId\":\"00000000-0000-0000-0000-000000000003\",\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";
            var command = ExecuteCommand();

            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(expectedJson),
                CareerOperationFingerprintV1.Encode(command));
            Assert.That(
                CareerOperationFingerprintV1.Hash(command).Value,
                Is.EqualTo("ad62ae072ff9cbefecb9934d18a4456fa99c4e9467a228bbdae3891e3cb1cd88"));
        }

        [Test]
        public void ExecuteWeekActionFingerprintV1_SlotTwoHasLockedCanonicalNullBytesAndHash()
        {
            var expectedJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"execute_week_action\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"expectedLineageId\":\"33333333-3333-3333-3333-333333333333\",\"expectedRevision\":7,\"expectedSnapshotHash\":\"" + new string('f', 64) + "\",\"weekPlanId\":\"44444444-4444-4444-4444-444444444444\",\"slotNumber\":2,\"slotActionId\":\"77777777-7777-7777-7777-777777777777\",\"actionOccurrenceId\":\"88888888-8888-8888-8888-888888888888\",\"contentId\":\"week_action.rest.standard\",\"triggeredEventOccurrenceId\":null,\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";
            var command = ExecuteCommand(
                expectedToken: Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    7,
                    new Sha256Digest(new string('f', 64))),
                slotNumber: 2,
                slotActionId: new SlotActionId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                actionOccurrenceId: new OccurrenceId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                contentId: "week_action.rest.standard",
                eventOccurrenceId: null,
                useDefaultEventOccurrence: false);

            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(expectedJson),
                CareerOperationFingerprintV1.Encode(command));
            Assert.That(
                CareerOperationFingerprintV1.Hash(command).Value,
                Is.EqualTo("ce66e7b0fe43edf4c833e99a050e0d765cd12c8fdba5237a4d4b981999162852"));
        }

        [Test]
        public void ExecuteWeekActionFingerprintV1_ExcludesReceiptLookupAndCompletionMetadata()
        {
            var baseline = ExecuteCommand();
            var changedOperation = ExecuteCommand(
                operationId: new OperationId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
            var changedTime = ExecuteCommand(completedAtUtcMs: 9007199254740991L);

            Assert.That(CareerOperationFingerprintV1.Hash(changedOperation),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
            Assert.That(CareerOperationFingerprintV1.Hash(changedTime),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
            var json = Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(baseline));
            Assert.That(json, Does.Not.Contain("operationId"));
            Assert.That(json, Does.Not.Contain("completedAtUtcMs"));
        }

        [Test]
        public void ExecuteWeekActionFingerprintV1_IsSensitiveToEveryBusinessInput()
        {
            var baseline = CareerOperationFingerprintV1.Hash(ExecuteCommand());
            var mutations = new[]
            {
                ExecuteCommand(profileId: new ProfileId(Guid.Parse("12121212-1212-1212-1212-121212121212"))),
                ExecuteCommand(saveId: new SaveId(Guid.Parse("13131313-1313-1313-1313-131313131313"))),
                ExecuteCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("14141414-1414-1414-1414-141414141414")),
                    5,
                    new Sha256Digest(ZeroHash))),
                ExecuteCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    6,
                    new Sha256Digest(ZeroHash))),
                ExecuteCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    5,
                    new Sha256Digest(new string('e', 64)))),
                ExecuteCommand(weekPlanId: new WeekPlanId(Guid.Parse("15151515-1515-1515-1515-151515151515"))),
                ExecuteCommand(slotNumber: 2),
                ExecuteCommand(slotActionId: new SlotActionId(Guid.Parse("16161616-1616-1616-1616-161616161616"))),
                ExecuteCommand(actionOccurrenceId: new OccurrenceId(Guid.Parse("17171717-1717-1717-1717-171717171717"))),
                ExecuteCommand(contentId: "week_action.specialized.serve"),
                ExecuteCommand(eventOccurrenceId: null, useDefaultEventOccurrence: false),
                ExecuteCommand(eventOccurrenceId: new OccurrenceId(Guid.Parse("18181818-1818-1818-1818-181818181818")))
            };

            foreach (var mutation in mutations)
            {
                Assert.That(CareerOperationFingerprintV1.Hash(mutation), Is.Not.EqualTo(baseline));
            }
        }

        [Test]
        public void ResolveEventChoiceCommand_HasExactImmutablePublicContract()
        {
            var type = typeof(ResolveEventChoiceCommand);
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "ProfileId", "SaveId", "ExpectedVersionToken", "OperationId",
                    "CompletedAtUtcMs", "WeekPlanId", "SourceSlotActionId",
                    "SourceActionOccurrenceId", "EventId", "EventOccurrenceId", "OptionId"
                },
                properties.Select(property => property.Name));
            Assert.That(properties, Has.All.Matches<PropertyInfo>(property => !property.CanWrite));
            Assert.That(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Has.Length.EqualTo(1));
        }

        [Test]
        public void ResolveEventChoiceFingerprintV1_HasLockedCanonicalBytesAndHash()
        {
            const string expectedJson =
                "{\"fingerprintSchemaVersion\":1,\"operationKind\":\"resolve_event_choice\",\"profileId\":\"11111111-1111-1111-1111-111111111111\",\"saveId\":\"22222222-2222-2222-2222-222222222222\",\"expectedLineageId\":\"33333333-3333-3333-3333-333333333333\",\"expectedRevision\":6,\"expectedSnapshotHash\":\"" + ZeroHash + "\",\"weekPlanId\":\"44444444-4444-4444-4444-444444444444\",\"sourceSlotActionId\":\"55555555-5555-5555-5555-555555555555\",\"sourceActionOccurrenceId\":\"66666666-6666-6666-6666-666666666666\",\"eventId\":\"event.team_meal\",\"eventOccurrenceId\":\"00000000-0000-0000-0000-000000000003\",\"optionId\":\"event.team_meal.option.extra_practice\",\"schemaVersion\":1,\"contentVersion\":1,\"rulesetVersion\":1,\"careerRandomAlgorithmVersion\":1}";
            var command = ResolveCommand();

            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(expectedJson),
                CareerOperationFingerprintV1.Encode(command));
            Assert.That(
                CareerOperationFingerprintV1.Hash(command).Value,
                Is.EqualTo("0b268051bdeb3dd7a9b0999c801240a8bf6d2b31ea69147f521a655fdc820f81"));
        }

        [Test]
        public void ResolveEventChoiceFingerprintV1_ExcludesReceiptLookupAndCompletionMetadata()
        {
            var baseline = ResolveCommand();
            var changedOperation = ResolveCommand(
                operationId: new OperationId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
            var changedTime = ResolveCommand(completedAtUtcMs: 9007199254740991L);

            Assert.That(CareerOperationFingerprintV1.Hash(changedOperation),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
            Assert.That(CareerOperationFingerprintV1.Hash(changedTime),
                Is.EqualTo(CareerOperationFingerprintV1.Hash(baseline)));
            var json = Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(baseline));
            Assert.That(json, Does.Not.Contain("operationId"));
            Assert.That(json, Does.Not.Contain("completedAtUtcMs"));
        }

        [Test]
        public void ResolveEventChoiceFingerprintV1_IsSensitiveToEveryBusinessInput()
        {
            var baseline = CareerOperationFingerprintV1.Hash(ResolveCommand());
            var mutations = new[]
            {
                ResolveCommand(profileId: new ProfileId(Guid.Parse("12121212-1212-1212-1212-121212121212"))),
                ResolveCommand(saveId: new SaveId(Guid.Parse("13131313-1313-1313-1313-131313131313"))),
                ResolveCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("14141414-1414-1414-1414-141414141414")),
                    6,
                    new Sha256Digest(ZeroHash))),
                ResolveCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    7,
                    new Sha256Digest(ZeroHash))),
                ResolveCommand(expectedToken: Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    6,
                    new Sha256Digest(new string('e', 64)))),
                ResolveCommand(weekPlanId: new WeekPlanId(Guid.Parse("15151515-1515-1515-1515-151515151515"))),
                ResolveCommand(sourceSlotActionId: new SlotActionId(Guid.Parse("16161616-1616-1616-1616-161616161616"))),
                ResolveCommand(sourceActionOccurrenceId: new OccurrenceId(Guid.Parse("17171717-1717-1717-1717-171717171717"))),
                ResolveCommand(eventId: "event.team_meal.changed"),
                ResolveCommand(eventOccurrenceId: new OccurrenceId(Guid.Parse("18181818-1818-1818-1818-181818181818"))),
                ResolveCommand(optionId: "event.team_meal.option.attend")
            };

            foreach (var mutation in mutations)
            {
                Assert.That(CareerOperationFingerprintV1.Hash(mutation), Is.Not.EqualTo(baseline));
            }
        }

        [Test]
        public void ResolveEventChoiceFingerprintV1_UsesStrictCanonicalStringEscapingAndRejectsLoneSurrogate()
        {
            var escaped = ResolveCommand(
                eventId: "event.\"\\/\b\t\n\f\r\u0001雪😀",
                optionId: "option.strict");
            var encoded = Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(escaped));

            Assert.That(
                encoded,
                Does.Contain("\"eventId\":\"event.\\\"\\\\/\\b\\t\\n\\f\\r\\u0001雪😀\""));

            var loneSurrogate = new string(new[] { '\ud800' });
            Assert.Throws<ArgumentException>(() =>
                CareerOperationFingerprintV1.Encode(ResolveCommand(eventId: loneSurrogate)));
        }

        private static string Fingerprint(CareerWeekPlanState candidate)
        {
            return Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(Command(candidate: candidate)));
        }

        private static ExecuteWeekActionCommand ExecuteCommand(
            ProfileId? profileId = null,
            SaveId? saveId = null,
            CareerVersionToken? expectedToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 100,
            WeekPlanId? weekPlanId = null,
            int slotNumber = 1,
            SlotActionId? slotActionId = null,
            OccurrenceId? actionOccurrenceId = null,
            string contentId = "week_action.specialized.spike",
            OccurrenceId? eventOccurrenceId = null,
            bool useDefaultEventOccurrence = true)
        {
            return new ExecuteWeekActionCommand(
                profileId ?? new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                saveId ?? new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                expectedToken ?? Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    5,
                    new Sha256Digest(ZeroHash)),
                operationId ?? new OperationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                completedAtUtcMs,
                weekPlanId ?? new WeekPlanId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                slotNumber,
                slotActionId ?? new SlotActionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                actionOccurrenceId ?? new OccurrenceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                contentId,
                useDefaultEventOccurrence && !eventOccurrenceId.HasValue
                    ? new OccurrenceId?(new OccurrenceId(Guid.Parse("00000000-0000-0000-0000-000000000003")))
                    : eventOccurrenceId);
        }

        private static ResolveEventChoiceCommand ResolveCommand(
            ProfileId? profileId = null,
            SaveId? saveId = null,
            CareerVersionToken? expectedToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 100,
            WeekPlanId? weekPlanId = null,
            SlotActionId? sourceSlotActionId = null,
            OccurrenceId? sourceActionOccurrenceId = null,
            string eventId = "event.team_meal",
            OccurrenceId? eventOccurrenceId = null,
            string optionId = "event.team_meal.option.extra_practice")
        {
            return new ResolveEventChoiceCommand(
                profileId ?? new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                saveId ?? new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                expectedToken ?? Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    6,
                    new Sha256Digest(ZeroHash)),
                operationId ?? new OperationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                completedAtUtcMs,
                weekPlanId ?? new WeekPlanId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                sourceSlotActionId ?? new SlotActionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                sourceActionOccurrenceId ?? new OccurrenceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                eventId,
                eventOccurrenceId ?? new OccurrenceId(Guid.Parse("00000000-0000-0000-0000-000000000003")),
                optionId);
        }

        private static ConfirmWeekPlanCommand Command(
            CareerVersionToken? expectedToken = null,
            OperationId? operationId = null,
            long completedAtUtcMs = 100,
            CareerWeekPlanState candidate = null)
        {
            return new ConfirmWeekPlanCommand(
                new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                expectedToken ?? Token(
                    new LineageId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                    4,
                    new Sha256Digest(ZeroHash)),
                operationId ?? new OperationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                completedAtUtcMs,
                candidate ?? CandidatePlan());
        }

        private static CareerVersionToken Token(
            LineageId lineageId,
            long revision,
            Sha256Digest hash)
        {
            return new CareerVersionToken(lineageId, revision, hash);
        }

        private static CareerWeekPlanState CandidatePlan(
            WeekPlanId? planId = null,
            SlotActionId? firstSlotActionId = null,
            OccurrenceId? firstOccurrenceId = null,
            CareerWeekActionKind firstKind = CareerWeekActionKind.SpecializedTraining,
            string firstContentId = "week_action.specialized.spike",
            SlotActionId? secondSlotActionId = null,
            OccurrenceId? secondOccurrenceId = null,
            CareerWeekActionKind secondKind = CareerWeekActionKind.Rest,
            string secondContentId = "week_action.rest.standard")
        {
            return new CareerWeekPlanState(
                planId ?? new WeekPlanId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                1,
                1,
                new[]
                {
                    new CareerWeekActionState(
                        firstSlotActionId ?? new SlotActionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                        firstOccurrenceId ?? new OccurrenceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                        firstKind,
                        firstContentId),
                    new CareerWeekActionState(
                        secondSlotActionId ?? new SlotActionId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                        secondOccurrenceId ?? new OccurrenceId(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                        secondKind,
                        secondContentId),
                    new CareerWeekActionState(
                        new SlotActionId(Guid.Parse("99999999-9999-9999-9999-999999999999")),
                        new OccurrenceId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                        CareerWeekActionKind.Match,
                        "schedule.u1w1.match.01")
                },
                true);
        }
    }
}
