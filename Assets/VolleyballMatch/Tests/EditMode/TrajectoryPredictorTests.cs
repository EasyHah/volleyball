using NUnit.Framework;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class TrajectoryPredictorTests
    {
        [Test]
        public void Predict_UsesTheRuntimeIntegratorWithoutMutatingSource()
        {
            var source = new BallState(
                new SimVector3(1f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var runtime = source.Clone();
            for (var index = 0; index < 10; index++)
            {
                BallIntegrator.Step(runtime, 1f / 120f, parameters);
            }

            var prediction = TrajectoryPredictor.Predict(
                source,
                parameters,
                1f / 120f,
                10f / 120f,
                11);

            var finalSample = prediction.Samples[prediction.Samples.Count - 1];
            Assert.That(finalSample.Position.X, Is.EqualTo(runtime.Position.X).Within(0.000001f));
            Assert.That(finalSample.Position.Y, Is.EqualTo(runtime.Position.Y).Within(0.000001f));
            Assert.That(finalSample.Position.Z, Is.EqualTo(runtime.Position.Z).Within(0.000001f));
            Assert.That(finalSample.Velocity, Is.EqualTo(runtime.Velocity));
            Assert.That(source.Position, Is.EqualTo(new SimVector3(1f, 3f, -2f)));
        }

        [Test]
        public void Predict_InterpolatesFirstGroundLanding()
        {
            var source = new BallState(
                new SimVector3(0f, 0.2f, 0f),
                new SimVector3(1f, -2f, 3f),
                0.12f);

            var prediction = TrajectoryPredictor.Predict(
                source,
                new BallSimulationParameters(-9.8f, 1f),
                1f / 120f,
                1f,
                121);

            Assert.That(prediction.GroundLanding.HasValue, Is.True);
            Assert.That(prediction.GroundLanding.Value.TimeSeconds, Is.GreaterThan(0f));
            Assert.That(prediction.GroundLanding.Value.Position.X, Is.GreaterThan(0f));
            Assert.That(prediction.GroundLanding.Value.Position.Z, Is.GreaterThan(0f));
        }
    }
}
