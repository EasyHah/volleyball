using NUnit.Framework;
using UnityEngine;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class CourtBuilderTests
    {
        [Test]
        public void Build_CreatesCourtNetAndOrthographicCamera()
        {
            var root = new GameObject("CourtTestRoot");

            try
            {
                var court = CourtBuilder.Build(root.transform);

                Assert.That(court.Find("Net"), Is.Not.Null);
                Assert.That(court.GetComponentInChildren<Camera>().orthographic, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
