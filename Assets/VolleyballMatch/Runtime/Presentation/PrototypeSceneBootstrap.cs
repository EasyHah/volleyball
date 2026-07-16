using System.Collections.Generic;
using UnityEngine;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.Presentation
{
    public sealed class PrototypeSceneBootstrap : MonoBehaviour
    {
        private static readonly Color BlueColor = new Color(0.1f, 0.42f, 0.95f);
        private static readonly Color OrangeColor = new Color(1f, 0.38f, 0.08f);

        [SerializeField]
        private int _seed = 7429;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CourtBuilder.Build(transform);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Volleyball";
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * 0.32f;
            ball.transform.localPosition = new Vector3(0f, 2.8f, -4.5f);
            var ballFlight = ball.AddComponent<BallFlight>();

            var scoreDisplay = ScoreDisplay.Create(transform);
            var agents = CreateSixAgents();
            var director = gameObject.AddComponent<AiRallyDirector>();
            director.Initialize(_seed, agents, ballFlight, scoreDisplay);
        }

        private List<PrototypePlayerAgent> CreateSixAgents()
        {
            var agents = new List<PrototypePlayerAgent>(6);
            CreateAgent(agents, TeamId.Blue, PlayerRole.Setter, new Vector3(-2.5f, 0f, -4.5f), BlueColor, "1");
            CreateAgent(agents, TeamId.Blue, PlayerRole.Attacker, new Vector3(0f, 0f, -2.5f), BlueColor, "2");
            CreateAgent(agents, TeamId.Blue, PlayerRole.Defender, new Vector3(2.5f, 0f, -4.5f), BlueColor, "3");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Setter, new Vector3(-2.5f, 0f, 4.5f), OrangeColor, "4");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Attacker, new Vector3(0f, 0f, 2.5f), OrangeColor, "5");
            CreateAgent(agents, TeamId.Orange, PlayerRole.Defender, new Vector3(2.5f, 0f, 4.5f), OrangeColor, "6");
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
            agents.Add(agent);
        }
    }
}
