using System;
using UnityEngine;
using UnityEngine.Rendering;
using Volleyball.Domain.Prototype;

namespace Volleyball.Presentation
{
    public sealed class BlockImpactFeedback : MonoBehaviour
    {
        private const float EffectDuration = 0.48f;
        private const int RingSegmentCount = 32;

        private static readonly Color BlueAccent = new Color(0.12f, 0.68f, 1f, 1f);
        private static readonly Color OrangeAccent = new Color(1f, 0.38f, 0.08f, 1f);

        private TrailRenderer _ballTrail;
        private Transform _core;
        private MeshRenderer _coreRenderer;
        private LineRenderer _ring;
        private Light _flash;
        private Material _coreMaterial;
        private Material _ringMaterial;
        private Color _baseTrailStartColor;
        private Color _baseTrailEndColor;
        private float _baseTrailStartWidth;
        private float _baseTrailEndWidth;
        private float _elapsed;
        private float _intensity;

        public int PlayedCount { get; private set; }

        public bool IsPlaying { get; private set; }

        public TeamId LastBlockingTeam { get; private set; }

        public Vector3 LastImpactPoint { get; private set; }

        public float LastReboundSpeed { get; private set; }

        public Color CurrentAccentColor { get; private set; }

        public int VisibleElementCount => (_coreRenderer != null ? 1 : 0) +
                                          (_ring != null ? 1 : 0) +
                                          (_flash != null ? 1 : 0);

        public static BlockImpactFeedback Create(Transform parent, TrailRenderer ballTrail)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var effectObject = new GameObject("BlockImpactFeedback");
            effectObject.transform.SetParent(parent, false);
            var feedback = effectObject.AddComponent<BlockImpactFeedback>();
            feedback.Initialize(ballTrail);
            return feedback;
        }

        public void Play(
            TeamId blockingTeam,
            Vector3 impactPoint,
            Vector3 surfaceNormal,
            float reboundSpeed)
        {
            if (!Enum.IsDefined(typeof(TeamId), blockingTeam))
            {
                throw new ArgumentOutOfRangeException(nameof(blockingTeam));
            }

            if (!IsFinite(impactPoint))
            {
                throw new ArgumentOutOfRangeException(nameof(impactPoint));
            }

            if (!IsFinite(surfaceNormal) || surfaceNormal.sqrMagnitude <= 0.000001f)
            {
                throw new ArgumentOutOfRangeException(nameof(surfaceNormal));
            }

            if (!IsFinite(reboundSpeed) || reboundSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reboundSpeed));
            }

            LastBlockingTeam = blockingTeam;
            LastImpactPoint = impactPoint;
            LastReboundSpeed = reboundSpeed;
            CurrentAccentColor = blockingTeam == TeamId.Blue ? BlueAccent : OrangeAccent;
            PlayedCount++;

            transform.position = impactPoint;
            var normal = surfaceNormal.normalized;
            var stableUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            transform.rotation = Quaternion.LookRotation(normal, stableUp);
            _elapsed = 0f;
            _intensity = Mathf.Lerp(0.68f, 1f, Mathf.Clamp01(reboundSpeed / 18f));
            IsPlaying = true;
            enabled = true;
            _coreRenderer.enabled = true;
            _ring.enabled = true;
            _flash.enabled = true;
            RenderFrame(0f);
        }

        private void Initialize(TrailRenderer ballTrail)
        {
            _ballTrail = ballTrail;
            if (_ballTrail != null)
            {
                _baseTrailStartColor = _ballTrail.startColor;
                _baseTrailEndColor = _ballTrail.endColor;
                _baseTrailStartWidth = _ballTrail.startWidth;
                _baseTrailEndWidth = _ballTrail.endWidth;
            }

            CreateCore();
            CreateRing();
            CreateFlash();
            _coreRenderer.enabled = false;
            _ring.enabled = false;
            _flash.enabled = false;
            enabled = false;
        }

        private void CreateCore()
        {
            var coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreObject.name = "BlockImpactCore";
            coreObject.transform.SetParent(transform, false);
            var collider = coreObject.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyOwnedObject(collider);
            }

            _core = coreObject.transform;
            _coreRenderer = coreObject.GetComponent<MeshRenderer>();
            _coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _coreRenderer.receiveShadows = false;
            _coreMaterial = CreateMaterial("BlockImpactCoreMaterial");
            _coreRenderer.sharedMaterial = _coreMaterial;
        }

        private void CreateRing()
        {
            var ringObject = new GameObject("BlockImpactRing");
            ringObject.transform.SetParent(transform, false);
            _ring = ringObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = RingSegmentCount;
            _ring.numCornerVertices = 3;
            _ring.numCapVertices = 2;
            _ring.alignment = LineAlignment.TransformZ;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ringMaterial = CreateMaterial("BlockImpactRingMaterial");
            _ring.sharedMaterial = _ringMaterial;
        }

        private void CreateFlash()
        {
            var flashObject = new GameObject("BlockImpactFlash");
            flashObject.transform.SetParent(transform, false);
            _flash = flashObject.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.range = 2.8f;
            _flash.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(_elapsed / EffectDuration);
            RenderFrame(progress);
            if (progress >= 1f)
            {
                StopEffect();
            }
        }

        private void RenderFrame(float progress)
        {
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            var visibility = 1f - Mathf.SmoothStep(0.12f, 1f, progress);
            var radius = Mathf.Lerp(0.16f, 1.25f, eased) * _intensity;
            for (var index = 0; index < RingSegmentCount; index++)
            {
                var angle = index * (Mathf.PI * 2f / RingSegmentCount);
                _ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            var ringColor = WithAlpha(CurrentAccentColor, visibility * 0.95f);
            _ring.startColor = ringColor;
            _ring.endColor = ringColor;
            _ring.startWidth = Mathf.Lerp(0.11f, 0.015f, progress) * _intensity;
            _ring.endWidth = _ring.startWidth;

            var coreScale = Mathf.Lerp(0.34f, 0.90f, eased) * _intensity;
            _core.localScale = Vector3.one * coreScale;
            _coreMaterial.color = WithAlpha(CurrentAccentColor, visibility * 0.82f);
            _flash.color = CurrentAccentColor;
            _flash.intensity = 4.5f * _intensity * visibility * visibility;

            if (_ballTrail == null)
            {
                return;
            }

            var pulse = visibility * _intensity;
            var pulseStartWidth = Mathf.Max(_baseTrailStartWidth * 2.1f, 0.12f);
            var pulseEndWidth = Mathf.Max(_baseTrailEndWidth * 2f, 0.025f);
            _ballTrail.startWidth = Mathf.Lerp(_baseTrailStartWidth, pulseStartWidth, pulse);
            _ballTrail.endWidth = Mathf.Lerp(_baseTrailEndWidth, pulseEndWidth, pulse);
            _ballTrail.startColor = Color.Lerp(
                _baseTrailStartColor,
                WithAlpha(CurrentAccentColor, 0.96f),
                pulse);
            _ballTrail.endColor = Color.Lerp(
                _baseTrailEndColor,
                WithAlpha(CurrentAccentColor, 0f),
                pulse);
        }

        private void StopEffect()
        {
            IsPlaying = false;
            _coreRenderer.enabled = false;
            _ring.enabled = false;
            _flash.enabled = false;
            RestoreTrail();
            enabled = false;
        }

        private void OnDisable()
        {
            if (!IsPlaying)
            {
                RestoreTrail();
            }
        }

        private void OnDestroy()
        {
            RestoreTrail();
            DestroyOwnedObject(_coreMaterial);
            DestroyOwnedObject(_ringMaterial);
        }

        private void RestoreTrail()
        {
            if (_ballTrail == null)
            {
                return;
            }

            _ballTrail.startWidth = _baseTrailStartWidth;
            _ballTrail.endWidth = _baseTrailEndWidth;
            _ballTrail.startColor = _baseTrailStartColor;
            _ballTrail.endColor = _baseTrailEndColor;
        }

        private static Material CreateMaterial(string materialName)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("A built-in unlit shader is required for block impact feedback.");
            }

            return new Material(shader)
            {
                name = materialName
            };
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
