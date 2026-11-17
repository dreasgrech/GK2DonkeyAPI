using BepInEx;

namespace GK2DonkeyAPI.IntegrationTests
{
    // Dev-only: the in-game integration tests of the mods built on the GK2 Donkey API: GK2 Saddle Up (its settings page,
    // its companion) and its packs (the Dragon, the Critters, the Donkey Skins, Larry). Their scenarios join the API's
    // own in the API's list (SelfTest.AddScenarios: in their groups, under the same start and finish), and the whole
    // list is handed to GK2 Mod Test Bench, which runs it from its panel (F4, the suite "GK2 Donkey API"; Bench.cs).
    // A scenario whose mod or pack isn't installed skips with a reason (MissingPack, MissingSaddleUp). Never shipped;
    // the API knows nothing of these mods, nor of the bench (docs/self-tests.md).
    [BepInPlugin(Guid, "GK2 Donkey API: integration tests", "0.2.0")]
    [BepInDependency("dreas.gk2.donkeyapi")]
    [BepInDependency(GK2ModTestBench.TestBench.Guid)]
    public sealed class TestsPlugin : BaseUnityPlugin // not "Plugin": the tests mean the API's
    {
        public const string Guid = "dreas.gk2.donkeyapi.integrationtests";

        private void Awake()
        {
            SelfTest.AddScenarios(Tests.Scenarios);
            int given = Bench.Register();
            Logger.LogInfo($"GK2 Donkey API integration tests: {Tests.Scenarios.Count} scenario(s) added to the API's; all {given} are in GK2 Mod Test Bench (F4, the suite \"GK2 Donkey API\").");
        }
    }
}
