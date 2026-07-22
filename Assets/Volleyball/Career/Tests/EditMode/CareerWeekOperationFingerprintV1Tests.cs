using System;
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

        private static string Fingerprint(CareerWeekPlanState candidate)
        {
            return Encoding.UTF8.GetString(CareerOperationFingerprintV1.Encode(Command(candidate: candidate)));
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
