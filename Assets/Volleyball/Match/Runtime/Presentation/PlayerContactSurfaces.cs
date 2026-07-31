using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public enum SetContactHand
    {
        Both,
        Left,
        Right
    }

    public sealed class PlayerContactSurfaces
    {
        private readonly StickFigureRig _rig;
        private readonly Transform _playerRoot;
        private readonly Dictionary<string, ContactSurfaceFrame> _previous =
            new Dictionary<string, ContactSurfaceFrame>();

        public PlayerContactSurfaces(StickFigureRig rig, Transform playerRoot)
        {
            _rig = rig != null ? rig : throw new ArgumentNullException(nameof(rig));
            _playerRoot = playerRoot != null ? playerRoot : throw new ArgumentNullException(nameof(playerRoot));
        }

        public IReadOnlyList<ContactSurfaceSnapshot> Capture(
            TechniqueAction action,
            bool active,
            int contactGroupId,
            Vector3 localPositionError = default,
            Vector3 localNormalErrorDegrees = default,
            SetContactHand setContactHand = SetContactHand.Both)
        {
            return CaptureSnapshots(
                action,
                active,
                contactGroupId,
                localPositionError,
                localNormalErrorDegrees,
                setContactHand,
                true);
        }

        internal IReadOnlyList<ContactSurfaceFrame> CaptureCurrent(
            TechniqueAction action,
            SetContactHand setContactHand = SetContactHand.Both)
        {
            var snapshots = CaptureSnapshots(
                action,
                true,
                0,
                default,
                default,
                setContactHand,
                false);
            var frames = new ContactSurfaceFrame[snapshots.Count];
            for (var index = 0; index < snapshots.Count; index++)
            {
                frames[index] = snapshots[index].Current;
            }

            return frames;
        }

        private IReadOnlyList<ContactSurfaceSnapshot> CaptureSnapshots(
            TechniqueAction action,
            bool active,
            int contactGroupId,
            Vector3 localPositionError,
            Vector3 localNormalErrorDegrees,
            SetContactHand setContactHand,
            bool recordHistory)
        {
            return action switch
            {
                TechniqueAction.Receive => new[]
                {
                    Snapshot("ForearmPlatform", BuildForearmPlatform(localPositionError, localNormalErrorDegrees), active, contactGroupId, false, recordHistory)
                },
                TechniqueAction.Set => CaptureSet(
                    active,
                    contactGroupId,
                    localPositionError,
                    localNormalErrorDegrees,
                    setContactHand,
                    recordHistory),
                TechniqueAction.Attack => new[]
                {
                    Snapshot("AttackPalm", BuildPalm(
                        StrikingPalm(setContactHand),
                        localPositionError,
                        localNormalErrorDegrees,
                        true), active, contactGroupId, true, recordHistory)
                },
                TechniqueAction.Block => new[]
                {
                    Snapshot("BlockLeftPalm", BuildBlockPalm("LeftPalm", localPositionError, localNormalErrorDegrees), active, contactGroupId, false, recordHistory),
                    Snapshot("BlockRightPalm", BuildBlockPalm("RightPalm", localPositionError, localNormalErrorDegrees), active, contactGroupId, false, recordHistory)
                },
                TechniqueAction.Serve => new[]
                {
                    Snapshot("ServePalm", BuildPalm(StrikingPalm(setContactHand), localPositionError, localNormalErrorDegrees, true), active, contactGroupId, false, recordHistory)
                },
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static string StrikingPalm(SetContactHand hand) =>
            hand == SetContactHand.Left ? "LeftPalm" : "RightPalm";

        public void Clear()
        {
            _previous.Clear();
        }

        private IReadOnlyList<ContactSurfaceSnapshot> CaptureSet(
            bool active,
            int contactGroupId,
            Vector3 localPositionError,
            Vector3 localNormalErrorDegrees,
            SetContactHand setContactHand,
            bool recordHistory)
        {
            if (setContactHand == SetContactHand.Left)
            {
                return new[]
                {
                    Snapshot("LeftPalm", BuildPalm("LeftPalm", localPositionError, localNormalErrorDegrees, false), active, contactGroupId, false, recordHistory)
                };
            }

            if (setContactHand == SetContactHand.Right)
            {
                return new[]
                {
                    Snapshot("RightPalm", BuildPalm("RightPalm", localPositionError, localNormalErrorDegrees, false), active, contactGroupId, false, recordHistory)
                };
            }

            return new[]
            {
                Snapshot("LeftPalm", BuildPalm("LeftPalm", localPositionError, localNormalErrorDegrees, false), active, contactGroupId, false, recordHistory),
                Snapshot("RightPalm", BuildPalm("RightPalm", localPositionError, localNormalErrorDegrees, false), active, contactGroupId, false, recordHistory)
            };
        }

        private ContactSurfaceFrame BuildForearmPlatform(Vector3 localPositionError, Vector3 normalErrorDegrees)
        {
            var leftElbow = _rig.GetJoint("LeftElbow").position;
            var rightElbow = _rig.GetJoint("RightElbow").position;
            var leftHand = _rig.GetJoint("LeftHand").position;
            var rightHand = _rig.GetJoint("RightHand").position;
            var leftMidpoint = Vector3.Lerp(leftElbow, leftHand, 0.58f);
            var rightMidpoint = Vector3.Lerp(rightElbow, rightHand, 0.58f);
            var right = (rightMidpoint - leftMidpoint).normalized;
            var forearmDirection = (((leftHand - leftElbow) + (rightHand - rightElbow)) * 0.5f).normalized;
            var normal = Vector3.Cross(right, forearmDirection).normalized;
            var desiredFacing = (_playerRoot.forward + (Vector3.up * 0.55f)).normalized;
            if (Vector3.Dot(normal, desiredFacing) < 0f)
            {
                normal = -normal;
            }

            normal = ApplyNormalError(normal, normalErrorDegrees);
            right = Vector3.ProjectOnPlane(right, normal).normalized;
            var up = Vector3.Cross(normal, right).normalized;
            var origin = ((leftMidpoint + rightMidpoint) * 0.5f) + _playerRoot.TransformVector(localPositionError);
            return ToFrame(origin, normal, right, up, Vector3.Distance(leftMidpoint, rightMidpoint) + 0.18f, 0.42f);
        }

        private ContactSurfaceFrame BuildPalm(
            string palmName,
            Vector3 localPositionError,
            Vector3 normalErrorDegrees,
            bool striking,
            float width = 0.22f,
            float height = 0.20f)
        {
            var palm = _rig.GetJoint(palmName);
            var normal = striking
                ? (_playerRoot.forward + (Vector3.up * 0.35f)).normalized
                : (Vector3.up + (_playerRoot.forward * 0.22f)).normalized;
            normal = ApplyNormalError(normal, normalErrorDegrees);
            var right = Vector3.ProjectOnPlane(_playerRoot.right, normal).normalized;
            var up = Vector3.Cross(normal, right).normalized;
            var origin = palm.position + _playerRoot.TransformVector(localPositionError);
            return ToFrame(origin, normal, right, up, width, height);
        }

        private ContactSurfaceFrame BuildBlockPalm(
            string palmName,
            Vector3 localPositionError,
            Vector3 normalErrorDegrees)
        {
            var palm = _rig.GetJoint(palmName);
            var normal = (_playerRoot.forward + (Vector3.up * 0.08f)).normalized;
            normal = ApplyNormalError(normal, normalErrorDegrees);
            var right = Vector3.ProjectOnPlane(_playerRoot.right, normal).normalized;
            var up = Vector3.Cross(normal, right).normalized;
            var origin = palm.position + _playerRoot.TransformVector(localPositionError);
            return ToFrame(origin, normal, right, up, 0.34f, 0.55f);
        }

        private Vector3 ApplyNormalError(Vector3 normal, Vector3 localDegrees)
        {
            var localNormal = _playerRoot.InverseTransformDirection(normal);
            return _playerRoot.TransformDirection(Quaternion.Euler(localDegrees) * localNormal).normalized;
        }

        private ContactSurfaceSnapshot Snapshot(
            string key,
            ContactSurfaceFrame current,
            bool active,
            int contactGroupId,
            bool twoSided,
            bool recordHistory)
        {
            var previous = _previous.TryGetValue(key, out var stored) ? stored : current;
            if (recordHistory)
            {
                _previous[key] = current;
            }
            return new ContactSurfaceSnapshot(previous, current, active, contactGroupId, twoSided);
        }

        private static ContactSurfaceFrame ToFrame(
            Vector3 origin,
            Vector3 normal,
            Vector3 right,
            Vector3 up,
            float width,
            float height)
        {
            return new ContactSurfaceFrame(
                ToSimulation(origin),
                ToSimulation(normal),
                ToSimulation(right),
                ToSimulation(up),
                width,
                height);
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }
    }
}
