using System;
using UnityEngine;

namespace Volleyball.Presentation
{
    public enum RallyCameraView
    {
        Tactical = 1,
        Sideline = 2,
        BallFollow = 3
    }

    public sealed class RallyCameraController : MonoBehaviour
    {
        private SimulatedBall _ball;
        private Camera _camera;

        public RallyCameraView CurrentView { get; private set; } = RallyCameraView.Tactical;

        public int ViewSwitchCount { get; private set; }

        public void Initialize(SimulatedBall ball)
        {
            _ball = ball != null ? ball : throw new ArgumentNullException(nameof(ball));
            _camera = Camera.main;
            if (_camera == null)
            {
                throw new InvalidOperationException("The rally scene requires a main camera.");
            }

            SetView(RallyCameraView.Tactical);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetView(RallyCameraView.Tactical);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetView(RallyCameraView.Sideline);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetView(RallyCameraView.BallFollow);
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                var next = CurrentView == RallyCameraView.BallFollow
                    ? RallyCameraView.Tactical
                    : (RallyCameraView)((int)CurrentView + 1);
                SetView(next);
            }
        }

        private void LateUpdate()
        {
            if (CurrentView != RallyCameraView.BallFollow || _ball == null || _camera == null)
            {
                return;
            }

            var target = _ball.transform.position;
            var desired = target + new Vector3(5.8f, 3.2f, -5.8f);
            _camera.transform.position = Vector3.Lerp(
                _camera.transform.position,
                desired,
                1f - Mathf.Exp(-8f * Time.deltaTime));
            _camera.transform.LookAt(target + (Vector3.up * 0.25f));
        }

        public void SetView(RallyCameraView view)
        {
            if (!Enum.IsDefined(typeof(RallyCameraView), view))
            {
                throw new ArgumentOutOfRangeException(nameof(view));
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            CurrentView = view;
            ViewSwitchCount++;
            switch (view)
            {
                case RallyCameraView.Tactical:
                    _camera.orthographic = true;
                    _camera.orthographicSize = 12f;
                    _camera.transform.SetPositionAndRotation(
                        new Vector3(0f, 18f, -15f),
                        Quaternion.Euler(54f, 0f, 0f));
                    break;
                case RallyCameraView.Sideline:
                    _camera.orthographic = false;
                    _camera.fieldOfView = 44f;
                    _camera.transform.position = new Vector3(14.5f, 6.2f, -0.5f);
                    _camera.transform.LookAt(new Vector3(0f, 1.7f, 0f));
                    break;
                case RallyCameraView.BallFollow:
                    _camera.orthographic = false;
                    _camera.fieldOfView = 50f;
                    _camera.transform.position =
                        _ball.transform.position + new Vector3(5.8f, 3.2f, -5.8f);
                    _camera.transform.LookAt(_ball.transform.position + (Vector3.up * 0.25f));
                    break;
            }
        }

        private void OnGUI()
        {
            var width = 330f;
            var left = Screen.width - width - 18f;
            GUI.Box(new Rect(left, 18f, width, 72f), string.Empty);
            GUI.Label(
                new Rect(left + 16f, 28f, width - 28f, 24f),
                $"CAMERA: {CurrentView.ToString().ToUpperInvariant()}");
            GUI.Label(
                new Rect(left + 16f, 54f, width - 28f, 24f),
                "[1] Tactical  [2] Sideline  [3] Follow  [C] Cycle");
        }
    }
}
