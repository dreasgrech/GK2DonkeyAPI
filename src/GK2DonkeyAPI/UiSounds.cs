using System;
using System.Collections.Generic;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GK2DonkeyAPI
{
    // Our windows' sounds, the game's own (docs/mounts-panel-design.md, "Sounds"). Read from the game's prefabs
    // (2026-11-11): about 55 of its windows play win_open / win_close (LazyWindow.openSoundId / closeSoundId: the
    // Settings window ours copy their look from, Character, Map, Customization, Craft, Pause, ...); its buttons hover
    // gui_hover and click gui_click, its item cells hover gui_hover_light, its tabs hover gui_hover_light and click
    // tab_click (LazyButton.on*Sound); a greyed one is silent. The gamepad plays the same: the game's focus moving
    // onto a button is its hover, Select its click (LazyButton.SetCallbacksIntoGamepadNavigationItem). All flat (2D),
    // through the game's sound-effects volume, the same id held back when it was just played (LazyAudio's own delay).
    internal static class UiSounds
    {
        internal enum Kind { Button, Cell, Tab }

        internal const string WindowOpenId = "win_open", WindowCloseId = "win_close";
        private const string ClickId = "gui_click", HoverId = "gui_hover", HoverLightId = "gui_hover_light", TabClickId = "tab_click";

        internal static string HoverOf(Kind k) => k == Kind.Button ? HoverId : HoverLightId;
        internal static string ClickOf(Kind k) => k == Kind.Tab ? TabClickId : ClickId;

        // the last played and how many (the self-tests read them)
        internal static string Last;
        internal static int Played;

        // an id the game no longer has stays silent (LazyAudio would log an error each time): looked up once
        private static readonly Dictionary<string, bool> known = new Dictionary<string, bool>();
        private static bool failedSaid;

        internal static void WindowOpened() => Play(WindowOpenId);
        internal static void WindowClosed() => Play(WindowCloseId);

        internal static void Play(string id)
        {
            try
            {
                if (!LazyAudio.IsInitialized) return;
                if (!known.TryGetValue(id, out bool has))
                {
                    AudioConfig cfg = SoundBank.Config;
                    if (cfg == null) return; // not up yet: asked again next time
                    has = cfg.Get(id) != null;
                    known[id] = has;
                    if (!has) Plugin.Log.LogWarning($"Settings: the game has no sound \"{id}\" any more; our windows stay silent there.");
                }
                if (!has) return;
                LazyAudio.PlayAndForget(id);
                Last = id;
                Played++;
            }
            catch (Exception e)
            {
                if (failedSaid) return;
                failedSaid = true;
                Plugin.Log.LogWarning($"Settings: a window sound could not play ({e.Message}); the windows go on silent.");
            }
        }

        // A control's hover and click, by the mouse here and by the gamepad through RowControls (its focus moving, its
        // Select). On the control's own GameObject, beside its Selectable.
        internal static UiSound Add(GameObject go, Kind kind)
        {
            if (go == null) return null;
            UiSound s = go.GetComponent<UiSound>() ?? go.AddComponent<UiSound>();
            s.Kind = kind;
            s.Target = go.GetComponent<Selectable>();
            return s;
        }
    }

    internal sealed class UiSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerClickHandler
    {
        internal UiSounds.Kind Kind;
        internal Selectable Target;
        private bool downLive;

        // greyed (its own interactable or a CanvasGroup's): silent, as the game's
        private bool Live => Target == null || Target.IsInteractable();

        // Unity sends the enter to the control under the mouse and to every parent it newly enters: the mouse onto a
        // colour part's arrow from the row below is the row's enter and the arrow's in one frame. Only the control
        // nearest the mouse plays (the game nests no buttons, so it never meets this).
        public void OnPointerEnter(PointerEventData e)
        {
            UiSound nearest = e != null && e.pointerEnter != null ? e.pointerEnter.GetComponentInParent<UiSound>() : this;
            if (nearest != null && nearest != this) return;
            if (Live) UiSounds.Play(UiSounds.HoverOf(Kind));
        }

        // the state at the press: the click may grey it or rebuild it before this hears the click
        public void OnPointerDown(PointerEventData e) => downLive = e.button == PointerEventData.InputButton.Left && Live;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !downLive) return;
            downLive = false;
            UiSounds.Play(UiSounds.ClickOf(Kind));
        }

        internal void Focused()
        {
            if (Live) UiSounds.Play(UiSounds.HoverOf(Kind));
        }

        internal void Pressed()
        {
            if (Live) UiSounds.Play(UiSounds.ClickOf(Kind));
        }
    }
}
