using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    internal readonly struct ScheduledPlayerContact
    {
        public ScheduledPlayerContact(
            TechniqueAction contactAction, TechniqueAction surfaceAction, int contactGroupId,
            float playerTechnique, SimVector3 targetVelocity, SetContactHand setContactHand,
            SimVector3? plannedContactCenter, bool useExactTargetVelocity = false,
            bool preservePlannedContactRoot = false)
        {
            ContactAction = contactAction;
            SurfaceAction = surfaceAction;
            ContactGroupId = contactGroupId;
            PlayerTechnique = playerTechnique;
            TargetVelocity = targetVelocity;
            SetContactHand = setContactHand;
            PlannedContactCenter = plannedContactCenter;
            UseExactTargetVelocity = useExactTargetVelocity;
            PreservePlannedContactRoot = preservePlannedContactRoot;
        }

        public TechniqueAction ContactAction { get; }
        public TechniqueAction SurfaceAction { get; }
        public int ContactGroupId { get; }
        public float PlayerTechnique { get; }
        public SimVector3 TargetVelocity { get; }
        public SetContactHand SetContactHand { get; }
        public SimVector3? PlannedContactCenter { get; }
        public bool UseExactTargetVelocity { get; }
        public bool PreservePlannedContactRoot { get; }

        public PlayerContactInput WithSample(PlayerId playerId, ActionTimelineSample sample) =>
            new PlayerContactInput(playerId, ContactAction, SurfaceAction, sample, ContactGroupId,
                PlayerTechnique, TargetVelocity, SetContactHand, PlannedContactCenter,
                UseExactTargetVelocity);
    }

    /// <summary>Builds contact candidates from already-resolved player presentation state.</summary>
    public readonly struct PlayerContactInput
    {
        public PlayerContactInput(
            PlayerId playerId,
            TechniqueAction contactAction,
            TechniqueAction surfaceAction,
            ActionTimelineSample sample,
            int contactGroupId,
            float playerTechnique,
            SimVector3 targetVelocity,
            SetContactHand setContactHand,
            SimVector3? plannedContactCenter = null,
            bool useExactTargetVelocity = false)
        {
            PlayerId = playerId;
            ContactAction = contactAction;
            SurfaceAction = surfaceAction;
            Sample = sample;
            ContactGroupId = contactGroupId;
            PlayerTechnique = playerTechnique;
            TargetVelocity = targetVelocity;
            SetContactHand = setContactHand;
            PlannedContactCenter = plannedContactCenter;
            UseExactTargetVelocity = useExactTargetVelocity;
        }

        public PlayerId PlayerId { get; }
        public TechniqueAction ContactAction { get; }
        public TechniqueAction SurfaceAction { get; }
        public ActionTimelineSample Sample { get; }
        public int ContactGroupId { get; }
        public float PlayerTechnique { get; }
        public SimVector3 TargetVelocity { get; }
        public SetContactHand SetContactHand { get; }
        public SimVector3? PlannedContactCenter { get; }
        public bool UseExactTargetVelocity { get; }
    }

    public sealed class PlayerContactSurfaceProvider
    {
        private readonly PlayerContactSurfaces _surfaces;
        private readonly BlockArmContactVolumes _blockVolumes;
        private bool _cleared;

        internal ScheduledPlayerContact ScheduledContact { get; private set; }
        internal bool HasPlannedContactCenter { get; private set; }
        internal bool PreservePlannedContactRoot => ScheduledContact.PreservePlannedContactRoot;
        internal SimVector3 PlannedContactCenter { get; private set; }
        internal bool HasPhysicalBlockContact { get; private set; }
        internal SimVector3 PhysicalBlockTargetVelocity { get; private set; }
        internal int PhysicalBlockContactGroupId { get; private set; }
        internal bool PhysicalBlockActivationLogged { get; set; }
        internal int PhysicalBlockContactAssignments { get; private set; }
        internal float BlockRetargetDistance { get; private set; }
        internal float BlockRetargetTimeShift { get; private set; }
        internal float PhysicalBlockContactTime { get; private set; }

        public PlayerContactSurfaceProvider(StickFigureRig rig, Transform playerRoot)
            : this(new PlayerContactSurfaces(rig, playerRoot), new BlockArmContactVolumes(rig))
        {
        }

        public PlayerContactSurfaceProvider(
            PlayerContactSurfaces surfaces,
            BlockArmContactVolumes blockVolumes)
        {
            _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _blockVolumes = blockVolumes ?? throw new ArgumentNullException(nameof(blockVolumes));
        }

        public SimVector3 LastScheduledSurfaceCenter { get; private set; }
        public SimVector3 LastScheduledSurfaceNormal { get; private set; }
        public float MinimumActiveSurfacePlanError { get; private set; } = float.PositiveInfinity;

        public PlayerContactSurfaces Surfaces => _surfaces;

        public void Begin()
        {
            _cleared = false;
            LastScheduledSurfaceCenter = SimVector3.Zero;
            LastScheduledSurfaceNormal = SimVector3.Zero;
            MinimumActiveSurfacePlanError = float.PositiveInfinity;
        }

        public void Clear()
        {
            _cleared = true;
            _surfaces.Clear();
            LastScheduledSurfaceCenter = SimVector3.Zero;
            LastScheduledSurfaceNormal = SimVector3.Zero;
            MinimumActiveSurfacePlanError = float.PositiveInfinity;
            ScheduledContact = default;
            HasPlannedContactCenter = false;
            HasPhysicalBlockContact = false;
            PhysicalBlockActivationLogged = false;
        }

        internal void ScheduleContact(ScheduledPlayerContact contact)
        {
            ScheduledContact = contact;
            HasPlannedContactCenter = contact.PlannedContactCenter.HasValue;
            PlannedContactCenter = contact.PlannedContactCenter.GetValueOrDefault();
        }

        internal void ClearScheduledContact()
        {
            ScheduledContact = default;
            HasPlannedContactCenter = false;
        }

        internal void SchedulePhysicalBlock(SimVector3 targetVelocity, int contactGroupId, float contactTime)
        {
            HasPhysicalBlockContact = true;
            PhysicalBlockTargetVelocity = targetVelocity;
            PhysicalBlockContactGroupId = contactGroupId;
            PhysicalBlockContactTime = contactTime;
            PhysicalBlockActivationLogged = false;
            PhysicalBlockContactAssignments++;
            BlockRetargetDistance = 0f;
            BlockRetargetTimeShift = 0f;
        }

        internal void RetargetPhysicalBlock(SimVector3 targetVelocity, float contactTime, float timeShift, float distance)
        {
            PhysicalBlockTargetVelocity = targetVelocity;
            PhysicalBlockContactTime = contactTime;
            BlockRetargetTimeShift = Mathf.Abs(timeShift);
            BlockRetargetDistance = distance;
        }

        internal void DisablePhysicalBlock()
        {
            HasPhysicalBlockContact = false;
            PhysicalBlockActivationLogged = false;
        }

        public void Collect(PlayerContactInput input, ICollection<BallContactCandidate> contacts)
        {
            if (_cleared || contacts == null)
            {
                return;
            }

            var surfaces = _surfaces.Capture(
                input.SurfaceAction,
                input.Sample.SurfaceActive,
                input.ContactGroupId,
                setContactHand: input.SetContactHand);
            UpdateScheduledSurfaceDiagnostics(
                surfaces,
                input.Sample.SurfaceActive ? input.PlannedContactCenter : null);
            var strikeDirection = input.TargetVelocity.SqrMagnitude > 0.000001f
                ? input.TargetVelocity.Normalized
                : SimVector3.Up;
            var response = ResponseFor(input.ContactAction);
            foreach (var surface in surfaces)
            {
                contacts.Add(new BallContactCandidate(
                    surface,
                    input.ContactAction,
                    input.PlayerId,
                    input.PlayerTechnique,
                    input.TargetVelocity,
                    strikeDirection,
                    response,
                    input.UseExactTargetVelocity));
            }
        }

        public IReadOnlyList<ContactCapsuleSnapshot> CaptureBlock(
            ActionTimelineSample sample,
            int contactGroupId)
        {
            return _blockVolumes.Capture(sample.SurfaceActive, contactGroupId);
        }

        public void CollectBlock(
            PlayerId playerId,
            ActionTimelineSample sample,
            int contactGroupId,
            float playerTechnique,
            SimVector3 targetVelocity,
            SimVector3 fallbackStrikeDirection,
            IReadOnlyList<ContactCapsuleSnapshot> volumes,
            ICollection<BallContactCandidate> contacts)
        {
            if (_cleared || contacts == null || !sample.SurfaceActive)
            {
                return;
            }

            var strikeDirection = targetVelocity.SqrMagnitude > 0.000001f
                ? targetVelocity.Normalized
                : fallbackStrikeDirection;
            foreach (var volume in volumes)
            {
                contacts.Add(new BallContactCandidate(
                    volume,
                    TechniqueAction.Block,
                    playerId,
                    playerTechnique,
                    targetVelocity,
                    strikeDirection,
                    ResponseFor(TechniqueAction.Block)));
            }
        }

        public IReadOnlyList<ContactSurfaceFrame> PreviewFrames(
            TechniqueAction action,
            SetContactHand setContactHand = SetContactHand.Both)
        {
            return _surfaces.CaptureCurrent(action, setContactHand);
        }

        public IReadOnlyList<ContactCapsuleFrame> PreviewBlockFrames()
        {
            return _blockVolumes.CaptureCurrent();
        }

        internal SimVector3 CaptureSurfaceCenter(
            TechniqueAction action,
            int contactGroupId,
            SetContactHand setContactHand)
        {
            var frames = _surfaces.CaptureCurrent(action, setContactHand);
            var center = SimVector3.Zero;
            foreach (var frame in frames)
            {
                center += frame.Origin + (frame.Normal * SimulatedBall.DefaultRadius);
            }

            return center / frames.Count;
        }

        private void UpdateScheduledSurfaceDiagnostics(
            IReadOnlyList<ContactSurfaceSnapshot> surfaces,
            SimVector3? plannedContactCenter)
        {
            LastScheduledSurfaceCenter = SimVector3.Zero;
            LastScheduledSurfaceNormal = SimVector3.Zero;
            foreach (var surface in surfaces)
            {
                LastScheduledSurfaceCenter += surface.Current.Origin +
                                              (surface.Current.Normal * SimulatedBall.DefaultRadius);
                LastScheduledSurfaceNormal += surface.Current.Normal;
            }

            LastScheduledSurfaceCenter /= surfaces.Count;
            LastScheduledSurfaceNormal = (LastScheduledSurfaceNormal / surfaces.Count).Normalized;
            if (plannedContactCenter.HasValue)
            {
                MinimumActiveSurfacePlanError = Mathf.Min(
                    MinimumActiveSurfacePlanError,
                    (LastScheduledSurfaceCenter - plannedContactCenter.Value).Magnitude);
            }
        }

        private static ContactResponseParameters ResponseFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new ContactResponseParameters(0.85f, 1f, 0.12f, 0.08f),
                TechniqueAction.Set => new ContactResponseParameters(0.75f, 1f, 0.08f, 0.08f),
                TechniqueAction.Attack => new ContactResponseParameters(0.55f, 0.42f, 0.18f, 0.08f),
                TechniqueAction.Block => new ContactResponseParameters(0.65f, 0.8f, 0.22f, 0.08f),
                TechniqueAction.Serve => new ContactResponseParameters(0.72f, 1f, 0.15f, 0.08f),
                _ => new ContactResponseParameters(0.75f, 1f, 0.1f, 0.08f)
            };
        }
    }
}
