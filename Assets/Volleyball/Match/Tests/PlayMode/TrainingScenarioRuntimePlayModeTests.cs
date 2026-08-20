using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
                RallyStartRecipeV3.ServeFlight,
                new SimVector3(0f, 2.1f, -10f),
                new SimVector3(0f, 2.5f, 12f));

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
        public IEnumerator ServeStart_UsesNoHistoricalContactAndStopsAfterOneResult()
        {
            var scenario = CreateScenario(
                "serve-start",
                RallyStartRecipeV3.ServeFlight,
                new SimVector3(0f, 2.1f, -10f),
                new SimVector3(0f, 2.5f, 12f));
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

        [Test]
        public void NonServeStarts_AreRejectedBeforeRuntime()
        {
            Assert.That(() => CreateScenario("after-set", RallyStartRecipeV3.AfterSet,
                    new SimVector3(0f, 2.1f, -10f), new SimVector3(0f, 2.5f, 12f)),
                Throws.ArgumentException.With.Message.Contains(TrainingScenarioIssueCodesV1.InvalidRallyStart));
            Assert.That(() => CreateScenario("after-block", RallyStartRecipeV3.AfterAcceptedBlock,
                    new SimVector3(0f, 2.1f, -10f), new SimVector3(0f, 2.5f, 12f)),
                Throws.ArgumentException.With.Message.Contains(TrainingScenarioIssueCodesV1.InvalidRallyStart));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PositionFault_EndsTrainingBeforeAnyPlayerContact()
        {
            var scenario = CreateScenario(
                "position-fault",
                RallyStartRecipeV3.ServeFlight,
                new SimVector3(0f, 2.1f, -10f),
                new SimVector3(0f, 2.5f, 12f),
                positionFault: true);
            var host = new GameObject("training-position-fault-test");
            try
            {
                var director = FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                    host.transform, scenario);
                var faults = new List<ReplayPositionFaultEvent>();
                var contacts = 0;
                ReplayRallyResolvedEvent resolution = null;
                director.ReplayPositionFault += faults.Add;
                director.ReplayContactAccepted += _ => contacts++;
                director.ReplayRallyResolved += value => resolution = value;

                var deadline = Time.realtimeSinceStartup + 10f;
                while (!director.TrainingSingleRallyCompleted &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(director.TrainingSingleRallyCompleted, Is.True);
                Assert.That(faults, Has.Count.EqualTo(1));
                Assert.That(faults[0].Evidence, Is.Empty);
                Assert.That(contacts, Is.Zero);
                Assert.That(resolution?.Reason, Is.EqualTo("PositionFault"));
                Assert.That(resolution?.Team,
                    Is.EqualTo(Volleyball.Domain.Prototype.TeamId.Orange));
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
            SimVector3 ballVelocity,
            bool positionFault = false)
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
                LastLegalActor = recipe == RallyStartRecipeV3.ServeFlight
                    ? null
                    : context.Home.Players[1].PlayerId,
                AccessLevel = TrainingScenarioAccessLevelV1.Developer
            };

            AddPoses(draft, context.Home, TeamSide.Home);
            AddPoses(draft, context.Away, TeamSide.Away);
            draft.HomeRotation.AddRange(context.Home.RotationOrder.Select(value => value.PlayerId));
            draft.AwayRotation.AddRange(context.Away.RotationOrder.Select(value => value.PlayerId));
            draft.RotationLocked = true;
            if (positionFault)
            {
                var slot4 = draft.Players.Single(value =>
                    value.PlayerId.Equals(draft.HomeRotation[3]));
                var slot5 = draft.Players.Single(value =>
                    value.PlayerId.Equals(draft.HomeRotation[4]));
                slot4.Position = new SimVector3(
                    slot4.Position.X,
                    slot4.Position.Y,
                    slot5.Position.Z - 1f);
            }
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
