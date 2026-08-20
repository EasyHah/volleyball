using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Volleyball.Match.Domain.PreServe;

namespace Volleyball.Presentation.TrainingLab
{
    [Serializable]
    public sealed class TrainingLabLocalScenarioFileV2
    {
        public int formatVersion;
        public string localId;
        public string displayName;
        public string createdUtc;
        public string modifiedUtc;
        public string matchSetupJson;
        public string matchSetupHash;
        public string activeStep;
        public string activeView;
        public string activeTool;
        public string selectedObjectId;
        public string bookmarksJson;
    }

    public sealed class TrainingLabLocalScenarioV2
    {
        public const int CurrentFormatVersion = 2;

        private TrainingLabLocalScenarioV2(
            string localId,
            string displayName,
            string createdUtc,
            string modifiedUtc,
            MatchSetupDraftV1 matchSetup,
            string activeStep,
            string activeView,
            string activeTool,
            string selectedObjectId,
            string bookmarksJson)
        {
            LocalId = Require(localId, nameof(localId));
            DisplayName = Require(displayName, nameof(displayName));
            CreatedUtc = Require(createdUtc, nameof(createdUtc));
            ModifiedUtc = Require(modifiedUtc, nameof(modifiedUtc));
            MatchSetup = matchSetup ?? throw new ArgumentNullException(nameof(matchSetup));
            ActiveStep = activeStep ?? string.Empty;
            ActiveView = activeView ?? string.Empty;
            ActiveTool = activeTool ?? string.Empty;
            SelectedObjectId = selectedObjectId ?? string.Empty;
            BookmarksJson = bookmarksJson ?? "[]";
        }

        public string LocalId { get; }
        public string DisplayName { get; set; }
        public string CreatedUtc { get; }
        public string ModifiedUtc { get; internal set; }
        public MatchSetupDraftV1 MatchSetup { get; internal set; }
        public string ActiveStep { get; set; }
        public string ActiveView { get; set; }
        public string ActiveTool { get; set; }
        public string SelectedObjectId { get; set; }
        public string BookmarksJson { get; set; }
        public string MatchSetupHash =>
            ComputeMatchSetupHash(MatchSetup);

        public static TrainingLabLocalScenarioV2 Create(
            string localId,
            string displayName,
            MatchSetupDraftV1 matchSetup,
            string activeStep,
            string activeView,
            string activeTool,
            string selectedObjectId,
            string bookmarksJson = "[]")
        {
            var now = DateTime.UtcNow.ToString("O");
            return new TrainingLabLocalScenarioV2(localId, displayName,
                now, now, matchSetup.DeepCopy(), activeStep, activeView,
                activeTool, selectedObjectId, bookmarksJson);
        }

        internal TrainingLabLocalScenarioFileV2 ToFile()
        {
            var setupJson = MatchSetupJsonV1.Serialize(MatchSetup);
            return new TrainingLabLocalScenarioFileV2
            {
                formatVersion = CurrentFormatVersion,
                localId = LocalId,
                displayName = DisplayName,
                createdUtc = CreatedUtc,
                modifiedUtc = ModifiedUtc,
                matchSetupJson = setupJson,
                matchSetupHash = MatchSetupHash,
                activeStep = ActiveStep,
                activeView = ActiveView,
                activeTool = ActiveTool,
                selectedObjectId = SelectedObjectId,
                bookmarksJson = BookmarksJson
            };
        }

        internal static TrainingLabLocalScenarioV2 FromFile(
            TrainingLabLocalScenarioFileV2 file)
        {
            if (file == null || file.formatVersion != CurrentFormatVersion)
                throw new InvalidOperationException(
                    "Unsupported TrainingLab local scenario version.");
            var setup = MatchSetupJsonV1.Deserialize(file.matchSetupJson);
            var actualHash = ComputeMatchSetupHash(setup);
            if (!string.Equals(actualHash, file.matchSetupHash,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "TrainingLab local scenario Match hash is invalid.");
            return new TrainingLabLocalScenarioV2(
                file.localId, file.displayName, file.createdUtc,
                file.modifiedUtc, setup, file.activeStep, file.activeView,
                file.activeTool, file.selectedObjectId, file.bookmarksJson);
        }

        private static string Require(string value, string name)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException(name + " is required.", name)
                : value;
        }

        private static string ComputeMatchSetupHash(MatchSetupDraftV1 setup)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(
                MatchSetupJsonV1.Serialize(setup)));
            var output = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) output.Append(value.ToString("x2"));
            return output.ToString();
        }
    }
}
