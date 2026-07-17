using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Simulation;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.PlayModeTests
{
    public sealed class PhysicsContactTrainingPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrainingScene_CompletesReceiveSetAndAttackWithPhysicalContacts()
        {
            yield return SceneManager.LoadSceneAsync("PhysicsContactTraining", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<PhysicsTrainingDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var receiver = GameObject.Find("Training_Defender")?.GetComponent<PrototypePlayerAgent>();
            var attacker = GameObject.Find("Training_Attacker")?.GetComponent<PrototypePlayerAgent>();
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);
            Assert.That(attacker, Is.Not.Null);

            var receivePalmSeparation = float.PositiveInfinity;
            var attackBallToPalmDistance = float.PositiveInfinity;
            var attackContactLocal = Vector3.zero;
            var attackOutgoing = SimVector3.Zero;
            ball.PlayerContact += contact =>
            {
                if (contact.Candidate.Action == TechniqueAction.Receive)
                {
                    receivePalmSeparation = Vector3.Distance(
                        receiver.Rig.GetJoint("LeftPalm").position,
                        receiver.Rig.GetJoint("RightPalm").position);
                }
                else if (contact.Candidate.Action == TechniqueAction.Attack)
                {
                    var impactCenter = new Vector3(
                        contact.Hit.ImpactCenter.X,
                        contact.Hit.ImpactCenter.Y,
                        contact.Hit.ImpactCenter.Z);
                    attackBallToPalmDistance = Vector3.Distance(
                        impactCenter,
                        attacker.Rig.GetJoint("RightPalm").position);
                    attackContactLocal = attacker.transform.InverseTransformPoint(impactCenter);
                    attackOutgoing = contact.TechniqueResponse.FinalOutgoing;
                }
            };

            var timeout = Time.realtimeSinceStartup + 10f;
            while (director.CompletedDrills < 3 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(director.CompletedDrills, Is.GreaterThanOrEqualTo(3));
            Assert.That(
                director.SuccessfulContacts,
                Is.GreaterThanOrEqualTo(3),
                $"contacts={director.SuccessfulContacts}, misses={director.MissedDrills}");
            Assert.That(director.MissedDrills, Is.Zero, $"contacts={director.SuccessfulContacts}");
            Assert.That(ball.Diagnostics.NonFiniteStates, Is.Zero);
            Assert.That(receivePalmSeparation, Is.LessThan(0.08f));
            Assert.That(
                attackBallToPalmDistance,
                Is.LessThanOrEqualTo(SimulatedBall.DefaultRadius + 0.05f));
            Assert.That(attackContactLocal.z, Is.GreaterThan(0.25f));
            Assert.That(attackContactLocal.y, Is.GreaterThan(1.1f));
            Assert.That(Mathf.Abs(attackContactLocal.x), Is.LessThan(0.4f));
            Assert.That(
                attackOutgoing.Magnitude,
                Is.GreaterThan(18f),
                $"attack outgoing={attackOutgoing}");
            Assert.That(
                attackOutgoing.Z / attackOutgoing.Magnitude,
                Is.GreaterThan(0.9f),
                $"attack outgoing={attackOutgoing}");
            Assert.That(
                Mathf.Abs(attackOutgoing.X),
                Is.LessThan(0.75f),
                $"attack outgoing={attackOutgoing}");
            var replay = new BallState(SimVector3.Zero, attackOutgoing, SimulatedBall.DefaultRadius);
            var replayParameters = new BallSimulationParameters(-9.8f, 0.9995f);
            for (var step = 0; step < 30; step++)
            {
                BallIntegrator.Step(replay, SimulatedBall.DefaultFixedStep, replayParameters);
            }

            var outgoingDirection = attackOutgoing.Normalized;
            var closestPointOnInitialRay = outgoingDirection *
                                           SimVector3.Dot(replay.Position, outgoingDirection);
            var straightLineDeviationRatio =
                (replay.Position - closestPointOnInitialRay).Magnitude / replay.Position.Magnitude;
            Assert.That(
                straightLineDeviationRatio,
                Is.LessThan(0.08f),
                $"attack path deviation ratio={straightLineDeviationRatio:0.000}");
            Assert.That(Camera.main.orthographicSize, Is.LessThanOrEqualTo(4.8f));
            Assert.That(ball.GetComponent<TrailRenderer>(), Is.Not.Null);
        }
    }
}
