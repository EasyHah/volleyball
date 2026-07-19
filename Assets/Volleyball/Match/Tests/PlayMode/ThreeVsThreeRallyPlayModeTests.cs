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
            while (director.Result == null && Time.realtimeSinceStartup < timeout)
            {
                sawActiveBlockFeedback |= blockFeedback.IsPlaying;
                foreach (var player in players)
                {
                    sawPlayerOutsideOwnCourt |= !player.IsWithinOwnCourt;
                }
                yield return null;
            }

            Assert.That(director.Result, Is.Not.Null);
            Assert.That(
                Mathf.Max(director.Result.HomeScore, director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(15));
            Assert.That(
                Mathf.Abs(director.Result.HomeScore - director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(
                Mathf.Min(director.Result.HomeScore, director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(5));
            Assert.That(director.Result.PlayerStats, Has.Count.EqualTo(6));
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

        private static void AssertMirroredRoleAbilities(PrototypePlayerAgent[] players)
        {
            foreach (PlayerRole role in System.Enum.GetValues(typeof(PlayerRole)))
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
