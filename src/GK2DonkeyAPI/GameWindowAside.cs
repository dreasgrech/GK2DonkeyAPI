using System;
using System.Collections.Generic;
using BepInEx;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The game window under our windows (the Mounts panel, the options window), and the player's controls while one is open.
    //
    // The way back, a stack: the game's own windows below (the pause menu, the Settings window, the Character window...),
    // then ours. The game window on top when the first of ours opens is not closed: it stays in the game's window stack as
    // it is (modal: the game stays paused) with its GameObject switched off, so its own Escape handling doesn't run under
    // ours; the last of ours closing switches it back on, same page and state, and its own Escape goes on from there.
    // Closing and reopening it would not do: a window's Close may reopen the one before it and forget where it came
    // from, and its Open starts on its first page (GK2 Mod Framework's ModsMenuWindow, read 2026-10-21).
    // Any game window: held as the game's base type (LazyWidgetBase, what the window stack holds). It was
    // LazyWindow<LazyWidgetDataBase>, which C# doesn't match for a window with its own data type (no generic variance on
    // classes): 44 of the game's 62, the Character window with the Tech Tree, the inventory and chests among them, stayed
    // live under the panel and open after Ride (the user, 2026-10-30; F11 caught it).
    internal static class GameWindowAside
    {
        private static LazyWidgetBase under;
        private static bool controlsTaken;
        private static int escapeFrame = -1;

        internal static bool ControlsTaken => controlsTaken;
        internal static string UnderText => under != null ? under.name : "the game";

        // The game window on top switched off, waiting under ours (nothing when ours opened over the game itself).
        internal static void SetAside(string who)
        {
            under = null;
            try
            {
                LazyWidgetBase top = LazyWindowsStackController.ActiveWindow;
                if (top != null && top.gameObject.activeSelf)
                {
                    under = top;
                    top.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo($"Settings: the window under {who} could not be set aside ({e.Message}).");
            }
        }

        // A game window opened over ours (a popup, another mod's window opened on a key; 2026-10-30, docs/options-window.md
        // §12): it would sit under our canvas, unseen, holding the game's modal focus. While it is the window on top, ours
        // step aside (invisible, no clicks, no keys: Covered, followed by each of ours) and come back when it closes.
        internal static bool Covered { get; private set; }
        private static LazyWidgetBase coveredBy;

        // Every frame (Plugin.LateUpdate) while one of ours is open. Also: the window set aside, shown again by something
        // else meanwhile (the stack showing it when a window over it closed), is set aside again.
        internal static void Tick(bool oursOpen)
        {
            if (!oursOpen)
            {
                Covered = false;
                coveredBy = null;
                return;
            }
            LazyWidgetBase top = null;
            try { top = LazyWindowsStackController.ActiveWindow; } catch { }
            bool now = top != null && top != under && top.gameObject.activeInHierarchy;
            if (now != Covered)
            {
                Covered = now;
                if (now)
                {
                    coveredBy = top;
                    Plugin.Log.LogInfo($"Settings: {top.name} opened over our window: ours waits, hidden, until it closes.");
                }
                else
                {
                    Plugin.Log.LogInfo($"Settings: {(coveredBy != null ? coveredBy.name : "the window over ours")} closed: ours is back.");
                    coveredBy = null;
                }
            }
            if (!now && under != null && under.gameObject.activeSelf) under.gameObject.SetActive(false);
        }

        // The game gone (a load, the main menu): its windows went with it.
        internal static void Forget() => under = null;

        // back: the window under ours shown again as it was. Not back (Ride, Drive): every game window closed, into the game.
        internal static void Restore(bool back, string who)
        {
            LazyWidgetBase w = under;
            under = null;
            if (w == null) return; // opened from the game: nothing to show again
            try
            {
                w.gameObject.SetActive(true);
                if (back)
                {
                    Plugin.Log.LogInfo($"Settings: {who} closed: back to {w.name}.");
                    return;
                }
                // into the game: close the chain the game built (each Close may open the one before it)
                for (int i = 0; i < 8; i++)
                {
                    LazyWidgetBase top = LazyWindowsStackController.ActiveWindow;
                    if (top == null) break;
                    if (!top.gameObject.activeSelf) top.gameObject.SetActive(true);
                    if (!CloseWindow(top) || LazyWindowsStackController.ActiveWindow == top)
                    {
                        Plugin.Log.LogWarning($"Settings: the game window {top.name} could not be closed after the Mounts panel's Ride or Drive; close it yourself.");
                        break; // the same window again would be closed again: stop
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: the window under {who} could not be shown again ({e.Message}).");
            }
        }

        // A game window's own Close(), whatever its data type: the game has no Close on its base type, only on
        // LazyWindow<T>. Looked up once per window type (a Ride or Drive from the panel, never per frame) on its
        // LazyWindow<T> base; the call goes to the window's own override. False: not a LazyWindow, or Close threw.
        private static readonly Dictionary<Type, System.Reflection.MethodInfo> closeOf = new Dictionary<Type, System.Reflection.MethodInfo>();

        private static bool CloseWindow(LazyWidgetBase w)
        {
            Type t = w.GetType();
            if (!closeOf.TryGetValue(t, out System.Reflection.MethodInfo close))
            {
                close = null;
                for (Type b = t; b != null; b = b.BaseType)
                {
                    if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(LazyWindow<>))
                    {
                        close = b.GetMethod("Close", Type.EmptyTypes);
                        break;
                    }
                }
                closeOf[t] = close;
            }
            if (close == null) return false;
            try
            {
                close.Invoke(w, null);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: closing the game window {w.name} failed: {(e.InnerException ?? e).Message}");
                return false;
            }
        }

        // The player's controls taken while ours is open, as the game's own modal windows do (ByUI: the ride goes on, as
        // with a chest or the inventory): the game's own key reader (PlayerInputHandler, the pause menu on Escape, E, I,
        // movement) does not run under ours. Re-asserted every tick: the game gives ByUI back when its last modal window
        // closes, which a popup over ours would do. No player (the main menu): nothing to take.
        internal static void TakeControls(string who)
        {
            if (!Mount.IsGameReady) return; // the main menu: no player (MainGame.PlayerController threw there, 2026-10-30)
            try
            {
                PlayerController pc = MainGame.PlayerController;
                if (pc == null) return;
                if (pc.IsControlEnabledByType(TakenControlType.ByUI)) pc.SetControlTakenType(TakenControlType.ByUI, isEnabled: false);
                controlsTaken = true;
            }
            catch (Exception e)
            {
                if (!controlsTaken) Plugin.Log.LogInfo($"Settings: {who} could not take the controls ({e.Message}); the game's keys work under it.");
            }
        }

        internal static void ReleaseControls(string who)
        {
            if (!controlsTaken) return;
            controlsTaken = false;
            try
            {
                PlayerController pc = MainGame.PlayerController;
                if (pc != null && !LazyWindowsStackController.HasAnyModalWindowOpened) pc.SetControlTakenType(TakenControlType.ByUI, isEnabled: true); // a game window open meanwhile keeps its own hold
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Settings: {who} could not give the controls back ({e.Message}).");
            }
        }

        // Escape (or the gamepad's Back, the game windows' close too) pressed this frame, for the window shown: true once a
        // frame, so one press is one step back however many of ours tick; never while a key is being set (KeyCapture
        // takes it). The game reads the same press and would open its pause menu: consumed (LazyInput.ClearKeyDown).
        internal static bool TakeEscape()
        {
            if (KeyCapture.Active || escapeFrame == Time.frameCount) return false;
            if (!UnityInput.Current.GetKeyDown(KeyCode.Escape) && !(LazyInput.IsGamepadActive && LazyInput.GetKeyDown(GameKey.Back))) return false;
            escapeFrame = Time.frameCount;
            try { LazyInput.ClearKeyDown(GameKey.InGameMenu); LazyInput.ClearKeyDown(GameKey.Back); } catch { }
            return true;
        }
    }
}
