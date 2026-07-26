using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class AttackDefensePlannerTests
    {
        [Test]
        public void Result_ExposesPostSetCandidateAndThreatFactsWithoutIncompletePlan()
        {
            var names = typeof(AttackPlanningResultV3).GetProperties().Select(value => value.Name).ToArray();

            Assert.That(names, Does.Contain("Candidates"));
            Assert.That(names, Does.Contain("QualifiedPowerRoutes"));
            Assert.That(names, Does.Contain("FallbackCandidates"));
            Assert.That(names, Does.Contain("PublicThreat"));
            Assert.That(names, Does.Not.Contain("Plan"));
        }

        [Test]
        public void Planner_HasNoSetContactCommandSurface()
        {
            var names = typeof(AttackDefensePlanner).GetMethods().Select(value => value.Name);
            Assert.That(names, Has.None.Matches<string>(value =>
                value.Contains("ScheduleSet") || value.Contains("CommandSet") || value.Contains("ExecuteSet")));
        }

        [Test]
        public void PlanningRequests_ExposeFactsButNoLegacyTargetOrCandidateInjectionSurface()
        {
            var setNames = typeof(SetIntentPlanningRequestV3).GetProperties().Select(x => x.Name).ToArray();
            var attackNames = typeof(AttackPlanningRequestV3).GetProperties().Select(x => x.Name).ToArray();

            Assert.That(setNames, Does.Not.Contain("Target"));
            Assert.That(setNames, Does.Not.Contain("PreparedAttacker"));
            Assert.That(setNames, Does.Not.Contain("ExecutionClassification"));
            Assert.That(setNames, Does.Not.Contain("TrajectoryArtifact"));
            Assert.That(attackNames, Does.Not.Contain("Candidates"));
            Assert.That(typeof(AttackDefensePlanner).Assembly.GetReferencedAssemblies().Select(x => x.Name), Does.Not.Contain("Volleyball.Presentation"));
        }

        [Test]
        public void PlanAttack_ReliableASetKeepsPowerAndAllFallbackPoolClasses()
        {
            var request = Request(); var planner = new AttackDefensePlanner(); var intent = planner.PlanSetIntent(request);
            var result = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), request.Players));

            Assert.That(result.QualifiedPowerRoutes, Is.Not.Empty);
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.Tip));
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.Roll));
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.Push));
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.HighSurvival));
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.BlockOut));
            Assert.That(result.Candidates.Select(x => x.ActionClass), Does.Contain(AttackActionClassV3.BlockToolRecovery));
            Assert.That(result.FallbackCandidates.Select(x => x.ActionClass), Has.None.EqualTo(AttackActionClassV3.BlockToolRecovery));
            Assert.That(result.PublicThreat.Entries.All(x => x.ArrivalTime > intent.GateHExpectedContactTime), Is.True);
            Assert.That(result.ExecutionEvidence.All(x => x.Candidate.EnvelopeIdentity == x.ExecutionClassification.ExecutableEnvelope.Identity && x.Candidate.TrajectoryArtifactIdentity == x.TrajectoryArtifact.ArtifactIdentity), Is.True);
            Assert.That(result.ExecutionEvidence.Where(x => x.Candidate.IsQualifiedPowerRoute).All(x => x.ExecutionClassification.ExecutableSample.CandidateCategory == ExecutionCandidateCategoryV4.Attack), Is.True);
            Assert.That(result.ExecutionEvidence.Where(x => !x.Candidate.IsQualifiedPowerRoute).All(x => x.ExecutionClassification.ExecutableSample.CandidateCategory == ExecutionCandidateCategoryV4.SoftAction), Is.True);
            Assert.That(result.ExecutionEvidence.Select(x => x.TrajectoryArtifact.ArtifactIdentity).Distinct().Count(), Is.EqualTo(result.ExecutionEvidence.Count));
        }

        [Test]
        public void PlanAttack_PoorSetEliminatesPowerBeforeScoringAndRejectsMismatchedEvidence()
        {
            var request = Request(); var planner = new AttackDefensePlanner(); var intent = planner.PlanSetIntent(request);
            var distant = new[] { new GateITacticalPlayerV3(request.Players[0].Player, TeamSide.Home, new SimVector3(20f, 2f, 20f), true, request.Players[0].Attributes) };

            var poor = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), distant));
            Assert.That(poor.QualifiedPowerRoutes, Is.Empty);
            Assert.That(() => planner.PlanAttack(new AttackPlanningRequestV3(7, intent,
                new AcceptedSetEvidenceV3(intent.Organizer, "wrong-envelope", intent.TrajectoryArtifact.ArtifactIdentity), request.Players)), Throws.ArgumentException);
        }

        [Test]
        public void PlanAttack_IsRepeatDeterministic()
        {
            var request = Request(); var planner = new AttackDefensePlanner(); var intent = planner.PlanSetIntent(request);
            var first = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), request.Players));
            var second = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), request.Players));

            Assert.That(second.Candidates.Select(x => x.CandidateIdentity), Is.EqualTo(first.Candidates.Select(x => x.CandidateIdentity)));
            Assert.That(second.PublicThreat.Entries.Select(x => x.ArrivalTime), Is.EqualTo(first.PublicThreat.Entries.Select(x => x.ArrivalTime)));
        }

        private static SetIntentPlanningRequestV3 Request()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var context = MatchV4TestFixture.CreateContext();
            var envelope = ExecutionEnvelopeFactoryV4.Create(derived, new ExecutionIntentV4("planner-test", ExecutionCandidateCategoryV4.Set,
                new SimVector3(0f, 2f, 1f), new SimVector3(0f, 2f, 1f), .5f), "planner-test", ExecutionEnvelopePolicyV4.GateI);
            var artifact = Volleyball.Presentation.PhysicalMatchRallyDirector.CreateTrajectoryPredictionProviderV4(context).Predict(
                new BallTrajectoryPredictionRequestV4(TeamSide.Home, 7, new BallState(new SimVector3(0f, 3f, -2f), new SimVector3(0f, 4f, 1f), .12f),
                    new BallSimulationParameters(-9.8f, .9995f), context.PhysicsConfigurationHash, "planner-test", context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                    context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash, envelope.Identity, ExecutionDegradationStepV4.FullSampling));
            return new SetIntentPlanningRequestV3(7, 1, TeamSide.Home, new PlayerId("home-setter"), 1f,
                new BallState(new SimVector3(0f, 3f, -2f), new SimVector3(0f, 4f, 1f), .12f),
                new[] { new GateITacticalPlayerV3(new PlayerId("home-attacker"), TeamSide.Home, new SimVector3(0f, 2f, 1f), true, derived) }, derived, artifact);
        }

        private static AcceptedSetEvidenceV3 Evidence(GateISetIntentV3 intent) =>
            new AcceptedSetEvidenceV3(intent.Organizer, intent.ExecutionClassification.ExecutableEnvelope.Identity, intent.TrajectoryArtifact.ArtifactIdentity);
    }
}
