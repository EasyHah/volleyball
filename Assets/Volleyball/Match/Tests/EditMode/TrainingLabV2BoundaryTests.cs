using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingLabV2BoundaryTests
    {
        [Test]
        public void AttributeOverride_StoresEveryV5FieldAndSupportsClearAndReset()
        {
            var value = new TrainingPlayerAttributeOverrideV2();
            var fields = Enum.GetValues(typeof(TrainingPlayerAttributeFieldV2))
                .Cast<TrainingPlayerAttributeFieldV2>()
                .Where(field => field != TrainingPlayerAttributeFieldV2.DominantHand)
                .ToArray();

            foreach (var field in fields)
                value.Set(field, field == TrainingPlayerAttributeFieldV2.Height
                    ? 2010 : 6789);
            value.SetDominantHand(DominantHandV5.Left);

            Assert.That(value.Strength, Is.EqualTo(6789));
            Assert.That(value.HeightMillimeters, Is.EqualTo(2010));
            Assert.That(value.Jump, Is.EqualTo(6789));
            Assert.That(value.Movement, Is.EqualTo(6789));
            Assert.That(value.Reaction, Is.EqualTo(6789));
            Assert.That(value.Coordination, Is.EqualTo(6789));
            Assert.That(value.Attack, Is.EqualTo(6789));
            Assert.That(value.Defense, Is.EqualTo(6789));
            Assert.That(value.CourtIq, Is.EqualTo(6789));
            Assert.That(value.Block, Is.EqualTo(6789));
            Assert.That(value.Serve, Is.EqualTo(6789));
            Assert.That(value.Setting, Is.EqualTo(6789));
            Assert.That(value.DominantHand, Is.EqualTo(DominantHandV5.Left));

            value.Clear(TrainingPlayerAttributeFieldV2.Attack);
            Assert.That(value.Attack, Is.Null);
            Assert.That(value.Defense, Is.EqualTo(6789));

            value.Reset();
            Assert.That(value.HasAny, Is.False);
            Assert.That(value.DominantHand, Is.Null);
        }

        [Test]
        public void AttributeOverride_RejectsOutOfRangeValues()
        {
            var value = new TrainingPlayerAttributeOverrideV2();
            Assert.That(() => value.Set(TrainingPlayerAttributeFieldV2.Strength, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => value.Set(TrainingPlayerAttributeFieldV2.Serve, 10001),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => value.Set(TrainingPlayerAttributeFieldV2.Height, 1399),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => value.Set(TrainingPlayerAttributeFieldV2.Height, 2301),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => value.SetDominantHand((DominantHandV5)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TeamLocalTransform_AwayEquivalentIsPointRotationNotNetMirror()
        {
            var local = new SimVector3(2.4f, 0f, 6.1f);
            var home = TrainingTeamCourtTransformV1.ToWorld(TeamSide.Home, local);
            var away = TrainingTeamCourtTransformV1.ToWorld(TeamSide.Away, local);

            Assert.That(away, Is.EqualTo(new SimVector3(-home.X, home.Y, -home.Z)));
            Assert.That(away.X, Is.Not.EqualTo(home.X));
            Assert.That(TrainingTeamCourtTransformV1.ToLocal(TeamSide.Home, home),
                Is.EqualTo(local));
            Assert.That(TrainingTeamCourtTransformV1.ToLocal(TeamSide.Away, away),
                Is.EqualTo(local));
        }

        [Test]
        public void VersionGate_RejectsV1WithoutChangingSourceBytes()
        {
            var source = Encoding.UTF8.GetBytes(
                "{\"formatVersion\":1,\"scenarioId\":\"training-v1/legacy\"}");
            var before = source.ToArray();

            var result = TrainingScenarioVersionGateV2.Inspect(source);

            Assert.That(result.IsSupported, Is.False);
            Assert.That(result.FormatVersion, Is.EqualTo(1));
            Assert.That(result.Diagnostic, Does.Contain("不支持"));
            Assert.That(source, Is.EqualTo(before));
        }

        [Test]
        public void BuiltInV2Templates_HaveNativeV5IdentityAndStableHash()
        {
            foreach (var id in TrainingScenarioCatalogV2.ScenarioIds)
            {
                var preset = Resources.Load<TrainingScenarioPresetV2>(
                    "TrainingScenariosV2/" + id);
                Assert.That(preset, Is.Not.Null, id);
                var definition = preset.ToDefinition();
                Assert.That(definition.FormatVersion,
                    Is.EqualTo(TrainingScenarioTemplateV2.CurrentFormatVersion));
                Assert.That(definition.ScenarioId, Does.StartWith("training-v2/"));
                Assert.That(definition.Context.ContractVersion,
                    Is.EqualTo(ContractVersions.MatchV5));
                Assert.That(definition.Context.ContextHash, Is.Not.Empty);
                Assert.That(definition.ContentHash,
                    Is.EqualTo(TrainingScenarioCanonicalizerV2.ComputeTemplateHash(
                        definition.ScenarioId, definition.DisplayName,
                        definition.Context)));
            }
        }

        [Test]
        public void BuiltInV2Templates_CreateTheirNamedMatchSetupSemantics()
        {
            Assert.That(TrainingScenarioCatalogV2.CreateSetup("home-serve")
                .FirstServingSide, Is.EqualTo(TeamSide.Home));
            Assert.That(TrainingScenarioCatalogV2.CreateSetup("away-serve")
                .FirstServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(new MatchSetupEditorV1(TrainingScenarioCatalogV2
                    .CreateSetup("position-fault-home"))
                .EvaluatePositionFaults().Any(value =>
                    value.Side == TeamSide.Home), Is.True);
            Assert.That(new MatchSetupEditorV1(TrainingScenarioCatalogV2
                    .CreateSetup("position-fault-away"))
                .EvaluatePositionFaults().Any(value =>
                    value.Side == TeamSide.Away), Is.True);
            var overridden = TrainingScenarioCatalogV2.CreateSetup(
                "attribute-override");
            Assert.That(overridden.AttributeOverrides[
                overridden.HomeRotation[0]].Attack, Is.EqualTo(9000));
        }

        [Test]
        public void V2ProductionBoundary_HasNoV4TrainingDependencies()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var paths = new[]
            {
                "Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingPlayerAttributeOverrideV2.cs",
                "Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingTeamCourtTransformV1.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioPresetV2.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioCatalogV2.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioCanonicalizerV2.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioVersionGateV2.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabWorkbenchControllerV2.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingRallySimulationControllerV5.cs",
                "Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs"
            };
            var forbidden = new[]
            {
                "MatchContextV4", "DominantHandV4", "PhysicalBaseAttributesV4",
                "TechnicalBaseAttributesV4", "MatchAttributeDerivationV4",
                "InitializeV4", "OnCourtLineupRulesV3", "TrainingRunEvidenceV1",
                "TrainingScenarioDraftV1", "TrainingScenarioRuntimeAdapterV1",
                "TrainingSimulationControllerV1", "TrainingScenarioStartupV1"
            };

            foreach (var path in paths)
            {
                var source = File.ReadAllText(Path.Combine(project, path));
                foreach (var token in forbidden)
                    Assert.That(source, Does.Not.Contain(token), path + ": " + token);
            }
        }

        [Test]
        public void TrainingSetupAndOutcomeTypes_DoNotEnterSharedOrCareerArtifacts()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var roots = new[]
            {
                "Assets/Volleyball/Shared/Runtime",
                "Assets/Volleyball/Career/Runtime"
            };
            var forbidden = new[]
            {
                "MatchSetupDraftV1", "MatchSetupSnapshotV1",
                "TrainingRallyStartV5", "TrainingRallyOutcomeV1",
                "TrainingPlayerAttributeOverrideV2"
            };
            foreach (var root in roots)
            foreach (var path in Directory.GetFiles(Path.Combine(project, root),
                         "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                foreach (var token in forbidden)
                    Assert.That(source, Does.Not.Contain(token), path + ": " + token);
            }
        }
    }
}
