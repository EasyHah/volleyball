using System;
using UnityEngine;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Presentation
{
    public sealed class CareerPresentationModule : MonoBehaviour
    {
        public static Type ApplicationBoundary => typeof(CareerMatchRequest);

        public static Type DomainBoundary => typeof(CareerPlayerRecord);
    }
}
