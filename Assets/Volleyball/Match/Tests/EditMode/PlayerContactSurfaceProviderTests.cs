using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerContactSurfaceProviderTests
    {
        [Test]
        public void Collect_UsesResolvedSurfaceAndCancelProducesNoFutureCandidates()
        {
            var player = new GameObject("ProviderCandidatePlayer");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "2");
                var provider = new PlayerContactSurfaceProvider(rig, player.transform);
                var contacts = new List<BallContactCandidate>();
                var input = new PlayerContactInput(
                    new PlayerId(TeamId.Blue, PlayerRole.Attacker),
                    TechniqueAction.Attack,
                    TechniqueAction.Attack,
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    801,
                    1f,
                    new SimVector3(0f, 2f, 8f),
                    SetContactHand.Both);

                provider.Collect(input, contacts);

                Assert.That(contacts, Has.Count.EqualTo(1));
                Assert.That(contacts[0].Surface.Active, Is.True);
                Assert.That(contacts[0].Surface.ContactGroupId, Is.EqualTo(801));
                Assert.That(contacts[0].Surface.Current.Origin.Y, Is.GreaterThan(0f));

                provider.Clear();
                contacts.Clear();
                provider.Collect(input, contacts);

                Assert.That(contacts, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
