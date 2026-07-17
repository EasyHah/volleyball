using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class SimulatedBallTests
    {
        [Test]
        public void AdvanceSimulation_UsesFixedStepGravityAndUpdatesDiagnostics()
        {
            var gameObject = new GameObject("SimulatedBallTest");
            try
            {
                gameObject.transform.position = new Vector3(0f, 2f, 0f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                ball.Launch(new Vector3(2f, 0f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(ball.State.Position.X, Is.GreaterThan(0f));
                Assert.That(ball.State.Velocity.Y, Is.LessThan(0f));
                Assert.That(ball.Diagnostics.CompletedSteps, Is.EqualTo(1));
                Assert.That(ball.Diagnostics.MaximumSpeed, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_StopsActiveFlightOnSweptGroundContact()
        {
            var gameObject = new GameObject("SimulatedBallGroundTest");
            try
            {
                gameObject.transform.position = new Vector3(0f, 0.4f, 2f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                var contacts = 0;
                ball.EnvironmentContact += _ => contacts++;
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(ball.State.IsActive, Is.False);
                Assert.That(contacts, Is.EqualTo(1));
                Assert.That(ball.Diagnostics.GroundContacts, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_OnlyEmitsPlayerContactAfterSweptIntersection()
        {
            var gameObject = new GameObject("SimulatedBallPlayerContact");
            try
            {
                gameObject.transform.position = new Vector3(0f, 1.3f, 0f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                var source = new StaticContactSource();
                var contacts = 0;
                ball.RegisterContactSource(source);
                ball.PlayerContact += _ => contacts++;
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(contacts, Is.EqualTo(1));
                Assert.That(ball.State.Velocity.Y, Is.GreaterThan(0f));
                Assert.That(ball.State.LastContactGroupId, Is.EqualTo(77));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class StaticContactSource : IBallContactSource
        {
            public void CollectContacts(
                float simulationTime,
                float deltaSeconds,
                System.Collections.Generic.ICollection<BallContactCandidate> contacts)
            {
                var frame = new ContactSurfaceFrame(
                    new SimVector3(0f, 1f, 0f),
                    SimVector3.Up,
                    new SimVector3(1f, 0f, 0f),
                    new SimVector3(0f, 0f, 1f),
                    1f,
                    1f);
                contacts.Add(new BallContactCandidate(
                    new ContactSurfaceSnapshot(frame, frame, true, 77),
                    TechniqueAction.Receive,
                    0.8f,
                    new SimVector3(0f, 8f, 2f),
                    SimVector3.Up,
                    new ContactResponseParameters(0.85f, 1f, 0.1f, 0.08f)));
            }
        }
    }
}
