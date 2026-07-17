using System.Linq;
using NUnit.Framework;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Shared.Contracts;
using PrototypePlayerRole = VolleyballMatch.Domain.Prototype.PlayerRole;
using PrototypeTeamId = VolleyballMatch.Domain.Prototype.TeamId;
using StablePlayerId = VolleyballMatch.Shared.Contracts.PlayerId;

namespace VolleyballMatch.EditModeTests
{
    public sealed class SharedBoundaryTests
    {
        [Test]
        public void SharedAssembly_DoesNotReferenceMatchAssemblies()
        {
            var references = typeof(MatchContextV1).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("VolleyballMatch.Domain"));
            Assert.That(references, Does.Not.Contain("VolleyballMatch.AI"));
            Assert.That(references, Does.Not.Contain("VolleyballMatch.Presentation"));
        }

        [Test]
        public void MatchDomain_ReferencesSharedAndAbilitySnapshotRoundTrips()
        {
            var references = typeof(PlayerAbilityProfile).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var original = new PlayerAbilityProfile(
                0.71f,
                0.72f,
                0.73f,
                0.74f,
                0.75f,
                0.76f,
                0.77f);

            var restored = new PlayerAbilityProfile(original.ToSnapshot());

            Assert.That(references, Does.Contain("VolleyballMatch.Shared"));
            Assert.That(restored.Mobility, Is.EqualTo(original.Mobility));
            Assert.That(restored.Reaction, Is.EqualTo(original.Reaction));
            Assert.That(restored.Jump, Is.EqualTo(original.Jump));
            Assert.That(restored.ReceiveTechnique, Is.EqualTo(original.ReceiveTechnique));
            Assert.That(restored.SetTechnique, Is.EqualTo(original.SetTechnique));
            Assert.That(restored.AttackTechnique, Is.EqualTo(original.AttackTechnique));
            Assert.That(restored.AttackPower, Is.EqualTo(original.AttackPower));
        }

        [Test]
        public void MatchPlayerBinding_KeepsStableCareerIdentitySeparateFromPrototypeSlot()
        {
            var snapshot = new PlayerAbilitySnapshotV1(
                0.71f,
                0.72f,
                0.73f,
                0.74f,
                0.75f,
                0.76f,
                0.77f);
            var binding = new MatchPlayerBinding(
                new StablePlayerId("career-player-0042"),
                new VolleyballMatch.Domain.Prototype.PlayerId(
                    PrototypeTeamId.Blue,
                    PrototypePlayerRole.Setter),
                snapshot);

            Assert.That(binding.StablePlayerId.Value, Is.EqualTo("career-player-0042"));
            Assert.That(binding.Slot.Team, Is.EqualTo(PrototypeTeamId.Blue));
            Assert.That(binding.Slot.Role, Is.EqualTo(PrototypePlayerRole.Setter));
            Assert.That(binding.Ability.SetTechnique, Is.EqualTo(0.75f));
        }

        [Test]
        public void MatchPlayerBinding_RejectsInvalidPrototypeSlot()
        {
            var snapshot = new PlayerAbilitySnapshotV1(
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f);

            Assert.That(
                () => new MatchPlayerBinding(
                    new StablePlayerId("career-player-0042"),
                    new VolleyballMatch.Domain.Prototype.PlayerId(
                        (PrototypeTeamId)99,
                        PrototypePlayerRole.Setter),
                    snapshot),
                Throws.TypeOf<System.ArgumentException>());
        }
    }
}
