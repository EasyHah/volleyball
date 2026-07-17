using System;
using UnityEngine;
using VolleyballMatch.Career.Application;
using VolleyballMatch.Career.Presentation;
using VolleyballMatch.Presentation;
using VolleyballMatch.Shared.Contracts;

namespace VolleyballMatch.Bootstrap
{
    public sealed class GameBootstrapModule : MonoBehaviour
    {
        public static Type[] RuntimeBoundaries => new[]
        {
            typeof(MatchContextV1),
            typeof(ThreeVsThreeRallyBootstrap),
            typeof(CareerMatchRequest),
            typeof(CareerPresentationModule)
        };
    }
}
