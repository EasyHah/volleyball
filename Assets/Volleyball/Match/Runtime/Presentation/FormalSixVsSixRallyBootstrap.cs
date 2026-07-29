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

        private void Awake()
        {
            var scenario = FormalMatchScenarioStartupV4.ConsumePendingScenario();
            if (scenario == null)
            {
                Initialize(transform, CreateSandboxContext(), TeamSide.Home, 0, 0,
                    tactics: null, aiWeights: null, provenance: null);
                return;
            }

            InitializeScenario(transform, scenario);
        }

        public static FormalSixVsSixRallyDirector InitializeScenario(
            Transform host,
            FormalMatchScenarioDefinitionV4 scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            return Initialize(
                host,
                scenario.Context,
                scenario.FirstServingSide,
                scenario.HomeInitialRotationOffset,
                scenario.AwayInitialRotationOffset,
                scenario.CreateTactics(),
                scenario.Ai.ToRuntime(),
                new FormalMatchScenarioProvenanceV4(
                    scenario.ScenarioId,
                    scenario.FormatVersionValue,
                    scenario.ContentHash));
        }

        private static FormalSixVsSixRallyDirector Initialize(
            Transform host,
            MatchContextV4 context,
            TeamSide firstServingSide,
            int homeInitialRotationOffset,
            int awayInitialRotationOffset,
            Volleyball.AI.PhysicalRallyTactics? tactics,
            Volleyball.AI.RallyTacticalWeights? aiWeights,
            FormalMatchScenarioProvenanceV4 provenance)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            Application.targetFrameRate = 60;
            CourtBuilder.Build(host, Configuration.CourtHalfLength);
            var ball = CreateBall(host);
            var agents = CreateRoster(
                host,
                context,
                homeInitialRotationOffset,
                awayInitialRotationOffset);
            var scoreDisplay = ScoreDisplay.Create(host);
            var director = host.gameObject.AddComponent<FormalSixVsSixRallyDirector>();
            if (tactics.HasValue)
            {
                director.ConfigureFormalScenario(
                    tactics.Value,
                    aiWeights ?? throw new InvalidOperationException(
                        "A formal scenario requires complete AI input."),
                    provenance);
            }
            director.InitializeV4(
                ball,
                agents,
                context,
                scoreDisplay,
                configuration: Configuration,
                firstServingSide: firstServingSide,
                homeInitialRotationOffset: homeInitialRotationOffset,
                awayInitialRotationOffset: awayInitialRotationOffset);
            director.ConfigureV3Rules(V3RulesMode.Authority);
            var rosterDisplay = host.gameObject.AddComponent<MatchRosterDisplay>();
            rosterDisplay.Initialize(director, agents);
            var cameras = host.gameObject.AddComponent<RallyCameraController>();
            cameras.Initialize(ball);
            return director;
        }

        private static SimulatedBall CreateBall(Transform host)
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Formal6v6Ball";
            ballObject.transform.SetParent(host, false);
            ballObject.transform.localScale = Vector3.one * (SimulatedBall.DefaultRadius * 2f);
            var collider = ballObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
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

        private static List<PrototypePlayerAgent> CreateRoster(
            Transform host,
            MatchContextV4 context,
            int homeInitialRotationOffset,
            int awayInitialRotationOffset)
        {
            var agents = new List<PrototypePlayerAgent>(12);
            CreateTeamAgents(
                host,
                agents,
                TeamId.Blue,
                context.Home,
                BlueColor,
                homeInitialRotationOffset);
            CreateTeamAgents(
                host,
                agents,
                TeamId.Orange,
                context.Away,
                OrangeColor,
                awayInitialRotationOffset);
            return agents;
        }

        private static void CreateTeamAgents(
            Transform host,
            ICollection<PrototypePlayerAgent> agents,
            TeamId team,
            TeamSnapshotV4 snapshot,
            Color color,
            int initialRotationOffset)
        {
            for (var index = 0; index < snapshot.Players.Count; index++)
            {
                var player = snapshot.Players[index];
                var role = RoleFor(player.Position);
                var playerObject = new GameObject($"{team}_{role}_{index + 1}");
                playerObject.transform.SetParent(host, false);
                var rotationPosition =
                    ((index - initialRotationOffset + snapshot.Players.Count) %
                     snapshot.Players.Count) + 1;
                playerObject.transform.localPosition = Configuration.PositionFor(
                    snapshot.Side,
                    rotationPosition);
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
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                    new BallSimulationParameters(-9.8f, 0.9995f)),
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    BallTrajectoryPredictionProviderV4.CurrentPredictorVersion,
                    BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash),
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
