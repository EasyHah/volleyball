using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VolleyballMatch.Domain.Players;
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
            Assert.That(Camera.main.orthographicSize, Is.LessThanOrEqualTo(4.5f));
        }
    }
}
