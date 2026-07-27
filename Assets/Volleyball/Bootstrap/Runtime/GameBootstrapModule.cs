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
            typeof(IMatchContext),
            typeof(MatchContextV1),
            typeof(MatchContextV2),
            typeof(MatchContextV3),
            typeof(IMatchResult),
            typeof(MatchResultV3),
            typeof(ThreeVsThreeRallyBootstrap),
            typeof(OperationReceiptIndex),
            typeof(CareerPresentationModule)
        };
    }
}
