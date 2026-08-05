using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterLabelDatasetWriterV1
    {
        public const string RelativeDirectory = "TestResults/TrainingLab/SetterLabels";

        private readonly string _projectRoot;

        public SetterLabelDatasetWriterV1(string projectRoot = null)
        {
            _projectRoot = string.IsNullOrWhiteSpace(projectRoot)
                ? Directory.GetCurrentDirectory()
                : projectRoot;
        }

        public string DirectoryPath => Path.Combine(_projectRoot,
            "TestResults", "TrainingLab", "SetterLabels");

        public string Append(SetterLabelRecordV1 record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            System.IO.Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "setter-labels-v1.jsonl");
            if (File.Exists(path) && File.ReadLines(path)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => JObject.Parse(line))
                    .Any(value => string.Equals(
                        value["RequestHash"]?.Value<string>(),
                        record.RequestHash,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "A reviewed label already exists for this frozen request.");
            }
            var line = JsonConvert.SerializeObject(record, Formatting.None);
            File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
            return path;
        }
    }
}
