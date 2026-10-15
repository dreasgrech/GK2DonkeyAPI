using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Cargo on the cart. The cart's cargo is the mounted donkey's own inventory, exactly as in the game's delivery
    // routine (Flow_AddItem puts the body into npc_donkey's 2-slot inventory). Loading and unloading follow the
    // game's town-pallet and conveyor-cell handlers: the keeper's overhead item goes in with
    // PlayerData.InsertOverheadItemTo, and comes back out with the inventory's remove call + TryAddOverheadItemNoReplace,
    // or is dropped beside the keeper when his hands are full.
    //
    // Drawing: the game only ever draws a corpse on the cart (its prefab's drawer switches one shroud mesh on while
    // a body_corpse is aboard) and arranges nothing; every pile in the game is hand-made art switched by item count.
    // So the mod draws the cargo itself with copies of the keeper's overhead mesh atom (one mesh per big item) and
    // places them with a size-based packer (RefreshViews): every item's box is measured, the bigger footprint goes to
    // the bottom or the far side, two items lie side by side across the bed when they fit and stack otherwise, at
    // the density of the game's own timber stockpile (touching, one width apart, one height up); further items go on
    // in layers of up to two (General.CartSlots, docs/cart-capacity.md). Corrections come from cargo-layout.cfg
    // (CargoLayout), keyed by size class, so the 27 items reduce to seven classes.
    //
    // How many: the cart's inventory is sized per cart (ApplySlots, from General.CartSlots) when its view is prepared,
    // before anything carried over goes back on; the game's definition (npc_donkey.inventorySize = 2) is never touched.
    //
    // Save safety: the donkey is a temp object, so its inventory never reaches a save. Everything is returned to the
    // keeper (hands first, then dropped at his feet) whenever the ride ends (dismount, cleanup, sleep) and right
    // before the game writes a save (prefix on SaveSystem.Save). Across a door with StayMountedThroughDoors the items
    // are kept in memory and put back on the re-summoned cart.
    internal static partial class Cargo
    {
        internal const string CorpseId = "body_corpse";
        internal const string ContainerName = "cart_cargo";

        // What the packer placed last, for the panel: item id and whether it sits on top of another item.
        public struct PlacedItem
        {
            public string Id;
            public bool OnTop;
            public int Layer;  // 0 on the bed, 1 on the first layer above it, ...
            public bool Alone; // the only item on the cart
            public Vector3 Box;
            public Vector3 Pos;      // where the packer put it: footprint centre along/across, bottom height above the floor
            // The relation that placed it: "bed" (the first item on the floor), "side" / "along" (beside Partner, the
            // layer's first item), "on" (on top of Partner, its support). Rel: its place relative to the partner (for
            // "bed": relative to the floor origin, i.e. Pos). Tuned: that relation came from a tuned layout entry.
            public string Relation;
            public int Partner;
            public Vector3 Rel;
            public bool Tuned;
            public string Guid;      // the item's UniqueId, for the panel's "Isolate"
            public Vector3 LayerPos; // its place relative to its layer's origin (the floor origin on the bed)
            public float LayerBase;  // beside, above the bed: the first item's bed height taken off both (CargoPacker.Result.LayerBase)
            public Vector3 BesideStored => LayerPos + new Vector3(0f, LayerBase, 0f);
        }

        // ------------------------------------------------------------------ the bound instance's cart (Mount.Current)

        private static readonly List<PlacedItem> NoPlaced = new List<PlacedItem>();
        internal static Rig Current => Mount.Current?.CargoRig;
        internal static bool Unloading => Current != null && Current.Unloading;
        public static float LastFloor => Current?.LastFloor ?? 0f;
        public static string LastPairMode => Current?.LastPairMode ?? "";
        public static bool LastPairTuned => Current != null && Current.LastPairTuned;
        public static List<PlacedItem> Placed => Current?.Placed ?? NoPlaced;
        public static bool Isolating => Current != null && Current.Isolating;
        // The panel's Cargo tab calls this every frame it is drawn: the ridden cart's items are tinted by their role.
        internal static void MarkPanelTint() => Rig.PanelTintFrame = Time.frameCount;
        internal static bool TintIsSecond(PlacedItem p, List<PlacedItem> all) => Rig.IsSecond(p, all);
        internal static Color TintFor(PlacedItem p, List<PlacedItem> all) => Rig.TintFor(p, all);
        public static int IsolatedFrom => Current?.IsolatedFrom ?? 0;
        public static void Isolate(string guidA, string guidB, string relation) => Current?.Isolate(guidA, guidB, relation);
        public static void ClearIsolation() => Current?.ClearIsolation();
        public static string LastLayoutNote => Current?.LastLayoutNote ?? "";
        public static int Count => Current?.Count ?? 0;
        public static bool Attached => Current != null && Current.Attached;
        internal static Inventory Inv => Current?.Inv;
        internal static WgoData DonkeyData => Current?.DonkeyData;
        internal static void Resync(string why) => Current?.Resync(why);
        public static void LoadFromHead(string reason) => Current?.LoadFromHead(reason);
        internal static void Announce() => Current?.Announce();
        public static void SetDownLast(PlayerData pd) => Current?.SetDownLast(pd);
        public static void Unload(string reason) => Current?.Unload(reason);
        public static void Unload(string reason, bool toHands) => Current?.Unload(reason, toHands);
        internal static bool DropAtKeeper(PlayerData pd, Item item) => Current != null && Current.DropAtKeeper(pd, item);
        public static string Summary() => Current?.Summary() ?? "cargo: none";

        // Helpers that belong to no cart in particular.
        internal static bool SuppressAnnounce
        {
            get => Rig.SuppressAnnounce;
            set => Rig.SuppressAnnounce = value;
        }

        internal static bool Allowed(Item item) => Rig.Allowed(item);

        // A floor under every cart's slot count while a tool needs it (the cargo sheet puts pairs on the cart whatever
        // CartSlots says). 0 normally.
        internal static int SlotFloor
        {
            get => Rig.SlotFloor;
            set => Rig.SlotFloor = value;
        }

        // An item that stands for a world object (a zombie: its UniqueId is the ZombieWgoData's, and the zombie's
        // organs and gear are its nested inventory). Such an item must never be conjured from an id or thrown away,
        // or the zombie is left with no body anywhere (or a body with no zombie).
        internal static bool IsLinked(ItemDef def) => def != null && (def.isLinkedToWgo || def.itemGroupIds.Contains("zombie"));

        // A zombie proper (item group "zombie", body_zombie): drawn as the game's zombie and a layout class of its own.
        // Narrower than IsLinked on purpose: body_wild_zombie is in "wild_zombie", rides the cart as a shroud and keeps
        // its place in the body class, whatever its isLinkedToWgo says.
        internal static bool IsZombie(ItemDef def) => def != null && def.itemGroupIds.Contains("zombie");

        // The item for the API and the log: its id, GUID and, for a zombie, its name.
        internal static CargoItem Describe(Item it)
        {
            bool zombie = IsZombie(it?.Definition);
            string name = null;
            if (zombie)
            {
                try
                {
                    name = MainGame.ZombieSystemData?.GetZombie(it.UniqueId)?.Name;
                }
                catch
                {
                }
            }
            return new CargoItem(it?.id, it?.UniqueId?.ToString(), zombie, name);
        }

        // For log lines: "wood", or "body_zombie (its name)".
        internal static string Name(Item it) => it == null ? "nothing" : Describe(it).ToString();

        // The last resort for a zombie whose item could not be set down or handed over: the zombie is stood up in the
        // world where it was meant to go, with the game's own call (ZombieSystemData.PutZombieFromStoreToGameScene, which
        // takes it out of the store of carried zombies, as placing one does). A carried zombie with no item anywhere
        // could never be placed again. The cart is not touched here: the caller takes the item off it. False for any
        // other item, or when the game refuses this too (then the caller reports it LOST).
        internal static bool RescueZombie(Item item, string sceneId, Vector3 at, string why)
        {
            if (item == null || item.IsEmpty || !IsLinked(item.Definition))
            {
                return false;
            }
            try
            {
                ZombieSystemData zs = MainGame.ZombieSystemData;
                ZombieWgoData z = zs?.GetZombie(item.UniqueId);
                if (z == null || string.IsNullOrEmpty(sceneId))
                {
                    return false;
                }
                if (!ReferenceEquals(z.ZombieItem, item))
                {
                    z.SetZombieItem(item); // the object with its organs and gear, whatever happened before
                }
                zs.PutZombieFromStoreToGameScene(item.UniqueId, sceneId, at);
                Plugin.Log.LogWarning($"Cargo: zombie {z.Name} could not be set down ({why}); it stands in the world at {at:F2} instead.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Cargo: standing zombie {item.id} up in the world failed ({why}): {e}");
                return false;
            }
        }
        internal static int SetDownHeld(PlayerData pd) => Rig.SetDownHeld(pd);
        internal static string ResolveIcon(ItemDef def) => Rig.ResolveIcon(def);
        internal static Transform FindOverheadElement(ItemDef def) => Rig.FindOverheadElement(def);
        internal static bool MeasureMeshes(Transform root, Transform space, out Bounds bounds, bool includeInactive = false) => Rig.MeasureMeshes(root, space, out bounds, includeInactive);
        internal static bool IsShadowMesh(string name) => Rig.IsShadowMesh(name);
        internal static DropViewAtomMesh FindSourceAtom() => Rig.FindSourceAtom();

        private static readonly AccessTools.FieldRef<DropViewAtomMesh, List<DropViewAtomMeshElement>> ZombieDropPrefabs =
            AccessTools.FieldRefAccess<DropViewAtomMesh, List<DropViewAtomMeshElement>>("zombieWorkerDropPrefabs");

        // The lying zombie (pose 2) of the keeper's overhead atom, the first head: what a zombie on the cart is drawn
        // with, for its size. Null when the atom or its list is missing.
        internal static Transform ZombieDropPrefab()
        {
            DropViewAtomMesh src = FindSourceAtom();
            List<DropViewAtomMeshElement> list = src != null ? ZombieDropPrefabs(src) : null;
            if (list == null)
            {
                return null;
            }
            foreach (DropViewAtomMeshElement el in list)
            {
                if (el != null)
                {
                    return el.transform;
                }
            }
            return null;
        }

        // A packer setting or a layout entry changed: every cart redraws with its own settings.
        public static void RefreshViews()
        {
            Mount.ForEachInstance(i => i.CargoRig.RefreshViews());
        }

        // One per instance (Mount.Instance.CargoRig).
        internal sealed partial class Rig
        {
            private readonly Mount.Instance owner;

            internal Rig(Mount.Instance owner)
            {
                this.owner = owner;
            }

            private Wgo donkey;
            private Inventory inv;
            private readonly List<Item> mirror = new List<Item>(); // what is on the cart, kept across scene changes
            private Transform container;
            private float bedWidth = 0.75f;  // measured from the empty cart body (its size across, local z)
            private float bedLength = 0.9f;  // measured from the empty cart body (its size along, local x)
            private readonly List<DropViewAtomMesh> views = new List<DropViewAtomMesh>();
            // What each view shows now ("icon:<id>" or "zombie:<head>:<lut>"): a zombie's IconId is its head id, so two
            // zombies with the same head but other colours swapping views would not be told apart by IconId alone.
            private readonly Dictionary<DropViewAtomMesh, string> viewKeys = new Dictionary<DropViewAtomMesh, string>();
            private bool subscribed;
            private bool unloading;
            internal bool Unloading => unloading;

            // The last pack: the floor height, how the pair was arranged ("side", "along", "stack"), and whether that came
            // from a tuned pair entry (for the panel).
            public float LastFloor { get; private set; }
            public string LastPairMode { get; private set; } = "";
            public bool LastPairTuned { get; private set; }

            public readonly List<PlacedItem> Placed = new List<PlacedItem>();
            public string LastLayoutNote { get; private set; } = "";

            public int Count => mirror.Count;
            public bool Attached => inv != null;
            internal Inventory Inv => inv;
            internal WgoData DonkeyData => donkey != null ? donkey.Data : null;

            // Re-read the cart inventory and redraw, for changes the inventory did not announce (an entry emptied by a
            // split elsewhere is removed silently, see Round 9 of the findings) and for probes that bypass the events.
            internal void Resync(string why)
            {
                if (inv == null)
                {
                    return;
                }
                SyncMirror();
                owner.OnCargoChanged();
                Plugin.Log.LogInfo($"Cargo: resynced ({why}); {mirror.Count} item(s) on the cart.");
            }

            // Called once the cart parts are prepared (cart style only). Items carried over from the previous view go
            // back onto the new inventory.
            public void Attach(Wgo d)
            {
                Detach();
                if (d == null || d.Data == null)
                {
                    return;
                }
                donkey = d;
                inv = d.Data.Inventory;
                EnsureContainer();
                ApplySlots(); // the slot count before the carried-over items go back on: a door never sheds any
                if (mirror.Count > 0)
                {
                    var carried = new List<Item>(mirror);
                    mirror.Clear();
                    int back = 0;
                    foreach (Item it in carried)
                    {
                        if (it == null || it.IsEmpty)
                        {
                            continue;
                        }
                        if (inv.AddItemToInventory(it))
                        {
                            HeadReroute.RelinkZombie(inv, it, owner.Describe());
                            back++;
                        }
                        else
                        {
                            if (owner.IsRidden)
                            {
                                DropAtKeeper(MainGame.PlayerData, it);
                            }
                            else if (!MainGame.Instance.dropSystem.DropItem(it, owner.SceneId, owner.CartPoint())
                                && !RescueZombie(it, owner.SceneId, owner.CartPoint(), "did not fit on the re-summoned cart"))
                            {
                                Plugin.Log.LogWarning($"Cargo: {it.id} did not fit on {owner.Describe()} and the game refused to set it down; it is LOST.");
                            }
                        }
                    }
                    Plugin.Log.LogInfo($"Cargo: {back} of {carried.Count} item(s) put back on the re-summoned cart.");
                }
                inv.OnItemsAdd += OnChanged;
                inv.OnItemsRemove += OnChanged;
                subscribed = true;
                SyncMirror();
                if (owner.IsRidden)
                {
                    LoadFromHead("mount"); // a standing cart takes nothing (its view may spawn next to a keeper holding something)
                }
                RefreshViews();
            }

            // There is no head storage while riding the cart: whatever allowed items the keeper holds go onto the cart
            // while there is room (at mount, and again after a save put them back in his hands). The game's own insert
            // path is used: add to the cart inventory, then remove from the head by GUID.
            public void LoadFromHead(string reason)
            {
                PlayerData pd = MainGame.PlayerData;
                if (inv == null || donkey == null || pd == null || !pd.HasOverheadItem || !MountRules.Allowed(MountFeature.Cargo))
                {
                    return;
                }
                // Repair: an emptied entry (count 0) on the head draws like the item but can neither be used nor dropped
                // (the game skips empty items everywhere). The first build of the reroute could leave one behind.
                int purged = 0;
                foreach (Item it in new List<Item>(pd.OverheadItems))
                {
                    if (it != null && it.IsEmpty)
                    {
                        pd.RemoveOverheadItem(it);
                        purged++;
                    }
                }
                if (purged > 0)
                {
                    Plugin.Log.LogWarning($"Cargo: removed {purged} emptied item entr{(purged == 1 ? "y" : "ies")} from the head ({reason}).");
                }
                int moved = 0;
                foreach (Item it in new List<Item>(pd.OverheadItems))
                {
                    if (Allowed(it) && inv.CanAddItemToInventory(it))
                    {
                        pd.InsertOverheadItemTo(donkey.Data, it);
                        HeadReroute.RelinkZombie(inv, it, owner.Describe());
                        moved++;
                    }
                }
                if (moved > 0)
                {
                    if (reason == "mount")
                    {
                        MountSounds.CargoLanded(); // not when the cargo goes back on after a save (waking up)
                    }
                    Plugin.Log.LogInfo($"Cargo: {moved} item(s) moved from the hands onto the cart ({reason}); cart {inv.Data.Inventory.Count}/{inv.Data.InventorySize}.");
                }
            }

            // On foot at a standing cart (docs/load-on-foot.md): the topmost held item the cart takes goes onto it,
            // one per call, as the game's town pallet takes held items. Added to the cart, then taken off the head by
            // GUID: the two steps of the game's InsertOverheadItemTo without its PlayerInsertOverheadToWgoAnItem trigger
            // (quests listen to that trigger by item id alone; the cart is no target of theirs). Null when loaded, else
            // why not.
            internal string LoadOneFromHead(PlayerData pd, out string itemId)
            {
                itemId = null;
                if (inv == null || donkey == null)
                {
                    return "The cart is not here.";
                }
                if (pd == null || !pd.HasOverheadItem)
                {
                    return "Nothing in hand.";
                }
                if (!MountRules.Allowed(MountFeature.Cargo))
                {
                    return "The cart takes nothing now.";
                }
                fitInv = inv;
                if (!pd.TryGetOverheadItem(FitsCart, out Item item))
                {
                    return pd.TryGetOverheadItem(AllowedItem, out _) ? "The cart is full." : "The cart can't take that.";
                }
                itemId = item.id;
                if (!inv.AddItemToInventory(item))
                {
                    Resync("load failed");
                    return "The cart is full.";
                }
                HeadReroute.RelinkZombie(inv, item, owner.Describe());
                pd.RemoveOverheadItem(item);
                MountSounds.CargoLanded();
                Plugin.Log.LogInfo($"Cargo: {Cargo.Name(item)} loaded onto {owner.Describe()} from the hands; cart {inv.Data.Inventory.Count}/{inv.Data.InventorySize}, {pd.OverheadCount} still held.");
                return null;
            }

            // The reverse: the cart's top item into the keeper's free hands, with the game's own pickup (its sound and
            // events). An item the hands refuse goes back onto the cart.
            internal string TakeOneToHead(PlayerData pd, out string itemId)
            {
                itemId = null;
                if (inv == null || donkey == null)
                {
                    return "The cart is not here.";
                }
                if (pd == null)
                {
                    return "No keeper.";
                }
                if (!MountRules.Allowed(MountFeature.Cargo))
                {
                    return "The cart gives nothing now.";
                }
                List<Item> list = inv.Data.Inventory;
                if (list.Count == 0)
                {
                    return "The cart is empty.";
                }
                if (!pd.HasFreeOverheadSlot)
                {
                    return "My hands are full.";
                }
                Item last = list[list.Count - 1];
                if (!inv.RemoveItemFromInventoryByUID(last))
                {
                    Resync("take failed");
                    return "It won't come off the cart.";
                }
                if (!pd.TryAddOverheadItemNoReplace(last))
                {
                    inv.AddItemToInventory(last);
                    HeadReroute.RelinkZombie(inv, last, owner.Describe());
                    Resync("hands refused");
                    return "My hands are full.";
                }
                itemId = last.id;
                Plugin.Log.LogInfo($"Cargo: {Cargo.Name(last)} taken off {owner.Describe()} into the hands; cart {list.Count}/{inv.Data.InventorySize}, {pd.OverheadCount} held.");
                return null;
            }

            // The corpses off this cart into the keeper's hands, as many as they take (Mount.MorgueHandover: the keeper
            // walked into the morgue and the cart stayed at the door). From the cart's inventory while it has a view,
            // else from the list kept for the next one; a body the hands refuse goes back where it was, so nothing is
            // dropped or lost. Returns how many went; stop says why it stopped short, if it did.
            internal int HandCorpsesTo(PlayerData pd, out string stop)
            {
                stop = null;
                if (pd == null)
                {
                    stop = "no keeper";
                    return 0;
                }
                int n = 0;
                foreach (Item it in ItemsNow())
                {
                    if (it.id != CorpseId)
                    {
                        continue;
                    }
                    if (!pd.HasFreeOverheadSlot)
                    {
                        stop = "his hands are full";
                        break;
                    }
                    bool fromInv = inv != null;
                    if (fromInv ? !inv.RemoveItemFromInventoryByUID(it) : !mirror.Remove(it))
                    {
                        Plugin.Log.LogWarning($"Cargo: {Cargo.Name(it)} would not come off {owner.Describe()}; it stays there.");
                        continue;
                    }
                    bool inHands;
                    try
                    {
                        inHands = pd.TryAddOverheadItemNoReplace(it);
                    }
                    catch (Exception e)
                    {
                        // The game adds the item to the hands and then plays its pickup sound (as in Unload): a failure
                        // after the add leaves it in hand; before the add, it goes back onto the cart. Never lost.
                        inHands = InHands(pd, it);
                        Plugin.Log.LogWarning($"Cargo: handing {Cargo.Name(it)} to the keeper threw ({e.Message}); it is {(inHands ? "in his hands" : "back on " + owner.Describe())}.");
                    }
                    if (!inHands)
                    {
                        if (!fromInv)
                        {
                            mirror.Add(it);
                        }
                        else if (!inv.AddItemToInventory(it))
                        {
                            // The slot it came from is free again, so this does not happen; if it ever did, the body
                            // goes onto the ground at the keeper's feet (the game's own drop), never away.
                            Plugin.Log.LogWarning($"Cargo: {Cargo.Name(it)} did not go back onto {owner.Describe()}; {(DropAtKeeper(pd, it) ? "set down at the keeper's feet" : "it is LOST")}.");
                            stop = "his hands refused it";
                            break;
                        }
                        Plugin.Log.LogWarning($"Cargo: the keeper's hands refused {Cargo.Name(it)}; it is back on {owner.Describe()}.");
                        stop = "his hands refused it";
                        break;
                    }
                    n++;
                }
                if (inv != null)
                {
                    SyncMirror();
                    owner.OnCargoChanged();
                }
                else
                {
                    Announce();
                }
                return n;
            }

            // The predicates above, made once (a lambda capturing the cart would be new garbage on every call; the
            // controls ask every frame while the keeper holds something next to a cart).
            private static Inventory fitInv;
            private static readonly Predicate<Item> FitsCart = it => Allowed(it) && fitInv != null && fitInv.CanAddItemToInventory(it);
            internal static readonly Predicate<Item> AllowedItem = Allowed;

            // Whether the topmost item the cart takes would fit (the controls' label), without moving anything.
            // How many slots this cart should have: General.CartSlots (an instance's own override first), never below
            // what it holds or carries over, so a lowered setting cannot shed items (at a door the carried-over items
            // go back on before anything else). The number only: a chest aboard keeps 0 (CartChest), see ApplySlots.
            internal static int SlotFloor;

            internal int WantedSlots()
            {
                int slots = owner.Overrides.TryGetValue("CartSlots", out object o) && o is int n ? n : Plugin.CartSlots.GlobalValue;
                slots = Mathf.Max(slots, SlotFloor);
                int held = Mathf.Max(mirror.Count, inv != null ? inv.Data.Inventory.Count : 0);
                return Mathf.Max(1, Mathf.Max(slots, held));
            }

            // Applies WantedSlots to the inventory. False when there is nothing to do: no inventory, a chest aboard (its
            // 0 stays), or the size already right. Never redraws; the callers do.
            internal bool ApplySlots()
            {
                if (inv == null || owner.ChestRig.Aboard)
                {
                    return false;
                }
                int want = WantedSlots();
                if (inv.Data.InventorySize == want)
                {
                    return false;
                }
                inv.Data.InventorySize = want;
                return true;
            }

            // Isolation (the panel's "Isolate these two", docs/cargo-layout-layers.md): only two of the cart's items are
            // drawn, laid out as a two-item cart in the given relation ("beside" or "stack"), so that relation can be
            // tuned with the two-item editor while the real load stays on the inventory untouched. Ends with
            // ClearIsolation, when either item leaves the cart, or when the view goes.
            private string isolateA, isolateB, isolateRelation;
            internal bool Isolating => isolateA != null;
            internal string IsolateRelation => isolateRelation;
            internal int IsolatedFrom => Isolating && inv != null ? inv.Data.Inventory.Count : 0;

            internal void Isolate(string guidA, string guidB, string relation)
            {
                if (string.IsNullOrEmpty(guidA) || string.IsNullOrEmpty(guidB) || guidA == guidB)
                {
                    return;
                }
                isolateA = guidA;
                isolateB = guidB;
                isolateRelation = relation == "stack" ? "stack" : "beside";
                Plugin.Log.LogInfo($"Cargo: isolating two of {Count} items on {owner.Describe()} ({isolateRelation}).");
                RefreshViews();
            }

            internal void ClearIsolation()
            {
                if (!Isolating)
                {
                    return;
                }
                isolateA = isolateB = isolateRelation = null;
                RefreshViews();
            }

            // Both isolated items still on the cart; else the isolation ends quietly.
            private bool IsolationHolds()
            {
                if (!Isolating)
                {
                    return false;
                }
                if (inv == null || !owner.IsRidden)
                {
                    isolateA = isolateB = isolateRelation = null; // a parked cart shows its whole load
                    return false;
                }
                bool a = false, b = false;
                foreach (Item it in inv.Data.Inventory)
                {
                    if (it == null || it.IsEmpty || it.UniqueId == null) continue;
                    string g = it.UniqueId.ToString();
                    if (g == isolateA) a = true;
                    if (g == isolateB) b = true;
                }
                if (!a || !b)
                {
                    isolateA = isolateB = isolateRelation = null;
                }
                return a && b;
            }

            // ---------------------------------------------------------- the panel's tints
            // While the panel's Cargo tab is open, the ridden cart's items are tinted by their role in their layer, through
            // the game's own selection tint on the item's meshes: the first item of a layer blue, the second orange (an
            // item alone on top of a layer counts as the second, so a two-item stack shows blue below and orange on top).
            // The tab's labels carry the same colours. Cleared when the tab closes or the cargo changes.
            internal static int PanelTintFrame = -10;
            internal static readonly Color TintFirst = new Color(0.35f, 0.65f, 1f), TintSecond = new Color(1f, 0.6f, 0.2f);
            // The tab's "show on the cart" filters: items hidden from view only, while the tab is open. They stay in the
            // layout (the packer places them), so nothing moves; their views are switched off after the pack.
            internal static bool HideFirst, HideSecond;
            internal static readonly HashSet<int> HiddenLayers = new HashSet<int>();
            internal static bool AnyHidden => HideFirst || HideSecond || HiddenLayers.Count > 0;

            internal static bool PanelHides(PlacedItem p, List<PlacedItem> all)
            {
                if (HiddenLayers.Contains(p.Layer)) return true;
                bool second = IsSecond(p, all);
                return second ? HideSecond : HideFirst;
            }
            private readonly List<DropViewAtomMesh> placedViews = new List<DropViewAtomMesh>();
            private bool tintApplied, tintDirty;

            // One and two items: hue by role (blue the first, orange the second: the pair box and Alone are built on it).
            // Three or more: one colour per item, by its place in the load, the same for its name in the panel; two
            // shades of one orange (a layer-lightened role colour) read as the same to the user (2026-10-15).
            internal static readonly Color[] TintByIndex =
            {
                new Color(0.35f, 0.65f, 1f),   // blue
                new Color(1f, 0.6f, 0.2f),     // orange
                new Color(0.45f, 0.9f, 0.4f),  // green
                new Color(1f, 0.45f, 0.95f),   // magenta
                new Color(1f, 0.92f, 0.3f),    // yellow
                new Color(0.3f, 0.95f, 0.95f), // cyan
            };

            internal static Color TintFor(PlacedItem p, List<PlacedItem> all)
            {
                if (all.Count <= 2)
                {
                    return IsSecond(p, all) ? TintSecond : TintFirst;
                }
                int index = IndexOf(p, all);
                return TintByIndex[index < 0 ? 0 : index % TintByIndex.Length];
            }

            // The item's place in the load (PlacedItem is a struct: matched by what it is and where it lies).
            internal static int IndexOf(PlacedItem p, List<PlacedItem> all)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    PlacedItem o = all[i];
                    if (o.Id == p.Id && o.Layer == p.Layer && o.Partner == p.Partner && o.Relation == p.Relation && o.Pos == p.Pos)
                    {
                        return i;
                    }
                }
                return -1;
            }

            // The second item of its relation (orange): beside another, or alone on top. PlacedItem is a struct, so the
            // items are counted by layer, never compared by reference (ReferenceEquals on two boxings is always false:
            // every lone on-top item was drawn blue, found 2026-10-15 on slab + slab).
            internal static bool IsSecond(PlacedItem p, List<PlacedItem> all)
            {
                if (p.Relation == "side" || p.Relation == "along") return true;
                if (p.Relation != "on") return false;
                int inLayer = 0;
                foreach (PlacedItem o in all)
                {
                    if (o.Layer == p.Layer) inLayer++;
                }
                return inLayer == 1; // alone in its layer: on top; with a second item there, this one is the layer's first
            }

            // Every frame (Mount.Instance.Tick): applies or clears the tints when the tab opens or closes, or after a pack.
            internal void TickTint()
            {
                bool panelOpen = owner.IsRidden && Time.frameCount - PanelTintFrame <= 2;
                if (!panelOpen && CargoLayout.Comparing && Time.frameCount - PanelTintFrame > 2)
                {
                    // the tab closed while showing the session's starting layout: the current one comes back
                    CargoLayout.Compare(false);
                    Cargo.RefreshViews();
                }
                if (!panelOpen && AnyHidden)
                {
                    // the tab closed with items hidden: everything shows again
                    HideFirst = HideSecond = false;
                    HiddenLayers.Clear();
                    RefreshViews();
                }
                bool want = SheetTint != null || (panelOpen && Plugin.CargoTabTint.Value);
                if (want == tintApplied && !tintDirty)
                {
                    return;
                }
                ApplyTints(want);
            }

            // The cargo sheet's own colours (a contact's two items highlighted, or the tab's colours), while it photographs;
            // null: the panel's tint. A null colour leaves that item untinted. Set it, then refresh the views.
            internal static Func<List<PlacedItem>, int, Color?> SheetTint;

            internal float BedWidth => bedWidth;
            internal float BedLength => bedLength;
            internal Transform Container => container;                       // the cargo holder (the packer's space)
            internal List<DropViewAtomMesh> PlacedViews => placedViews;      // in Placed's order

            // The world box around the drawn cargo (the sheet sizes its crop to a tall stack with it).
            internal bool CargoWorldBounds(out Bounds b)
            {
                b = default;
                bool any = false;
                foreach (DropViewAtomMesh v in placedViews)
                {
                    if (v == null || !v || !v.gameObject.activeInHierarchy) continue;
                    foreach (Renderer r in v.GetComponentsInChildren<Renderer>())
                    {
                        if (r == null || !r.enabled) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                }
                return any;
            }

            private void ApplyTints(bool on)
            {
                try
                {
                    for (int i = 0; i < placedViews.Count && i < Placed.Count; i++)
                    {
                        DropViewAtomMesh v = placedViews[i];
                        if (v == null || !v || v.MeshElement == null || v.MeshElement.object3D == null || v.MeshElement.object3D.Object3DMeshes == null)
                        {
                            continue;
                        }
                        Color? sheet = SheetTint != null ? SheetTint(Placed, i) : null;
                        bool tint = on && (SheetTint == null || sheet.HasValue);
                        Color c = sheet ?? TintFor(Placed[i], Placed);
                        foreach (Object3DMesh m in v.MeshElement.object3D.Object3DMeshes)
                        {
                            if (m != null) m.SetSelectionTint(tint ? c : Color.white, tint ? 0.45f : 0f);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Cargo: tinting the cargo for the panel failed: " + e.Message);
                }
                tintApplied = on;
                tintDirty = false;
            }

            internal bool HeldItemFits(PlayerData pd)
            {
                if (inv == null || pd == null)
                {
                    return false;
                }
                fitInv = inv;
                return pd.TryGetOverheadItem(FitsCart, out _);
            }

            // The view is gone (dismount or scene change); the mirror is kept, Unload clears it.
            public void Detach()
            {
                isolateA = isolateB = isolateRelation = null;
                placedViews.Clear();
                tintApplied = false; // the views go with the holder
                if (subscribed && inv != null)
                {
                    inv.OnItemsAdd -= OnChanged;
                    inv.OnItemsRemove -= OnChanged;
                }
                subscribed = false;
                views.Clear();
                viewKeys.Clear();
                Placed.Clear();
                owner.ChestRig.OnHolderGone(); // the stand-in goes with the holder below
                // The holder (and the cargo meshes in it) is the mod's: it must not go back to the game's pool with the
                // cart parts, where the next donkey built from them (the game's delivery donkey too) would show it. Out
                // of the tree first, so a new holder made this frame is not confused with it (Destroy waits for the
                // frame's end).
                try
                {
                    if (!ReferenceEquals(container, null))
                    {
                        Live.CargoHolders--; // also when the game's destroying the view already took it (a child of the view)
                    }
                    if (container != null && container)
                    {
                        container.SetParent(null, worldPositionStays: false);
                        container.gameObject.SetActive(false);
                        UnityEngine.Object.Destroy(container.gameObject);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Cargo: removing the cargo holder of {owner.Describe()} failed: {e.Message}");
                }
                finally
                {
                    container = null;
                    donkey = null;
                    inv = null;
                }
            }

            // ---------------------------------------------------------- between instances and around saves

            // The items on this cart now (the inventory while there is a view, else the list kept for the next one).
            internal List<Item> ItemsNow()
            {
                var l = new List<Item>();
                foreach (Item it in inv != null ? inv.Data.Inventory : mirror)
                {
                    if (it != null && !it.IsEmpty) l.Add(it);
                }
                return l;
            }

            internal List<CargoItem> Described()
            {
                var l = new List<CargoItem>();
                foreach (Item it in ItemsNow()) l.Add(Describe(it));
                return l;
            }

            internal List<string> Ids()
            {
                var l = new List<string>();
                foreach (Item it in ItemsNow()) l.Add(it.id);
                return l;
            }

            // Hitching and unhitching: every item leaves this cart for the other instance's (SetCarried), handed to
            // no one in between.
            internal List<Item> TakeAllForTransfer()
            {
                CargoSheet.Abort("hitch or unhitch"); // the sheet's items never go to another cart
                List<Item> items = ItemsNow();
                unloading = true;
                try
                {
                    if (inv != null)
                    {
                        foreach (Item it in items) inv.RemoveItemFromInventoryByUID(it);
                    }
                }
                finally
                {
                    unloading = false;
                }
                mirror.Clear();
                Announce();
                return items;
            }

            // Items for this cart: they go onto its inventory when its view is next prepared (Attach).
            internal void SetCarried(List<Item> items)
            {
                mirror.Clear();
                if (items != null) mirror.AddRange(items);
            }

            // An item now lives elsewhere (on the ground after a save it could not be taken back from): off this cart.
            internal void Forget(Item item)
            {
                if (item == null) return;
                unloading = true;
                try
                {
                    if (inv != null) inv.RemoveItemFromInventoryByUID(item);
                }
                finally
                {
                    unloading = false;
                }
                mirror.RemoveAll(x => ReferenceEquals(x, item));
                Announce();
                owner.OnCargoChanged();
            }

            private void OnChanged(List<Item> _)
            {
                SyncMirror();
                if (owner.IsRidden)
                {
                    TestLoads.CartChanged(inv); // a test item that left the cart is ordinary cargo from now on
                }
                ApplySlots(); // a lowered setting takes effect once the extra items are off
                if (!unloading)
                {
                    owner.OnCargoChanged(); // refreshes the views and re-collects the lifted cart parts
                }
                if (owner.IsRidden)
                {
                    RefreshTargetHint();
                }
            }

            // The hint over what the keeper faces is built when he turns to it and rebuilt only when the game asks
            // (Wgo.DrawWidgets); room on the cart changes what it offers (a working zombie's "[E] Take" while the cart
            // is the hands), so it is asked here. Found in game 2026-10-05: after setting a zombie down, "[E] Take" came
            // back only after turning away and back.
            private static void RefreshTargetHint()
            {
                try
                {
                    MainGame.PlayerController?.PlayerInteractionComponent?.WgoUnderInteraction?.DrawWidgets();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Cargo: redrawing the hint of what the keeper faces failed: " + e.Message);
                }
            }

            private void SyncMirror()
            {
                mirror.Clear();
                if (inv != null)
                {
                    mirror.AddRange(inv.Data.Inventory);
                }
                Announce();
            }

            // What the API events last reported as on the cart (item GUID -> id). Diffing against it, rather than raising
            // at each call site, reports every way items come and go exactly once, and nothing for the core's own
            // bookkeeping: the cart summoned again after a door holds the same GUIDs, and around a save the announcing is
            // held (SuppressAnnounce) until the cargo is back.
            private readonly Dictionary<string, CargoItem> announced = new Dictionary<string, CargoItem>();
            internal static bool SuppressAnnounce;

            internal void Announce()
            {
                if (SuppressAnnounce)
                {
                    return;
                }
                var now = new Dictionary<string, CargoItem>();
                foreach (Item it in inv != null ? inv.Data.Inventory : mirror)
                {
                    if (it != null && !it.IsEmpty && it.UniqueId != null)
                    {
                        string key = it.UniqueId.ToString();
                        now[key] = announced.TryGetValue(key, out CargoItem had) ? had : Describe(it);
                    }
                }
                foreach (var kv in new List<KeyValuePair<string, CargoItem>>(announced))
                {
                    if (!now.ContainsKey(kv.Key))
                    {
                        announced.Remove(kv.Key);
                        if (owner.IsRidden || owner.Ephemeral) Mounts.RaiseCargoUnloaded(kv.Value); // the ridden cart (the summon too, while it hands its cargo back) speaks to the API
                    }
                }
                foreach (var kv in now)
                {
                    if (!announced.ContainsKey(kv.Key))
                    {
                        announced[kv.Key] = kv.Value;
                        if (owner.IsRidden || owner.Ephemeral) Mounts.RaiseCargoLoaded(kv.Value);
                    }
                }
            }

            // Sets the top cart item down on the ground: what the game's interact press does with a held item when there
            // is nothing to interact with (PlayerInputHandler.UpdateInput), one item per press. Called from that press
            // while riding the cart (PlayerInputHandler_UpdateInput_Patch). Silent when the cart is empty, as the game is
            // with empty hands. With CargoSetDownBehindCart the item goes behind the cart (DropBehindCart), else in front
            // of the keeper, where the keeper's interaction box then finds it and the next press picks it up again.
            public void SetDownLast(PlayerData pd)
            {
                if (inv == null || donkey == null || pd == null)
                {
                    return;
                }
                try
                {
                    List<Item> list = inv.Data.Inventory;
                    if (list.Count == 0)
                    {
                        return;
                    }
                    Item last = list[list.Count - 1];
                    if (!inv.RemoveItemFromInventoryByUID(last))
                    {
                        Plugin.Log.LogWarning("Cargo: could not take the item off the cart.");
                        Resync("take failed");
                        return;
                    }
                    if (owner.S.CargoSetDownBehindCart.Value && DropBehindCart(pd, last))
                    {
                        Plugin.Log.LogInfo($"Cargo: set {Cargo.Name(last)} down behind the cart; cart {list.Count}/{inv.Data.InventorySize}.");
                        return;
                    }
                    DropAtKeeper(pd, last);
                    Plugin.Log.LogInfo($"Cargo: set {Cargo.Name(last)} down in front of the keeper; cart {list.Count}/{inv.Data.InventorySize}.");
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Cargo: setting the item down failed: " + e);
                }
            }
            // Zombies only with the behaviour key CargoZombies (docs/zombies-on-cart.md); everything else always.
            internal static bool Allowed(Item item)
            {
                return item != null && !item.IsEmpty && item.Definition != null
                    && (!item.Definition.itemGroupIds.Contains("zombie") || Plugin.CargoZombies.Value);
            }

            // Everything back to the keeper: hands while they are free, the rest dropped at his feet (the game's own
            // drop position in front of him). Used at every ride end and before a save.
            public void Unload(string reason) => Unload(reason, toHands: true);

            // toHands false: straight onto the ground (used while still riding, when the hands must stay empty); the
            // hands are then only the fallback for an item the game refuses to drop, so nothing is lost.
            public void Unload(string reason, bool toHands)
            {
                if (CargoSheet.Running)
                {
                    // The sheet's items are test copies made in memory; they must not be set down in the world.
                    CargoSheet.Abort("unload on " + reason);
                }
                List<Item> items = inv != null ? new List<Item>(inv.Data.Inventory) : new List<Item>(mirror);
                if (items.Count == 0)
                {
                    mirror.Clear();
                    Announce();
                    return;
                }
                if (Plugin.Quitting)
                {
                    // The game is closing: its world and sound system are being torn down, and closing never saves (the
                    // only save is on waking, EnergySystem.StopSleeping, which hands the cargo back first). Handing items
                    // back now could only fail; the last save already has everything where it was.
                    Plugin.Log.LogInfo($"Cargo: the game is closing with {items.Count} item(s) on the cart; not handed back (closing does not save, the last save is unaffected).");
                    mirror.Clear();
                    return;
                }
                PlayerData pd = MainGame.PlayerData;
                int hands = 0, dropped = 0, lost = 0;
                unloading = true;
                try
                {
                    foreach (Item it in items)
                    {
                        if (it == null || it.IsEmpty)
                        {
                            continue;
                        }
                        // One item at a time, so a failure with one never loses the others.
                        try
                        {
                            if (inv != null)
                            {
                                inv.RemoveItemFromInventoryByUID(it);
                            }
                            // A zombie goes to the hands only for the save hand-back (it comes back onto the cart right
                            // after). At any other ride end the ground: an end forced by the game (a fight, sleep, work,
                            // a script) has already emptied or checked the hands by then.
                            bool linked = IsZombie(it.Definition);
                            bool handsOk = toHands && (!linked || reason == "save");
                            if (pd == null)
                            {
                                if (RescueZombie(it, owner.SceneId, owner.CartPoint(), "unload on " + reason + ", no player data"))
                                {
                                    dropped++;
                                }
                                else
                                {
                                    lost++;
                                    Plugin.Log.LogWarning($"Cargo: {it.id} LOST on unload ({reason}): no player data.");
                                }
                            }
                            else if (handsOk && pd.TryAddOverheadItemNoReplace(it))
                            {
                                hands++;
                            }
                            else if (DropAtKeeper(pd, it))
                            {
                                dropped++;
                            }
                            else if (!toHands && !linked && pd.TryAddOverheadItemNoReplace(it))
                            {
                                hands++;
                            }
                            else
                            {
                                lost++;
                            }
                        }
                        catch (Exception e)
                        {
                            // The game adds the item to the hands and then plays its pickup sound; a failure after the add
                            // (a torn-down sound system) leaves the item in hand. Look before calling it lost, and try the
                            // ground when it is not there.
                            if (pd != null && InHands(pd, it))
                            {
                                hands++;
                                Plugin.Log.LogWarning($"Cargo: {it.id} handed over on unload ({reason}), but the game's pickup side effects failed: {e.Message}");
                                continue;
                            }
                            try
                            {
                                if (pd != null && DropAtKeeper(pd, it))
                                {
                                    dropped++;
                                    Plugin.Log.LogWarning($"Cargo: {it.id} set down on unload ({reason}) after handing it over failed: {e.Message}");
                                    continue;
                                }
                            }
                            catch
                            {
                            }
                            if (RescueZombie(it, pd != null ? pd.currentGameSceneId : owner.SceneId, pd != null ? pd.position.Value : owner.CartPoint(), "unload on " + reason))
                            {
                                dropped++;
                                continue;
                            }
                            lost++;
                            Plugin.Log.LogWarning($"Cargo: {it.id} LOST on unload ({reason}): {e}");
                        }
                    }
                }
                finally
                {
                    unloading = false;
                }
                mirror.Clear();
                Announce();
                if (inv != null)
                {
                    owner.OnCargoChanged();
                }
                Plugin.Log.LogInfo($"Cargo: unloaded on {reason}: {hands} in hand, {dropped} dropped{(lost > 0 ? $", {lost} LOST (see warnings)" : "")}.");
            }

            // Sets whatever the keeper holds down in front of him, the way the game's own drop does it (drop, then take it
            // off the head), for the empty-hands rule while riding. An item the game refuses to drop stays in hand.
            internal static int SetDownHeld(PlayerData pd)
            {
                if (pd == null || !pd.HasOverheadItem)
                {
                    return 0;
                }
                int n = 0;
                foreach (Item it in new List<Item>(pd.OverheadItems))
                {
                    if (it == null || it.IsEmpty || refusedDrops.Contains(it.UniqueId.ToString()))
                    {
                        continue; // called every frame while riding: an item the game refused is tried once, not 60 times a second
                    }
                    Vector3 pos;
                    if (!SpecialPhysicsCastUtils.GetPlayerDropPosition(pd.position.Value, pd.Direction, out pos))
                    {
                        pos = pd.position.Value;
                    }
                    if (MainGame.Instance.dropSystem.DropItem(it, pd.currentGameSceneId, pos))
                    {
                        pd.RemoveOverheadItem(it);
                        n++;
                    }
                    else
                    {
                        refusedDrops.Add(it.UniqueId.ToString());
                        Plugin.Log.LogWarning($"Mount: the game refused to set {it.id} down; it stays in the keeper's hands.");
                    }
                }
                return n;
            }

            private static readonly HashSet<string> refusedDrops = new HashSet<string>();

            private static bool InHands(PlayerData pd, Item item)
            {
                foreach (Item h in pd.OverheadItems)
                {
                    if (h != null && h.UniqueId == item.UniqueId)
                    {
                        return true;
                    }
                }
                return false;
            }

            // Sets an item down behind the cart: the game's own drop-spot search (SpecialPhysicsCastUtils.
            // GetPlayerDropPosition: rings 0.75 to 1.95 out, fanning 15 degrees either way from the given direction, each
            // spot free of walls, water and other drops and in sight of the anchor) run backwards instead of from the
            // keeper forwards. The spot is the item's centre, so it is put half the item's length (its measured box,
            // CargoLayout.BoxOf, along the way out) plus CargoSetDownGap behind the cart's back end: the gap is between the
            // tailboard and the item's near end whatever its size (a fixed centre distance, the second version, put a log
            // into the cart and a sack well clear of it). The search's nearest ring is 0.75 out, so its anchor is put that
            // much short of the spot (inside the cart for a small item; the cart has no collider to block the sight
            // test); a taken spot moves the item 0.1 further out. MeasureSetDown logs where it really came to rest. The keeper's interaction box sits in front of him, so the
            // item is out of its reach and a second press sets the next item down instead of picking this one up.
            // False (nothing dropped) when there is no cart, no free spot behind it or the game refused the drop; the
            // caller then sets it down in front of the keeper as before.
            private bool DropBehindCart(PlayerData pd, Item item)
            {
                Vector3 box = CargoLayout.BoxOf(item.id); // long, high, across of the mesh; a dropped view is not rotated
                if (!TrySpotBehindCart(pd, box, out Vector3 pos, out Vector3 rear, out Vector3 back, out float half, item.id))
                {
                    return false;
                }
                if (!MainGame.Instance.dropSystem.DropItem(item, pd.currentGameSceneId, pos))
                {
                    return false;
                }
                Plugin.Instance.StartCoroutine(MeasureSetDown(item, rear, back, half));
                return true;
            }

            // A free spot behind the cart for something of this box (long, high, across), in the keeper's sight: the
            // drop search from an anchor so that the thing's near edge lands CargoSetDownGap behind the cart's back end.
            // Used for cargo and for the chest (CartChest.SetDown). False when there is no cart, no free spot, or the spot
            // is out of the keeper's sight; the caller then sets the thing down in front of him.
            internal bool TrySpotBehindCart(PlayerData pd, Vector3 box, out Vector3 pos) => TrySpotBehindCart(pd, box, out pos, out _, out _, out _, "the chest");

            private bool TrySpotBehindCart(PlayerData pd, Vector3 box, out Vector3 pos, out Vector3 rear, out Vector3 back, out float half, string what)
            {
                pos = rear = back = Vector3.zero;
                half = 0f;
                if (!owner.TryCartRear(out rear, out back, out string detail))
                {
                    return false;
                }
                const float NearestRing = 0.75f; // GetPlayerDropPosition's first distance
                half = box == Vector3.zero ? 0.4f : 0.5f * (Mathf.Abs(back.x) * box.x + Mathf.Abs(back.z) * box.z);
                float want = half + owner.S.CargoSetDownGap.Value;
                Vector3 anchor = rear + back * (want - NearestRing);
                if (!SpecialPhysicsCastUtils.GetPlayerDropPosition(anchor, new Vector2(back.x, back.z), out pos))
                {
                    Plugin.Log.LogInfo($"Cargo: no free spot behind the cart for {what} ({detail}); setting it down in front of the keeper.");
                    return false;
                }
                Vector3 off = pos - rear;
                Plugin.Log.LogInfo($"Cargo: set-down spot for {what} {Vector3.Dot(off, back):0.00} behind the cart's back end, {Vector3.Cross(back, off).y:0.00} to the side, wanted {want:0.00} (half length {half:0.00} + gap {owner.S.CargoSetDownGap.Value:0.00}; {detail}).");
                // The cart has no collider and can reach through a fence or wall, and the search only checks the sight
                // line from its anchor. So the spot must also be in sight of the keeper, or the thing could land where he
                // cannot get to it.
                if (!InKeepersSight(pd, pos))
                {
                    Plugin.Log.LogInfo($"Cargo: the spot behind the cart is out of the keeper's sight (behind a wall); setting {what} down in front of him.");
                    return false;
                }
                return true;
            }

            // The drop search's own sight test (0.6 up, walls and buildings mask 3328) from the keeper to a spot.
            internal static bool InKeepersSight(PlayerData pd, Vector3 spot)
            {
                Vector3 eye = pd.position.Value + Vector3.up * 0.6f;
                Vector3 to = spot + Vector3.up * 0.6f - eye;
                return !Physics.Raycast(eye, to.normalized, to.magnitude, 3328, QueryTriggerInteraction.Ignore);
            }

            // A second after the drop (its bounce is over), where the item's drawn meshes really lie relative to the
            // cart's back end, against the half length the spot was worked out with. Log only.
            private static IEnumerator MeasureSetDown(Item item, Vector3 rear, Vector3 back, float half)
            {
                yield return new WaitForSeconds(1f);
                DropView view = null;
                try
                {
                    foreach (GameScene scene in LazyBearTechnology.LazySingleton<GameSceneManager>.Instance.LoadedGameScenes)
                    {
                        if (scene.TryGetDropView(item, out view))
                        {
                            break;
                        }
                    }
                    if (view == null)
                    {
                        yield break;
                    }
                    float near = float.PositiveInfinity, far = float.NegativeInfinity;
                    foreach (Renderer r in view.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled) continue;
                        Bounds b = r.bounds;
                        for (int i = 0; i < 4; i++)
                        {
                            Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, rear.y, (i & 2) == 0 ? b.min.z : b.max.z);
                            float d = Vector3.Dot(c - rear, back);
                            near = Mathf.Min(near, d);
                            far = Mathf.Max(far, d);
                        }
                    }
                    if (far >= near)
                    {
                        Plugin.Log.LogInfo($"Cargo: {item.id} came to rest {near:0.00} to {far:0.00} behind the cart's back end (drawn length {far - near:0.00}; the spot used half length {half:0.00}, i.e. {2f * half:0.00}).");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogInfo("Cargo: could not measure the set-down item: " + e.Message);
                }
            }

            // Sets an item down in front of the keeper with the game's drop routine. False when the game refused the
            // drop (it refuses fuel and tech points, and an empty scene id); the caller then keeps the item somewhere
            // else rather than letting it vanish.
            internal bool DropAtKeeper(PlayerData pd, Item item)
            {
                if (pd == null || item == null || item.IsEmpty)
                {
                    return false;
                }
                Vector3 pos;
                if (!SpecialPhysicsCastUtils.GetPlayerDropPosition(pd.position.Value, pd.Direction, out pos))
                {
                    pos = pd.position.Value;
                }
                if (MainGame.Instance.dropSystem.DropItem(item, pd.currentGameSceneId, pos))
                {
                    return true;
                }
                // The head as the last resort: an item in view beats an item gone. The game's own path, not the
                // reroute (which may be what called here with a full cart), so the flag sends it to the head.
                if (!unloading)
                {
                    HeadReroute.GateAnswersForHead = true;
                    try
                    {
                        pd.AddOverheadItem(item);
                    }
                    finally
                    {
                        HeadReroute.GateAnswersForHead = false;
                    }
                    Plugin.Log.LogWarning($"Cargo: the game refused to drop {item.id} (scene '{pd.currentGameSceneId}'); it went to the keeper's hands.");
                    return true;
                }
                if (RescueZombie(item, pd.currentGameSceneId, pd.position.Value, "the game refused the drop"))
                {
                    return true;
                }
                Plugin.Log.LogWarning($"Cargo: the game refused to drop {item.id} (scene '{pd.currentGameSceneId}'); it is LOST.");
                return false;
            }

            // ------------------------------------------------------------------ views and the packer

            // The drop view's zombie heads (the asset bundles, 2026-10-04: zombieWorkerDropPrefabs holds
            // drops_item_zombie_pose2_t1050..1059 on the keeper's overhead atom the views are cloned from).
            private const int FirstZombieHead = 1050, LastZombieHead = 1059;

            // Shows the item on the view: the item's own mesh, or for a zombie the game's lying zombie (pose 2, as on the
            // ground: the views have isOverhead false) with its head and head colours, as the game draws a carried or
            // dropped zombie (AnimationComponentBase.ApplyOverheadItemToView, DropView). Re-activated only when what it
            // shows changes. Colliders off and the holder's layer again afterwards: a zombie's mesh is instantiated into
            // the view on first use (DropViewAtomMesh.GetCachedZombieElement), after MakeView set them.
            // True when the view was (re)activated, false when it already showed this.
            private bool ActivateFor(DropViewAtomMesh view, Item item)
            {
                viewKeys.TryGetValue(view, out string had);
                string key;
                ZombieWgoData z = IsZombie(item.Definition) ? MainGame.ZombieSystemData?.GetZombie(item.UniqueId) : null;
                if (z != null)
                {
                    int head = z.GetGameResInt("zombie_head_id");
                    string lut = z.GameResStr.Get("zombie_head_lut");
                    if (head < FirstZombieHead || head > LastZombieHead)
                    {
                        Plugin.Log.LogInfo($"Cargo: zombie {z.Name} has head {head}, which the drop view doesn't carry; drawn with head {FirstZombieHead}.");
                        head = FirstZombieHead;
                    }
                    key = "zombie:" + head + ":" + lut;
                    if (had == key)
                    {
                        return false;
                    }
                    Texture2D tex = null;
                    if (!string.IsNullOrEmpty(lut))
                    {
                        try
                        {
                            tex = ZombieCustomizationConfig.GetHeadTextureByName(ZombieSkinHelper.ZOMBIE_WORKER_DATA_ID, lut);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogInfo($"Cargo: zombie {z.Name}'s head colours '{lut}' not found ({e.GetType().Name}); drawn without them.");
                        }
                    }
                    string headId = head.ToString("D4");
                    view.isOverhead = false;
                    view.ActivateZombie(headId, tex);
                    string want = "drops_item_zombie_pose2_t" + headId;
                    if (view.MeshElement == null || view.MeshElement.name != want)
                    {
                        Plugin.Log.LogWarning($"Cargo: the cargo view has no lying zombie '{want}' (shows '{view.MeshElement?.name ?? "nothing"}'); zombie {z.Name} is drawn as the game's placeholder.");
                        string icon = ResolveIcon(item.Definition);
                        view.Activate(icon);
                        key = "icon:" + icon;
                    }
                    else
                    {
                        Plugin.Log.LogInfo($"Cargo: zombie {z.Name} drawn lying on {owner.Describe()} (head {headId}, colours {(string.IsNullOrEmpty(lut) ? "none" : lut)}).");
                    }
                }
                else
                {
                    string icon = ResolveIcon(item.Definition);
                    key = "icon:" + icon;
                    if (had == key && view.IconId == icon)
                    {
                        return false;
                    }
                    view.Activate(icon);
                }
                viewKeys[view] = key;
                view.SetInteractionState(isUnderInteraction: false);
                int layer = container != null ? container.gameObject.layer : view.gameObject.layer;
                foreach (Transform t in view.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    t.gameObject.layer = layer;
                }
                foreach (Collider c in view.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    c.enabled = false;
                }
                return true;
            }

            private class Drawn
            {
                public Item Item;
                public DropViewAtomMesh View;
                public Bounds Box;      // in the view's own space: size, bottom (min.y) and footprint centre
                public int LoadOrder;
            }

            // One mesh copy per cargo item, placed by the size-based packer. Called on every inventory change and when
            // a setting or a layout entry moves.
            public void RefreshViews()
            {
                if (inv == null || donkey == null || !donkey)
                {
                    return;
                }
                try
                {
                    EnsureContainer();
                    if (container == null)
                    {
                        return;
                    }
                    StyleSettings s = owner.S;
                    bool modDrawsCorpse = s.CargoDrawCorpse.Value;

                    // Items the mod draws. Without CargoDrawCorpse the game's shroud stands for the first corpse and the
                    // other items ride on top of it.
                    var drawn = new List<Drawn>();
                    bool gameCorpse = false;
                    bool isolating = IsolationHolds();
                    for (int k = 0; k < inv.Data.Inventory.Count; k++)
                    {
                        Item it = inv.Data.Inventory[k];
                        if (it == null || it.IsEmpty || it.Definition == null)
                        {
                            continue;
                        }
                        if (isolating && it.UniqueId != null && it.UniqueId.ToString() != isolateA && it.UniqueId.ToString() != isolateB)
                        {
                            continue; // the panel looks at two of them
                        }
                        if (it.id == CorpseId && !gameCorpse && !modDrawsCorpse)
                        {
                            gameCorpse = true;
                            continue;
                        }
                        drawn.Add(new Drawn { Item = it, LoadOrder = k });
                    }
                    SetGameLoadedBody(!modDrawsCorpse);

                    // Views: one per drawn item, activated with the item's mesh, then measured.
                    for (int i = 0; i < drawn.Count; i++)
                    {
                        DropViewAtomMesh view = i < views.Count ? views[i] : null;
                        if (view == null)
                        {
                            view = MakeView(i);
                            if (view == null)
                            {
                                return;
                            }
                            if (i < views.Count)
                            {
                                views[i] = view;
                            }
                            else
                            {
                                views.Add(view);
                            }
                        }
                        view.gameObject.SetActive(true);
                        bool fresh = ActivateFor(view, drawn[i].Item);
                        drawn[i].View = view;
                        bool measured = MeasureMeshes(view.transform, view.transform, out drawn[i].Box);
                        if (!measured)
                        {
                            drawn[i].Box = new Bounds(Vector3.zero, new Vector3(0.5f, 0.4f, 0.5f));
                        }
                        if (fresh && IsZombie(drawn[i].Item.Definition))
                        {
                            Plugin.Log.LogInfo($"Cargo: zombie box on {owner.Describe()}: size {drawn[i].Box.size:F2}, centre {drawn[i].Box.center:F2}{(measured ? "" : " (not measured: the default box)")}.");
                        }
                    }
                    for (int j = drawn.Count; j < views.Count; j++)
                    {
                        if (views[j] != null)
                        {
                            views[j].Deactivate();
                            views[j].gameObject.SetActive(false);
                            viewKeys.Remove(views[j]);
                        }
                    }

                    Pack(drawn, s, gameCorpse ? LoadedBodyExtra() : 0f);
                    if (AnyHidden && owner.IsRidden)
                    {
                        for (int i = 0; i < placedViews.Count && i < Placed.Count; i++)
                        {
                            if (placedViews[i] != null && PanelHides(Placed[i], Placed))
                            {
                                placedViews[i].gameObject.SetActive(false); // view only; the next pack shows it again
                            }
                        }
                    }
                    // A chest riding (CartChest): its stand-in on the bed, placed like a lone item, listed for the panel.
                    owner.ChestRig.Layout(container, s.CargoBedDepth.Value, Placed, out string chestNote);
                    if (chestNote != null)
                    {
                        LastFloor = s.CargoBedDepth.Value;
                        LastLayoutNote = chestNote;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Cargo: refreshing the cart views failed: " + e);
                }
            }

            // The packer (CargoPacker.cs, scene-free so tests/PackerScan can run it). Container space: origin at the
            // measured top of the empty cart body, x along the cart, z across (+z away from the camera), y up. Every
            // item: bottom on the floor (bed top + CargoBedDepth), footprint centre at its place. The result is applied
            // to the views (pivot-corrected) and listed for the panel (Placed).
            private static readonly CargoPacker.Lookup LayoutLookup = new CargoPacker.Lookup
            {
                ForItem = CargoLayout.ForItem,
                ForPair = CargoLayout.ForPair,
                FirstIsA = CargoLayout.FirstIsA,
                SameClass = CargoLayout.SameClass,
                BoxOf = CargoLayout.BoxOf,
                ForOnPair = CargoLayout.ForOnPair,
                ShapeOf = ShapeForPacker, // the seat pass (CargoPacker.SeatOnShapes) when the shapes are measured and the switch is on
            };

            private static CargoShapes.Shape ShapeForPacker(string itemId) =>
                Plugin.CargoSeatOnShapes != null && Plugin.CargoSeatOnShapes.Value && CargoShapes.Count > 0 ? CargoShapes.ByClass(CargoSheet.ClassOf(itemId)) : null;

            private void Pack(List<Drawn> drawn, StyleSettings s, float floorExtra)
            {
                Placed.Clear();
                if (drawn.Count == 0)
                {
                    placedViews.Clear();
                    LastLayoutNote = "empty";
                    return;
                }
                var items = new CargoPacker.Item[drawn.Count];
                for (int k = 0; k < drawn.Count; k++)
                {
                    items[k] = new CargoPacker.Item { Id = drawn[k].Item.id, Box = drawn[k].Box.size, LoadOrder = drawn[k].LoadOrder };
                }
                var settings = new CargoPacker.Settings
                {
                    Floor = s.CargoBedDepth.Value + floorExtra,
                    Gap = s.CargoSideGap.Value,
                    Overhang = s.CargoOverhang.Value,
                    Nestle = s.CargoNestle.Value,
                    LayerShift = s.CargoLayerShift.Value,
                    BedWidth = bedWidth,
                    BedLength = bedLength,
                    Forced = isolateRelation ?? CargoLayout.ForcedRelation, // the panel's Alone or the sheet: a pair shown in one relation
                };
                CargoPacker.Result r = CargoPacker.Pack(items, settings, LayoutLookup);
                LastFloor = r.Floor;
                LastPairMode = r.PairMode;
                LastPairTuned = r.PairTuned;
                LastLayoutNote = r.Note;
                placedViews.Clear();
                tintDirty = true; // re-tinted (or cleared) by the next TickTint, in the new order
                for (int i = 0; i < r.Count; i++)
                {
                    Drawn d = drawn[r.Order[i]];
                    Bounds b = d.Box;
                    placedViews.Add(d.View);
                    d.View.transform.localPosition = new Vector3(r.Target[i].x - b.center.x, r.Target[i].y - b.min.y, r.Target[i].z - b.center.z);
                    Placed.Add(new PlacedItem
                    {
                        Id = d.Item.id, OnTop = r.OnTop[i], Layer = r.Layer[i], Alone = r.Count == 1, Box = b.size,
                        Pos = r.Target[i] - new Vector3(0f, r.Floor, 0f),
                        Relation = r.Relation[i], Partner = r.Partner[i], Tuned = r.Tuned[i],
                        Rel = r.Rel(i), LayerPos = r.LayerPos(i), LayerBase = r.LayerBase[i], Guid = d.Item.UniqueId?.ToString(),
                    });
                }
            }

            // ------------------------------------------------------------------ meshes

            private static HashSet<string> elementNames;
            private static readonly Dictionary<string, string> iconFallback = new Dictionary<string, string>
            {
                { "i_marble", "i_stone" }, // marble has no overhead mesh in the game; the slab is the nearest shape
            };

            // The overhead element name to activate for an item: its icon id when an element of that name exists; else
            // an element whose name is a prefix of the icon id (the game's box_engineering_mechanism element is named
            // i_box_engineering_mec, truncated); else a stand-in from the table; else the icon id (the game then shows
            // its placeholder and logs an error, which is the honest outcome for an unknown future item).
            internal static string ResolveIcon(ItemDef def)
            {
                if (def == null)
                {
                    return "";
                }
                HashSet<string> names = ElementNames();
                if (names.Contains(def.iconId))
                {
                    return def.iconId;
                }
                foreach (string n in names)
                {
                    if (n.Length >= 8 && def.iconId.StartsWith(n, StringComparison.Ordinal))
                    {
                        return n;
                    }
                }
                if (iconFallback.TryGetValue(def.iconId, out string alt) && names.Contains(alt))
                {
                    return alt;
                }
                return def.iconId;
            }

            internal static Transform FindOverheadElement(ItemDef def)
            {
                DropViewAtomMesh src = FindSourceAtom();
                if (src == null || def == null)
                {
                    return null;
                }
                string icon = ResolveIcon(def);
                foreach (Transform t in src.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name == icon && t.GetComponent<DropViewAtomMeshElement>() != null)
                    {
                        return t;
                    }
                }
                return null;
            }

            private static HashSet<string> ElementNames()
            {
                if (elementNames != null && elementNames.Count > 0)
                {
                    return elementNames;
                }
                elementNames = new HashSet<string>(StringComparer.Ordinal);
                DropViewAtomMesh src = FindSourceAtom();
                if (src != null)
                {
                    foreach (DropViewAtomMeshElement el in src.GetComponentsInChildren<DropViewAtomMeshElement>(includeInactive: true))
                    {
                        elementNames.Add(el.name);
                    }
                }
                return elementNames;
            }

            // Union of the mesh bounds under root, in the given transform's space, ignoring the root's own local
            // position so the result describes the model itself. Mesh bounds are valid on inactive objects; renderer
            // bounds are not, which is why meshes are used. Shadow planes do not count.
            internal static bool MeasureMeshes(Transform root, Transform space, out Bounds bounds, bool includeInactive = false)
            {
                bounds = default;
                bool any = false;
                Vector3 rootPos = root.localPosition;
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(includeInactive))
                {
                    Mesh mesh = mf.sharedMesh;
                    if (mesh == null || IsShadowMesh(mf.gameObject.name))
                    {
                        continue;
                    }
                    Bounds b = mesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                        Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if (space != root)
                        {
                            p -= rootPos;
                        }
                        if (!any)
                        {
                            bounds = new Bounds(p, Vector3.zero);
                            any = true;
                        }
                        else
                        {
                            bounds.Encapsulate(p);
                        }
                    }
                }
                return any;
            }

            // The game's own naming rule for shadow meshes (Object3DSubMesh.MeshNameFlags): a flat plane under the object
            // that must not count towards its size.
            internal static bool IsShadowMesh(string name)
            {
                return name.EndsWith("_sh") || name.EndsWith("-sh") || name.EndsWith("_sh2") || name.EndsWith("-sh2");
            }

            // allowGame = true: the prefab's own drawer decides (loaded body while a corpse is aboard); false: the empty
            // body stays on and the game's loaded body off, because the mod draws the corpses itself. The drawer only
            // re-evaluates on inventory changes, and this runs after each of them, so the override holds.
            private void SetGameLoadedBody(bool allowGame)
            {
                if (donkey == null)
                {
                    return;
                }
                if (allowGame)
                {
                    foreach (ConditionalDrawer cd in donkey.GetComponentsInChildren<ConditionalDrawer>(includeInactive: true))
                    {
                        cd.UpdateVisualsByConditions();
                    }
                    return;
                }
                foreach (Transform t in donkey.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name == "donkey_cart_body_sway" && t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                    }
                    else if (t.name == "donkey_cart_empty_sway" && !t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(true);
                    }
                }
            }

            // How much higher the loaded cart body (the corpse shroud the game turns on) reaches than the empty bed top,
            // so items put on the cart with a game-drawn corpse ride on top of the shroud. 0 when it is not active.
            private float LoadedBodyExtra()
            {
                if (donkey == null || container == null)
                {
                    return 0f;
                }
                foreach (Transform t in donkey.GetComponentsInChildren<Transform>(includeInactive: false))
                {
                    if (t.name != "donkey_cart_body_sway")
                    {
                        continue;
                    }
                    Renderer best = null;
                    foreach (Renderer r in t.GetComponentsInChildren<Renderer>(includeInactive: false))
                    {
                        if (best == null || r.bounds.max.y > best.bounds.max.y)
                        {
                            best = r;
                        }
                    }
                    if (best == null)
                    {
                        return 0f;
                    }
                    float top = container.parent.InverseTransformPoint(new Vector3(best.bounds.center.x, best.bounds.max.y, best.bounds.center.z)).y;
                    return Mathf.Max(0f, top - container.localPosition.y);
                }
                return 0f;
            }

            // The container sits under the cart's parent ("Base") at the top centre of the empty cart body, measured
            // from its renderer; it is named so Mount lifts it with the other cart parts on slopes.
            private void EnsureContainer()
            {
                if (container != null && container)
                {
                    return;
                }
                container = null;
                Transform emptySway = null, emptyBody = null;
                foreach (Transform t in donkey.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t.name == "donkey_cart_empty_sway")
                    {
                        emptySway = t;
                    }
                    else if (t.name == "donkey_cart_empty")
                    {
                        emptyBody = t;
                    }
                    else if (t.name == ContainerName)
                    {
                        container = t;
                    }
                }
                if (container != null)
                {
                    return;
                }
                if (emptySway == null || emptySway.parent == null)
                {
                    Plugin.Log.LogWarning("Cargo: the cart body was not found; cargo views off.");
                    return;
                }
                Transform parent = emptySway.parent;
                var go = new GameObject(ContainerName);
                Live.CargoHolders++;
                container = go.transform;
                container.SetParent(parent, worldPositionStays: false);
                container.localRotation = Quaternion.identity;
                container.localScale = Vector3.one;
                Renderer r = (emptyBody ?? emptySway).GetComponentInChildren<Renderer>(includeInactive: true);
                if (r != null)
                {
                    Bounds b = r.bounds;
                    Vector3 top = parent.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                    container.localPosition = top;
                    bedWidth = Mathf.Max(0.3f, b.size.z / Mathf.Max(0.01f, Mathf.Abs(parent.lossyScale.z)));
                    bedLength = Mathf.Max(0.3f, b.size.x / Mathf.Max(0.01f, Mathf.Abs(parent.lossyScale.x)));
                    Plugin.Log.LogInfo($"Cargo: bed top at local {top}, bed width {bedWidth:0.00}, length {bedLength:0.00} (cart body bounds centre {b.center}, size {b.size}).");
                }
                else
                {
                    container.localPosition = emptySway.localPosition + Vector3.up * 0.3f;
                    Plugin.Log.LogInfo("Cargo: no renderer on the cart body, bed guessed 0.3 above it.");
                }
                go.layer = (emptyBody ?? emptySway).gameObject.layer;
            }

            private DropViewAtomMesh MakeView(int index)
            {
                DropViewAtomMesh source = FindSourceAtom();
                if (source == null)
                {
                    Plugin.Log.LogWarning("Cargo: the keeper's overhead mesh atom was not found; cargo views off.");
                    return null;
                }
                DropViewAtomMesh view = UnityEngine.Object.Instantiate(source, container);
                view.name = $"cargo_{index}";
                view.transform.SetParent(container, worldPositionStays: false);
                view.transform.localRotation = Quaternion.identity;
                view.transform.localScale = Vector3.one;
                view.isOverhead = false;
                int layer = container.gameObject.layer;
                foreach (Transform t in view.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    t.gameObject.layer = layer;
                }
                foreach (Collider c in view.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    c.enabled = false;
                }
                return view;
            }

            internal static DropViewAtomMesh FindSourceAtom()
            {
                PlayerController pc = MainGame.PlayerController;
                if (pc == null)
                {
                    return null;
                }
                DropViewAtomMesh first = null;
                foreach (DropViewAtomMesh atom in pc.GetComponentsInChildren<DropViewAtomMesh>(includeInactive: true))
                {
                    if (atom.transform.parent != null && atom.transform.parent.name == "overhead")
                    {
                        return atom;
                    }
                    if (first == null)
                    {
                        first = atom;
                    }
                }
                return first;
            }

            // For the panel and the geometry log.
            public string Summary()
            {
                if (inv == null)
                {
                    return "cargo: none";
                }
                var l = new List<string>();
                foreach (Item it in inv.Data.Inventory)
                {
                    l.Add(it.id);
                }
                return $"cargo {inv.Data.Inventory.Count}/{inv.Data.InventorySize} [{string.Join(" ", l)}]; layout: {LastLayoutNote}; bed {bedLength:0.00} long × {bedWidth:0.00} across";
            }
        }
    }
}
