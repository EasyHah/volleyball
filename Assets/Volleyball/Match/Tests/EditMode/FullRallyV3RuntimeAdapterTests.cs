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
            var legacyContext = CreateFormalV2Context();
            var set = new MatchSet(legacyContext, TeamSide.Home);
            var context = MatchContextV3.UpgradeFromV2(legacyContext);
            var initialHome = RotationFor(set, TeamSide.Home);
            var initialAway = RotationFor(set, TeamSide.Away);
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                context,
                CreateEligibility(context, initialHome, initialAway),
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
        public void AuthorityMode_IsRejectedUntilAuthorityGateExists()
        {
            var context = CreateContext();
            var eligibility = CreateEligibility(context, HomeRotation, AwayRotation);

            Assert.That(
                () => new FullRallyV3RulesRuntimeAdapter(
                    context,
                    eligibility,
                    TeamSide.Home,
                    V3RulesMode.Authority),
                Throws.TypeOf<NotSupportedException>());
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
        public void ConfigureV3Rules_RejectsMidRallyConfiguration()
        {
            var gameObject = new GameObject("active-formal-director");
            try
            {
                var director = gameObject.AddComponent<PhysicalMatchRallyDirector>();
                var context = PrepareFormalDirector(director);
                SetPrivateField(director, "_rallyActive", true);

                Assert.That(
                    () => director.ConfigureV3Rules(
                        MatchContextV3.UpgradeFromV2(context),
                        V3RulesMode.Shadow),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConfigureV3Rules_InvalidReplacementPreservesExistingShadowConfiguration()
        {
            var gameObject = new GameObject("configured-formal-director");
            try
            {
                var director = gameObject.AddComponent<PhysicalMatchRallyDirector>();
                var context = PrepareFormalDirector(director);
                director.ConfigureV3Rules(
                    MatchContextV3.UpgradeFromV2(context),
                    V3RulesMode.Shadow);
                var originalAdapter = GetPrivateField(director, "_v3RulesAdapter");

                Assert.That(
                    () => director.ConfigureV3Rules(CreateContext(), V3RulesMode.Shadow),
                    Throws.TypeOf<ArgumentException>());
                Assert.That(GetPrivateField(director, "_v3RulesAdapter"), Is.SameAs(originalAdapter));
                Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Shadow));
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
                context,
                eligibility,
                TeamSide.Home,
                V3RulesMode.Shadow);
        }

        private static MatchContextV3 CreateContext()
        {
            return MatchContextV3.Create(
                Guid.Parse("463f889f-8043-46d0-af82-b9331f316eae"),
                7351,
                CreateTeam("home", TeamSide.Home, HomeRotation),
                CreateTeam("away", TeamSide.Away, AwayRotation));
        }

        private static OnCourtEligibilitySnapshot CreateEligibility(
            MatchContextV3 context,
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

        private static MatchContextV2 PrepareFormalDirector(PhysicalMatchRallyDirector director)
        {
            var context = CreateFormalV2Context();
            SetPrivateField(director, "_set", new MatchSet(context, TeamSide.Home));
            SetPrivateField(
                director,
                "_configuration",
                PhysicalMatchConfiguration.FormalIndoorSixVsSix);
            return context;
        }

        private static MatchContextV2 CreateFormalV2Context()
        {
            var createContext = typeof(FormalSixVsSixRallyBootstrap).GetMethod(
                "CreateSandboxContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (MatchContextV2)createContext.Invoke(null, null);
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

        private static TeamSnapshotV3 CreateTeam(
            string teamId,
            TeamSide side,
            PlayerId[] rotation)
        {
            var players = new PlayerSnapshotV3[rotation.Length];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerSnapshotV3(
                    rotation[index],
                    rotation[index].Value,
                    index + 1,
                    index == 0 ? PlayerPosition.Setter : PlayerPosition.OutsideHitter,
                    new PlayerAbilitySnapshotV3(
                        0.5f,
                        0.5f,
                        0.5f,
                        3.3f,
                        0.5f,
                        0.5f,
                        0.5f,
                        0.5f,
                        0.5f,
                        0.5f,
                        0.5f,
                        ContractVersions.MatchV3,
                        0,
                        false,
                        Array.Empty<string>()));
            }

            return new TeamSnapshotV3(
                new TeamId(teamId),
                teamId,
                side,
                players);
        }
    }
}
