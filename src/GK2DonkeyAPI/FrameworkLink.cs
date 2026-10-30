using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GK2.Framework;
using GK2DonkeyAPI.Api;
using HarmonyLib;

namespace GK2DonkeyAPI
{
    // Optional GK2 Mod Framework integration (SuperMan4eg's, docs/gk2-mod-framework.md). The options themselves are the
    // API's own window (OptionsWindow, the same for every player: the framework isn't on the Steam Workshop, the user
    // 2026-10-30, docs/options-window.md); with the framework installed a page is in its Mods menu too, and its Settings
    // button opens that window. The framework has no public way to say what Settings does: it opens its own settings
    // page (ModsMenuWindow.OpenSettingsPage(RegisteredMod), private, the same in 0.1.16 and 0.1.24), and only for a mod
    // with a setting. So the page's entry has one setting, a button opening the window, and a Harmony prefix on that
    // method opens the window straight away for our pages. The prefix is found by its exact signature and is an extra:
    // if a framework version changes the method, it isn't applied (said once), and Settings shows the one button.
    // Registered, the page is also no longer one of the framework's "Imported settings" (every row of its cfg, raw).
    //
    // Only Register, RegisterApi and the classes nested here touch GK2.Framework types. Mono loads GK2.Framework.dll the
    // first time a method using its types is compiled, so as long as nothing calls them while the framework is absent,
    // the DLL is never needed. tests/FrameworkScan checks that no other code references the framework.
    internal static class FrameworkLink
    {
        internal const string FrameworkGuid = "ru.superman4eg.gk2.framework";

        internal static bool Installed => Chainloader.PluginInfos.TryGetValue(FrameworkGuid, out var info) && info.Instance != null;

        // A page is in the Mods menu, and its Settings button opens the options window (the prefix applied): the self-test's view.
        internal static bool AnyRegistered { get; private set; }
        internal static bool SettingsButtonPointed { get; private set; }

        // True: the page is in the Mods menu. False: not installed, or registering failed (logged); its options are in
        // the window either way.
        internal static bool TryRegister(MountSettingsPage page)
        {
            if (!Chainloader.PluginInfos.TryGetValue(FrameworkGuid, out var info) || info.Instance == null)
            {
                return false;
            }
            try
            {
                Register(page);
                return true;
            }
            catch (Exception e)
            {
                // e.g. a framework version whose API changed: the mod runs on, its options open from the Settings window
                Plugin.Log.LogWarning($"GK2 Mod Framework {info.Metadata.Version} is installed, but registering {page.Title} with it failed, so it isn't in its Mods menu; its options open from the game's Settings window. {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(MountSettingsPage page)
        {
            FrameworkApi.RegisterMod(new PageMod(page), page.Config);
            PageMod.Pages[page.OwnerId] = page;
            AnyRegistered = true;
            SettingsButtonPointed = PageMod.PatchSettingsButton();
        }

        // GK2 Donkey API itself in the Mods list (no settings there): so the pages' mods can name it as their
        // dependency, which the framework checks against its own list, and its cfg isn't imported raw. Its build is
        // confirmed only while it runs with every game member its features use identical (Mounts.StatusText "Active";
        // a frozen or forced run is not).
        internal static void TryRegisterApi(ConfigFile config)
        {
            if (!Chainloader.PluginInfos.TryGetValue(FrameworkGuid, out var info) || info.Instance == null)
            {
                return;
            }
            try
            {
                RegisterApi(config);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"GK2 Mod Framework {info.Metadata.Version} is installed, but listing GK2 Donkey API in its Mods menu failed; the mods built on it work as normal. {e.GetType().Name}: {e.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterApi(ConfigFile config)
        {
            FrameworkApi.RegisterMod(new ApiMod(), config);
        }

        private sealed class ApiMod : Gk2ModBase
        {
            private readonly Gk2ModMetadata metadata = new Gk2ModMetadata(Plugin.Guid, "GK2 Donkey API", "dreas", Plugin.Version,
                $"The mounts, their skins, carts and riding that other mods build on (API {Mounts.ApiVersion}). It does nothing on its own: "
                + "install a mod that uses it.",
                supportsRuntimeToggle: false, requiresKnownBuild: true, frameworkManagesEnabledState: false);

            public override Gk2ModMetadata Metadata => metadata;

            public override void OnRegister(Gk2ModContext context)
            {
                if (Mounts.IsAvailable && Mounts.StatusText == "Active")
                {
                    context.ConfirmCurrentBuildCompatibility("Its own check passed: every game member its features use is identical to the audited build.");
                }
            }
        }

        private sealed class PageMod : Gk2ModBase
        {
            internal static readonly Dictionary<string, MountSettingsPage> Pages = new Dictionary<string, MountSettingsPage>(StringComparer.OrdinalIgnoreCase);

            private readonly MountSettingsPage page;
            private readonly Gk2ModMetadata metadata;

            // The mod keeps its own switches: the framework adds no Enabled entry (frameworkManagesEnabledState false).
            // requiresKnownBuild: the list says "Unknown build" unless OnRegister confirms the build, which it does
            // only while the mod says it's active (its status) and GK2 Donkey API runs. Dependencies: GK2 Donkey API
            // only (not the framework: the menu printed it with no "(optional)", which read as if the mod needed it).
            internal PageMod(MountSettingsPage page)
            {
                this.page = page;
                metadata = new Gk2ModMetadata(page.OwnerId, page.Title, page.Author, page.Version, page.Description,
                    supportsRuntimeToggle: false, requiresKnownBuild: true, frameworkManagesEnabledState: false);
            }

            public override Gk2ModMetadata Metadata => metadata;

            // GK2 Donkey API (listed by ApiMod): shown as the mod's dependency, and the framework marks the mod
            // unavailable while the API is. Here, in the nested class: a field of FrameworkLink itself would load the
            // framework with FrameworkLink (tests/FrameworkScan).
            private static readonly Gk2ModDependency[] NeedsApi = { new Gk2ModDependency(Plugin.Guid) };

            public override IReadOnlyList<Gk2ModDependency> Dependencies => NeedsApi;

            public override void OnRegister(Gk2ModContext context)
            {
                if (Mounts.IsAvailable && page.StatusValue() == "Active")
                {
                    try
                    {
                        context.ConfirmCurrentBuildCompatibility("Its own check passed: every game member its features use is identical to the audited build.");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("GK2 Mod Framework didn't take the build confirmation (" + e.Message + "); its Mods menu may show " + page.Title + " as not compatible. The mod itself works as normal.");
                    }
                }
                // One setting: a button opening the options window. The framework's Settings button is live only for a mod
                // with a setting; the prefix below opens the window from it straight away, and without the prefix the
                // framework's page shows this button. No cfg entry (a button binds nothing).
                MountSettingsPage p = page;
                context.Settings.AddButton("Options", "OpenOptions", p.Title + " options", "Opens the options window: every setting of " + p.Title + ".", () => "Open", () =>
                {
                    try
                    {
                        OptionsWindow.Open(p); // over the Mods menu, which waits underneath and is shown again on Escape
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Mods menu: opening the options of {p.Title} failed: {e.Message}");
                    }
                }, 1);
            }

            // The framework's Settings button for one of our pages opens the options window, not the framework's page.
            private static bool patchTried, patched;

            // True: the prefix is in place (tried once a session).
            internal static bool PatchSettingsButton()
            {
                if (patchTried) return patched;
                patchTried = true;
                try
                {
                    // System.Type's own reflection, by the exact signature (one method, never an overload picked by name): a
                    // lookup meant to survive the method's absence (the build's dependency manifest follows AccessTools'
                    // lookups into the game, not these)
                    Type window = Type.GetType("GK2.Framework.ModsMenuWindow, GK2.Framework");
                    Type registered = Type.GetType("GK2.Framework.RegisteredMod, GK2.Framework");
                    System.Reflection.MethodInfo open = window != null && registered != null
                        ? window.GetMethod("OpenSettingsPage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { registered }, null)
                        : null;
                    if (open == null)
                    {
                        Plugin.Log.LogInfo("Settings: GK2 Mod Framework's Mods menu has no OpenSettingsPage(RegisteredMod) (another version): its Settings button shows the page's one button, which opens the options window.");
                        return false;
                    }
                    new Harmony(Plugin.Guid + ".modsmenu").Patch(open, prefix: new HarmonyMethod(typeof(PageMod), nameof(OpenSettingsPrefix)));
                    patched = true;
                    Plugin.Log.LogInfo("Settings: GK2 Mod Framework's Settings button opens the options window of our pages.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogInfo($"Settings: GK2 Mod Framework's Settings button could not be pointed at the options window ({e.GetType().Name}: {e.Message}); it shows the page's one button, which opens it.");
                }
                return patched;
            }

            private static bool prefixFailed;

            // false: the window is open instead of the framework's page; true: the framework goes on (another mod's).
            private static bool OpenSettingsPrefix(RegisteredMod mod)
            {
                if (prefixFailed || mod == null) return true;
                try
                {
                    if (Pages.TryGetValue(mod.Metadata.Id, out MountSettingsPage p))
                    {
                        OptionsWindow.Open(p);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    prefixFailed = true; // the framework's own page from now on (the button on it opens the window)
                    Plugin.Log.LogWarning($"Mods menu: the Settings button could not open the options window ({e.Message}); it opens the framework's page from now on.");
                }
                return true;
            }
        }
    }
}
