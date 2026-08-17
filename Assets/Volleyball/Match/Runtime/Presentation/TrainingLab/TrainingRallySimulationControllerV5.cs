using System;
using UnityEngine;
using Volleyball.Match.Domain.PreServe;

namespace Volleyball.Presentation.TrainingLab
{
    public interface ITrainingRallySimulationControllerV5
    {
        event Action<TrainingRallyOutcomeV1> Completed;
        event Action<string> Faulted;
        bool HasRuntime { get; }
        bool IsPaused { get; }
        MatchSetupSnapshotV1 StartedSnapshot { get; }
        void Start(MatchSetupSnapshotV1 snapshot);
        void Pause();
        void Resume();
        void Step();
        void Reset();
    }

    public sealed class TrainingRallySimulationControllerV5 :
        ITrainingRallySimulationControllerV5, IDisposable
    {
        private readonly Transform _host;
        private GameObject _runtimeRoot;
        private FormalSixVsSixRallyDirector _director;
        private SimulatedBall _ball;
        private TrainingRallyOutcomeWatcherV1 _watcher;
        private float _timeScaleBeforeRun = 1f;
        private bool _paused;
        private bool _disposed;

        public TrainingRallySimulationControllerV5(Transform host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public event Action<TrainingRallyOutcomeV1> Completed;
        public event Action<string> Faulted;
        public bool HasRuntime => _runtimeRoot != null;
        public bool IsPaused => _paused;
        public MatchSetupSnapshotV1 StartedSnapshot { get; private set; }

        public void Start(MatchSetupSnapshotV1 snapshot)
        {
            ThrowIfDisposed();
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (HasRuntime) throw new InvalidOperationException(
                "Reset the current V5 training rally before starting again.");
            _timeScaleBeforeRun = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = _timeScaleBeforeRun;
            try
            {
                StartedSnapshot = snapshot;
                _runtimeRoot = new GameObject("TrainingRallyRuntimeV5");
                _runtimeRoot.transform.SetParent(_host, false);
                _director = FormalSixVsSixRallyBootstrap
                    .InitializeTrainingRallyV5(_runtimeRoot.transform,
                        new TrainingRallyStartV5(snapshot));
                _ball = _runtimeRoot.GetComponentInChildren<SimulatedBall>();
                if (_ball == null || _runtimeRoot
                        .GetComponentsInChildren<PrototypePlayerAgent>().Length != 12)
                    throw new InvalidOperationException(
                        "Native V5 training composition is incomplete.");
                _watcher = _runtimeRoot.AddComponent<
                    TrainingRallyOutcomeWatcherV1>();
                _watcher.Initialize(_director, OnCompleted, OnFaulted);
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
            RequireActive();
            if (_paused) return;
            Time.timeScale = 0f;
            _paused = true;
        }

        public void Resume()
        {
            RequireActive();
            if (!_paused) return;
            Time.timeScale = _timeScaleBeforeRun;
            _paused = false;
        }

        public void Step()
        {
            RequireActive();
            if (!_paused) throw new InvalidOperationException(
                "Fixed-step advance requires a paused V5 training rally.");
            _ball.AdvanceSimulation(SimulatedBall.DefaultFixedStep);
        }

        public void Reset()
        {
            _watcher = null;
            _director = null;
            _ball = null;
            StartedSnapshot = null;
            if (_paused || Time.timeScale <= 0f)
                Time.timeScale = _timeScaleBeforeRun > 0f
                    ? _timeScaleBeforeRun : 1f;
            _paused = false;
            if (_runtimeRoot == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_runtimeRoot);
            else
                UnityEngine.Object.DestroyImmediate(_runtimeRoot);
            _runtimeRoot = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Reset();
            _disposed = true;
        }

        private void OnCompleted(TrainingRallyOutcomeV1 outcome)
        {
            Completed?.Invoke(outcome);
        }

        private void OnFaulted(string message)
        {
            Faulted?.Invoke(message);
        }

        private void RequireActive()
        {
            ThrowIfDisposed();
            if (!HasRuntime || _director.TrainingSingleRallyCompleted)
                throw new InvalidOperationException(
                    "A live native V5 training rally is required.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(
                nameof(TrainingRallySimulationControllerV5));
        }
    }

    internal sealed class TrainingRallyOutcomeWatcherV1 : MonoBehaviour
    {
        private FormalSixVsSixRallyDirector _director;
        private Action<TrainingRallyOutcomeV1> _completed;
        private Action<string> _faulted;
        private bool _reported;

        public void Initialize(FormalSixVsSixRallyDirector director,
            Action<TrainingRallyOutcomeV1> completed, Action<string> faulted)
        {
            _director = director;
            _completed = completed;
            _faulted = faulted;
        }

        private void Update()
        {
            if (_reported || _director == null) return;
            try
            {
                var outcome = _director.TrainingRallyOutcomeV1;
                if (outcome == null) return;
                _reported = true;
                _completed?.Invoke(outcome);
            }
            catch (Exception exception)
            {
                _reported = true;
                _faulted?.Invoke(exception.Message);
            }
        }
    }
}
