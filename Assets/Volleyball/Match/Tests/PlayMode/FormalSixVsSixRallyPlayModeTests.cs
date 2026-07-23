using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Prototype;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixRallyPlayModeTests
    {
        [UnityTest]
        public IEnumerator FormalScene_CompletesTwentyFivePointSetWithTwelvePlayers()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var cameras = Object.FindFirstObjectByType<RallyCameraController>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            Assert.That(director.PlayerCount, Is.EqualTo(12));
            Assert.That(director.RosterSize, Is.EqualTo(6));
            Assert.That(director.TargetScore, Is.EqualTo(25));
            Assert.That(director.CourtHalfLength, Is.EqualTo(9f));
            Assert.That(director.MatchContextV2, Is.Not.Null);
            Assert.That(director.MatchContext, Is.Null);
            Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Shadow));
            Assert.That(players, Has.Some.Matches<PrototypePlayerAgent>(
                player => player.Id.Role == PlayerRole.MiddleBlocker && player.Ability.MaxAttackReach == 3.48f));
            AssertRoster(players, director);

            var initialServer = director.CurrentServer;
            var originalTimeScale = Time.timeScale;
            var aiSource = new ImmediateWeightSource();
            director.ConfigureAiDecisionSource(
                aiSource,
                realTimeTimeoutSeconds: 0.5f,
                restoreDurationSeconds: 0.04f);
            Assert.That(
                Object.FindObjectsByType<AiDecisionTimeController>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            var timeout = Time.realtimeSinceStartup + 300f;
            var sawOutsideOwnCourt = false;
            var minimumSameTeamSeparation = float.PositiveInfinity;
            var awaitingFirstPostRotationRally = false;
            var verifiedPostRotationV3Eligibility = false;
            while (director.ResultV2 == null && Time.realtimeSinceStartup < timeout)
            {
                foreach (var player in players)
                {
                    sawOutsideOwnCourt |= !player.IsWithinOwnCourt;
                }

                minimumSameTeamSeparation = Mathf.Min(
                    minimumSameTeamSeparation,
                    MinimumSameTeamSeparation(players));
                if (!verifiedPostRotationV3Eligibility &&
                    director.HomeRotationOffset + director.AwayRotationOffset > 0 &&
                    !director.IsLoopRunning)
                {
                    awaitingFirstPostRotationRally = true;
                }
                if (awaitingFirstPostRotationRally && director.IsLoopRunning)
                {
                    AssertV3EligibilityMatchesLiveRotation(director, players);
                    verifiedPostRotationV3Eligibility = true;
                    awaitingFirstPostRotationRally = false;
                }
                yield return null;
            }

            Assert.That(director.ResultV2, Is.Not.Null, "Formal 6v6 set did not complete in real time.");
            Assert.That(Mathf.Max(director.ResultV2.HomeScore, director.ResultV2.AwayScore),
                Is.GreaterThanOrEqualTo(25));
            Assert.That(Mathf.Abs(director.ResultV2.HomeScore - director.ResultV2.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(director.ResultV2.PlayerStats, Has.Count.EqualTo(12));
            Assert.DoesNotThrow(() => director.ResultV2.ValidateAgainst(director.MatchContextV2));
            Assert.That(director.V3RuleTransitions, Is.GreaterThan(0));
            Assert.That(director.V3RuleTransitions, Is.EqualTo(director.SuccessfulContacts));
            Assert.That(director.V3RuleParityMatches, Is.EqualTo(director.V3RuleTransitions));
            Assert.That(director.V3RuleIntentionalCorrections, Is.Zero);
            Assert.That(director.V3RuleUnexpectedMismatches, Is.Zero);
            Assert.That(director.LastV3RuleDiagnostic, Is.Not.Empty);
            Assert.That(
                director.ResultV2.PlayerStats.Sum(stat => stat.Contacts),
                Is.EqualTo(
                    director.SuccessfulContacts +
                    director.ResultV2.HomeScore +
                    director.ResultV2.AwayScore));
            Assert.That(director.IsLoopRunning, Is.False);
            Assert.That(director.GroundResolvedRallies, Is.GreaterThan(0));
            Assert.That(director.ScheduledMultiBlockUnits, Is.GreaterThan(0));
            Assert.That(director.ScheduledBackRowBlockers, Is.Zero);
            Assert.That(director.BlueAttackContacts, Is.GreaterThan(0));
            Assert.That(director.OrangeAttackContacts, Is.GreaterThan(0));
            Assert.That(director.HomeRotationOffset + director.AwayRotationOffset,
                Is.GreaterThan(0));
            Assert.That(
                verifiedPostRotationV3Eligibility,
                Is.True,
                "No post-side-out rally exposed refreshed V3 eligibility.");
            Assert.That(director.CurrentServer, Is.Not.EqualTo(initialServer));
            Assert.That(aiSource.RequestCount, Is.EqualTo(director.AiDecisionRequests));
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.001f));
            Assert.That(sawOutsideOwnCourt, Is.False);
            Assert.That(minimumSameTeamSeparation, Is.GreaterThan(0.08f));
            Assert.That(ball.Diagnostics.NonFiniteStates, Is.Zero);

            cameras.SetView(RallyCameraView.Tactical);
            yield return null;
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(Camera.main.orthographicSize, Is.GreaterThanOrEqualTo(12f));
        }

        private static void AssertRoster(
            PrototypePlayerAgent[] players,
            FormalSixVsSixRallyDirector director)
        {
            var stableIds = new HashSet<string>();
            var blue = 0;
            var orange = 0;
            var frontBlue = 0;
            var frontOrange = 0;
            var roles = new HashSet<PlayerRole>();
            foreach (var player in players)
            {
                Assert.That(stableIds.Add(player.StableId.Value), Is.True);
                roles.Add(player.Id.Role);
                if (player.Id.Team == TeamId.Blue)
                {
                    blue++;
                    frontBlue += director.IsFrontRow(player.Id) ? 1 : 0;
                }
                else
                {
                    orange++;
                    frontOrange += director.IsFrontRow(player.Id) ? 1 : 0;
                }
            }

            Assert.That(blue, Is.EqualTo(6));
            Assert.That(orange, Is.EqualTo(6));
            Assert.That(frontBlue, Is.EqualTo(3));
            Assert.That(frontOrange, Is.EqualTo(3));
            Assert.That(roles, Does.Contain(PlayerRole.Setter));
            Assert.That(roles, Does.Contain(PlayerRole.OutsideHitter));
            Assert.That(roles, Does.Contain(PlayerRole.Opposite));
            Assert.That(roles, Does.Contain(PlayerRole.MiddleBlocker));
            Assert.That(roles, Does.Contain(PlayerRole.Defender));
        }

        private static float MinimumSameTeamSeparation(PrototypePlayerAgent[] players)
        {
            var minimum = float.PositiveInfinity;
            for (var first = 0; first < players.Length; first++)
            {
                for (var second = first + 1; second < players.Length; second++)
                {
                    if (players[first].Id.Team != players[second].Id.Team)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(
                        minimum,
                        Vector3.Distance(
                            players[first].transform.position,
                            players[second].transform.position));
                }
            }

            return minimum;
        }

        private static void AssertV3EligibilityMatchesLiveRotation(
            FormalSixVsSixRallyDirector director,
            PrototypePlayerAgent[] players)
        {
            var adapter = GetPrivateField<FullRallyV3RulesRuntimeAdapter>(
                director,
                "_v3RulesAdapter");
            var eligibility = GetPrivateField<OnCourtEligibilitySnapshot>(
                adapter,
                "_eligibility");
            var set = GetPrivateField<MatchSet>(director, "_set");

            Assert.That(adapter, Is.Not.Null);
            Assert.That(eligibility.Players, Has.Count.EqualTo(12));
            foreach (var player in players)
            {
                Assert.That(
                    eligibility.For(player.StableId).RotationPosition,
                    Is.EqualTo(director.RotationPositionFor(player.Id)),
                    player.StableId.Value);
            }

            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var currentServers = eligibility.Players
                    .Where(player => player.Side == side && player.IsCurrentServer)
                    .ToArray();
                Assert.That(currentServers, Has.Length.EqualTo(1), side.ToString());
                Assert.That(currentServers[0].PlayerId, Is.EqualTo(set.ServerFor(side)));
            }

            Assert.That(
                director.CurrentServer,
                Is.EqualTo(set.ServerFor(set.ServingSide)));
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(target);
                }
            }

            Assert.Fail($"Missing private field {fieldName}.");
            return default;
        }

        private sealed class ImmediateWeightSource : IRallyTacticalWeightSource
        {
            public int RequestCount { get; private set; }

            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.FromResult(new RallyTacticalWeightProposal(1f, 1.15f, 1f, 1f));
            }
        }
    }
}
