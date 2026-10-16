using UnityEngine;

namespace GK2DonkeyAPI
{
    // GK2 Mod Framework's Mods window ("GK2ModsMenuWindow"), for LookPreview and MenuLabels. Looking it up with
    // GameObject.Find walks the whole scene: in a game scene each lookup took about 1 ms and found nothing while playing,
    // four times a second in each of the two (2026-10-16, Performance Hog and a probe: 2-3 ms frames). The window opens
    // only at the main menu or over the pause menu (a window on the game's modal stack: MainGame pauses the game while
    // one is open), and the framework makes it once and keeps it (ModsMenuWindow.CreateInstance; a language change makes
    // a new one). So it is looked for only at the menu or while the game is paused, and once found it is kept until it is
    // destroyed.
    internal static class ModsWindow
    {
        private const string Name = "GK2ModsMenuWindow";
        private static GameObject found;

        // The window while it is showing, or null.
        internal static GameObject Showing()
        {
            if (found == null)
            {
                if (Mount.IsGameReady && !MainGame.IsGamePaused)
                {
                    return null; // playing: the window can't be open
                }
                found = GameObject.Find(Name);
                if (found == null)
                {
                    return null;
                }
            }
            return found.activeInHierarchy ? found : null;
        }
    }
}
