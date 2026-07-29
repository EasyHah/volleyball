using System;
using UnityEngine;
using Volleyball.Career.Application;
using Volleyball.Career.Presentation;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    public sealed class GameBootstrapModule : MonoBehaviour
    {
        public static Type[] RuntimeBoundaries => new[]
        {
            typeof(MatchContextV4),
            typeof(MatchResultV4),
            typeof(MatchReplayV4),
            typeof(ThreeVsThreeRallyBootstrap),
            typeof(OperationReceiptIndex),
            typeof(CareerPresentationModule)
        };
    }
}
