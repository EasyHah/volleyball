using System.Diagnostics;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.PlayModeTests
{
    public sealed class GateKCalibrationMatrixPlayModeTests
    {
        private const string FixedKey = "gate-k-calibration-seed-73421";

        [Test]
        public void AttackControl_ChangesAttackErrorButNotCapacity()
        {
            Measure("AttackControl", () =>
            {
                var low = Envelope(Derived(attackTechnique: .1f),
                    ExecutionCandidateCategoryV4.Attack);
                var high = Envelope(Derived(attackTechnique: .9f),
                    ExecutionCandidateCategoryV4.Attack);

                Assert.That(high.TargetError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.TargetError.MaximumAbsoluteError.Magnitude));
                Assert.That(high.VelocityError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.VelocityError.MaximumAbsoluteError.Magnitude));
                Assert.That(high.MaximumVelocity, Is.EqualTo(low.MaximumVelocity));
                Assert.That(high.MaximumEffort, Is.EqualTo(low.MaximumEffort));
                ReportEnvelopePair(
                    "AttackControl", low, high,
                    Derived(attackTechnique: .1f),
                    Derived(attackTechnique: .9f));
            });
        }

        [Test]
        public void SoftTouch_ChangesSoftEnvelopeButNotPowerAttackAim()
        {
            Measure("SoftTouch", () =>
            {
                var lowDerived = Derived(softTouch: .1f);
                var highDerived = Derived(softTouch: .9f);
                var low = Envelope(lowDerived,
                    ExecutionCandidateCategoryV4.SoftAction);
                var high = Envelope(highDerived,
                    ExecutionCandidateCategoryV4.SoftAction);

                Assert.That(high.TargetError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.TargetError.MaximumAbsoluteError.Magnitude));
                Assert.That(high.VelocityError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.VelocityError.MaximumAbsoluteError.Magnitude));
                Assert.That(
                    Envelope(highDerived, ExecutionCandidateCategoryV4.Attack)
                        .TargetError,
                    Is.EqualTo(
                        Envelope(lowDerived, ExecutionCandidateCategoryV4.Attack)
                            .TargetError));
                ReportEnvelopePair(
                    "SoftTouch", low, high, lowDerived, highDerived);
            });
        }

        [Test]
        public void BlockTechnique_ChangesBlockControlButNotReachOrMobility()
        {
            Measure("BlockTechnique", () =>
            {
                var lowDerived = Derived(blockTechnique: .1f);
                var highDerived = Derived(blockTechnique: .9f);
                var low = Envelope(lowDerived,
                    ExecutionCandidateCategoryV4.Block);
                var high = Envelope(highDerived,
                    ExecutionCandidateCategoryV4.Block);

                Assert.That(high.TargetError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.TargetError.MaximumAbsoluteError.Magnitude));
                Assert.That(high.VelocityError.MaximumAbsoluteError.Magnitude,
                    Is.LessThan(low.VelocityError.MaximumAbsoluteError.Magnitude));
                Assert.That(high.MaximumVelocity, Is.EqualTo(low.MaximumVelocity));
                Assert.That(highDerived.Attributes.Block.ReachHeightMeters,
                    Is.EqualTo(lowDerived.Attributes.Block.ReachHeightMeters));
                ReportEnvelopePair(
                    "BlockTechnique", low, high, lowDerived, highDerived);
            });
        }

        [Test]
        public void CourtAwareness_ChangesPerceptionButNotPublicAuthority()
        {
            Measure("CourtAwareness", () =>
            {
                var adapter = new CourtPerceptionAdapterV3(
                    new CourtPerceptionConfigurationV3(
                        "gate-j-v1", .05f, .30f, .08f, 1.20f, .03f, .35f));
                var low = adapter.Observe(PerceptionRequest(0f));
                var high = adapter.Observe(PerceptionRequest(1f));

                Assert.That(high.RecognitionDelaySeconds,
                    Is.LessThan(low.RecognitionDelaySeconds));
                Assert.That(high.ObservedBall.Uncertainty,
                    Is.LessThan(low.ObservedBall.Uncertainty));
                Assert.That(high.ObservedBall.Confidence,
                    Is.GreaterThan(low.ObservedBall.Confidence));
                Assert.That(high.ArrivalUncertaintySeconds,
                    Is.LessThan(low.ArrivalUncertaintySeconds));
                Assert.That(high.View.AuthoritativeArtifactIdentity,
                    Is.EqualTo(low.View.AuthoritativeArtifactIdentity));
                Assert.That(high.View.Revision, Is.EqualTo(low.View.Revision));
                Assert.That(high.View.SourceSequence,
                    Is.EqualTo(low.View.SourceSequence));
                TestContext.WriteLine(
                    "[GateKCalibrationEvidence] axis=CourtAwareness " +
                    "configuration=gate-j-v1 publicArtifact=" +
                    low.View.AuthoritativeArtifactIdentity +
                    " viewIdentity=" + low.View.ViewIdentity);
            });
        }

        private static ExecutionEnvelopeV4 Envelope(
            DerivedMatchAttributesV4 derived,
            ExecutionCandidateCategoryV4 category)
        {
            return ExecutionEnvelopeFactoryV4.Create(
                derived,
                new ExecutionIntentV4(
                    "gate-k-calibration-" + category,
                    category,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(1f, 1f, 1f),
                    .1f),
                FixedKey + ":" + category,
                ExecutionEnvelopePolicyV4.GateI);
        }

        private static DerivedMatchAttributesV4 Derived(
            float attackTechnique = .72f,
            float softTouch = .72f,
            float blockTechnique = .72f)
        {
            return MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(
                    1.91f, 2.43f, .73f, .71f, .72f, .70f),
                new TechnicalBaseAttributesV4(
                    attackTechnique, .77f, blockTechnique, .72f, .74f,
                    .75f, .76f, softTouch, .78f),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static CourtPerceptionRequestV3 PerceptionRequest(
            float awareness)
        {
            return new CourtPerceptionRequestV3(
                FixedKey,
                4,
                7,
                TeamSide.Home,
                new PlayerId("home-observer"),
                awareness,
                "public-threat-4",
                new SimVector3(0f, 2f, 1f),
                new[]
                {
                    new PerceivedThreatEntryV3(
                        "threat-line", "line", .8f, 1f)
                },
                new[]
                {
                    new PerceivedSupportCandidateV3(
                        new PlayerId("home-fast"), .9f, .8f, false),
                    new PerceivedSupportCandidateV3(
                        new PlayerId("home-committed"), .4f, .2f, true)
                },
                new PlayerId("home-committed"));
        }

        private static void Measure(string axis, TestDelegate assertion)
        {
            var stopwatch = Stopwatch.StartNew();
            assertion();
            stopwatch.Stop();
            TestContext.WriteLine(
                $"[GateKCalibration] axis={axis} seed={FixedKey} " +
                $"wallClockMs={stopwatch.Elapsed.TotalMilliseconds:0.###}");
        }

        private static void ReportEnvelopePair(
            string axis,
            ExecutionEnvelopeV4 lowEnvelope,
            ExecutionEnvelopeV4 highEnvelope,
            DerivedMatchAttributesV4 lowDerived,
            DerivedMatchAttributesV4 highDerived)
        {
            TestContext.WriteLine(
                $"[GateKCalibrationEvidence] axis={axis} " +
                $"lowProfile={lowDerived.InputFingerprint} " +
                $"highProfile={highDerived.InputFingerprint} " +
                $"lowResult={lowDerived.ResultFingerprint} " +
                $"highResult={highDerived.ResultFingerprint} " +
                $"lowEnvelope={lowEnvelope.Identity} " +
                $"highEnvelope={highEnvelope.Identity}");
        }
    }
}
