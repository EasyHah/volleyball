using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);

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
        }
    }
}
