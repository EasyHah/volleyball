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
        public void PlanSetIntent_SolvesExactPredictedContactFlightAndPositiveAttackArrival()
        {
            var request = Request();
            var intent = new AttackDefensePlanner().PlanSetIntent(request);
            var sample = intent.ExecutionClassification.ExecutableSample;
            var state = new BallState(request.AcceptedPass.Position, sample.Velocity,
                request.AcceptedPass.Radius);
            const float fixedStep = 1f / 120f;
            var steps = (int)System.Math.Round(intent.SetFlightSeconds / fixedStep);

            Assert.That(intent.SetFlightSeconds, Is.GreaterThan(0f));
            Assert.That(intent.AttackReadyArrivalTime,
                Is.GreaterThan(intent.GateHExpectedContactTime));
            Assert.That(sample.Target, Is.EqualTo(intent.Target));
            Assert.That(intent.TrajectoryArtifact.Key.BallStateFingerprint,
                Is.Not.EqualTo(request.PassPrediction.Key.BallStateFingerprint));
            for (var index = 0; index < steps; index++)
                BallIntegrator.Step(state, fixedStep,
                    request.SimulationParameters);
            Assert.That((state.Position - intent.Target).Magnitude,
                Is.LessThan(.0003f));
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
            Assert.That(result.ExecutionEvidence.Where(x => !IsPower(x.Candidate.ActionClass)).All(x => x.ExecutionClassification.ExecutableSample.CandidateCategory == ExecutionCandidateCategoryV4.SoftAction), Is.True);
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
        public void PlanAttack_ASetPowerLine_UsesExactExecutableTrajectoryAcrossNetIntoOpponentCourt()
        {
            var request = Request();
            var planner = new AttackDefensePlanner();
            var intent = planner.PlanSetIntent(request);
            var result = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), request.Players));
            var evidence = result.ExecutionEvidence.Single(value =>
                value.Candidate.ActionClass == AttackActionClassV3.PowerLine);

            Assert.That(evidence.Candidate.IsQualifiedPowerRoute, Is.True);
            Assert.That(evidence.Candidate.LegalSampleRatio, Is.GreaterThanOrEqualTo(.6f));
            Assert.That(evidence.ExecutionClassification.ExecutableSample.Velocity,
                Is.Not.EqualTo(new SimVector3(0f, 1.5f, 2f)));
            Assert.That(evidence.ExecutionClassification.ExecutableSample.Velocity,
                Is.Not.EqualTo(new SimVector3(0f, 1.5f, -2f)));
            Assert.That(CrossesAndLandsOpponentSide(evidence.TrajectoryArtifact, TeamSide.Home), Is.True);
            Assert.That(evidence.Candidate.EnvelopeIdentity,
                Is.EqualTo(evidence.ExecutionClassification.ExecutableEnvelope.Identity));
            Assert.That(evidence.Candidate.TrajectoryArtifactIdentity,
                Is.EqualTo(evidence.TrajectoryArtifact.ArtifactIdentity));
        }

        [Test]
        public void PlanAttack_PoorSetWithInfeasibleContactGeometry_EliminatesPowerBeforeScoring()
        {
            var request = Request();
            var planner = new AttackDefensePlanner();
            var normal = planner.PlanSetIntent(request);
            var poor = new GateISetIntentV3(normal.PlanRevision, normal.SourceSequence,
                normal.Organizer, normal.PreparedAttacker,
                new SimVector3(normal.Target.X, .2f, normal.Target.Z),
                normal.GateHExpectedContactTime, normal.ExecutionClassification,
                normal.TrajectoryArtifact, normal.SetFlightSeconds);
            var result = planner.PlanAttack(new AttackPlanningRequestV3(7, poor, Evidence(poor), request.Players));

            Assert.That(result.Candidates.Where(value => value.ActionClass == AttackActionClassV3.PowerLine ||
                value.ActionClass == AttackActionClassV3.PowerCross ||
                value.ActionClass == AttackActionClassV3.PowerEdge ||
                value.ActionClass == AttackActionClassV3.PowerOverHand),
                Has.All.Matches<AttackCandidateV3>(value => !value.IsQualifiedPowerRoute &&
                    value.EliminationReason == "ContactGeometryInfeasible"));
        }

        [Test]
        public void PlanAttack_LegalSampleRatio_ComesFromTrajectoryRatherThanAttackerDistance()
        {
            var request = Request();
            var planner = new AttackDefensePlanner();
            var intent = planner.PlanSetIntent(request);
            var near = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), request.Players));
            var farPlayers = new[] { new GateITacticalPlayerV3(request.Players[0].Player,
                TeamSide.Home, new SimVector3(30f, 2f, 30f), true,
                request.Players[0].Attributes) };
            var far = planner.PlanAttack(new AttackPlanningRequestV3(7, intent, Evidence(intent), farPlayers));

            var nearLine = near.Candidates.Single(value => value.ActionClass == AttackActionClassV3.PowerLine);
            var farLine = far.Candidates.Single(value => value.ActionClass == AttackActionClassV3.PowerLine);
            Assert.That(farLine.LegalSampleRatio, Is.EqualTo(nearLine.LegalSampleRatio));
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
                new[] { new GateITacticalPlayerV3(new PlayerId("home-attacker"), TeamSide.Home, new SimVector3(0f, 2f, -2f), true, derived) }, derived, artifact);
        }

        private static AcceptedSetEvidenceV3 Evidence(GateISetIntentV3 intent) =>
            new AcceptedSetEvidenceV3(intent.Organizer, intent.ExecutionClassification.ExecutableEnvelope.Identity, intent.TrajectoryArtifact.ArtifactIdentity);

        private static bool CrossesAndLandsOpponentSide(BallTrajectoryPredictionArtifactV4 artifact,
            TeamSide attackingSide)
        {
            var depthSign = attackingSide == TeamSide.Home ? 1f : -1f;
            var crossed = artifact.PredictionSnapshot.Samples.Any(sample => sample.Position.Z * depthSign > 0f && sample.Position.Y > 2.6f);
            var landing = artifact.PredictionSnapshot.GroundLanding;
            return crossed && landing.HasValue && landing.Value.Position.Z * depthSign > 0f &&
                System.Math.Abs(landing.Value.Position.X) < 4.5f;
        }

        private static bool IsPower(AttackActionClassV3 value) =>
            value == AttackActionClassV3.PowerLine || value == AttackActionClassV3.PowerCross ||
            value == AttackActionClassV3.PowerEdge || value == AttackActionClassV3.PowerOverHand;

    }
}
