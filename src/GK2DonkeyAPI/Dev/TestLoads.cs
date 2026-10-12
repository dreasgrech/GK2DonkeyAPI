using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Test loads for tuning the cargo layout (docs/cargo-coverage.md): puts a load of size classes straight onto the
    // ridden cart, so a contact can be tuned without finding its items in the world. The items are made the way the
    // game makes them (a corpse from the delivery script's body definition, a zombie with the game's own zombie drop
    // and linked like any carried zombie, everything else from its id), so they are real cargo: set down, they are
    // normal world items, and a save keeps them.
    // Safety (the user: items must never be lost):
    // - A new test load only ever replaces a test load: while the cart holds anything else, it is refused.
    // - Taking it off removes only test items that are on this cart right now, and a test zombie only together with its
    //   own item just taken off (a zombie set down and put to work elsewhere is never touched: it is not on the cart).
    // - Which items are test items is kept in BepInEx/GK2-test-loads.txt, so a test load left on the cart can still be
    //   taken off after a restart. An item that is no longer on the cart (set down, carried elsewhere) is forgotten:
    //   forgetting only ever turns a test item into ordinary cargo.
    internal static class TestLoads
    {
        private static readonly HashSet<Guid> made = new HashSet<Guid>();
        private static bool loaded;

        public static string Status { get; private set; } = "";

        internal static void Note(string text) => Status = text;

        private static string FilePath => Path.Combine(Paths.BepInExRootPath, "GK2-test-loads.txt");

        private static void LoadFile()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (Guid.TryParse(line.Trim(), out Guid g)) made.Add(g);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[testload] reading " + FilePath + " failed (test loads made before are ordinary cargo now): " + e.Message);
            }
        }

        private static void SaveFile()
        {
            try
            {
                var lines = new List<string> { "# GK2 Donkey API: the items of the test load on the cart (Cargo tab, Load). Delete this file to treat them as ordinary cargo." };
                foreach (Guid g in made) lines.Add(g.ToString());
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[testload] writing " + FilePath + " failed: " + e.Message);
            }
        }

        private static Inventory CartInventory() => Mount.Donkey != null && Mount.CurrentStyle == MountStyle.Cart && Cargo.Attached ? Mount.Donkey.Data.Inventory : null;

        // Test items no longer on this cart are forgotten (the safe direction: they become ordinary cargo).
        private static void Prune(Inventory inv)
        {
            var on = new HashSet<Guid>();
            foreach (Item it in inv.Data.Inventory) if (it != null && !it.IsEmpty) on.Add(it.UniqueId.Guid);
            int before = made.Count;
            made.IntersectWith(on);
            if (made.Count != before) SaveFile();
        }

        // The ridden cart's inventory changed (Cargo.Rig.OnChanged): a test item that left it is forgotten at once, so one
        // set down, used and loaded again later is ordinary cargo and never taken off (a review found it was remembered).
        internal static void CartChanged(Inventory inv)
        {
            LoadFile();
            if (made.Count == 0 || inv == null || inv != CartInventory()) return;
            Prune(inv);
        }

        // How many of the cart's items are test items (for the panel's Take off button).
        internal static int OnCart()
        {
            LoadFile();
            Inventory inv = CartInventory();
            if (inv == null || made.Count == 0) return 0;
            int n = 0;
            foreach (Item it in inv.Data.Inventory) if (it != null && !it.IsEmpty && made.Contains(it.UniqueId.Guid)) n++;
            return n;
        }

        // classes: one item id per item of the load (class representatives, CargoSheet.ZombieRep for a zombie).
        public static string Put(IList<string> classes, string why)
        {
            try
            {
                LoadFile();
                if (CargoSheet.Running) return Status = "a sheet run is in progress";
                Inventory inv = CartInventory();
                if (inv == null) return Status = "ride in Cart style first";
                if (classes == null || classes.Count == 0) return Status = "an empty load";
                if (classes.Count > CargoSheet.CoverageMaxItems) return Status = $"at most {CargoSheet.CoverageMaxItems} items";
                foreach (Item it in inv.Data.Inventory)
                {
                    if (it != null && !it.IsEmpty && !made.Contains(it.UniqueId.Guid))
                    {
                        return Status = "the cart holds cargo that is not a test load: set it down first (a test load only ever replaces a test load)";
                    }
                }
                string off = TakeOffInner(inv);
                // room for the whole load whatever CartSlots says (back to the setting when the test load is taken off)
                Cargo.SlotFloor = Mathf.Max(Cargo.SlotFloor, classes.Count);
                Cargo.Current?.ApplySlots();
                var names = new List<string>();
                foreach (string id in classes)
                {
                    Item item = Make(id, out string note, out ZombieWgoData zombie);
                    if (item == null)
                    {
                        names.Add(id + " (" + note + ")");
                        continue;
                    }
                    if (!inv.AddItemToInventory(item))
                    {
                        // refused: nothing may be left behind (a zombie without its item anywhere)
                        if (zombie != null) MainGame.ZombieSystemData.RemoveZombie(zombie.UniqueId);
                        names.Add(CargoSheet.ClassLabel(id) + " (refused by the cart)");
                        continue;
                    }
                    made.Add(item.UniqueId.Guid);
                    if (zombie != null)
                    {
                        HeadReroute.RelinkZombie(inv, item, "a test load");
                    }
                    names.Add(CargoSheet.ClassLabel(id));
                }
                SaveFile();
                Mount.OnCargoChanged();
                Cargo.RefreshViews();
                int on = OnCart();
                string msg = $"on the cart: {string.Join(" + ", names)} ({on} of {classes.Count}){(why != null ? " for " + why : "")}";
                Plugin.Log.LogInfo("[testload] " + msg + (off.Length > 0 ? "; " + off : ""));
                return Status = msg;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[testload] putting a test load on the cart failed: " + e);
                return Status = "failed: " + e.Message;
            }
        }

        public static string TakeOff()
        {
            try
            {
                LoadFile();
                if (CargoSheet.Running) return Status = "a sheet run is in progress";
                Inventory inv = Mount.Donkey != null && Cargo.Attached ? Mount.Donkey.Data.Inventory : null;
                if (inv == null) return Status = "ride in Cart style first";
                string msg = TakeOffInner(inv);
                Cargo.SlotFloor = 0;
                Mount.ApplyCartSlots("the test load was taken off");
                Mount.OnCargoChanged();
                Cargo.RefreshViews();
                if (msg.Length == 0) msg = "no test items on the cart";
                Plugin.Log.LogInfo("[testload] " + msg);
                return Status = msg;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[testload] taking the test load off failed: " + e);
                return Status = "failed: " + e.Message;
            }
        }

        // Removes the test items on this cart; a zombie only with its own item just removed here, and only when it is
        // nowhere else now. Then forgets test items that are not on the cart.
        private static string TakeOffInner(Inventory inv)
        {
            int removed = 0, gone = 0, kept = 0;
            foreach (Item it in new List<Item>(inv.Data.Inventory))
            {
                if (it == null || it.IsEmpty || !made.Contains(it.UniqueId.Guid)) continue;
                bool zombie = Cargo.IsZombie(it.Definition);
                if (!inv.RemoveItemFromInventoryByUID(it)) continue;
                made.Remove(it.UniqueId.Guid);
                removed++;
                if (!zombie) continue;
                try
                {
                    ZombieWgoData z = FindZombie(it.UniqueId.Guid);
                    if (z == null) continue;
                    if (CargoSheet.FoundOutsideSheet(z) != null)
                    {
                        kept++;
                        continue;
                    }
                    MainGame.ZombieSystemData.RemoveZombie(z.UniqueId);
                    gone++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("[testload] removing a test zombie failed: " + e.Message);
                }
            }
            Prune(inv);
            SaveFile();
            if (removed == 0) return "";
            return $"took {removed} test item(s) off the cart{(gone > 0 ? $", {gone} test zombie(s) removed" : "")}{(kept > 0 ? $", {kept} zombie(s) left (found elsewhere)" : "")}";
        }

        // A carried zombie's item shares its zombie's id.
        private static ZombieWgoData FindZombie(Guid g)
        {
            List<ZombieWgoData> all = MainGame.ZombieSystemData?.zombieDrops;
            if (all == null) return null;
            foreach (ZombieWgoData z in all)
            {
                if (z != null && (z.UniqueId?.Guid == g || (z.ZombieItem != null && z.ZombieItem.UniqueId?.Guid == g))) return z;
            }
            return null;
        }

        private static Item Make(string id, out string note, out ZombieWgoData zombie)
        {
            note = null;
            zombie = null;
            if (id == CargoSheet.ZombieRep)
            {
                PlayerData pd = MainGame.PlayerData;
                if (pd == null || MainGame.ZombieSystemData == null)
                {
                    note = "no game loaded";
                    return null;
                }
                Item body = CarryProbe.NewZombieBody(out string how);
                if (body == null)
                {
                    note = "no body definition to make a zombie from";
                    return null;
                }
                zombie = MainGame.ZombieSystemData.CreateZombieDrop("zombie", pd.position.Value, pd.currentGameSceneId, body);
                return zombie.ZombieItem;
            }
            ItemDef def = GameBalance.Me.GetDataOrNull<ItemDef>(id);
            if (def == null)
            {
                note = "unknown item";
                return null;
            }
            if (def.itemGroupIds.Contains("body"))
            {
                BodyDef body = GameBalance.Me.GetDataOrNull<BodyDef>("body_0_1");
                if (body != null) return body.GenerateItem();
            }
            return new Item(id);
        }
    }
}
