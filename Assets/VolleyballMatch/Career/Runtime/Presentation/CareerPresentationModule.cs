using System;
using UnityEngine;
using VolleyballMatch.Career.Application;
using VolleyballMatch.Career.Domain;

namespace VolleyballMatch.Career.Presentation
{
    public sealed class CareerPresentationModule : MonoBehaviour
    {
        public static Type ApplicationBoundary => typeof(CareerMatchRequest);

        public static Type DomainBoundary => typeof(CareerPlayerRecord);
    }
}
