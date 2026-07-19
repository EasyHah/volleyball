using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Volleyball.AI;

namespace Volleyball.Presentation
{
    public enum AiDecisionWaitStatus
    {
        None,
        Success,
        InvalidProposal,
        TimedOut,
        Faulted,
        Cancelled
    }

    public sealed class AiDecisionTimeController : MonoBehaviour
    {
        public const float DefaultRealTimeTimeoutSeconds = 2f;
        public const float DefaultMinimumTimeScaleFactor = 0.08f;
        public const float DefaultSafetyReserveSeconds = 0.18f;
        public const float DefaultRestoreDurationSeconds = 0.18f;
        public const float DefaultMinimumSimulationWindowSeconds = 0.30f;

        private IRallyTacticalWeightSource _source;
        private CancellationTokenSource _requestCancellation;
        private Coroutine _requestCoroutine;
        private Coroutine _restoreCoroutine;
        private float _realTimeTimeoutSeconds;
        private float _minimumTimeScaleFactor;
        private float _safetyReserveSeconds;
        private float _restoreDurationSeconds;
        private float _minimumSimulationWindowSeconds;
        private float _baselineTimeScale;
        private float _baselineFixedDeltaTime;
        private float _waitStartedAt;
        private bool _ownsTimeScale;
        private int _requestVersion;

        public bool IsConfigured => _source != null;

        public bool IsWaiting { get; private set; }

        public bool IsRestoring => _restoreCoroutine != null;

        public int RequestCount { get; private set; }

        public int SuccessCount { get; private set; }

        public int FallbackCount { get; private set; }

        public float LastTargetTimeScale { get; private set; } = 1f;

        public float LastRealWaitSeconds { get; private set; }

        public AiDecisionWaitStatus LastStatus { get; private set; }

        public void Configure(
            IRallyTacticalWeightSource source,
            float realTimeTimeoutSeconds = DefaultRealTimeTimeoutSeconds,
            float minimumTimeScaleFactor = DefaultMinimumTimeScaleFactor,
            float safetyReserveSeconds = DefaultSafetyReserveSeconds,
            float restoreDurationSeconds = DefaultRestoreDurationSeconds,
            float minimumSimulationWindowSeconds = DefaultMinimumSimulationWindowSeconds)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ValidatePositive(realTimeTimeoutSeconds, nameof(realTimeTimeoutSeconds));
            ValidateTimeScaleFactor(minimumTimeScaleFactor, nameof(minimumTimeScaleFactor));
            ValidateNonNegative(safetyReserveSeconds, nameof(safetyReserveSeconds));
            ValidateNonNegative(restoreDurationSeconds, nameof(restoreDurationSeconds));
            ValidatePositive(minimumSimulationWindowSeconds, nameof(minimumSimulationWindowSeconds));

            CancelPending();
            _source = source;
            _realTimeTimeoutSeconds = realTimeTimeoutSeconds;
            _minimumTimeScaleFactor = minimumTimeScaleFactor;
            _safetyReserveSeconds = safetyReserveSeconds;
            _restoreDurationSeconds = restoreDurationSeconds;
            _minimumSimulationWindowSeconds = minimumSimulationWindowSeconds;
        }

        public bool CanRequest(float availableSimulationSeconds)
        {
            return IsConfigured &&
                   IsFinite(availableSimulationSeconds) &&
                   availableSimulationSeconds >= _minimumSimulationWindowSeconds;
        }

        public bool TryRequestWeights(
            RallyTacticalWeightRequest request,
            RallyTacticalWeights fallback,
            Action<RallyTacticalWeights, AiDecisionWaitStatus> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            if (!CanRequest(request.AvailableSimulationSeconds))
            {
                return false;
            }

            CancelPending();
            _requestVersion++;
            var requestVersion = _requestVersion;
            _baselineTimeScale = Time.timeScale;
            _baselineFixedDeltaTime = Time.fixedDeltaTime;
            _ownsTimeScale = true;
            LastTargetTimeScale = CalculateTargetTimeScale(
                request.AvailableSimulationSeconds,
                _realTimeTimeoutSeconds,
                _baselineTimeScale,
                _minimumTimeScaleFactor,
                _safetyReserveSeconds);
            ApplyTimeScale(LastTargetTimeScale);

            _requestCancellation = new CancellationTokenSource();
            _waitStartedAt = Time.realtimeSinceStartup;
            LastRealWaitSeconds = 0f;
            LastStatus = AiDecisionWaitStatus.None;
            IsWaiting = true;
            RequestCount++;

            Task<RallyTacticalWeightProposal> task;
            try
            {
                task = _source.RequestAsync(request, _requestCancellation.Token);
            }
            catch (Exception)
            {
                CompleteRequest(
                    requestVersion,
                    fallback,
                    AiDecisionWaitStatus.Faulted,
                    completed);
                return true;
            }

            if (task == null)
            {
                CompleteRequest(
                    requestVersion,
                    fallback,
                    AiDecisionWaitStatus.Faulted,
                    completed);
                return true;
            }

            _requestCoroutine = StartCoroutine(
                WaitForResponse(task, requestVersion, fallback, completed));
            return true;
        }

        public void CancelPending()
        {
            _requestVersion++;
            var wasWaiting = IsWaiting;
            IsWaiting = false;
            if (_requestCoroutine != null)
            {
                StopCoroutine(_requestCoroutine);
                _requestCoroutine = null;
            }

            if (_requestCancellation != null)
            {
                _requestCancellation.Cancel();
                _requestCancellation.Dispose();
                _requestCancellation = null;
            }

            if (wasWaiting)
            {
                LastRealWaitSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _waitStartedAt);
                LastStatus = AiDecisionWaitStatus.Cancelled;
            }

            RestoreImmediately();
        }

        public static float CalculateTargetTimeScale(
            float availableSimulationSeconds,
            float realTimeTimeoutSeconds,
            float baselineTimeScale,
            float minimumTimeScaleFactor = DefaultMinimumTimeScaleFactor,
            float safetyReserveSeconds = DefaultSafetyReserveSeconds)
        {
            ValidatePositive(availableSimulationSeconds, nameof(availableSimulationSeconds));
            ValidatePositive(realTimeTimeoutSeconds, nameof(realTimeTimeoutSeconds));
            ValidateNonNegative(baselineTimeScale, nameof(baselineTimeScale));
            ValidateTimeScaleFactor(minimumTimeScaleFactor, nameof(minimumTimeScaleFactor));
            ValidateNonNegative(safetyReserveSeconds, nameof(safetyReserveSeconds));
            if (baselineTimeScale <= 0f)
            {
                return 0f;
            }

            var usableSimulationSeconds = Mathf.Max(
                0f,
                availableSimulationSeconds - safetyReserveSeconds);
            var timeoutSafeScale = usableSimulationSeconds / realTimeTimeoutSeconds;
            return Mathf.Clamp(
                timeoutSafeScale,
                baselineTimeScale * minimumTimeScaleFactor,
                baselineTimeScale);
        }

        private IEnumerator WaitForResponse(
            Task<RallyTacticalWeightProposal> task,
            int requestVersion,
            RallyTacticalWeights fallback,
            Action<RallyTacticalWeights, AiDecisionWaitStatus> completed)
        {
            // Always cross one Unity frame so StartCoroutine can publish its handle before
            // a synchronously completed source is finalized on the main thread.
            yield return null;
            var deadline = _waitStartedAt + _realTimeTimeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            _requestCoroutine = null;
            if (requestVersion != _requestVersion || !IsWaiting)
            {
                yield break;
            }

            if (!task.IsCompleted)
            {
                _requestCancellation.Cancel();
                CompleteRequest(
                    requestVersion,
                    fallback,
                    AiDecisionWaitStatus.TimedOut,
                    completed);
                yield break;
            }

            try
            {
                var proposal = task.GetAwaiter().GetResult();
                if (RallyTacticalWeights.TryResolve(proposal, out var resolved))
                {
                    CompleteRequest(
                        requestVersion,
                        resolved,
                        AiDecisionWaitStatus.Success,
                        completed);
                }
                else
                {
                    CompleteRequest(
                        requestVersion,
                        fallback,
                        AiDecisionWaitStatus.InvalidProposal,
                        completed);
                }
            }
            catch (OperationCanceledException)
            {
                CompleteRequest(
                    requestVersion,
                    fallback,
                    AiDecisionWaitStatus.Cancelled,
                    completed);
            }
            catch (Exception)
            {
                CompleteRequest(
                    requestVersion,
                    fallback,
                    AiDecisionWaitStatus.Faulted,
                    completed);
            }
        }

        private void CompleteRequest(
            int requestVersion,
            RallyTacticalWeights weights,
            AiDecisionWaitStatus status,
            Action<RallyTacticalWeights, AiDecisionWaitStatus> completed)
        {
            if (requestVersion != _requestVersion || !IsWaiting)
            {
                return;
            }

            IsWaiting = false;
            LastRealWaitSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _waitStartedAt);
            LastStatus = status;
            if (status == AiDecisionWaitStatus.Success)
            {
                SuccessCount++;
            }
            else
            {
                FallbackCount++;
            }

            if (_requestCancellation != null)
            {
                _requestCancellation.Dispose();
                _requestCancellation = null;
            }

            BeginRestore();
            completed(weights, status);
        }

        private void BeginRestore()
        {
            if (!_ownsTimeScale)
            {
                return;
            }

            if (_restoreDurationSeconds <= 0f ||
                Mathf.Approximately(Time.timeScale, _baselineTimeScale))
            {
                RestoreImmediately();
                return;
            }

            _restoreCoroutine = StartCoroutine(RestoreTimeScale());
        }

        private IEnumerator RestoreTimeScale()
        {
            var startTimeScale = Time.timeScale;
            var startFixedDeltaTime = Time.fixedDeltaTime;
            var elapsed = 0f;
            while (elapsed < _restoreDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / _restoreDurationSeconds);
                progress = progress * progress * (3f - (2f * progress));
                Time.timeScale = Mathf.Lerp(startTimeScale, _baselineTimeScale, progress);
                Time.fixedDeltaTime = Mathf.Lerp(
                    startFixedDeltaTime,
                    _baselineFixedDeltaTime,
                    progress);
                yield return null;
            }

            Time.timeScale = _baselineTimeScale;
            Time.fixedDeltaTime = _baselineFixedDeltaTime;
            _restoreCoroutine = null;
            _ownsTimeScale = false;
        }

        private void RestoreImmediately()
        {
            if (_restoreCoroutine != null)
            {
                StopCoroutine(_restoreCoroutine);
                _restoreCoroutine = null;
            }

            if (!_ownsTimeScale)
            {
                return;
            }

            Time.timeScale = _baselineTimeScale;
            Time.fixedDeltaTime = _baselineFixedDeltaTime;
            _ownsTimeScale = false;
        }

        private void ApplyTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
            if (_baselineTimeScale > 0.000001f)
            {
                Time.fixedDeltaTime = _baselineFixedDeltaTime *
                                      (timeScale / _baselineTimeScale);
            }
        }

        private void OnDisable()
        {
            CancelPending();
        }

        private static void ValidatePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateTimeScaleFactor(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
