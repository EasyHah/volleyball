using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private const string PlayerUpdatesInEditModeFeature =
            "RUN_PLAYER_UPDATES_IN_EDIT_MODE";

        private static readonly string[] FrozenInputIds =
        {
            "75ac25e7-a0c1-4ac8-bb1c-c5f60590de13",
            "fa903345-2208-4499-99ba-ea051ae4e24a",
            "327436b6-c55a-484a-8601-252598e08e83",
            "fa40f24b-76ad-4a63-82d7-a63b76bc4850",
            "6c1734e9-23b5-4a60-b393-1bd5bedc03ba",
            "5271325d-7709-4df1-a280-698668174a51",
            "28672dd7-a7f1-480d-b425-952c6aedc9dc",
            "0de4e435-c595-40aa-babe-c676d9533ef7",
            "2aded20c-82be-4d9f-9096-51028703e43d",
            "ce617216-4e27-4f5b-b4c5-26d44740c46c",
            "f97eaa29-ab4e-45e6-bcbc-65cdd68482d4",
            "d2979326-f3d6-4a5a-95b8-c95ebafd533f",
            "9707decb-a747-4de1-900b-bd9c34637c27",
            "deef8a6f-f0b7-485e-a212-7373af16ebac",
            "2e66ae07-71f9-48d2-98a7-acf120b924f7",
            "aa115987-2de8-45ac-8384-c576dfe2837a",
            "c056576d-8978-48d9-9b3e-e35f2b33f459",
            "bdc656e9-f096-4ef3-a12e-df3111c8acf0",
            "824bb25b-8a26-4d4d-8b17-e45bfed31a6b",
            "45effc5c-e070-490a-85c2-41cd36dd7919",
            "a0408a6f-cc11-43c0-af30-526571f9d7f2",
            "c7a86bd9-1989-4df3-b47d-f2d65b3496d8",
            "5b38cce4-4717-4224-b2f5-b836aa9c5ebd",
            "4e6bbe96-5774-439a-b3ec-ab48e301a005",
            "65b9368b-2f65-4013-8bd0-21aac5a13d6a",
            "ac01411d-744a-47ca-a23a-713cf9b75fc1",
            "3f0f8f91-e576-454f-9301-78e113541876",
            "e97d9ea4-5c2a-4d69-952e-6f866167df3c",
            "c36ce87b-a652-4794-b764-ad2fc726cdfe",
            "b7a43a40-e309-4aec-b963-5ebea648e80a",
            "c7df8156-4adb-4a3f-b72d-ebba53585b60",
            "97944980-f904-4f92-9e8a-2edbfdbd2817",
            "1a04cc30-e14c-448c-bb91-61d24839f9dc",
            "5ac6e52c-337b-4973-bc32-6ea2c39e27f6",
            "0fd32bec-68fd-42cf-aedf-d98e25127c04",
            "954635fb-7ff6-4ccd-b520-d607110ca30e",
            "8b87f0e1-c510-42ef-ad0b-12aca7ab53ed",
            "6152f4f7-9df5-4831-99d7-36d1be219b28",
            "0debf6f6-02dd-4d06-b05d-41a715c5556f"
        };

        private readonly List<InputDevice> _addedDevices = new List<InputDevice>();
        private InputActionMap _trackedMap;
        private bool _mapStateCaptured;
        private bool _mapWasEnabled;
        private bool _featureStateCaptured;
        private bool _featureWasEnabled;

        [TearDown]
        public void TearDown()
        {
            if (_mapStateCaptured && _trackedMap != null)
            {
                if (_mapWasEnabled)
                {
                    _trackedMap.Enable();
                }
                else
                {
                    _trackedMap.Disable();
                }
            }

            _trackedMap = null;
            _mapStateCaptured = false;

            for (var index = _addedDevices.Count - 1; index >= 0; index--)
            {
                var device = _addedDevices[index];
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            _addedDevices.Clear();
            if (_featureStateCaptured)
            {
                InputSystem.settings.SetInternalFeatureFlag(
                    PlayerUpdatesInEditModeFeature,
                    _featureWasEnabled);
            }

            _featureStateCaptured = false;

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
            AssertAction(map, "Point", InputActionType.PassThrough, "Vector2");
            AssertAction(map, "Click", InputActionType.PassThrough, "Button");
            AssertAction(map, "ScrollWheel", InputActionType.PassThrough, "Vector2");
            Assert.That(
                map.actions.Select(action => action.name),
                Is.EqualTo(new[]
                {
                    "Navigate", "Submit", "Cancel", "Back", "PageLeft", "PageRight",
                    "Point", "Click", "ScrollWheel"
                }));
            var actualIds = new[] { map.id }
                .Concat(map.actions.Select(action => action.id))
                .Concat(map.bindings.Select(binding => binding.id))
                .Select(id => id.ToString("D"))
                .ToArray();
            Assert.That(actualIds, Is.EqualTo(FrozenInputIds));
            Assert.That(actualIds, Has.None.EqualTo(Guid.Empty.ToString("D")));
            Assert.That(actualIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(actualIds.Length));
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
            AssertBindings(map, "Point", "KeyboardMouse", "<Mouse>/position");
            AssertBindings(map, "Click", "KeyboardMouse", "<Mouse>/leftButton");
            AssertBindings(map, "ScrollWheel", "KeyboardMouse", "<Mouse>/scroll");
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
            _featureWasEnabled = IsFeatureEnabled(PlayerUpdatesInEditModeFeature);
            _featureStateCaptured = true;
            InputSystem.settings.SetInternalFeatureFlag(
                PlayerUpdatesInEditModeFeature,
                true);
            _trackedMap = map;
            _mapWasEnabled = map.enabled;
            _mapStateCaptured = true;
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

        private static bool IsFeatureEnabled(string featureName)
        {
            var method = typeof(InputSettings).GetMethod(
                "IsFeatureEnabled",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(
                    typeof(InputSettings).FullName,
                    "IsFeatureEnabled");
            }

            return (bool)method.Invoke(
                InputSystem.settings,
                new object[] { featureName });
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
