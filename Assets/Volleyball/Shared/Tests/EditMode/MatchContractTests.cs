using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.EditModeTests
{
    public sealed class MatchContractTests
    {
        [Test]
        public void ProductionContractSurface_RequiresConcreteV4ContextAndResult()
        {
            var assembly = typeof(MatchContextV1).Assembly;
            var contextV4 = assembly.GetType("Volleyball.Shared.Contracts.MatchContextV4");
            var resultV4 = assembly.GetType("Volleyball.Shared.Contracts.MatchResultV4");

            Assert.That(contextV4, Is.Not.Null);
            Assert.That(resultV4, Is.Not.Null);
            AssertNoPublicEntryPointAccepts(typeof(ContractJson).Assembly, typeof(IMatchContext), typeof(IMatchResult));
        }

        private static void AssertNoPublicEntryPointAccepts(Assembly assembly, params Type[] prohibited)
        {
            var publicTypes = assembly.GetTypes().Where(type => type.IsPublic).ToArray();
            var exposedTypes = publicTypes
                .SelectMany(type => type.GetConstructors().Cast<MethodBase>()
                    .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)))
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Concat(new[] { method is MethodInfo methodInfo ? methodInfo.ReturnType : null }))
                .Concat(publicTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    .Select(property => property.PropertyType))
                .Where(type => type != null)
                .ToArray();

            foreach (var type in prohibited)
            {
                Assert.That(exposedTypes, Has.None.EqualTo(type), "Production entry points must not expose " + type.Name + ".");
            }
        }

        [Test]
        public void MatchContext_RoundTripsWithStableVersionIdentityAndHash()
        {
            var context = CreateContext(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var json = ContractJson.Serialize(context);
            var restored = ContractJson.DeserializeContext(json);

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV1));
            Assert.That(restored.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(restored.Seed, Is.EqualTo(7351));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.Home.TeamId, Is.EqualTo(new TeamId("team-blue")));
            Assert.That(restored.Away.Players, Has.Count.EqualTo(3));
            Assert.That(restored.Home.Players[0].Ability.AttackPower, Is.EqualTo(0.87f));
        }

        [Test]
        public void MatchContext_ProducesSameHashForSamePayload()
        {
            var session = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var first = CreateContext(session, 7351);
            var second = CreateContext(session, 7351);

            Assert.That(second.ContextHash, Is.EqualTo(first.ContextHash));
            Assert.That(second.ContextHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void MatchContext_RejectsTamperedPayload()
        {
            var context = CreateContext(Guid.NewGuid(), 7351);
            var json = ContractJson.Serialize(context);
            var tampered = json.Replace("\"seed\":7351", "\"seed\":7352");

            Assert.That(tampered, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeContext(tampered),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("contextHash"));
        }

        [Test]
        public void MatchContext_RejectsUnsupportedVersionAndMalformedJson()
        {
            var json = ContractJson.Serialize(CreateContext(Guid.NewGuid(), 12));
            var unsupported = json.Replace("\"contractVersion\":1", "\"contractVersion\":2");

            Assert.That(
                () => ContractJson.DeserializeContext(unsupported),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("Unsupported"));
            Assert.That(
                () => ContractJson.DeserializeContext("{not-json}"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContext_RejectsDuplicateStablePlayerIdsAcrossTeams()
        {
            var home = CreateTeam("team-blue", TeamSide.Home, "shared-player");
            var away = CreateTeam("team-orange", TeamSide.Away, "shared-player");

            Assert.That(
                () => MatchContextV1.Create(Guid.NewGuid(), 1, home, away),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("unique across"));
        }

        [Test]
        public void MatchResult_RoundTripsAndMustBelongToItsContext()
        {
            var context = CreateContext(Guid.NewGuid(), 99);
            var stats = new[]
            {
                new PlayerMatchStatsV1(
                    context.Home.Players[0].PlayerId,
                    points: 6,
                    contacts: 12,
                    errors: 1,
                    workload: 0.72f)
            };
            var result = MatchResultV1.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                stats);

            var restored = ContractJson.DeserializeResult(ContractJson.Serialize(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.WinnerTeamId, Is.EqualTo(context.Home.TeamId));
            Assert.That(restored.PlayerStats[0].Workload, Is.EqualTo(0.72f));
            var otherContext = CreateContext(Guid.NewGuid(), 99);
            Assert.That(
                () => restored.ValidateAgainst(otherContext),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void LegacyV1Json_DeserializesWithItsOriginalHash()
        {
            var context = ContractJson.DeserializeContext(LegacyMatchContextV1Fixture.Json);

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV1));
            Assert.That(context.ContextHash, Is.EqualTo(LegacyMatchContextV1Fixture.ContextHash));
        }

        [Test]
        public void MatchContextV2_RoundTripsWithAttackReach()
        {
            var context = CreateContextV2(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var restored = ContractJson.DeserializeContextV2(ContractJson.SerializeV2(context));

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(3.42f));
        }

        [TestCase(PlayerPosition.Setter, 3.20f)]
        [TestCase(PlayerPosition.OutsideHitter, 3.42f)]
        [TestCase(PlayerPosition.MiddleBlocker, 3.48f)]
        [TestCase(PlayerPosition.Opposite, 3.42f)]
        [TestCase(PlayerPosition.Libero, 3.20f)]
        [TestCase(PlayerPosition.Defender, 3.20f)]
        public void MatchContextV2_UpgradeFromV1UsesDeterministicPositionReach(
            PlayerPosition position,
            float expectedReach)
        {
            var legacy = MatchContextV1.Create(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351,
                CreateTeam("team-blue", TeamSide.Home, "blue", position),
                CreateTeam("team-orange", TeamSide.Away, "orange", position));

            var upgraded = MatchContextV2.UpgradeFromV1(legacy);

            Assert.That(upgraded.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(upgraded.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(expectedReach));
            Assert.That(upgraded.Away.Players[0].Ability.MaxAttackReach, Is.EqualTo(expectedReach));
        }

        [Test]
        public void PlayerAbilitySnapshotV2_RejectsReachBelowMinimum()
        {
            Assert.That(
                () => new PlayerAbilitySnapshotV2(
                    0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 3.19f),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV2_RejectsReachBelowMinimumAfterDeserialization()
        {
            var json = ContractJson.SerializeV2(CreateContextV2(Guid.NewGuid(), 1));
            var invalid = json.Replace("\"maxAttackReach\":3.42", "\"maxAttackReach\":3.19");

            Assert.That(invalid, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeContextV2(invalid),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("[3.20, 3.55]"));
        }

        [Test]
        public void MatchResultV2_RoundTripsWithV2ContextIdentity()
        {
            var context = CreateContextV2(Guid.NewGuid(), 99);
            var result = MatchResultV2.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV2(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            var restored = ContractJson.DeserializeResultV2(ContractJson.SerializeV2(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
        }

        [Test]
        public void ContractVersions_ReserveV3ContextAndReplayV2()
        {
            Assert.That(ContractVersions.MatchV3, Is.EqualTo(3));
            Assert.That(ContractVersions.ReplayV2, Is.EqualTo(2));
        }

        [Test]
        public void ContractVersions_ReserveV4MatchAndReplayIndependentlyOfV3Rules()
        {
            Assert.That(ContractVersions.MatchV3, Is.EqualTo(3));
            Assert.That(ContractVersions.MatchV4, Is.EqualTo(4));
            Assert.That(ContractVersions.ReplayV4, Is.EqualTo(4));
        }

        [Test]
        public void DominantHandV4_DeclaresExactlyBothValidHands()
        {
            Assert.That(DominantHandV4.Left, Is.EqualTo((DominantHandV4)0));
            Assert.That(DominantHandV4.Right, Is.EqualTo((DominantHandV4)1));
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), DominantHandV4.Left), Is.True);
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), DominantHandV4.Right), Is.True);
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), (DominantHandV4)2), Is.False);
        }

        [Test]
        public void PhysicalBaseAttributesV4_PreservesEveryValidBoundaryAndUsesValueEquality()
        {
            var minimum = new PhysicalBaseAttributesV4(1.40f, 1.70f, 0f, 0f, 0f, 0f);
            var maximum = new PhysicalBaseAttributesV4(2.30f, 3.10f, 1f, 1f, 1f, 1f);
            var sameAsMinimum = new PhysicalBaseAttributesV4(1.40f, 1.70f, 0f, 0f, 0f, 0f);

            Assert.That(minimum.HeightMeters, Is.EqualTo(1.40f));
            Assert.That(minimum.StandingReachMeters, Is.EqualTo(1.70f));
            Assert.That(minimum.Jump, Is.Zero);
            Assert.That(minimum.Mobility, Is.Zero);
            Assert.That(minimum.Reaction, Is.Zero);
            Assert.That(minimum.Coordination, Is.Zero);
            Assert.That(maximum.HeightMeters, Is.EqualTo(2.30f));
            Assert.That(maximum.StandingReachMeters, Is.EqualTo(3.10f));
            Assert.That(maximum.Jump, Is.EqualTo(1f));
            Assert.That(maximum.Mobility, Is.EqualTo(1f));
            Assert.That(maximum.Reaction, Is.EqualTo(1f));
            Assert.That(maximum.Coordination, Is.EqualTo(1f));
            Assert.That(sameAsMinimum, Is.EqualTo(minimum));
            Assert.That(sameAsMinimum.GetHashCode(), Is.EqualTo(minimum.GetHashCode()));

            var baseline = CreatePhysicalBaseAttributes(0f, -1);
            for (var field = 0; field < 6; field++)
            {
                Assert.That(CreatePhysicalBaseAttributes(field < 2 ? baseline.HeightMeters + 0.01f : 0.51f, field),
                    Is.Not.EqualTo(baseline), "Physical equality must include field " + field + ".");
            }
        }

        [Test]
        public void PhysicalBaseAttributesV4_RejectsNonFiniteAndOutOfRangeValuesForEveryField()
        {
            var minimums = new[] { 1.40f, 1.70f, 0f, 0f, 0f, 0f };
            var maximums = new[] { 2.30f, 3.10f, 1f, 1f, 1f, 1f };

            for (var field = 0; field < minimums.Length; field++)
            {
                AssertPhysicalBaseAttributeRejected(field, float.NaN);
                AssertPhysicalBaseAttributeRejected(field, float.PositiveInfinity);
                AssertPhysicalBaseAttributeRejected(field, float.NegativeInfinity);
                AssertPhysicalBaseAttributeRejected(field, minimums[field] - 0.001f);
                AssertPhysicalBaseAttributeRejected(field, maximums[field] + 0.001f);
            }
        }

        [Test]
        public void PhysicalBaseAttributesV4_RejectsStandingReachBelowHeightWithoutCorrectingIt()
        {
            Assert.That(
                () => new PhysicalBaseAttributesV4(2.0f, 1.99f, 0.5f, 0.5f, 0.5f, 0.5f),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void TechnicalBaseAttributesV4_PreservesEveryValidBoundaryAndUsesValueEquality()
        {
            var minimum = CreateTechnicalBaseAttributes(0f, -1);
            var maximum = CreateTechnicalBaseAttributes(1f, -1);
            var sameAsMinimum = CreateTechnicalBaseAttributes(0f, -1);

            Assert.That(minimum.AttackTechnique, Is.Zero);
            Assert.That(minimum.AttackPower, Is.Zero);
            Assert.That(minimum.BlockTechnique, Is.Zero);
            Assert.That(minimum.DefenseTechnique, Is.Zero);
            Assert.That(minimum.ReceiveTechnique, Is.Zero);
            Assert.That(minimum.SetTechnique, Is.Zero);
            Assert.That(minimum.ServeTechnique, Is.Zero);
            Assert.That(minimum.SoftTouch, Is.Zero);
            Assert.That(minimum.CourtAwareness, Is.Zero);
            Assert.That(maximum.AttackTechnique, Is.EqualTo(1f));
            Assert.That(maximum.AttackPower, Is.EqualTo(1f));
            Assert.That(maximum.BlockTechnique, Is.EqualTo(1f));
            Assert.That(maximum.DefenseTechnique, Is.EqualTo(1f));
            Assert.That(maximum.ReceiveTechnique, Is.EqualTo(1f));
            Assert.That(maximum.SetTechnique, Is.EqualTo(1f));
            Assert.That(maximum.ServeTechnique, Is.EqualTo(1f));
            Assert.That(maximum.SoftTouch, Is.EqualTo(1f));
            Assert.That(maximum.CourtAwareness, Is.EqualTo(1f));
            Assert.That(sameAsMinimum, Is.EqualTo(minimum));
            Assert.That(sameAsMinimum.GetHashCode(), Is.EqualTo(minimum.GetHashCode()));

            var baseline = CreateTechnicalBaseAttributes(0.5f, -1);
            for (var field = 0; field < 9; field++)
            {
                Assert.That(CreateTechnicalBaseAttributes(0.51f, field),
                    Is.Not.EqualTo(baseline), "Technical equality must include field " + field + ".");
            }
        }

        [Test]
        public void TechnicalBaseAttributesV4_RejectsNonFiniteAndOutOfRangeValuesForEveryField()
        {
            for (var field = 0; field < 9; field++)
            {
                AssertTechnicalBaseAttributeRejected(field, float.NaN);
                AssertTechnicalBaseAttributeRejected(field, float.PositiveInfinity);
                AssertTechnicalBaseAttributeRejected(field, float.NegativeInfinity);
                AssertTechnicalBaseAttributeRejected(field, -0.001f);
                AssertTechnicalBaseAttributeRejected(field, 1.001f);
            }
        }

        [Test]
        public void MatchAttributeDerivationV4_ProducesByteIdenticalCanonicalResultsForIdenticalInputs()
        {
            var physical = CreateDerivationPhysical();
            var technical = CreateDerivationTechnical();

            var first = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
            var second = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            CollectionAssert.AreEqual(first.ToCanonicalBytes(), second.ToCanonicalBytes());
            Assert.That(second.Attributes, Is.EqualTo(first.Attributes));
            Assert.That(second.InputFingerprint, Is.EqualTo(first.InputFingerprint));
            Assert.That(second.ResultFingerprint, Is.EqualTo(first.ResultFingerprint));
            Assert.That(second.InputFingerprint, Has.Length.EqualTo(64));
            Assert.That(second.ResultFingerprint, Has.Length.EqualTo(64));
        }

        [Test]
        public void MatchAttributeDerivationV4_UsesFrozenVersionOneFormulasAndExplanationOrder()
        {
            var derived = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(derived.FormulaVersion, Is.EqualTo(1));
            Assert.That(derived.CoefficientVersion, Is.EqualTo(1));
            Assert.That(derived.Attributes.Attack.DirectionControl, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.SpeedControl, Is.EqualTo(0.6425f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.PowerCapacity, Is.EqualTo(0.65f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.ContactHeightMeters, Is.EqualTo(2.96f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.ApproachMobility, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.Timing, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.HandControl, Is.EqualTo(0.62f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.ReachHeightMeters, Is.EqualTo(2.8925f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.LateralMobility, Is.EqualTo(0.70f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.Reaction, Is.EqualTo(0.67f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.PlatformControl, Is.EqualTo(0.5875f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.CoverageMobility, Is.EqualTo(0.69f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Receive.FirstTouchControl, Is.EqualTo(0.6225f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Reaction, Is.EqualTo(0.67f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Movement, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Set.PlacementControl, Is.EqualTo(0.6425f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.TempoControl, Is.EqualTo(0.68f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.SoftTouch, Is.EqualTo(0.6175f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.Movement, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Serve.DirectionControl, Is.EqualTo(0.6225f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.SpeedControl, Is.EqualTo(0.615f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.PowerCapacity, Is.EqualTo(0.695f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.Consistency, Is.EqualTo(0.6325f).Within(0.000001f));
            Assert.That(
                derived.Explanations.Select(explanation => explanation.OutputName),
                Is.EqualTo(new[]
                {
                    "Attack.DirectionControl",
                    "Attack.SpeedControl",
                    "Attack.PowerCapacity",
                    "Attack.ContactHeightMeters",
                    "Attack.ApproachMobility",
                    "Block.Timing",
                    "Block.HandControl",
                    "Block.ReachHeightMeters",
                    "Block.LateralMobility",
                    "Defense.Reaction",
                    "Defense.PlatformControl",
                    "Defense.CoverageMobility",
                    "Defense.Awareness",
                    "Receive.FirstTouchControl",
                    "Receive.Reaction",
                    "Receive.Movement",
                    "Receive.Awareness",
                    "Set.PlacementControl",
                    "Set.TempoControl",
                    "Set.SoftTouch",
                    "Set.Movement",
                    "Set.Awareness",
                    "Serve.DirectionControl",
                    "Serve.SpeedControl",
                    "Serve.PowerCapacity",
                    "Serve.Consistency"
                }));
            Assert.That(derived.Explanations[0].InputNames, Is.EqualTo(new[]
            {
                "AttackTechnique", "Coordination", "CourtAwareness"
            }));
            Assert.That(derived.Explanations[0].Coefficients, Is.EqualTo(new[] { 0.65f, 0.20f, 0.15f }));
            Assert.That(derived.Explanations[0].Result, Is.EqualTo(derived.Attributes.Attack.DirectionControl));
        }

        [Test]
        public void MatchAttributeDerivationV4_EveryFormulaInputChangesItsDocumentedOutput()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());

            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(standingReachMeters: 2.51f), CreateDerivationTechnical()),
                attributes => attributes.Attack.ContactHeightMeters);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(jump: 0.61f), CreateDerivationTechnical()),
                attributes => attributes.Attack.PowerCapacity);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(mobility: 0.61f), CreateDerivationTechnical()),
                attributes => attributes.Attack.ApproachMobility);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(reaction: 0.71f), CreateDerivationTechnical()),
                attributes => attributes.Defense.Reaction);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(coordination: 0.56f), CreateDerivationTechnical()),
                attributes => attributes.Attack.DirectionControl);

            for (var field = 0; field < 9; field++)
            {
                var changed = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical(0.01f, field));
                Assert.That(
                    changed.Attributes,
                    Is.Not.EqualTo(baseline.Attributes),
                    "Technical base field " + field + " must affect at least one derived output.");
            }

            var changedHeight = DeriveV4(
                CreateDerivationPhysical(heightMeters: 1.91f),
                CreateDerivationTechnical());
            Assert.That(changedHeight.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(changedHeight.InputFingerprint, Is.Not.EqualTo(baseline.InputFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationV4_SeparatesAttackTechniqueFromPowerAuthority()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var greaterPower = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 1));
            var greaterTechnique = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 0));

            Assert.That(greaterPower.Attributes.Attack.PowerCapacity,
                Is.GreaterThan(baseline.Attributes.Attack.PowerCapacity));
            Assert.That(greaterPower.Attributes.Serve.PowerCapacity,
                Is.GreaterThan(baseline.Attributes.Serve.PowerCapacity));
            Assert.That(greaterPower.Attributes.Attack.DirectionControl,
                Is.EqualTo(baseline.Attributes.Attack.DirectionControl));
            Assert.That(greaterTechnique.Attributes.Attack.DirectionControl,
                Is.GreaterThan(baseline.Attributes.Attack.DirectionControl));
            Assert.That(greaterTechnique.Attributes.Attack.SpeedControl,
                Is.GreaterThan(baseline.Attributes.Attack.SpeedControl));
            Assert.That(greaterTechnique.Attributes.Attack.PowerCapacity,
                Is.EqualTo(baseline.Attributes.Attack.PowerCapacity));
        }

        [Test]
        public void MatchAttributeDerivationV4_PreservesSpecialistV4AuthoritiesInDeclaredGroups()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var softTouch = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 7));
            var blockTechnique = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 2));
            var awareness = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 8));

            Assert.That(softTouch.Attributes.Set.SoftTouch, Is.GreaterThan(baseline.Attributes.Set.SoftTouch));
            Assert.That(softTouch.Attributes.Receive.FirstTouchControl,
                Is.GreaterThan(baseline.Attributes.Receive.FirstTouchControl));
            Assert.That(blockTechnique.Attributes.Block.Timing, Is.GreaterThan(baseline.Attributes.Block.Timing));
            Assert.That(blockTechnique.Attributes.Block.HandControl,
                Is.GreaterThan(baseline.Attributes.Block.HandControl));
            Assert.That(awareness.Attributes.Defense.Awareness, Is.GreaterThan(baseline.Attributes.Defense.Awareness));
            Assert.That(awareness.Attributes.Receive.Awareness, Is.GreaterThan(baseline.Attributes.Receive.Awareness));
            Assert.That(awareness.Attributes.Set.Awareness, Is.GreaterThan(baseline.Attributes.Set.Awareness));
        }

        [Test]
        public void MatchAttributeDerivationV4_HandednessChangesInputAndResultIdentity()
        {
            var left = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Left,
                MatchAttributeDerivationConfigV4.Version1);
            var right = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(left.Attributes.DominantHand, Is.EqualTo(DominantHandV4.Left));
            Assert.That(right.Attributes.DominantHand, Is.EqualTo(DominantHandV4.Right));
            Assert.That(right.InputFingerprint, Is.Not.EqualTo(left.InputFingerprint));
            Assert.That(right.ResultFingerprint, Is.Not.EqualTo(left.ResultFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationV4_VersionsChangeResultIdentityWithoutChangingNumericOutputs()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var formulaVersion = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 2,
                    coefficientVersion: 1,
                    MatchAttributeDerivationConfigV4.Version1.Coefficients));
            var coefficientVersion = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 2,
                    MatchAttributeDerivationConfigV4.Version1.Coefficients));

            Assert.That(formulaVersion.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(coefficientVersion.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(formulaVersion.InputFingerprint, Is.EqualTo(baseline.InputFingerprint));
            Assert.That(coefficientVersion.InputFingerprint, Is.EqualTo(baseline.InputFingerprint));
            Assert.That(formulaVersion.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
            Assert.That(coefficientVersion.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationConfigV4_RejectsIncompleteDuplicateNonFiniteAndNonUnitWeights()
        {
            var coefficients = MatchAttributeDerivationConfigV4.Version1.Coefficients;
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(1, 1, coefficients.Skip(1)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("missing"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(1, 1, coefficients.Concat(new[] { coefficients[0] })),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("duplicate"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(
                    1,
                    1,
                    ReplaceCoefficient(coefficients, 0, float.NaN)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("finite"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(
                    1,
                    1,
                    ReplaceCoefficient(coefficients, 0, 0.64f)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("sum to 1"));
        }

        [Test]
        public void MatchAttributeDerivationV4_RejectsInvalidOutputsWithoutClamping()
        {
            var coefficients = MatchAttributeDerivationConfigV4.Version1.Coefficients;
            var invalid = ReplaceCoefficient(coefficients, 0, -2f).ToArray();
            invalid = ReplaceCoefficient(invalid, 1, 3f).ToArray();
            invalid = ReplaceCoefficient(invalid, 2, 0f).ToArray();
            var config = new MatchAttributeDerivationConfigV4(1, 2, invalid);
            var technical = new TechnicalBaseAttributesV4(
                1f, 0.75f, 0.65f, 0.6f, 0.65f, 0.7f, 0.65f, 0.6f, 0.6f);

            Assert.That(
                () => MatchAttributeDerivationV4.Derive(
                    CreateDerivationPhysical(),
                    technical,
                    DominantHandV4.Right,
                    config),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("directionControl"));
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationIsDeterministicAndRecordsProvenance()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            var first = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
            var second = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(first.MigrationVersion, Is.EqualTo(PlayerAbilitySnapshotV3.CurrentMigrationVersion));
            Assert.That(first.IsCompatibilityEstimate, Is.True);
            Assert.That(first.CompatibilityCollapsedAxes, Is.Empty);
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationDistinguishesAttackControlAndSoftTouchWhenRoleProxyExists()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            var hitter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
            var setter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.Setter);

            Assert.That(hitter.AttackControl, Is.Not.EqualTo(hitter.SoftTouch));
            Assert.That(setter.AttackControl, Is.Not.EqualTo(setter.SoftTouch));
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationRejectsNullSource()
        {
            Assert.That(
                () => PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(null, PlayerPosition.Setter),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationRejectsUndefinedPosition()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            Assert.That(
                () => PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, (PlayerPosition)999),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV3_ConstructsWithStableHashAndAllV3AbilityFields()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var first = CreateContextV3(sessionId, 7351);
            var second = CreateContextV3(sessionId, 7351);

            Assert.That(first.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(first.ContextHash, Is.EqualTo(second.ContextHash));
            Assert.That(first.ContextHash, Has.Length.EqualTo(64));
            Assert.That(first.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(first)));
            Assert.That(first.Home.Players[0].Ability.Mobility, Is.EqualTo(0.71f));
            Assert.That(first.Home.Players[0].Ability.Reaction, Is.EqualTo(0.72f));
            Assert.That(first.Home.Players[0].Ability.Jump, Is.EqualTo(0.73f));
            Assert.That(first.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(3.42f));
            Assert.That(first.Home.Players[0].Ability.ReceiveTechnique, Is.EqualTo(0.74f));
            Assert.That(first.Home.Players[0].Ability.SetTechnique, Is.EqualTo(0.75f));
            Assert.That(first.Home.Players[0].Ability.AttackControl, Is.EqualTo(0.76f));
            Assert.That(first.Home.Players[0].Ability.AttackPower, Is.EqualTo(0.77f));
            Assert.That(first.Home.Players[0].Ability.SoftTouch, Is.EqualTo(0.78f));
            Assert.That(first.Home.Players[0].Ability.BlockTechnique, Is.EqualTo(0.79f));
            Assert.That(first.Home.Players[0].Ability.CourtAwareness, Is.EqualTo(0.80f));
            Assert.That(first.Home.Players[0].Ability.SourceVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(first.Home.Players[0].Ability.MigrationVersion, Is.Zero);
            Assert.That(first.Home.Players[0].Ability.IsCompatibilityEstimate, Is.False);
            Assert.That(first.Home.Players[0].Ability.CompatibilityCollapsedAxes, Is.EqualTo(new[] { "none" }));
        }

        [Test]
        public void MatchContextV3_HashIncludesAbilityProvenanceAndCompatibilityFields()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var native = CreateContextV3(sessionId, 7351);
            var compatibilityEstimate = MatchContextV3.Create(
                sessionId,
                7351,
                CreateTeamV3("team-blue", TeamSide.Home, "blue", sourceVersion: ContractVersions.MatchV2, migrationVersion: 1, isCompatibilityEstimate: true),
                CreateTeamV3("team-orange", TeamSide.Away, "orange", sourceVersion: ContractVersions.MatchV2, migrationVersion: 1, isCompatibilityEstimate: true));

            Assert.That(compatibilityEstimate.ContextHash, Is.Not.EqualTo(native.ContextHash));
        }

        [Test]
        public void MatchContextV3_UpgradeFromV2UsesExplicitMigration()
        {
            var legacy = CreateContextV2(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var upgraded = MatchContextV3.UpgradeFromV2(legacy);

            Assert.That(upgraded.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(upgraded.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(upgraded)));
            Assert.That(upgraded.Home.Players[0].Ability.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(upgraded.Home.Players[0].Ability.MigrationVersion, Is.EqualTo(PlayerAbilitySnapshotV3.CurrentMigrationVersion));
            Assert.That(upgraded.Home.Players[0].Ability.IsCompatibilityEstimate, Is.True);
            Assert.That(upgraded.Home.Players[0].Ability.AttackControl, Is.EqualTo(
                PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(
                    legacy.Home.Players[0].Ability,
                    legacy.Home.Players[0].Position).AttackControl));
            Assert.That(upgraded.Home.Players[0].Ability.SoftTouch, Is.EqualTo(
                PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(
                    legacy.Home.Players[0].Ability,
                    legacy.Home.Players[0].Position).SoftTouch));
        }

        [Test]
        public void MatchResultV3_UsesV3ContextIdentityAndValidatesWinner()
        {
            var context = CreateContextV3(Guid.NewGuid(), 99);
            var result = MatchResultV3.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            Assert.DoesNotThrow(() => result.ValidateAgainst(context));
            Assert.That(result.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(result.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(result.ResultHash, Is.EqualTo(CanonicalMatchResultHashV3.Compute(result)));
            Assert.That(
                () => MatchResultV3.Create(
                    context,
                    context.Away.TeamId,
                    homeScore: 25,
                    awayScore: 21,
                    new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("higher final score"));
        }

        [Test]
        public void MatchContextV3_RoundTripsThroughContractJson()
        {
            var context = CreateContextV3(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var restored = ContractJson.DeserializeContextV3(ContractJson.SerializeV3(context));

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(restored.Seed, Is.EqualTo(context.Seed));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(restored)));
            Assert.That(restored.Home.Players[0].Ability.AttackControl, Is.EqualTo(0.76f));
            Assert.That(restored.Home.Players[0].Ability.SoftTouch, Is.EqualTo(0.78f));
            Assert.That(restored.Home.Players[0].Ability.CompatibilityCollapsedAxes, Is.EqualTo(new[] { "none" }));
        }

        [Test]
        public void MatchResultV3_RoundTripsThroughContractJson()
        {
            var context = CreateContextV3(Guid.NewGuid(), 99);
            var result = MatchResultV3.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            var restored = ContractJson.DeserializeResultV3(ContractJson.SerializeV3(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.ResultHash, Is.EqualTo(result.ResultHash));
            Assert.That(restored.ResultHash, Is.EqualTo(CanonicalMatchResultHashV3.Compute(restored)));
            Assert.That(restored.PlayerStats[0].Workload, Is.EqualTo(0.72f));
        }

        [Test]
        public void ContractJson_DoesNotDeserializeV2AsV3OrV3AsV2()
        {
            var v2Context = CreateContextV2(Guid.NewGuid(), 7);
            var v3Context = CreateContextV3(Guid.NewGuid(), 7);
            var v2Json = ContractJson.SerializeV2(v2Context);
            var v3Json = ContractJson.SerializeV3(v3Context);
            var v2ResultJson = ContractJson.SerializeV2(MatchResultV2.Create(
                v2Context,
                v2Context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV2(v2Context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }));
            var v3ResultJson = ContractJson.SerializeV3(MatchResultV3.Create(
                v3Context,
                v3Context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(v3Context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }));

            Assert.That(() => ContractJson.DeserializeContextV3(v2Json), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeContextV2(v3Json), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeResultV3(v2ResultJson), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeResultV2(v3ResultJson), Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV4_CanonicalJsonIsByteStableAndPreservesExplicitRotationOrder()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var expected = ContractJson.SerializeV4(context);

            for (var repetition = 0; repetition < 100; repetition++)
            {
                Assert.That(ContractJson.SerializeV4(context), Is.EqualTo(expected));
            }

            var restored = ContractJson.DeserializeMatchContextV4(expected);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(expected));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(restored.RulesVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.CoefficientVersion, Is.EqualTo(1));
            Assert.That(restored.PhysicsConfigurationHash, Is.EqualTo(PhysicsConfigurationHashV4));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.CacheCapacity,
                Is.EqualTo(128));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.CacheEvictionPolicy,
                Is.EqualTo(TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                Is.EqualTo(4));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                Is.EqualTo(PredictorConfigurationHashV4));
            Assert.That(
                restored.Home.RotationOrder.Select(player => player.PlayerId.Value),
                Is.EqualTo(new[]
                {
                    "blue-player-1",
                    "blue-player-2",
                    "blue-player-3",
                    "blue-player-4",
                    "blue-player-5",
                    "blue-player-6"
                }));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
        }

        [Test]
        public void TrajectoryPredictionProviderConfigurationV4_RequiresBoundedFifoAndVersionedHash()
        {
            var configuration = CreatePredictionProviderConfigurationV4();

            Assert.That(configuration.CacheCapacity, Is.EqualTo(128));
            Assert.That(
                configuration.CacheEvictionPolicy,
                Is.EqualTo(TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut));
            Assert.That(configuration.PredictorVersion, Is.EqualTo(4));
            Assert.That(
                configuration.PredictorConfigurationHash,
                Is.EqualTo(PredictorConfigurationHashV4));
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    0,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    (TrajectoryPredictionCacheEvictionPolicyV4)999,
                    4,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    0,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    "not-a-hash"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void NativeV4ContextHashIncludesEveryPredictionProviderConfigurationField()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");
            var baseline = CreateContextV4(sessionId, 7351);
            var changedCapacity = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    64,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorConfigurationHashV4));
            var changedVersion = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    5,
                    PredictorConfigurationHashV4));
            var changedHash = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    AlternatePredictorConfigurationHashV4));

            Assert.That(changedCapacity.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedVersion.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedHash.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
        }

        [Test]
        public void NativeV4FingerprintsCoverBaseHandSeedAndRulesIdentity()
        {
            var baseline = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var changedBase = CreateContextV4(
                baseline.SessionId,
                baseline.Seed,
                firstPhysical: new PhysicalBaseAttributesV4(1.90f, 2.50f, 0.36f, 0.70f, 0.70f, 0.55f));
            var changedHand = CreateContextV4(
                baseline.SessionId,
                baseline.Seed,
                firstHand: DominantHandV4.Left);
            var changedSeed = CreateContextV4(baseline.SessionId, 7352);

            Assert.That(
                changedBase.Home.RotationOrder[0].Derived.InputFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.InputFingerprint));
            Assert.That(
                changedBase.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.ResultFingerprint));
            Assert.That(
                changedHand.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.ResultFingerprint));
            Assert.That(changedSeed.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedBase.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedHand.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(
                () => MatchContextV4.Create(
                    baseline.SessionId,
                    baseline.Seed,
                    baseline.Home,
                    baseline.Away,
                    baseline.PhysicsConfigurationHash,
                    baseline.TrajectoryPredictionProviderConfiguration,
                    rulesVersion: 4),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("rulesVersion"));
        }

        [Test]
        public void PlayerSnapshotV4_RejectsEveryNonAuthoritativeDerivationConfigAtBoundary()
        {
            var published = MatchAttributeDerivationConfigV4.Version1;
            var reweighted = ReplaceCoefficient(
                published.Coefficients,
                0,
                0.64f).ToArray();
            reweighted = ReplaceCoefficient(reweighted, 1, 0.21f).ToArray();
            var unsupported = new[]
            {
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 2,
                    coefficientVersion: 1,
                    published.Coefficients),
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 2,
                    published.Coefficients),
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 1,
                    reweighted)
            };

            foreach (var config in unsupported)
            {
                Assert.That(
                    () => new PlayerSnapshotV4(
                        new PlayerId("non-authoritative"),
                        "Non Authoritative",
                        1,
                        PlayerPosition.Setter,
                        DominantHandV4.Right,
                        CreateDerivationPhysical(),
                        CreateDerivationTechnical(),
                        config),
                    Throws.TypeOf<ContractValidationException>()
                        .With.Message.Contains("published V1"));
            }
        }

        [Test]
        public void PlayerSnapshotV4_PublishedV1PreservesDerivedFingerprintThroughContextRoundTrip()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351,
                config: MatchAttributeDerivationConfigV4.Version1);
            var expected = context.Home.RotationOrder[0].Derived.ResultFingerprint;

            var restored = ContractJson.DeserializeMatchContextV4(
                ContractJson.SerializeV4(context));

            Assert.That(restored.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.CoefficientVersion, Is.EqualTo(1));
            Assert.That(
                restored.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.EqualTo(expected));
        }

        [Test]
        public void PlayerSnapshotV4_RecomputesDerivationAndRejectsSuppliedFingerprintMismatch()
        {
            var physical = CreateDerivationPhysical();
            var technical = CreateDerivationTechnical();
            var supplied = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(
                () => new PlayerSnapshotV4(
                    new PlayerId("player-one"),
                    "Player One",
                    1,
                    PlayerPosition.Setter,
                    DominantHandV4.Left,
                    physical,
                    technical,
                    MatchAttributeDerivationConfigV4.Version1,
                    supplied),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("fingerprint"));
        }

        [Test]
        public void TeamSnapshotV4_RequiresExactlySixUniquePlayersInRotationOrder()
        {
            var players = CreatePlayersV4("blue");
            var team = new TeamSnapshotV4(new TeamId("team-blue"), "Blue Team", TeamSide.Home, players);

            Assert.That(team.RotationOrder, Has.Count.EqualTo(6));
            Assert.That(team.Players, Is.EqualTo(team.RotationOrder));
            Assert.That(
                () => new TeamSnapshotV4(
                    new TeamId("too-small"),
                    "Too Small",
                    TeamSide.Home,
                    players.Take(5).ToArray()),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("exactly six"));
            Assert.That(
                () => new TeamSnapshotV4(
                    new TeamId("duplicates"),
                    "Duplicates",
                    TeamSide.Home,
                    players.Take(5).Concat(new[] { players[0] }).ToArray()),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("unique"));
        }

        [Test]
        public void MatchContextV4DeserializerRejectsMissingLegacyUnknownAndWrongVersionPayloads()
        {
            var context = CreateContextV4(Guid.NewGuid(), 19);
            var json = ContractJson.SerializeV4(context);
            var missing = json.Replace(
                "\"physicsConfigurationHash\":\"" + PhysicsConfigurationHashV4 + "\",",
                string.Empty);
            var missingPredictionConfiguration = json.Replace(
                "\"trajectoryPredictionProviderConfiguration\":" +
                "{\"cacheCapacity\":128,\"cacheEvictionPolicy\":0,\"predictorVersion\":4," +
                "\"predictorConfigurationHash\":\"" + PredictorConfigurationHashV4 + "\"},",
                string.Empty);
            var unknownPredictionConfigurationField = json.Replace(
                "\"cacheCapacity\":128,",
                "\"cacheCapacity\":128,\"requestingTeam\":0,");
            var legacyAbility = json.Replace("\"derived\":{", "\"ability\":{},\"derived\":{");
            var wrongVersion = json.Replace("\"contractVersion\":4", "\"contractVersion\":3");

            Assert.That(missing, Is.Not.EqualTo(json));
            Assert.That(missingPredictionConfiguration, Is.Not.EqualTo(json));
            Assert.That(unknownPredictionConfigurationField, Is.Not.EqualTo(json));
            Assert.That(legacyAbility, Is.Not.EqualTo(json));
            Assert.That(wrongVersion, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(missing),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(missingPredictionConfiguration),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(unknownPredictionConfigurationField),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(legacyAbility),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(wrongVersion),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(
                    ContractJson.Serialize(CreateContext(Guid.NewGuid(), 19))),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(
                    ContractJson.SerializeV2(CreateContextV2(Guid.NewGuid(), 19))),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(
                    ContractJson.SerializeV3(CreateContextV3(Guid.NewGuid(), 19))),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchResultV4_CanonicalJsonRoundTripsWithAuthoritySummary()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var result = MatchResultV4.Create(
                context,
                context.Home.TeamId,
                homeScore: 15,
                awayScore: 12,
                ralliesPlayed: 27,
                acceptedContacts: 81,
                v3RuleTransitionCount: 81,
                new[]
                {
                    new PlayerMatchStatsV4(
                        context.Home.RotationOrder[0].PlayerId,
                        points: 6,
                        contacts: 12,
                        errors: 1,
                        workload: 16.5f)
                });
            var expected = ContractJson.SerializeV4(result);

            for (var repetition = 0; repetition < 100; repetition++)
            {
                Assert.That(ContractJson.SerializeV4(result), Is.EqualTo(expected));
            }

            var restored = ContractJson.DeserializeMatchResultV4(expected);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(expected));
            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(restored.WinnerTeamId, Is.EqualTo(context.Home.TeamId));
            Assert.That(restored.HomeScore, Is.EqualTo(15));
            Assert.That(restored.AwayScore, Is.EqualTo(12));
            Assert.That(restored.RalliesPlayed, Is.EqualTo(27));
            Assert.That(restored.AcceptedContacts, Is.EqualTo(81));
            Assert.That(restored.V3RuleTransitionCount, Is.EqualTo(81));
            Assert.That(restored.ResultHash, Is.EqualTo(result.ResultHash));
        }

        [Test]
        public void MatchResultV4DeserializerRejectsMissingUnknownWrongVersionAndLegacyJson()
        {
            var context = CreateContextV4(Guid.NewGuid(), 17);
            var result = MatchResultV4.Create(
                context,
                context.Home.TeamId,
                15,
                9,
                24,
                72,
                72,
                Array.Empty<PlayerMatchStatsV4>());
            var json = ContractJson.SerializeV4(result);
            var missing = json.Replace("\"acceptedContacts\":72,", string.Empty);
            var unknown = json.Replace("\"winnerTeamId\":", "\"ability\":{},\"winnerTeamId\":");
            var wrongVersion = json.Replace("\"contractVersion\":4", "\"contractVersion\":3");
            var v1 = CreateContext(Guid.NewGuid(), 17);
            var v1Result = MatchResultV1.Create(
                v1,
                v1.Home.TeamId,
                25,
                18,
                Array.Empty<PlayerMatchStatsV1>());
            var v2 = CreateContextV2(Guid.NewGuid(), 17);
            var v2Result = MatchResultV2.Create(
                v2,
                v2.Home.TeamId,
                25,
                19,
                Array.Empty<PlayerMatchStatsV2>());
            var v3 = CreateContextV3(Guid.NewGuid(), 17);
            var v3Result = MatchResultV3.Create(
                v3,
                v3.Home.TeamId,
                25,
                20,
                Array.Empty<PlayerMatchStatsV3>());

            Assert.That(() => ContractJson.DeserializeMatchResultV4(missing), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeMatchResultV4(unknown), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeMatchResultV4(wrongVersion), Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchResultV4(ContractJson.Serialize(v1Result)),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchResultV4(ContractJson.SerializeV2(v2Result)),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchResultV4(ContractJson.SerializeV3(v3Result)),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchReplayV2_RoundTripsWithFormatVersionAndReservedDiagnostics()
        {
            var replay = MatchReplayV2.Create(
                "replay-001",
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                new[] { "PlanCoverageDecision", "ExecutionEnvelopeV3", "BallTrajectoryArtifactV3" });

            var restored = ContractJson.DeserializeReplayV2(ContractJson.SerializeReplayV2(replay));

            Assert.That(restored.FormatVersion, Is.EqualTo(ContractVersions.ReplayV2));
            Assert.That(restored.ReplayId, Is.EqualTo("replay-001"));
            Assert.That(restored.ContextHash, Is.EqualTo(replay.ContextHash));
            Assert.That(restored.ReservedSections, Does.Contain("PlanCoverageDecision"));
            Assert.That(restored.ReservedSections, Does.Contain("ExecutionEnvelopeV3"));
            Assert.That(restored.ReservedSections, Does.Contain("BallTrajectoryArtifactV3"));
        }

        private static void AssertPhysicalBaseAttributeRejected(int field, float value)
        {
            Assert.That(
                () => CreatePhysicalBaseAttributes(value, field),
                Throws.TypeOf<ContractValidationException>(),
                "Physical field " + field + " must reject " + value + ".");
        }

        private static PhysicalBaseAttributesV4 CreatePhysicalBaseAttributes(float value, int field)
        {
            var values = new[] { 1.80f, 2.35f, 0.5f, 0.5f, 0.5f, 0.5f };
            if (field >= 0)
            {
                values[field] = value;
            }

            return new PhysicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        private static void AssertTechnicalBaseAttributeRejected(int field, float value)
        {
            Assert.That(
                () => CreateTechnicalBaseAttributes(value, field),
                Throws.TypeOf<ContractValidationException>(),
                "Technical field " + field + " must reject " + value + ".");
        }

        private static TechnicalBaseAttributesV4 CreateTechnicalBaseAttributes(float value, int field)
        {
            var values = new[] { value, value, value, value, value, value, value, value, value };
            if (field >= 0)
            {
                values = new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
                values[field] = value;
            }

            return new TechnicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }

        private static PhysicalBaseAttributesV4 CreateDerivationPhysical(
            float heightMeters = 1.90f,
            float standingReachMeters = 2.50f,
            float jump = 0.35f,
            float mobility = 0.70f,
            float reaction = 0.70f,
            float coordination = 0.55f)
        {
            return new PhysicalBaseAttributesV4(
                heightMeters,
                standingReachMeters,
                jump,
                mobility,
                reaction,
                coordination);
        }

        private static TechnicalBaseAttributesV4 CreateDerivationTechnical(
            float delta = 0f,
            int technicalField = -1)
        {
            var values = new[] { 0.70f, 0.75f, 0.65f, 0.60f, 0.65f, 0.70f, 0.65f, 0.60f, 0.60f };
            if (technicalField >= 0)
            {
                values[technicalField] += delta;
            }

            return new TechnicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }

        private static DerivedMatchAttributesV4 DeriveV4(
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical)
        {
            return MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static void AssertDerivedOutputChanges(
            DerivedMatchAttributesV4 baseline,
            DerivedMatchAttributesV4 changed,
            Func<MatchAttributesV4, float> select)
        {
            Assert.That(select(changed.Attributes), Is.Not.EqualTo(select(baseline.Attributes)));
            Assert.That(changed.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
        }

        private static IEnumerable<MatchAttributeCoefficientV4> ReplaceCoefficient(
            IEnumerable<MatchAttributeCoefficientV4> source,
            int index,
            float value)
        {
            return source.Select((coefficient, current) =>
                current == index
                    ? new MatchAttributeCoefficientV4(
                        coefficient.OutputName,
                        coefficient.InputName,
                        value)
                    : coefficient);
        }

        private const string PhysicsConfigurationHashV4 =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private const string PredictorConfigurationHashV4 =
            "123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0";

        private const string AlternatePredictorConfigurationHashV4 =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        private static MatchContextV4 CreateContextV4(
            Guid sessionId,
            int seed,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null,
            TrajectoryPredictionProviderConfigurationV4 predictionConfiguration = null)
        {
            var derivationConfig = config ?? MatchAttributeDerivationConfigV4.Version1;
            return MatchContextV4.Create(
                sessionId,
                seed,
                CreateTeamV4(
                    "team-blue",
                    TeamSide.Home,
                    "blue",
                    firstPhysical,
                    firstHand,
                    derivationConfig),
                CreateTeamV4(
                    "team-orange",
                    TeamSide.Away,
                    "orange",
                    config: derivationConfig),
                PhysicsConfigurationHashV4,
                predictionConfiguration ?? CreatePredictionProviderConfigurationV4(),
                rulesVersion: ContractVersions.MatchV3);
        }

        private static TrajectoryPredictionProviderConfigurationV4
            CreatePredictionProviderConfigurationV4()
        {
            return new TrajectoryPredictionProviderConfigurationV4(
                128,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                4,
                PredictorConfigurationHashV4);
        }

        private static TeamSnapshotV4 CreateTeamV4(
            string teamId,
            TeamSide side,
            string playerPrefix,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null)
        {
            return new TeamSnapshotV4(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                CreatePlayersV4(playerPrefix, firstPhysical, firstHand, config));
        }

        private static PlayerSnapshotV4[] CreatePlayersV4(
            string playerPrefix,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null)
        {
            var derivationConfig = config ?? MatchAttributeDerivationConfigV4.Version1;
            var positions = new[]
            {
                PlayerPosition.Setter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.MiddleBlocker,
                PlayerPosition.Opposite,
                PlayerPosition.Libero,
                PlayerPosition.Defender
            };
            var players = new PlayerSnapshotV4[6];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerSnapshotV4(
                    new PlayerId(playerPrefix + "-player-" + (index + 1)),
                    playerPrefix + " Player " + (index + 1),
                    index + 1,
                    positions[index],
                    index == 0 ? firstHand : DominantHandV4.Right,
                    index == 0 && firstPhysical != null ? firstPhysical : CreateDerivationPhysical(),
                    CreateDerivationTechnical(),
                    derivationConfig);
            }

            return players;
        }

        private static MatchContextV1 CreateContext(Guid sessionId, int seed)
        {
            return MatchContextV1.Create(
                sessionId,
                seed,
                CreateTeam("team-blue", TeamSide.Home, "blue"),
                CreateTeam("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV1 CreateTeam(
            string teamId,
            TeamSide side,
            string playerPrefix,
            PlayerPosition firstPlayerPosition = PlayerPosition.Setter)
        {
            var players = new List<PlayerSnapshotV1>
            {
                CreatePlayer(playerPrefix + "-setter", "Setter", 1, firstPlayerPosition),
                CreatePlayer(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                CreatePlayer(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender)
            };
            return new TeamSnapshotV1(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                players);
        }

        private static PlayerSnapshotV1 CreatePlayer(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position)
        {
            return new PlayerSnapshotV1(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV1(
                    0.81f,
                    0.82f,
                    0.83f,
                    0.84f,
                    0.85f,
                    0.86f,
                    0.87f));
        }

        private static MatchContextV2 CreateContextV2(Guid sessionId, int seed)
        {
            return MatchContextV2.Create(
                sessionId,
                seed,
                CreateTeamV2("team-blue", TeamSide.Home, "blue"),
                CreateTeamV2("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV2 CreateTeamV2(string teamId, TeamSide side, string playerPrefix)
        {
            return new TeamSnapshotV2(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                new[]
                {
                    CreatePlayerV2(playerPrefix + "-setter", "Setter", 1, PlayerPosition.Setter, 3.42f),
                    CreatePlayerV2(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter, 3.42f),
                    CreatePlayerV2(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender, 3.20f)
                });
        }

        private static PlayerSnapshotV2 CreatePlayerV2(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position,
            float maxAttackReach)
        {
            return new PlayerSnapshotV2(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV2(
                    0.81f,
                    0.82f,
                    0.83f,
                    0.84f,
                    0.85f,
                    0.86f,
                    0.87f,
                    maxAttackReach));
        }

        private static MatchContextV3 CreateContextV3(Guid sessionId, int seed)
        {
            return MatchContextV3.Create(
                sessionId,
                seed,
                CreateTeamV3("team-blue", TeamSide.Home, "blue"),
                CreateTeamV3("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV3 CreateTeamV3(
            string teamId,
            TeamSide side,
            string playerPrefix,
            int sourceVersion = ContractVersions.MatchV3,
            int migrationVersion = 0,
            bool isCompatibilityEstimate = false)
        {
            return new TeamSnapshotV3(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                new[]
                {
                    CreatePlayerV3(playerPrefix + "-setter", "Setter", 1, PlayerPosition.Setter, 3.42f, sourceVersion, migrationVersion, isCompatibilityEstimate),
                    CreatePlayerV3(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter, 3.42f, sourceVersion, migrationVersion, isCompatibilityEstimate),
                    CreatePlayerV3(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender, 3.20f, sourceVersion, migrationVersion, isCompatibilityEstimate)
                });
        }

        private static PlayerSnapshotV3 CreatePlayerV3(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position,
            float maxAttackReach,
            int sourceVersion,
            int migrationVersion,
            bool isCompatibilityEstimate)
        {
            return new PlayerSnapshotV3(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV3(
                    0.71f,
                    0.72f,
                    0.73f,
                    maxAttackReach,
                    0.74f,
                    0.75f,
                    0.76f,
                    0.77f,
                    0.78f,
                    0.79f,
                    0.80f,
                    sourceVersion,
                    migrationVersion,
                    isCompatibilityEstimate,
                    isCompatibilityEstimate ? new[] { "attackTechnique" } : new[] { "none" }));
        }
    }
}
