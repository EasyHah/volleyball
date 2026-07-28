using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class AttackAttemptFactsV3
    {
        public AttackAttemptFactsV3(
            OnCourtPlayerEligibilityV3 player,
            SimVector3 takeoffPoint,
            SimVector3 contactPoint,
            float attackLineDistanceFromCenter,
            float netHeight)
        {
            Player = RequirePlayer(player);
            TakeoffPoint = RequireFinite(takeoffPoint, nameof(takeoffPoint));
            ContactPoint = RequireFinite(contactPoint, nameof(contactPoint));
            AttackLineDistanceFromCenter = RequirePositiveFinite(
                attackLineDistanceFromCenter, nameof(attackLineDistanceFromCenter));
            NetHeight = RequirePositiveFinite(netHeight, nameof(netHeight));
        }

        public OnCourtPlayerEligibilityV3 Player { get; }

        public SimVector3 TakeoffPoint { get; }

        public SimVector3 ContactPoint { get; }

        public float AttackLineDistanceFromCenter { get; }

        public float NetHeight { get; }

        public bool IsContactAboveNet => ContactPoint.Y > NetHeight;

        public bool IsTakeoffInFrontZone => Player.Side == Volleyball.Shared.Contracts.TeamSide.Home
            ? TakeoffPoint.Z >= -AttackLineDistanceFromCenter && TakeoffPoint.Z <= 0f
            : TakeoffPoint.Z <= AttackLineDistanceFromCenter && TakeoffPoint.Z >= 0f;

        internal static OnCourtPlayerEligibilityV3 RequirePlayer(OnCourtPlayerEligibilityV3 player)
        {
            return player ?? throw new ArgumentNullException(nameof(player));
        }

        internal static SimVector3 RequireFinite(SimVector3 value, string parameterName)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float RequirePositiveFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public sealed class BlockAttemptFactsV3
    {
        public BlockAttemptFactsV3(OnCourtPlayerEligibilityV3 player)
        {
            Player = AttackAttemptFactsV3.RequirePlayer(player);
        }

        public OnCourtPlayerEligibilityV3 Player { get; }
    }

    public sealed class ActionEligibilityDecisionV3
    {
        internal ActionEligibilityDecisionV3(bool isEligible, string reason)
        {
            IsEligible = isEligible;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public bool IsEligible { get; }

        public string Reason { get; }
    }

    public static class AttackEligibilityRulesV3
    {
        public static ActionEligibilityDecisionV3 CanAttempt(
            OnCourtPlayerEligibilityV3 player,
            AttackGeometryFactV3 geometry)
        {
            var eligiblePlayer = AttackAttemptFactsV3.RequirePlayer(player);
            var observedGeometry =
                geometry ?? throw new ArgumentNullException(nameof(geometry));
            if (!eligiblePlayer.PlayerId.Equals(observedGeometry.Actor))
            {
                throw new ArgumentException(
                    "Attack geometry actor must match the eligible player.",
                    nameof(geometry));
            }
            if (eligiblePlayer.Side != observedGeometry.Side)
            {
                throw new ArgumentException(
                    "Attack geometry side must match the eligible player's side.",
                    nameof(geometry));
            }

            return CanAttempt(
                eligiblePlayer,
                observedGeometry.TakeoffPoint,
                observedGeometry.ContactPoint,
                observedGeometry.AttackLineDistanceFromCenter,
                observedGeometry.NetHeight);
        }

        public static ActionEligibilityDecisionV3 CanAttempt(
            OnCourtPlayerEligibilityV3 player,
            SimVector3 takeoffPoint,
            SimVector3 contactPoint,
            float attackLineDistanceFromCenter,
            float netHeight)
        {
            var facts = new AttackAttemptFactsV3(
                player, takeoffPoint, contactPoint, attackLineDistanceFromCenter, netHeight);
            var isEligible = !facts.IsContactAboveNet ||
                             !facts.IsTakeoffInFrontZone ||
                             facts.Player.CanAttackAboveNetFromFrontZone;
            return new ActionEligibilityDecisionV3(
                isEligible,
                isEligible ? "eligible attack attempt" : "ineligible above-net front-zone attack");
        }
    }

    public static class BlockEligibilityRulesV3
    {
        public static ActionEligibilityDecisionV3 CanAttempt(OnCourtPlayerEligibilityV3 player)
        {
            var facts = new BlockAttemptFactsV3(player);
            return new ActionEligibilityDecisionV3(
                facts.Player.CanBlock,
                facts.Player.CanBlock ? "eligible block attempt" : "ineligible block attempt");
        }
    }
}
