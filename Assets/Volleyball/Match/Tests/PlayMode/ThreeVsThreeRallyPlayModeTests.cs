using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Presentation;
using MatchContextV1 = Volleyball.Shared.Contracts.MatchContextV1;
using PlayerAbilitySnapshotV1 = Volleyball.Shared.Contracts.PlayerAbilitySnapshotV1;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using PlayerSnapshotV1 = Volleyball.Shared.Contracts.PlayerSnapshotV1;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using StableTeamId = Volleyball.Shared.Contracts.TeamId;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using TeamSnapshotV1 = Volleyball.Shared.Contracts.TeamSnapshotV1;

namespace Volleyball.PlayModeTests
{
    public sealed class ThreeVsThreeRallyPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras()
        {
            yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var cameras = Object.FindFirstObjectByType<RallyCameraController>();
            var blockFeedback = Object.FindFirstObjectByType<BlockImpactFeedback>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(blockFeedback, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(6));
            Assert.That(director.MatchContextV2, Is.Not.Null);
            Assert.That(director.MatchContext, Is.Null);
            Assert.That(players, Has.Some.Matches<PrototypePlayerAgent>(
                player => player.Id.Role == PlayerRole.Attacker && player.Ability.MaxAttackReach == 3.42f));
            Assert.That(
                Object.FindObjectsByType<SimulatedBall>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            AssertMirroredRoleAbilities(players);
            var originalTimeScale = Time.timeScale;
            var aiSource = new ImmediateLocalWeightSource();
            director.ConfigureAiDecisionSource(
                aiSource,
                realTimeTimeoutSeconds: 0.5f,
                restoreDurationSeconds: 0.04f);

            var timeout = Time.realtimeSinceStartup + 120f;
            var sawActiveBlockFeedback = false;
            var sawPlayerOutsideOwnCourt = false;
            while (director.ResultV2 == null && Time.realtimeSinceStartup < timeout)
            {
                sawActiveBlockFeedback |= blockFeedback.IsPlaying;
                foreach (var player in players)
                {
                    sawPlayerOutsideOwnCourt |= !player.IsWithinOwnCourt;
                }
                yield return null;
            }

            Assert.That(director.ResultV2, Is.Not.Null);
            Assert.That(
                Mathf.Max(director.ResultV2.HomeScore, director.ResultV2.AwayScore),
                Is.GreaterThanOrEqualTo(15));
            Assert.That(
                Mathf.Abs(director.ResultV2.HomeScore - director.ResultV2.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(
                Mathf.Min(director.ResultV2.HomeScore, director.ResultV2.AwayScore),
                Is.GreaterThanOrEqualTo(5));
            Assert.That(director.ResultV2.PlayerStats, Has.Count.EqualTo(6));
            Assert.DoesNotThrow(() => director.ResultV2.ValidateAgainst(director.MatchContextV2));
            Assert.That(director.IsLoopRunning, Is.False);
            Assert.That(director.GroundResolvedRallies, Is.GreaterThan(0));
            Assert.That(director.PhysicalBlockContacts, Is.GreaterThan(0));
            Assert.That(sawActiveBlockFeedback, Is.True);
            Assert.That(director.BlockImpactEffects, Is.EqualTo(director.PhysicalBlockContacts));
            Assert.That(blockFeedback.PlayedCount, Is.EqualTo(director.PhysicalBlockContacts));
            Assert.That(blockFeedback.LastReboundSpeed, Is.GreaterThan(0f));
            Assert.That(blockFeedback.VisibleElementCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(director.PostBlockContinuations, Is.GreaterThan(0));
            Assert.That(director.BlueAttackContacts, Is.GreaterThan(0));
            Assert.That(director.OrangeAttackContacts, Is.GreaterThan(0));
            Assert.That(director.AiDecisionRequests, Is.GreaterThan(0));
            Assert.That(director.AiDecisionRequests, Is.EqualTo(aiSource.RequestCount));
            Assert.That(director.AiDecisionFallbacks, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.001f));
            Assert.That(director.NonSetterSetContacts, Is.GreaterThan(0));
            Assert.That(director.DefenderAttackContacts, Is.GreaterThan(0));
            Assert.That(director.IllegalContactFaults, Is.GreaterThanOrEqualTo(0));
            Assert.That(director.MaximumAppliedMovementCorrection, Is.LessThanOrEqualTo(0.70f));
            Assert.That(ball.Diagnostics.NonFiniteStates, Is.Zero);
            Assert.That(sawPlayerOutsideOwnCourt, Is.False);

            cameras.SetView(RallyCameraView.Sideline);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.Sideline));
            Assert.That(Camera.main.orthographic, Is.False);

            cameras.SetView(RallyCameraView.BallFollow);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.BallFollow));

            cameras.SetView(RallyCameraView.Tactical);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.Tactical));
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(cameras.ViewSwitchCount, Is.GreaterThanOrEqualTo(4));

        }

        [UnityTest]
        public IEnumerator LegacyV1Context_InitializesAndProducesAV1Result()
        {
            yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
            var v2Director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var scoreDisplay = Object.FindFirstObjectByType<ScoreDisplay>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);

            Assert.That(v2Director, Is.Not.Null);
            var directorObject = v2Director.gameObject;
            Object.Destroy(v2Director);
            yield return null;

            var director = directorObject.AddComponent<ThreeVsThreeRallyDirector>();
            director.Initialize(
                ball,
                players,
                CreateLegacyContext(),
                scoreDisplay,
                new ImmediateLocalWeightSource());

            var timeout = Time.realtimeSinceStartup + 120f;
            while (director.Result == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(director.Result, Is.Not.Null);
            Assert.That(director.ResultV2, Is.Null);
            Assert.That(director.MatchContext, Is.Not.Null);
            Assert.That(director.MatchContextV2, Is.Null);
            Assert.DoesNotThrow(() => director.Result.ValidateAgainst(director.MatchContext));
        }

        private sealed class ImmediateLocalWeightSource : IRallyTacticalWeightSource
        {
            public int RequestCount { get; private set; }

            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                var rolePreference = request.TacticRevision % 4 == 3 ? 0.35f : 1f;
                return Task.FromResult(
                    new RallyTacticalWeightProposal(rolePreference, 1.15f, 1f, 1f));
            }
        }

        private static MatchContextV1 CreateLegacyContext()
        {
            return MatchContextV1.Create(
                new System.Guid("11111111-2222-3333-4444-555555555555"),
                7351,
                CreateLegacyTeam("legacy-home", "Blue", TeamSide.Home, "home"),
                CreateLegacyTeam("legacy-away", "Orange", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV1 CreateLegacyTeam(
            string id,
            string name,
            TeamSide side,
            string prefix)
        {
            return new TeamSnapshotV1(
                new StableTeamId(id),
                name,
                side,
                new[]
                {
                    CreateLegacyPlayer(prefix + "-setter", "Setter", 1, PlayerPosition.Setter),
                    CreateLegacyPlayer(prefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                    CreateLegacyPlayer(prefix + "-defender", "Defender", 3, PlayerPosition.Defender)
                });
        }

        private static PlayerSnapshotV1 CreateLegacyPlayer(
            string id,
            string name,
            int number,
            PlayerPosition position)
        {
            return new PlayerSnapshotV1(
                new StablePlayerId(id),
                name,
                number,
                position,
                new PlayerAbilitySnapshotV1(0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f));
        }

        private static void AssertMirroredRoleAbilities(PrototypePlayerAgent[] players)
        {
            foreach (var role in new[]
                     {
                         PlayerRole.Setter,
                         PlayerRole.Attacker,
                         PlayerRole.Defender
                     })
            {
                PrototypePlayerAgent blue = null;
                PrototypePlayerAgent orange = null;
                foreach (var player in players)
                {
                    if (player.Id.Role != role)
                    {
                        continue;
                    }

                    if (player.Id.Team == TeamId.Blue)
                    {
                        blue = player;
                    }
                    else
                    {
                        orange = player;
                    }
                }

                Assert.That(blue, Is.Not.Null);
                Assert.That(orange, Is.Not.Null);
                AssertAbilitiesEqual(blue.Ability, orange.Ability, role);
            }
        }

        private static void AssertAbilitiesEqual(
            PlayerAbilityProfile blue,
            PlayerAbilityProfile orange,
            PlayerRole role)
        {
            Assert.That(blue.Mobility, Is.EqualTo(orange.Mobility), $"{role} mobility");
            Assert.That(blue.Reaction, Is.EqualTo(orange.Reaction), $"{role} reaction");
            Assert.That(blue.Jump, Is.EqualTo(orange.Jump), $"{role} jump");
            Assert.That(
                blue.ReceiveTechnique,
                Is.EqualTo(orange.ReceiveTechnique),
                $"{role} receive");
            Assert.That(blue.SetTechnique, Is.EqualTo(orange.SetTechnique), $"{role} set");
            Assert.That(
                blue.AttackTechnique,
                Is.EqualTo(orange.AttackTechnique),
                $"{role} attack");
            Assert.That(blue.AttackPower, Is.EqualTo(orange.AttackPower), $"{role} power");
        }
    }
}
