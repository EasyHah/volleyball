using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
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

        [Test]
        public void AdvanceSimulation_IgnoresRejectedEarlyCandidateBeforeApplyingResponse()
        {
            var ball = CreateBallWithTwoSweptCandidates(out var gameObject);
            try
            {
                ball.ContactCandidateResolver = (candidate, _, __) =>
                    candidate.Actor.Value.Role == PlayerRole.Defender
                        ? BallContactResolution.Ignore()
                        : BallContactResolution.Accept();
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(ball.State.LastContactGroupId, Is.EqualTo(78));
                Assert.That(ball.State.Velocity.Y, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_FaultsBeforeAnyVelocityResponse()
        {
            var ball = CreateBallWithTwoSweptCandidates(out var gameObject);
            try
            {
                PlayerContactRejectedEvent rejected = default;
                ball.ContactCandidateResolver = (_, __, ___) =>
                    BallContactResolution.Fault("fourth counted touch");
                ball.PlayerContactRejected += value => rejected = value;
                ball.Launch(new Vector3(0f, -40f, 0f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(rejected.Reason, Is.EqualTo("fourth counted touch"));
                Assert.That(rejected.Candidate.Actor.Value.Role, Is.EqualTo(PlayerRole.Defender));
                Assert.That(rejected.ContactSimulationTime, Is.GreaterThan(0f));
                Assert.That(ball.State.LastContactGroupId, Is.Null);
                Assert.That(ball.State.Velocity.Y, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_BlockBeforeNetPlaneSuppressesCrossingEvent()
        {
            var gameObject = new GameObject("SimulatedBallBlockOrdering");
            try
            {
                gameObject.transform.position = new Vector3(0f, 1.3f, -0.3f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                ball.RegisterContactSource(new NetBlockContactSource(-0.1f, 79));
                ball.ContactCandidateResolver = (_, __, ___) => BallContactResolution.Accept();
                var crossings = 0;
                var contacts = 0;
                ball.NetPlaneCrossed += _ => crossings++;
                ball.PlayerContact += _ => contacts++;
                ball.Launch(new Vector3(0f, 0f, 40f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(contacts, Is.EqualTo(1));
                Assert.That(crossings, Is.Zero);
                Assert.That(ball.State.LastContactGroupId, Is.EqualTo(79));
                Assert.That(ball.State.Velocity.Z, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_OverlappingArmCapsulesEmitOneBlockContact()
        {
            var gameObject = new GameObject("SimulatedBallArmCapsules");
            try
            {
                gameObject.transform.position = new Vector3(0f, 1.3f, 0.3f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                ball.RegisterContactSource(new OverlappingArmCapsuleSource());
                ball.ContactCandidateResolver = (_, __, ___) => BallContactResolution.Accept();
                var contacts = 0;
                PlayerBallContactEvent accepted = default;
                ball.PlayerContact += value =>
                {
                    contacts++;
                    accepted = value;
                };
                ball.Launch(new Vector3(0f, 0f, -40f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(contacts, Is.EqualTo(1));
                Assert.That(accepted.Candidate.Action, Is.EqualTo(TechniqueAction.Block));
                Assert.That(accepted.Hit.ContactGroupId, Is.EqualTo(81));
                Assert.That(ball.State.LastContactGroupId, Is.EqualTo(81));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AdvanceSimulation_ConsumedCrossingSuppressesLaterPlayerAndGroundResponses()
        {
            var gameObject = new GameObject("SimulatedBallConsumedCrossing");
            try
            {
                gameObject.transform.position = new Vector3(5f, 0.4f, -0.3f);
                var ball = gameObject.AddComponent<SimulatedBall>();
                ball.RegisterContactSource(new NetBlockContactSource(0.3f, 80, 5f));
                var playerContacts = 0;
                var environmentContacts = 0;
                var crossings = 0;
                ball.PlayerContact += _ => playerContacts++;
                ball.EnvironmentContact += _ => environmentContacts++;
                ball.NetPlaneCrossed += crossing =>
                {
                    crossings++;
                    crossing.ConsumeRemainingStep();
                };
                ball.Launch(new Vector3(0f, -20f, 80f));

                ball.AdvanceSimulation(1d / 120d);

                Assert.That(crossings, Is.EqualTo(1));
                Assert.That(playerContacts, Is.Zero);
                Assert.That(environmentContacts, Is.Zero);
                Assert.That(ball.State.IsActive, Is.True);
                Assert.That(ball.State.LastContactGroupId, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static SimulatedBall CreateBallWithTwoSweptCandidates(out GameObject gameObject)
        {
            gameObject = new GameObject("SimulatedBallTwoCandidates");
            gameObject.transform.position = new Vector3(0f, 1.4f, 0f);
            var ball = gameObject.AddComponent<SimulatedBall>();
            ball.RegisterContactSource(new TwoCandidateContactSource());
            return ball;
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

        private sealed class TwoCandidateContactSource : IBallContactSource
        {
            public void CollectContacts(
                float simulationTime,
                float deltaSeconds,
                System.Collections.Generic.ICollection<BallContactCandidate> contacts)
            {
                contacts.Add(CreateHorizontalCandidate(
                    1.1f,
                    77,
                    new PlayerId(TeamId.Blue, PlayerRole.Defender)));
                contacts.Add(CreateHorizontalCandidate(
                    0.98f,
                    78,
                    new PlayerId(TeamId.Blue, PlayerRole.Setter)));
            }

            private static BallContactCandidate CreateHorizontalCandidate(
                float worldHeight,
                int contactGroupId,
                PlayerId actor)
            {
                var frame = new ContactSurfaceFrame(
                    new SimVector3(0f, worldHeight, 0f),
                    SimVector3.Up,
                    new SimVector3(1f, 0f, 0f),
                    new SimVector3(0f, 0f, 1f),
                    1f,
                    1f);
                return new BallContactCandidate(
                    new ContactSurfaceSnapshot(frame, frame, true, contactGroupId),
                    TechniqueAction.Receive,
                    actor,
                    0.8f,
                    new SimVector3(0f, 8f, 2f),
                    SimVector3.Up,
                    new ContactResponseParameters(0.85f, 1f, 0.1f, 0.08f));
            }
        }

        private sealed class NetBlockContactSource : IBallContactSource
        {
            private readonly float _worldDepth;
            private readonly int _contactGroupId;
            private readonly float _worldX;

            public NetBlockContactSource(float worldDepth, int contactGroupId, float worldX = 0f)
            {
                _worldDepth = worldDepth;
                _contactGroupId = contactGroupId;
                _worldX = worldX;
            }

            public void CollectContacts(
                float simulationTime,
                float deltaSeconds,
                System.Collections.Generic.ICollection<BallContactCandidate> contacts)
            {
                var frame = new ContactSurfaceFrame(
                    new SimVector3(_worldX, 0.8f, _worldDepth),
                    new SimVector3(0f, 0f, -1f),
                    new SimVector3(1f, 0f, 0f),
                    SimVector3.Up,
                    1f,
                    2f);
                contacts.Add(new BallContactCandidate(
                    new ContactSurfaceSnapshot(frame, frame, true, _contactGroupId),
                    TechniqueAction.Block,
                    new PlayerId(TeamId.Orange, PlayerRole.Attacker),
                    0.8f,
                    new SimVector3(0f, 2f, -12f),
                    new SimVector3(0f, 0f, -1f),
                    new ContactResponseParameters(0.85f, 1f, 0.1f, 0.08f)));
            }
        }

        private sealed class OverlappingArmCapsuleSource : IBallContactSource
        {
            public void CollectContacts(
                float simulationTime,
                float deltaSeconds,
                System.Collections.Generic.ICollection<BallContactCandidate> contacts)
            {
                var frame = new ContactCapsuleFrame(
                    new SimVector3(-0.3f, 1.3f, 0f),
                    new SimVector3(0.3f, 1.3f, 0f),
                    0.065f);
                var capsule = new ContactCapsuleSnapshot(frame, frame, true, 81);
                for (var index = 0; index < 2; index++)
                {
                    contacts.Add(new BallContactCandidate(
                        capsule,
                        TechniqueAction.Block,
                        new PlayerId(TeamId.Orange, PlayerRole.Attacker),
                        0.8f,
                        new SimVector3(0f, 2f, 12f),
                        new SimVector3(0f, 0f, 1f),
                        new ContactResponseParameters(0.85f, 1f, 0.1f, 0.08f)));
                }
            }
        }
    }
}
