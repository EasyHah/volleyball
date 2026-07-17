using System;
using Volleyball.Domain.Prototype;

namespace Volleyball.AI
{
    public enum TouchDecisionAction
    {
        Receive,
        Set,
        Attack,
        FreeBall,
        EmergencySave
    }

    public enum TargetZone
    {
        LeftFront,
        MiddleFront,
        RightFront,
        LeftBack,
        MiddleBack,
        RightBack
    }

    public enum DecisionTempo
    {
        Quick,
        Normal,
        High
    }

    public enum DecisionRisk
    {
        Safe,
        Balanced,
        Aggressive
    }

    public readonly struct RoundDecisionV1 : IEquatable<RoundDecisionV1>
    {
        public RoundDecisionV1(
            PlayerRole receiver,
            PlayerRole secondActor,
            SetRoute setRoute,
            PlayerRole thirdActor,
            SpikeRoute attackRoute)
        {
            ValidateRole(receiver, nameof(receiver));
            ValidateRole(secondActor, nameof(secondActor));
            ValidateRole(thirdActor, nameof(thirdActor));
            if (receiver == secondActor || secondActor == thirdActor)
            {
                throw new ArgumentException("Adjacent contacts require different actors.");
            }

            if (!Enum.IsDefined(typeof(SetRoute), setRoute))
            {
                throw new ArgumentOutOfRangeException(nameof(setRoute));
            }

            if (!Enum.IsDefined(typeof(SpikeRoute), attackRoute))
            {
                throw new ArgumentOutOfRangeException(nameof(attackRoute));
            }

            Receiver = receiver;
            SecondActor = secondActor;
            SetRoute = setRoute;
            ThirdActor = thirdActor;
            AttackRoute = attackRoute;
        }

        public PlayerRole Receiver { get; }

        public PlayerRole SecondActor { get; }

        public SetRoute SetRoute { get; }

        public PlayerRole ThirdActor { get; }

        public SpikeRoute AttackRoute { get; }

        public bool Equals(RoundDecisionV1 other)
        {
            return Receiver == other.Receiver &&
                   SecondActor == other.SecondActor &&
                   SetRoute == other.SetRoute &&
                   ThirdActor == other.ThirdActor &&
                   AttackRoute == other.AttackRoute;
        }

        public override bool Equals(object obj)
        {
            return obj is RoundDecisionV1 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Receiver;
                hashCode = (hashCode * 397) ^ (int)SecondActor;
                hashCode = (hashCode * 397) ^ (int)SetRoute;
                hashCode = (hashCode * 397) ^ (int)ThirdActor;
                return (hashCode * 397) ^ (int)AttackRoute;
            }
        }

        private static void ValidateRole(PlayerRole role, string parameterName)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), role))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public readonly struct TouchDecisionV1 : IEquatable<TouchDecisionV1>
    {
        public TouchDecisionV1(
            PlayerRole nextActor,
            TouchDecisionAction action,
            TargetZone targetZone,
            DecisionTempo tempo,
            DecisionRisk risk)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), nextActor))
            {
                throw new ArgumentOutOfRangeException(nameof(nextActor));
            }

            if (!Enum.IsDefined(typeof(TouchDecisionAction), action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            if (!Enum.IsDefined(typeof(TargetZone), targetZone))
            {
                throw new ArgumentOutOfRangeException(nameof(targetZone));
            }

            if (!Enum.IsDefined(typeof(DecisionTempo), tempo))
            {
                throw new ArgumentOutOfRangeException(nameof(tempo));
            }

            if (!Enum.IsDefined(typeof(DecisionRisk), risk))
            {
                throw new ArgumentOutOfRangeException(nameof(risk));
            }

            NextActor = nextActor;
            Action = action;
            TargetZone = targetZone;
            Tempo = tempo;
            Risk = risk;
        }

        public PlayerRole NextActor { get; }

        public TouchDecisionAction Action { get; }

        public TargetZone TargetZone { get; }

        public DecisionTempo Tempo { get; }

        public DecisionRisk Risk { get; }

        public bool Equals(TouchDecisionV1 other)
        {
            return NextActor == other.NextActor &&
                   Action == other.Action &&
                   TargetZone == other.TargetZone &&
                   Tempo == other.Tempo &&
                   Risk == other.Risk;
        }

        public override bool Equals(object obj)
        {
            return obj is TouchDecisionV1 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)NextActor;
                hashCode = (hashCode * 397) ^ (int)Action;
                hashCode = (hashCode * 397) ^ (int)TargetZone;
                hashCode = (hashCode * 397) ^ (int)Tempo;
                return (hashCode * 397) ^ (int)Risk;
            }
        }
    }

    public readonly struct DecisionValidationResult
    {
        private DecisionValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public bool IsValid { get; }

        public string Error { get; }

        public static DecisionValidationResult Valid()
        {
            return new DecisionValidationResult(true, string.Empty);
        }

        public static DecisionValidationResult Invalid(string error)
        {
            return new DecisionValidationResult(false, error);
        }
    }

    public static class TouchDecisionRules
    {
        public static DecisionValidationResult Validate(TouchDecisionV1 decision, int countedTeamTouches)
        {
            if (countedTeamTouches < 0 || countedTeamTouches > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(countedTeamTouches));
            }

            if (countedTeamTouches == 2 &&
                decision.Action != TouchDecisionAction.Attack &&
                decision.Action != TouchDecisionAction.FreeBall)
            {
                return DecisionValidationResult.Invalid("Third counted touch must go over the net.");
            }

            return DecisionValidationResult.Valid();
        }
    }
}
