using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.PlayModeTests
{
    public sealed class AiDecisionTimeControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator PendingResponse_SlowsTimeThenRestoresItAfterSuccess()
        {
            var root = new GameObject("AiDecisionTimeSuccessTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var source = new DeferredWeightSource();
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(
                    source,
                    realTimeTimeoutSeconds: 0.5f,
                    minimumTimeScaleFactor: 0.1f,
                    safetyReserveSeconds: 0.1f,
                    restoreDurationSeconds: 0.04f,
                    minimumSimulationWindowSeconds: 0.1f);
                var completed = false;
                var completedStatus = AiDecisionWaitStatus.None;
                var completedWeights = default(RallyTacticalWeights);

                Assert.That(controller.TryRequestWeights(
                    CreateRequest(0.4f),
                    RallyTacticalWeights.Default,
                    (weights, status) =>
                    {
                        completed = true;
                        completedWeights = weights;
                        completedStatus = status;
                    }), Is.True);

                Assert.That(controller.IsWaiting, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0.6f).Within(0.01f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.012f).Within(0.001f));

                source.Complete(new RallyTacticalWeightProposal(1.2f, 0.8f, 1.4f, 0.6f));
                var deadline = Time.realtimeSinceStartup + 1f;
                while ((!completed || controller.IsRestoring) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(completed, Is.True);
                Assert.That(completedStatus, Is.EqualTo(AiDecisionWaitStatus.Success));
                Assert.That(completedWeights, Is.EqualTo(
                    new RallyTacticalWeights(1.2f, 0.8f, 1.4f, 0.6f)));
                Assert.That(controller.IsWaiting, Is.False);
                Assert.That(controller.SuccessCount, Is.EqualTo(1));
                Assert.That(controller.FallbackCount, Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
            }
            finally
            {
                var controller = root.GetComponent<AiDecisionTimeController>();
                controller?.CancelPending();
                Object.Destroy(root);
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator MissingResponse_TimesOutInRealTimeAndUsesLocalFallback()
        {
            var root = new GameObject("AiDecisionTimeTimeoutTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var source = new DeferredWeightSource();
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(
                    source,
                    realTimeTimeoutSeconds: 0.05f,
                    minimumTimeScaleFactor: 0.1f,
                    safetyReserveSeconds: 0.1f,
                    restoreDurationSeconds: 0.02f,
                    minimumSimulationWindowSeconds: 0.1f);
                var fallback = new RallyTacticalWeights(0.35f, 1.15f, 1f, 1f);
                var completed = false;
                var completedStatus = AiDecisionWaitStatus.None;
                var completedWeights = default(RallyTacticalWeights);

                Assert.That(controller.TryRequestWeights(
                    CreateRequest(0.12f),
                    fallback,
                    (weights, status) =>
                    {
                        completed = true;
                        completedWeights = weights;
                        completedStatus = status;
                    }), Is.True);

                Assert.That(Time.timeScale, Is.LessThan(1f));
                var deadline = Time.realtimeSinceStartup + 1f;
                while ((!completed || controller.IsRestoring) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(completed, Is.True);
                Assert.That(completedStatus, Is.EqualTo(AiDecisionWaitStatus.TimedOut));
                Assert.That(completedWeights, Is.EqualTo(fallback));
                Assert.That(source.CancellationRequested, Is.True);
                Assert.That(controller.FallbackCount, Is.EqualTo(1));
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
            }
            finally
            {
                var controller = root.GetComponent<AiDecisionTimeController>();
                controller?.CancelPending();
                Object.Destroy(root);
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        private static RallyTacticalWeightRequest CreateRequest(float availableSimulationSeconds)
        {
            return new RallyTacticalWeightRequest(
                TeamId.Blue,
                RallyDecisionStage.Receive,
                tacticRevision: 2,
                requestSequence: 5,
                countedTeamTouches: 0,
                availableSimulationSeconds,
                new SimVector3(0f, 3f, -2f),
                new SimVector3(0f, -2f, -7f));
        }

        private sealed class DeferredWeightSource : IRallyTacticalWeightSource
        {
            private readonly TaskCompletionSource<RallyTacticalWeightProposal> _completion =
                new TaskCompletionSource<RallyTacticalWeightProposal>();
            private CancellationToken _cancellationToken;

            public bool CancellationRequested => _cancellationToken.IsCancellationRequested;

            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                return _completion.Task;
            }

            public void Complete(RallyTacticalWeightProposal proposal)
            {
                _completion.TrySetResult(proposal);
            }
        }
    }
}
