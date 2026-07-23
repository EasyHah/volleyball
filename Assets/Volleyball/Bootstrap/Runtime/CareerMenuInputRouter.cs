using System;
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
        private InputAction _pageLeft;
        private InputAction _pageRight;
        private UIDocument _document;
        private CareerUiSessionController _controller;

        public void Initialize(
            InputActionAsset actions,
            UIDocument document,
            CareerUiSessionController controller)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            _document = document != null
                ? document
                : throw new ArgumentNullException(nameof(document));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            var map = actions.FindActionMap("CareerMenu", true);
            _back = map.FindAction("Back", true);
            _cancel = map.FindAction("Cancel", true);
            _pageLeft = map.FindAction("PageLeft", true);
            _pageRight = map.FindAction("PageRight", true);
            _back.performed += OnBack;
            _cancel.performed += OnBack;
            _pageLeft.performed += OnPageLeft;
            _pageRight.performed += OnPageRight;
            _back.Enable();
            _cancel.Enable();
            _pageLeft.Enable();
            _pageRight.Enable();
        }

        private void OnDestroy()
        {
            Unsubscribe(_back, OnBack);
            Unsubscribe(_cancel, OnBack);
            Unsubscribe(_pageLeft, OnPageLeft);
            Unsubscribe(_pageRight, OnPageRight);
        }

        private void OnBack(InputAction.CallbackContext context)
        {
            if (IsEditingText())
            {
                return;
            }

            _controller?.Back();
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
