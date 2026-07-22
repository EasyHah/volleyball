using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveJsonCodecTests
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;
        private const string GoldenHash =
            "21e935e98a4fdc6f3158f22b5adb115f5e0eab94ce755a64ba4882d676b4a98a";

        private const string GoldenBase64 =
            "eyJ2ZXJzaW9ucyI6eyJzY2hlbWFWZXJzaW9uIjoxLCJjb250ZW50VmVyc2lvbiI6MSwicnVsZXNldFZlcnNpb24iOjEsImNhcmVl" +
            "clJhbmRvbUFsZ29yaXRobVZlcnNpb24iOjF9LCJpZGVudGl0eSI6eyJwcm9maWxlSWQiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAw" +
            "MC0wMDAwMDAwMDAwMDEiLCJzYXZlSWQiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDIiLCJsaW5lYWdlSWQi" +
            "OiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDMiLCJyZXZpc2lvbiI6MSwicmVzdG9yZWRGcm9tVmVyc2lvblRv" +
            "a2VuIjpudWxsLCJjcmVhdGVkQXRVdGNNcyI6MCwidXBkYXRlZEF0VXRjTXMiOjkwMDcxOTkyNTQ3NDA5OTF9LCJpbnRlZ3JpdHki" +
            "Onsic25hcHNob3RIYXNoIjoiMjFlOTM1ZTk4YTRmZGM2ZjMxNThmMjJiNWFkYjExNWY1ZTBlYWI5NGNlNzU1YTY0YmE0ODgyZDY3" +
            "NmI0YTk4YSJ9LCJjYXJlZXJTZWVkIjoiMDAwMTAyMDMwNDA1MDYwNzA4MDkwYTBiMGMwZDBlMGYxMDExMTIxMzE0MTUxNjE3MTgx" +
            "OTFhMWIxYzFkMWUxZiIsImNhcmVlck5hbWUiOiJFc2NhcGVzOlwiXFwvIENvbnRyb2xzOlx1MDAwMFx1MDAwMVx1MDAwMlx1MDAw" +
            "M1x1MDAwNFx1MDAwNVx1MDAwNlx1MDAwN1xiXHRcblx1MDAwYlxmXHJcdTAwMGVcdTAwMGZcdTAwMTBcdTAwMTFcdTAwMTJcdTAw" +
            "MTNcdTAwMTRcdTAwMTVcdTAwMTZcdTAwMTdcdTAwMThcdTAwMTlcdTAwMWFcdTAwMWJcdTAwMWNcdTAwMWRcdTAwMWVcdTAwMWYg" +
            "VW5pY29kZTrpm6rwn5iAIMOpIGXMgSIsInBsYXllckRyYWZ0Ijp7InBsYXllcklkIjoicGxheWVyLmFscGhhIiwiZGlzcGxheU5h" +
            "bWUiOiJQbGF5ZXIg6Zuq8J+YgCIsImplcnNleU51bWJlciI6MTJ9LCJvbmJvYXJkaW5nIjp7InN0YWdlcyI6W3sic3RhZ2VOdW1i" +
            "ZXIiOjEsIm9jY3VycmVuY2VJZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDEwMSIsInJhbmRvbVZlcnNpb24i" +
            "OjEsImNob2ljZUlkIjpudWxsLCJyZXNvbHZlZE91dHB1dHMiOltdfSx7InN0YWdlTnVtYmVyIjoyLCJvY2N1cnJlbmNlSWQiOiIw" +
            "MDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAxMDIiLCJyYW5kb21WZXJzaW9uIjoxLCJjaG9pY2VJZCI6bnVsbCwicmVz" +
            "b2x2ZWRPdXRwdXRzIjpbXX0seyJzdGFnZU51bWJlciI6Mywib2NjdXJyZW5jZUlkIjoiMDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAt" +
            "MDAwMDAwMDAwMTAzIiwicmFuZG9tVmVyc2lvbiI6MSwiY2hvaWNlSWQiOm51bGwsInJlc29sdmVkT3V0cHV0cyI6W119XSwibmV4" +
            "dFN0YWdlTnVtYmVyIjoxLCJpc0Zvcm1hbGx5RW5yb2xsZWQiOmZhbHNlfSwicHJvZ3Jlc3Npb24iOnsia2luZCI6ImNhcmVlcl9j" +
            "cmVhdGVkIiwicGhhc2UiOiJ1bml2ZXJzaXR5IiwidHJ5b3V0U3RhZ2UiOjAsIndlZWtQbGFuIjpudWxsLCJuZXh0U2xvdE51bWJl" +
            "ciI6MCwicGVuZGluZ0V2ZW50IjpudWxsfSwicGxheWVyIjpudWxsLCJ0ZWFtSWQiOm51bGwsInBvdGVudGlhbEdyYWRlIjpudWxs" +
            "LCJmYXRpZ3VlIjpudWxsLCJtaW5kc2V0IjpudWxsLCJjb2FjaFRydXN0IjpudWxsLCJvcGVyYXRpb25SZWNlaXB0cyI6W3sib3Bl" +
            "cmF0aW9uSWQiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAyMDAiLCJvcGVyYXRpb25LaW5kIjoiY3JlYXRlX2Nh" +
            "cmVlciIsInRhcmdldCI6eyJ0cnlvdXRTdGFnZSI6MCwidHJ5b3V0T2NjdXJyZW5jZUlkIjpudWxsLCJjaG9pY2VJZCI6bnVsbCwi" +
            "d2Vla1BsYW5JZCI6bnVsbCwic2xvdEFjdGlvbklkIjpudWxsLCJhY3Rpb25PY2N1cnJlbmNlSWQiOm51bGwsImV2ZW50T2NjdXJy" +
            "ZW5jZUlkIjpudWxsLCJvcHRpb25JZCI6bnVsbH0sImlucHV0RmluZ2VycHJpbnQiOiJhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFh" +
            "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhIiwiYXBwbGllZExpbmVhZ2VJZCI6IjAwMDAwMDAwLTAwMDAt" +
            "MDAwMC0wMDAwLTAwMDAwMDAwMDAwMyIsImFwcGxpZWRSZXZpc2lvbiI6MSwiY29tcGxldGVkQXRVdGNNcyI6OTAwNzE5OTI1NDc0" +
            "MDk5MSwib3V0Y29tZUtpbmQiOiJjYXJlZXJfY3JlYXRlZCIsIm91dGNvbWVTdW1tYXJ5Ijp7InRyeW91dFJlc29sdmVkT3V0cHV0" +
            "cyI6W10sImdyb3d0aEV4cGVyaWVuY2VEZWx0YSI6bnVsbCwiZmF0aWd1ZURlbHRhIjpudWxsLCJtaW5kc2V0RGVsdGEiOm51bGws" +
            "ImNvYWNoVHJ1c3REZWx0YSI6bnVsbH19XX0=";

        [Test]
        public void GoldenSnapshot_HasIndependentCanonicalBytesAndHash()
        {
            var candidate = CreateGoldenCandidate();

            var sealedSnapshot = CareerSaveJsonCodec.Seal(candidate);
            var actualBytes = CareerSaveJsonCodec.Serialize(sealedSnapshot);
            var expectedBytes = Convert.FromBase64String(GoldenBase64);

            Assert.That(actualBytes, Is.EqualTo(expectedBytes));
            Assert.That(
                CareerSaveJsonCodec.ComputeSnapshotHash(candidate).Value,
                Is.EqualTo(GoldenHash));
            Assert.That(sealedSnapshot.Identity.SnapshotHash.Value, Is.EqualTo(GoldenHash));
            Assert.That(actualBytes[0], Is.EqualTo((byte)'{'));
            Assert.That(candidate.Identity.SnapshotHash.Value, Is.EqualTo(new string('0', 64)));

            var json = Encoding.UTF8.GetString(actualBytes);
            StringAssert.Contains("\\u0000\\u0001\\u0002", json);
            StringAssert.Contains("\\b\\t\\n\\u000b\\f\\r", json);
            StringAssert.Contains("\\u001d\\u001e\\u001f", json);
            StringAssert.Contains("Unicode:雪" + char.ConvertFromUtf32(0x1f600), json);
            StringAssert.Contains("é e\u0301", json);
        }

        [Test]
        public void GoldenSnapshot_StrictlyDeserializesToTheDomainModel()
        {
            var restored = CareerSaveJsonCodec.Deserialize(
                Convert.FromBase64String(GoldenBase64));

            Assert.That(restored.Identity.SnapshotHash.Value, Is.EqualTo(GoldenHash));
            Assert.That(restored.Identity.RestoredFromVersionToken, Is.Null);
            Assert.That(restored.Identity.UpdatedAtUtcMs, Is.EqualTo(MaximumIJsonSafeInteger));
            Assert.That(restored.CareerName, Is.EqualTo(SpecialCareerName()));
            Assert.That(restored.Onboarding.Stages[0].ResolvedOutputs, Is.Empty);
            Assert.That(restored.Player, Is.Null);
            Assert.That(restored.TeamId, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullSchemaRoundTrip_PreservesNestedStateAndRestoreProvenance(
            bool eventAlreadyResolved)
        {
            var source = CreateFullCandidate(eventAlreadyResolved);

            var sealedSnapshot = CareerSaveJsonCodec.Seal(source);
            var bytes = CareerSaveJsonCodec.Serialize(sealedSnapshot);
            var restored = CareerSaveJsonCodec.Deserialize(bytes);

            Assert.That(restored.Identity.SnapshotHash, Is.EqualTo(sealedSnapshot.Identity.SnapshotHash));
            Assert.That(restored.Identity.RestoredFromVersionToken.HasValue, Is.True);
            Assert.That(
                restored.Identity.RestoredFromVersionToken.Value.LineageId,
                Is.EqualTo(new LineageId(GuidValue(4))));
            Assert.That(restored.Player.Attributes.Stamina.GrowthExperience, Is.EqualTo(108));
            Assert.That(restored.OperationReceipts.Count, Is.EqualTo(eventAlreadyResolved ? 7 : 6));

            if (eventAlreadyResolved)
            {
                Assert.That(restored.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planned));
                Assert.That(restored.Progression.PendingEvent, Is.Null);
                Assert.That(
                    restored.OperationReceipts[6].OutcomeSummary.CoachTrustDelta,
                    Is.EqualTo(-2));
            }
            else
            {
                Assert.That(
                    restored.Progression.Kind,
                    Is.EqualTo(CareerProgressionKind.AwaitingEventChoice));
                Assert.That(restored.Progression.PendingEvent.Options.Count, Is.EqualTo(2));
                Assert.That(
                    restored.Progression.PendingEvent.Options[0].GrowthExperienceDelta.Stamina,
                    Is.EqualTo(80));
            }
        }

        [Test]
        public void Serialize_RejectsAnUnsealedSnapshot()
        {
            Assert.That(
                () => CareerSaveJsonCodec.Serialize(CreateGoldenCandidate()),
                Throws.InvalidOperationException);
        }

        [Test]
        public void SnapshotHash_ExcludesOnlyTheHashFieldItself()
        {
            var first = CareerSaveJsonCodec.ComputeSnapshotHash(
                CreateGoldenCandidate('0'));
            var sameContentWithDifferentStoredHash = CareerSaveJsonCodec.ComputeSnapshotHash(
                CreateGoldenCandidate('f'));
            var changedDisplayContent = CareerSaveJsonCodec.ComputeSnapshotHash(
                CreateGoldenCandidate('0', SpecialCareerName() + " changed"));

            Assert.That(sameContentWithDifferentStoredHash, Is.EqualTo(first));
            Assert.That(changedDisplayContent, Is.Not.EqualTo(first));
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("missing")]
        [TestCase("leading_zero")]
        [TestCase("negative_zero")]
        [TestCase("floating_point")]
        [TestCase("scientific_notation")]
        [TestCase("plus_sign")]
        [TestCase("too_large")]
        [TestCase("too_small")]
        [TestCase("trailing_token")]
        [TestCase("lone_surrogate")]
        [TestCase("raw_control")]
        [TestCase("whitespace")]
        [TestCase("property_order")]
        [TestCase("escaped_slash")]
        [TestCase("uppercase_unicode_escape")]
        [TestCase("long_control_escape")]
        [TestCase("noncanonical_unicode_escape")]
        [TestCase("noncanonical_surrogate_escape")]
        [TestCase("tampered_value")]
        [TestCase("missing_hash")]
        public void Deserialize_RejectsMalformedNoncanonicalOrCorruptDocuments(string mutation)
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(GoldenBase64));

            switch (mutation)
            {
                case "duplicate":
                    json = json.Replace(
                        "\"careerSeed\":",
                        "\"careerSeed\":\"" + SeedHex() + "\",\"careerSeed\":");
                    break;
                case "unknown":
                    json = "{\"unknown\":null," + json.Substring(1);
                    break;
                case "missing":
                    json = json.Replace("\"coachTrust\":null,", string.Empty);
                    break;
                case "leading_zero":
                    json = json.Replace(
                        "\"revision\":1,\"restoredFromVersionToken\"",
                        "\"revision\":01,\"restoredFromVersionToken\"");
                    break;
                case "negative_zero":
                    json = json.Replace("\"createdAtUtcMs\":0", "\"createdAtUtcMs\":-0");
                    break;
                case "floating_point":
                    json = json.Replace(
                        "\"revision\":1,\"restoredFromVersionToken\"",
                        "\"revision\":1.0,\"restoredFromVersionToken\"");
                    break;
                case "scientific_notation":
                    json = json.Replace(
                        "\"revision\":1,\"restoredFromVersionToken\"",
                        "\"revision\":1e0,\"restoredFromVersionToken\"");
                    break;
                case "plus_sign":
                    json = json.Replace(
                        "\"revision\":1,\"restoredFromVersionToken\"",
                        "\"revision\":+1,\"restoredFromVersionToken\"");
                    break;
                case "too_large":
                    json = json.Replace("9007199254740991", "9007199254740992");
                    break;
                case "too_small":
                    json = json.Replace(
                        "\"revision\":1,\"restoredFromVersionToken\"",
                        "\"revision\":-9007199254740992,\"restoredFromVersionToken\"");
                    break;
                case "trailing_token":
                    json += "{}";
                    break;
                case "lone_surrogate":
                    json = json.Replace(
                        "\"displayName\":\"Player 雪" + char.ConvertFromUtf32(0x1f600) + "\"",
                        "\"displayName\":\"\\ud800\"");
                    break;
                case "raw_control":
                    json = json.Replace("\\u0000", "\0");
                    break;
                case "whitespace":
                    json = "{ " + json.Substring(1);
                    break;
                case "property_order":
                    json = json.Replace(
                        "\"schemaVersion\":1,\"contentVersion\":1",
                        "\"contentVersion\":1,\"schemaVersion\":1");
                    break;
                case "escaped_slash":
                    json = json.Replace("/ Controls", "\\/ Controls");
                    break;
                case "uppercase_unicode_escape":
                    json = json.Replace("\\u000b", "\\u000B");
                    break;
                case "long_control_escape":
                    json = json.Replace("\\b\\t\\n", "\\u0008\\u0009\\u000a");
                    break;
                case "noncanonical_unicode_escape":
                    json = json.Replace("Player 雪", "Player \\u96ea");
                    break;
                case "noncanonical_surrogate_escape":
                    json = json.Replace(
                        char.ConvertFromUtf32(0x1f600),
                        "\\ud83d\\ude00");
                    break;
                case "tampered_value":
                    json = json.Replace("Player 雪", "Player 霜");
                    break;
                case "missing_hash":
                    json = json.Replace(
                        "\"snapshotHash\":\"" + GoldenHash + "\"",
                        "\"notSnapshotHash\":\"" + GoldenHash + "\"");
                    break;
                default:
                    throw new AssertionException("Unknown mutation: " + mutation);
            }

            Assert.That(
                () => CareerSaveJsonCodec.Deserialize(Encoding.UTF8.GetBytes(json)),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Deserialize_RejectsInvalidUtf8AndBom()
        {
            Assert.That(
                () => CareerSaveJsonCodec.Deserialize(
                    new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x7d }),
                Throws.TypeOf<FormatException>());

            var canonical = Convert.FromBase64String(GoldenBase64);
            var withBom = new byte[canonical.Length + 3];
            withBom[0] = 0xef;
            withBom[1] = 0xbb;
            withBom[2] = 0xbf;
            Buffer.BlockCopy(canonical, 0, withBom, 3, canonical.Length);
            Assert.That(
                () => CareerSaveJsonCodec.Deserialize(withBom),
                Throws.TypeOf<FormatException>());
        }

        private static CareerSaveSnapshot CreateGoldenCandidate(
            char storedHashCharacter = '0',
            string careerName = null)
        {
            var lineageId = new LineageId(GuidValue(3));
            var onboarding = new TryoutOnboardingState(
                new[]
                {
                    UnconfirmedStage(1),
                    UnconfirmedStage(2),
                    UnconfirmedStage(3)
                },
                1,
                false);
            var receipt = new OperationReceipt(
                new OperationId(GuidValue(200)),
                OperationKind.CreateCareer,
                OperationReceiptTarget.ForCreateCareer(),
                Digest('a'),
                lineageId,
                1,
                MaximumIJsonSafeInteger,
                OperationOutcomeKind.CareerCreated,
                OperationOutcomeSummary.ForCareerCreated());

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    new ProfileId(GuidValue(1)),
                    new SaveId(GuidValue(2)),
                    lineageId,
                    1,
                    0,
                    MaximumIJsonSafeInteger,
                    Digest(storedHashCharacter)),
                CareerSeed.Parse(SeedHex()),
                careerName ?? SpecialCareerName(),
                new CareerPlayerDraft(
                    new PlayerId("player.alpha"),
                    "Player 雪" + char.ConvertFromUtf32(0x1f600),
                    12),
                onboarding,
                CareerProgressionState.Created(),
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { receipt });
        }

        private static CareerSaveSnapshot CreateFullCandidate(bool eventAlreadyResolved)
        {
            var lineageId = new LineageId(GuidValue(3));
            var stages = new[] { ConfirmedStage(1), ConfirmedStage(2), ConfirmedStage(3) };
            var onboarding = new TryoutOnboardingState(stages, 0, true);
            var plan = new CareerWeekPlanState(
                new WeekPlanId(GuidValue(10)),
                1,
                2,
                new[]
                {
                    Action(11, 41, CareerWeekActionKind.TeamPractice),
                    Action(12, 42, CareerWeekActionKind.Rest),
                    Action(13, 43, CareerWeekActionKind.Match)
                },
                true);
            var pendingEvent = PendingEvent(plan);
            var progression = eventAlreadyResolved
                ? CareerProgressionState.Planned(plan, 2)
                : CareerProgressionState.AwaitingEventChoice(plan, pendingEvent);

            var receipts = new List<OperationReceipt>
            {
                Receipt(
                    200,
                    1,
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    OperationOutcomeSummary.ForCareerCreated(),
                    lineageId)
            };
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                receipts.Add(
                    Receipt(
                        201 + index,
                        2 + index,
                        OperationKind.ConfirmTryoutStage,
                        OperationReceiptTarget.ForTryoutStage(
                            stage.StageNumber,
                            stage.OccurrenceId,
                            stage.ChoiceId),
                        OperationOutcomeSummary.ForTryoutAdvanced(
                            stage.ResolvedOutputs),
                        lineageId));
            }

            receipts.Add(
                Receipt(
                    210,
                    5,
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(plan.PlanId),
                    OperationOutcomeSummary.ForWeekPlanConfirmed(),
                    lineageId));
            receipts.Add(
                Receipt(
                    211,
                    6,
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        plan.PlanId,
                        plan.Slots[0].SlotActionId,
                        plan.Slots[0].OccurrenceId),
                    OperationOutcomeSummary.ForSlotCompleted(
                        Growth(1),
                        4,
                        1,
                        2),
                    lineageId));
            if (eventAlreadyResolved)
            {
                receipts.Add(
                    Receipt(
                        212,
                        7,
                        OperationKind.ResolveEventChoice,
                        OperationReceiptTarget.ForEventChoice(
                            plan.PlanId,
                            plan.Slots[0].SlotActionId,
                            plan.Slots[0].OccurrenceId,
                            pendingEvent.OccurrenceId,
                            pendingEvent.Options[0].OptionId),
                        OperationOutcomeSummary.ForEventChoiceApplied(
                            Growth(10),
                            5,
                            10,
                            -2),
                        lineageId));
            }

            var revision = eventAlreadyResolved ? 7 : 6;
            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    new ProfileId(GuidValue(1)),
                    new SaveId(GuidValue(2)),
                    lineageId,
                    revision,
                    1000,
                    2000,
                    Digest('0'),
                    new CareerVersionToken(
                        new LineageId(GuidValue(4)),
                        1,
                        Digest('b'))),
                CareerSeed.Parse(SeedHex()),
                "Road to V League",
                new CareerPlayerDraft(new PlayerId("player.alpha"), "Lin", 12),
                onboarding,
                progression,
                Player(),
                new TeamId("team.university-a"),
                PotentialGrade.B,
                23,
                72,
                61,
                receipts);
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

        private static CareerWeekActionState Action(
            int actionId,
            int occurrenceId,
            CareerWeekActionKind kind)
        {
            return new CareerWeekActionState(
                new SlotActionId(GuidValue(actionId)),
                new OccurrenceId(GuidValue(occurrenceId)),
                kind);
        }

        private static PendingCareerEvent PendingEvent(CareerWeekPlanState plan)
        {
            return new PendingCareerEvent(
                plan.PlanId,
                plan.Slots[0].SlotActionId,
                plan.Slots[0].OccurrenceId,
                "social.first-week",
                new OccurrenceId(GuidValue(50)),
                1,
                new CareerEventOptionEffect("accept", Growth(10), 5, 10, -2),
                new CareerEventOptionEffect("decline", Growth(1), -5, -10, 2));
        }

        private static CareerAttributeGrowthDelta Growth(long start)
        {
            return new CareerAttributeGrowthDelta(
                start,
                start + 10,
                start + 20,
                start + 30,
                start + 40,
                start + 50,
                start + 60,
                start + 70);
        }

        private static CareerPlayerRecord Player()
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

        private static OperationReceipt Receipt(
            int id,
            long revision,
            OperationKind operationKind,
            OperationReceiptTarget target,
            OperationOutcomeSummary outcomeSummary,
            LineageId lineageId)
        {
            return new OperationReceipt(
                new OperationId(GuidValue(id)),
                operationKind,
                target,
                Digest((char)('a' + (id % 6))),
                lineageId,
                revision,
                3000 + id,
                OutcomeKind(operationKind),
                outcomeSummary);
        }

        private static OperationOutcomeKind OutcomeKind(OperationKind kind)
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

        private static string SpecialCareerName()
        {
            var builder = new StringBuilder("Escapes:\"\\/ Controls:");
            for (var value = 0; value < 32; value++)
            {
                builder.Append((char)value);
            }

            builder.Append(" Unicode:雪");
            builder.Append(char.ConvertFromUtf32(0x1f600));
            builder.Append(" é e\u0301");
            return builder.ToString();
        }

        private static string SeedHex()
        {
            return "000102030405060708090a0b0c0d0e0f" +
                   "101112131415161718191a1b1c1d1e1f";
        }

        private static Sha256Digest Digest(char value)
        {
            return new Sha256Digest(new string(value, 64));
        }

        private static Guid GuidValue(int value)
        {
            return Guid.Parse("00000000-0000-0000-0000-" + value.ToString("D12"));
        }
    }
}
