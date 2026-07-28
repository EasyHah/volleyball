using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public sealed class BlockArmContactVolumes
    {
        private static readonly SegmentDefinition[] Segments =
        {
            new SegmentDefinition("LeftUpperArm", "LeftShoulder", "LeftElbow", 0.065f),
            new SegmentDefinition("LeftForearm", "LeftElbow", "LeftHand", 0.065f),
            new SegmentDefinition("LeftPalm", "LeftHand", "LeftPalm", 0.11f),
            new SegmentDefinition("RightUpperArm", "RightShoulder", "RightElbow", 0.065f),
            new SegmentDefinition("RightForearm", "RightElbow", "RightHand", 0.065f),
            new SegmentDefinition("RightPalm", "RightHand", "RightPalm", 0.11f)
        };

        private readonly StickFigureRig _rig;
        private readonly Dictionary<string, ContactCapsuleFrame> _previous =
            new Dictionary<string, ContactCapsuleFrame>();

        public BlockArmContactVolumes(StickFigureRig rig)
        {
            _rig = rig ?? throw new ArgumentNullException(nameof(rig));
        }

        public IReadOnlyList<ContactCapsuleSnapshot> Capture(bool active, int contactGroupId)
        {
            var snapshots = new ContactCapsuleSnapshot[Segments.Length];
            var currentFrames = CaptureCurrent();
            for (var index = 0; index < Segments.Length; index++)
            {
                var segment = Segments[index];
                var current = currentFrames[index];
                var previous = _previous.TryGetValue(segment.Name, out var stored)
                    ? stored
                    : current;
                _previous[segment.Name] = current;
                snapshots[index] = new ContactCapsuleSnapshot(
                    previous,
                    current,
                    active,
                    contactGroupId);
            }

            return snapshots;
        }

        public IReadOnlyList<ContactCapsuleFrame> CaptureCurrent()
        {
            var frames = new ContactCapsuleFrame[Segments.Length];
            for (var index = 0; index < Segments.Length; index++)
            {
                var segment = Segments[index];
                frames[index] = new ContactCapsuleFrame(
                    ToSimulation(_rig.GetJoint(segment.StartJoint).position),
                    ToSimulation(_rig.GetJoint(segment.EndJoint).position),
                    segment.Radius);
            }

            return frames;
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }

        private readonly struct SegmentDefinition
        {
            public SegmentDefinition(
                string name,
                string startJoint,
                string endJoint,
                float radius)
            {
                Name = name;
                StartJoint = startJoint;
                EndJoint = endJoint;
                Radius = radius;
            }

            public string Name { get; }

            public string StartJoint { get; }

            public string EndJoint { get; }

            public float Radius { get; }
        }
    }
}
