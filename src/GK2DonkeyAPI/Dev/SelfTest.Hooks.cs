using UnityEngine;

namespace GK2DonkeyAPI
{
    // Read-only views of an instance's state for the self-tests (Dev/SelfTest.cs): what decides what is seen (the
    // animator's facing, which cart parts are on, whose materials the sprites wear) rather than what a screenshot shows.
    internal static partial class Mount
    {
        // Each catch-up (a foal placed straight next to its parent): a test of following counts the jumps.
        internal static int CatchUpCount;

        internal sealed partial class Instance
        {
            // The drawing around this one into the log (the Instances tab's "Log drawing"): every sprite renderer
            // within 2.5 of its root, with what would explain a wrong picture, a box or a clipped frame: name and path,
            // the sprite (name, rect, pivot, pixels per unit), the texture, enabled and forceRenderingOff, sorting,
            // the transform's lossy scale, the renderer's world bounds against the root, the material's shader; the
            // skin's overlays marked. Plus the view's root and body transforms and the seat.
            internal void LogDrawing()
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"Drawing around {Describe()} (skin '{LooksRig.Wearing}', scale {ViewScale:F2}, facing {(FacingRight ? "right" : "left")}):\n");
                if (donkey == null || !donkey)
                {
                    sb.Append("  no view.");
                    Plugin.Log.LogInfo(sb.ToString());
                    return;
                }
                Vector3 root = donkey.transform.position;
                sb.Append($"  root {root:F3}, root lossy scale {donkey.transform.lossyScale:F3}; body sprite {(bodySprite != null && bodySprite ? bodySprite.name + " at " + bodySprite.position.ToString("F3") + " lossy " + bodySprite.lossyScale.ToString("F3") : "-")}; seat applied {seatApplied:F3}\n");
                foreach (SpriteRenderer r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || !r) continue;
                    Vector3 p = r.transform.position;
                    if ((p - root).sqrMagnitude > 2.5f * 2.5f) continue;
                    string path = r.transform.name;
                    for (Transform t = r.transform.parent; t != null && t != donkey.transform.parent; t = t.parent) path = t.name + "/" + path;
                    bool ours = r.name.StartsWith("gk2_skin", System.StringComparison.Ordinal);
                    Sprite s = r.sprite;
                    Bounds b = r.bounds;
                    sb.Append(ours ? "  * " : "    ")
                      .Append(path)
                      .Append(": sprite ").Append(s != null ? $"'{s.name}' rect {s.rect.width:0}x{s.rect.height:0} pivot ({s.pivot.x:0.#},{s.pivot.y:0.#}) ppu {s.pixelsPerUnit:0} tex '{(s.texture != null ? s.texture.name + " " + s.texture.width + "x" + s.texture.height : "-")}'" : "none")
                      .Append($"; enabled {r.enabled}, forceOff {r.forceRenderingOff}, active {r.gameObject.activeInHierarchy}; sorting {r.sortingLayerName}/{r.sortingOrder}; draw mode {r.drawMode}{(r.drawMode != SpriteDrawMode.Simple ? " size " + r.size.ToString("F2") : "")}; colour {r.color}; lossy scale {r.transform.lossyScale:F3}; pos {p - root:F3} from the root; bounds {b.size:F2} from y {b.min.y - root.y:F2} to {b.max.y - root.y:F2} above the root; shader {(r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "-")}")
                      .Append(r.maskInteraction != SpriteMaskInteraction.None ? "; mask interaction " + r.maskInteraction : "")
                      .Append('\n');
                }
                Plugin.Log.LogInfo(sb.ToString());
            }

            internal bool TestSwitchedOff => switchedOff;

            internal float TestFacing => riderFacingX;

            internal bool TestViewActive => donkey != null && donkey && donkey.gameObject.activeInHierarchy;

            // The view's animator Direction parameter (0 = right, -180 = left), or null without one.
            internal float? TestAnimatorDirection()
            {
                if (donkey == null || !donkey)
                {
                    return null;
                }
                foreach (Animator a in donkey.GetComponentsInChildren<Animator>())
                {
                    if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null)
                    {
                        continue;
                    }
                    foreach (AnimatorControllerParameter p in a.parameters)
                    {
                        if (p.name == "Direction" && p.type == AnimatorControllerParameterType.Float)
                        {
                            return a.GetFloat(p.nameHash);
                        }
                    }
                }
                return null;
            }

            // The drawn cart body's place in the world (CartPoint; null without a cart body), the way from the mount
            // to its cart as the view measured it, and the hitch gap in effect: the "hitch and unhitch" self-test
            // checks HitchGapPx with these.
            internal Vector3? TestCartBodyWorld() => cartBodyPart != null && cartBodyPart.T != null && cartBodyPart.T ? CartPoint() : (Vector3?)null;
            internal Vector3 TestHitchDir() => HitchDirWorld;
            internal float TestHitchGap() => HitchGapWorld;

            // How many of the cart's parts are switched on (a donkey without a cart: none).
            internal int TestCartPartsOn()
            {
                if (donkey == null || !donkey)
                {
                    return 0;
                }
                int n = 0;
                foreach (Transform t in donkey.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (System.Array.IndexOf(CartPartNames, t.name) >= 0 && t.gameObject.activeSelf)
                    {
                        n++;
                    }
                }
                return n;
            }

            // Renderers of the view left out of drawing by the levels (idle) or the experiment: none while it is active.
            internal int TestQuietRenderers => quietRenderers.Count + quietAnimators.Count;

            // The size its view's root was made at (WgoData.Scale on the root).
            internal float TestViewScale => donkey != null && donkey ? donkey.transform.localScale.x : float.NaN;

            // Every coloured sprite wears this instance's own material (null: colours not attached or off).
            internal bool? TestColoursOurs() => ColorsRig.TestSpritesOurs();
            internal bool TestSkinShowing() => LooksRig.TestShowing();
            internal bool TestSkinFrontShowing() => LooksRig.TestFrontShowing();
            internal string TestSkinWearing() => LooksRig.Wearing;
            internal bool TestSkinBuilding() => LooksRig.Building;
            internal int TestEffectEmitters() => EffectsRig.TestEmitters;
            internal bool TestEffectLit() => EffectsRig.Lit;
            internal Vector3? TestEffectRingPosition() => EffectsRig.TestRingPosition;
            internal int TestEffectInWorld() => EffectsRig.TestInWorld;

            // A baby with no room (Dev/FoalRoomTests.cs): put on the spot a catch-up gives it now, facing away from its
            // lead, so the test can build its walls. Returns the body point; forward: the way it faces (flat), depth: away
            // from the camera.
            // Bound to it, as in its own frame: the slots read its own ShuffleFollowGap override.
            internal Vector3 TestPlaceFacingAway(out Vector3 forward, out Vector3 depth)
            {
                using (Bind(this))
                {
                    Lead l = LeadNow();
                    Vector3 away = AwayFromCamera();
                    Vector3 right = Vector3.Cross(Vector3.up, away).normalized;
                    float worldToRight = Vector3.Dot(Vector3.right, right) >= 0f ? 1f : -1f;
                    Vector3 s = CatchUpSpot(l, out bool clear, out string fail);
                    float h = Vector3.Dot(s - l.Body, right) >= 0f ? 1f : -1f; // its side of the lead along the screen
                    riderFacingX = h * worldToRight; // a step goes along right * facing * worldToRight (TryStartStep)
                    PlaceBy(l, s, clear, fail, "self-test: placed beside its lead, facing away from it");
                    forward = right * h;
                    depth = away;
                    return s;
                }
            }

            // Why a catch-up wouldn't put it on its own slot (FreeSlot's first choice), or null when it would. F11 of
            // 2026-11-06: by the yard the catch-up gave the slot beside its own, so its own was a real place to go (a
            // route out of the test's walls), and the test wasn't the case it is about.
            internal string TestWhyNotOwnSlot()
            {
                using (Bind(this))
                {
                    Lead l = LeadNow();
                    if (!l.Here)
                    {
                        return "its lead isn't here";
                    }
                    Vector3 s = SlotNearParent(SiblingIndex());
                    if (Taken(s, l.SceneId))
                    {
                        return $"its own slot {s:F2} has another donkey's body on it";
                    }
                    groundFail = null;
                    bool ok = LineWalkable(l.Body, s, "catch-up");
                    string why = groundFail;
                    groundFail = null;
                    return ok ? null : $"no clear line from its lead to its own slot {s:F2} ({why ?? "no reason kept"})";
                }
            }

            internal float TestLateral => shuffleLateralKnown ? shuffleLateral : -1f;
            internal bool TestLost => followLost;
            internal bool TestWedged => wedged;
            internal Vector3 TestBody => BodyAt(Position, riderFacingX);
        }
    }

    internal static partial class Colors
    {
        internal sealed partial class Rig
        {
            internal bool? TestSpritesOurs()
            {
                if (attachedTo == null || sprites.Count == 0)
                {
                    return null;
                }
                bool any = false;
                foreach (SpriteTarget t in sprites)
                {
                    if (t.Mine == null)
                    {
                        continue;
                    }
                    any = true;
                    if (t.R == null || !t.R || t.R.sharedMaterial != t.Mine)
                    {
                        return false;
                    }
                }
                return any ? true : (bool?)null;
            }
        }
    }
}
