using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingScenarioV1Tests
    {
        [Test]
        public void Build_SameCompleteInputProducesStableCanonicalBytesAndHash()
        {
            var first = TrainingScenarioValidatorV1.Build(CreateValidDraft());
            var second = TrainingScenarioValidatorV1.Build(CreateValidDraft());

            CollectionAssert.AreEqual(
                TrainingScenarioCanonicalizerV1.ToCanonicalBytes(first),
                TrainingScenarioCanonicalizerV1.ToCanonicalBytes(second));
            Assert.That(first.ContentHash, Is.EqualTo(second.ContentHash));
            Assert.That(first.Players, Has.Count.EqualTo(12));
            Assert.That(first.StartState.TouchSequence.CountedHits, Is.Zero);
        }

        [Test]
        public void Build_CanonicallySortsPlayerPosesByStableId()
        {
            var forward = CreateValidDraft();
            var reverse = forward.DeepCopy();
            reverse.Players.Reverse();

            var first = TrainingScenarioValidatorV1.Build(forward);
            var second = TrainingScenarioValidatorV1.Build(reverse);

            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
            Assert.That(
                second.Players.Select(player => player.PlayerId.Value),
                Is.Ordered.Using<string>(StringComparer.Ordinal));
        }

        [Test]
        public void Build_CompleteInputChangesAlterContentHash()
        {
            var baseline = TrainingScenarioValidatorV1.Build(CreateValidDraft());
            var variants = new[]
            {
                Change(CreateValidDraft(), draft =>
                    draft.Players[0].Position += new SimVector3(.05f, 0f, 0f)),
                Change(CreateValidDraft(), draft =>
                    draft.BallPosition += new SimVector3(.05f, 0f, 0f)),
                Change(CreateValidDraft(), draft =>
                    draft.BallVelocity += new SimVector3(0f, .05f, 0f)),
                Change(CreateValidDraft(), draft =>
                    draft.HomeTactics.SetterX += .03f),
                Change(CreateValidDraft(), draft =>
                    draft.HomeTactics.SetterX += .05f),
                Change(CreateValidDraft(), draft =>
                    draft.Context = MatchContextV4.Create(
                        draft.Context.SessionId,
                        draft.Context.Seed + 1,
                        draft.Context.Home,
                        draft.Context.Away,
                        draft.Context.PhysicsConfigurationHash,
                        draft.Context.TrajectoryPredictionProviderConfiguration,
                        draft.Context.RulesVersion)),
                Change(CreateValidDraft(), draft => draft.AttributeOverrides.Add(
                    draft.Context.Home.Players[0].PlayerId,
                    new TrainingPlayerAttributeOverrideV1(2050,
                        DominantHandV4.Left,
                        draft.Context.Home.Players[0].Physical,
                        draft.Context.Home.Players[0].Technical))),
                Change(CreateValidDraft(), draft => draft.CameraBookmarks.Add(
                    new TrainingCameraBookmarkV1("hash-camera",
                        new SimVector3(1f, 8f, -12f),
                        new SimVector3(0f, -.3f, 1f), 8f, false)))
            };

            foreach (var variant in variants)
            {
                Assert.That(
                    TrainingScenarioValidatorV1.Build(variant).ContentHash,
                    Is.Not.EqualTo(baseline.ContentHash));
            }
        }

        [Test]
        public void Build_RejectsTamperedSuppliedHashAndUnknownFormat()
        {
            var draft = CreateValidDraft();
            var scenario = TrainingScenarioValidatorV1.Build(draft);

            Assert.That(
                () => TrainingScenarioValidatorV1.Build(
                    draft,
                    new string('0', scenario.ContentHash.Length)),
                Throws.ArgumentException);

            draft.FormatVersion = 999;
            var result = TrainingScenarioValidatorV1.Validate(draft);
            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Does.Contain(TrainingScenarioIssueCodesV1.UnsupportedFormat));
        }

        [Test]
        public void Contract_IsPresentationOwnedAndHasNoOutcomeOrAngularVelocitySurface()
        {
            Assert.That(
                typeof(TrainingScenarioV1).Assembly.GetName().Name,
                Is.EqualTo("Volleyball.Match.Presentation"));
            var names = typeof(TrainingScenarioV1)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(names, Does.Not.Contain("AngularVelocity"));
            Assert.That(names, Does.Not.Contain("ExpectedResult"));
            Assert.That(names, Does.Not.Contain("ForcedSuccess"));
            Assert.That(names, Does.Not.Contain("Score"));
            Assert.That(names, Does.Not.Contain("CountedHits"));
            Assert.That(names, Does.Not.Contain("ContactWindow"));
        }

        [Test]
        public void RuntimeAdapter_AppliesScenarioPrivateAttributeOverrideOnlyToTrainingAgents()
        {
            var draft = CreateValidDraft();
            var player = draft.Context.Home.Players[0];
            var attributeOverride = new TrainingPlayerAttributeOverrideV1(
                2050,
                DominantHandV4.Left,
                player.Physical,
                player.Technical);
            draft.AttributeOverrides.Add(player.PlayerId, attributeOverride);
            var scenario = TrainingScenarioValidatorV1.Build(draft);
            var objects = new System.Collections.Generic.List<GameObject>();

            try
            {
                var homeIds = draft.Context.Home.Players
                    .Select(value => value.PlayerId)
                    .ToHashSet();
                var agents = scenario.Players.Select((snapshot, index) =>
                {
                    var gameObject = new GameObject("training-agent-" + index);
                    objects.Add(gameObject);
                    var agent = gameObject.AddComponent<PrototypePlayerAgent>();
                    agent.Initialize(new Volleyball.Domain.Prototype.PlayerId(
                            homeIds.Contains(snapshot.PlayerId)
                                ? Volleyball.Domain.Prototype.TeamId.Blue
                                : Volleyball.Domain.Prototype.TeamId.Orange,
                            Volleyball.Domain.Prototype.PlayerRole.Defender,
                            index),
                        snapshot.PlayerId, Color.white, index.ToString());
                    agent.SetAbility(PlayerAbilityProfile.Default);
                    return agent;
                }).ToArray();

                new TrainingScenarioRuntimeAdapterV1(scenario).ApplyPlayerSnapshots(agents);

                var runtime = agents.Single(agent => agent.StableId.Equals(player.PlayerId));
                var expectedPhysical = new PhysicalBaseAttributesV4(
                    2.05f,
                    2.05f + (player.Physical.StandingReachMeters - player.Physical.HeightMeters),
                    player.Physical.Jump,
                    player.Physical.Mobility,
                    player.Physical.Reaction,
                    player.Physical.Coordination);
                var expected = MatchAttributeDerivationV4.Derive(
                    expectedPhysical,
                    player.Technical,
                    DominantHandV4.Left,
                    MatchAttributeDerivationConfigV4.Version1);
                Assert.That(runtime.Ability.Derived.InputFingerprint,
                    Is.EqualTo(expected.InputFingerprint));
                Assert.That(runtime.Ability.Derived.ResultFingerprint,
                    Is.EqualTo(expected.ResultFingerprint));
            }
            finally
            {
                foreach (var gameObject in objects)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ProjectPresets_MatchCatalogDefinitionsAndHashes()
        {
            var hashes = TrainingScenarioCatalogV1.ScenarioIds
                .Select(id =>
                {
                    var preset = Resources.Load<TrainingScenarioPresetV1>(
                        "TrainingScenariosV1/" + id);
                    Assert.That(preset, Is.Not.Null, id);
                    var asset = preset.ToDefinition();
                    var catalog = TrainingScenarioCatalogV1.Create(id);
                    Assert.That(
                        asset.ScenarioId,
                        Is.EqualTo(catalog.ScenarioId),
                        id);
                    Assert.That(
                        asset.ContentHash,
                        Is.EqualTo(catalog.ContentHash),
                        id);
                    return asset.ContentHash;
                })
                .ToArray();

            Assert.That(
                hashes.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(TrainingScenarioCatalogV1.ScenarioIds.Count));
        }

        internal static TrainingScenarioDraftV1 CreateValidDraft()
        {
            var context = MatchV4TestFixture.CreateContext(
                Guid.Parse("a39fa0a3-53c7-45e2-904a-c600bb971f6c"),
                18427);
            var draft = new TrainingScenarioDraftV1
            {
                ScenarioId = "training-v1/unit-contract",
                DisplayName = "Unit Contract",
                Source = "test",
                FormatVersion = TrainingScenarioV1.FormatVersion,
                Context = context,
                FirstServingSide = TeamSide.Away,
                HomeInitialRotationOffset = 0,
                AwayInitialRotationOffset = 0,
                HomeTactics = CreateTactics(-1f),
                AwayTactics = CreateTactics(1f),
                Ai = new FormalMatchAiInputV4(),
                BallPosition = new SimVector3(0f, 2.1f, 9.2f),
                BallVelocity = new SimVector3(.1f, 2.5f, -8.5f),
                StartRecipe = RallyStartRecipeV3.ServeFlight,
                SourceTeam = TeamSide.Away,
                LastLegalActor = null,
                AccessLevel = TrainingScenarioAccessLevelV1.Developer
            };

            AddTeamPoses(draft, context.Home, -1f);
            AddTeamPoses(draft, context.Away, 1f);
            draft.HomeRotation.AddRange(context.Home.RotationOrder.Select(value => value.PlayerId));
            draft.AwayRotation.AddRange(context.Away.RotationOrder.Select(value => value.PlayerId));
            draft.RotationLocked = true;
            return draft;
        }

        private static void AddTeamPoses(
            TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 team,
            float sideSign)
        {
            for (var index = 0; index < team.Players.Count; index++)
            {
                var position = PhysicalMatchConfiguration.FormalIndoorSixVsSix
                    .PositionFor(team.Side, index + 1);
                draft.Players.Add(new TrainingPlayerPoseDraftV1
                {
                    PlayerId = team.Players[index].PlayerId,
                    Position = new SimVector3(position.x, 0f, position.z),
                    Forward = new SimVector3(0f, 0f, -sideSign),
                    Pose = StickFigurePose.Ready
                });
            }
        }

        private static FormalMatchTacticInputV4 CreateTactics(float sideSign)
        {
            return new FormalMatchTacticInputV4
            {
                SetterX = 0f,
                SetterZ = 2.5f * sideSign,
                AttackerX = 2.4f,
                AttackerZ = 1.8f * sideSign,
                DefenderX = -2.2f,
                DefenderZ = 5.4f * sideSign,
                Blocker = Volleyball.Domain.Prototype.PlayerRole.OutsideHitter,
                BlockX = 2.2f,
                BlockZ = .55f * sideSign,
                CoverReceiver = Volleyball.Domain.Prototype.PlayerRole.Setter,
                CoverX = 0f,
                CoverZ = 1.6f * sideSign,
                AttackFlightSeconds = .45f
            };
        }

        private static TrainingScenarioDraftV1 Change(
            TrainingScenarioDraftV1 draft,
            Action<TrainingScenarioDraftV1> change)
        {
            change(draft);
            return draft;
        }
    }
}
