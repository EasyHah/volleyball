using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Career.Persistence;
using Volleyball.Career.Presentation;
using Volleyball.Presentation;

namespace Volleyball.Bootstrap
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument), typeof(CareerUiShell))]
    public sealed class CareerVerticalSliceBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset menuActions;

        public CareerUiSessionController Controller { get; private set; }
        public CareerV5MatchLifecycleService V5MatchLifecycle { get; private set; }

        private void Awake()
        {
            try
            {
                RequireAssets();
                var fileSystem = new SystemAtomicFileSystem();
                var paths = new CareerStoragePaths(Application.persistentDataPath);
                var careerRepository = new LocalCareerSaveRepository(paths, fileSystem);
                var profileRepository = new LocalPlayerProfileRepository(paths, fileSystem);
                var catalogRepository = new LocalProfileCatalogRepository(paths, fileSystem);
                var random = new CareerDeterministicRandom();
                var formalSceneRunner = gameObject.AddComponent<
                    CareerFormalSixVsSixMatchRunnerV4>();
                var formalSceneRunnerV5 = gameObject.AddComponent<
                    CareerFormalSixVsSixMatchRunnerV5>();
                var matchRunner = new CareerOfflineMatchRouterV4(
                    formalSceneRunner);
                var mapper = new CareerMatchV4Mapper(
                    new CareerMatchV4RuntimeConfiguration(
                        FormalSixVsSixRallyBootstrap.FormalPhysicsConfigurationHash,
                        FormalSixVsSixRallyBootstrap
                            .CreateFormalTrajectoryPredictionProviderConfiguration(),
                        CareerMatchV4FactPolicy
                            .DirectAggregateWithLegacyFixtureCompatibility));
                var executor = new CareerMatchExecutorV4(
                    matchRunner,
                    mapper);
                var mapperV5 = new CareerMatchV5Mapper(
                    FormalSixVsSixRallyBootstrap.FormalPhysicsConfigurationHash,
                    FormalSixVsSixRallyBootstrap
                        .CreateFormalTrajectoryPredictionProviderConfigurationV5());
                V5MatchLifecycle = new CareerV5MatchLifecycleService(
                    new CareerFirstMatchLaunchFactoryV5(),
                    mapperV5,
                    new CareerMatchExecutorV5(mapperV5, formalSceneRunnerV5),
                    new CareerV5PendingStore(paths, fileSystem));
                var adapter = new CareerUiUseCasesAdapter(
                    new CareerLocalUiWorkflow(
                        profileRepository,
                        catalogRepository,
                        careerRepository),
                    new CareerOnboardingService(
                        careerRepository,
                        new CryptographicCareerSeedSource(),
                        random,
                        TryoutCatalogV1.Create()),
                    new CareerWeekCommandService(careerRepository, random),
                    new CareerPendingMatchService(
                        careerRepository,
                        random,
                        new CareerFirstMatchLaunchFactoryV1(
                            CareerMatchExecutionMode.Direct),
                        executor),
                    new CareerMatchSettlementService(
                        careerRepository,
                        executor,
                        new CareerMatchSettlementRulesV1Calculator()),
                    new CareerRecentSessionStore(Application.persistentDataPath),
                    new CareerDiagnosticExporter(Application.persistentDataPath));

                Controller = new CareerUiSessionController(adapter);
                var document = GetComponent<UIDocument>();
                GetComponent<CareerUiShell>().Bind(Controller);
                ConfigureInput(document);
                formalSceneRunner.Initialize(
                    document,
                    GetComponent<CareerMenuInputRouter>());
                formalSceneRunnerV5.Initialize(
                    document,
                    GetComponent<CareerMenuInputRouter>());
                Controller.Initialize();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void RequireAssets()
        {
            if (menuActions == null)
            {
                throw new InvalidOperationException(
                    "Career UI requires its input asset.");
            }
        }

        private void ConfigureInput(UIDocument document)
        {
            // Unity 6 UI Toolkit supplies its runtime pointer/navigation event provider.
            // The Career router owns only the explicit Submit/Back/Cancel/Page actions so the
            // project does not need to add the legacy UGUI/EventSystem package.
            var router = GetComponent<CareerMenuInputRouter>();
            if (router == null)
            {
                router = gameObject.AddComponent<CareerMenuInputRouter>();
            }

            router.Initialize(menuActions, document, Controller);
        }
    }
}
