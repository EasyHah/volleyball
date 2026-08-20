using System;
using UnityEngine;

namespace Volleyball.Presentation.TrainingLab
{
    public static class TrainingScenarioStartupV1
    {
        private static TrainingScenarioV1 _pendingScenario;

        internal static bool HasPendingScenario => _pendingScenario != null;

        public static void PrepareNextTrainingStart(TrainingScenarioV1 scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (FormalMatchContextStartupV4.HasPendingContext ||
                FormalMatchScenarioStartupV4.HasPendingScenario)
            {
                throw new InvalidOperationException(
                    "A formal context or opening scenario is already pending startup.");
            }

            if (_pendingScenario != null)
            {
                throw new InvalidOperationException(
                    "A training scenario is already pending startup.");
            }

            _pendingScenario = scenario;
        }

        internal static TrainingScenarioV1 ConsumePendingScenario()
        {
            var scenario = _pendingScenario;
            _pendingScenario = null;
            return scenario;
        }

        internal static void ClearPendingForTests()
        {
            _pendingScenario = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            _pendingScenario = null;
        }
    }
}
