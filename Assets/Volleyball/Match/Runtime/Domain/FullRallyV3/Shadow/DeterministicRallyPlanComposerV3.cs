using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class DeterministicRallyPlanComposerV3
    {
        public static TeamRallyPlanV3 Compose(RallyWorldSnapshotV3 snapshot, TeamSide side, string trajectoryIdentity)
        {
            return Compose(snapshot, side, trajectoryIdentity, null);
        }

        public static TeamRallyPlanV3 Compose(
            RallyWorldSnapshotV3 snapshot,
            TeamSide side,
            string trajectoryIdentity,
            ReceiveOrganizationPlanV3 receiveOrganization)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            trajectoryIdentity = PlayerWorldSnapshotV3.RequireText(trajectoryIdentity, nameof(trajectoryIdentity));

            // Eligibility is the authoritative on-court roster; stale snapshot players never reach scoring.
            var candidates = snapshot.Eligibility.Players
                .Where(player => player.Side == side)
                .SelectMany(CandidatesFor)
                .OrderByDescending(candidate => candidate.Value)
                .ThenBy(candidate => candidate.PlayerId.Value, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Task)
                .ThenBy(candidate => candidate.Claim)
                .ToArray();

            var assignments = new List<PlayerResponsibilityAssignmentV3>(6);
            var claimedPlayers = new HashSet<PlayerId>();
            var claimedSpaces = new HashSet<RallyPlanSpatialClaimV3>();
            foreach (var candidate in candidates)
            {
                if (!claimedPlayers.Add(candidate.PlayerId) || !claimedSpaces.Add(candidate.Claim))
                {
                    continue;
                }

                assignments.Add(new PlayerResponsibilityAssignmentV3(
                    candidate.PlayerId, candidate.Task, RallyPlanConditionV3.Always, candidate.Claim,
                    RallyPlanBranchV3.Primary, candidate.Value, assignments.Count + 1));
                if (assignments.Count == 6)
                {
                    break;
                }
            }

            if (assignments.Count != 6)
            {
                throw new InvalidOperationException("Eligible candidates cannot form a six-player exclusive beam.");
            }

            return new TeamRallyPlanV3(
                side,
                assignments,
                new[] { "artifact=" + trajectoryIdentity },
                snapshot.Eligibility,
                receiveOrganization);
        }

        public static PlanCoverageDecision EvaluateCoverage(RallyPlanV3 plan, AcceptedRuleEventV3 acceptedEvent)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (acceptedEvent == null)
            {
                throw new ArgumentNullException(nameof(acceptedEvent));
            }

            var revision = plan.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
            switch (acceptedEvent.CoverageReason)
            {
                case PlanCoverageReason.WithinConditionalEnvelope:
                    return CoveredBranchOrDiagnostic(plan, acceptedEvent, revision);
                case PlanCoverageReason.ResponsibleActorChanged:
                    return Decision(PlanCoverageDecisionKind.LocalRevision, revision, acceptedEvent.CoverageReason, 1);
                case PlanCoverageReason.BallEnvelopeExceeded:
                    return Decision(PlanCoverageDecisionKind.ScopedReplan, revision, acceptedEvent.CoverageReason, 2);
                case PlanCoverageReason.EnvelopeExceeded:
                    return Decision(PlanCoverageDecisionKind.GlobalReplan, revision, acceptedEvent.CoverageReason, 3);
                case PlanCoverageReason.RallyEnd:
                    return Decision(PlanCoverageDecisionKind.TerminalNoPlan, revision, acceptedEvent.CoverageReason, 0);
                default:
                    return Decision(PlanCoverageDecisionKind.ScopedReplan, revision, acceptedEvent.CoverageReason, 2);
            }
        }

        private static PlanCoverageDecision Decision(PlanCoverageDecisionKind kind, string revision, PlanCoverageReason reason, int depth)
        {
            return new PlanCoverageDecision(kind, revision, reason, Array.Empty<string>(), depth);
        }

        private static PlanCoverageDecision CoveredBranchOrDiagnostic(
            RallyPlanV3 plan, AcceptedRuleEventV3 acceptedEvent, string revision)
        {
            var branches = plan.HomePlan.Assignments
                .Concat(plan.AwayPlan.Assignments)
                .Where(assignment => assignment.Condition == RallyPlanConditionV3.Always
                    || assignment.Condition == acceptedEvent.ActiveCondition)
                .Select(assignment => (RallyPlanBranchV3?)assignment.Branch)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (branches.Length == 1)
            {
                return new PlanCoverageDecision(
                    PlanCoverageDecisionKind.CoveredActivateBranch,
                    revision,
                    acceptedEvent.CoverageReason,
                    Array.Empty<string>(),
                    0,
                    branches[0]);
            }

            if (branches.Length > 1)
            {
                return new PlanCoverageDecision(
                    PlanCoverageDecisionKind.ScopedReplan,
                    revision,
                    acceptedEvent.CoverageReason,
                    new[] { "condition=" + acceptedEvent.ActiveCondition, "branch=ambiguous" },
                    2);
            }

            return new PlanCoverageDecision(
                PlanCoverageDecisionKind.ScopedReplan,
                revision,
                acceptedEvent.CoverageReason,
                new[] { "condition=" + acceptedEvent.ActiveCondition },
                2);
        }

        private static IEnumerable<Candidate> CandidatesFor(OnCourtPlayerEligibilityV3 eligibility)
        {
            var claim = (RallyPlanSpatialClaimV3)eligibility.RotationPosition;
            yield return new Candidate(eligibility.PlayerId, RallyPlanTaskV3.Cover, claim, 100f);
            yield return new Candidate(eligibility.PlayerId, RallyPlanTaskV3.Defend, claim, 90f);
            if (eligibility.RegisteredPosition != PlayerPosition.Libero)
            {
                yield return new Candidate(eligibility.PlayerId, RallyPlanTaskV3.Set, claim, 80f);
            }

            if (eligibility.CanAttackAboveNetFromFrontZone)
            {
                yield return new Candidate(eligibility.PlayerId, RallyPlanTaskV3.Attack, claim, 110f);
            }

            if (eligibility.CanBlock)
            {
                yield return new Candidate(eligibility.PlayerId, RallyPlanTaskV3.Block, claim, 105f);
            }
        }

        private readonly struct Candidate
        {
            public Candidate(PlayerId playerId, RallyPlanTaskV3 task, RallyPlanSpatialClaimV3 claim, float value)
            {
                PlayerId = playerId;
                Task = task;
                Claim = claim;
                Value = value;
            }

            public PlayerId PlayerId { get; }
            public RallyPlanTaskV3 Task { get; }
            public RallyPlanSpatialClaimV3 Claim { get; }
            public float Value { get; }
        }
    }
}
