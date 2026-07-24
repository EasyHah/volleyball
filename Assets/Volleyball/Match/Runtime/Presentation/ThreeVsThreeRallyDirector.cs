using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Prototype;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using MatchResultV4 = Volleyball.Shared.Contracts.MatchResultV4;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;

namespace Volleyball.Presentation
{
    // Explicitly isolated V4 entry point for the prototype-only 3v3 scene.
    public sealed class ThreeVsThreeRallyDirector : PhysicalMatchRallyDirector
    {
        private MatchSet _prototypeSet;

        public MatchContextV4 PrototypeContext { get; private set; }

        public MatchResultV4 PrototypeResult { get; private set; }

        protected override bool HasPrototypeResult =>
            PrototypeResult != null;

        public void InitializePrototypeV4(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV4 context,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource = null,
            PhysicalMatchConfiguration configuration = null,
            TeamSide firstServingSide = TeamSide.Home)
        {
            var prototypeContext =
                context ?? throw new ArgumentNullException(nameof(context));
            var prototypeConfiguration =
                configuration ?? PhysicalMatchConfiguration.ThreeVsThree;
            var courtAgents =
                (agents ?? throw new ArgumentNullException(nameof(agents))).ToArray();
            var activeHomePlayers = courtAgents
                .Where(agent => agent != null && agent.Id.Team == TeamId.Blue)
                .Select(agent => agent.StableId)
                .ToArray();
            var activeAwayPlayers = courtAgents
                .Where(agent => agent != null && agent.Id.Team == TeamId.Orange)
                .Select(agent => agent.StableId)
                .ToArray();
            PrototypeContext = prototypeContext;
            InitializeCore(
                ball,
                courtAgents,
                scoreDisplay,
                tacticalWeightSource,
                prototypeConfiguration,
                activeHomePlayers.Length,
                activeAwayPlayers.Length,
                () =>
                {
                    _prototypeSet = new MatchSet(
                        prototypeContext,
                        activeHomePlayers,
                        activeAwayPlayers,
                        firstServingSide,
                        prototypeConfiguration.SetRules);
                    return _prototypeSet;
                });
        }

        protected override void CompletePrototypeMatch()
        {
            if (_prototypeSet == null)
            {
                throw new InvalidOperationException(
                    "The prototype V4 set has not been initialized.");
            }

            PrototypeResult = _prototypeSet.CreateResult(
                SuccessfulContacts,
                V3RuleTransitions);
        }
    }
}
