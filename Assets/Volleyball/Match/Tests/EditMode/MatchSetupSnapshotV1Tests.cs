using System.Linq;
using NUnit.Framework;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class MatchSetupSnapshotV1Tests
    {
        [Test]
        public void Freeze_IsStableAndDeepCopiesMutableSetup()
        {
            var draft = MatchSetupDraftV1.CreateDefault(
                FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5(),
                TeamSide.Home);
            var editor = new MatchSetupEditorV1(draft);
            var first = editor.Freeze();
            var second = editor.Freeze();
            var frozenFirstPlayer = first.Players[0].Position;

            editor.ExchangeRotation(TeamSide.Home, 1, 2);
            editor.SetPlayerPosition(draft.HomeRotation[0],
                new Volleyball.Domain.Simulation.SimVector3(1.1f, 0f, -5.5f));

            Assert.That(first.SetupHash, Is.EqualTo(second.SetupHash));
            Assert.That(first.Players[0].Position, Is.EqualTo(frozenFirstPlayer));
            Assert.That(editor.Freeze().SetupHash, Is.Not.EqualTo(first.SetupHash));
        }

        [Test]
        public void Freeze_BindsExactV5OverridesWithoutMutatingBaseContext()
        {
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5();
            var baseJson = ContractJson.SerializeV5(context);
            var draft = MatchSetupDraftV1.CreateDefault(context, TeamSide.Home);
            var player = draft.HomeRotation[0];
            var value = new TrainingPlayerAttributeOverrideV2();
            value.Set(TrainingPlayerAttributeFieldV2.Attack, 1234);
            value.Set(TrainingPlayerAttributeFieldV2.Height, 2222);
            value.SetDominantHand(DominantHandV5.Left);
            draft.AttributeOverrides.Add(player, value);

            var snapshot = new MatchSetupEditorV1(draft).Freeze();
            var frozen = snapshot.AttributeOverrides.Single(pair =>
                pair.PlayerId.Equals(player));

            Assert.That(frozen.Attack, Is.EqualTo(1234));
            Assert.That(frozen.HeightMillimeters, Is.EqualTo(2222));
            Assert.That(frozen.DominantHand, Is.EqualTo(DominantHandV5.Left));
            Assert.That(ContractJson.SerializeV5(context), Is.EqualTo(baseJson));
        }
    }
}
