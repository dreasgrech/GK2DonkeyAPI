using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GK2DonkeyAPI
{
    // The mods' keys wait while a text box of any window has the cursor (the user, 2026-10-30: an L typed in a search box
    // on the Tech Tree, another mod's, opened the Mounts panel; the keys read the keyboard, not the game's LazyInput,
    // which that box switches off). Asks KeyActions.WaitReason, what Poll asks before any key: a self-test can't press
    // a key. Both kinds of text box (TextMeshPro, Unity UI), and a selected button, which is no text box. The panel's
    // own boxes: PanelSmoke.
    internal static partial class SelfTest
    {
        internal static IEnumerator KeysWaitWhileTyping()
        {
            if (EventSystem.current == null)
            {
                Skip("no EventSystem in the scene (no text box can have the cursor)");
                yield break;
            }
            string before = KeyActions.WaitReason();
            if (before != null)
            {
                Warn($"the keys already wait ({before}): skipped");
                yield break;
            }
            var holder = new GameObject("GK2DonkeyAPI self-test text boxes", typeof(RectTransform), typeof(Canvas));
            try
            {
                // a TextMeshPro box (as the Tech Tree's search box and the game's bug report window)
                var tmpGo = new GameObject("tmp box", typeof(RectTransform));
                tmpGo.transform.SetParent(holder.transform, false);
                var tmpText = new GameObject("text", typeof(RectTransform));
                tmpText.transform.SetParent(tmpGo.transform, false);
                TMPro.TMP_InputField tmp = tmpGo.AddComponent<TMPro.TMP_InputField>();
                tmp.textViewport = (RectTransform)tmpText.transform;
                tmp.textComponent = tmpText.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.ActivateInputField();
                yield return Frames(2);
                string why = KeyActions.WaitReason();
                Check(tmp.isFocused && why == "typing in a text box", $"a TextMeshPro box of another window has the cursor: the keys wait (focused {tmp.isFocused}, {why ?? "they don't wait"})");
                tmp.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                yield return Frames(1);
                why = KeyActions.WaitReason();
                Check(why == null, $"its cursor gone: the keys work again ({why ?? "they don't wait"})");

                // a Unity UI box (older mods' kind)
                var uiGo = new GameObject("ui box", typeof(RectTransform));
                uiGo.transform.SetParent(holder.transform, false);
                var uiText = new GameObject("text", typeof(RectTransform));
                uiText.transform.SetParent(uiGo.transform, false);
                UnityEngine.UI.Text label = uiText.AddComponent<UnityEngine.UI.Text>();
                label.font = Font.CreateDynamicFontFromOSFont("Arial", 14); // a box without a font never takes the cursor
                UnityEngine.UI.InputField ui = uiGo.AddComponent<UnityEngine.UI.InputField>();
                ui.textComponent = label;
                ui.ActivateInputField();
                yield return Frames(2);
                why = KeyActions.WaitReason();
                Check(ui.isFocused && why == "typing in a text box", $"a Unity UI box has the cursor: the keys wait (focused {ui.isFocused}, {why ?? "they don't wait"})");
                ui.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                yield return Frames(1);
                why = KeyActions.WaitReason();
                Check(why == null, $"its cursor gone: the keys work again ({why ?? "they don't wait"})");

                // a selected button is no text box: the keys work
                var buttonGo = new GameObject("button", typeof(RectTransform));
                buttonGo.transform.SetParent(holder.transform, false);
                buttonGo.AddComponent<UnityEngine.UI.Button>();
                EventSystem.current.SetSelectedGameObject(buttonGo);
                yield return Frames(1);
                why = KeyActions.WaitReason();
                Check(EventSystem.current.currentSelectedGameObject == buttonGo && why == null, $"a selected button: the keys work ({why ?? "they don't wait"})");
            }
            finally
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                    && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(holder.transform))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                UnityEngine.Object.Destroy(holder);
            }
        }
    }
}
