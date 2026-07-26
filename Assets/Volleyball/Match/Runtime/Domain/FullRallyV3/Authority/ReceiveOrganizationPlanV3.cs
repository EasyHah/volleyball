using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum OrganizationFallbackReasonV3
    {
        None,
        SetterPreviousTouch,
        SetterUnavailable,
        SetterIllegal,
        SetterUnreachable,
        NoLegalOrganizer
    }

    public sealed class ReceiveOrganizationPlanV3
    {
        public ReceiveOrganizationPlanV3(
            TeamSide side,
            long revision,
            PlayerId primaryReceiver,
            PlayerId registeredSetter,
            IReadOnlyList<PlayerId> emergencyReceivers,
            IReadOnlyList<PlayerId> backupOrganizers,
            PlayerId attackPreparation,
            SimVector3 organizationTarget)
        {
            Side = PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Revision = revision;
            PrimaryReceiver = PlayerWorldSnapshotV3.RequirePlayerId(
                primaryReceiver,
                nameof(primaryReceiver));
            RegisteredSetter = PlayerWorldSnapshotV3.RequirePlayerId(
                registeredSetter,
                nameof(registeredSetter));
            AttackPreparation = PlayerWorldSnapshotV3.RequirePlayerId(
                attackPreparation,
                nameof(attackPreparation));
            if (!organizationTarget.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(organizationTarget));
            }

            OrganizationTarget = organizationTarget;
            EmergencyReceivers = CopyDistinct(
                emergencyReceivers,
                0,
                2,
                nameof(emergencyReceivers));
            BackupOrganizers = CopyDistinct(
                backupOrganizers,
                0,
                5,
                nameof(backupOrganizers));
            ValidateNoRoleCollision();
        }

        public TeamSide Side { get; }

        public long Revision { get; }

        public PlayerId PrimaryReceiver { get; }

        public PlayerId RegisteredSetter { get; }

        public IReadOnlyList<PlayerId> EmergencyReceivers { get; }

        public IReadOnlyList<PlayerId> BackupOrganizers { get; }

        public PlayerId AttackPreparation { get; }

        public SimVector3 OrganizationTarget { get; }

        private static IReadOnlyList<PlayerId> CopyDistinct(
            IReadOnlyList<PlayerId> source,
            int minimum,
            int maximum,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (source.Count < minimum || source.Count > maximum)
            {
                throw new ArgumentException(
                    $"Expected {minimum} to {maximum} players.",
                    parameterName);
            }

            var copy = new PlayerId[source.Count];
            var seen = new HashSet<PlayerId>();
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = PlayerWorldSnapshotV3.RequirePlayerId(
                    source[index],
                    parameterName);
                if (!seen.Add(copy[index]))
                {
                    throw new ArgumentException(
                        "Responsibility players must be distinct.",
                        parameterName);
                }
            }

            return new ReadOnlyCollection<PlayerId>(copy);
        }

        private void ValidateNoRoleCollision()
        {
            if (EmergencyReceivers.Contains(PrimaryReceiver))
            {
                throw new ArgumentException(
                    "Primary receiver cannot also be an emergency receiver.");
            }

            if (BackupOrganizers.Contains(RegisteredSetter))
            {
                throw new ArgumentException(
                    "Registered setter cannot also be a backup organizer.");
            }
        }
    }
}
