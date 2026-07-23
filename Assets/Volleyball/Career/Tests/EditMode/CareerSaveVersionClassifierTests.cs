using System;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveVersionClassifierTests
    {
        [Test]
        public void Classify_RecognizesOnlyTheExactCurrentFiveAxisTupleAsSupported()
        {
            var supported = CareerSaveVersionClassifier.Classify(Utf8(
                "{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1," +
                "\"rulesetVersion\":1,\"contractVersion\":2," +
                "\"careerRandomAlgorithmVersion\":1}}"));
            var mismatched = CareerSaveVersionClassifier.Classify(Utf8(
                "{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1," +
                "\"rulesetVersion\":1,\"contractVersion\":1," +
                "\"careerRandomAlgorithmVersion\":1}}"));

            Assert.That(supported.Kind, Is.EqualTo(CareerSaveVersionClassification.Supported));
            Assert.That(supported.ObservedSchemaVersion, Is.EqualTo(2));
            Assert.That(mismatched.Kind, Is.EqualTo(CareerSaveVersionClassification.Unsupported));
            Assert.That(mismatched.ObservedSchemaVersion, Is.EqualTo(2));
        }

        [TestCase("{\"versions\":{\"schemaVersion\":1}}", 1)]
        [TestCase("{\"versions\":{\"schemaVersion\":3,\"futureAxis\":99}}", 3)]
        public void Classify_TreatsV1AndFutureSchemasAsUnsupportedBeforeBodyMapping(
            string json,
            int observedSchema)
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(json));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Unsupported));
            Assert.That(result.ObservedSchemaVersion, Is.EqualTo(observedSchema));
        }

        [TestCase("not-json")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"versions\":null}")]
        [TestCase("{\"versions\":{\"schemaVersion\":0}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1,\"rulesetVersion\":1,\"contractVersion\":2}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1,\"rulesetVersion\":1,\"contractVersion\":2,\"careerRandomAlgorithmVersion\":1,\"extra\":1}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1,\"rulesetVersion\":1,\"contractVersion\":2,\"careerRandomAlgorithmVersion\":0}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1,\"rulesetVersion\":1,\"contractVersion\":2,\"careerRandomAlgorithmVersion\":1.0}}")]
        public void Classify_TreatsInvalidEnvelopeOrSchema2TupleAsMalformed(string json)
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(json));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Malformed));
        }

        [TestCase("{\"versions\":{\"schemaVersion\":1}}", 1)]
        [TestCase("{\"versions\":{\"schemaVersion\":3,\"futureAxis\":99}}", 3)]
        public void Deserialize_ThrowsDedicatedUnsupportedExceptionBeforeReadingTheBody(
            string json,
            int observedSchema)
        {
            var exception = Assert.Throws<CareerSaveVersionNotSupportedException>(
                () => CareerSaveJsonCodec.Deserialize(Utf8(json)));

            Assert.That(exception.ObservedSchemaVersion, Is.EqualTo(observedSchema));
        }

        [Test]
        public void Deserialize_KeepsMalformedSchema2DistinctFromUnsupported()
        {
            var exception = Assert.Throws<FormatException>(() =>
                CareerSaveJsonCodec.Deserialize(Utf8(
                    "{\"versions\":{\"schemaVersion\":2,\"contentVersion\":1," +
                    "\"rulesetVersion\":1,\"contractVersion\":2}}")));

            Assert.That(exception, Is.Not.TypeOf<CareerSaveVersionNotSupportedException>());
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
