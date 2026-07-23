using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMenuInputActionAssetTests
    {
        private const string AssetPath =
            "Assets/Volleyball/Career/Runtime/Presentation/Input/CareerMenu.inputactions";

        private readonly List<InputDevice> _addedDevices = new List<InputDevice>();
        private InputActionMap _enabledMap;
        private bool _playerUpdatesInEditModeEnabled;

        [TearDown]
        public void TearDown()
        {
            if (_enabledMap != null)
            {
                _enabledMap.Disable();
                _enabledMap = null;
            }

            for (var index = _addedDevices.Count - 1; index >= 0; index--)
            {
                var device = _addedDevices[index];
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            _addedDevices.Clear();
            if (_playerUpdatesInEditModeEnabled)
            {
                InputSystem.settings.SetInternalFeatureFlag(
                    "RUN_PLAYER_UPDATES_IN_EDIT_MODE",
                    false);
                _playerUpdatesInEditModeEnabled = false;
            }

            InputSystem.Update();
        }

        [Test]
        public void Asset_LocksMapActionsSchemesAndRequiredBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);

            Assert.That(asset, Is.Not.Null, AssetPath);
            Assert.That(asset.actionMaps.Select(map => map.name), Is.EqualTo(new[] { "CareerMenu" }));
            var map = asset.FindActionMap("CareerMenu", true);
            AssertAction(map, "Navigate", InputActionType.Value, "Vector2");
            AssertAction(map, "Submit", InputActionType.Button, "Button");
            AssertAction(map, "Cancel", InputActionType.Button, "Button");
            AssertAction(map, "Back", InputActionType.Button, "Button");
            AssertAction(map, "PageLeft", InputActionType.Button, "Button");
            AssertAction(map, "PageRight", InputActionType.Button, "Button");
            Assert.That(
                map.actions.Select(action => action.name),
                Is.EqualTo(new[]
                {
                    "Navigate", "Submit", "Cancel", "Back", "PageLeft", "PageRight"
                }));
            Assert.That(
                asset.controlSchemes.Select(scheme => scheme.name),
                Is.EqualTo(new[] { "KeyboardMouse", "Gamepad" }));
            var keyboardMouse = asset.controlSchemes.Single(
                scheme => scheme.name == "KeyboardMouse");
            Assert.That(keyboardMouse.bindingGroup, Is.EqualTo("KeyboardMouse"));
            Assert.That(
                keyboardMouse.deviceRequirements.Select(requirement => requirement.controlPath),
                Is.EqualTo(new[] { "<Keyboard>", "<Mouse>" }));
            Assert.That(
                keyboardMouse.deviceRequirements.Select(requirement => requirement.isOptional),
                Is.EqualTo(new[] { false, true }));
            Assert.That(
                keyboardMouse.deviceRequirements.Select(requirement => requirement.isOR),
                Is.EqualTo(new[] { false, false }));
            var gamepadScheme = asset.controlSchemes.Single(scheme => scheme.name == "Gamepad");
            Assert.That(gamepadScheme.bindingGroup, Is.EqualTo("Gamepad"));
            Assert.That(
                gamepadScheme.deviceRequirements.Select(requirement => requirement.controlPath),
                Is.EqualTo(new[] { "<Gamepad>" }));
            Assert.That(
                gamepadScheme.deviceRequirements.Select(requirement => requirement.isOptional),
                Is.EqualTo(new[] { false }));
            Assert.That(
                gamepadScheme.deviceRequirements.Select(requirement => requirement.isOR),
                Is.EqualTo(new[] { false }));

            AssertBindings(map, "Navigate", "KeyboardMouse",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d");
            AssertBindings(map, "Navigate", "Gamepad",
                "<Gamepad>/dpad", "<Gamepad>/leftStick");
            AssertBindings(map, "Submit", "KeyboardMouse",
                "<Keyboard>/enter", "<Keyboard>/space");
            AssertBindings(map, "Submit", "Gamepad", "<Gamepad>/buttonSouth");
            AssertBindings(map, "Cancel", "KeyboardMouse", "<Keyboard>/escape");
            AssertBindings(map, "Cancel", "Gamepad", "<Gamepad>/buttonEast");
            AssertBindings(map, "Back", "KeyboardMouse",
                "<Keyboard>/backspace", "<Mouse>/backButton");
            AssertBindings(map, "Back", "Gamepad", "<Gamepad>/select");
            AssertBindings(map, "PageLeft", "KeyboardMouse",
                "<Keyboard>/pageUp", "<Keyboard>/q");
            AssertBindings(map, "PageLeft", "Gamepad", "<Gamepad>/leftShoulder");
            AssertBindings(map, "PageRight", "KeyboardMouse",
                "<Keyboard>/pageDown", "<Keyboard>/e");
            AssertBindings(map, "PageRight", "Gamepad", "<Gamepad>/rightShoulder");
        }

        [Test]
        public void RealKeyboardAndGamepadState_DrivesLinearNavigateAndSubmit()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, AssetPath);
            var map = asset.FindActionMap("CareerMenu", true);
            var navigate = map.FindAction("Navigate", true);
            var submit = map.FindAction("Submit", true);
            var keyboard = AddDevice<Keyboard>();
            var gamepad = AddDevice<Gamepad>();
            InputSystem.settings.SetInternalFeatureFlag(
                "RUN_PLAYER_UPDATES_IN_EDIT_MODE",
                true);
            _playerUpdatesInEditModeEnabled = true;
            _enabledMap = map;
            map.Enable();

            var focusIndex = 1;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            InputSystem.Update();
            Assert.That(keyboard.enabled, Is.True, "The test keyboard must be enabled.");
            Assert.That(
                keyboard.downArrowKey.isPressed,
                Is.True,
                "The queued keyboard state must reach the test device.");
            Assert.That(
                navigate.ReadValue<Vector2>(),
                Is.EqualTo(Vector2.down),
                "The imported Navigate action must consume the queued keyboard state. " +
                "enabled=" + navigate.enabled +
                ", phase=" + navigate.phase +
                ", controls=" + string.Join(
                    ", ",
                    navigate.controls.Select(
                        control => control.device.deviceId + ":" + control.path)));
            focusIndex = MoveLinear(focusIndex, navigate.ReadValue<Vector2>(), 3);
            Assert.That(focusIndex, Is.EqualTo(2));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            InputSystem.Update();
            Assert.That(submit.IsPressed(), Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(
                gamepad,
                new GamepadState().WithButton(GamepadButton.DpadLeft));
            InputSystem.Update();
            focusIndex = MoveLinear(focusIndex, navigate.ReadValue<Vector2>(), 3);
            Assert.That(focusIndex, Is.EqualTo(1));

            InputSystem.QueueStateEvent(
                gamepad,
                new GamepadState().WithButton(GamepadButton.South));
            InputSystem.Update();
            Assert.That(submit.IsPressed(), Is.True);
        }

        [Test]
        public void Repository_LocksPackageAndBothInputHandling()
        {
            var root = Directory.GetCurrentDirectory();
            var manifest = File.ReadAllText(Path.Combine(root, "Packages", "manifest.json"));
            var packageLock = File.ReadAllText(Path.Combine(root, "Packages", "packages-lock.json"));
            var projectSettings = File.ReadAllText(
                Path.Combine(root, "ProjectSettings", "ProjectSettings.asset"));

            StringAssert.Contains("\"com.unity.inputsystem\": \"1.17.0\"", manifest);
            StringAssert.Contains("\"com.unity.inputsystem\"", packageLock);
            StringAssert.Contains("\"version\": \"1.17.0\"", packageLock);
            StringAssert.Contains("activeInputHandler: 2", projectSettings);
        }

        private T AddDevice<T>() where T : InputDevice
        {
            var device = InputSystem.AddDevice<T>();
            _addedDevices.Add(device);
            return device;
        }

        private static int MoveLinear(int current, Vector2 direction, int count)
        {
            if (direction.y < -0.5f || direction.x > 0.5f)
            {
                return Math.Min(count - 1, current + 1);
            }

            if (direction.y > 0.5f || direction.x < -0.5f)
            {
                return Math.Max(0, current - 1);
            }

            return current;
        }

        private static void AssertAction(
            InputActionMap map,
            string name,
            InputActionType type,
            string controlType)
        {
            var action = map.FindAction(name, true);
            Assert.That(action.type, Is.EqualTo(type), name);
            Assert.That(action.expectedControlType, Is.EqualTo(controlType), name);
        }

        private static void AssertBindings(
            InputActionMap map,
            string actionName,
            string group,
            params string[] requiredPaths)
        {
            var action = map.FindAction(actionName, true);
            var actual = action.bindings
                .Where(binding => !binding.isComposite &&
                                  !string.IsNullOrEmpty(binding.path) &&
                                  BindingHasGroup(binding.groups, group))
                .Select(binding => binding.path)
                .ToArray();
            foreach (var path in requiredPaths)
            {
                Assert.That(actual, Does.Contain(path), actionName + " / " + group);
            }
        }

        private static bool BindingHasGroup(string groups, string required)
        {
            return (groups ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Contains(required);
        }
    }
}
