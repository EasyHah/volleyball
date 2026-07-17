using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct SimVector3 : IEquatable<SimVector3>
    {
        public SimVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SimVector3 Zero => new SimVector3(0f, 0f, 0f);

        public static SimVector3 Up => new SimVector3(0f, 1f, 0f);

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public float SqrMagnitude => Dot(this, this);

        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y) && IsFiniteValue(Z);

        public SimVector3 Normalized
        {
            get
            {
                var magnitude = Magnitude;
                return magnitude <= float.Epsilon ? Zero : this / magnitude;
            }
        }

        public static SimVector3 operator +(SimVector3 left, SimVector3 right)
        {
            return new SimVector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static SimVector3 operator -(SimVector3 left, SimVector3 right)
        {
            return new SimVector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static SimVector3 operator -(SimVector3 value)
        {
            return new SimVector3(-value.X, -value.Y, -value.Z);
        }

        public static SimVector3 operator *(SimVector3 value, float scale)
        {
            return new SimVector3(value.X * scale, value.Y * scale, value.Z * scale);
        }

        public static SimVector3 operator *(float scale, SimVector3 value)
        {
            return value * scale;
        }

        public static SimVector3 operator /(SimVector3 value, float divisor)
        {
            return new SimVector3(value.X / divisor, value.Y / divisor, value.Z / divisor);
        }

        public static float Dot(SimVector3 left, SimVector3 right)
        {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        }

        public static SimVector3 Cross(SimVector3 left, SimVector3 right)
        {
            return new SimVector3(
                (left.Y * right.Z) - (left.Z * right.Y),
                (left.Z * right.X) - (left.X * right.Z),
                (left.X * right.Y) - (left.Y * right.X));
        }

        public static SimVector3 Lerp(SimVector3 start, SimVector3 end, float alpha)
        {
            return start + ((end - start) * alpha);
        }

        public static SimVector3 Reflect(SimVector3 incoming, SimVector3 normal)
        {
            return incoming - (2f * Dot(incoming, normal) * normal);
        }

        public bool Equals(SimVector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is SimVector3 other && Equals(other);
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

        private static bool IsFiniteValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
