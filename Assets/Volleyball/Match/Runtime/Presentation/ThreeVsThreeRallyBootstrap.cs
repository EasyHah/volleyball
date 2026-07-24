using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using DominantHandV4 = Volleyball.Shared.Contracts.DominantHandV4;
using MatchAttributeDerivationConfigV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationConfigV4;
using MatchAttributeDerivationV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationV4;
using MatchContextV2 = Volleyball.Shared.Contracts.MatchContextV2;
using PhysicalBaseAttributesV4 = Volleyball.Shared.Contracts.PhysicalBaseAttributesV4;
using PlayerAbilitySnapshotV2 = Volleyball.Shared.Contracts.PlayerAbilitySnapshotV2;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using PlayerSnapshotV2 = Volleyball.Shared.Contracts.PlayerSnapshotV2;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TechnicalBaseAttributesV4 = Volleyball.Shared.Contracts.TechnicalBaseAttributesV4;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TeamSnapshotV2 = Volleyball.Shared.Contracts.TeamSnapshotV2;

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
            director.InitializePrototypeLegacyV2(ball, agents, context, scoreDisplay);
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

        private List<PrototypePlayerAgent> CreateSixAgents(MatchContextV2 context)
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
            MatchContextV2 context,
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
            agent.Initialize(new PlayerId(team, role), color, jerseyNumber);
            var positionForAbility = role switch
            {
                PlayerRole.Defender => PlayerPosition.Defender,
                PlayerRole.Setter => PlayerPosition.Setter,
                PlayerRole.Attacker => PlayerPosition.OutsideHitter,
                _ => throw new System.ArgumentOutOfRangeException(nameof(role))
            };
            agent.SetAbility(AbilityFor(positionForAbility));
            agents.Add(agent);
        }

        private static PlayerAbilitySnapshotV2 AbilityFor(
            MatchContextV2 context,
            TeamId team,
            PlayerRole role)
        {
            var snapshot = team == TeamId.Blue ? context.Home : context.Away;
            var position = role switch
            {
                PlayerRole.Defender => PlayerPosition.Defender,
                PlayerRole.Setter => PlayerPosition.Setter,
                PlayerRole.Attacker => PlayerPosition.OutsideHitter,
                _ => throw new System.ArgumentOutOfRangeException(nameof(role))
            };

            foreach (var player in snapshot.Players)
            {
                if (player.Position == position)
                {
                    return player.Ability;
                }
            }

            throw new System.InvalidOperationException("The sandbox roster is missing its required position.");
        }

        private static MatchContextV2 CreateSandboxContext()
        {
            return MatchContextV2.Create(
                System.Guid.Parse("22222222-2222-2222-2222-222222222222"),
                7351,
                CreateTeam("sandbox-home", "Blue", TeamSide.Home, "home"),
                CreateTeam("sandbox-away", "Orange", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV2 CreateTeam(string id, string name, TeamSide side, string prefix)
        {
            return new TeamSnapshotV2(
                new Volleyball.Shared.Contracts.TeamId(id),
                name,
                side,
                new[]
                {
                    CreatePlayer(prefix + "-setter", "Setter", 1, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-defender", "Defender", 3, PlayerPosition.Defender)
                });
        }

        private static PlayerSnapshotV2 CreatePlayer(string id, string name, int number, PlayerPosition position)
        {
            var ability = AbilityFor(position);
            return new PlayerSnapshotV2(
                new StablePlayerId(id),
                name,
                number,
                position,
                new PlayerAbilitySnapshotV2(
                    ability.Mobility,
                    ability.Reaction,
                    ability.Jump,
                    ability.ReceiveTechnique,
                    ability.SetTechnique,
                    ability.AttackDirectionControl,
                    ability.AttackPowerCapacity,
                    Mathf.Max(3.20f, ability.PlannedAttackContactHeightMeters)));
        }

        private static PlayerAbilityProfile AbilityFor(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Defender => new PlayerAbilityProfile(
                    Derive(0.88f, 0.91f, 0.78f, 0.94f, 0.74f, 0.70f, 0.68f)),
                PlayerPosition.Setter => new PlayerAbilityProfile(
                    Derive(0.90f, 0.93f, 0.80f, 0.80f, 0.95f, 0.74f, 0.70f)),
                PlayerPosition.OutsideHitter => new PlayerAbilityProfile(
                    Derive(0.91f, 0.89f, 0.94f, 0.72f, 0.72f, 0.93f, 0.92f)),
                _ => throw new System.ArgumentOutOfRangeException(nameof(position))
            };
        }

        private static Volleyball.Shared.Contracts.DerivedMatchAttributesV4 Derive(
            float mobility,
            float reaction,
            float jump,
            float receiveTechnique,
            float setTechnique,
            float attackTechnique,
            float attackPower)
        {
            return MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(
                    1.90f,
                    2.42f,
                    jump,
                    mobility,
                    reaction,
                    0.8f),
                new TechnicalBaseAttributesV4(
                    attackTechnique,
                    attackPower,
                    0.8f,
                    receiveTechnique,
                    receiveTechnique,
                    setTechnique,
                    attackTechnique,
                    0.8f,
                    reaction),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }
    }
}
