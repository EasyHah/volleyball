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
            if (mode == V3RulesMode.Authority)
            {
                throw new NotSupportedException(
                    "V3 authority is not available until the authority gate is configured.");
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
            var eligibleActor = _eligibility.For(actor);
            if (eligibleActor.Side != side)
            {
                throw new ArgumentException(
                    "The observed contact side must match the actor's on-court side.",
                    nameof(side));
            }

            return _engine.Apply(new ActualContactEventV3(
                actor,
                side,
                classification,
                contactGroup));
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
