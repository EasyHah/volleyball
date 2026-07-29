using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Domain.Players;

namespace Volleyball.Domain.Prototype
{
    public enum RallyContactDisposition
    {
        Ignore,
        Accept,
        Fault
    }

    public enum RallyContactRejectionReason
    {
        None,
        WindowClosed,
        WrongTeam,
        WrongAction,
        WrongActor,
        WrongPossessionTeam,
        ConsecutiveCountedTouch,
        FourthCountedTouch
    }

    public readonly struct RallyContactEvaluation : IEquatable<RallyContactEvaluation>
    {
        public RallyContactEvaluation(RallyContactDisposition disposition, RallyContactRejectionReason reason)
        {
            Disposition = disposition;
            Reason = reason;
        }

        public RallyContactDisposition Disposition { get; }

        public RallyContactRejectionReason Reason { get; }

        public bool Equals(RallyContactEvaluation other)
        {
            return Disposition == other.Disposition && Reason == other.Reason;
        }

        public override bool Equals(object obj)
        {
            return obj is RallyContactEvaluation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Disposition * 397) ^ (int)Reason;
            }
        }

        public static bool operator ==(RallyContactEvaluation left, RallyContactEvaluation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RallyContactEvaluation left, RallyContactEvaluation right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class RallyContactWindow
    {
        public RallyContactWindow(
            TeamId team,
            TechniqueAction action,
            float startSimulationTime,
            float endSimulationTime,
            IEnumerable<PlayerId> eligibleActors)
        {
            ValidateTeam(team, nameof(team));
            ValidateAction(action, nameof(action));
            ValidateFinite(startSimulationTime, nameof(startSimulationTime));
            ValidateFinite(endSimulationTime, nameof(endSimulationTime));
            if (endSimulationTime < startSimulationTime)
            {
                throw new ArgumentOutOfRangeException(nameof(endSimulationTime), endSimulationTime, "End time cannot precede start time.");
            }

            if (eligibleActors == null)
            {
                throw new ArgumentNullException(nameof(eligibleActors));
            }

            var actors = new List<PlayerId>();
            foreach (var actor in eligibleActors)
            {
                ValidateActor(actor, nameof(eligibleActors));
                if (actor.Team != team)
                {
                    throw new ArgumentException("Eligible actors must belong to the window team.", nameof(eligibleActors));
                }

                actors.Add(actor);
            }

            if (actors.Count == 0)
            {
                throw new ArgumentException("At least one actor must be eligible.", nameof(eligibleActors));
            }

            Team = team;
            Action = action;
            StartSimulationTime = startSimulationTime;
            EndSimulationTime = endSimulationTime;
            EligibleActors = new ReadOnlyCollection<PlayerId>(actors);
        }

        public TeamId Team { get; }

        public TechniqueAction Action { get; }

        public float StartSimulationTime { get; }

        public float EndSimulationTime { get; }

        public IReadOnlyList<PlayerId> EligibleActors { get; }

        public bool Contains(PlayerId actor, float simulationTime)
        {
            ValidateActor(actor, nameof(actor));
            ValidateFinite(simulationTime, nameof(simulationTime));
            if (simulationTime < StartSimulationTime || simulationTime > EndSimulationTime)
            {
                return false;
            }

            for (var index = 0; index < EligibleActors.Count; index++)
            {
                if (EligibleActors[index].Equals(actor))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ValidateTeam(TeamId team, string parameterName)
        {
            if (team != TeamId.Blue && team != TeamId.Orange)
            {
                throw new ArgumentOutOfRangeException(parameterName, team, "Unknown team.");
            }
        }

        internal static void ValidateAction(TechniqueAction action, string parameterName)
        {
            if (action != TechniqueAction.Receive
                && action != TechniqueAction.Set
                && action != TechniqueAction.Attack
                && action != TechniqueAction.Block
                && action != TechniqueAction.Serve)
            {
                throw new ArgumentOutOfRangeException(parameterName, action, "Unknown technique action.");
            }
        }

        internal static void ValidateActor(PlayerId actor, string parameterName)
        {
            ValidateTeam(actor.Team, parameterName);
            if (!Enum.IsDefined(typeof(PlayerRole), actor.Role))
            {
                throw new ArgumentOutOfRangeException(parameterName, actor.Role, "Unknown player role.");
            }
        }

        internal static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Simulation time must be finite.");
            }
        }
    }

    public sealed class RallyTouchState
    {
        public RallyTouchState(TeamId initialPossessionTeam)
        {
            RallyContactWindow.ValidateTeam(initialPossessionTeam, nameof(initialPossessionTeam));
            PossessionTeam = initialPossessionTeam;
        }

        public TeamId PossessionTeam { get; private set; }

        public int CountedTeamTouches { get; private set; }

        public PlayerId? LastCountedActor { get; private set; }

        public PlayerId? LastPhysicalTouch { get; private set; }

        public RallyContactWindow ContactWindow { get; private set; }

        public void BeginPossession(TeamId team)
        {
            RallyContactWindow.ValidateTeam(team, nameof(team));
            PossessionTeam = team;
            CountedTeamTouches = 0;
            LastCountedActor = null;
            ContactWindow = null;
        }

        public void OpenWindow(RallyContactWindow window)
        {
            ContactWindow = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void CloseWindow()
        {
            ContactWindow = null;
        }

        public RallyContactEvaluation Evaluate(PlayerId actor, TechniqueAction action, float simulationTime)
        {
            RallyContactWindow.ValidateActor(actor, nameof(actor));
            RallyContactWindow.ValidateAction(action, nameof(action));
            RallyContactWindow.ValidateFinite(simulationTime, nameof(simulationTime));

            var window = ContactWindow;
            if (window == null || simulationTime < window.StartSimulationTime || simulationTime > window.EndSimulationTime)
            {
                return Ignore(RallyContactRejectionReason.WindowClosed);
            }

            if (actor.Team != window.Team)
            {
                return Ignore(RallyContactRejectionReason.WrongTeam);
            }

            if (action != window.Action)
            {
                return Ignore(RallyContactRejectionReason.WrongAction);
            }

            if (!window.Contains(actor, simulationTime))
            {
                return Ignore(RallyContactRejectionReason.WrongActor);
            }

            if (IsCountedAction(action))
            {
                if (actor.Team != PossessionTeam)
                {
                    return Fault(RallyContactRejectionReason.WrongPossessionTeam);
                }

                if (LastCountedActor.HasValue && LastCountedActor.Value.Equals(actor))
                {
                    return Fault(RallyContactRejectionReason.ConsecutiveCountedTouch);
                }

                if (CountedTeamTouches >= 3)
                {
                    return Fault(RallyContactRejectionReason.FourthCountedTouch);
                }
            }

            return new RallyContactEvaluation(RallyContactDisposition.Accept, RallyContactRejectionReason.None);
        }

        public RallyContactEvaluation Accept(PlayerId actor, TechniqueAction action, float simulationTime)
        {
            var evaluation = Evaluate(actor, action, simulationTime);
            if (evaluation.Disposition != RallyContactDisposition.Accept)
            {
                return evaluation;
            }

            LastPhysicalTouch = actor;
            if (IsCountedAction(action))
            {
                CountedTeamTouches++;
                LastCountedActor = actor;
            }

            ContactWindow = null;
            return evaluation;
        }

        public void SynchronizeAuthoritativeContact(
            PlayerId actor,
            TechniqueAction action,
            int authoritativeCountedTouches)
        {
            RallyContactWindow.ValidateActor(actor, nameof(actor));
            RallyContactWindow.ValidateAction(action, nameof(action));
            if (authoritativeCountedTouches < 0 || authoritativeCountedTouches > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeCountedTouches));
            }

            if (IsCountedAction(action))
            {
                if (authoritativeCountedTouches == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(authoritativeCountedTouches));
                }

                PossessionTeam = actor.Team;
                CountedTeamTouches = authoritativeCountedTouches;
                LastCountedActor = actor;
            }
            LastPhysicalTouch = actor;
            ContactWindow = null;
        }

        private static bool IsCountedAction(TechniqueAction action)
        {
            return action == TechniqueAction.Receive
                || action == TechniqueAction.Set
                || action == TechniqueAction.Attack;
        }

        private static RallyContactEvaluation Ignore(RallyContactRejectionReason reason)
        {
            return new RallyContactEvaluation(RallyContactDisposition.Ignore, reason);
        }

        private static RallyContactEvaluation Fault(RallyContactRejectionReason reason)
        {
            return new RallyContactEvaluation(RallyContactDisposition.Fault, reason);
        }
    }
}
