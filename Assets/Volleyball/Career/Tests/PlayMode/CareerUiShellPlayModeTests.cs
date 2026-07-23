using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Volleyball.Bootstrap;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Presentation;

namespace Volleyball.Career.PlayModeTests
{
    public sealed class CareerUiShellPlayModeTests
    {
        private const string PanelPath =
            "Assets/Volleyball/Career/Runtime/Presentation/CareerPanelSettings.asset";
        private const string ShellPath =
            "Assets/Volleyball/Career/Runtime/Presentation/CareerShell.uxml";
        private const string InputPath =
            "Assets/Volleyball/Career/Runtime/Presentation/Input/CareerMenuRuntime.asset";

        [UnityTest]
        public IEnumerator ShellRendersAtTwoSizesAndRouterEnablesMenuActions()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            var rootObject = new GameObject("Career UI PlayMode Test");
            rootObject.SetActive(false);
            try
            {
                var document = rootObject.AddComponent<UIDocument>();
                document.panelSettings = Required<PanelSettings>(PanelPath);
                document.visualTreeAsset = Required<VisualTreeAsset>(ShellPath);
                var shell = rootObject.AddComponent<CareerUiShell>();
                var useCases = new ShellUseCases();
                var controller = new CareerUiSessionController(useCases);
                rootObject.SetActive(true);
                shell.Bind(controller);
                controller.Initialize();
                Assert.That(controller.SelectProfile(ShellUseCases.TestProfileId), Is.True);

                var actions = Required<InputActionAsset>(InputPath);
                var map = actions.FindActionMap("CareerMenu", true);
                var router = rootObject.AddComponent<CareerMenuInputRouter>();
                router.Initialize(actions, document, controller);
                yield return null;

                Assert.That(map.FindAction("Back", true).enabled, Is.True);
                Assert.That(map.FindAction("Cancel", true).enabled, Is.True);
                Assert.That(map.FindAction("PageLeft", true).enabled, Is.True);
                Assert.That(map.FindAction("PageRight", true).enabled, Is.True);

                AssertShell(document, "职业生涯");
                Screen.SetResolution(1280, 720, false);
                yield return null;
                AssertShell(document, "职业生涯");
                Screen.SetResolution(1920, 1080, false);
                yield return null;
                AssertShell(document, "职业生涯");

                Assert.That(controller.Back(), Is.True);
                yield return null;
                Assert.That(controller.Route, Is.EqualTo(CareerUiRoute.ProfileHub));
                Assert.That(document.rootVisualElement.Q<Label>("route-title").text,
                    Is.EqualTo("本地档案"));
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, false);
                UnityEngine.Object.Destroy(rootObject);
            }
        }

        private static void AssertShell(UIDocument document, string expectedRoute)
        {
            var root = document.rootVisualElement;
            Assert.That(root.Q<Label>("route-title").text, Is.EqualTo(expectedRoute));
            Assert.That(root.Q<VisualElement>("route-content").childCount, Is.GreaterThan(0));
            Assert.That(root.Q<Button>("back-button").enabledSelf, Is.True);
            Assert.That(root.layout.width, Is.GreaterThan(0f));
            Assert.That(root.layout.height, Is.GreaterThan(0f));
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private sealed class ShellUseCases : ICareerUiUseCases
        {
            public static readonly ProfileId TestProfileId = new ProfileId(
                Guid.Parse("11111111-1111-4111-8111-111111111111"));

            private static readonly LocalPlayerProfile Profile = new LocalPlayerProfile(
                LocalPlayerProfile.CurrentSchemaVersion,
                TestProfileId,
                1,
                new Sha256Digest(new string('a', 64)),
                "测试档案",
                0,
                0,
                Array.Empty<CareerIndexEntry>());

            public CareerUiUseCaseResult LoadProfiles() =>
                CareerUiUseCaseResult.ForProfiles(Array.Empty<LocalProfileCatalogEntry>());

            public CareerUiUseCaseResult LoadRecentCareer() =>
                CareerUiUseCaseResult.Failure("no_recent_career");

            public void ClearRecentCareer()
            {
            }

            public CareerUiUseCaseResult CreateProfile(string displayName) =>
                CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult LoadProfile(ProfileId profileId) =>
                CareerUiUseCaseResult.ForProfile(Profile);

            public CareerUiUseCaseResult LoadCareer(ProfileId profileId, SaveId saveId) =>
                CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult RecoverCareer(ProfileId profileId, SaveId saveId) =>
                CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult CreateCareer(
                ProfileId profileId,
                string careerName,
                string playerName,
                int jerseyNumber) => CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult ConfirmTryout(
                CareerSaveSnapshot snapshot,
                string choiceId) => CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult ConfirmWeekPlan(
                CareerSaveSnapshot snapshot,
                string firstContentId,
                string secondContentId) => CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult ExecuteNextAction(CareerSaveSnapshot snapshot) =>
                CareerUiUseCaseResult.Failure("unused");

            public CareerUiUseCaseResult ResolveEvent(
                CareerSaveSnapshot snapshot,
                string optionId) => CareerUiUseCaseResult.Failure("unused");

            public CareerUiPreMatchPreview GetPreMatchPreview(
                CareerSaveSnapshot snapshot) => null;

            public Task<CareerUiUseCaseResult> PlayAndSettleAsync(
                CareerSaveSnapshot snapshot,
                CareerMatchPriority priority,
                CancellationToken cancellationToken) =>
                Task.FromResult(CareerUiUseCaseResult.Failure("unused"));

            public CareerUiUseCaseResult SaveNow(CareerSaveSnapshot snapshot) =>
                CareerUiUseCaseResult.Failure("unused");
        }
    }
}
