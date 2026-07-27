using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class CourtPerceptionV3Tests
    {
        [Test]
        public void Configuration_RejectsInvalidIdentityFiniteValuesAndInvertedRanges()
        {
            Assert.Throws<ArgumentException>(() => new CourtPerceptionConfigurationV3(" ", 0f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CourtPerceptionConfigurationV3("gate-j", float.NaN, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CourtPerceptionConfigurationV3("gate-j", -0.01f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CourtPerceptionConfigurationV3("gate-j", 1f, 0f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CourtPerceptionConfigurationV3("gate-j", 0f, 1f, 2f, 1f));
        }

        [Test]
        public void TeamSnapshot_CopiesAndCanonicallySortsPublicFacts()
        {
            var threats = new[]
            {
                new PerceivedThreatEntryV3("threat-b", "deep", 0.4f, 1.2f),
                new PerceivedThreatEntryV3("threat-a", "line", 0.6f, 1.1f)
            };
            var supports = new[]
            {
                new PerceivedSupportCandidateV3(new PlayerId("home-2"), 0.5f, 0.7f, false),
                new PerceivedSupportCandidateV3(new PlayerId("home-1"), 0.8f, 0.9f, true)
            };

            var view = new TeamPerceptionSnapshotV3(
                "view-1", "artifact-1", TeamSide.Home, 2L, 4L,
                new[]
                {
                    new PlayerPerceptionSnapshotV3(new PlayerId("home-2"), 0.4f, 0.1f),
                    new PlayerPerceptionSnapshotV3(new PlayerId("home-1"), 0.9f, 0.2f)
                }, threats, supports);
            threats[0] = new PerceivedThreatEntryV3("changed", "x", 0.1f, 1f);
            supports[0] = new PerceivedSupportCandidateV3(new PlayerId("home-9"), 0.1f, 0.1f, false);

            CollectionAssert.AreEqual(new[] { "home-1", "home-2" }, view.Players.Select(value => value.PlayerId.Value));
            CollectionAssert.AreEqual(new[] { "threat-a", "threat-b" }, view.Threats.Select(value => value.ThreatIdentity));
            CollectionAssert.AreEqual(new[] { "home-1", "home-2" }, view.SupportCandidates.Select(value => value.PlayerId.Value));
            Assert.Throws<NotSupportedException>(() => ((IList<PerceivedThreatEntryV3>)view.Threats)[0] = threats[0]);
        }

        [Test]
        public void PerceptionContracts_ExposeNoSelectedRouteOrFutureSample()
        {
            var names = new[]
            {
                typeof(PerceptionObservationV3<string>), typeof(PlayerPerceptionSnapshotV3),
                typeof(TeamPerceptionSnapshotV3), typeof(PerceivedThreatEntryV3),
                typeof(PerceivedSupportCandidateV3), typeof(PerceptionSupportDecisionV3)
            }.SelectMany(type => type.GetProperties()).Select(property => property.Name);

            Assert.That(names, Has.None.Matches<string>(value =>
                value.IndexOf("Route", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Sample", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void Observation_RetainsImmutableConfidenceUncertaintyAndSources()
        {
            var sources = new[] { new PlayerId("home-2"), new PlayerId("home-1") };
            var observation = new PerceptionObservationV3<string>(
                "visible-ball", .7f, .2f, .1f, "key-1", sources);
            sources[0] = new PlayerId("changed");

            Assert.That(observation.Uncertainty, Is.EqualTo(.2f));
            Assert.That(observation.ObservedAtSimulationTime, Is.EqualTo(.1f));
            Assert.That(observation.UncertaintyKey, Is.EqualTo("key-1"));
            CollectionAssert.AreEqual(new[] { "home-1", "home-2" },
                observation.Sources.Select(value => value.Value));
        }
    }
}
