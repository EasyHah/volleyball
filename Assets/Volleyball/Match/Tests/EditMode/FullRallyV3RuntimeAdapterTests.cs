using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3RuntimeAdapterTests
    {
        private static readonly PlayerId[] HomeRotation =
        {
            new PlayerId("home-1"),
            new PlayerId("home-2"),
            new PlayerId("home-3"),
            new PlayerId("home-4"),
            new PlayerId("home-5"),
            new PlayerId("home-6")
        };

        private static readonly PlayerId[] AwayRotation =
        {
            new PlayerId("away-1"),
            new PlayerId("away-2"),
            new PlayerId("away-3"),
            new PlayerId("away-4"),
            new PlayerId("away-5"),
            new PlayerId("away-6")
        };

        [Test]
        public void BeginRally_ResetsCountAndDuplicateGroupState()
        {
            var adapter = CreateAdapter();
            Assert.That(
                adapter.ObserveAcceptedContact(
                    HomeRotation[0], TeamSide.Home, RallyContactClassificationV3.TeamContact, 71)
                    .Accepted,
                Is.True);
            Assert.That(
                adapter.ObserveAcceptedContact(
                    HomeRotation[0], TeamSide.Home, RallyContactClassificationV3.TeamContact, 72)
                    .RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.ConsecutiveCountedContact));

            adapter.BeginRally(TeamSide.Away);
            var firstContactOfNextRally = adapter.ObserveAcceptedContact(
                HomeRotation[0], TeamSide.Home, RallyContactClassificationV3.TeamContact, 71);

            Assert.That(firstContactOfNextRally.Accepted, Is.True);
            Assert.That(firstContactOfNextRally.Before.CountedHits, Is.Zero);
            Assert.That(firstContactOfNextRally.Before.LastContactGroup, Is.Null);
            Assert.That(firstContactOfNextRally.After.CountedHits, Is.EqualTo(1));
        }

        [Test]
        public void BeginRally_RefreshesRotationAndServerEligibility()
        {
            var context = CreateFormalV4Context();
            var set = new MatchSet(context, TeamSide.Home);
            var initialHome = RotationFor(set, TeamSide.Home);
            var initialAway = RotationFor(set, TeamSide.Away);
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                OnCourtLineupRulesV3.Create(
                    context,
                    initialHome,
                    initialAway,
                    initialHome[0],
                    initialAway[0],
                    Array.Empty<LiberoReplacementV3>()),
                TeamSide.Home,
                V3RulesMode.Shadow);
            var initialAwayServer = set.ServerFor(TeamSide.Away);

            set.ResolveRally(TeamSide.Away, null, null);
            var rotatedHome = RotationFor(set, TeamSide.Home);
            var rotatedAway = RotationFor(set, TeamSide.Away);
            var refreshedEligibility = OnCourtLineupRulesV3.Create(
                context,
                rotatedHome,
                rotatedAway,
                set.ServerFor(TeamSide.Home),
                set.ServerFor(TeamSide.Away),
                Array.Empty<LiberoReplacementV3>());

            adapter.BeginRally(refreshedEligibility, TeamSide.Away);

            var eligibilityField = typeof(FullRallyV3RulesRuntimeAdapter).GetField(
                "_eligibility",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var activeEligibility =
                (OnCourtEligibilitySnapshot)eligibilityField.GetValue(adapter);
            Assert.That(set.ServerFor(TeamSide.Away), Is.Not.EqualTo(initialAwayServer));
            Assert.That(activeEligibility.For(set.ServerFor(TeamSide.Away)).RotationPosition, Is.EqualTo(1));
            Assert.That(activeEligibility.For(set.ServerFor(TeamSide.Away)).IsCurrentServer, Is.True);
            Assert.That(activeEligibility.For(initialAwayServer).RotationPosition, Is.EqualTo(6));
            Assert.That(activeEligibility.For(initialAwayServer).IsCurrentServer, Is.False);
        }

        [Test]
        public void ObserveAcceptedContact_PreservesActualClassificationAndPhysicalGroup()
        {
            var transition = CreateAdapter().ObserveAcceptedContact(
                AwayRotation[2],
                TeamSide.Away,
                RallyContactClassificationV3.BlockContact,
                987654);

            Assert.That(transition.Accepted, Is.True);
            Assert.That(
                transition.After.LastContactClassification,
                Is.EqualTo(RallyContactClassificationV3.BlockContact));
            Assert.That(transition.After.LastContactGroup, Is.EqualTo(987654));
            Assert.That(transition.After.LastLegalPhysicalContactTeam, Is.EqualTo(TeamSide.Away));
        }

        [Test]
        public void AuthorityMode_CanBeConfiguredAtTheExplicitGate()
        {
            var context = CreateContext();
            var eligibility = CreateEligibility(context, HomeRotation, AwayRotation);

            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                eligibility,
                TeamSide.Home,
                V3RulesMode.Authority);

            Assert.That(adapter.Mode, Is.EqualTo(V3RulesMode.Authority));
        }

        [Test]
        public void CommitContact_ObservedGeometryDecidesOtherwiseIdenticalAttackEligibility()
        {
            var illegalGeometry = new AttackGeometryFactV3(
                HomeRotation[4], TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 1f, -1f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.50f, -0.2f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);
            var legalGeometry = new AttackGeometryFactV3(
                HomeRotation[4], TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 1f, -3.1f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.50f, -0.2f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            var legal = CreateAdapter().CommitContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                901L,
                legalGeometry);
            var illegal = CreateAdapter().CommitContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                901L,
                illegalGeometry);

            Assert.That(legal.Accepted, Is.True);
            Assert.That(illegal.Accepted, Is.False);
            Assert.That(illegal.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.ActionIneligible));
        }

        [TestCase(2.43f, true)]
        [TestCase(2.431f, false)]
        public void EvaluateContact_ObservedHeightThresholdReturnsExactV3Decision(
            float observedContactHeight,
            bool expectedAccepted)
        {
            var geometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -1f),
                new Volleyball.Domain.Simulation.SimVector3(0f, observedContactHeight, -0.2f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            var transition = CreateAdapter().EvaluateContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                902L,
                geometry);

            Assert.That(transition.Accepted, Is.EqualTo(expectedAccepted));
            Assert.That(
                transition.RejectionReason,
                Is.EqualTo(
                    expectedAccepted
                        ? RuleRejectionReasonV3.None
                        : RuleRejectionReasonV3.ActionIneligible));
        }

        [Test]
        public void CommitContact_RejectsGeometryForAnotherActorOrSide()
        {
            var actorMismatch = new AttackGeometryFactV3(
                HomeRotation[5],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -3.1f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);
            var sideMismatch = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Away,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, 3.1f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, 0.2f),
                3f,
                2.43f);

            Assert.That(
                () => CreateAdapter().CommitContact(
                    HomeRotation[4],
                    TeamSide.Home,
                    RallyContactClassificationV3.TeamContact,
                    903L,
                    actorMismatch),
                Throws.ArgumentException);
            Assert.That(
                () => CreateAdapter().CommitContact(
                    HomeRotation[4],
                    TeamSide.Home,
                    RallyContactClassificationV3.TeamContact,
                    904L,
                    sideMismatch),
                Throws.ArgumentException);
        }

        [Test]
        public void CommitContact_PlannedLegalObservedIllegal_UsesObservedTransition()
        {
            var plannedGeometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -3.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);
            var observedGeometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -1.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);

            var transition = CreateAdapter().CommitContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                905L,
                observedGeometry);

            Assert.That(plannedGeometry.IsTakeoffInFrontZone, Is.False);
            Assert.That(observedGeometry.IsTakeoffInFrontZone, Is.True);
            Assert.That(transition.Accepted, Is.False);
            Assert.That(
                transition.RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.ActionIneligible));
        }

        [Test]
        public void CommitContact_PlannedIllegalObservedLegal_UsesObservedTransition()
        {
            var plannedGeometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -1.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);
            var observedGeometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -3.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);

            var transition = CreateAdapter().CommitContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                906L,
                observedGeometry);

            Assert.That(plannedGeometry.IsTakeoffInFrontZone, Is.True);
            Assert.That(observedGeometry.IsTakeoffInFrontZone, Is.False);
            Assert.That(transition.Accepted, Is.True);
            Assert.That(transition.RejectionReason, Is.EqualTo(RuleRejectionReasonV3.None));
        }

        [Test]
        public void AttackGeometryEvaluateAndCommit_ReturnIdenticalDecisionAndCommitOnce()
        {
            var adapter = CreateAdapter();
            var geometry = new AttackGeometryFactV3(
                HomeRotation[4],
                TeamSide.Home,
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -3.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.5f, -0.2f),
                3f,
                2.43f);

            var evaluation = adapter.EvaluateContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                907L,
                geometry);
            var committed = adapter.CommitContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                907L,
                geometry);

            Assert.That(evaluation.Accepted, Is.True);
            Assert.That(evaluation.RejectionReason, Is.EqualTo(committed.RejectionReason));
            Assert.That(evaluation.Before.CountedHits, Is.Zero);
            Assert.That(committed.Before.CountedHits, Is.Zero);
            Assert.That(committed.After.CountedHits, Is.EqualTo(1));
        }

        [Test]
        public void CreateObservedAttackGeometryFact_UsesCollisionPointAndValidatesTakeoffTime()
        {
            var method = typeof(PhysicalMatchRallyDirector).GetMethod(
                "CreateObservedAttackGeometryFact",
                BindingFlags.Static | BindingFlags.NonPublic);
            var takeoff = new ObservedAttackTakeoff(
                new Volleyball.Domain.Simulation.SimVector3(0f, 0f, -1.2f),
                4.62f);
            var hit = new Volleyball.Domain.Simulation.SweptBallHit(
                0.5f,
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.55f, -0.2f),
                new Volleyball.Domain.Simulation.SimVector3(0f, 2.42f, -0.2f),
                Volleyball.Domain.Simulation.SimVector3.Up,
                Volleyball.Domain.Simulation.SimVector3.Zero,
                908,
                1f);

            Assert.That(method, Is.Not.Null);
            var fact = (AttackGeometryFactV3)method.Invoke(
                null,
                new object[] { HomeRotation[4], TeamSide.Home, takeoff, hit, 5f });

            Assert.That(fact.TakeoffPoint, Is.EqualTo(takeoff.Point));
            Assert.That(fact.ContactPoint, Is.EqualTo(hit.ContactPoint));
            Assert.That(fact.IsContactAboveNet, Is.False);
            var equalTimeException = Assert.Throws<TargetInvocationException>(() => method.Invoke(
                null,
                new object[] { HomeRotation[4], TeamSide.Home, takeoff, hit, 4.62f }));
            var laterTakeoffException = Assert.Throws<TargetInvocationException>(() => method.Invoke(
                null,
                new object[] { HomeRotation[4], TeamSide.Home, takeoff, hit, 4.61f }));
            Assert.That(
                equalTimeException.InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(
                laterTakeoffException.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void EvaluateContact_IsPureAcrossMultipleCandidatesAndCommitAdvancesOnce()
        {
            var context = CreateContext();
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                CreateEligibility(context, HomeRotation, AwayRotation),
                TeamSide.Home,
                V3RulesMode.Authority);

            var firstEvaluation = adapter.EvaluateContact(
                HomeRotation[0],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                801);
            var secondEvaluation = adapter.EvaluateContact(
                HomeRotation[0],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                802);

            Assert.That(firstEvaluation.Accepted, Is.True);
            Assert.That(secondEvaluation.Accepted, Is.True);
            Assert.That(firstEvaluation.Before.CountedHits, Is.Zero);
            Assert.That(secondEvaluation.Before.CountedHits, Is.Zero);

            var committed = adapter.CommitContact(
                HomeRotation[0],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                801);
            var afterCommit = adapter.EvaluateContact(
                HomeRotation[0],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                802);

            Assert.That(committed.Accepted, Is.True);
            Assert.That(committed.After.CountedHits, Is.EqualTo(1));
            Assert.That(
                afterCommit.RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.ConsecutiveCountedContact));
        }

        [Test]
        public void AuthorityCommit_BlockThenSameBlockerFirstCountedContact_IsAccepted()
        {
            var context = CreateContext();
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                CreateEligibility(context, HomeRotation, AwayRotation),
                TeamSide.Home,
                V3RulesMode.Authority);

            var block = adapter.CommitContact(
                HomeRotation[1],
                TeamSide.Home,
                RallyContactClassificationV3.BlockContact,
                811);
            var countedContact = adapter.CommitContact(
                HomeRotation[1],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                812);

            Assert.That(block.Accepted, Is.True);
            Assert.That(countedContact.Accepted, Is.True);
            Assert.That(countedContact.Before.CountedHits, Is.Zero);
            Assert.That(countedContact.After.CountedHits, Is.EqualTo(1));
        }

        [Test]
        public void EvaluateContact_BackRowBlocker_IsRejectedWithoutAdvancingRules()
        {
            var context = CreateContext();
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                CreateEligibility(context, HomeRotation, AwayRotation),
                TeamSide.Home,
                V3RulesMode.Authority);

            var evaluation = adapter.EvaluateContact(
                HomeRotation[4],
                TeamSide.Home,
                RallyContactClassificationV3.BlockContact,
                821);
            var laterTeamContact = adapter.EvaluateContact(
                HomeRotation[0],
                TeamSide.Home,
                RallyContactClassificationV3.TeamContact,
                822);

            Assert.That(evaluation.Accepted, Is.False);
            Assert.That(
                evaluation.RejectionReason,
                Is.EqualTo(RuleRejectionReasonV3.ActionIneligible));
            Assert.That(laterTeamContact.Before.CountedHits, Is.Zero);
        }

        [Test]
        public void UnconfiguredDirector_IsDisabledWithZeroDiagnosticsAndNoAdapter()
        {
            var gameObject = new GameObject("unconfigured-physical-director");
            try
            {
                var director = gameObject.AddComponent<PhysicalMatchRallyDirector>();
                var adapterField = typeof(PhysicalMatchRallyDirector).GetField(
                    "_v3RulesAdapter",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Disabled));
                Assert.That(director.V3RuleTransitions, Is.Zero);
                Assert.That(director.V3RuleParityMatches, Is.Zero);
                Assert.That(director.V3RuleIntentionalCorrections, Is.Zero);
                Assert.That(director.V3RuleUnexpectedMismatches, Is.Zero);
                Assert.That(director.LastV3RuleDiagnostic, Is.Empty);
                Assert.That(adapterField, Is.Not.Null);
                Assert.That(adapterField.GetValue(director), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PhysicalDirector_ExposesShadowPlanReplayEvent()
        {
            var replayEvent = typeof(PhysicalMatchRallyDirector).GetEvent(
                "ReplayShadowPlanRecorded");

            Assert.That(replayEvent, Is.Not.Null);
            Assert.That(
                replayEvent.EventHandlerType,
                Is.EqualTo(typeof(Action<RallyPlanV3>)));
        }

        [Test]
        public void ConfigureV3Rules_RejectsMidRallyConfiguration()
        {
            var gameObject = new GameObject("active-formal-director");
            try
            {
                var director = gameObject.AddComponent<PhysicalMatchRallyDirector>();
                var context = PrepareFormalDirector(director);
                SetPrivateField(director, "_rallyActive", true);

                Assert.That(
                    () => director.ConfigureV3Rules(V3RulesMode.Shadow),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConfigureV3Rules_DisablingRemovesExistingShadowConfiguration()
        {
            var gameObject = new GameObject("configured-formal-director");
            try
            {
                var director = gameObject.AddComponent<PhysicalMatchRallyDirector>();
                PrepareFormalDirector(director);
                director.ConfigureV3Rules(V3RulesMode.Shadow);
                var originalAdapter = GetPrivateField(director, "_v3RulesAdapter");
                Assert.That(director.GateHAuthorityEnabled, Is.False);

                director.ConfigureV3Rules(V3RulesMode.Disabled);

                Assert.That(originalAdapter, Is.Not.Null);
                Assert.That(GetPrivateField(director, "_v3RulesAdapter"), Is.Null);
                Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Disabled));
                Assert.That(director.GateHAuthorityEnabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static FullRallyV3RulesRuntimeAdapter CreateAdapter()
        {
            var context = CreateContext();
            var eligibility = CreateEligibility(context, HomeRotation, AwayRotation);
            return new FullRallyV3RulesRuntimeAdapter(
                RulesVersions.FullRallyV3,
                eligibility,
                TeamSide.Home,
                V3RulesMode.Shadow);
        }

        private static MatchContextV4 CreateContext()
        {
            var positions = new[]
            {
                PlayerPosition.Setter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.OutsideHitter
            };
            return MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("463f889f-8043-46d0-af82-b9331f316eae"),
                7351,
                HomeRotation,
                positions,
                AwayRotation,
                positions);
        }

        private static OnCourtEligibilitySnapshot CreateEligibility(
            MatchContextV4 context,
            PlayerId[] homeRotation,
            PlayerId[] awayRotation)
        {
            return OnCourtLineupRulesV3.Create(
                context,
                homeRotation,
                awayRotation,
                homeRotation[0],
                awayRotation[0],
                Array.Empty<LiberoReplacementV3>());
        }

        private static MatchContextV4 PrepareFormalDirector(PhysicalMatchRallyDirector director)
        {
            var context = CreateFormalV4Context();
            var set = new MatchSet(context, TeamSide.Home);
            SetPrivateField(director, "_set", set);
            SetPrivateField(director, "_formalSet", set);
            SetPrivateField(director, "_matchContext", context);
            SetPrivateField(
                director,
                "_configuration",
                PhysicalMatchConfiguration.FormalIndoorSixVsSix);
            return context;
        }

        private static MatchContextV4 CreateFormalV4Context()
        {
            var createContext = typeof(FormalSixVsSixRallyBootstrap).GetMethod(
                "CreateSandboxContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (MatchContextV4)createContext.Invoke(null, null);
        }

        private static PlayerId[] RotationFor(MatchSet set, TeamSide side)
        {
            var rotation = new PlayerId[6];
            for (var position = 1; position <= rotation.Length; position++)
            {
                rotation[position - 1] = set.PlayerAtRotationPosition(side, position);
            }

            return rotation;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = FindPrivateField(target, fieldName);
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return FindPrivateField(target, fieldName).GetValue(target);
        }

        private static FieldInfo FindPrivateField(object target, string fieldName)
        {
            return target.GetType().BaseType?.GetField(
                       fieldName,
                       BindingFlags.Instance | BindingFlags.NonPublic) ??
                   target.GetType().GetField(
                       fieldName,
                       BindingFlags.Instance | BindingFlags.NonPublic);
        }

    }
}
