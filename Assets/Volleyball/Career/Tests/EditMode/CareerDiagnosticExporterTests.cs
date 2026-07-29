using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Bootstrap;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerDiagnosticExporterTests
    {
        [Serializable]
        private sealed class DiagnosticProbe
        {
            public int schemaVersion;
            public long revision;
            public bool hasPendingMatch;
            public int settlementReceiptCount;
        }

        [Test]
        public void ExportWritesParseablePrivacyBoundedReport()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "volleyball-diagnostic-export",
                Guid.NewGuid().ToString("N"));
            try
            {
                var snapshot = CareerSaveV2LifecycleTestData.SettledSnapshot();
                var exporter = new CareerDiagnosticExporter(root);
                var result = exporter.Export(
                    snapshot,
                    "WeekHome",
                    "loaded",
                    123456789L,
                    Guid.Parse("11111111-2222-4333-8444-555555555555"),
                    "6000.3.20f1",
                    "0.1.0",
                    "WindowsPlayer");

                Assert.That(result.Succeeded, Is.True);
                var json = File.ReadAllText(Path.Combine(
                    root,
                    "Diagnostics",
                    result.FileName));
                var probe = JsonUtility.FromJson<DiagnosticProbe>(json);

                Assert.That(probe, Is.Not.Null);
                Assert.That(probe.schemaVersion, Is.EqualTo(1));
                Assert.That(probe.revision, Is.EqualTo(snapshot.Identity.Revision));
                Assert.That(probe.hasPendingMatch, Is.False);
                Assert.That(probe.settlementReceiptCount, Is.EqualTo(1));
                Assert.That(json, Does.Not.Contain(",\n}"));
                Assert.That(json, Does.Not.Contain(snapshot.Identity.ProfileId.ToString()));
                Assert.That(json, Does.Not.Contain(snapshot.Identity.SaveId.ToString()));
                Assert.That(json, Does.Not.Contain(snapshot.Player.DisplayName));
                Assert.That(json, Does.Not.Contain(snapshot.CareerName));
                Assert.That(json, Does.Not.Contain(root));
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
