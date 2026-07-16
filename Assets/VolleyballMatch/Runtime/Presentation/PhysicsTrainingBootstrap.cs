using UnityEngine;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.Presentation
{
    public sealed class PhysicsTrainingBootstrap : MonoBehaviour
    {
        private static readonly Color BlueColor = new Color(0.1f, 0.42f, 0.95f);

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CourtBuilder.Build(transform);
            ConfigureTrainingCamera();

            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "PhysicalVolleyball";
            ballObject.transform.SetParent(transform, false);
            ballObject.transform.localScale = Vector3.one * (SimulatedBall.DefaultRadius * 2f);
            ballObject.transform.localPosition = new Vector3(0f, 2f, -4f);
            var collider = ballObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var ball = ballObject.AddComponent<SimulatedBall>();
            var receiver = CreateAgent(PlayerRole.Defender, new Vector3(-2.4f, 0f, -4.6f), "3");
            var setter = CreateAgent(PlayerRole.Setter, new Vector3(0f, 0f, -3.4f), "1");
            var attacker = CreateAgent(PlayerRole.Attacker, new Vector3(2.1f, 0f, -2.8f), "2");
            var director = gameObject.AddComponent<PhysicsTrainingDirector>();
            director.Initialize(ball, receiver, setter, attacker);
        }

        private static void ConfigureTrainingCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 8.5f, -10.2f),
                Quaternion.Euler(52f, 0f, 0f));
            camera.orthographicSize = 4.5f;
        }

        private PrototypePlayerAgent CreateAgent(PlayerRole role, Vector3 position, string jerseyNumber)
        {
            var playerObject = new GameObject("Training_" + role);
            playerObject.transform.SetParent(transform, false);
            playerObject.transform.localPosition = position;
            var agent = playerObject.AddComponent<PrototypePlayerAgent>();
            agent.Initialize(new PlayerId(TeamId.Blue, role), BlueColor, jerseyNumber);
            return agent;
        }
    }
}
