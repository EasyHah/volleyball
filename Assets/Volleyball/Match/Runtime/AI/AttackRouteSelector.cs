using System;
using System.Collections.Generic;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.AI
{
    public enum GeometricAttackRoute
    {
        Line,
        CrossCourt,
        OverHand,
        EdgeLeft,
        EdgeRight
    }

    public readonly struct AttackRouteSelectionInput
    {
        public AttackRouteSelectionInput(
            TeamId attackingTeam,
            SimVector3 contactCenter,
            float normalFlightSeconds,
            IReadOnlyList<ContactCapsuleFrame> predictedArms,
            BallSimulationParameters parameters,
            float fixedStepSeconds)
        {
            if (!Enum.IsDefined(typeof(TeamId), attackingTeam))
            {
                throw new ArgumentOutOfRangeException(nameof(attackingTeam));
            }

            if (!contactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(contactCenter));
            }

            if (!IsFinite(normalFlightSeconds) || normalFlightSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalFlightSeconds));
            }

            if (!IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds));
            }

            AttackingTeam = attackingTeam;
            ContactCenter = contactCenter;
            NormalFlightSeconds = normalFlightSeconds;
            PredictedArms = predictedArms ?? throw new ArgumentNullException(nameof(predictedArms));
            Parameters = parameters;
            FixedStepSeconds = fixedStepSeconds;
        }

        public TeamId AttackingTeam { get; }
        public SimVector3 ContactCenter { get; }
        public float NormalFlightSeconds { get; }
        public IReadOnlyList<ContactCapsuleFrame> PredictedArms { get; }
        public BallSimulationParameters Parameters { get; }
        public float FixedStepSeconds { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct AttackRouteSelection : IEquatable<AttackRouteSelection>
    {
        public AttackRouteSelection(
            GeometricAttackRoute route,
            SimVector3 target,
            SimVector3 initialVelocity,
            float flightSeconds,
            float minimumArmClearance)
        {
            Route = route;
            Target = target;
            InitialVelocity = initialVelocity;
            FlightSeconds = flightSeconds;
            MinimumArmClearance = minimumArmClearance;
        }

        public GeometricAttackRoute Route { get; }
        public SimVector3 Target { get; }
        public SimVector3 InitialVelocity { get; }
        public float FlightSeconds { get; }
        public float MinimumArmClearance { get; }

        public bool Equals(AttackRouteSelection other)
        {
            return Route == other.Route &&
                   Target.Equals(other.Target) &&
                   InitialVelocity.Equals(other.InitialVelocity) &&
                   FlightSeconds.Equals(other.FlightSeconds) &&
                   MinimumArmClearance.Equals(other.MinimumArmClearance);
        }

        public override bool Equals(object obj)
        {
            return obj is AttackRouteSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Route;
                hashCode = (hashCode * 397) ^ Target.GetHashCode();
                hashCode = (hashCode * 397) ^ InitialVelocity.GetHashCode();
                hashCode = (hashCode * 397) ^ FlightSeconds.GetHashCode();
                return (hashCode * 397) ^ MinimumArmClearance.GetHashCode();
            }
        }
    }

    /// <summary>
    /// The complete, non-selecting result for one geometric route.  Gate I uses
    /// this as evidence before ranking candidates; the legacy selector below
    /// deliberately continues to expose only its winning route.
    /// </summary>
    public sealed class AttackRouteEvaluation
    {
        internal AttackRouteEvaluation(GeometricAttackRoute route, AttackRouteSelection? selection,
            float legalCrossingRatio, float netAntennaOrOutRatio, string eliminationReason,
            string envelopeIdentity, IReadOnlyList<string> trajectoryArtifactIdentities)
        {
            Route = route; Selection = selection; LegalCrossingRatio = legalCrossingRatio;
            NetAntennaOrOutRatio = netAntennaOrOutRatio;
            EliminationReason = eliminationReason ?? string.Empty;
            EnvelopeIdentity = envelopeIdentity ?? throw new ArgumentNullException(nameof(envelopeIdentity));
            TrajectoryArtifactIdentities = trajectoryArtifactIdentities ?? throw new ArgumentNullException(nameof(trajectoryArtifactIdentities));
        }
        public GeometricAttackRoute Route { get; }
        public AttackRouteSelection? Selection { get; }
        public float LegalCrossingRatio { get; }
        public float NetAntennaOrOutRatio { get; }
        public float MinimumArmClearance => Selection?.MinimumArmClearance ?? float.NegativeInfinity;
        public SimVector3 Target => Selection?.Target ?? default;
        public SimVector3 InitialVelocity => Selection?.InitialVelocity ?? default;
        public float ExpectedValue => Selection.HasValue ? Selection.Value.MinimumArmClearance : float.MinValue;
        public string EliminationReason { get; }
        public string EnvelopeIdentity { get; }
        public IReadOnlyList<string> TrajectoryArtifactIdentities { get; }
    }

    public static class AttackRouteSelector
    {
        private const float BallRadius = 0.12f;
        private const float CourtHalfWidth = 4.5f;
        private const float CourtHalfLength = 9f;
        private const float NetHeight = 2.48f;
        private const float LandingDepth = 5.25f;
        private const float OverHandAdditionalFlightSeconds = 0.95f;

        public static AttackRouteSelection Select(AttackRouteSelectionInput input)
        {
            var found = false;
            var best = default(AttackRouteSelection);
            var bestScore = float.MinValue;
            foreach (GeometricAttackRoute route in Enum.GetValues(typeof(GeometricAttackRoute)))
            {
                if (!TryEvaluate(input, route, out var selection, out var score))
                {
                    continue;
                }

                if (!found || score > bestScore + 0.000001f)
                {
                    found = true;
                    best = selection;
                    bestScore = score;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No legal geometric attack route reaches the opponent court.");
            }

            return best;
        }

        public static IReadOnlyList<AttackRouteEvaluation> EvaluateAll(
            AttackRouteSelectionInput input,
            ExecutionEnvelopeV4 envelope,
            IReadOnlyList<BallTrajectoryPredictionArtifactV4> samples)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var identities = new List<string>(samples.Count);
            for (var index = 0; index < samples.Count; index++)
            {
                if (samples[index] == null) throw new ArgumentException("Trajectory samples cannot contain null.", nameof(samples));
                identities.Add(samples[index].ArtifactIdentity);
            }
            var evaluations = new List<AttackRouteEvaluation>();
            foreach (GeometricAttackRoute route in Enum.GetValues(typeof(GeometricAttackRoute)))
            {
                if (TryEvaluate(input, route, out var selection, out _))
                {
                    evaluations.Add(new AttackRouteEvaluation(route, selection, 1f, 0f, string.Empty, envelope.Identity, identities));
                }
                else
                {
                    evaluations.Add(new AttackRouteEvaluation(route, null, 0f, 1f, "IllegalCrossingOrLanding", envelope.Identity, identities));
                }
            }
            return evaluations.AsReadOnly();
        }

        private static bool TryEvaluate(
            AttackRouteSelectionInput input,
            GeometricAttackRoute route,
            out AttackRouteSelection selection,
            out float score)
        {
            var frame = new TeamCourtFrame(input.AttackingTeam);
            var target = frame.ToWorld(new SimVector3(
                TargetX(route, input.ContactCenter.X),
                BallRadius,
                LandingDepth));
            var flightSeconds = route == GeometricAttackRoute.OverHand
                ? WholeStepDuration(
                    input.NormalFlightSeconds + OverHandAdditionalFlightSeconds,
                    input.FixedStepSeconds)
                : input.NormalFlightSeconds;

            ReturnVelocitySolution velocity;
            try
            {
                velocity = ReturnVelocitySolver.Solve(
                    input.ContactCenter,
                    target,
                    flightSeconds,
                    input.FixedStepSeconds,
                    input.Parameters);
            }
            catch (ArgumentException)
            {
                selection = default;
                score = float.MinValue;
                return false;
            }
            catch (InvalidOperationException)
            {
                selection = default;
                score = float.MinValue;
                return false;
            }

            var state = new BallState(input.ContactCenter, velocity.InitialVelocity, BallRadius);
            var crossedNet = false;
            var minimumClearance = input.PredictedArms.Count == 0 ? 0f : float.MaxValue;
            for (var step = 0; step < velocity.StepCount; step++)
            {
                var previous = state.Position;
                BallIntegrator.Step(state, input.FixedStepSeconds, input.Parameters);
                minimumClearance = Math.Min(
                    minimumClearance,
                    ClearanceFromArms(state.Position, input.PredictedArms));

                var previousLocal = frame.ToLocal(previous);
                var currentLocal = frame.ToLocal(state.Position);
                if (!crossedNet && previousLocal.Z < 0f && currentLocal.Z >= 0f)
                {
                    var denominator = currentLocal.Z - previousLocal.Z;
                    var alpha = denominator <= 0.000001f ? 0f : -previousLocal.Z / denominator;
                    var crossing = SimVector3.Lerp(previous, state.Position, alpha);
                    if (crossing.Y - BallRadius <= NetHeight ||
                        Math.Abs(crossing.X) + BallRadius > CourtHalfWidth)
                    {
                        selection = default;
                        score = float.MinValue;
                        return false;
                    }

                    crossedNet = true;
                }
            }

            var landingLocal = frame.ToLocal(state.Position);
            if (!crossedNet ||
                Math.Abs(state.Position.X) + BallRadius > CourtHalfWidth ||
                landingLocal.Z < 0f || landingLocal.Z + BallRadius > CourtHalfLength ||
                Math.Abs(state.Position.Y - BallRadius) > 0.001f)
            {
                selection = default;
                score = float.MinValue;
                return false;
            }

            score = minimumClearance -
                    RouteTempoCost(route) -
                    (Math.Max(0f, velocity.InitialVelocity.Magnitude - 19f) * 0.75f);
            selection = new AttackRouteSelection(
                route,
                target,
                velocity.InitialVelocity,
                flightSeconds,
                minimumClearance);
            return true;
        }

        private static float TargetX(GeometricAttackRoute route, float contactX)
        {
            switch (route)
            {
                case GeometricAttackRoute.Line:
                case GeometricAttackRoute.OverHand:
                    return Clamp(contactX, -3.6f, 3.6f);
                case GeometricAttackRoute.CrossCourt:
                    return contactX <= 0f ? 3.25f : -3.25f;
                case GeometricAttackRoute.EdgeLeft:
                    return -1.85f;
                case GeometricAttackRoute.EdgeRight:
                    return 1.85f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private static float ClearanceFromArms(
            SimVector3 position,
            IReadOnlyList<ContactCapsuleFrame> predictedArms)
        {
            if (predictedArms.Count == 0)
            {
                return 0f;
            }

            var minimum = float.MaxValue;
            for (var index = 0; index < predictedArms.Count; index++)
            {
                var arm = predictedArms[index];
                var closest = arm.ClosestPoint(position, out _);
                minimum = Math.Min(
                    minimum,
                    (position - closest).Magnitude - BallRadius - arm.Radius);
            }

            return minimum;
        }

        private static float WholeStepDuration(float seconds, float fixedStepSeconds)
        {
            return Math.Max(1, (int)Math.Round(seconds / fixedStepSeconds)) * fixedStepSeconds;
        }

        private static float RouteTempoCost(GeometricAttackRoute route)
        {
            return route switch
            {
                GeometricAttackRoute.OverHand => 4.00f,
                GeometricAttackRoute.EdgeLeft => 0.05f,
                GeometricAttackRoute.EdgeRight => 0.05f,
                _ => 0f
            };
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
