using System;
using System.Collections.Generic;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingLab3DPreviewWindowV1
    {
        private readonly Dictionary<string, CameraState> _bookmarks =
            new Dictionary<string, CameraState>(StringComparer.Ordinal);
        private readonly CameraState _defaultCamera = new CameraState(
            32f, 28f, 18f);

        public TrainingLab3DPreviewWindowV1(MatchSetupSnapshotV1 snapshot,
            TrainingServeViewV1 returnView)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(
                nameof(snapshot));
            ReturnView = returnView;
            Camera = _defaultCamera;
        }

        public MatchSetupSnapshotV1 Snapshot { get; }
        public TrainingServeViewV1 ReturnView { get; }
        public CameraState Camera { get; private set; }
        public bool IsOpen { get; private set; } = true;

        public void Orbit(float yawDeltaDegrees, float pitchDeltaDegrees)
        {
            EnsureOpen();
            Camera = new CameraState(Camera.YawDegrees + yawDeltaDegrees,
                Math.Max(-80f, Math.Min(80f,
                    Camera.PitchDegrees + pitchDeltaDegrees)),
                Camera.DistanceMeters);
        }

        public void Zoom(float deltaMeters)
        {
            EnsureOpen();
            Camera = new CameraState(Camera.YawDegrees,
                Camera.PitchDegrees,
                Math.Max(4f, Math.Min(40f,
                    Camera.DistanceMeters + deltaMeters)));
        }

        public void ResetCamera()
        {
            EnsureOpen();
            Camera = _defaultCamera;
        }

        public void SaveBookmark(string name)
        {
            EnsureOpen();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Bookmark name is required.",
                    nameof(name));
            _bookmarks[name] = Camera;
        }

        public void LoadBookmark(string name)
        {
            EnsureOpen();
            if (!_bookmarks.TryGetValue(name, out var camera))
                throw new KeyNotFoundException(
                    "Unknown TrainingLab camera bookmark: " + name);
            Camera = camera;
        }

        public TrainingServeViewV1 Close()
        {
            EnsureOpen();
            IsOpen = false;
            return ReturnView;
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
                throw new InvalidOperationException(
                    "The TrainingLab 3D preview is closed.");
        }

        public readonly struct CameraState : IEquatable<CameraState>
        {
            public CameraState(float yawDegrees, float pitchDegrees,
                float distanceMeters)
            {
                YawDegrees = yawDegrees;
                PitchDegrees = pitchDegrees;
                DistanceMeters = distanceMeters;
            }

            public float YawDegrees { get; }
            public float PitchDegrees { get; }
            public float DistanceMeters { get; }

            public bool Equals(CameraState other)
            {
                return YawDegrees.Equals(other.YawDegrees) &&
                       PitchDegrees.Equals(other.PitchDegrees) &&
                       DistanceMeters.Equals(other.DistanceMeters);
            }

            public override bool Equals(object obj)
            {
                return obj is CameraState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = YawDegrees.GetHashCode();
                    hash = hash * 397 ^ PitchDegrees.GetHashCode();
                    return hash * 397 ^ DistanceMeters.GetHashCode();
                }
            }
        }
    }
}
