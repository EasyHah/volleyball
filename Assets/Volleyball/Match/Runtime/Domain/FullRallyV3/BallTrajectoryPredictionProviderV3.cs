using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryPredictionProviderV3
    {
        public const string PredictorVersion = "trajectory-predictor-v3-1";

        private readonly Dictionary<string, BallTrajectoryArtifactV3> _cache;
        private readonly float _stepSeconds;
        private readonly float _maximumTimeSeconds;
        private readonly int _maximumSamples;

        public BallTrajectoryPredictionProviderV3(
            float stepSeconds = 1f / 120f,
            float maximumTimeSeconds = 2f,
            int maximumSamples = 241)
        {
            if (!IsFinite(stepSeconds) || stepSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if (!IsFinite(maximumTimeSeconds) || maximumTimeSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(maximumTimeSeconds));
            if (maximumSamples <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSamples));

            _cache = new Dictionary<string, BallTrajectoryArtifactV3>();
            _stepSeconds = stepSeconds;
            _maximumTimeSeconds = maximumTimeSeconds;
            _maximumSamples = maximumSamples;
        }

        public int CacheCount => _cache.Count;

        public BallTrajectoryArtifactV3 Predict(
            BallState source,
            BallSimulationParameters parameters,
            string sampleKey,
            string degradationMode = "normal")
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var key = BuildKey(source, parameters, sampleKey);

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var prediction = TrajectoryPredictor.Predict(
                source,
                parameters,
                _stepSeconds,
                _maximumTimeSeconds,
                _maximumSamples);

            var artifact = BallTrajectoryArtifactV3.CreateWithPrediction(
                BuildBallStateVersion(source),
                BuildPhysicsHash(parameters),
                sampleKey,
                PredictorVersion,
                degradationMode,
                prediction);

            _cache[key] = artifact;
            return artifact;
        }

        public bool TryGetCached(string cacheKey, out BallTrajectoryArtifactV3 artifact)
        {
            return _cache.TryGetValue(cacheKey, out artifact);
        }

        public string BuildCacheKey(BallState source, BallSimulationParameters parameters, string sampleKey)
        {
            return BuildKey(source, parameters, sampleKey);
        }

        public static string BuildBallStateVersion(BallState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var sb = new StringBuilder(128);
            sb.Append(source.Position.X.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(':').Append(source.Position.Y.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(':').Append(source.Position.Z.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('|').Append(source.Velocity.X.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(':').Append(source.Velocity.Y.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(':').Append(source.Velocity.Z.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('|').Append(source.Radius.ToString("R", CultureInfo.InvariantCulture));
            return HashString(sb.ToString());
        }

        public static string BuildPhysicsHash(BallSimulationParameters parameters)
        {
            var sb = new StringBuilder(64);
            sb.Append(parameters.Gravity.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('|').Append(parameters.LinearDampingPer60Hz.ToString("R", CultureInfo.InvariantCulture));
            return HashString(sb.ToString());
        }

        private static string BuildKey(BallState source, BallSimulationParameters parameters, string sampleKey)
        {
            var sb = new StringBuilder(256);
            sb.Append(BuildBallStateVersion(source));
            sb.Append('|').Append(BuildPhysicsHash(parameters));
            sb.Append('|').Append(sampleKey ?? string.Empty);
            return sb.ToString();
        }

        private static string HashString(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(64);
            for (var i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
