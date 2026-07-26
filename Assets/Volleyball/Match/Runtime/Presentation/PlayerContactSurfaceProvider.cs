using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
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
            SimVector3? plannedContactCenter = null)
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
    }

    public sealed class PlayerContactSurfaceProvider
    {
        private readonly PlayerContactSurfaces _surfaces;
        private readonly BlockArmContactVolumes _blockVolumes;
        private bool _cleared;

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
                    response));
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
            var snapshots = _surfaces.Capture(action, true, 0, setContactHand: setContactHand);
            var frames = new ContactSurfaceFrame[snapshots.Count];
            for (var index = 0; index < snapshots.Count; index++)
            {
                frames[index] = snapshots[index].Current;
            }

            return frames;
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
