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

        [UnityTest]
        public IEnumerator InvalidProposal_RestoresMatchTimeAndUsesFallback()
        {
            var root = new GameObject("AiDecisionTimeInvalidTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(new ImmediateSource(
                    new RallyTacticalWeightProposal(float.NaN, 1f, 1f, 1f)),
                    realTimeTimeoutSeconds: 0.5f,
                    restoreDurationSeconds: 0f,
                    minimumSimulationWindowSeconds: 0.1f);
                var completed = false;
                var status = AiDecisionWaitStatus.None;
                controller.TryRequestWeights(
                    CreateRequest(0.4f),
                    RallyTacticalWeights.Default,
                    (_, resolvedStatus) =>
                    {
                        status = resolvedStatus;
                        completed = true;
                    });

                yield return null;
                yield return null;

                Assert.That(completed, Is.True);
                Assert.That(status, Is.EqualTo(AiDecisionWaitStatus.InvalidProposal));
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
            }
            finally
            {
                Object.Destroy(root);
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator SynchronousException_RestoresMatchTimeAndUsesFallback()
        {
            var root = new GameObject("AiDecisionTimeExceptionTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(
                    new ThrowingSource(),
                    realTimeTimeoutSeconds: 0.5f,
                    restoreDurationSeconds: 0f,
                    minimumSimulationWindowSeconds: 0.1f);
                var status = AiDecisionWaitStatus.None;

                Assert.That(controller.TryRequestWeights(
                    CreateRequest(0.4f),
                    RallyTacticalWeights.Default,
                    (_, resolvedStatus) => status = resolvedStatus), Is.True);

                Assert.That(status, Is.EqualTo(AiDecisionWaitStatus.Faulted));
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator ExplicitCancellation_RestoresMatchTimeImmediately()
        {
            var root = new GameObject("AiDecisionTimeCancelTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(
                    new DeferredWeightSource(),
                    realTimeTimeoutSeconds: 0.5f,
                    restoreDurationSeconds: 0.1f,
                    minimumSimulationWindowSeconds: 0.1f);
                controller.TryRequestWeights(
                    CreateRequest(0.4f),
                    RallyTacticalWeights.Default,
                    (_, _) => { });
                Assert.That(Time.timeScale, Is.LessThan(1f));

                controller.CancelPending();

                Assert.That(controller.LastStatus, Is.EqualTo(AiDecisionWaitStatus.Cancelled));
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator ComponentDisable_CancelsPendingRequestAndRestoresMatchTime()
        {
            var root = new GameObject("AiDecisionTimeDisableTest");
            var originalTimeScale = Time.timeScale;
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                var controller = root.AddComponent<AiDecisionTimeController>();
                controller.Configure(
                    new DeferredWeightSource(),
                    realTimeTimeoutSeconds: 0.5f,
                    restoreDurationSeconds: 0.1f,
                    minimumSimulationWindowSeconds: 0.1f);
                controller.TryRequestWeights(
                    CreateRequest(0.4f),
                    RallyTacticalWeights.Default,
                    (_, _) => { });

                controller.enabled = false;

                Assert.That(controller.LastStatus, Is.EqualTo(AiDecisionWaitStatus.Cancelled));
                Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.001f));
                yield return null;
            }
            finally
            {
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

        private sealed class ImmediateSource : IRallyTacticalWeightSource
        {
            private readonly RallyTacticalWeightProposal _proposal;

            public ImmediateSource(RallyTacticalWeightProposal proposal)
            {
                _proposal = proposal;
            }

            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_proposal);
            }
        }

        private sealed class ThrowingSource : IRallyTacticalWeightSource
        {
            public Task<RallyTacticalWeightProposal> RequestAsync(
                RallyTacticalWeightRequest request,
                CancellationToken cancellationToken)
            {
                throw new System.InvalidOperationException("deterministic test failure");
            }
        }
    }
}
