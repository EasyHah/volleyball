using System;
using System.Collections.Generic;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    public enum V3RulesMode
    {
        Disabled,
        Shadow,
        Authority
    }

    public sealed class FullRallyV3RulesRuntimeAdapter
    {
        private readonly int _rulesVersion;
        private OnCourtEligibilitySnapshot _eligibility;
        private RallyRulesEngineV3 _engine;

        public FullRallyV3RulesRuntimeAdapter(
            int rulesVersion,
            OnCourtEligibilitySnapshot eligibility,
            TeamSide initialPossession,
            V3RulesMode mode)
        {
            if (rulesVersion != 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rulesVersion),
                    rulesVersion,
                    "FullRallyV3RulesRuntimeAdapter requires rules version 3.");
            }

            _rulesVersion = rulesVersion;
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            if (!Enum.IsDefined(typeof(V3RulesMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Mode = mode;
            BeginRally(initialPossession);
        }

        public V3RulesMode Mode { get; }

        public int RulesVersion => _rulesVersion;

        public void BeginRally(TeamSide initialPossession)
        {
            _engine = RallyRulesEngineV3.Open(initialPossession);
        }

        public void BeginRally(
            OnCourtEligibilitySnapshot eligibility,
            TeamSide initialPossession)
        {
            var refreshedEligibility =
                eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            _eligibility = refreshedEligibility;
            BeginRally(initialPossession);
        }

        public RuleTransitionV3 ObserveAcceptedContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            return CommitContact(actor, side, classification, contactGroup);
        }

        public RuleTransitionV3 EvaluateContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            return EvaluateContactCore(actor, side, classification, contactGroup, null);
        }

        public RuleTransitionV3 EvaluateContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup,
            AttackGeometryFactV3 attackGeometry)
        {
            return EvaluateContactCore(
                actor,
                side,
                classification,
                contactGroup,
                attackGeometry ?? throw new ArgumentNullException(nameof(attackGeometry)));
        }

        private RuleTransitionV3 EvaluateContactCore(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup,
            AttackGeometryFactV3 attackGeometry)
        {
            var contact = CreateContact(actor, side, classification, contactGroup);
            var eligibilityRejection = EvaluateEligibility(contact, attackGeometry);
            return eligibilityRejection ?? _engine.CanAttempt(contact);
        }

        public RuleTransitionV3 CommitContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            return CommitContactCore(actor, side, classification, contactGroup, null);
        }

        public RuleTransitionV3 CommitContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup,
            AttackGeometryFactV3 attackGeometry)
        {
            return CommitContactCore(
                actor,
                side,
                classification,
                contactGroup,
                attackGeometry ?? throw new ArgumentNullException(nameof(attackGeometry)));
        }

        private RuleTransitionV3 CommitContactCore(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup,
            AttackGeometryFactV3 attackGeometry)
        {
            var contact = CreateContact(actor, side, classification, contactGroup);
            var eligibilityRejection = EvaluateEligibility(contact, attackGeometry);
            return eligibilityRejection ?? _engine.Apply(contact);
        }

        private ActualContactEventV3 CreateContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            var contact = new ActualContactEventV3(actor, side, classification, contactGroup);
            OnCourtPlayerEligibilityV3 eligibleActor;
            try
            {
                eligibleActor = _eligibility.For(actor);
            }
            catch (KeyNotFoundException)
            {
                return contact;
            }

            if (eligibleActor.Side != side)
            {
                throw new ArgumentException(
                    "The contact side must match the actor's on-court side.",
                    nameof(side));
            }

            return contact;
        }

        private RuleTransitionV3 EvaluateEligibility(
            ActualContactEventV3 contact,
            AttackGeometryFactV3 attackGeometry)
        {
            OnCourtPlayerEligibilityV3 eligibleActor;
            try
            {
                eligibleActor = _eligibility.For(contact.Actor.Value);
            }
            catch (KeyNotFoundException)
            {
                return Reject(RuleRejectionReasonV3.ActorNotOnCourt);
            }

            if (contact.Classification == RallyContactClassificationV3.BlockContact &&
                !BlockEligibilityRulesV3.CanAttempt(eligibleActor).IsEligible)
            {
                return Reject(RuleRejectionReasonV3.ActionIneligible);
            }

            if (attackGeometry != null &&
                !AttackEligibilityRulesV3.CanAttempt(eligibleActor, attackGeometry).IsEligible)
            {
                return Reject(RuleRejectionReasonV3.ActionIneligible);
            }

            return null;
        }

        private RuleTransitionV3 Reject(RuleRejectionReasonV3 reason)
        {
            return RuleTransitionV3.Reject(reason, _engine.State);
        }

    }
}
