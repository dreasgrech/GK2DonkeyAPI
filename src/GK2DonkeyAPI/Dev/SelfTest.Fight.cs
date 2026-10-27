using System.Collections;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Fights from the saddle (docs/mounted-combat.md) as self-tests: the parts that need no real fight. The keeper's
    // armour view is switched with the game's own PlayerController.SetArmorView, as the fights do; the attack state is
    // entered the way the input handler enters it (Ssm.ForceEnterState) with the view's sword equipped, no item
    // touched; build mode is simulated with the control flag the game's BuildManager sets. Every scenario puts the
    // armour, the weapon and the control flag back, also when it fails early. Needs body armour on the tool belt
    // (SetArmorView would otherwise move one there from the inventory, or log the game's "No armor" warning): without
    // it the armour scenarios skip with a warning. What still needs the real fight: the look, the sounds, hits, being
    // hit, death, the fight's end, the strafe (docs/self-tests.md, "Not automated").
    internal static partial class SelfTest
    {
        internal const string GroupFight = "Fights from the saddle";
        internal const string NotTheCart = "Not the cart in a fight.";
        internal const float WeaponRestAboveBody = 0.632f; // main_character at 0.6 + Weapon Container at 0.032, the probe's dump

        internal static bool ArmourOnBelt()
        {
            try
            {
                return !MainGame.PlayerData.toolBeltInventory.GetItemByType(ItemType.BodyArmor).IsEmpty;
            }
            catch
            {
                return false;
            }
        }

        internal static Animator KeeperAnimator() => PC?.View?.PlayerAnimation?.Animator;

        internal static int KeeperState()
        {
            Animator a = KeeperAnimator();
            return a != null ? a.GetInteger(AnimationComponentBase.idStateAnimator) : -1;
        }

        internal static SpriteRenderer KeeperLayer(string path)
        {
            Transform t = KeeperAnimator()?.transform.Find(path);
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }

        internal static string SpriteName(SpriteRenderer r) => r != null && r.sprite != null ? r.sprite.name : "none";

        internal static void ArmourView(bool on)
        {
            try
            {
                PC.SetArmorView(on);
            }
            catch (System.Exception e)
            {
                Fail($"SetArmorView({on}) threw: {e.Message}");
            }
        }

        // The checks shared by the armour scenarios: the pose the armour has frames for, the body drawn with an armour
        // sprite, the rod gone, the weapons at their rest height over the physical body.
        internal static void CheckArmouredRider(string when)
        {
            int state = KeeperState();
            Check(state == (int)AnimationState.Idle || state == (int)AnimationState.Walk, $"{when}: the animator's State is Idle or Walk ({state})");
            SpriteRenderer bdy = KeeperLayer("gfx/bdy");
            Check(bdy != null && bdy.enabled && bdy.sprite != null && !bdy.sprite.name.StartsWith("1001_bdy_fishing", System.StringComparison.Ordinal),
                $"{when}: the body is drawn with an armour frame ({SpriteName(bdy)}, {(bdy != null && bdy.enabled ? "on" : "off")})");
            SpriteRenderer rod = KeeperLayer("gfx/tol/tol_rod");
            Check(rod == null || !rod.gameObject.activeInHierarchy || rod.sprite == null, $"{when}: no fishing rod ({SpriteName(rod)})");
            Transform weapons = KeeperAnimator()?.transform.Find("Weapon Container");
            if (weapons != null)
            {
                float rest = PC.PhysicalBody.transform.position.y + WeaponRestAboveBody;
                Check(Mathf.Abs(weapons.position.y - rest) < 0.05f, $"{when}: the weapon container is at its rest height over the body ({weapons.position.y:F3}, rest {rest:F3})");
            }
            else
            {
                Warn($"{when}: no Weapon Container under the animator root");
            }
        }

        // The armour goes on and off while riding (the quest's talk, the fight's end): the ride goes on, the pose
        // follows the armour, the body is never a blue shape.
        internal static IEnumerator FightArmourWhileRiding()
        {
            if (!ArmourOnBelt())
            {
                Warn("no body armour on the tool belt: skipped (put the armour on the belt)");
                yield break;
            }
            MountHandle h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                yield return Until(() => h.IsReady, 6f);
                MountResult ride = h.Ride(showMessage: false);
                Check(ride.Ok, $"ridden{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok) yield break;
                yield return Frames(4);
                int poseBefore = KeeperState();
                ArmourView(true);
                yield return Frames(4);
                Check(Mounts.IsMounted, "the armour going on did not end the ride");
                Check(Mount.Instance.InArmour(PC), "the mod sees the armour view");
                CheckArmouredRider("armour on while riding");
                ArmourView(false);
                yield return Frames(4);
                Check(Mounts.IsMounted, "the armour going off did not end the ride");
                Check(KeeperState() == poseBefore, $"the riding pose is back ({KeeperState()}, was {poseBefore})");
                SpriteRenderer bdy = KeeperLayer("gfx/bdy");
                Check(bdy != null && bdy.enabled && bdy.sprite != null, $"the body is drawn again ({SpriteName(bdy)})");
            }
            finally
            {
                if (Mount.Instance.InArmour(PC)) ArmourView(false);
            }
        }

        // Mounting in armour: the ride works, every way to a cart is refused with the same words (a donkey with its
        // cart, the style switch, a hitch from the saddle, a hitch on foot).
        internal static IEnumerator FightMountInArmour()
        {
            if (!ArmourOnBelt())
            {
                Warn("no body armour on the tool belt: skipped (put the armour on the belt)");
                yield break;
            }
            Vector3 near = Spot(home, 3f) ?? home;
            MountHandle withCart = Spawn(near, right: homeFacing > 0f, withCart: true);
            MountHandle plain = Spawn(near + new Vector3(0f, 0f, 2.5f), right: homeFacing > 0f);
            MountHandle freeCart = Spawn(near + new Vector3(3f, 0f, 0f), right: homeFacing > 0f, donkey: false);
            if (withCart == null || plain == null || freeCart == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                yield return Until(() => withCart.IsReady && plain.IsReady && freeCart.IsReady, 6f);
                ArmourView(true);
                yield return Frames(3);
                MountResult cart = withCart.Ride(showMessage: false);
                Check(!cart.Ok && cart.Reason == NotTheCart, $"a donkey with its cart refused in armour ({cart.Reason})");
                MountResult hitchOnFoot = Mounts.Hitch(plain, freeCart);
                Check(!hitchOnFoot.Ok && hitchOnFoot.Reason == NotTheCart, $"a hitch on foot refused in armour ({hitchOnFoot.Reason})");
                MountResult ride = plain.Ride(showMessage: false);
                Check(ride.Ok, $"a plain donkey ridden in armour{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok) yield break;
                yield return Frames(4);
                CheckArmouredRider("mounted in armour");
                MountResult sw = Mounts.SwitchStyle(MountStyle.Cart, showMessage: false);
                Check(!sw.Ok && sw.Reason == NotTheCart, $"the style switch to Cart refused ({sw.Reason})");
                MountResult hitch = Mounts.Hitch(plain, freeCart);
                Check(!hitch.Ok && hitch.Reason == NotTheCart, $"a hitch from the saddle refused ({hitch.Reason})");
                Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride, "still riding, plain");
                Mounts.Dismount("self-test");
                yield return Frames(2);
                ArmourView(false);
                yield return Frames(2);
                MountResult hitchAfter = Mounts.Hitch(plain, freeCart);
                Check(hitchAfter.Ok, $"the same hitch works once the armour is off{(hitchAfter.Ok ? "" : " (" + hitchAfter.Reason + ")")}");
            }
            finally
            {
                if (Mount.Instance.InArmour(PC)) ArmourView(false);
            }
        }

        // The armour goes on while driving (the quest's talk with the cart hitched): the cart is left standing by the
        // keeper, the ride goes on without it.
        internal static IEnumerator FightArmourWhileDriving()
        {
            if (!ArmourOnBelt())
            {
                Warn("no body armour on the tool belt: skipped (put the armour on the belt)");
                yield break;
            }
            MountHandle h = Spawn(Spot(home, 4f) ?? home, right: homeFacing > 0f, withCart: true);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                yield return Until(() => h.IsReady, 6f);
                MountResult ride = h.Ride(showMessage: false);
                Check(ride.Ok && Mounts.Style == MountStyle.Cart, $"driving the cart{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok) yield break;
                yield return Frames(4);
                ArmourView(true);
                yield return Until(() => Mounts.IsMounted && h.IsReady && !h.HasCart, 5f);
                Check(Mounts.IsMounted, "the ride went on");
                Check(Mounts.Style == MountStyle.Ride && !h.HasCart, $"without the cart (style {Mounts.Style}, has cart {h.HasCart})");
                MountHandle left = null;
                foreach (MountHandle c in Mounts.InstancesOf(Owner))
                {
                    if (c.Exists && c.HasCart && !c.HasDonkey) left = c;
                }
                Check(left != null, "the cart stands as a free cart");
                if (left != null)
                {
                    float d = Vector2.Distance(new Vector2(left.Position.x, left.Position.z), new Vector2(Keeper().x, Keeper().z));
                    Check(d < 4f, $"the cart stands by the keeper ({d:F1} away)");
                }
                yield return Frames(3);
                CheckArmouredRider("armour on while driving");
            }
            finally
            {
                if (Mount.Instance.InArmour(PC)) ArmourView(false);
            }
        }

        // A sword attack from the saddle: the attack state does not end the ride, and the ride is still on when the
        // free state is back.
        internal static IEnumerator FightAttackRides()
        {
            if (!ArmourOnBelt())
            {
                Warn("no body armour on the tool belt: skipped (put the armour on the belt)");
                yield break;
            }
            MountHandle h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            bool equipped = false;
            try
            {
                yield return Until(() => h.IsReady, 6f);
                MountResult ride = h.Ride(showMessage: false);
                Check(ride.Ok, $"ridden{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok) yield break;
                ArmourView(true);
                yield return Frames(3);
                PC.AttackComponent.EquipWeapon(ItemType.Sword);
                equipped = PC.AttackComponent.HasEquippedWeapon;
                Check(equipped, "the view's sword equipped (no item touched)");
                if (!equipped) yield break;
                yield return Frames(2);
                Check(Mounts.IsMounted, "equipping did not end the ride");
                PC.Ssm.ForceEnterState<AttackSwordDefaultPlayerState>();
                yield return Frames(3);
                SSMState cur = PC.Ssm?.CurState;
                Check(Mounts.IsMounted, "the attack state did not end the ride");
                Check(cur != null && cur.GetType().Name.StartsWith("Attack", System.StringComparison.Ordinal), $"in an attack state ({cur?.GetType().Name ?? "none"})");
                yield return Until(() => PC.Ssm?.CurState is FreePlayerState, 5f);
                Check(waited, $"the attack ended by itself ({PC.Ssm?.CurState?.GetType().Name ?? "none"})");
                Check(Mounts.IsMounted, "still riding after the attack");
                CheckArmouredRider("after the attack");
            }
            finally
            {
                if (equipped)
                {
                    try { PC.AttackComponent.UnequipWeapon(); } catch { }
                }
                if (Mount.Instance.InArmour(PC)) ArmourView(false);
            }
        }

        // Build mode (placing a barricade from the command post in a pre-fight) takes control ByBuilding: the ride ends
        // with that reason, as the rules say, and nothing else breaks.
        internal static IEnumerator FightBuildModeDismounts()
        {
            MountHandle h = Spawn(Spot(home, 3f) ?? home, right: homeFacing > 0f);
            if (h == null)
            {
                Fail("could not spawn");
                yield break;
            }
            string reason = null;
            System.Action<string> onDismounted = r => reason = r;
            Mounts.Dismounted += onDismounted;
            bool taken = false;
            try
            {
                yield return Until(() => h.IsReady, 6f);
                MountResult ride = h.Ride(showMessage: false);
                Check(ride.Ok, $"ridden{(ride.Ok ? "" : " (" + ride.Reason + ")")}");
                if (!ride.Ok) yield break;
                yield return Frames(3);
                PC.SetControlTakenType(TakenControlType.ByBuilding, isEnabled: false);
                taken = true;
                yield return Frames(3);
                Check(!Mounts.IsMounted, "build mode ended the ride");
                Check(reason == "control taken ByBuilding", $"with the reason \"control taken ByBuilding\" ({reason ?? "none"})");
                PC.SetControlTakenType(TakenControlType.ByBuilding, isEnabled: true);
                taken = false;
                yield return Frames(2);
                Check(PC.IsControlEnabledByType(TakenControlType.ByBuilding), "control given back");
                MountResult again = h.Ride(showMessage: false);
                Check(again.Ok, $"ridden again afterwards{(again.Ok ? "" : " (" + again.Reason + ")")}");
            }
            finally
            {
                Mounts.Dismounted -= onDismounted;
                if (taken)
                {
                    try { PC.SetControlTakenType(TakenControlType.ByBuilding, isEnabled: true); } catch { }
                }
            }
        }
    }
}
