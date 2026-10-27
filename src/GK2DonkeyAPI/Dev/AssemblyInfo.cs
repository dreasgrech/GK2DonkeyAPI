// The integration tests (tools/GK2DonkeyAPI.IntegrationTests): a dev-only plugin, never shipped, whose in-game
// scenarios test the mods built on the API through the self-test runner here (SelfTest.AddScenarios, its helpers).
// The API knows only that its tests exist, never the mods they test (the user, 2026-10-27).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GK2DonkeyAPI.IntegrationTests")]
