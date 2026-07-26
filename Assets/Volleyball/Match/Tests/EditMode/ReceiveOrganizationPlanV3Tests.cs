using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class ReceiveOrganizationPlanV3Tests
    {
        [Test]
        public void Responsibilities_PreserveDeclaredAuthorityOrderAndOwnCollections()
        {
            var emergency = new List<PlayerId> { Id("home-4"), Id("home-5") };
            var backups = new List<PlayerId> { Id("home-2"), Id("home-3") };
            var value = new ReceiveOrganizationPlanV3(
                TeamSide.Home,
                revision: 4,
                primaryReceiver: Id("home-6"),
                registeredSetter: Id("home-1"),
                emergencyReceivers: emergency,
                backupOrganizers: backups,
                attackPreparation: Id("home-4"),
                organizationTarget: new SimVector3(1.5f, 0f, -1.1f));

            emergency.Reverse();
            backups.Clear();

            Assert.That(value.Revision, Is.EqualTo(4));
            Assert.That(
                value.EmergencyReceivers.Select(id => id.Value),
                Is.EqualTo(new[] { "home-4", "home-5" }));
            Assert.That(
                value.BackupOrganizers.Select(id => id.Value),
                Is.EqualTo(new[] { "home-2", "home-3" }));
            Assert.That(value.RegisteredSetter.Value, Is.EqualTo("home-1"));
            Assert.That(
                ((IList<PlayerId>)value.EmergencyReceivers).IsReadOnly,
                Is.True);
        }

        [Test]
        public void Responsibilities_RejectPrimaryReceiverAsEmergencyReceiver()
        {
            Assert.That(
                () => Responsibilities(
                    primaryReceiver: Id("home-6"),
                    emergencyReceivers: new[]
                    {
                        Id("home-6"),
                        Id("home-5")
                    }),
                Throws.ArgumentException);
        }

        [Test]
        public void TeamPlan_RejectsResponsibilityForPlayerOutsideEligibility()
        {
            var snapshot = CreateSnapshot();
            var responsibility = Responsibilities(
                primaryReceiver: Id("bench-player"));

            Assert.That(
                () => new TeamRallyPlanV3(
                    TeamSide.Home,
                    Assignments("home"),
                    Array.Empty<string>(),
                    snapshot.Eligibility,
                    responsibility),
                Throws.ArgumentException);
        }

        [Test]
        public void Composer_PreservesEnrichedResponsibilityIdentity()
        {
            var snapshot = CreateSnapshot();
            var responsibility = Responsibilities();

            var plan = DeterministicRallyPlanComposerV3.Compose(
                snapshot,
                TeamSide.Home,
                "artifact-1",
                responsibility);

            Assert.That(plan.ReceiveOrganization, Is.SameAs(responsibility));
        }

        private static ReceiveOrganizationPlanV3 Responsibilities(
            PlayerId? primaryReceiver = null,
            IReadOnlyList<PlayerId> emergencyReceivers = null)
        {
            return new ReceiveOrganizationPlanV3(
                TeamSide.Home,
                revision: 4,
                primaryReceiver ?? Id("home-6"),
                Id("home-1"),
                emergencyReceivers ?? new[] { Id("home-4"), Id("home-5") },
                new[] { Id("home-2"), Id("home-3") },
                Id("home-4"),
                new SimVector3(1.5f, 0f, -1.1f));
        }

        private static List<PlayerResponsibilityAssignmentV3> Assignments(
            string prefix)
        {
            var assignments = new List<PlayerResponsibilityAssignmentV3>();
            for (var index = 1; index <= 6; index++)
            {
                assignments.Add(new PlayerResponsibilityAssignmentV3(
                    Id(prefix + "-" + index),
                    RallyPlanTaskV3.Cover,
                    RallyPlanConditionV3.Always,
                    (RallyPlanSpatialClaimV3)index,
                    RallyPlanBranchV3.Primary,
                    1f,
                    index));
            }

            return assignments;
        }

        private static RallyWorldSnapshotV3 CreateSnapshot()
        {
            var players = new List<PlayerWorldSnapshotV3>();
            for (var index = 1; index <= 6; index++)
            {
                players.Add(Player("home-" + index, TeamSide.Home));
                players.Add(Player("away-" + index, TeamSide.Away));
            }

            var homeIds = Enumerable.Range(1, 6)
                .Select(index => Id("home-" + index))
                .ToArray();
            var awayIds = Enumerable.Range(1, 6)
                .Select(index => Id("away-" + index))
                .ToArray();
            var positions = Enumerable.Repeat(PlayerPosition.Setter, 6).ToArray();
            var context = MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("f3c6513f-90f4-4d3d-a90d-d0411bd8bb45"),
                31,
                homeIds,
                positions,
                awayIds,
                positions);
            var eligibility = OnCourtLineupRulesV3.Create(
                context,
                homeIds,
                awayIds,
                homeIds[0],
                awayIds[0],
                Array.Empty<LiberoReplacementV3>());
            return new RallyWorldSnapshotV3(
                new BallWorldSnapshotV3(
                    SimVector3.Zero,
                    SimVector3.Zero,
                    SimVector3.Zero,
                    0.1f,
                    0f),
                players,
                TouchSequenceStateV3.Initial,
                eligibility,
                new CourtConfigurationV3(),
                new AcceptedRuleEventV3(),
                0);
        }

        private static PlayerWorldSnapshotV3 Player(
            string playerId,
            TeamSide side)
        {
            return new PlayerWorldSnapshotV3(
                Id(playerId),
                side,
                PlayerPosition.Setter,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Up,
                "ready",
                RallyCommitmentStateV3.Uncommitted,
                0f);
        }

        private static PlayerId Id(string value)
        {
            return new PlayerId(value);
        }
    }
}
