using System;
using UnityEngine;
using Volleyball.Domain;
using Volleyball.Domain.Prototype;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    public sealed class PhysicalMatchConfiguration
    {
        private PhysicalMatchConfiguration(
            string displayName,
            string logTag,
            int rosterSize,
            float courtHalfLength,
            MatchSetRules setRules)
        {
            DisplayName = displayName;
            LogTag = logTag;
            RosterSize = rosterSize;
            CourtHalfLength = courtHalfLength;
            SetRules = setRules;
        }

        public string DisplayName { get; }

        public string LogTag { get; }

        public int RosterSize { get; }

        public float CourtHalfLength { get; }

        public MatchSetRules SetRules { get; }

        public static PhysicalMatchConfiguration ThreeVsThree { get; } =
            new PhysicalMatchConfiguration(
                "DYNAMIC PHYSICAL 3v3",
                "Physical3v3",
                3,
                CourtBuilder.HalfLength,
                MatchSetRules.ThreeVsThree);

        public static PhysicalMatchConfiguration FormalIndoorSixVsSix { get; } =
            new PhysicalMatchConfiguration(
                "FORMAL INDOOR 6v6",
                "Formal6v6",
                6,
                CourtBuilder.FormalHalfLength,
                MatchSetRules.FormalIndoor);

        public static PhysicalMatchConfiguration CreateCalibration(
            PhysicalMatchConfiguration source,
            int targetScore,
            int minimumLead)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new PhysicalMatchConfiguration(
                source.DisplayName + " CALIBRATION",
                source.LogTag + "Calibration",
                source.RosterSize,
                source.CourtHalfLength,
                new MatchSetRules(
                    targetScore,
                    minimumLead,
                    MatchSetRules.AbsoluteMaximumScore));
        }

        public Vector3 PositionFor(TeamSide side, int rotationPosition)
        {
            if (rotationPosition < 1 || rotationPosition > RosterSize)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationPosition));
            }

            if (RosterSize == 6)
            {
                var zSign = side == TeamSide.Home ? -1f : 1f;
                var sixPlayerPositions = new[]
                {
                    new Vector3(3.05f, 0f, 6.25f * zSign),
                    new Vector3(3.0f, 0f, 2.35f * zSign),
                    new Vector3(0f, 0f, 2.05f * zSign),
                    new Vector3(-3.0f, 0f, 2.35f * zSign),
                    new Vector3(-3.05f, 0f, 6.25f * zSign),
                    new Vector3(0f, 0f, 5.55f * zSign)
                };
                return sixPlayerPositions[rotationPosition - 1];
            }

            var homePositions = new[]
            {
                new Vector3(-2.5f, 0f, -5.2f),
                new Vector3(0f, 0f, -3.4f),
                new Vector3(2.1f, 0f, -2.6f)
            };
            var position = homePositions[rotationPosition - 1];
            return side == TeamSide.Home
                ? position
                : new Vector3(-position.x, position.y, -position.z);
        }

        public static bool IsAttackSpecialist(PlayerRole role)
        {
            return role == PlayerRole.Attacker ||
                   role == PlayerRole.OutsideHitter ||
                   role == PlayerRole.Opposite ||
                   role == PlayerRole.MiddleBlocker;
        }
    }
}
