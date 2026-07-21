using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using MatchContextV2 = Volleyball.Shared.Contracts.MatchContextV2;
using PlayerAbilitySnapshotV2 = Volleyball.Shared.Contracts.PlayerAbilitySnapshotV2;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using PlayerSnapshotV2 = Volleyball.Shared.Contracts.PlayerSnapshotV2;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TeamSnapshotV2 = Volleyball.Shared.Contracts.TeamSnapshotV2;

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
            Application.targetFrameRate = 60;
            CourtBuilder.Build(transform, Configuration.CourtHalfLength);
            var ball = CreateBall();
            var context = CreateSandboxContext();
            var agents = CreateRoster(context);
            var scoreDisplay = ScoreDisplay.Create(transform);
            var director = gameObject.AddComponent<FormalSixVsSixRallyDirector>();
            director.Initialize(
                ball,
                agents,
                context,
                scoreDisplay,
                configuration: Configuration);
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

        private List<PrototypePlayerAgent> CreateRoster(MatchContextV2 context)
        {
            var agents = new List<PrototypePlayerAgent>(12);
            CreateTeamAgents(agents, TeamId.Blue, context.Home, BlueColor);
            CreateTeamAgents(agents, TeamId.Orange, context.Away, OrangeColor);
            return agents;
        }

        private void CreateTeamAgents(
            ICollection<PrototypePlayerAgent> agents,
            TeamId team,
            TeamSnapshotV2 snapshot,
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
                agent.SetAbility(new PlayerAbilityProfile(player.Ability));
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

        private static PlayerAbilityProfile AbilityFor(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Setter => new PlayerAbilityProfile(
                    0.90f, 0.93f, 0.80f, 0.80f, 0.95f, 0.74f, 0.70f),
                PlayerPosition.Libero => new PlayerAbilityProfile(
                    0.94f, 0.95f, 0.72f, 0.97f, 0.76f, 0.62f, 0.60f),
                PlayerPosition.MiddleBlocker => new PlayerAbilityProfile(
                    0.87f, 0.91f, 0.97f, 0.72f, 0.70f, 0.91f, 0.92f),
                PlayerPosition.Opposite => new PlayerAbilityProfile(
                    0.90f, 0.89f, 0.95f, 0.75f, 0.74f, 0.95f, 0.95f),
                PlayerPosition.OutsideHitter => new PlayerAbilityProfile(
                    0.92f, 0.91f, 0.93f, 0.86f, 0.76f, 0.94f, 0.92f),
                _ => PlayerAbilityProfile.Default
            };
        }

        private static MatchContextV2 CreateSandboxContext()
        {
            return MatchContextV2.Create(
                Guid.Parse("66666666-2222-6666-2222-666666666666"),
                7351,
                CreateTeam("formal-home", "Blue", TeamSide.Home, "home"),
                CreateTeam("formal-away", "Orange", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV2 CreateTeam(
            string id,
            string name,
            TeamSide side,
            string prefix)
        {
            return new TeamSnapshotV2(
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

        private static PlayerSnapshotV2 CreatePlayer(
            string id,
            string name,
            int number,
            PlayerPosition position)
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
                    ability.AttackTechnique,
                    ability.AttackPower,
                    ReachFor(position)));
        }

        private static float ReachFor(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.MiddleBlocker => 3.48f,
                PlayerPosition.OutsideHitter => 3.42f,
                PlayerPosition.Opposite => 3.42f,
                _ => 3.20f
            };
        }
    }
}
