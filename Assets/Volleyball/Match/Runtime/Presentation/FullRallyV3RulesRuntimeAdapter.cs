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
        private readonly MatchContextV3 _context;
        private OnCourtEligibilitySnapshot _eligibility;
        private RallyRulesEngineV3 _engine;

        public FullRallyV3RulesRuntimeAdapter(
            MatchContextV3 context,
            OnCourtEligibilitySnapshot eligibility,
            TeamSide initialPossession,
            V3RulesMode mode)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            if (!Enum.IsDefined(typeof(V3RulesMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            ValidateEligibility(_context, eligibility);
            Mode = mode;
            BeginRally(initialPossession);
        }

        public V3RulesMode Mode { get; }

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
            ValidateEligibility(_context, refreshedEligibility);
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
            var contact = CreateContact(actor, side, classification, contactGroup);
            var eligibilityRejection = EvaluateEligibility(contact);
            return eligibilityRejection ?? _engine.CanAttempt(contact);
        }

        public RuleTransitionV3 CommitContact(
            PlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            var contact = CreateContact(actor, side, classification, contactGroup);
            var eligibilityRejection = EvaluateEligibility(contact);
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

        private RuleTransitionV3 EvaluateEligibility(ActualContactEventV3 contact)
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

            return null;
        }

        private RuleTransitionV3 Reject(RuleRejectionReasonV3 reason)
        {
            return RuleTransitionV3.Reject(reason, _engine.State);
        }

        private static void ValidateEligibility(
            MatchContextV3 context,
            OnCourtEligibilitySnapshot eligibility)
        {
            var sidesByPlayer = new Dictionary<PlayerId, TeamSide>();
            AddTeam(context.Home, sidesByPlayer);
            AddTeam(context.Away, sidesByPlayer);
            foreach (var player in eligibility.Players)
            {
                if (!sidesByPlayer.TryGetValue(player.PlayerId, out var expectedSide) ||
                    expectedSide != player.Side)
                {
                    throw new ArgumentException(
                        "On-court eligibility must use players and sides from the V3 context.",
                        nameof(eligibility));
                }
            }
        }

        private static void AddTeam(
            TeamSnapshotV3 team,
            IDictionary<PlayerId, TeamSide> sidesByPlayer)
        {
            foreach (var player in team.Players)
            {
                sidesByPlayer.Add(player.PlayerId, team.Side);
            }
        }
    }
}
