using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class TeamRallyPlanV3
    {
        public TeamRallyPlanV3(
            TeamSide side,
            IReadOnlyList<PlayerResponsibilityAssignmentV3> assignments,
            IReadOnlyList<string> candidateEvidence,
            OnCourtEligibilitySnapshot eligibility,
            ReceiveOrganizationPlanV3 receiveOrganization = null,
            AttackDefensePlanV3 attackDefense = null)
        {
            Side = PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            if (eligibility == null)
            {
                throw new ArgumentNullException(nameof(eligibility));
            }

            Assignments = new ReadOnlyCollection<PlayerResponsibilityAssignmentV3>(CopyAssignments(side, assignments, eligibility));
            CandidateEvidence = new ReadOnlyCollection<string>(CopyEvidence(candidateEvidence));
            ReceiveOrganization = ValidateReceiveOrganization(
                side,
                receiveOrganization,
                eligibility);
            AttackDefense = ValidateAttackDefense(side, attackDefense, eligibility);
        }

        public TeamSide Side { get; }
        public IReadOnlyList<PlayerResponsibilityAssignmentV3> Assignments { get; }
        public IReadOnlyList<string> CandidateEvidence { get; }
        public ReceiveOrganizationPlanV3 ReceiveOrganization { get; }
        public AttackDefensePlanV3 AttackDefense { get; }

        private static AttackDefensePlanV3 ValidateAttackDefense(
            TeamSide side,
            AttackDefensePlanV3 plan,
            OnCourtEligibilitySnapshot eligibility)
        {
            if (plan == null)
            {
                return null;
            }

            if (plan.AttackingSide != side)
            {
                throw new ArgumentException(
                    "Attack/defense plan side must match the team plan.",
                    nameof(plan));
            }

            ValidatePlanPlayer(side, plan.SetIntent.Organizer, eligibility);
            ValidatePlanPlayer(side, plan.SetIntent.PreparedAttacker, eligibility);
            foreach (var candidate in plan.AttackCandidates)
            {
                ValidatePlanPlayer(side, candidate.Actor, eligibility);
            }

            var defendingSide = side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            foreach (var responsibility in plan.Defense.Responsibilities)
            {
                ValidatePlanPlayer(defendingSide, responsibility.Actor, eligibility);
            }

            foreach (var exit in plan.Defense.ReorganizationExits)
            {
                ValidatePlanPlayer(defendingSide, exit.Actor, eligibility);
            }

            foreach (var exit in plan.ReorganizationExits)
            {
                ValidatePlanPlayer(defendingSide, exit.Actor, eligibility);
            }

            return plan;
        }

        private static ReceiveOrganizationPlanV3 ValidateReceiveOrganization(
            TeamSide side,
            ReceiveOrganizationPlanV3 plan,
            OnCourtEligibilitySnapshot eligibility)
        {
            if (plan == null)
            {
                return null;
            }

            if (plan.Side != side)
            {
                throw new ArgumentException(
                    "Receive/organization plan side must match the team plan.",
                    nameof(plan));
            }

            ValidatePlanPlayer(side, plan.PrimaryReceiver, eligibility);
            ValidatePlanPlayer(side, plan.RegisteredSetter, eligibility);
            ValidatePlanPlayer(side, plan.AttackPreparation, eligibility);
            foreach (var player in plan.EmergencyReceivers)
            {
                ValidatePlanPlayer(side, player, eligibility);
            }

            foreach (var player in plan.BackupOrganizers)
            {
                ValidatePlanPlayer(side, player, eligibility);
            }

            return plan;
        }

        private static void ValidatePlanPlayer(
            TeamSide side,
            PlayerId playerId,
            OnCourtEligibilitySnapshot eligibility)
        {
            try
            {
                if (eligibility.For(playerId).Side != side)
                {
                    throw new ArgumentException(
                        "Receive/organization responsibility must reference an eligible player on the plan side.");
                }
            }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException(
                    "Receive/organization responsibility player is not on court.",
                    nameof(playerId),
                    exception);
            }
        }

        private static PlayerResponsibilityAssignmentV3[] CopyAssignments(
            TeamSide side, IReadOnlyList<PlayerResponsibilityAssignmentV3> assignments, OnCourtEligibilitySnapshot eligibility)
        {
            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            if (assignments.Count != 6)
            {
                throw new ArgumentException("Exactly six assignments are required.", nameof(assignments));
            }

            var copy = new PlayerResponsibilityAssignmentV3[6];
            var playerIds = new HashSet<PlayerId>();
            var ranks = new HashSet<int>();
            var claims = new HashSet<RallyPlanSpatialClaimV3>();
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                if (assignment == null)
                {
                    throw new ArgumentException("Assignments are required.", nameof(assignments));
                }

                var player = eligibility.For(assignment.PlayerId);
                if (player.Side != side || !playerIds.Add(assignment.PlayerId))
                {
                    throw new ArgumentException("Assignments must reference distinct eligible players on the plan side.", nameof(assignments));
                }

                if (!ranks.Add(assignment.Rank) || !claims.Add(assignment.SpatialClaim))
                {
                    throw new ArgumentException("Assignment ranks and spatial claims must be distinct.", nameof(assignments));
                }

                copy[index] = assignment;
            }

            return copy;
        }

        private static string[] CopyEvidence(IReadOnlyList<string> candidateEvidence)
        {
            if (candidateEvidence == null)
            {
                return Array.Empty<string>();
            }

            var copy = new string[candidateEvidence.Count];
            for (var index = 0; index < candidateEvidence.Count; index++)
            {
                copy[index] = PlayerWorldSnapshotV3.RequireText(candidateEvidence[index], nameof(candidateEvidence));
            }

            return copy;
        }
    }
}
