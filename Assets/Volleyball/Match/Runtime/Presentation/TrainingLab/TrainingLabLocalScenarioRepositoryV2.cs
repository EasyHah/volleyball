using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Volleyball.Presentation.TrainingLab
{
    public interface ITrainingLabFileReplacementV2
    {
        void Replace(string temporaryPath, string destinationPath);
    }

    internal sealed class TrainingLabAtomicFileReplacementV2 :
        ITrainingLabFileReplacementV2
    {
        public void Replace(string temporaryPath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            var backup = destinationPath + ".bak";
            try
            {
                File.Replace(temporaryPath, destinationPath, backup, true);
                if (File.Exists(backup)) File.Delete(backup);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithBackup(temporaryPath, destinationPath, backup);
            }
        }

        private static void ReplaceWithBackup(
            string temporaryPath,
            string destinationPath,
            string backupPath)
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
                File.Delete(backupPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                    File.Move(backupPath, destinationPath);
                throw;
            }
        }
    }

    public sealed class TrainingLabLocalScenarioRepositoryV2
    {
        private readonly string _root;
        private readonly ITrainingLabFileReplacementV2 _replacement;

        public TrainingLabLocalScenarioRepositoryV2(
            string root = null,
            ITrainingLabFileReplacementV2 replacement = null)
        {
            _root = Path.GetFullPath(root ?? Path.Combine(
                Application.persistentDataPath, "TrainingLab", "Scenarios"));
            _replacement = replacement ?? new TrainingLabAtomicFileReplacementV2();
        }

        public string Root => _root;

        public void Save(TrainingLabLocalScenarioV2 local)
        {
            if (local == null) throw new ArgumentNullException(nameof(local));
            Directory.CreateDirectory(_root);
            local.ModifiedUtc = DateTime.UtcNow.ToString("O");
            var destination = PathFor(local.LocalId);
            var temporary = destination + ".tmp";
            var json = JsonUtility.ToJson(local.ToFile());
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                _replacement.Replace(temporary, destination);
            }
            catch
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                throw;
            }
        }

        public TrainingLabLocalScenarioV2 Load(string localId)
        {
            var path = PathFor(localId);
            var bytes = File.ReadAllBytes(path);
            var inspection = TrainingScenarioVersionGateV2.Inspect(bytes);
            if (!inspection.IsSupported)
                throw new InvalidOperationException(inspection.Diagnostic);
            TrainingLabLocalScenarioFileV2 file;
            try
            {
                file = JsonUtility.FromJson<TrainingLabLocalScenarioFileV2>(
                    Encoding.UTF8.GetString(bytes));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "TrainingLab local scenario JSON is malformed.", exception);
            }
            return TrainingLabLocalScenarioV2.FromFile(file);
        }

        public TrainingScenarioSourceInspectionV2 Inspect(string localId)
        {
            return TrainingScenarioVersionGateV2.Inspect(
                File.ReadAllBytes(PathFor(localId)));
        }

        private string PathFor(string localId)
        {
            if (string.IsNullOrWhiteSpace(localId))
                throw new ArgumentException("Local scenario ID is required.",
                    nameof(localId));
            foreach (var character in localId)
            {
                if (!(char.IsLetterOrDigit(character) || character == '-' ||
                      character == '_' || character == '.'))
                    throw new ArgumentException(
                        "Local scenario ID contains an invalid character.",
                        nameof(localId));
            }
            var path = Path.GetFullPath(Path.Combine(_root, localId + ".json"));
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Local scenario path escaped its repository root.");
            return path;
        }
    }
}
