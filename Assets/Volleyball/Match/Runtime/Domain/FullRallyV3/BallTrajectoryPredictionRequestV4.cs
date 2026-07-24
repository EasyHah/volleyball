using System;
using System.Text;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryPredictionRequestV4
    {
        private readonly BallState _source;

        public BallTrajectoryPredictionRequestV4(
            TeamSide requestingTeam,
            long ballStateVersion,
            BallState source,
            BallSimulationParameters parameters,
            string physicsConfigurationHash,
            string samplingKey,
            int predictorVersion,
            string predictorConfigurationHash,
            string envelopeIdentity,
            ExecutionDegradationStepV4 degradationStep)
        {
            if (!Enum.IsDefined(typeof(TeamSide), requestingTeam))
            {
                throw new ArgumentOutOfRangeException(nameof(requestingTeam));
            }

            if (!Enum.IsDefined(
                    typeof(ExecutionDegradationStepV4),
                    degradationStep))
            {
                throw new ArgumentOutOfRangeException(nameof(degradationStep));
            }

            _source = source?.Clone() ??
                throw new ArgumentNullException(nameof(source));
            var expectedPhysicsConfigurationHash =
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                    parameters);
            if (!string.Equals(
                    physicsConfigurationHash,
                    expectedPhysicsConfigurationHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "physicsConfigurationHash does not match the supplied simulation parameters.",
                    nameof(physicsConfigurationHash));
            }

            RequestingTeam = requestingTeam;
            Parameters = parameters;
            Key = new BallTrajectoryPredictionCacheKeyV4(
                ballStateVersion,
                BuildBallStateFingerprint(_source),
                physicsConfigurationHash,
                samplingKey,
                predictorVersion,
                predictorConfigurationHash,
                envelopeIdentity,
                (int)degradationStep);
        }

        public TeamSide RequestingTeam { get; }

        public BallState Source => _source.Clone();

        public BallSimulationParameters Parameters { get; }

        public BallTrajectoryPredictionCacheKeyV4 Key { get; }

        public ExecutionDegradationStepV4 DegradationStep =>
            (ExecutionDegradationStepV4)Key.DegradationStep;

        public BallTrajectoryPredictionRequestV4 WithDegradationStep(
            ExecutionDegradationStepV4 degradationStep)
        {
            return new BallTrajectoryPredictionRequestV4(
                RequestingTeam,
                Key.BallStateVersion,
                _source,
                Parameters,
                Key.PhysicsConfigurationHash,
                Key.SamplingKey,
                Key.PredictorVersion,
                Key.PredictorConfigurationHash,
                Key.EnvelopeIdentity,
                degradationStep);
        }

        public static string BuildBallStateFingerprint(BallState source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var canonical = new StringBuilder(256);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "schema",
                "volleyball.ball-state.v4");
            ExecutionEnvelopeCanonicalV4.AppendVector(
                canonical,
                "position",
                source.Position);
            ExecutionEnvelopeCanonicalV4.AppendVector(
                canonical,
                "velocity",
                source.Velocity);
            ExecutionEnvelopeCanonicalV4.AppendFloat(
                canonical,
                "radius",
                source.Radius);
            return ExecutionEnvelopeCanonicalV4.Sha256(
                Encoding.UTF8.GetBytes(canonical.ToString()));
        }
    }
}
