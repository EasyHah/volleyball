using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingCameraBookmarkV1
    {
        public TrainingCameraBookmarkV1(string name, SimVector3 position,
            SimVector3 forward, float orthographicSize, bool orthographic)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 ||
                !position.IsFinite || !forward.IsFinite || forward.SqrMagnitude < .01f ||
                float.IsNaN(orthographicSize) || float.IsInfinity(orthographicSize) ||
                orthographicSize <= 0f)
                throw new ArgumentException("Camera bookmark values are invalid.");
            Name = name;
            Position = position;
            Forward = forward;
            OrthographicSize = orthographicSize;
            Orthographic = orthographic;
        }

        public string Name { get; }
        public SimVector3 Position { get; }
        public SimVector3 Forward { get; }
        public float OrthographicSize { get; }
        public bool Orthographic { get; }
    }
}
