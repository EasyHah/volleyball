using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VolleyballMatch.Domain.Prototype
{
    public sealed class ContactPlan : IEquatable<ContactPlan>
    {
        public ContactPlan(
            PlayerId actor,
            RallyActionKind kind,
            CourtPoint actorTarget,
            CourtPoint ballTarget,
            float contactHeight,
            float ballTargetHeight,
            float durationSeconds,
            float arcHeight)
        {
            Actor = actor;
            Kind = kind;
            ActorTarget = actorTarget;
            BallTarget = ballTarget;
            ContactHeight = contactHeight;
            BallTargetHeight = ballTargetHeight;
            DurationSeconds = durationSeconds;
            ArcHeight = arcHeight;
        }

        public PlayerId Actor { get; }

        public RallyActionKind Kind { get; }

        public CourtPoint ActorTarget { get; }

        public CourtPoint BallTarget { get; }

        public float ContactHeight { get; }

        public float BallTargetHeight { get; }

        public float DurationSeconds { get; }

        public float ArcHeight { get; }

        public bool Equals(ContactPlan other)
        {
            return other != null
                && Actor.Equals(other.Actor)
                && Kind == other.Kind
                && ActorTarget.Equals(other.ActorTarget)
                && BallTarget.Equals(other.BallTarget)
                && ContactHeight.Equals(other.ContactHeight)
                && BallTargetHeight.Equals(other.BallTargetHeight)
                && DurationSeconds.Equals(other.DurationSeconds)
                && ArcHeight.Equals(other.ArcHeight);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContactPlan);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Actor.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ ActorTarget.GetHashCode();
                hashCode = (hashCode * 397) ^ BallTarget.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactHeight.GetHashCode();
                hashCode = (hashCode * 397) ^ BallTargetHeight.GetHashCode();
                hashCode = (hashCode * 397) ^ DurationSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ ArcHeight.GetHashCode();
                return hashCode;
            }
        }
    }

    public sealed class RallyPlan
    {
        public RallyPlan(IReadOnlyList<ContactPlan> contacts, TeamId winningTeam)
        {
            if (contacts == null)
            {
                throw new ArgumentNullException(nameof(contacts));
            }

            var contactsCopy = new ContactPlan[contacts.Count];
            for (var index = 0; index < contacts.Count; index++)
            {
                contactsCopy[index] = contacts[index]
                    ?? throw new ArgumentException("Contacts cannot contain null entries.", nameof(contacts));
            }

            Contacts = new ReadOnlyCollection<ContactPlan>(contactsCopy);
            WinningTeam = winningTeam;
        }

        public IReadOnlyList<ContactPlan> Contacts { get; }

        public TeamId WinningTeam { get; }
    }
}
