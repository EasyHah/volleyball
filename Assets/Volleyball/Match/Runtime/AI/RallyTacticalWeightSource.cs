using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct RallyTacticalWeightRequest
    {
        public RallyTacticalWeightRequest(
            TeamId team,
            RallyDecisionStage stage,
            int tacticRevision,
            int requestSequence,
            int countedTeamTouches,
            float availableSimulationSeconds,
            SimVector3 ballPosition,
            SimVector3 ballVelocity)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if (!Enum.IsDefined(typeof(RallyDecisionStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }

            if (tacticRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tacticRevision));
            }

            if (requestSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestSequence));
            }

            if (countedTeamTouches < 0 || countedTeamTouches > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(countedTeamTouches));
            }

            if (!IsFinite(availableSimulationSeconds) || availableSimulationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(availableSimulationSeconds));
            }

            if (!ballPosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(ballPosition));
            }

            if (!ballVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(ballVelocity));
            }

            Team = team;
            Stage = stage;
            TacticRevision = tacticRevision;
            RequestSequence = requestSequence;
            CountedTeamTouches = countedTeamTouches;
            AvailableSimulationSeconds = availableSimulationSeconds;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
        }

        public TeamId Team { get; }

        public RallyDecisionStage Stage { get; }

        public int TacticRevision { get; }

        public int RequestSequence { get; }

        public int CountedTeamTouches { get; }

        public float AvailableSimulationSeconds { get; }

        public SimVector3 BallPosition { get; }

        public SimVector3 BallVelocity { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IRallyTacticalWeightSource
    {
        Task<RallyTacticalWeightProposal> RequestAsync(
            RallyTacticalWeightRequest request,
            CancellationToken cancellationToken);
    }
}
