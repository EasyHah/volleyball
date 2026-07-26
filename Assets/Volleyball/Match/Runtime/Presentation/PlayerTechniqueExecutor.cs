using System;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.Presentation
{
    internal sealed class PlayerExecutionCommand
    {
        public PlayerExecutionCommand(
            TechniqueAction action,
            float scheduledSimulationTime,
            SkillExecutionError error,
            int contactGroupId,
            SimVector3? plannedContactCenter,
            bool emergencyOneHand,
            Vector3? movementTarget,
            float movementStartSimulationTime,
            AttackApproachPlan? attackApproach,
            AttackContactPlan? attackContactPlan,
            SetRoute? normalSetRoute,
            bool controlledHandling,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact,
            SimVector3 targetVelocity)
        {
            Action = action;
            ScheduledSimulationTime = scheduledSimulationTime;
            ActualContactTime = scheduledSimulationTime + error.ContactTimingError;
            Error = error;
            ContactGroupId = contactGroupId;
            PlannedContactCenter = plannedContactCenter;
            EmergencyOneHand = emergencyOneHand;
            MovementTarget = movementTarget;
            MovementStartSimulationTime = movementStartSimulationTime;
            AttackApproach = attackApproach;
            AttackContactPlan = attackContactPlan;
            NormalSetRoute = normalSetRoute;
            ControlledHandling = controlledHandling;
            TrajectoryArtifact = trajectoryArtifact;
            TargetVelocity = targetVelocity;
        }

        public TechniqueAction Action { get; }
        public float ScheduledSimulationTime { get; }
        public float ActualContactTime { get; }
        public SkillExecutionError Error { get; }
        public int ContactGroupId { get; }
        public SimVector3? PlannedContactCenter { get; }
        public bool EmergencyOneHand { get; }
        public Vector3? MovementTarget { get; }
        public float MovementStartSimulationTime { get; }
        public AttackApproachPlan? AttackApproach { get; }
        public AttackContactPlan? AttackContactPlan { get; }
        public SetRoute? NormalSetRoute { get; }
        public bool ControlledHandling { get; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
        public SimVector3 TargetVelocity { get; }
    }

    public sealed class PlayerTechniqueExecutor
    {
        public ExecutionEnvelopeV4 ExecutionEnvelope { get; private set; }
        public ExecutionSampleV4 ExecutionSample { get; private set; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; private set; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; private set; }
        internal PlayerExecutionCommand ExecutionCommand { get; private set; }

        // Execution-specific state stays with the executor; the facade only coordinates it.
        internal TechniqueAction ScheduledAction { get; private set; }
        internal SkillExecutionError ScheduledError { get; private set; }
        internal SimVector3 ScheduledTargetVelocity { get; private set; }
        internal int ScheduledContactGroupId { get; private set; }
        internal SetTechniqueDecision SetDecision { get; private set; }
        internal AttackContactPlan ScheduledAttackContactPlan { get; private set; }
        internal bool HasAttackContactCommand { get; private set; }
        internal bool IsControlledHandling { get; private set; }

        internal void ConfigureLegacy(
            TechniqueAction action,
            SkillExecutionError error,
            int contactGroupId,
            SimVector3 targetVelocity,
            AttackContactPlan? attackContactPlan,
            SetTechniqueDecision setDecision,
            bool controlledHandling)
        {
            ScheduledAction = action;
            ScheduledError = error;
            ScheduledContactGroupId = contactGroupId;
            ScheduledTargetVelocity = targetVelocity;
            ScheduledAttackContactPlan = attackContactPlan.GetValueOrDefault();
            HasAttackContactCommand = attackContactPlan.HasValue;
            SetDecision = setDecision;
            IsControlledHandling = controlledHandling;
        }

        internal void SetControlledHandling(bool value)
        {
            IsControlledHandling = value;
        }

        public void ScheduleV4(
            TechniqueAction action,
            float scheduledSimulationTime,
            ExecutionSampleClassificationV4 classification,
            SkillExecutionError executionError,
            int contactGroupId,
            SimVector3? plannedContactCenter = null,
            bool emergencyOneHand = false,
            Vector3? movementTarget = null,
            float movementStartSimulationTime = 0f,
            AttackApproachPlan? attackApproach = null,
            AttackContactPlan? attackContactPlan = null,
            SetRoute? normalSetRoute = null,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact = null,
            bool controlledHandling = false)
        {
            ValidateV4(classification);

            var executableEnvelope = classification.ExecutableEnvelope;
            var executableSample = classification.ExecutableSample;
            var consumedVelocityError = new SkillExecutionError(
                executionError.ReactionDelay,
                executionError.ContactPositionError,
                executionError.ContactNormalErrorDegrees,
                executionError.ContactTimingError,
                executionError.SurfaceSpeedScale,
                SimVector3.Zero,
                executionError.MaximumTechniqueControl);
            ExecutionEnvelope = executableEnvelope;
            ExecutionSample = executableSample;
            ExecutionClassification = classification;
            TrajectoryArtifact = trajectoryArtifact;
            ExecutionCommand = new PlayerExecutionCommand(
                action,
                scheduledSimulationTime,
                consumedVelocityError,
                contactGroupId,
                plannedContactCenter,
                emergencyOneHand,
                movementTarget,
                movementStartSimulationTime,
                attackApproach,
                attackContactPlan,
                normalSetRoute,
                controlledHandling,
                trajectoryArtifact,
                executableSample.Velocity);
            ConfigureLegacy(
                action,
                consumedVelocityError,
                contactGroupId,
                executableSample.Velocity,
                attackContactPlan,
                default,
                controlledHandling);
        }

        public static void ValidateV4(
            ExecutionSampleClassificationV4 classification)
        {
            if (classification == null)
            {
                throw new ArgumentNullException(nameof(classification));
            }

            if (classification.Kind is not ExecutionSampleClassificationKindV4.Accepted
                and not ExecutionSampleClassificationKindV4.EnvelopeExpanded)
            {
                throw new InvalidOperationException("Only accepted or expanded V4 samples may be scheduled.");
            }

            var executableEnvelope = classification.ExecutableEnvelope ??
                throw new InvalidOperationException("Executable V4 envelope is required.");
            var executableSample = classification.ExecutableSample ??
                throw new InvalidOperationException("Executable V4 sample is required.");
            if (executableSample.EnvelopeIdentity != executableEnvelope.Identity)
            {
                throw new InvalidOperationException("Executable V4 sample must retain its envelope identity.");
            }

            if (executableSample.CandidateCategory !=
                executableEnvelope.CandidateCategory)
            {
                throw new InvalidOperationException(
                    "Executable V4 sample must retain its candidate category.");
            }
        }

        // Gate I uses this validation-only seam during batch preflight.  It must
        // not alter any scheduled command or expose a Set-contact path.
        public void ValidateGateIContact(
            TechniqueAction action,
            ExecutionSampleClassificationV4 classification,
            BallTrajectoryPredictionArtifactV4 trajectory,
            AttackApproachPlan? approach,
            AttackContactPlan? contactPlan)
        {
            if (string.IsNullOrWhiteSpace(trajectory?.ArtifactIdentity))
            {
                throw new InvalidOperationException(
                    "Gate I contact requires a trajectory artifact identity.");
            }

            ValidateV4(classification);
            if (approach.HasValue && action != TechniqueAction.Attack ||
                contactPlan.HasValue && action != TechniqueAction.Attack ||
                contactPlan.HasValue && !approach.HasValue)
            {
                throw new ArgumentException(
                    "Only a complete attack contact may carry attack planning.");
            }

            if (contactPlan.HasValue)
            {
                contactPlan.Value.Validate();
                if (!contactPlan.Value.Takeoff.Equals(approach.Value.Takeoff))
                {
                    throw new ArgumentException(
                        "Attack contact plan must retain the planned takeoff.");
                }
            }
        }

        public void ValidateGateISupport(
            TechniqueAction action,
            float scheduledTime,
            Vector3 target)
        {
            if (action != TechniqueAction.Block && action != TechniqueAction.Receive)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            if (float.IsNaN(scheduledTime) || float.IsInfinity(scheduledTime) ||
                float.IsNaN(target.x) || float.IsInfinity(target.x) ||
                float.IsNaN(target.y) || float.IsInfinity(target.y) ||
                float.IsNaN(target.z) || float.IsInfinity(target.z))
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledTime));
            }
        }

        internal void Clear()
        {
            ExecutionEnvelope = null;
            ExecutionSample = null;
            ExecutionClassification = null;
            TrajectoryArtifact = null;
            ExecutionCommand = null;
            ScheduledAction = default;
            ScheduledError = default;
            ScheduledTargetVelocity = default;
            ScheduledContactGroupId = default;
            SetDecision = default;
            ScheduledAttackContactPlan = default;
            HasAttackContactCommand = false;
            IsControlledHandling = false;
        }
    }
}
