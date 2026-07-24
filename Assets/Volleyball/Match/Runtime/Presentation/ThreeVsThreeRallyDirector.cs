using System;
using System.Collections.Generic;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Prototype;
using MatchContextV2 = Volleyball.Shared.Contracts.MatchContextV2;
using MatchResultV2 = Volleyball.Shared.Contracts.MatchResultV2;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;

namespace Volleyball.Presentation
{
    // Explicitly isolated compatibility entry point for the prototype-only 3v3 scene.
    public sealed class ThreeVsThreeRallyDirector : PhysicalMatchRallyDirector
    {
        private PrototypeLegacyMatchSetAdapter _prototypeSet;

        public MatchContextV2 PrototypeLegacyContext { get; private set; }

        public MatchResultV2 PrototypeLegacyResult { get; private set; }

        protected override bool HasPrototypeResult =>
            PrototypeLegacyResult != null;

        public void InitializePrototypeLegacyV2(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV2 context,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource = null,
            PhysicalMatchConfiguration configuration = null,
            TeamSide firstServingSide = TeamSide.Home)
        {
            var prototypeContext =
                context ?? throw new ArgumentNullException(nameof(context));
            var prototypeConfiguration =
                configuration ?? PhysicalMatchConfiguration.ThreeVsThree;
            PrototypeLegacyContext = prototypeContext;
            InitializeCore(
                ball,
                agents,
                scoreDisplay,
                tacticalWeightSource,
                prototypeConfiguration,
                prototypeContext.Home.Players.Count,
                prototypeContext.Away.Players.Count,
                () =>
                {
                    _prototypeSet = new PrototypeLegacyMatchSetAdapter(
                        prototypeContext,
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
                    "The prototype legacy set has not been initialized.");
            }

            PrototypeLegacyResult = _prototypeSet.CreatePrototypeResult();
        }
    }
}
