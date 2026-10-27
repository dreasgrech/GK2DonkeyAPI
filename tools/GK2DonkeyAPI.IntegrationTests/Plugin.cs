using BepInEx;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Dev-only: the in-game integration tests of the mods built on the GK2 Donkey API: GK2 Saddle Up (its settings page,
    // its companion) and its packs (the Dragon, the Critters, the Donkey Skins, Gerry). Their scenarios join the API's
    // self-test runner at the start (SelfTest.AddScenarios) and run from F11's Tests tab with the API's own, in their
    // groups, under the same start and finish. A scenario whose mod or pack isn't installed skips with a reason
    // (MissingPack, MissingSaddleUp). Never shipped; the API knows nothing of these mods (docs/self-tests.md).
    [BepInPlugin(Guid, "GK2 Donkey API: integration tests", "0.1.0")]
    [BepInDependency("dreas.gk2.donkeyapi")]
    public sealed class TestsPlugin : BaseUnityPlugin // not "Plugin": the tests mean the API's
    {
        public const string Guid = "dreas.gk2.donkeyapi.integrationtests";

        private void Awake()
        {
            SelfTest.AddScenarios(Tests.Scenarios);
            Logger.LogInfo($"GK2 Donkey API integration tests: {Tests.Scenarios.Count} scenario(s) added to the self-tests.");
        }
    }
}
