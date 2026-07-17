using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using MatchContextV1 = Volleyball.Shared.Contracts.MatchContextV1;
using PlayerAbilitySnapshotV1 = Volleyball.Shared.Contracts.PlayerAbilitySnapshotV1;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using PlayerSnapshotV1 = Volleyball.Shared.Contracts.PlayerSnapshotV1;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TeamSnapshotV1 = Volleyball.Shared.Contracts.TeamSnapshotV1;

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
            var agents = CreateSixAgents();
            var scoreDisplay = ScoreDisplay.Create(transform);
            var director = gameObject.AddComponent<ThreeVsThreeRallyDirector>();
            director.Initialize(ball, agents, CreateSandboxContext(), scoreDisplay);
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

        private List<PrototypePlayerAgent> CreateSixAgents()
        {
            var agents = new List<PrototypePlayerAgent>(6);
            CreateAgent(agents, TeamId.Blue, PlayerRole.Defender, new Vector3(-2.5f, 0f, -5.2f), BlueColor, "3");
            CreateAgent(agents, TeamId.Blue, PlayerRole.Setter, new Vector3(0f, 0f, -3.4f), BlueColor, "1");
            CreateAgent(agents, TeamId.Blue, PlayerRole.Attacker, new Vector3(2.1f, 0f, -2.6f), BlueColor, "2");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Defender, new Vector3(2.5f, 0f, 5.2f), OrangeColor, "6");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Setter, new Vector3(0f, 0f, 3.4f), OrangeColor, "4");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Attacker, new Vector3(-2.1f, 0f, 2.6f), OrangeColor, "5");
            return agents;
        }

        private void CreateAgent(
            ICollection<PrototypePlayerAgent> agents,
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
            agent.SetAbility(AbilityFor(team, role));
            agents.Add(agent);
        }

        private static PlayerAbilityProfile AbilityFor(TeamId team, PlayerRole role)
        {
            var teamAdjustment = team == TeamId.Blue ? 0.01f : 0f;
            return role switch
            {
                PlayerRole.Defender => new PlayerAbilityProfile(
                    0.88f + teamAdjustment,
                    0.91f + teamAdjustment,
                    0.78f,
                    0.94f + teamAdjustment,
                    0.74f,
                    0.70f,
                    0.68f),
                PlayerRole.Setter => new PlayerAbilityProfile(
                    0.90f + teamAdjustment,
                    0.93f + teamAdjustment,
                    0.80f,
                    0.80f,
                    0.95f + teamAdjustment,
                    0.74f,
                    0.70f),
                PlayerRole.Attacker => new PlayerAbilityProfile(
                    0.91f + teamAdjustment,
                    0.89f + teamAdjustment,
                    0.94f,
                    0.72f,
                    0.72f,
                    0.93f + teamAdjustment,
                    0.92f + teamAdjustment),
                _ => PlayerAbilityProfile.Default
            };
        }

        private static MatchContextV1 CreateSandboxContext()
        {
            return MatchContextV1.Create(
                System.Guid.Parse("22222222-2222-2222-2222-222222222222"),
                7351,
                CreateTeam("sandbox-home", "Blue", TeamSide.Home, "home"),
                CreateTeam("sandbox-away", "Orange", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV1 CreateTeam(string id, string name, TeamSide side, string prefix)
        {
            return new TeamSnapshotV1(
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

        private static PlayerSnapshotV1 CreatePlayer(string id, string name, int number, PlayerPosition position)
        {
            return new PlayerSnapshotV1(
                new StablePlayerId(id),
                name,
                number,
                position,
                new PlayerAbilitySnapshotV1(0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f));
        }
    }
}
