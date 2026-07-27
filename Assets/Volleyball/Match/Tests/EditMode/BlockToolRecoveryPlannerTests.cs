using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class BlockToolRecoveryPlannerTests
    {
        [Test]
        public void Qualify_AcceptsLegalHomeReboundWithNonAttackerExit()
        {
            var result = new BlockToolRecoveryPlanner().Qualify(Fixture.PlayableToolRecovery());

            Assert.That(result.IsQualified, Is.True);
            Assert.That(result.RecoveryActor, Is.Not.EqualTo(result.Attacker));
            Assert.That(result.ReorganizationExit, Is.Not.Null);
            Assert.That(result.ReboundEvidence.TrajectoryArtifactIdentity, Is.EqualTo("trajectory"));
            Assert.That(result.ReboundEvidence.SampleIdentity, Is.EqualTo("sample-17"));
            Assert.That(result.ReboundEvidence.BlockContactIdentity, Is.EqualTo("block-contact-17"));
            Assert.That(result.ToolRecoveryCandidate.EnvelopeIdentity, Is.EqualTo("envelope"));
            Assert.That(result.PlanEnvelopeIdentity, Is.EqualTo(result.ToolRecoveryCandidate.EnvelopeIdentity));
            Assert.That(result.ToolRecoveryCandidate.ReorganizationExitIdentity, Is.EqualTo(result.ReorganizationExit.Identity));
            Assert.That(result.ToolRecoveryCandidate.ExpectedRallyValue, Is.EqualTo(result.Value));
        }

        [TestCase(ToolRecoveryFailure.NoBlockContact)]
        [TestCase(ToolRecoveryFailure.ReboundsAway)]
        [TestCase(ToolRecoveryFailure.NoNonAttackerContinuation)]
        [TestCase(ToolRecoveryFailure.NoRemainingTouch)]
        [TestCase(ToolRecoveryFailure.NoReorganizationExit)]
        public void Qualify_RejectsMissingRequiredLink(ToolRecoveryFailure failure)
        {
            var result = new BlockToolRecoveryPlanner().Qualify(Fixture.InvalidRecovery(failure));

            Assert.That(result.IsQualified, Is.False);
            Assert.That(result.Failure, Is.EqualTo(failure));
        }

        [Test]
        public void AddQualifiedToolRecoveryFallback_UsesSameFallbackPool()
        {
            var candidate = Fixture.ToolCandidate();
            var result = AttackDefensePlanner.AddQualifiedToolRecoveryFallback(
                new[] { Fixture.RollCandidate() }, new BlockToolRecoveryPlanner().Qualify(Fixture.PlayableToolRecovery()), candidate);

            Assert.That(result.Select(value => value.CandidateIdentity), Is.EqualTo(new[] { "roll", "tool" }));
            var evidence = result.Last().ToolRecoveryEvidence;
            Assert.That(evidence, Is.Not.Null);
            Assert.That(evidence.Blocker, Is.EqualTo(new PlayerId("red-blocker-3")));
            Assert.That(evidence.RecoveryActor, Is.EqualTo(new PlayerId("blue-defender-2")));
            Assert.That(evidence.ReboundSide, Is.EqualTo(TeamSide.Home));
            Assert.That(evidence.RemainingTouches, Is.EqualTo(1));
        }

        [TestCase("wrong-trajectory", "envelope", "recovery-exit", .332f)]
        [TestCase("trajectory", "wrong-envelope", "recovery-exit", .332f)]
        [TestCase("trajectory", "envelope", "wrong-exit", .332f)]
        [TestCase("trajectory", "envelope", "recovery-exit", .331f)]
        public void AddQualifiedToolRecoveryFallback_RejectsCandidateWithMismatchedEvidence(
            string trajectory, string envelope, string exit, float value)
        {
            var recovery = new BlockToolRecoveryPlanner().Qualify(Fixture.PlayableToolRecovery());
            var mismatched = Fixture.ToolCandidate("tool", envelope, trajectory, exit, value);

            Assert.That(() => AttackDefensePlanner.AddQualifiedToolRecoveryFallback(
                new[] { Fixture.RollCandidate() }, recovery, mismatched), Throws.ArgumentException);
        }

        [Test]
        public void AddQualifiedToolRecoveryFallback_CannotAppendAnArbitraryCallerCandidate()
        {
            var recovery = new BlockToolRecoveryPlanner().Qualify(Fixture.PlayableToolRecovery());

            Assert.That(() => AttackDefensePlanner.AddQualifiedToolRecoveryFallback(
                new[] { Fixture.RollCandidate() }, recovery, Fixture.ToolCandidate(identity: "unqualified-tool")),
                Throws.ArgumentException);
        }

        private static class Fixture
        {
            private static readonly PlayerId Attacker = new PlayerId("blue-attacker-1");
            private static readonly PlayerId Recovery = new PlayerId("blue-defender-2");

            public static BlockToolRecoveryPlanningRequestV3 PlayableToolRecovery() =>
                new BlockToolRecoveryPlanningRequestV3(Attacker, TeamSide.Home, true, 1, .8f, .75f, .1f,
                    new[] { new ToolRecoveryTeammateV3(Recovery, true, .9f, .8f) },
                    new[] { new ReorganizationExitV3("recovery-exit", Recovery, "Organize") },
                    new ToolRecoveryReboundEvidenceV3("trajectory", "sample-17", new PlayerId("red-blocker-3"), "block-contact-17"),
                    "envelope", ToolCandidate());

            public static BlockToolRecoveryPlanningRequestV3 InvalidRecovery(ToolRecoveryFailure failure)
            {
                var ruleEligible = failure != ToolRecoveryFailure.NoBlockContact;
                var homeRebound = failure == ToolRecoveryFailure.ReboundsAway ? 0f : .75f;
                var teammates = failure == ToolRecoveryFailure.NoNonAttackerContinuation
                    ? new[] { new ToolRecoveryTeammateV3(Attacker, true, .9f, .8f) }
                    : new[] { new ToolRecoveryTeammateV3(Recovery, true, .9f, .8f) };
                var remainingTouches = failure == ToolRecoveryFailure.NoRemainingTouch ? 0 : 1;
                var exits = failure == ToolRecoveryFailure.NoReorganizationExit
                    ? new ReorganizationExitV3[0]
                    : new[] { new ReorganizationExitV3("recovery-exit", Recovery, "Organize") };
                return new BlockToolRecoveryPlanningRequestV3(Attacker, TeamSide.Home, ruleEligible, remainingTouches, .8f, homeRebound, .1f, teammates, exits,
                    new ToolRecoveryReboundEvidenceV3("trajectory", "sample-17", new PlayerId("red-blocker-3"), "block-contact-17"), "envelope", ToolCandidate());
            }

            public static AttackCandidateV3 ToolCandidate(string identity = "tool", string envelope = "envelope", string trajectory = "trajectory", string exit = "recovery-exit", float value = .332f) =>
                Candidate(identity, AttackActionClassV3.BlockToolRecovery, envelope, trajectory, exit, value);
            public static AttackCandidateV3 RollCandidate() => Candidate("roll", AttackActionClassV3.Roll);
            private static AttackCandidateV3 Candidate(string identity, AttackActionClassV3 action, string envelope = "envelope", string trajectory = "trajectory", string exit = "", float value = .4f) =>
                new AttackCandidateV3(identity, Attacker, action, new SimVector3(0f, 2f, 0f), new SimVector3(0f, 0f, 5f),
                    value, 1f, false, string.Empty, envelope, trajectory, exit);
        }
    }
}
