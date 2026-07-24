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
    public sealed class ThreeVsThreeRallyBootstrap : MonoBehaviour
    {
        private static readonly Color BlueColor = new Color(0.1f, 0.42f, 0.95f);
        private static readonly Color OrangeColor = new Color(1f, 0.38f, 0.08f);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CourtBuilder.Build(transform);
            var ball = CreateBall();
            var context = CreateSandboxContext();
            var agents = CreateSixAgents(context);
            var scoreDisplay = ScoreDisplay.Create(transform);
            var director = gameObject.AddComponent<ThreeVsThreeRallyDirector>();
            director.InitializePrototypeV4(ball, agents, context, scoreDisplay);
            var cameras = gameObject.AddComponent<RallyCameraController>();
            cameras.Initialize(ball);
        }

        private SimulatedBall CreateBall()
        {
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Physical3v3Ball";
            ballObject.transform.SetParent(transform, false);
            ballObject.transform.localScale = Vector3.one * (SimulatedBall.DefaultRadius * 2f);
            var collider = ballObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var trail = ballObject.AddComponent<TrailRenderer>();
            trail.time = 0.32f;
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

        private List<PrototypePlayerAgent> CreateSixAgents(MatchContextV4 context)
        {
            var agents = new List<PrototypePlayerAgent>(6);
            CreateAgent(agents, context, TeamId.Blue, PlayerRole.Defender, new Vector3(-2.5f, 0f, -5.2f), BlueColor, "3");
            CreateAgent(agents, context, TeamId.Blue, PlayerRole.Setter, new Vector3(0f, 0f, -3.4f), BlueColor, "1");
            CreateAgent(agents, context, TeamId.Blue, PlayerRole.Attacker, new Vector3(2.1f, 0f, -2.6f), BlueColor, "2");
            CreateAgent(agents, context, TeamId.Orange, PlayerRole.Defender, new Vector3(2.5f, 0f, 5.2f), OrangeColor, "6");
            CreateAgent(agents, context, TeamId.Orange, PlayerRole.Setter, new Vector3(0f, 0f, 3.4f), OrangeColor, "4");
            CreateAgent(agents, context, TeamId.Orange, PlayerRole.Attacker, new Vector3(-2.1f, 0f, 2.6f), OrangeColor, "5");
            return agents;
        }

        private void CreateAgent(
            ICollection<PrototypePlayerAgent> agents,
            MatchContextV4 context,
            TeamId team,
            PlayerRole role,
            Vector3 position,
            Color color,
            string jerseyNumber)
        {
            var playerObject = new GameObject(team + "_" + role);
            playerObject.transform.SetParent(transform, false);
            playerObject.transform.localPosition = position;
            if (team == TeamId.Orange)
            {
                playerObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            var agent = playerObject.AddComponent<PrototypePlayerAgent>();
            var positionForAbility = role switch
            {
                PlayerRole.Defender => PlayerPosition.Defender,
                PlayerRole.Setter => PlayerPosition.Setter,
                PlayerRole.Attacker => PlayerPosition.OutsideHitter,
                _ => throw new System.ArgumentOutOfRangeException(nameof(role))
            };
            var snapshot = PlayerFor(context, team, positionForAbility);
            agent.Initialize(
                new PlayerId(team, role),
                snapshot.PlayerId,
                color,
                jerseyNumber);
            agent.SetAbility(new PlayerAbilityProfile(snapshot.Derived));
            agents.Add(agent);
        }

        private static PlayerSnapshotV4 PlayerFor(
            MatchContextV4 context,
            TeamId team,
            PlayerPosition position)
        {
            var snapshot = team == TeamId.Blue ? context.Home : context.Away;

            foreach (var player in snapshot.Players)
            {
                if (player.Position == position)
                {
                    return player;
                }
            }

            throw new System.InvalidOperationException("The sandbox roster is missing its required position.");
        }

        private static MatchContextV4 CreateSandboxContext()
        {
            return MatchContextV4.Create(
                System.Guid.Parse("22222222-2222-2222-2222-222222222222"),
                7351,
                CreateTeam("sandbox-home", "Blue", TeamSide.Home, "home"),
                CreateTeam("sandbox-away", "Orange", TeamSide.Away, "away"),
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                    new BallSimulationParameters(-9.8f, 0.9995f)),
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    BallTrajectoryPredictionProviderV4.CurrentPredictorVersion,
                    BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash),
                rulesVersion: Volleyball.Shared.Contracts.RulesVersions.FullRallyV3);
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
                    CreatePlayer(prefix + "-setter", "Setter", 1, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-defender", "Defender", 3, PlayerPosition.Defender),
                    CreatePlayer(prefix + "-reserve-outside", "Reserve Outside", 4, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-reserve-middle", "Reserve Middle", 5, PlayerPosition.MiddleBlocker),
                    CreatePlayer(prefix + "-reserve-libero", "Reserve Libero", 6, PlayerPosition.Libero)
                });
        }

        private static PlayerSnapshotV4 CreatePlayer(
            string id,
            string name,
            int number,
            PlayerPosition position)
        {
            var ratings = RatingsFor(position);
            return new PlayerSnapshotV4(
                new StablePlayerId(id),
                name,
                number,
                position,
                DominantHandV4.Right,
                new PhysicalBaseAttributesV4(
                    1.90f,
                    2.42f,
                    ratings.Jump,
                    ratings.Mobility,
                    ratings.Reaction,
                    0.8f),
                new TechnicalBaseAttributesV4(
                    ratings.AttackTechnique,
                    ratings.AttackPower,
                    0.8f,
                    ratings.ReceiveTechnique,
                    ratings.ReceiveTechnique,
                    ratings.SetTechnique,
                    ratings.AttackTechnique,
                    0.8f,
                    ratings.Reaction),
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static SandboxRatings RatingsFor(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Defender => new SandboxRatings(
                    0.88f, 0.91f, 0.78f, 0.94f, 0.74f, 0.70f, 0.68f),
                PlayerPosition.Setter => new SandboxRatings(
                    0.90f, 0.93f, 0.80f, 0.80f, 0.95f, 0.74f, 0.70f),
                PlayerPosition.OutsideHitter => new SandboxRatings(
                    0.91f, 0.89f, 0.94f, 0.72f, 0.72f, 0.93f, 0.92f),
                PlayerPosition.MiddleBlocker => new SandboxRatings(
                    0.84f, 0.86f, 0.92f, 0.68f, 0.64f, 0.86f, 0.88f),
                PlayerPosition.Libero => new SandboxRatings(
                    0.92f, 0.94f, 0.72f, 0.96f, 0.78f, 0.62f, 0.58f),
                _ => throw new System.ArgumentOutOfRangeException(nameof(position))
            };
        }

        private readonly struct SandboxRatings
        {
            public SandboxRatings(
                float mobility,
                float reaction,
                float jump,
                float receiveTechnique,
                float setTechnique,
                float attackTechnique,
                float attackPower)
            {
                Mobility = mobility;
                Reaction = reaction;
                Jump = jump;
                ReceiveTechnique = receiveTechnique;
                SetTechnique = setTechnique;
                AttackTechnique = attackTechnique;
                AttackPower = attackPower;
            }

            public float Mobility { get; }
            public float Reaction { get; }
            public float Jump { get; }
            public float ReceiveTechnique { get; }
            public float SetTechnique { get; }
            public float AttackTechnique { get; }
            public float AttackPower { get; }
        }
    }
}
