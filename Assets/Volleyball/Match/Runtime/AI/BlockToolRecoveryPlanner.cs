using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.AI
{
    public enum ToolRecoveryFailure
    {
        None,
        NoBlockContact,
        ReboundsAway,
        NoNonAttackerContinuation,
        NoRemainingTouch,
        NoReorganizationExit
    }

    // The rebound inputs are shared trajectory-sample facts.  This planner only
    // qualifies a continuation; it neither simulates nor commands a contact.
    public readonly struct ToolRecoveryTeammateV3
    {
        public ToolRecoveryTeammateV3(PlayerId actor, bool isOnCourt, float reachProbability, float controlMargin)
        {
            if (!FiniteUnit(reachProbability) || !FiniteUnit(controlMargin)) throw new ArgumentOutOfRangeException(nameof(reachProbability));
            Actor = actor; IsOnCourt = isOnCourt; ReachProbability = reachProbability; ControlMargin = controlMargin;
        }
        public PlayerId Actor { get; } public bool IsOnCourt { get; }
        public float ReachProbability { get; } public float ControlMargin { get; }
        private static bool FiniteUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }

    public sealed class BlockToolRecoveryPlanningRequestV3
    {
        public BlockToolRecoveryPlanningRequestV3(PlayerId attacker, bool actualRuleEligibleForBlockContact,
            int remainingTouches, float blockContactProbability, float homeReboundProbability, float immediateLossRisk,
            IReadOnlyList<ToolRecoveryTeammateV3> teammates, IReadOnlyList<ReorganizationExitV3> reorganizationExits)
        {
            if (remainingTouches < 0) throw new ArgumentOutOfRangeException(nameof(remainingTouches));
            if (!FiniteUnit(blockContactProbability) || !FiniteUnit(homeReboundProbability) || !FiniteUnit(immediateLossRisk))
                throw new ArgumentOutOfRangeException(nameof(blockContactProbability));
            if (teammates == null) throw new ArgumentNullException(nameof(teammates));
            if (reorganizationExits == null) throw new ArgumentNullException(nameof(reorganizationExits));
            Attacker = attacker; ActualRuleEligibleForBlockContact = actualRuleEligibleForBlockContact; RemainingTouches = remainingTouches;
            BlockContactProbability = blockContactProbability; HomeReboundProbability = homeReboundProbability; ImmediateLossRisk = immediateLossRisk;
            Teammates = new ReadOnlyCollection<ToolRecoveryTeammateV3>(teammates.ToArray());
            ReorganizationExits = new ReadOnlyCollection<ReorganizationExitV3>(reorganizationExits.Select(value => value ?? throw new ArgumentException("Exits cannot contain null.", nameof(reorganizationExits))).ToArray());
        }
        public PlayerId Attacker { get; } public bool ActualRuleEligibleForBlockContact { get; } public int RemainingTouches { get; }
        public float BlockContactProbability { get; } public float HomeReboundProbability { get; } public float ImmediateLossRisk { get; }
        public IReadOnlyList<ToolRecoveryTeammateV3> Teammates { get; } public IReadOnlyList<ReorganizationExitV3> ReorganizationExits { get; }
        private static bool FiniteUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }

    public sealed class BlockToolRecoveryResultV3
    {
        internal BlockToolRecoveryResultV3(bool isQualified, ToolRecoveryFailure failure, PlayerId attacker, PlayerId? recoveryActor,
            float blockContactProbability, float homeReboundProbability, float teammateReachProbability,
            float continuationQuality, float immediateLossRisk, ReorganizationExitV3 reorganizationExit)
        {
            IsQualified = isQualified; Failure = failure; Attacker = attacker; RecoveryActor = recoveryActor;
            BlockContactProbability = blockContactProbability; HomeReboundProbability = homeReboundProbability;
            TeammateReachProbability = teammateReachProbability; ContinuationQuality = continuationQuality;
            ImmediateLossRisk = immediateLossRisk; ReorganizationExit = reorganizationExit;
        }
        public bool IsQualified { get; } public ToolRecoveryFailure Failure { get; } public PlayerId Attacker { get; }
        public PlayerId? RecoveryActor { get; } public float BlockContactProbability { get; } public float HomeReboundProbability { get; }
        public float TeammateReachProbability { get; } public float ContinuationQuality { get; } public float ImmediateLossRisk { get; }
        public ReorganizationExitV3 ReorganizationExit { get; }
        public float Value => BlockContactProbability * HomeReboundProbability * TeammateReachProbability * ContinuationQuality - ImmediateLossRisk;
    }

    public sealed class BlockToolRecoveryPlanner
    {
        public BlockToolRecoveryResultV3 Qualify(BlockToolRecoveryPlanningRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.ActualRuleEligibleForBlockContact || request.BlockContactProbability <= 0f)
                return Failure(request, ToolRecoveryFailure.NoBlockContact, null, 0f, 0f, null);
            if (request.HomeReboundProbability <= 0f)
                return Failure(request, ToolRecoveryFailure.ReboundsAway, null, 0f, 0f, null);

            var recoveryCandidates = request.Teammates.Where(value => value.IsOnCourt && !value.Actor.Equals(request.Attacker) &&
                    value.ReachProbability > 0f && value.ControlMargin > 0f)
                .OrderByDescending(value => value.ReachProbability * value.ControlMargin)
                .ThenBy(value => value.Actor.ToString(), StringComparer.Ordinal).ToArray();
            if (recoveryCandidates.Length == 0)
                return Failure(request, ToolRecoveryFailure.NoNonAttackerContinuation, null, 0f, 0f, null);
            var recovery = recoveryCandidates[0];
            if (request.RemainingTouches <= 0)
                return Failure(request, ToolRecoveryFailure.NoRemainingTouch, recovery.Actor, recovery.ReachProbability, recovery.ControlMargin, null);

            var exit = request.ReorganizationExits.OrderBy(value => value.Identity, StringComparer.Ordinal)
                .FirstOrDefault(value => value.Actor.Equals(recovery.Actor));
            if (exit == null)
                return Failure(request, ToolRecoveryFailure.NoReorganizationExit, recovery.Actor, recovery.ReachProbability, recovery.ControlMargin, null);
            return new BlockToolRecoveryResultV3(true, ToolRecoveryFailure.None, request.Attacker, recovery.Actor,
                request.BlockContactProbability, request.HomeReboundProbability, recovery.ReachProbability,
                recovery.ControlMargin, request.ImmediateLossRisk, exit);
        }

        private static BlockToolRecoveryResultV3 Failure(BlockToolRecoveryPlanningRequestV3 request, ToolRecoveryFailure failure,
            PlayerId? recoveryActor, float reachProbability, float continuationQuality, ReorganizationExitV3 exit) =>
            new BlockToolRecoveryResultV3(false, failure, request.Attacker, recoveryActor, request.BlockContactProbability,
                request.HomeReboundProbability, reachProbability, continuationQuality, request.ImmediateLossRisk, exit);
    }
}
