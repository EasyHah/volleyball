using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Volleyball.Presentation.TrainingLab
{
    // Camera-only observer. Keeping the controller out of this class enforces
    // that free 3D inspection cannot mutate the scenario draft.
    public sealed class TrainingLabFreeObservationPresenterV1
    {
        private readonly VisualElement _surface;
        private readonly Camera _camera;
        private int _dragPointer = -1;
        private bool _panning;
        private float _yaw;
        private float _pitch = 48f;
        private float _distance = 22f;
        private Vector3 _pivot;
        private RenderTexture _output;

        public TrainingLabFreeObservationPresenterV1(
            VisualElement surface,
            Camera camera)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _surface.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _surface.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _surface.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _surface.RegisterCallback<WheelEvent>(OnWheel);
        }

        public bool CameraChanged { get; private set; }

        public RenderTexture Output => _output;

        public void Activate()
        {
            EnsureOutput();
            var direction = _camera.transform.position - _pivot;
            _distance = Mathf.Clamp(direction.magnitude, 6f, 40f);
            var flattened = new Vector3(direction.x, 0f, direction.z);
            _yaw = Mathf.Atan2(flattened.x, -flattened.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(
                Mathf.Atan2(direction.y, flattened.magnitude) * Mathf.Rad2Deg,
                10f,
                80f);
            ApplyCamera();
        }

        public void Dispose()
        {
            if (_camera != null && _camera.targetTexture == _output)
                _camera.targetTexture = null;
            _surface.style.backgroundImage = StyleKeyword.None;
            if (_output == null) return;
            _output.Release();
            UnityEngine.Object.Destroy(_output);
            _output = null;
        }

        private void OnPointerDown(PointerDownEvent value)
        {
            _dragPointer = value.pointerId;
            _panning = value.button == 1 || value.button == 2;
            _surface.CapturePointer(value.pointerId);
            value.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent value)
        {
            if (_dragPointer != value.pointerId ||
                !_surface.HasPointerCapture(value.pointerId))
                return;
            if (_panning)
            {
                var scale = _distance * .004f;
                _pivot -= _camera.transform.right * value.deltaPosition.x * scale;
                _pivot -= _camera.transform.up * value.deltaPosition.y * scale;
            }
            else
            {
                _yaw += value.deltaPosition.x * .25f;
                _pitch = Mathf.Clamp(_pitch - value.deltaPosition.y * .25f,
                    10f, 80f);
            }
            CameraChanged = true;
            ApplyCamera();
            value.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent value)
        {
            if (_dragPointer != value.pointerId) return;
            if (_surface.HasPointerCapture(value.pointerId))
                _surface.ReleasePointer(value.pointerId);
            _dragPointer = -1;
            _panning = false;
            value.StopPropagation();
        }

        private void OnWheel(WheelEvent value)
        {
            _distance = Mathf.Clamp(_distance + value.delta.y * .02f,
                6f, 40f);
            CameraChanged = true;
            ApplyCamera();
            value.StopPropagation();
        }

        private void ApplyCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var position = _pivot +
                rotation * new Vector3(0f, 0f, -_distance);
            _camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(_pivot - position, Vector3.up));
            _camera.orthographic = false;
        }

        private void EnsureOutput()
        {
            if (_output == null)
            {
                _output = new RenderTexture(1280, 720, 24,
                    RenderTextureFormat.ARGB32)
                {
                    name = "TrainingLabFreeObservationV1"
                };
                _output.Create();
            }

            _camera.targetTexture = _output;
            _surface.style.backgroundImage =
                new StyleBackground(Background.FromRenderTexture(_output));
        }
    }
}
