using NUnit.Framework;
using UnityEngine;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class AiRallyDirectorStateTests
    {
        [Test]
        public void NewDirector_HasNoCompletedRalliesOrScore()
        {
            var gameObject = new GameObject("DirectorTest");

            try
            {
                var director = gameObject.AddComponent<AiRallyDirector>();

                Assert.That(director.CompletedRallies, Is.Zero);
                Assert.That(director.TotalScore, Is.Zero);
                Assert.That(director.IsRallyActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
