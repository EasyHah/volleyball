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

        [TestCase(
            " {\n\t\"futureBody\" : {\"ratio\":1.25e+3,\"items\":[true,false,null]}," +
            "\r\n \"versions\" : { \"futureAxis\" : 99, \"schemaVersion\" : 3 } } ",
            3L)]
        [TestCase(
            "{\"versions\":{\"schemaVersion\":2147483648}," +
            "\"futureBody\":{\"ratio\":0.5,\"scale\":2E-4}}",
            2147483648L)]
        public void Classify_AcceptsCompleteStandardJsonForFutureSchemaEnvelopes(
            string json,
            long observedSchema)
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(json));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Unsupported));
            Assert.That(result.ObservedSchemaVersion, Is.EqualTo(observedSchema));
        }

        [Test]
        public void Classify_TreatsPositiveSchemaBeyondInt64AsUnsupportedWithoutObservation()
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(
                "{\"versions\":{\"schemaVersion\":9223372036854775808}," +
                "\"futureBody\":1.5}"));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Unsupported));
            Assert.That(result.ObservedSchemaVersion, Is.Null);
        }

        [TestCase("{\"versions\":{\"schemaVersion\":3},\"futureBody\":1.}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3},\"futureBody\":\"\\ud800\"}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3},\"futureBody\":\"\\udc00\"}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3}}{}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3,\"schemaVersion\":4}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3},\"versions\":{\"schemaVersion\":4}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3,\"\\u0073chemaVersion\":4}}")]
        [TestCase("{\"versions\":{\"schemaVersion\":3},\"\\u0076ersions\":{\"schemaVersion\":4}}")]
        public void Classify_StillValidatesTheEntireFutureJsonEnvelope(string json)
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(json));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Malformed));
        }

        [Test]
        public void Classify_StillRequiresCanonicalJsonForTheCurrentSchema()
        {
            var result = CareerSaveVersionClassifier.Classify(Utf8(
                " {\"versions\":{\"schemaVersion\":2,\"contentVersion\":1," +
                "\"rulesetVersion\":1,\"contractVersion\":2," +
                "\"careerRandomAlgorithmVersion\":1},\"futureBody\":1.5}"));

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Malformed));
        }

        [Test]
        public void Classify_RejectsInvalidUtf8InAFutureJsonEnvelope()
        {
            var result = CareerSaveVersionClassifier.Classify(
                new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x7d });

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Malformed));
        }

        [Test]
        public void Classify_RejectsAUtf8BomBeforeAFutureJsonEnvelope()
        {
            var json = Utf8("{\"versions\":{\"schemaVersion\":3}}");
            var withBom = new byte[json.Length + 3];
            withBom[0] = 0xef;
            withBom[1] = 0xbb;
            withBom[2] = 0xbf;
            Buffer.BlockCopy(json, 0, withBom, 3, json.Length);

            var result = CareerSaveVersionClassifier.Classify(withBom);

            Assert.That(result.Kind, Is.EqualTo(CareerSaveVersionClassification.Malformed));
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
