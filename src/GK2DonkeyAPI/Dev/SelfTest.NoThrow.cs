using System;
using System.Collections;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Between mods nothing throws into another mod's Awake (docs/multi-mod-loading.md section 3): an effect and a code
    // skin meet the version rule as mounts do (the loser left out with a warning, its handle doing nothing), a mod's own
    // look of a mount that lost is left out with it, an effect id needs its mod's prefix (a mistake in the call still
    // throws), and a row's function that throws is taken as its fallback.
    internal static partial class SelfTest
    {
        internal static IEnumerator NoThrowBetweenMods()
        {
            const string oA = Owner + ".nothrow-a", oB = Owner + ".nothrow-b", oC = Owner + ".nothrow-c";
            const string fx = "selftest.nothrow-fx", mount = "selftest.nothrow-mount", skin = "donkey/selftest.nothrow-skin";
            IDisposable fxA = null, fxB = null, fxC = null, skA = null, skB = null, mA = null, mB = null, lookB = null;
            MountEffectDef Fx(string version)
            {
                var d = new MountEffectDef { Id = fx, Name = "No Throw", Version = version };
                var part = new MountEffectPart { Source = Mounts.Effects.GameParticles("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat") };
                part.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 1, Radius = 0.5f, Size = 1f });
                d.Parts.Add(part);
                return d;
            }
            Exception Try(Func<IDisposable> f, out IDisposable h)
            {
                h = null;
                try { h = f(); return null; } catch (Exception e) { return e; }
            }
            try
            {
                Check(Try(() => Mounts.Effects.Register(oA, Fx("1.0")), out fxA) == null && GK2DonkeyAPI.Effects.Find(fx)?.Owner == oA, "an effect of a mod registered");
                Info("the next two Effects warnings (\"is left out\", \"replaces the one of\") are this test's own");
                Exception e = Try(() => Mounts.Effects.Register(oB, Fx("1.0")), out fxB);
                Check(e == null && GK2DonkeyAPI.Effects.Find(fx)?.Owner == oA, $"another mod's effect of the same id, not newer: left out, no throw ({e?.Message ?? "none"})");
                fxB?.Dispose();
                Check(GK2DonkeyAPI.Effects.Find(fx)?.Owner == oA, "... and its handle does nothing");
                e = Try(() => Mounts.Effects.Register(oC, Fx("2.0")), out fxC);
                Check(e == null && GK2DonkeyAPI.Effects.Find(fx)?.Owner == oC, "a newer one replaces it");
                var bare = Fx("1.0");
                bare.Id = "nothrow-fx";
                e = Try(() => Mounts.Effects.Register(oA, bare), out _);
                Check(e is ArgumentException, $"an effect id without its mod's prefix is a mistake in the call: refused ({e?.Message})");

                Check(Try(() => Mounts.Looks.Register(oA, new MountSkinDef { Id = "selftest.nothrow-skin", Name = "A", Version = "1.0", Source = new NoPictures() }), out skA) == null, "a code skin of a mod registered");
                Info("the next \"is already registered\" warning is this test's own: another mod's code skin of the same id");
                e = Try(() => Mounts.Looks.Register(oB, new MountSkinDef { Id = "selftest.nothrow-skin", Name = "B", Version = "1.0", Source = new NoPictures() }), out skB);
                Check(e == null && Looks.Find(skin)?.Owner == oA, $"another mod's code skin of the same id, not newer: left out, no throw ({e?.Message ?? "none"})");

                Info("the next two warnings (\"is left out\" for the mount and its own look) are this test's own");
                mA = Mounts.Catalog.Register(oA, new MountDef { Id = mount, Name = "No Throw A", Version = "2.0" });
                mB = Mounts.Catalog.Register(oB, new MountDef { Id = mount, Name = "No Throw B", Version = "1.0" });
                Check(Catalog.Find(mount)?.Owner == oA && Catalog.Lost(mount, oB), "a mount that loses the version rule is noted as lost");
                e = Try(() => Mounts.Looks.Register(oB, new MountSkinDef { Id = Looks.DefaultSkinName, Name = "B's own look", Mount = mount, Source = new NoPictures() }), out lookB);
                Check(e == null && Looks.Find(mount + "/" + Looks.DefaultSkinName) == null, $"its mod's own look of it is left out with it, no throw ({e?.Message ?? "none"})");

                Info("the next \"its Visible threw\" warning is this test's own");
                Func<bool> guarded = MountSettingsPage.Guard(() => throw new InvalidOperationException("the self-test's"), true, "the self-test's row", "Visible");
                bool got = false;
                Check(Try(() => { got = guarded(); return null; }, out _) == null && got, "a row's Visible that throws is taken as true, never thrown on");
            }
            finally
            {
                foreach (IDisposable h in new[] { lookB, mB, mA, skB, skA, fxC, fxB, fxA }) { try { h?.Dispose(); } catch { } }
            }
            yield break;
        }
    }
}
