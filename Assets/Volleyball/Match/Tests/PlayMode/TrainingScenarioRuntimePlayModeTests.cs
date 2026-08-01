using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingScenarioRuntimePlayModeTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PendingTrainingStart_IsExclusiveAndConsumedOnceByFormalBootstrap()
        {
            var scenario = CreateScenario(
                "pending",
                RallyStartRecipeV3.AfterAttack,
                new SimVector3(0f, 1.2f, -4f),
                new SimVector3(0f, -1.5f, 0f));

            TrainingScenarioStartupV1.PrepareNextTrainingStart(scenario);
            Assert.That(
                () => TrainingScenarioStartupV1.PrepareNextTrainingStart(scenario),
                Throws.InvalidOperationException);
            Assert.That(
                () => FormalMatchContextStartupV4.PrepareNextFormalStart(
                    scenario.Context),
                Throws.InvalidOperationException);

            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6",
                LoadSceneMode.Single);

            var director =
                UnityEngine.Object.FindFirstObjectByType<
                    FormalSixVsSixRallyDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.TrainingScenario, Is.SameAs(scenario));
            Assert.That(director.IsTrainingSingleRally, Is.True);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator SeededStart_UsesFormalStateWithoutHistoricalContactAndStopsAfterOneResult()
        {
            var scenario = CreateScenario(
                "seeded-ground",
                RallyStartRecipeV3.AfterAttack,
                new SimVector3(0f, 1.2f, -4f),
                new SimVector3(0f, -1.5f, 0f));
            var host = new GameObject("training-runtime-test");
            var accepted = new List<ReplayContactEvent>();
            var resolved = new List<ReplayRallyResolvedEvent>();
            try
            {
                var director =
                    FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                        host.transform,
                        scenario);
                director.ReplayContactAccepted += accepted.Add;
                director.ReplayRallyResolved += resolved.Add;

                var startDeadline = Time.realtimeSinceStartup + 10f;
                while (!director.IsLoopRunning &&
                       Time.realtimeSinceStartup < startDeadline)
                {
                    yield return null;
                }

                Assert.That(director.IsLoopRunning, Is.True);
                Assert.That(accepted, Is.Empty);
                Assert.That(director.CountedTeamTouches, Is.EqualTo(3));
                Assert.That(director.V3CountedHits, Is.EqualTo(3));
                Assert.That(
                    director.LastTouchPlayer,
                    Is.EqualTo(scenario.StartState.LastLegalActor));
                Assert.That(
                    director.V3LastCountedActor,
                    Is.EqualTo(scenario.StartState.LastLegalActor));
                Assert.That(director.ScenarioProvenance.ScenarioId,
                    Is.EqualTo(scenario.ScenarioId));
                Assert.That(director.ScenarioProvenance.ContentHash,
                    Is.EqualTo(scenario.ContentHash));

                var resultDeadline = Time.realtimeSinceStartup + 10f;
                while (!director.TrainingSingleRallyCompleted &&
                       Time.realtimeSinceStartup < resultDeadline)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(director.TrainingSingleRallyCompleted, Is.True);
                Assert.That(resolved, Has.Count.EqualTo(1));
                Assert.That(accepted, Is.Empty);
                Assert.That(director.IsLoopRunning, Is.False);
                var score = director.HomeScore + director.AwayScore;
                Assert.That(score, Is.EqualTo(1));

                yield return new WaitForSeconds(.7f);
                Assert.That(resolved, Has.Count.EqualTo(1));
                Assert.That(director.HomeScore + director.AwayScore,
                    Is.EqualTo(score));
                Assert.That(director.IsLoopRunning, Is.False);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AfterSet_ReentersCommittedGateIWithoutHistoricalContact()
        {
            var scenario = CreateScenario(
                "after-set-gate-i",
                RallyStartRecipeV3.AfterSet,
                new SimVector3(0f, 3f, -3f),
                new SimVector3(0f, 2.5f, 2.4f));
            var host = new GameObject("training-after-set-test");
            var accepted = new List<ReplayContactEvent>();
            try
            {
                var director =
                    FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                        host.transform,
                        scenario);
                director.ReplayContactAccepted += accepted.Add;

                var deadline = Time.realtimeSinceStartup + 10f;
                while (!director.IsLoopRunning &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(director.IsLoopRunning, Is.True);
                Assert.That(accepted, Is.Empty);
                Assert.That(
                    director.GateIAuthorityPhase,
                    Is.EqualTo(AttackDefenseAuthorityPhaseV3.AttackCommitted));
                Assert.That(director.CountedTeamTouches, Is.EqualTo(2));
                Assert.That(director.V3CountedHits, Is.EqualTo(2));

                deadline = Time.realtimeSinceStartup + 5f;
                while (accepted.Count == 0 &&
                       !director.TrainingSingleRallyCompleted &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(
                    accepted,
                    Has.Count.GreaterThanOrEqualTo(1),
                    "The seeded Gate I plan must be completed by real swept geometry.");
                Assert.That(
                    accepted[0].Action,
                    Is.EqualTo(TechniqueAction.Attack));
                Assert.That(accepted[0].AttackDefenseAuthority, Is.Not.Null);
                Assert.That(
                    accepted[0].RuleTransition.Before.CountedHits,
                    Is.EqualTo(2));
                Assert.That(
                    accepted[0].RuleTransition.After.CountedHits,
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AfterAcceptedBlock_SeedsOneGateIContinuationAuthority()
        {
            var scenario = CreateScenario(
                "post-block-gate-i",
                RallyStartRecipeV3.AfterAcceptedBlock,
                new SimVector3(0f, 2.4f, -.25f),
                new SimVector3(0f, 1.2f, -3.2f));
            var host = new GameObject("training-post-block-test");
            var accepted = new List<ReplayContactEvent>();
            var attempts = new List<ReplayDefenseAttemptEvent>();
            try
            {
                var director =
                    FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                        host.transform,
                        scenario);
                director.ReplayContactAccepted += accepted.Add;
                director.ReplayDefenseAttemptRecorded += attempts.Add;

                var deadline = Time.realtimeSinceStartup + 10f;
                while (!director.IsLoopRunning &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(director.IsLoopRunning, Is.True);
                Assert.That(accepted, Is.Empty);
                Assert.That(
                    director.GateIAuthorityPhase,
                    Is.EqualTo(
                        AttackDefenseAuthorityPhaseV3.AwaitingActualContact));
                Assert.That(director.CountedTeamTouches, Is.Zero);
                Assert.That(director.V3CountedHits, Is.Zero);
                Assert.That(director.NetDeflectionDispatches, Is.Zero);

                deadline = Time.realtimeSinceStartup + 5f;
                while (attempts.Count == 0 &&
                       !director.TrainingSingleRallyCompleted &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(director.PostBlockContinuations, Is.EqualTo(1));
                Assert.That(director.PostBlockPossessionDeferrals, Is.EqualTo(1));
                Assert.That(
                    director.PrematurePostBlockReceiveWindows,
                    Is.Zero);
                Assert.That(director.NetDeflectionDispatches, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        private static TrainingScenarioV1 CreateScenario(
            string id,
            RallyStartRecipeV3 recipe,
            SimVector3 ballPosition,
            SimVector3 ballVelocity)
        {
            var context =
                FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            var draft = new TrainingScenarioDraftV1
            {
                ScenarioId = "training-v1/" + id,
                DisplayName = id,
                Source = "playmode-test",
                FormatVersion = TrainingScenarioV1.FormatVersion,
                Context = context,
                FirstServingSide = TeamSide.Home,
                HomeTactics = Tactic(-1f),
                AwayTactics = Tactic(1f),
                Ai = new FormalMatchAiInputV4(),
                BallPosition = ballPosition,
                BallVelocity = ballVelocity,
                StartRecipe = recipe,
                SourceTeam = TeamSide.Home,
                LastLegalActor = context.Home.Players[1].PlayerId,
                AccessLevel = TrainingScenarioAccessLevelV1.Developer
            };

            AddPoses(draft, context.Home, TeamSide.Home);
            AddPoses(draft, context.Away, TeamSide.Away);
            return TrainingScenarioValidatorV1.Build(draft);
        }

        private static void AddPoses(
            TrainingScenarioDraftV1 draft,
            TeamSnapshotV4 team,
            TeamSide side)
        {
            for (var index = 0; index < team.Players.Count; index++)
            {
                var position =
                    PhysicalMatchConfiguration.FormalIndoorSixVsSix.PositionFor(
                        side,
                        index + 1);
                draft.Players.Add(new TrainingPlayerPoseDraftV1
                {
                    PlayerId = team.Players[index].PlayerId,
                    Position = new SimVector3(
                        position.x,
                        position.y,
                        position.z),
                    Forward = side == TeamSide.Home
                        ? new SimVector3(0f, 0f, 1f)
                        : new SimVector3(0f, 0f, -1f),
                    Pose = StickFigurePose.Ready
                });
            }
        }

        private static FormalMatchTacticInputV4 Tactic(float sideSign)
        {
            return new FormalMatchTacticInputV4
            {
                SetterX = 0f,
                SetterZ = 2.5f * sideSign,
                AttackerX = 2.4f,
                AttackerZ = 1.8f * sideSign,
                DefenderX = -2.2f,
                DefenderZ = 5.4f * sideSign,
                Blocker = PlayerRole.OutsideHitter,
                BlockX = 2.2f,
                BlockZ = .55f * sideSign,
                CoverReceiver = PlayerRole.Setter,
                CoverX = 0f,
                CoverZ = 1.6f * sideSign,
                AttackFlightSeconds = .45f
            };
        }
    }
}
