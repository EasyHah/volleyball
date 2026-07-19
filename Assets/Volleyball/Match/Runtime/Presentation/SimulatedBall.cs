using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public readonly struct BallContactCandidate
    {
        public BallContactCandidate(
            ContactSurfaceSnapshot surface,
            TechniqueAction action,
            float playerTechnique,
            SimVector3 targetVelocity,
            SimVector3 strikeDirection,
            ContactResponseParameters responseParameters)
            : this(
                surface,
                action,
                null,
                playerTechnique,
                targetVelocity,
                strikeDirection,
                responseParameters)
        {
        }

        public BallContactCandidate(
            ContactSurfaceSnapshot surface,
            TechniqueAction action,
            PlayerId? actor,
            float playerTechnique,
            SimVector3 targetVelocity,
            SimVector3 strikeDirection,
            ContactResponseParameters responseParameters)
        {
            Surface = surface;
            Action = action;
            Actor = actor;
            PlayerTechnique = playerTechnique;
            TargetVelocity = targetVelocity;
            StrikeDirection = strikeDirection;
            ResponseParameters = responseParameters;
        }

        public ContactSurfaceSnapshot Surface { get; }

        public TechniqueAction Action { get; }

        public PlayerId? Actor { get; }

        public float PlayerTechnique { get; }

        public SimVector3 TargetVelocity { get; }

        public SimVector3 StrikeDirection { get; }

        public ContactResponseParameters ResponseParameters { get; }
    }

    public enum BallContactDisposition
    {
        Ignore,
        Accept,
        Fault
    }

    public readonly struct BallContactResolution
    {
        private BallContactResolution(BallContactDisposition disposition, string reason)
        {
            Disposition = disposition;
            Reason = reason;
        }

        public BallContactDisposition Disposition { get; }

        public string Reason { get; }

        public static BallContactResolution Ignore()
        {
            return new BallContactResolution(BallContactDisposition.Ignore, string.Empty);
        }

        public static BallContactResolution Accept()
        {
            return new BallContactResolution(BallContactDisposition.Accept, string.Empty);
        }

        public static BallContactResolution Fault(string reason)
        {
            return new BallContactResolution(
                BallContactDisposition.Fault,
                string.IsNullOrWhiteSpace(reason) ? "illegal player contact" : reason);
        }
    }

    public interface IBallContactSource
    {
        void CollectContacts(float simulationTime, float deltaSeconds, ICollection<BallContactCandidate> contacts);
    }

    public readonly struct PlayerBallContactEvent
    {
        public PlayerBallContactEvent(
            BallContactCandidate candidate,
            SweptBallHit hit,
            float contactSimulationTime,
            ContactResponseResult physicalResponse,
            TechniqueControlResult techniqueResponse)
        {
            Candidate = candidate;
            Hit = hit;
            ContactSimulationTime = contactSimulationTime;
            PhysicalResponse = physicalResponse;
            TechniqueResponse = techniqueResponse;
        }

        public BallContactCandidate Candidate { get; }

        public SweptBallHit Hit { get; }

        public float ContactSimulationTime { get; }

        public ContactResponseResult PhysicalResponse { get; }

        public TechniqueControlResult TechniqueResponse { get; }
    }

    public readonly struct PlayerContactRejectedEvent
    {
        public PlayerContactRejectedEvent(
            BallContactCandidate candidate,
            SweptBallHit hit,
            float contactSimulationTime,
            string reason)
        {
            Candidate = candidate;
            Hit = hit;
            ContactSimulationTime = contactSimulationTime;
            Reason = string.IsNullOrWhiteSpace(reason) ? "illegal player contact" : reason;
        }

        public BallContactCandidate Candidate { get; }

        public SweptBallHit Hit { get; }

        public float ContactSimulationTime { get; }

        public string Reason { get; }
    }

    public sealed class NetPlaneCrossingEvent
    {
        public NetPlaneCrossingEvent(SimVector3 point)
        {
            Point = point;
        }

        public SimVector3 Point { get; }

        public bool IsRemainingStepConsumed { get; private set; }

        public void ConsumeRemainingStep()
        {
            IsRemainingStepConsumed = true;
        }
    }

    public readonly struct BallSimulationDiagnostics
    {
        public BallSimulationDiagnostics(
            long completedSteps,
            int resetCount,
            int groundContacts,
            int netContacts,
            int nonFiniteStates,
            float maximumSpeed)
        {
            CompletedSteps = completedSteps;
            ResetCount = resetCount;
            GroundContacts = groundContacts;
            NetContacts = netContacts;
            NonFiniteStates = nonFiniteStates;
            MaximumSpeed = maximumSpeed;
        }

        public long CompletedSteps { get; }

        public int ResetCount { get; }

        public int GroundContacts { get; }

        public int NetContacts { get; }

        public int NonFiniteStates { get; }

        public float MaximumSpeed { get; }
    }

    public sealed class SimulatedBall : MonoBehaviour
    {
        public const float DefaultRadius = 0.12f;
        public const float DefaultFixedStep = 1f / 120f;

        [SerializeField]
        private float _gravity = -9.8f;

        [SerializeField]
        private float _linearDampingPer60Hz = 0.9995f;

        [SerializeField]
        private float _groundHeight = 0.15f;

        [SerializeField]
        private float _groundRestitution = 0.55f;

        [SerializeField]
        private float _groundFriction = 0.25f;

        private readonly NetCollisionGeometry _net =
            new NetCollisionGeometry(CourtBuilder.HalfWidth, 2.48f, 0.08f, 0.15f);
        private readonly List<IBallContactSource> _contactSources = new List<IBallContactSource>();
        private readonly List<BallContactCandidate> _contactCandidates = new List<BallContactCandidate>();

        private FixedStepAccumulator _accumulator;
        private BallSimulationParameters _parameters;
        private long _completedSteps;
        private int _resetCount;
        private int _groundContacts;
        private int _netContacts;
        private int _nonFiniteStates;
        private float _maximumSpeed;
        private float _simulationTime;

        public event Action<EnvironmentCollisionHit> EnvironmentContact;

        public event Action<PlayerBallContactEvent> PlayerContact;

        public event Action<PlayerContactRejectedEvent> PlayerContactRejected;

        public event Action<NetPlaneCrossingEvent> NetPlaneCrossed;

        public Func<BallContactCandidate, SweptBallHit, float, BallContactResolution>
            ContactCandidateResolver { get; set; }

        public BallState State { get; private set; }

        public BallSimulationDiagnostics Diagnostics => new BallSimulationDiagnostics(
            _completedSteps,
            _resetCount,
            _groundContacts,
            _netContacts,
            _nonFiniteStates,
            _maximumSpeed);

        public float SimulationTime => _simulationTime;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            AdvanceSimulation(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (State == null)
            {
                return;
            }

            var alpha = _accumulator == null ? 1f : (float)_accumulator.InterpolationAlpha;
            transform.position = ToUnity(SimVector3.Lerp(State.PreviousPosition, State.Position, alpha));
        }

        public void ResetBall(Vector3 position, bool active = false)
        {
            EnsureInitialized();
            var trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
            }
            State.Reset(ToSimulation(position), SimVector3.Zero, active);
            _accumulator.Reset();
            transform.position = position;
            _simulationTime = 0f;
            _resetCount++;
        }

        public void Launch(Vector3 initialVelocity)
        {
            EnsureInitialized();
            var velocity = ToSimulation(initialVelocity);
            if (!velocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(initialVelocity));
            }

            State.Reset(ToSimulation(transform.position), velocity, true);
            _accumulator.Reset();
            _maximumSpeed = Math.Max(_maximumSpeed, velocity.Magnitude);
        }

        public void Stop()
        {
            EnsureInitialized();
            State.Reset(State.Position, SimVector3.Zero, false);
            _accumulator.Reset();
        }

        public void AdvanceSimulation(double elapsedSeconds)
        {
            EnsureInitialized();
            _accumulator.Advance(elapsedSeconds, StepSimulation);
        }

        public void RegisterContactSource(IBallContactSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!_contactSources.Contains(source))
            {
                _contactSources.Add(source);
            }
        }

        public void UnregisterContactSource(IBallContactSource source)
        {
            if (source != null)
            {
                _contactSources.Remove(source);
            }
        }

        private void StepSimulation(float deltaSeconds)
        {
            if (!State.IsActive)
            {
                return;
            }

            var stepStartTime = _simulationTime;
            BallIntegrator.Step(State, deltaSeconds, _parameters);
            _completedSteps++;
            _simulationTime += deltaSeconds;
            if (!State.Position.IsFinite || !State.Velocity.IsFinite)
            {
                _nonFiniteStates++;
                State.MarkDead();
                return;
            }

            var hasGround = EnvironmentCollision.TryGround(State, _groundHeight, out var groundHit);
            var hasNet = EnvironmentCollision.TryNet(State, _net, out var netHit);
            var hasCrossing = TryNetPlaneCrossing(
                State.PreviousPosition,
                State.Position,
                out var crossing,
                out var crossingFraction);
            var hasPlayer = TryFindEarliestPlayerContact(
                deltaSeconds,
                stepStartTime,
                out var playerCandidate,
                out var playerHit,
                out var playerResolution,
                out var contactSimulationTime);
            var environmentFraction = hasGround && (!hasNet || groundHit.TimeFraction <= netHit.TimeFraction)
                ? groundHit.TimeFraction
                : hasNet ? netHit.TimeFraction : float.PositiveInfinity;
            var playerFraction = hasPlayer ? playerHit.TimeFraction : float.PositiveInfinity;
            var physicalFraction = Math.Min(environmentFraction, playerFraction);
            if (hasCrossing && crossingFraction < physicalFraction)
            {
                var crossingEvent = new NetPlaneCrossingEvent(crossing);
                NetPlaneCrossed?.Invoke(crossingEvent);
                if (crossingEvent.IsRemainingStepConsumed)
                {
                    UpdateMaximumSpeed();
                    return;
                }
            }

            if (hasPlayer && playerHit.TimeFraction <= environmentFraction)
            {
                if (playerResolution.Disposition == BallContactDisposition.Fault)
                {
                    PlayerContactRejected?.Invoke(new PlayerContactRejectedEvent(
                        playerCandidate,
                        playerHit,
                        contactSimulationTime,
                        playerResolution.Reason));
                    UpdateMaximumSpeed();
                    return;
                }

                var physical = ContactResponse.Apply(State, playerHit, playerCandidate.ResponseParameters);
                var technique = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                    playerCandidate.Action,
                    physical.PhysicalOutgoing,
                    playerCandidate.TargetVelocity,
                    playerCandidate.StrikeDirection,
                    playerCandidate.PlayerTechnique,
                    playerHit.Centeredness));
                ContactResponse.ApplyTechniqueVelocity(State, playerHit, technique.FinalOutgoing);
                PlayerContact?.Invoke(new PlayerBallContactEvent(
                    playerCandidate,
                    playerHit,
                    contactSimulationTime,
                    physical,
                    technique));
            }
            else if (hasGround && (!hasNet || groundHit.TimeFraction <= netHit.TimeFraction))
            {
                EnvironmentCollision.ApplyResponse(State, groundHit, _groundRestitution, _groundFriction);
                State.MarkDead();
                _groundContacts++;
                EnvironmentContact?.Invoke(groundHit);
            }
            else if (hasNet)
            {
                EnvironmentCollision.ApplyResponse(State, netHit, 0.35f, 0.35f);
                _netContacts++;
                EnvironmentContact?.Invoke(netHit);
            }

            UpdateMaximumSpeed();
        }

        private bool TryFindEarliestPlayerContact(
            float deltaSeconds,
            float stepStartTime,
            out BallContactCandidate earliestCandidate,
            out SweptBallHit earliestHit,
            out BallContactResolution earliestResolution,
            out float earliestContactSimulationTime)
        {
            _contactCandidates.Clear();
            foreach (var source in _contactSources)
            {
                source.CollectContacts(_simulationTime, deltaSeconds, _contactCandidates);
            }

            earliestCandidate = default;
            earliestHit = default;
            earliestResolution = default;
            earliestContactSimulationTime = 0f;
            var found = false;
            foreach (var candidate in _contactCandidates)
            {
                if (!SweptBallCollision.TryFindContact(State, candidate.Surface, deltaSeconds, out var hit))
                {
                    continue;
                }

                var contactSimulationTime = stepStartTime + (deltaSeconds * hit.TimeFraction);
                var resolution = ContactCandidateResolver?.Invoke(candidate, hit, contactSimulationTime) ??
                                 BallContactResolution.Accept();
                if (resolution.Disposition == BallContactDisposition.Ignore ||
                    found && hit.TimeFraction >= earliestHit.TimeFraction)
                {
                    continue;
                }

                found = true;
                earliestCandidate = candidate;
                earliestHit = hit;
                earliestResolution = resolution;
                earliestContactSimulationTime = contactSimulationTime;
            }

            return found;
        }

        private void UpdateMaximumSpeed()
        {
            _maximumSpeed = Math.Max(_maximumSpeed, State.Velocity.Magnitude);
        }

        public static bool TryNetPlaneCrossing(
            SimVector3 previous,
            SimVector3 current,
            out SimVector3 crossing)
        {
            return TryNetPlaneCrossing(previous, current, out crossing, out _);
        }

        public static bool TryNetPlaneCrossing(
            SimVector3 previous,
            SimVector3 current,
            out SimVector3 crossing,
            out float timeFraction)
        {
            crossing = SimVector3.Zero;
            timeFraction = 0f;
            if ((previous.Z < 0f && current.Z < 0f) ||
                (previous.Z > 0f && current.Z > 0f) ||
                previous.Z == current.Z)
            {
                return false;
            }

            timeFraction = -previous.Z / (current.Z - previous.Z);
            if (timeFraction < 0f || timeFraction > 1f)
            {
                return false;
            }

            crossing = SimVector3.Lerp(previous, current, timeFraction);
            return true;
        }


        private void EnsureInitialized()
        {
            if (State != null)
            {
                return;
            }

            _parameters = new BallSimulationParameters(_gravity, _linearDampingPer60Hz);
            _accumulator = new FixedStepAccumulator(1d / 120d, 16);
            State = new BallState(ToSimulation(transform.position), SimVector3.Zero, DefaultRadius, false);
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }
}
