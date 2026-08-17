using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingLabLocalScenarioRepositoryV2Tests
    {
        private readonly List<string> _roots = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots)
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            _roots.Clear();
        }

        [Test]
        public void SaveThenReload_PreservesMatchHashOverridesAndUiSession()
        {
            var repository = Repository();
            var local = Local("local-1");
            var player = local.MatchSetup.HomeRotation[0];
            var attributes = new TrainingPlayerAttributeOverrideV2();
            attributes.Set(TrainingPlayerAttributeFieldV2.Attack, 7654);
            attributes.Set(TrainingPlayerAttributeFieldV2.Height, 2110);
            attributes.SetDominantHand(DominantHandV5.Left);
            local.MatchSetup.AttributeOverrides.Add(player, attributes);

            repository.Save(local);
            var reloaded = new TrainingLabLocalScenarioRepositoryV2(
                repository.Root).Load("local-1");

            Assert.That(reloaded.MatchSetupHash,
                Is.EqualTo(local.MatchSetupHash));
            Assert.That(reloaded.ActiveStep, Is.EqualTo("Positioning"));
            Assert.That(reloaded.ActiveView, Is.EqualTo("Top"));
            Assert.That(reloaded.ActiveTool, Is.EqualTo("MoveBall"));
            Assert.That(reloaded.SelectedObjectId, Is.EqualTo("home-1"));
            Assert.That(reloaded.BookmarksJson, Is.EqualTo("[{\"name\":\"A\"}]"));
            Assert.That(reloaded.MatchSetup.AttributeOverrides[player].Attack,
                Is.EqualTo(7654));
            Assert.That(reloaded.MatchSetup.AttributeOverrides[player]
                .HeightMillimeters, Is.EqualTo(2110));
            Assert.That(reloaded.MatchSetup.AttributeOverrides[player]
                .DominantHand, Is.EqualTo(DominantHandV5.Left));
        }

        [Test]
        public void SaveSameId_AtomicallyOverwritesExistingLocalFile()
        {
            var repository = Repository();
            var local = Local("same-id");
            repository.Save(local);

            local.DisplayName = "Updated";
            new MatchSetupEditorV1(local.MatchSetup).SetBallVelocity(
                new SimVector3(1f, 7f, 8f));
            repository.Save(local);

            var reloaded = repository.Load("same-id");
            Assert.That(reloaded.DisplayName, Is.EqualTo("Updated"));
            Assert.That(reloaded.MatchSetup.BallVelocity,
                Is.EqualTo(new SimVector3(1f, 7f, 8f)));
        }

        [Test]
        public void LoadMalformedJson_ReturnsStableDiagnostic()
        {
            var repository = Repository();
            Directory.CreateDirectory(repository.Root);
            File.WriteAllText(Path.Combine(repository.Root, "broken.json"),
                "{not-json", new UTF8Encoding(false));

            Assert.That(() => repository.Load("broken"),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("格式损坏"));
        }

        [Test]
        public void LoadV1_RejectsWithoutChangingOriginalBytes()
        {
            var repository = Repository();
            Directory.CreateDirectory(repository.Root);
            var path = Path.Combine(repository.Root, "legacy.json");
            var bytes = Encoding.UTF8.GetBytes(
                "{\"formatVersion\":1,\"scenarioId\":\"training-v1/legacy\"}");
            File.WriteAllBytes(path, bytes);

            Assert.That(() => repository.Load("legacy"),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("不支持"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [Test]
        public void ReplacementFailure_RetainsPreviousValidBytes()
        {
            var repository = Repository();
            var local = Local("durable");
            repository.Save(local);
            var path = Path.Combine(repository.Root, "durable.json");
            var before = File.ReadAllBytes(path);
            local.DisplayName = "Must not partially replace";
            var failing = new TrainingLabLocalScenarioRepositoryV2(
                repository.Root, new FailingReplacement());

            Assert.That(() => failing.Save(local), Throws.TypeOf<IOException>());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void DirtyDiscard_RestoresSavedCopyWithoutWriting()
        {
            var repository = Repository();
            var local = Local("discard");
            repository.Save(local);
            var path = Path.Combine(repository.Root, "discard.json");
            var savedBytes = File.ReadAllBytes(path);
            var savedHash = local.MatchSetupHash;
            using var controller = new TrainingScenarioLabController(
                repository, local, true);
            new MatchSetupEditorV1(controller.MatchSetup).SetBallVelocity(
                new SimVector3(2f, 6f, 9f));

            var request = controller.RequestLeaveToHub();
            Assert.That(controller.IsDirty, Is.True);
            Assert.That(request.RequiresDecision, Is.True);
            var resolved = controller.ResolveLeave(
                TrainingLabLeaveDecisionV1.Discard);

            Assert.That(resolved.CanLeave, Is.True);
            Assert.That(controller.IsDirty, Is.False);
            Assert.That(controller.LocalScenario.MatchSetupHash,
                Is.EqualTo(savedHash));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedBytes));
        }

        [Test]
        public void FailedSave_LeavesControllerDirtyAndPending()
        {
            var initial = Repository();
            var local = Local("failed-save");
            initial.Save(local);
            var failing = new TrainingLabLocalScenarioRepositoryV2(
                initial.Root, new FailingReplacement());
            using var controller = new TrainingScenarioLabController(
                failing, local, true);
            new MatchSetupEditorV1(controller.MatchSetup).SetBallVelocity(
                new SimVector3(3f, 5f, 9f));

            Assert.That(controller.RequestSwitch("another").RequiresDecision,
                Is.True);
            Assert.That(() => controller.ResolveLeave(
                    TrainingLabLeaveDecisionV1.Save),
                Throws.TypeOf<IOException>());
            Assert.That(controller.IsDirty, Is.True);
        }

        private TrainingLabLocalScenarioRepositoryV2 Repository()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "training-lab-v2-" + Guid.NewGuid().ToString("N"));
            _roots.Add(root);
            return new TrainingLabLocalScenarioRepositoryV2(root);
        }

        private static TrainingLabLocalScenarioV2 Local(string id)
        {
            return TrainingLabLocalScenarioV2.Create(
                id,
                "Local Scenario",
                MatchSetupDraftV1.CreateDefault(
                    FormalSixVsSixRallyBootstrap
                        .CreateDefaultFormalContextV5(), TeamSide.Home),
                "Positioning",
                "Top",
                "MoveBall",
                "home-1",
                "[{\"name\":\"A\"}]");
        }

        private sealed class FailingReplacement :
            ITrainingLabFileReplacementV2
        {
            public void Replace(string temporaryPath, string destinationPath)
            {
                throw new IOException("Injected replacement failure.");
            }
        }
    }
}
