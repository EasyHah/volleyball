using System;

namespace VolleyballMatch.Domain.Prototype
{
    public readonly struct BallArcPoint : IEquatable<BallArcPoint>
    {
        public BallArcPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public bool Equals(BallArcPoint other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is BallArcPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                return (hashCode * 397) ^ Z.GetHashCode();
            }
        }
    }

    public readonly struct BallArc
    {
        private readonly CourtPoint _start;
        private readonly CourtPoint _end;
        private readonly float _startHeight;
        private readonly float _endHeight;
        private readonly float _apexOffset;

        public BallArc(
            CourtPoint start,
            CourtPoint end,
            float startHeight,
            float endHeight,
            float apexOffset)
        {
            if (!IsFinite(start.X) || !IsFinite(start.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Start point coordinates must be finite.");
            }

            if (!IsFinite(end.X) || !IsFinite(end.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(end), end, "End point coordinates must be finite.");
            }

            if (!IsFinite(startHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(startHeight), startHeight, "Start height must be finite.");
            }

            if (!IsFinite(endHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(endHeight), endHeight, "End height must be finite.");
            }

            if (!IsFinite(apexOffset) || apexOffset < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(apexOffset), apexOffset, "Apex offset must be finite and non-negative.");
            }

            _start = start;
            _end = end;
            _startHeight = startHeight;
            _endHeight = endHeight;
            _apexOffset = apexOffset;
        }

        public BallArcPoint Evaluate(float normalizedTime)
        {
            var time = normalizedTime < 0f ? 0f : normalizedTime > 1f ? 1f : normalizedTime;
            var x = _start.X + ((_end.X - _start.X) * time);
            var z = _start.Z + ((_end.Z - _start.Z) * time);
            var y = _startHeight
                + ((_endHeight - _startHeight) * time)
                + (_apexOffset * 4f * time * (1f - time));

            return new BallArcPoint(x, y, z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
