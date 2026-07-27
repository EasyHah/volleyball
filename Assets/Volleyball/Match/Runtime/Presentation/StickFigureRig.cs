using System;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation
{
    public sealed class StickFigureRig : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _joints = new Dictionary<string, Transform>();
        private readonly Dictionary<StickFigurePose, Dictionary<string, Vector3>> _poses =
            new Dictionary<StickFigurePose, Dictionary<string, Vector3>>();

        public static StickFigureRig Create(Transform parent, Color teamColor, string jerseyNumber)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject("StickFigureRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<StickFigureRig>();
            rig.Build(teamColor, jerseyNumber ?? string.Empty);
            return rig;
        }

        public bool HasJoint(string jointName)
        {
            return jointName != null && _joints.ContainsKey(jointName);
        }

        public Transform GetJoint(string jointName)
        {
            if (jointName == null)
            {
                throw new ArgumentNullException(nameof(jointName));
            }

            return _joints[jointName];
        }

        public Dictionary<string, Quaternion> CaptureLocalRotations()
        {
            var snapshot = new Dictionary<string, Quaternion>(_joints.Count);
            foreach (var joint in _joints)
            {
                snapshot.Add(joint.Key, joint.Value.localRotation);
            }

            return snapshot;
        }

        public void RestoreLocalRotations(IReadOnlyDictionary<string, Quaternion> snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            foreach (var joint in _joints)
            {
                if (!snapshot.TryGetValue(joint.Key, out var rotation))
                {
                    throw new ArgumentException("Pose snapshot does not contain every rig joint.", nameof(snapshot));
                }

                joint.Value.localRotation = rotation;
            }
        }

        public void SetPose(StickFigurePose pose, float normalizedBlend)
        {
            SetPoseWithContactError(pose, normalizedBlend, TechniqueAction.Receive, SimVector3.Zero, SimVector3.Zero, 0f);
        }

        public void SetPoseWithContactError(
            StickFigurePose pose,
            float normalizedBlend,
            TechniqueAction action,
            SimVector3 positionError,
            SimVector3 normalErrorDegrees,
            float errorWeight)
        {
            if (!_poses.TryGetValue(pose, out var targets))
            {
                throw new ArgumentOutOfRangeException(nameof(pose), pose, "Unknown stick-figure pose.");
            }

            var blend = Mathf.Clamp01(normalizedBlend);
            foreach (var joint in _joints)
            {
                var target = targets[joint.Key] + ContactRotationOffset(
                    joint.Key,
                    action,
                    positionError,
                    normalErrorDegrees,
                    Mathf.Clamp01(errorWeight));
                joint.Value.localRotation = Quaternion.Slerp(
                    joint.Value.localRotation,
                    Quaternion.Euler(target),
                    blend);
            }
        }

        public void SetPoseTransition(
            StickFigurePose from,
            StickFigurePose to,
            float normalizedProgress,
            TechniqueAction action,
            SimVector3 positionError,
            SimVector3 normalErrorDegrees,
            float errorWeight)
        {
            if (!_poses.TryGetValue(from, out var fromTargets))
            {
                throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown start pose.");
            }

            if (!_poses.TryGetValue(to, out var toTargets))
            {
                throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown end pose.");
            }

            var progress = Mathf.Clamp01(normalizedProgress);
            foreach (var joint in _joints)
            {
                var offset = ContactRotationOffset(
                    joint.Key,
                    action,
                    positionError,
                    normalErrorDegrees,
                    Mathf.Clamp01(errorWeight));
                joint.Value.localRotation = Quaternion.Slerp(
                    Quaternion.Euler(fromTargets[joint.Key]),
                    Quaternion.Euler(toTargets[joint.Key] + offset),
                    progress);
            }
        }

        private static Vector3 ContactRotationOffset(
            string jointName,
            TechniqueAction action,
            SimVector3 positionError,
            SimVector3 normalErrorDegrees,
            float weight)
        {
            var isLeft = jointName.StartsWith("Left", StringComparison.Ordinal);
            var side = isLeft ? -1f : 1f;
            var offset = Vector3.zero;
            switch (action)
            {
                case TechniqueAction.Receive when jointName.EndsWith("Shoulder", StringComparison.Ordinal):
                    offset = new Vector3(
                        (-positionError.Z * 90f) + normalErrorDegrees.X,
                        normalErrorDegrees.Y,
                        (positionError.X * 100f * side) + normalErrorDegrees.Z);
                    break;
                case TechniqueAction.Set when jointName.EndsWith("Shoulder", StringComparison.Ordinal):
                case TechniqueAction.Block when jointName.EndsWith("Shoulder", StringComparison.Ordinal):
                    offset = new Vector3(
                        (-positionError.Y * 80f) + normalErrorDegrees.X,
                        positionError.X * 90f * side,
                        normalErrorDegrees.Z);
                    break;
                case TechniqueAction.Attack when jointName == "RightShoulder":
                case TechniqueAction.Serve when jointName == "RightShoulder":
                    offset = new Vector3(
                        (-positionError.Y * 100f) + normalErrorDegrees.X,
                        positionError.X * 100f,
                        normalErrorDegrees.Z);
                    break;
                case TechniqueAction.Attack when jointName == "RightElbow":
                case TechniqueAction.Serve when jointName == "RightElbow":
                    offset = new Vector3(positionError.Z * 90f, normalErrorDegrees.Y, 0f);
                    break;
            }

            return offset * weight;
        }

        private void Build(Color teamColor, string jerseyNumber)
        {
            CreateJoint("Hips", transform, new Vector3(0f, 1.05f, 0f));
            CreateVisual(_joints["Hips"], "HipsVisual", PrimitiveType.Cube, new Vector3(0.42f, 0.24f, 0.24f), teamColor);

            CreateJoint("Torso", _joints["Hips"], new Vector3(0f, 0.42f, 0f));
            CreateVisual(_joints["Torso"], "TorsoVisual", PrimitiveType.Cube, new Vector3(0.52f, 0.68f, 0.26f), teamColor);

            CreateJoint("Head", _joints["Torso"], new Vector3(0f, 0.52f, 0f));
            CreateVisual(_joints["Head"], "HeadVisual", PrimitiveType.Sphere, Vector3.one * 0.34f, new Color(1f, 0.78f, 0.61f));

            CreateArm("Left", -1f, teamColor);
            CreateArm("Right", 1f, teamColor);
            CreateLeg("Left", -1f, teamColor);
            CreateLeg("Right", 1f, teamColor);
            CreateJerseyNumber(jerseyNumber);
            CreatePoseLibrary();
            SetPose(StickFigurePose.Ready, 1f);
        }

        private void CreateArm(string side, float direction, Color color)
        {
            var shoulder = CreateJoint(side + "Shoulder", _joints["Torso"], new Vector3(0.34f * direction, 0.24f, 0f));
            CreateSegment(shoulder, side + "UpperArm", 0.42f, color);
            var elbow = CreateJoint(side + "Elbow", shoulder, new Vector3(0f, -0.42f, 0f));
            CreateSegment(elbow, side + "Forearm", 0.38f, color);
            var hand = CreateJoint(side + "Hand", elbow, new Vector3(0f, -0.38f, 0f));
            var palm = CreateJoint(side + "Palm", hand, new Vector3(0f, -0.04f, 0.06f));
            var handVisual = CreateVisual(
                palm,
                side + "HandVisual",
                PrimitiveType.Cube,
                new Vector3(0.16f, 0.08f, 0.22f),
                new Color(1f, 0.78f, 0.61f));
            handVisual.localPosition = Vector3.zero;
        }

        private void CreateLeg(string side, float direction, Color color)
        {
            var hip = CreateJoint(side + "Hip", _joints["Hips"], new Vector3(0.16f * direction, -0.12f, 0f));
            CreateSegment(hip, side + "Thigh", 0.5f, color);
            var knee = CreateJoint(side + "Knee", hip, new Vector3(0f, -0.5f, 0f));
            CreateSegment(knee, side + "Shin", 0.48f, color);
            var foot = CreateJoint(side + "Foot", knee, new Vector3(0f, -0.48f, 0.08f));
            CreateVisual(foot, side + "FootVisual", PrimitiveType.Cube, new Vector3(0.18f, 0.12f, 0.34f), Color.white);
        }

        private Transform CreateJoint(string jointName, Transform parent, Vector3 localPosition)
        {
            var jointObject = new GameObject(jointName);
            var joint = jointObject.transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            _joints.Add(jointName, joint);
            return joint;
        }

        private static void CreateSegment(Transform parent, string name, float length, Color color)
        {
            var visual = CreateVisual(parent, name, PrimitiveType.Cube, new Vector3(0.13f, length, 0.13f), color);
            visual.localPosition = new Vector3(0f, -length * 0.5f, 0f);
        }

        private static Transform CreateVisual(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 scale,
            Color color)
        {
            var visualObject = GameObject.CreatePrimitive(primitive);
            visualObject.name = name;
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localScale = scale;
            var collider = visualObject.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = visualObject.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
            return visualObject.transform;
        }

        private void CreateJerseyNumber(string jerseyNumber)
        {
            var numberObject = new GameObject("JerseyNumber");
            numberObject.transform.SetParent(_joints["Torso"], false);
            numberObject.transform.localPosition = new Vector3(0f, 0f, -0.14f);
            numberObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var number = numberObject.AddComponent<TextMesh>();
            number.text = jerseyNumber;
            number.anchor = TextAnchor.MiddleCenter;
            number.alignment = TextAlignment.Center;
            number.characterSize = 0.08f;
            number.fontSize = 48;
            number.color = Color.white;
        }

        private void CreatePoseLibrary()
        {
            foreach (StickFigurePose pose in Enum.GetValues(typeof(StickFigurePose)))
            {
                var targets = new Dictionary<string, Vector3>();
                foreach (var jointName in _joints.Keys)
                {
                    targets.Add(jointName, Vector3.zero);
                }

                _poses.Add(pose, targets);
            }

            SetTargets(StickFigurePose.Ready,
                ("LeftShoulder", new Vector3(20f, 0f, -18f)),
                ("RightShoulder", new Vector3(20f, 0f, 18f)),
                ("LeftElbow", new Vector3(-28f, 0f, 0f)),
                ("RightElbow", new Vector3(-28f, 0f, 0f)),
                ("LeftHip", new Vector3(12f, 0f, -8f)),
                ("RightHip", new Vector3(12f, 0f, 8f)),
                ("LeftKnee", new Vector3(-22f, 0f, 0f)),
                ("RightKnee", new Vector3(-22f, 0f, 0f)));

            SetTargets(StickFigurePose.Run,
                ("LeftShoulder", new Vector3(-45f, 0f, -8f)),
                ("RightShoulder", new Vector3(45f, 0f, 8f)),
                ("LeftHip", new Vector3(38f, 0f, 0f)),
                ("RightHip", new Vector3(-38f, 0f, 0f)),
                ("LeftKnee", new Vector3(-35f, 0f, 0f)),
                ("RightKnee", new Vector3(-8f, 0f, 0f)));

            SetTargets(StickFigurePose.Serve,
                ("LeftShoulder", new Vector3(-75f, 0f, -10f)),
                ("RightShoulder", new Vector3(-145f, 0f, 10f)),
                ("RightElbow", new Vector3(-35f, 0f, 0f)),
                ("Torso", new Vector3(-8f, 0f, 0f)));

            SetTargets(StickFigurePose.Receive,
                ("LeftShoulder", new Vector3(-58f, 0f, 25f)),
                ("RightShoulder", new Vector3(-58f, 0f, -25f)),
                ("LeftElbow", new Vector3(8f, 0f, 0f)),
                ("RightElbow", new Vector3(8f, 0f, 0f)),
                ("LeftHip", new Vector3(25f, 0f, -8f)),
                ("RightHip", new Vector3(25f, 0f, 8f)),
                ("LeftKnee", new Vector3(-45f, 0f, 0f)),
                ("RightKnee", new Vector3(-45f, 0f, 0f)));

            SetTargets(StickFigurePose.SetDraw,
                ("LeftShoulder", new Vector3(-108f, 0f, 25f)),
                ("RightShoulder", new Vector3(-108f, 0f, -25f)),
                ("LeftElbow", new Vector3(-68f, 0f, 0f)),
                ("RightElbow", new Vector3(-68f, 0f, 0f)),
                ("Torso", new Vector3(5f, 0f, 0f)));

            SetTargets(StickFigurePose.Set,
                ("LeftShoulder", new Vector3(-125f, 0f, 28f)),
                ("RightShoulder", new Vector3(-125f, 0f, -28f)),
                ("LeftElbow", new Vector3(-48f, 0f, 0f)),
                ("RightElbow", new Vector3(-48f, 0f, 0f)));

            SetTargets(StickFigurePose.SetSideLeft,
                ("LeftShoulder", new Vector3(-125f, 0f, 28f)),
                ("RightShoulder", new Vector3(-125f, 0f, -28f)),
                ("LeftElbow", new Vector3(-48f, 0f, 0f)),
                ("RightElbow", new Vector3(-48f, 0f, 0f)),
                ("Torso", new Vector3(0f, -8f, -3f)));

            SetTargets(StickFigurePose.SetSideRight,
                ("LeftShoulder", new Vector3(-125f, 0f, 28f)),
                ("RightShoulder", new Vector3(-125f, 0f, -28f)),
                ("LeftElbow", new Vector3(-48f, 0f, 0f)),
                ("RightElbow", new Vector3(-48f, 0f, 0f)),
                ("Torso", new Vector3(0f, 8f, 3f)));

            SetTargets(StickFigurePose.SetBack,
                ("LeftShoulder", new Vector3(-137f, 0f, 25f)),
                ("RightShoulder", new Vector3(-137f, 0f, -25f)),
                ("LeftElbow", new Vector3(-40f, 0f, 0f)),
                ("RightElbow", new Vector3(-40f, 0f, 0f)),
                ("Torso", new Vector3(12f, 0f, 0f)));

            SetTargets(StickFigurePose.SetOneHandLeft,
                ("LeftShoulder", new Vector3(-137f, 0f, 12f)),
                ("LeftElbow", new Vector3(-30f, 0f, 0f)),
                ("RightShoulder", new Vector3(15f, 0f, 18f)),
                ("RightElbow", new Vector3(-25f, 0f, 0f)),
                ("Torso", new Vector3(0f, -8f, -5f)));

            SetTargets(StickFigurePose.SetOneHandRight,
                ("RightShoulder", new Vector3(-137f, 0f, -12f)),
                ("RightElbow", new Vector3(-30f, 0f, 0f)),
                ("LeftShoulder", new Vector3(15f, 0f, -18f)),
                ("LeftElbow", new Vector3(-25f, 0f, 0f)),
                ("Torso", new Vector3(0f, 8f, 5f)));

            SetTargets(StickFigurePose.Approach,
                ("LeftShoulder", new Vector3(38f, 0f, -12f)),
                ("RightShoulder", new Vector3(38f, 0f, 12f)),
                ("LeftHip", new Vector3(-28f, 0f, 0f)),
                ("RightHip", new Vector3(28f, 0f, 0f)));

            SetTargets(StickFigurePose.SpikeWindup,
                ("LeftShoulder", new Vector3(-112f, 0f, 16f)),
                ("RightShoulder", new Vector3(-205f, 0f, -12f)),
                ("LeftElbow", new Vector3(-42f, 0f, 0f)),
                ("RightElbow", new Vector3(-48f, 0f, 0f)),
                ("Torso", new Vector3(-6f, 0f, 8f)));

            SetTargets(StickFigurePose.Spike,
                ("LeftShoulder", new Vector3(-118f, 0f, 12f)),
                ("RightShoulder", new Vector3(-135f, 0f, -18f)),
                ("LeftElbow", new Vector3(-55f, 0f, 0f)),
                ("RightElbow", Vector3.zero),
                ("Torso", new Vector3(-12f, 0f, -6f)));

            SetTargets(StickFigurePose.Block,
                ("LeftShoulder", new Vector3(-155f, 0f, 25f)),
                ("RightShoulder", new Vector3(-155f, 0f, -25f)),
                ("LeftElbow", new Vector3(5f, 0f, 0f)),
                ("RightElbow", new Vector3(5f, 0f, 0f)));

            SetTargets(StickFigurePose.Landing,
                ("LeftShoulder", new Vector3(35f, 0f, -20f)),
                ("RightShoulder", new Vector3(35f, 0f, 20f)),
                ("LeftHip", new Vector3(32f, 0f, -10f)),
                ("RightHip", new Vector3(32f, 0f, 10f)),
                ("LeftKnee", new Vector3(-58f, 0f, 0f)),
                ("RightKnee", new Vector3(-58f, 0f, 0f)));

            SetTargets(StickFigurePose.Celebrate,
                ("LeftShoulder", new Vector3(-155f, 0f, -28f)),
                ("RightShoulder", new Vector3(-155f, 0f, 28f)),
                ("LeftElbow", new Vector3(-12f, 0f, 0f)),
                ("RightElbow", new Vector3(-12f, 0f, 0f)),
                ("LeftHip", new Vector3(-12f, 0f, -8f)),
                ("RightHip", new Vector3(-12f, 0f, 8f)));
        }

        private void SetTargets(StickFigurePose pose, params (string Joint, Vector3 Rotation)[] targets)
        {
            foreach (var target in targets)
            {
                _poses[pose][target.Joint] = target.Rotation;
            }
        }
    }
}
