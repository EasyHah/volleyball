using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class RallyRulesEngineV3
    {
        private TouchSequenceStateV3 _state;

        private RallyRulesEngineV3(TouchSequenceStateV3 initialState)
        {
            _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        public TouchSequenceStateV3 State => _state;

        public static RallyRulesEngineV3 Open(TeamSide servingTeam)
        {
            PlayerWorldSnapshotV3.RequireDefinedEnum(servingTeam, nameof(servingTeam));
            return new RallyRulesEngineV3(TouchSequenceStateV3.Initial);
        }

        public static RallyRulesEngineV3 Open(RallyStartStateV3 startState)
        {
            if (startState == null)
            {
                throw new ArgumentNullException(nameof(startState));
            }

            return new RallyRulesEngineV3(startState.TouchSequence);
        }

        public RuleTransitionV3 CanAttempt(ActualContactEventV3 contact)
        {
            if (contact == null)
            {
                throw new ArgumentNullException(nameof(contact));
            }

            return Evaluate(_state, contact);
        }

        public RuleTransitionV3 Apply(ActualContactEventV3 contact)
        {
            var transition = CanAttempt(contact);
            if (transition.Accepted)
            {
                _state = transition.After;
            }

            return transition;
        }

        private static RuleTransitionV3 Evaluate(TouchSequenceStateV3 state, ActualContactEventV3 contact)
        {
            if (state.IsTerminal)
            {
                return Reject(state, RuleRejectionReasonV3.RallyClosed);
            }

            if (state.LastContactGroup.HasValue && state.LastContactGroup.Value == contact.ContactGroup)
            {
                return Reject(state, RuleRejectionReasonV3.DuplicateContactGroup);
            }

            if (contact.Classification == RallyContactClassificationV3.EnvironmentContact)
            {
                return Accept(state, new TouchSequenceStateV3(
                    state.LastLegalPhysicalContactTeam,
                    state.CurrentCountedSequenceTeam,
                    state.CountedHits,
                    state.LastCountedActor,
                    contact.Classification,
                    contact.ContactGroup,
                    true));
            }

            var team = contact.Team.Value;
            if (contact.Classification == RallyContactClassificationV3.BlockContact)
            {
                return Accept(state, new TouchSequenceStateV3(
                    team,
                    null,
                    0,
                    null,
                    contact.Classification,
                    contact.ContactGroup,
                    false));
            }

            if (state.CurrentCountedSequenceTeam.HasValue && state.CurrentCountedSequenceTeam.Value == team)
            {
                if (state.LastCountedActor.HasValue && state.LastCountedActor.Value.Equals(contact.Actor.Value))
                {
                    return Reject(state, RuleRejectionReasonV3.ConsecutiveCountedContact);
                }

                if (state.CountedHits >= 3)
                {
                    return Reject(state, RuleRejectionReasonV3.FourthCountedContact);
                }

                return Accept(state, new TouchSequenceStateV3(
                    team,
                    team,
                    state.CountedHits + 1,
                    contact.Actor,
                    contact.Classification,
                    contact.ContactGroup,
                    false));
            }

            return Accept(state, new TouchSequenceStateV3(
                team,
                team,
                1,
                contact.Actor,
                contact.Classification,
                contact.ContactGroup,
                false));
        }

        private static RuleTransitionV3 Accept(TouchSequenceStateV3 before, TouchSequenceStateV3 after)
        {
            return new RuleTransitionV3(true, RuleRejectionReasonV3.None, before, after);
        }

        private static RuleTransitionV3 Reject(TouchSequenceStateV3 state, RuleRejectionReasonV3 reason)
        {
            return new RuleTransitionV3(false, reason, state, state);
        }
    }
}
