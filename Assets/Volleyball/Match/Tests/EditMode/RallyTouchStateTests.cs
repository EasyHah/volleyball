using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;

namespace Volleyball.EditModeTests
{
    public sealed class RallyTouchStateTests
    {
        private static readonly PlayerId BlueSetter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
        private static readonly PlayerId BlueAttacker = new PlayerId(TeamId.Blue, PlayerRole.Attacker);
        private static readonly PlayerId BlueDefender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
        private static readonly PlayerId OrangeSetter = new PlayerId(TeamId.Orange, PlayerRole.Setter);

        [Test]
        public void FourthCountedTouch_AfterThreeAcceptedTouches_IsFaultAndDoesNotChangeState()
        {
            var state = new RallyTouchState(TeamId.Blue);

            Accept(state, BlueDefender, TechniqueAction.Receive, 1f);
            Accept(state, BlueSetter, TechniqueAction.Set, 2f);
            Accept(state, BlueAttacker, TechniqueAction.Attack, 3f);
            state.OpenWindow(Window(BlueDefender, TechniqueAction.Receive, 4f, 5f));

            var evaluation = state.Evaluate(BlueDefender, TechniqueAction.Receive, 4f);

            Assert.That(evaluation, Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Fault,
                RallyContactRejectionReason.FourthCountedTouch)));
            Assert.That(state.Accept(BlueDefender, TechniqueAction.Receive, 4f), Is.EqualTo(evaluation));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(3));
            Assert.That(state.LastCountedActor, Is.EqualTo(BlueAttacker));
            Assert.That(state.ContactWindow, Is.Not.Null);
        }

        [Test]
        public void ConsecutiveCountedActor_IsFaultWhileIneligibleActorIsIgnored()
        {
            var state = new RallyTouchState(TeamId.Blue);
            Accept(state, BlueSetter, TechniqueAction.Receive, 1f);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 2f, 3f));

            Assert.That(state.Evaluate(BlueAttacker, TechniqueAction.Set, 2f), Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Ignore,
                RallyContactRejectionReason.WrongActor)));
            Assert.That(state.Evaluate(BlueSetter, TechniqueAction.Set, 2f), Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Fault,
                RallyContactRejectionReason.ConsecutiveCountedTouch)));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(1));
            Assert.That(state.ContactWindow, Is.Not.Null);
        }

        [Test]
        public void ConsecutiveTouches_ByDifferentRosterSlotsWithSameRole_AreLegal()
        {
            var outsideA = new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter, 1);
            var outsideB = new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter, 4);
            var state = new RallyTouchState(TeamId.Blue);
            Accept(state, outsideA, TechniqueAction.Receive, 1f);
            state.OpenWindow(Window(outsideB, TechniqueAction.Set, 2f, 3f));

            var evaluation = state.Accept(outsideB, TechniqueAction.Set, 2f);

            Assert.That(evaluation.Disposition, Is.EqualTo(RallyContactDisposition.Accept));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(2));
            Assert.That(state.LastCountedActor, Is.EqualTo(outsideB));
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void BlockAndBeginPossession_HandleCountedAndPhysicalTouchState(TeamId nextTeam)
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Block, 1f, 2f));

            Assert.That(state.Accept(BlueSetter, TechniqueAction.Block, 1f).Disposition, Is.EqualTo(RallyContactDisposition.Accept));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueSetter));
            state.OpenWindow(Window(BlueAttacker, TechniqueAction.Attack, 3f, 4f));

            state.BeginPossession(nextTeam);

            Assert.That(state.PossessionTeam, Is.EqualTo(nextTeam));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueSetter));
            Assert.That(state.ContactWindow, Is.Null);
        }

        [Test]
        public void AcceptedServe_UpdatesPhysicalTouchWithoutChangingCountedTouchState()
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Serve, 1f, 2f));

            var evaluation = state.Accept(BlueSetter, TechniqueAction.Serve, 1f);

            Assert.That(evaluation, Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Accept,
                RallyContactRejectionReason.None)));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueSetter));
        }

        [Test]
        public void Evaluate_LegalCandidate_DoesNotChangeAnyState()
        {
            var state = new RallyTouchState(TeamId.Blue);
            Accept(state, BlueDefender, TechniqueAction.Receive, 1f);
            var window = Window(BlueSetter, TechniqueAction.Set, 2f, 3f);
            state.OpenWindow(window);

            var evaluation = state.Evaluate(BlueSetter, TechniqueAction.Set, 2f);

            Assert.That(evaluation.Disposition, Is.EqualTo(RallyContactDisposition.Accept));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(1));
            Assert.That(state.LastCountedActor, Is.EqualTo(BlueDefender));
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueDefender));
            Assert.That(state.ContactWindow, Is.SameAs(window));
        }

        [Test]
        public void CloseWindow_StopsFurtherContactsWithoutChangingTouchLedger()
        {
            var state = new RallyTouchState(TeamId.Blue);
            Accept(state, BlueDefender, TechniqueAction.Receive, 1f);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 2f, 3f));

            state.CloseWindow();

            Assert.That(state.ContactWindow, Is.Null);
            Assert.That(state.CountedTeamTouches, Is.EqualTo(1));
            Assert.That(state.LastCountedActor, Is.EqualTo(BlueDefender));
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueDefender));
            Assert.That(
                state.Evaluate(BlueSetter, TechniqueAction.Set, 2f),
                Is.EqualTo(new RallyContactEvaluation(
                    RallyContactDisposition.Ignore,
                    RallyContactRejectionReason.WindowClosed)));
        }

        [Test]
        public void SynchronizeAuthoritativeContact_UpdatesCompatibilityStateAfterLegacyRejection()
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 1f, 2f));
            Assert.That(
                state.Evaluate(BlueAttacker, TechniqueAction.Attack, 1f).Disposition,
                Is.EqualTo(RallyContactDisposition.Ignore));

            state.SynchronizeAuthoritativeContact(
                BlueAttacker,
                TechniqueAction.Attack,
                authoritativeCountedTouches: 1);

            Assert.That(state.PossessionTeam, Is.EqualTo(TeamId.Blue));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(1));
            Assert.That(state.LastCountedActor, Is.EqualTo(BlueAttacker));
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueAttacker));
            Assert.That(state.ContactWindow, Is.Null);
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Accept_AtWindowBoundary_IsAccepted(float contactTime)
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 1f, 2f));

            var evaluation = state.Accept(BlueSetter, TechniqueAction.Set, contactTime);

            Assert.That(evaluation.Disposition, Is.EqualTo(RallyContactDisposition.Accept));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(1));
            Assert.That(state.LastCountedActor, Is.EqualTo(BlueSetter));
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(BlueSetter));
            Assert.That(state.ContactWindow, Is.Null);
        }

        [Test]
        public void CountedTouchFromNonPossessionTeam_IsFaultAndDoesNotChangeState()
        {
            var state = new RallyTouchState(TeamId.Blue);
            var window = Window(OrangeSetter, TechniqueAction.Receive, 1f, 2f);
            state.OpenWindow(window);

            var evaluation = state.Evaluate(OrangeSetter, TechniqueAction.Receive, 1f);

            Assert.That(evaluation, Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Fault,
                RallyContactRejectionReason.WrongPossessionTeam)));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch.HasValue, Is.False);
            Assert.That(state.ContactWindow, Is.SameAs(window));

            Assert.That(state.Accept(OrangeSetter, TechniqueAction.Receive, 1f), Is.EqualTo(new RallyContactEvaluation(
                RallyContactDisposition.Fault,
                RallyContactRejectionReason.WrongPossessionTeam)));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch.HasValue, Is.False);
            Assert.That(state.ContactWindow, Is.SameAs(window));
        }

        [TestCase(TechniqueAction.Block)]
        [TestCase(TechniqueAction.Serve)]
        public void NonCountedTouchFromNonPossessionTeam_IsAccepted(TechniqueAction action)
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(OrangeSetter, action, 1f, 2f));

            var evaluation = state.Accept(OrangeSetter, action, 1f);

            Assert.That(evaluation.Disposition, Is.EqualTo(RallyContactDisposition.Accept));
            Assert.That(state.PossessionTeam, Is.EqualTo(TeamId.Blue));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch, Is.EqualTo(OrangeSetter));
            Assert.That(state.ContactWindow, Is.Null);
        }

        [Test]
        public void InvalidWindowContacts_AreIgnoredAndLeaveStateUnchanged()
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 1f, 2f));

            AssertIgnoredAndUnchanged(state, OrangeSetter, TechniqueAction.Set, 1f, RallyContactRejectionReason.WrongTeam);
            AssertIgnoredAndUnchanged(state, BlueSetter, TechniqueAction.Attack, 1f, RallyContactRejectionReason.WrongAction);
            AssertIgnoredAndUnchanged(state, BlueAttacker, TechniqueAction.Set, 1f, RallyContactRejectionReason.WrongActor);
            AssertIgnoredAndUnchanged(state, BlueSetter, TechniqueAction.Set, 2.01f, RallyContactRejectionReason.WindowClosed);
        }

        [Test]
        public void ContactWindow_ValidatesArgumentsDefensivelyCopiesActorsAndIncludesBoundaries()
        {
            var actors = new List<PlayerId> { BlueSetter };
            var window = new RallyContactWindow(TeamId.Blue, TechniqueAction.Set, 1f, 2f, actors);
            actors[0] = BlueAttacker;

            Assert.That(window.Contains(BlueSetter, 1f), Is.True);
            Assert.That(window.Contains(BlueSetter, 2f), Is.True);
            Assert.That(window.Contains(BlueSetter, 2.01f), Is.False);
            Assert.That(window.EligibleActors, Is.EquivalentTo(new[] { BlueSetter }));
            Assert.Throws<NotSupportedException>(() => ((IList<PlayerId>)window.EligibleActors)[0] = BlueAttacker);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyContactWindow((TeamId)99, TechniqueAction.Set, 1f, 2f, actors));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyContactWindow(TeamId.Blue, (TechniqueAction)99, 1f, 2f, actors));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyContactWindow(TeamId.Blue, TechniqueAction.Set, float.NaN, 2f, actors));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyContactWindow(TeamId.Blue, TechniqueAction.Set, 2f, 1f, actors));
            Assert.Throws<ArgumentException>(() => new RallyContactWindow(TeamId.Blue, TechniqueAction.Set, 1f, 2f, Array.Empty<PlayerId>()));
            Assert.Throws<ArgumentException>(() => new RallyContactWindow(TeamId.Blue, TechniqueAction.Set, 1f, 2f, new[] { OrangeSetter }));
        }

        [Test]
        public void Accept_ClearsWindowOnlyForAcceptedContact()
        {
            var state = new RallyTouchState(TeamId.Blue);
            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 1f, 2f));

            state.Accept(BlueAttacker, TechniqueAction.Set, 1f);
            Assert.That(state.ContactWindow, Is.Not.Null);
            state.Accept(BlueSetter, TechniqueAction.Set, 1f);
            Assert.That(state.ContactWindow, Is.Null);

            state.OpenWindow(Window(BlueSetter, TechniqueAction.Set, 3f, 4f));
            state.Accept(BlueSetter, TechniqueAction.Set, 3f);
            Assert.That(state.ContactWindow, Is.Not.Null);
        }

        [Test]
        public void State_ValidatesActorsActionsAndTimes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyTouchState((TeamId)99));

            var state = new RallyTouchState(TeamId.Blue);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginPossession((TeamId)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Evaluate(new PlayerId((TeamId)99, PlayerRole.Setter), TechniqueAction.Set, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Evaluate(BlueSetter, (TechniqueAction)99, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Evaluate(BlueSetter, TechniqueAction.Set, float.PositiveInfinity));
        }

        private static void Accept(RallyTouchState state, PlayerId actor, TechniqueAction action, float time)
        {
            state.OpenWindow(Window(actor, action, time, time + 0.5f));
            Assert.That(state.Accept(actor, action, time).Disposition, Is.EqualTo(RallyContactDisposition.Accept));
        }

        private static RallyContactWindow Window(PlayerId actor, TechniqueAction action, float start, float end)
        {
            return new RallyContactWindow(actor.Team, action, start, end, new[] { actor });
        }

        private static void AssertIgnoredAndUnchanged(
            RallyTouchState state,
            PlayerId actor,
            TechniqueAction action,
            float time,
            RallyContactRejectionReason reason)
        {
            var evaluation = state.Accept(actor, action, time);

            Assert.That(evaluation, Is.EqualTo(new RallyContactEvaluation(RallyContactDisposition.Ignore, reason)));
            Assert.That(state.CountedTeamTouches, Is.EqualTo(0));
            Assert.That(state.LastCountedActor.HasValue, Is.False);
            Assert.That(state.LastPhysicalTouch.HasValue, Is.False);
            Assert.That(state.ContactWindow, Is.Not.Null);
        }
    }
}
