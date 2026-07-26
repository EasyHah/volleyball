using System.Collections;
using System.Reflection;
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
using DominantHandV4 = Volleyball.Shared.Contracts.DominantHandV4;
using MatchAttributeDerivationConfigV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationConfigV4;
using MatchAttributeDerivationV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationV4;
using PhysicalBaseAttributesV4 = Volleyball.Shared.Contracts.PhysicalBaseAttributesV4;
using PlayerPosition = Volleyball.Shared.Contracts.PlayerPosition;
using TechnicalBaseAttributesV4 = Volleyball.Shared.Contracts.TechnicalBaseAttributesV4;

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
            Assert.That(director.PrototypeContext, Is.Not.Null);
            Assert.That(director.MatchContext, Is.Null);
            Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Disabled));
            Assert.That(director.V3RuleTransitions, Is.Zero);
            Assert.That(director.V3RuleParityMatches, Is.Zero);
            Assert.That(director.V3RuleIntentionalCorrections, Is.Zero);
            Assert.That(director.V3RuleUnexpectedMismatches, Is.Zero);
            Assert.That(director.LastV3RuleDiagnostic, Is.Empty);
            Assert.That(
                typeof(PhysicalMatchRallyDirector)
                    .GetField("_v3RulesAdapter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(director),
                Is.Null);
            Assert.That(players, Has.Some.Matches<PrototypePlayerAgent>(
                player => player.Id.Role == PlayerRole.Attacker &&
                          player.Ability.PlannedAttackContactHeightMeters > 3.2f));
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
            while (director.PrototypeResult == null && Time.realtimeSinceStartup < timeout)
            {
                sawActiveBlockFeedback |= blockFeedback.IsPlaying;
                foreach (var player in players)
                {
                    sawPlayerOutsideOwnCourt |= !player.IsWithinOwnCourt;
                }
                yield return null;
            }

            Assert.That(director.PrototypeResult, Is.Not.Null);
            Assert.That(
                Mathf.Max(director.PrototypeResult.HomeScore, director.PrototypeResult.AwayScore),
                Is.GreaterThanOrEqualTo(15));
            Assert.That(
                Mathf.Abs(director.PrototypeResult.HomeScore - director.PrototypeResult.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(
                Mathf.Min(director.PrototypeResult.HomeScore, director.PrototypeResult.AwayScore),
                Is.GreaterThanOrEqualTo(5));
            Assert.That(director.PrototypeResult.PlayerStats, Has.Count.EqualTo(6));
            Assert.DoesNotThrow(() => director.PrototypeResult.ValidateAgainst(director.PrototypeContext));
            Assert.That(director.IsLoopRunning, Is.False);
            Assert.That(director.GroundResolvedRallies, Is.GreaterThan(0));
            Assert.That(director.PhysicalBlockContacts, Is.GreaterThan(0));
            Assert.That(sawActiveBlockFeedback, Is.True);
            Assert.That(director.BlockImpactEffects, Is.EqualTo(director.PhysicalBlockContacts));
            Assert.That(blockFeedback.PlayedCount, Is.EqualTo(director.PhysicalBlockContacts));
            Assert.That(blockFeedback.LastReboundSpeed, Is.GreaterThan(0f));
            Assert.That(blockFeedback.VisibleElementCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(director.PostBlockContinuations, Is.GreaterThan(0));
            Assert.That(director.ScheduledMultiBlockUnits, Is.GreaterThan(0));
            Assert.That(director.BlueAttackContacts, Is.GreaterThan(0));
            Assert.That(director.OrangeAttackContacts, Is.GreaterThan(0));
            Assert.That(director.AiDecisionRequests, Is.GreaterThan(0));
            Assert.That(director.AiDecisionRequests, Is.EqualTo(aiSource.RequestCount));
            Assert.That(director.AiDecisionFallbacks, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale).Within(0.001f));
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
        [Timeout(180000)]
        public IEnumerator AcceptedBlock_DefersReceiveUntilCrossingAndLetsGroundRefereeScore()
        {
            yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();
            var originalTimeScale = Time.timeScale;
            try
            {
                var timeout = Time.realtimeSinceStartup + 120f;
                while (director.PostBlockGroundPoints == 0 &&
                       director.PrototypeResult == null &&
                       Time.realtimeSinceStartup < timeout)
                {
                    Time.timeScale = 8f;
                    yield return null;
                }

                Assert.That(director.PostBlockPossessionDeferrals, Is.GreaterThan(0));
                Assert.That(director.PrematurePostBlockReceiveWindows, Is.Zero);
                Assert.That(director.PrematurePostBlockEmergencyWindows, Is.Zero);
                Assert.That(director.PostBlockGroundPoints, Is.GreaterThan(0));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator InfeasibleSetters_ProduceARealNonSetterOrganizationContact()
        {
            yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            var originalTimeScale = Time.timeScale;
            foreach (var player in players)
            {
                if (player.Id.Role != PlayerRole.Setter)
                {
                    continue;
                }

                var ability = player.Ability;
                player.SetAbility(CreateAbility(
                    mobility: 0f,
                    reaction: 0f,
                    jump: ability.Jump,
                    receiveTechnique: ability.ReceiveTechnique,
                    setTechnique: 0f,
                    attackTechnique: ability.AttackDirectionControl,
                    attackPower: ability.AttackPowerCapacity,
                    plannedContactHeightMeters:
                        ability.PlannedAttackContactHeightMeters));
            }

            director.ConfigureAiDecisionSource(
                new ImmediateLocalWeightSource(),
                realTimeTimeoutSeconds: 0.5f,
                restoreDurationSeconds: 0.04f);
            try
            {
                var timeout = Time.realtimeSinceStartup + 120f;
                while (director.NonSetterSetContacts == 0 &&
                       director.PrototypeResult == null &&
                       Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.That(director.NonSetterSetContacts, Is.GreaterThan(0));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
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

        private static PlayerAbilityProfile CreateAbility(
            float mobility,
            float reaction,
            float jump,
            float receiveTechnique,
            float setTechnique,
            float attackTechnique,
            float attackPower,
            float plannedContactHeightMeters)
        {
            var standingReach =
                plannedContactHeightMeters - 0.25f - (0.60f * jump);
            return new PlayerAbilityProfile(
                MatchAttributeDerivationV4.Derive(
                    new PhysicalBaseAttributesV4(
                        1.80f,
                        standingReach,
                        jump,
                        mobility,
                        reaction,
                        0.8f),
                    new TechnicalBaseAttributesV4(
                        attackTechnique,
                        attackPower,
                        jump,
                        receiveTechnique,
                        receiveTechnique,
                        setTechnique,
                        attackTechnique,
                        setTechnique,
                        reaction),
                    DominantHandV4.Right,
                    MatchAttributeDerivationConfigV4.Version1));
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
                blue.AttackDirectionControl,
                Is.EqualTo(orange.AttackDirectionControl),
                $"{role} attack");
            Assert.That(blue.AttackPowerCapacity, Is.EqualTo(orange.AttackPowerCapacity), $"{role} power");
        }
    }
}
