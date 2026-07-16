using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.PlayModeTests
{
    public sealed class AiRallyPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator PrototypeScene_CompletesThreeRalliesWithOnePointEach()
        {
            yield return SceneManager.LoadSceneAsync("AiRallyPrototype", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<AiRallyDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None), Has.Length.EqualTo(6));

            var timeout = Time.realtimeSinceStartup + 35f;
            while (director.CompletedRallies < 3 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(director.CompletedRallies, Is.EqualTo(3));
            Assert.That(director.TotalScore, Is.EqualTo(3));

            while (!director.IsRallyActive && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(director.IsRallyActive, Is.True);
        }
    }
}
