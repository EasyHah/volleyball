using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Volleyball.Career.Presentation;

namespace Volleyball.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class CareerMenuInputRouter : MonoBehaviour
    {
        private InputAction _back;
        private InputAction _cancel;
        private InputAction _submit;
        private InputAction _pageLeft;
        private InputAction _pageRight;
        private UIDocument _document;
        private CareerUiSessionController _controller;
        private bool _initialized;
        private bool _keyboardCallbacksRegistered;
        private readonly HashSet<KeyCode> _heldSubmitKeys = new HashSet<KeyCode>();

        public void Initialize(
            InputActionAsset actions,
            UIDocument document,
            CareerUiSessionController controller)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (_initialized)
            {
                throw new InvalidOperationException(
                    "Career menu input can only be initialized once.");
            }

            _document = document != null
                ? document
                : throw new ArgumentNullException(nameof(document));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            var map = actions.FindActionMap("CareerMenu", true);
            _back = map.FindAction("Back", true);
            _cancel = map.FindAction("Cancel", true);
            _submit = map.FindAction("Submit", true);
            _pageLeft = map.FindAction("PageLeft", true);
            _pageRight = map.FindAction("PageRight", true);
            _back.performed += OnBack;
            _cancel.performed += OnBack;
            _submit.performed += OnSubmit;
            _pageLeft.performed += OnPageLeft;
            _pageRight.performed += OnPageRight;
            _initialized = true;
            SetInputEnabled(isActiveAndEnabled);
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                SetInputEnabled(true);
            }
        }

        private void OnDisable()
        {
            if (_initialized)
            {
                SetInputEnabled(false);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe(_back, OnBack);
            Unsubscribe(_cancel, OnBack);
            Unsubscribe(_submit, OnSubmit);
            Unsubscribe(_pageLeft, OnPageLeft);
            Unsubscribe(_pageRight, OnPageRight);
            SetKeyboardCallbacksEnabled(false);
            _initialized = false;
        }

        private void OnBack(InputAction.CallbackContext context)
        {
            if (IsEditingText())
            {
                return;
            }

            _controller?.Back();
        }

        private void OnSubmit(InputAction.CallbackContext context)
        {
            if (context.control?.device is Keyboard)
            {
                return;
            }

            SubmitFocusedElement();
        }

        private void OnKeyDown(KeyDownEvent keyDown)
        {
            if (!isActiveAndEnabled || keyDown == null ||
                !IsSubmitKey(keyDown.keyCode))
            {
                return;
            }

            if (IsEditingText())
            {
                return;
            }

            if (_heldSubmitKeys.Add(keyDown.keyCode))
            {
                SubmitFocusedElement();
            }

            keyDown.StopImmediatePropagation();
            keyDown.PreventDefault();
        }

        private void OnKeyUp(KeyUpEvent keyUp)
        {
            if (keyUp != null && IsSubmitKey(keyUp.keyCode))
            {
                _heldSubmitKeys.Remove(keyUp.keyCode);
            }
        }

        private void SubmitFocusedElement()
        {
            if (IsEditingText())
            {
                return;
            }

            var focused = _document?.rootVisualElement?.focusController?.focusedElement
                as VisualElement;
            if (focused == null || !focused.enabledInHierarchy)
            {
                return;
            }

            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = focused;
                focused.SendEvent(submit);
            }
        }

        private void OnPageLeft(InputAction.CallbackContext context) => ScrollPage(-1f);
        private void OnPageRight(InputAction.CallbackContext context) => ScrollPage(1f);

        private void ScrollPage(float direction)
        {
            var scroll = _document?.rootVisualElement?.Q<ScrollView>("route-scroll");
            if (scroll == null)
            {
                return;
            }

            var page = Mathf.Max(240f, scroll.contentViewport.resolvedStyle.height * 0.8f);
            scroll.scrollOffset = new Vector2(
                scroll.scrollOffset.x,
                Mathf.Max(0f, scroll.scrollOffset.y + direction * page));
        }

        private bool IsEditingText()
        {
            var focused = _document?.rootVisualElement?.focusController?.focusedElement
                as VisualElement;
            return focused is TextField || focused is IntegerField ||
                   focused?.GetFirstAncestorOfType<TextField>() != null ||
                   focused?.GetFirstAncestorOfType<IntegerField>() != null;
        }

        private void SetInputEnabled(bool enabled)
        {
            SetKeyboardCallbacksEnabled(enabled);
            SetEnabled(_back, enabled);
            SetEnabled(_cancel, enabled);
            SetEnabled(_submit, enabled);
            SetEnabled(_pageLeft, enabled);
            SetEnabled(_pageRight, enabled);
        }

        private void SetKeyboardCallbacksEnabled(bool enabled)
        {
            var root = _document?.rootVisualElement;
            if (root == null || enabled == _keyboardCallbacksRegistered)
            {
                if (!enabled)
                {
                    _heldSubmitKeys.Clear();
                }

                return;
            }

            if (enabled)
            {
                root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                root.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
            }
            else
            {
                root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                root.UnregisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
                _heldSubmitKeys.Clear();
            }

            _keyboardCallbacksRegistered = enabled;
        }

        private static bool IsSubmitKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.Return ||
                   keyCode == KeyCode.KeypadEnter ||
                   keyCode == KeyCode.Space;
        }

        private static void SetEnabled(InputAction action, bool enabled)
        {
            if (action == null)
            {
                return;
            }

            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        private static void Unsubscribe(
            InputAction action,
            Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
            {
                return;
            }

            action.performed -= callback;
            action.Disable();
        }
    }
}
