using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using DominantHandV4 = Volleyball.Shared.Contracts.DominantHandV4;
using MatchAttributeDerivationConfigV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationConfigV4;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using PhysicalBaseAttributesV4 = Volleyball.Shared.Contracts.PhysicalBaseAttributesV4;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using PlayerSnapshotV4 = Volleyball.Shared.Contracts.PlayerSnapshotV4;
using RulesVersions = Volleyball.Shared.Contracts.RulesVersions;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TechnicalBaseAttributesV4 = Volleyball.Shared.Contracts.TechnicalBaseAttributesV4;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TeamSnapshotV4 = Volleyball.Shared.Contracts.TeamSnapshotV4;
using TrajectoryPredictionCacheEvictionPolicyV4 =
    Volleyball.Shared.Contracts.TrajectoryPredictionCacheEvictionPolicyV4;
using TrajectoryPredictionProviderConfigurationV4 =
    Volleyball.Shared.Contracts.TrajectoryPredictionProviderConfigurationV4;

namespace Volleyball.Presentation
{
    public sealed class FormalSixVsSixRallyBootstrap : MonoBehaviour
    {
        private static readonly Color BlueColor = new Color(0.1f, 0.42f, 0.95f);
        private static readonly Color OrangeColor = new Color(1f, 0.38f, 0.08f);
        private static readonly PhysicalMatchConfiguration Configuration =
            PhysicalMatchConfiguration.FormalIndoorSixVsSix;
        private static readonly object ExternalContextGate = new object();

        private static MatchContextV4 _queuedExternalContext;
        private bool _initialized;

        public FormalSixVsSixRallyDirector Director { get; private set; }

        public Exception InitializationException { get; private set; }

        public static string RuntimePhysicsConfigurationHash =>
            BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                new BallSimulationParameters(-9.8f, 0.9995f));

        public static TrajectoryPredictionProviderConfigurationV4
            CreateRuntimeTrajectoryConfiguration()
        {
            return new TrajectoryPredictionProviderConfigurationV4(
                128,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                BallTrajectoryPredictionProviderV4.CurrentPredictorVersion,
                BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash);
        }

        public static void QueueExternalContext(MatchContextV4 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            lock (ExternalContextGate)
            {
                if (_queuedExternalContext != null)
                {
                    throw new InvalidOperationException(
                        "A formal 6v6 external context is already queued.");
                }

                _queuedExternalContext = context;
            }
        }

        public static void ClearQueuedExternalContext(MatchContextV4 context)
        {
            lock (ExternalContextGate)
            {
                if (ReferenceEquals(_queuedExternalContext, context))
                {
                    _queuedExternalContext = null;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetExternalContextSlot()
        {
            lock (ExternalContextGate)
            {
                _queuedExternalContext = null;
            }
        }

        private static MatchContextV4 ConsumeQueuedExternalContext()
        {
            lock (ExternalContextGate)
            {
                var context = _queuedExternalContext;
                _queuedExternalContext = null;
                return context;
            }
        }

        private void Awake()
        {
            try
            {
                InitializeRuntime(
                    ConsumeQueuedExternalContext() ?? CreateSandboxContext());
            }
            catch (Exception exception)
            {
                InitializationException = exception;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void InitializeRuntime(MatchContextV4 context)
        {
            if (_initialized)
            {
                throw new InvalidOperationException(
                    "The formal 6v6 scene can only initialize once.");
            }

            _initialized = true;
            Application.targetFrameRate = 60;
            CourtBuilder.Build(transform, Configuration.CourtHalfLength);
            var ball = CreateBall();
            var agents = CreateRoster(context);
            var scoreDisplay = ScoreDisplay.Create(transform);
            var director = gameObject.AddComponent<FormalSixVsSixRallyDirector>();
            director.InitializeV4(
                ball,
                agents,
                context,
                scoreDisplay,
                configuration: Configuration);
            director.ConfigureV3Rules(V3RulesMode.Authority);
            Director = director;
            var rosterDisplay = gameObject.AddComponent<MatchRosterDisplay>();
            rosterDisplay.Initialize(director, agents);
            var cameras = gameObject.AddComponent<RallyCameraController>();
            cameras.Initialize(ball);
        }

        private SimulatedBall CreateBall()
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Formal6v6Ball";
            ballObject.transform.SetParent(transform, false);
            ballObject.transform.localScale = Vector3.one * (SimulatedBall.DefaultRadius * 2f);
            var collider = ballObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var trail = ballObject.AddComponent<TrailRenderer>();
            trail.time = 0.36f;
            trail.minVertexDistance = 0.025f;
            trail.startWidth = 0.065f;
            trail.endWidth = 0.01f;
            trail.startColor = new Color(1f, 0.96f, 0.35f, 0.9f);
            trail.endColor = new Color(1f, 0.96f, 0.35f, 0f);
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                trail.material = new Material(shader);
            }

            return ballObject.AddComponent<SimulatedBall>();
        }

        private List<PrototypePlayerAgent> CreateRoster(MatchContextV4 context)
        {
            var agents = new List<PrototypePlayerAgent>(12);
            CreateTeamAgents(agents, TeamId.Blue, context.Home, BlueColor);
            CreateTeamAgents(agents, TeamId.Orange, context.Away, OrangeColor);
            return agents;
        }

        private void CreateTeamAgents(
            ICollection<PrototypePlayerAgent> agents,
            TeamId team,
            TeamSnapshotV4 snapshot,
            Color color)
        {
            for (var index = 0; index < snapshot.Players.Count; index++)
            {
                var player = snapshot.Players[index];
                var role = RoleFor(player.Position);
                var playerObject = new GameObject($"{team}_{role}_{index + 1}");
                playerObject.transform.SetParent(transform, false);
                playerObject.transform.localPosition = Configuration.PositionFor(snapshot.Side, index + 1);
                if (team == TeamId.Orange)
                {
                    playerObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }

                var agent = playerObject.AddComponent<PrototypePlayerAgent>();
                agent.Initialize(
                    new PlayerId(team, role, index),
                    player.PlayerId,
                    color,
                    player.JerseyNumber.ToString());
                agent.SetAbility(new PlayerAbilityProfile(player.Derived));
                agent.SetCourtHalfLength(Configuration.CourtHalfLength);
                agents.Add(agent);
            }
        }

        private static PlayerRole RoleFor(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Setter => PlayerRole.Setter,
                PlayerPosition.OutsideHitter => PlayerRole.OutsideHitter,
                PlayerPosition.MiddleBlocker => PlayerRole.MiddleBlocker,
                PlayerPosition.Opposite => PlayerRole.Opposite,
                PlayerPosition.Libero => PlayerRole.Defender,
                _ => PlayerRole.Defender
            };
        }

        private static MatchContextV4 CreateSandboxContext()
        {
            return MatchContextV4.Create(
                Guid.Parse("66666666-2222-6666-2222-666666666666"),
                7351,
                CreateTeam("formal-home", "Blue", TeamSide.Home, "home"),
                CreateTeam("formal-away", "Orange", TeamSide.Away, "away"),
                RuntimePhysicsConfigurationHash,
                CreateRuntimeTrajectoryConfiguration(),
                rulesVersion: RulesVersions.FullRallyV3);
        }

        private static TeamSnapshotV4 CreateTeam(
            string id,
            string name,
            TeamSide side,
            string prefix)
        {
            return new TeamSnapshotV4(
                new Volleyball.Shared.Contracts.TeamId(id),
                name,
                side,
                new[]
                {
                    CreatePlayer(prefix + "-opposite", "Opposite", 1, PlayerPosition.Opposite),
                    CreatePlayer(prefix + "-outside-a", "Outside A", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-middle", "Middle", 3, PlayerPosition.MiddleBlocker),
                    CreatePlayer(prefix + "-setter", "Setter", 4, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-outside-b", "Outside B", 5, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-libero", "Libero", 6, PlayerPosition.Libero)
                });
        }

        private static PlayerSnapshotV4 CreatePlayer(
            string id,
            string name,
            int number,
            PlayerPosition position)
        {
            return new PlayerSnapshotV4(
                new StablePlayerId(id),
                name,
                number,
                position,
                DominantHandV4.Right,
                PhysicalFor(position),
                TechnicalFor(position),
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static PhysicalBaseAttributesV4 PhysicalFor(
            PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Setter => new PhysicalBaseAttributesV4(
                    1.91f, 2.43f, 0.80f, 0.90f, 0.93f, 0.91f),
                PlayerPosition.Libero => new PhysicalBaseAttributesV4(
                    1.84f, 2.34f, 0.72f, 0.94f, 0.95f, 0.94f),
                PlayerPosition.MiddleBlocker => new PhysicalBaseAttributesV4(
                    2.04f, 2.62f, 0.97f, 0.87f, 0.91f, 0.90f),
                PlayerPosition.Opposite => new PhysicalBaseAttributesV4(
                    2.00f, 2.57f, 0.95f, 0.90f, 0.89f, 0.90f),
                PlayerPosition.OutsideHitter => new PhysicalBaseAttributesV4(
                    1.96f, 2.51f, 0.93f, 0.92f, 0.91f, 0.92f),
                _ => new PhysicalBaseAttributesV4(
                    1.90f, 2.42f, 0.80f, 0.80f, 0.80f, 0.80f)
            };
        }

        private static TechnicalBaseAttributesV4 TechnicalFor(
            PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Setter => new TechnicalBaseAttributesV4(
                    0.74f, 0.70f, 0.72f, 0.80f, 0.80f, 0.95f, 0.72f, 0.94f, 0.93f),
                PlayerPosition.Libero => new TechnicalBaseAttributesV4(
                    0.62f, 0.60f, 0.65f, 0.97f, 0.97f, 0.76f, 0.68f, 0.90f, 0.95f),
                PlayerPosition.MiddleBlocker => new TechnicalBaseAttributesV4(
                    0.91f, 0.92f, 0.96f, 0.72f, 0.72f, 0.70f, 0.82f, 0.76f, 0.86f),
                PlayerPosition.Opposite => new TechnicalBaseAttributesV4(
                    0.95f, 0.95f, 0.91f, 0.75f, 0.75f, 0.74f, 0.91f, 0.78f, 0.88f),
                PlayerPosition.OutsideHitter => new TechnicalBaseAttributesV4(
                    0.94f, 0.92f, 0.86f, 0.86f, 0.86f, 0.76f, 0.90f, 0.82f, 0.91f),
                _ => new TechnicalBaseAttributesV4(
                    0.80f, 0.80f, 0.80f, 0.80f, 0.80f, 0.80f, 0.80f, 0.80f, 0.80f)
            };
        }
    }
}
