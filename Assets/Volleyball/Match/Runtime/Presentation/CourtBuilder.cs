using System;
using UnityEngine;
using Volleyball.Match.Domain.PreServe;

namespace Volleyball.Presentation
{
    public static class CourtBuilder
    {
        public const float HalfWidth = FormalCourtGeometryV1.HalfWidthMeters;
        public const float HalfLength = 7.5f;
        public const float FormalHalfLength = FormalCourtGeometryV1.HalfLengthMeters;
        public const float NetHeight = FormalCourtGeometryV1.NetHeightMeters;

        private static readonly Color FloorColor = new Color(0.36f, 0.76f, 0.94f);
        private static readonly Color CourtColor = new Color(0.93f, 0.71f, 0.4f);

        public static Transform Build(Transform parent, float halfLength = HalfLength)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (float.IsNaN(halfLength) || float.IsInfinity(halfLength) || halfLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfLength));
            }

            var root = new GameObject("Court").transform;
            root.SetParent(parent, false);
            CreateBox(root, "Floor", Vector3.zero, new Vector3(HalfWidth * 2f + 2f, 0.2f, halfLength * 2f + 2f), FloorColor);
            CreateBox(root, "PlayingSurface", Vector3.up * 0.11f, new Vector3(HalfWidth * 2f, 0.04f, halfLength * 2f), CourtColor);
            CreateNet(root);
            CreateLines(root, halfLength);
            CreateLight(root);
            CreateCamera(root);
            return root;
        }

        private static Transform CreateBox(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            RemoveCollider(box);
            SetColor(box.GetComponent<Renderer>(), color);
            return box.transform;
        }

        private static void CreateNet(Transform parent)
        {
            var net = new GameObject("Net").transform;
            net.SetParent(parent, false);
            CreateBox(net, "LeftPost", new Vector3(-HalfWidth - 0.15f, NetHeight * 0.5f, 0f), new Vector3(0.16f, NetHeight, 0.16f), Color.white);
            CreateBox(net, "RightPost", new Vector3(HalfWidth + 0.15f, NetHeight * 0.5f, 0f), new Vector3(0.16f, NetHeight, 0.16f), Color.white);
            CreateBox(net, "TopTape", new Vector3(0f, NetHeight, 0f), new Vector3(HalfWidth * 2f, 0.09f, 0.08f), Color.white);

            var gridColor = new Color(0.92f, 0.96f, 1f);
            for (var row = 0; row < 5; row++)
            {
                var y = 1.15f + (row * 0.25f);
                CreateBox(net, "HorizontalCord" + row, new Vector3(0f, y, 0f), new Vector3(HalfWidth * 2f, 0.025f, 0.025f), gridColor);
            }

            for (var column = -9; column <= 9; column++)
            {
                CreateBox(net, "VerticalCord" + (column + 9), new Vector3(column * 0.48f, 1.75f, 0f), new Vector3(0.025f, 1.2f, 0.025f), gridColor);
            }
        }

        private static void CreateLines(Transform parent, float halfLength)
        {
            const float lineHeight = 0.145f;
            const float lineThickness = 0.08f;
            CreateBox(parent, "LeftSideline", new Vector3(-HalfWidth, lineHeight, 0f), new Vector3(lineThickness, 0.025f, halfLength * 2f), Color.white);
            CreateBox(parent, "RightSideline", new Vector3(HalfWidth, lineHeight, 0f), new Vector3(lineThickness, 0.025f, halfLength * 2f), Color.white);
            CreateBox(parent, "BlueEndLine", new Vector3(0f, lineHeight, -halfLength), new Vector3(HalfWidth * 2f, 0.025f, lineThickness), Color.white);
            CreateBox(parent, "OrangeEndLine", new Vector3(0f, lineHeight, halfLength), new Vector3(HalfWidth * 2f, 0.025f, lineThickness), Color.white);
            CreateBox(parent, "CenterLine", new Vector3(0f, lineHeight, 0f), new Vector3(HalfWidth * 2f, 0.025f, lineThickness), Color.white);
            CreateBox(parent, "BlueAttackLine", new Vector3(0f, lineHeight, -3f), new Vector3(HalfWidth * 2f, 0.025f, lineThickness), Color.white);
            CreateBox(parent, "OrangeAttackLine", new Vector3(0f, lineHeight, 3f), new Vector3(HalfWidth * 2f, 0.025f, lineThickness), Color.white);
        }

        private static void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("GymLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
        }

        private static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("TacticalCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.SetLocalPositionAndRotation(
                new Vector3(0f, 16f, -13f),
                Quaternion.Euler(52f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.65f, 0.88f, 1f);
            camera.tag = "MainCamera";
        }

        private static void SetColor(Renderer renderer, Color color)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(collider);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
    }
}
