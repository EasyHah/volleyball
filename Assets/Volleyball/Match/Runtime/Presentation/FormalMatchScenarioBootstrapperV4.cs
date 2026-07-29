using System;
using UnityEngine;

namespace Volleyball.Presentation
{
    // Runtime API for selecting a complete formal opening without introducing
    // a second match scene or a test-only mid-rally path.
    public sealed class FormalMatchScenarioBootstrapperV4 : MonoBehaviour
    {
        public void PrepareNextFormalStart(
            FormalMatchScenarioPresetV4 scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            FormalMatchScenarioStartupV4.PrepareNextFormalStart(
                scenario.ToDefinition());
        }
    }

    public static class FormalMatchScenarioStartupV4
    {
        private static FormalMatchScenarioDefinitionV4 _pendingScenario;

        public static void PrepareNextFormalStart(
            FormalMatchScenarioDefinitionV4 scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (_pendingScenario != null)
            {
                throw new InvalidOperationException(
                    "A formal scenario is already pending scene startup.");
            }

            _pendingScenario = scenario;
        }

        internal static FormalMatchScenarioDefinitionV4 ConsumePendingScenario()
        {
            var scenario = _pendingScenario;
            _pendingScenario = null;
            return scenario;
        }
    }
}
