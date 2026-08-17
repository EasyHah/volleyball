using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using Object = UnityEngine.Object;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingRallyV5PlayModeTests
    {
        [UnityTest]
        [Timeout(45000)]
        public IEnumerator LegalRun_LaunchesFrozenBallAndStopsAfterOneRally()
        {
            var host = new GameObject("TrainingRallyV5LegalTest");
            try
            {
                var draft = MatchSetupDraftV1.CreateDefault(
                    FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5(),
                    TeamSide.Home);
                var snapshot = new MatchSetupEditorV1(draft).Freeze();
                var director = FormalSixVsSixRallyBootstrap
                    .InitializeTrainingRallyV5(
                        host.transform, new TrainingRallyStartV5(snapshot));
                var ball = host.GetComponentInChildren<SimulatedBall>();
                var launchObserved = false;
                var launchedPosition = SimVector3.Zero;
                var launchedVelocity = SimVector3.Zero;
                director.ReplayServeStarted += _ =>
                {
                    launchObserved = true;
                    launchedPosition = ball.State.Position;
                    launchedVelocity = ball.State.Velocity;
                };

                yield return new WaitForSeconds(.6f);
                for (var interval = 0; interval < 24 &&
                    director.TrainingRallyOutcomeV1 == null; interval++)
                    yield return new WaitForSeconds(.5f);

                Assert.That(launchObserved, Is.True);
                Assert.That(launchedPosition,
                    Is.EqualTo(snapshot.BallPosition));
                Assert.That(launchedVelocity,
                    Is.EqualTo(snapshot.BallVelocity));
                Assert.That(director.TrainingRallyOutcomeV1, Is.Not.Null);
                Assert.That(director.TrainingRallyOutcomeV1.SetupHash,
                    Is.EqualTo(snapshot.SetupHash));
                Assert.That(director.TrainingRallyOutcomeV1.HomeScoreDelta +
                    director.TrainingRallyOutcomeV1.AwayScoreDelta, Is.EqualTo(1));
                Assert.That(director.ResultV5, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PositionFaultRun_UsesFrozenNativeV5SetupAndEmitsOnlyTrainingOutcome()
        {
            var host = new GameObject("TrainingRallyV5Test");
            try
            {
                var context = FormalSixVsSixRallyBootstrap
                    .CreateDefaultFormalContextV5();
                var contextJson = ContractJson.SerializeV5(context);
                var draft = MatchSetupDraftV1.CreateDefault(context, TeamSide.Home);
                var editor = new MatchSetupEditorV1(draft);
                var homeSlotFour = draft.HomeRotation[3];
                editor.SetPlayerPosition(homeSlotFour,
                    new SimVector3(-3f, 0f, -7f));
                var snapshot = editor.Freeze();
                var start = new TrainingRallyStartV5(snapshot);

                var director = FormalSixVsSixRallyBootstrap
                    .InitializeTrainingRallyV5(host.transform, start);
                yield return new WaitForSeconds(.6f);

                Assert.That(director.MatchContextV5, Is.SameAs(context));
                Assert.That(director.V3RulesMode, Is.EqualTo(V3RulesMode.Authority));
                Assert.That(director.IsTrainingSingleRally, Is.True);
                Assert.That(director.TrainingSingleRallyCompleted, Is.True);
                Assert.That(director.TrainingRallyOutcomeV1, Is.Not.Null);
                Assert.That(director.TrainingRallyOutcomeV1.SetupHash,
                    Is.EqualTo(snapshot.SetupHash));
                Assert.That(director.TrainingRallyOutcomeV1.CompletionReason,
                    Is.EqualTo("PositionFault"));
                Assert.That(director.TrainingRallyOutcomeV1.TouchCount, Is.Zero);
                Assert.That(director.TrainingRallyOutcomeV1.PositionFaults,
                    Is.Not.Empty);
                Assert.That(director.ResultV5, Is.Null);
                Assert.That(host.GetComponent<MatchReplayRecorderV5>(), Is.Null);
                Assert.That(ContractJson.SerializeV5(context), Is.EqualTo(contextJson));

                var runtimePlayer = host.GetComponentsInChildren<PrototypePlayerAgent>()
                    .Single(value =>
                    value.StableId.Equals(homeSlotFour));
                var frozen = snapshot.Players.Single(value =>
                    value.PlayerId.Equals(homeSlotFour)).Position;
                Assert.That(runtimePlayer.transform.position.x,
                    Is.EqualTo(frozen.X).Within(.001f));
                Assert.That(runtimePlayer.transform.position.z,
                    Is.EqualTo(frozen.Z).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
