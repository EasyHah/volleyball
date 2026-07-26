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
        }

        private static class Fixture
        {
            private static readonly PlayerId Attacker = new PlayerId("blue-attacker-1");
            private static readonly PlayerId Recovery = new PlayerId("blue-defender-2");

            public static BlockToolRecoveryPlanningRequestV3 PlayableToolRecovery() =>
                new BlockToolRecoveryPlanningRequestV3(Attacker, true, 1, .8f, .75f, .1f,
                    new[] { new ToolRecoveryTeammateV3(Recovery, true, .9f, .8f) },
                    new[] { new ReorganizationExitV3("recovery-exit", Recovery, "Organize") });

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
                return new BlockToolRecoveryPlanningRequestV3(Attacker, ruleEligible, remainingTouches, .8f, homeRebound, .1f, teammates, exits);
            }

            public static AttackCandidateV3 ToolCandidate() => Candidate("tool", AttackActionClassV3.BlockToolRecovery);
            public static AttackCandidateV3 RollCandidate() => Candidate("roll", AttackActionClassV3.Roll);
            private static AttackCandidateV3 Candidate(string identity, AttackActionClassV3 action) =>
                new AttackCandidateV3(identity, Attacker, action, new SimVector3(0f, 2f, 0f), new SimVector3(0f, 0f, 5f),
                    .4f, 1f, false, string.Empty, "envelope", "trajectory");
        }
    }
}
