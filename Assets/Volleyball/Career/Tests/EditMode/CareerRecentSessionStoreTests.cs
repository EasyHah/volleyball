using System;
using System.IO;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerRecentSessionStoreTests
    {
        [Test]
        public void PointerRoundTripsAndCorruptionFallsBackWithoutAuthority()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "volleyball-recent-session",
                Guid.NewGuid().ToString("N"));
            try
            {
                var profileId = new ProfileId(Guid.NewGuid());
                var saveId = new SaveId(Guid.NewGuid());
                var store = new CareerRecentSessionStore(root);

                Assert.That(store.Remember(profileId, saveId), Is.True);
                Assert.That(store.TryRead(out var loadedProfile, out var loadedSave), Is.True);
                Assert.That(loadedProfile, Is.EqualTo(profileId));
                Assert.That(loadedSave, Is.EqualTo(saveId));

                File.WriteAllText(
                    Path.Combine(root, "CareerUi", "recent-session.v1"),
                    "not-a-session");
                Assert.That(store.TryRead(out _, out _), Is.False);
                Assert.That(File.Exists(Path.Combine(
                    root,
                    "CareerUi",
                    "recent-session.v1")), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
