using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingScenarioDraftEntryV1
    {
        internal TrainingScenarioDraftEntryV1(
            string key,
            string displayName,
            bool isBuiltIn)
        {
            Key = key;
            DisplayName = displayName;
            IsBuiltIn = isBuiltIn;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public bool IsBuiltIn { get; }
    }

    public sealed class TrainingScenarioDraftStoreV1
    {
        private readonly Dictionary<string, TrainingScenarioDraftV1> _builtIns;
        private readonly Dictionary<string, TrainingScenarioDraftV1> _session =
            new Dictionary<string, TrainingScenarioDraftV1>(
                StringComparer.Ordinal);
        private int _sessionSequence;

        public TrainingScenarioDraftStoreV1(
            IReadOnlyDictionary<string, TrainingScenarioDraftV1> builtIns)
        {
            if (builtIns == null || builtIns.Count == 0)
                throw new ArgumentException(
                    "The training lab requires at least one built-in scenario.",
                    nameof(builtIns));
            _builtIns = builtIns.ToDictionary(
                pair => RequireKey(pair.Key),
                pair => (pair.Value ??
                         throw new ArgumentException(
                             "Built-in drafts cannot be null.",
                             nameof(builtIns)))
                    .DeepCopy(),
                StringComparer.Ordinal);
        }

        public IReadOnlyList<TrainingScenarioDraftEntryV1> Entries =>
            new ReadOnlyCollection<TrainingScenarioDraftEntryV1>(
                _builtIns
                    .OrderBy(pair => pair.Value.DisplayName,
                        StringComparer.Ordinal)
                    .Select(pair => new TrainingScenarioDraftEntryV1(
                        pair.Key,
                        pair.Value.DisplayName,
                        true))
                    .Concat(_session
                        .OrderBy(pair => pair.Value.DisplayName,
                            StringComparer.Ordinal)
                        .Select(pair => new TrainingScenarioDraftEntryV1(
                            pair.Key,
                            pair.Value.DisplayName,
                            false)))
                    .ToArray());

        public string FirstBuiltInKey => _builtIns.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .First();

        public TrainingScenarioDraftV1 Load(string key)
        {
            key = RequireKey(key);
            if (_builtIns.TryGetValue(key, out var builtIn))
                return builtIn.DeepCopy();
            if (_session.TryGetValue(key, out var session))
                return session.DeepCopy();
            throw new KeyNotFoundException(
                "Unknown training draft entry: " + key);
        }

        public string AddSession(TrainingScenarioDraftV1 draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var key = "session:" + (++_sessionSequence);
            _session.Add(key, draft.DeepCopy());
            return key;
        }

        public void UpdateSession(
            string key,
            TrainingScenarioDraftV1 draft)
        {
            if (!_session.ContainsKey(RequireKey(key)))
                throw new InvalidOperationException(
                    "Only session drafts can be updated.");
            _session[key] = (draft ??
                             throw new ArgumentNullException(nameof(draft)))
                .DeepCopy();
        }

        public bool IsSession(string key)
        {
            return key != null && _session.ContainsKey(key);
        }

        public static TrainingScenarioDraftStoreV1 LoadProjectCatalog()
        {
            var drafts =
                new Dictionary<string, TrainingScenarioDraftV1>(
                    StringComparer.Ordinal);
            foreach (var id in TrainingScenarioCatalogV1.ScenarioIds)
            {
                var preset = Resources.Load<TrainingScenarioPresetV1>(
                    "TrainingScenariosV1/" + id);
                if (preset == null)
                    throw new InvalidOperationException(
                        "Missing project training scenario: " + id);
                drafts.Add("builtin:" + id, preset.CreateDraft());
            }

            return new TrainingScenarioDraftStoreV1(drafts);
        }

        private static string RequireKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Draft key is required.")
                : value;
        }
    }
}
