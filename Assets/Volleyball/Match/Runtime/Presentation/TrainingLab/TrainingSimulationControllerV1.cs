using System;
using UnityEngine;
using Volleyball.Domain.Prototype;

namespace Volleyball.Presentation.TrainingLab
{
    public interface ITrainingSimulationControllerV1
    {
        event Action<TrainingRunEvidenceV1> Completed;
        event Action<string> Faulted;

        bool HasRuntime { get; }
        bool IsPaused { get; }
        TrainingRunEvidenceV1 CurrentEvidence { get; }

        void Start(TrainingScenarioV1 scenario);
        void Pause();
        void Resume();
        void Step();
        void Reset();
    }

    public sealed class TrainingSimulationControllerV1 :
        ITrainingSimulationControllerV1,
        IDisposable
    {
        private readonly Transform _host;
        private GameObject _runtimeRoot;
        private FormalSixVsSixRallyDirector _director;
        private SimulatedBall _ball;
        private TrainingTimelineRecorderV1 _recorder;
        private float _timeScaleBeforeRun = 1f;
        private bool _paused;
        private bool _disposed;
        private TrainingRunEvidenceV1 _finalEvidence;

        public TrainingSimulationControllerV1(Transform host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public event Action<TrainingRunEvidenceV1> Completed;
        public event Action<string> Faulted;

        public bool HasRuntime => _runtimeRoot != null;
        public bool IsPaused => _paused;
        public TrainingRunEvidenceV1 CurrentEvidence =>
            _finalEvidence ?? _recorder?.Capture();

        public void Start(TrainingScenarioV1 scenario)
        {
            ThrowIfDisposed();
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (HasRuntime)
                throw new InvalidOperationException(
                    "Reset the current training runtime before starting again.");

            _timeScaleBeforeRun = Time.timeScale;
            if (_timeScaleBeforeRun <= 0f)
            {
                _timeScaleBeforeRun = 1f;
                Time.timeScale = 1f;
            }

            try
            {
                _runtimeRoot = new GameObject("FormalTrainingRuntimeV1");
                _runtimeRoot.transform.SetParent(_host, false);
                _director =
                    FormalSixVsSixRallyBootstrap.InitializeTrainingScenario(
                        _runtimeRoot.transform,
                        scenario);
                _ball = _runtimeRoot.GetComponentInChildren<SimulatedBall>();
                var players =
                    _runtimeRoot.GetComponentsInChildren<PrototypePlayerAgent>();
                if (_ball == null || players.Length != 12)
                    throw new InvalidOperationException(
                        "Formal training composition is incomplete.");
                _recorder = new TrainingTimelineRecorderV1(
                    _director,
                    _ball,
                    players);
                _director.ReplayRallyResolved += OnRallyResolved;
                _finalEvidence = null;
                _paused = false;
            }
            catch (Exception exception)
            {
                Reset();
                Faulted?.Invoke(exception.Message);
                throw;
            }
        }

        public void Pause()
        {
            RequireActiveRuntime();
            if (_paused) return;
            Time.timeScale = 0f;
            _paused = true;
        }

        public void Resume()
        {
            RequireActiveRuntime();
            if (!_paused) return;
            Time.timeScale = _timeScaleBeforeRun;
            _paused = false;
        }

        public void Step()
        {
            RequireActiveRuntime();
            if (!_paused)
                throw new InvalidOperationException(
                    "Fixed-step advance is only available while paused.");
            _ball.AdvanceSimulation(SimulatedBall.DefaultFixedStep);
        }

        public void Reset()
        {
            if (_director != null)
                _director.ReplayRallyResolved -= OnRallyResolved;
            _recorder?.Dispose();
            _recorder = null;
            _director = null;
            _ball = null;
            _finalEvidence = null;
            if (_paused || Time.timeScale <= 0f)
                Time.timeScale = _timeScaleBeforeRun > 0f
                    ? _timeScaleBeforeRun
                    : 1f;
            _paused = false;
            if (_runtimeRoot != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runtimeRoot);
                else
                    UnityEngine.Object.DestroyImmediate(_runtimeRoot);
                _runtimeRoot = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Reset();
            _disposed = true;
        }

        private void OnRallyResolved(ReplayRallyResolvedEvent _)
        {
            try
            {
                _finalEvidence = _recorder.Capture();
                Completed?.Invoke(_finalEvidence);
            }
            catch (Exception exception)
            {
                Faulted?.Invoke(exception.Message);
            }
        }

        private void RequireActiveRuntime()
        {
            ThrowIfDisposed();
            if (!HasRuntime || _finalEvidence != null)
                throw new InvalidOperationException(
                    "A live formal training runtime is required.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(TrainingSimulationControllerV1));
        }
    }
}
