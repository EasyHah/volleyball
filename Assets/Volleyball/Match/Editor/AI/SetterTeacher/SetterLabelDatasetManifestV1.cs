using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterLabelDatasetManifestV1
    {
        public const int SchemaVersion = 1;

        public SetterLabelDatasetManifestV1(int sampleCount, int humanConfirmedCount)
        {
            if (sampleCount < 0 || humanConfirmedCount < 0 || humanConfirmedCount > sampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            SampleCount = sampleCount;
            HumanConfirmedCount = humanConfirmedCount;
        }

        public int SchemaVersionValue => SchemaVersion;
        public int SampleCount { get; }
        public int HumanConfirmedCount { get; }
        public int GoldenSetCount => HumanConfirmedCount;

        public static SetterLabelDatasetManifestV1 Read(string jsonlPath)
        {
            if (string.IsNullOrWhiteSpace(jsonlPath))
                throw new ArgumentException("JSONL path is required.", nameof(jsonlPath));
            if (!File.Exists(jsonlPath)) return new SetterLabelDatasetManifestV1(0, 0);
            var records = File.ReadLines(jsonlPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JObject.Parse(line))
                .ToArray();
            if (records.Any(record => record["SchemaVersionValue"]?.Value<int>() != SchemaVersion))
                throw new InvalidDataException("Setter label dataset contains an unsupported schema.");
            return new SetterLabelDatasetManifestV1(records.Length, records.Length);
        }
    }
}
