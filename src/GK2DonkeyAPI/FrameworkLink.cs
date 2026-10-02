using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GK2.Framework;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Optional GK2 Mod Framework integration (SuperMan4eg's, docs/gk2-mod-framework.md): a player mod's settings page
    // (MountSettingsPage) shows in the framework's Mods menu when it's installed. Without it nothing here runs, and the
    // page goes into the game's own Settings window instead (SettingsRows.cs).
    //
    // Only Register and PageMod touch GK2.Framework types. Mono loads GK2.Framework.dll the first time a method using
    // its types is compiled, so as long as nothing calls Register while the framework is absent, the DLL is never
    // needed. tests/FrameworkScan checks that no other code references the framework.
    internal static class FrameworkLink
    {
        internal const string FrameworkGuid = "ru.superman4eg.gk2.framework";

        internal static bool Installed => Chainloader.PluginInfos.TryGetValue(FrameworkGuid, out var info) && info.Instance != null;

        // True: the page is in the Mods menu. False: not installed, or registering failed (logged); the caller then
        // shows the page another way.
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
                // e.g. a framework version whose API changed: the mod runs on, its settings go to the Settings window
                Plugin.Log.LogWarning($"GK2 Mod Framework {info.Metadata.Version} is installed, but registering {page.Title} with it failed, so its settings go into the game's Settings window instead. {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(MountSettingsPage page)
        {
            FrameworkApi.RegisterMod(new PageMod(page), page.Config);
        }

        private sealed class PageMod : Gk2ModBase
        {
            private readonly MountSettingsPage page;
            private readonly Gk2ModMetadata metadata;

            // The mod keeps its own switches: the framework adds no Enabled entry (frameworkManagesEnabledState false).
            // requiresKnownBuild: the list says "Unknown build" unless OnRegister confirms the build, which it does
            // only while the mod says it's active (its status) and GK2 Donkey API runs. No Dependencies: the menu
            // printed the framework there with no "(optional)", which read as if the mod needed it.
            internal PageMod(MountSettingsPage page)
            {
                this.page = page;
                metadata = new Gk2ModMetadata(page.OwnerId, page.Title, page.Author, page.Version, page.Description,
                    supportsRuntimeToggle: false, requiresKnownBuild: true, frameworkManagesEnabledState: false);
            }

            public override Gk2ModMetadata Metadata => metadata;

            public override void OnRegister(Gk2ModContext context)
            {
                if (Mounts.IsAvailable && (page.Status == null || page.Status() == "Active"))
                {
                    try
                    {
                        context.ConfirmCurrentBuildCompatibility("Its own check of the game code passed (the audited build).");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning("GK2 Mod Framework didn't take the build confirmation (" + e.Message + "); its Mods menu may show " + page.Title + " as not compatible. The mod itself works as normal.");
                    }
                }
                Gk2Settings s = context.Settings;
                int order = 0;
                foreach (MountSettingsPage.Row r in page.Rows)
                {
                    order++;
                    // The same Section/Key/type as the page's binds, so BepInEx hands back those entries: the menu
                    // edits the values the mod reads, and their SettingChanged handlers apply them.
                    switch (r.Kind)
                    {
                        case MountSettingsPage.Kind.Toggle:
                            s.AddToggle(r.Section, r.Entry.Definition.Key, (bool)r.Entry.DefaultValue, r.Label, r.Description, order);
                            break;
                        case MountSettingsPage.Kind.Choice:
                            AddChoice(s, r, order);
                            break;
                        case MountSettingsPage.Kind.Steps:
                            s.AddIntSlider(r.Section, r.Entry.Definition.Key, (int)r.Entry.DefaultValue, r.Min, r.Max, r.Label, r.Description, r.Step, order);
                            break;
                        case MountSettingsPage.Kind.Key:
                            s.AddKeybind(r.Section, r.Entry.Definition.Key, (KeyboardShortcut)r.Entry.DefaultValue, r.Label, r.Description, order);
                            break;
                        case MountSettingsPage.Kind.Text:
                            s.AddText(r.Section, r.Entry.Definition.Key, (string)r.Entry.DefaultValue, r.Label, r.Description, order);
                            break;
                        case MountSettingsPage.Kind.Info:
                            s.AddReadOnly(r.Section, "Info" + order, r.Label, r.Description, r.InfoValue, order);
                            break;
                    }
                    if (r.Visible != null && r.Entry != null)
                    {
                        s.SetVisibilityCondition(r.Section, r.Entry.Definition.Key, r.Visible);
                    }
                }
            }

            // AddEnum<T> needs the enum type at compile time; the row only has it at run time.
            private static void AddChoice(Gk2Settings s, MountSettingsPage.Row r, int order)
            {
                Type t = r.Entry.SettingType;
                var m = typeof(Gk2Settings).GetMethod(nameof(Gk2Settings.AddEnum)).MakeGenericMethod(t);
                m.Invoke(s, new object[] { r.Section, r.Entry.Definition.Key, r.Entry.DefaultValue, r.Label, r.Description, order });
            }
        }
    }
}
