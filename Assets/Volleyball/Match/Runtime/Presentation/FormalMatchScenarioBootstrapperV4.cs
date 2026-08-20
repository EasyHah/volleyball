using System;
using UnityEngine;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

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

        internal static bool HasPendingScenario => _pendingScenario != null;

        public static void PrepareNextFormalStart(
            FormalMatchScenarioDefinitionV4 scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (FormalMatchContextStartupV4.HasPendingContext)
            {
                throw new InvalidOperationException(
                    "A formal MatchContextV4 is already pending scene startup.");
            }

            if (TrainingScenarioStartupV1.HasPendingScenario)
            {
                throw new InvalidOperationException(
                    "A formal training scenario is already pending scene startup.");
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

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPendingScenarioOnSubsystemRegistration()
        {
            _pendingScenario = null;
        }
    }

    /// <summary>
    /// One-shot public handoff for a caller that already owns a canonical V4
    /// context. The formal scene consumes the exact instance during Awake.
    /// </summary>
    public static class FormalMatchContextStartupV4
    {
        private static MatchContextV4 _pendingContext;

        internal static bool HasPendingContext => _pendingContext != null;

        public static void PrepareNextFormalStart(MatchContextV4 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (FormalMatchScenarioStartupV4.HasPendingScenario)
            {
                throw new InvalidOperationException(
                    "A formal scenario is already pending scene startup.");
            }
            if (TrainingScenarioStartupV1.HasPendingScenario)
            {
                throw new InvalidOperationException(
                    "A formal training scenario is already pending scene startup.");
            }
            if (_pendingContext != null)
            {
                throw new InvalidOperationException(
                    "A formal MatchContextV4 is already pending scene startup.");
            }

            _pendingContext = context;
        }

        public static bool CancelPendingFormalStart(Guid sessionId)
        {
            if (_pendingContext == null || _pendingContext.SessionId != sessionId)
            {
                return false;
            }

            _pendingContext = null;
            return true;
        }

        internal static MatchContextV4 ConsumePendingContext()
        {
            var context = _pendingContext;
            _pendingContext = null;
            return context;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPendingContextOnSubsystemRegistration()
        {
            _pendingContext = null;
        }
    }

    /// <summary>One-shot native V5 handoff; it never converts to a V4 context.</summary>
    public static class FormalMatchContextStartupV5
    {
        private static MatchContextV5 _pendingContext;

        public static void PrepareNextFormalStart(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_pendingContext != null || FormalMatchContextStartupV4.HasPendingContext)
                throw new InvalidOperationException("A formal match context is already pending scene startup.");
            _pendingContext = context;
        }

        public static bool CancelPendingFormalStart(Guid sessionId)
        {
            if (_pendingContext == null || _pendingContext.SessionId != sessionId) return false;
            _pendingContext = null;
            return true;
        }

        internal static MatchContextV5 ConsumePendingContext()
        {
            var context = _pendingContext;
            _pendingContext = null;
            return context;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPendingContextOnSubsystemRegistration() => _pendingContext = null;
    }
}
