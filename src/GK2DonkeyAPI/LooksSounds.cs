using System;
using System.Collections.Generic;

namespace GK2DonkeyAPI
{
    // The sound blocks of a skin ([Sounds], [Sounds.Ride], [Sounds.Cart], [Sounds.Baby]) and of a mount ([Mount.Sounds]
    // and its style and baby sections), parsed from the raw lines the loader kept, once per change of the catalogue
    // (the Mounts tab's edits and a reload bump Catalog.Version). The raw lines stay on the def for SaveCfg.
    // docs/mount-sounds-design.md.
    internal static partial class Looks
    {
        internal sealed class SoundBlocks
        {
            internal int Version = -1;
            internal bool FromCode;   // a code skin's or mount's blocks (the API's MountSound): never parsed again
            internal SoundBlock Both, Ride, Cart, Baby;

            internal SoundBlock For(SoundLayer layer)
            {
                switch (layer)
                {
                    case SoundLayer.Ride: return Ride;
                    case SoundLayer.Cart: return Cart;
                    case SoundLayer.Baby: return Baby;
                    default: return Both;
                }
            }
        }

        // Which block of a def: both styles, one style, or the babies' (an instance drawn smaller than the grown one).
        internal enum SoundLayer { Both, Ride, Cart, Baby }

        internal static SoundBlocks BlocksFromApi(IDictionary<string, Api.MountSound> both, IDictionary<string, Api.MountSound> ride, IDictionary<string, Api.MountSound> cart, IDictionary<string, Api.MountSound> baby, string section, string who)
        {
            return new SoundBlocks
            {
                FromCode = true,
                Both = SoundBlock.FromApi(both, section, who),
                Ride = SoundBlock.FromApi(ride, section + ".Ride", who),
                Cart = SoundBlock.FromApi(cart, section + ".Cart", who),
                Baby = SoundBlock.FromApi(baby, section + ".Baby", who),
            };
        }

        // The skin's block for a layer (a line in a style's or the babies' block wins over [Sounds]; the rig applies
        // the both-styles block first, then the style's, then the babies' for a baby).
        internal static SoundBlock SkinSoundBlock(SkinDef d, SoundLayer layer)
        {
            if (d == null) return null;
            SoundBlocks b = d.SoundBlocks;
            if (b == null || !b.FromCode && b.Version != Catalog.Version)
            {
                b = d.SoundBlocks = new SoundBlocks { Version = Catalog.Version };
                b.Both = SoundBlock.Parse(d.Sounds, "Sounds", d.FileNames, null);
                b.Both.AddConvention(d.FileNames, "Sounds", null);
                b.Ride = SoundBlock.Parse(d.SoundsRide, "Sounds.Ride", d.FileNames, null);
                b.Cart = SoundBlock.Parse(d.SoundsCart, "Sounds.Cart", d.FileNames, null);
                b.Baby = SoundBlock.Parse(d.SoundsBaby, "Sounds.Baby", d.FileNames, null);
            }
            return b.For(layer);
        }

        // The mount's block for a layer. A pack mount's files are the files of the skin that declared it (the block
        // sits in that skin's cfg); a code mount's lines carry their own clips.
        internal static SoundBlock MountSoundBlock(Catalog.Entry k, SoundLayer layer)
        {
            if (k == null) return null;
            SoundBlocks b = k.SoundBlocks;
            if (b == null || !b.FromCode && b.Version != Catalog.Version)
            {
                SkinDef declaring = k.DeclaredBy != null ? Find(k.DeclaredBy) : null;
                List<string> files = declaring?.FileNames;
                b = k.SoundBlocks = new SoundBlocks { Version = Catalog.Version };
                b.Both = SoundBlock.Parse(Lines(k.Sounds), "Mount.Sounds", files, null);
                b.Ride = SoundBlock.Parse(Lines(k.SoundsRide), "Mount.Sounds.Ride", files, null);
                b.Cart = SoundBlock.Parse(Lines(k.SoundsCart), "Mount.Sounds.Cart", files, null);
                b.Baby = SoundBlock.Parse(Lines(k.SoundsBaby), "Mount.Sounds.Baby", files, null);
            }
            return b.For(layer);
        }

        // The skin whose folder a mount's file lines read from: the declaring one (a pack mount).
        internal static SkinDef MountSoundFileOwner(Catalog.Entry k) => k?.DeclaredBy != null ? Find(k.DeclaredBy) : null;

        private static IEnumerable<KeyValuePair<string, string>> Lines(Dictionary<string, object> raw)
        {
            if (raw == null) yield break;
            foreach (KeyValuePair<string, object> kv in raw)
            {
                if (kv.Value is string s) yield return new KeyValuePair<string, string>(kv.Key, s);
            }
        }

        // The blocks parsed for the loader's report (FromCfg): every problem named once in the skin's report.
        internal static int ReportSoundBlocks(SkinDef d, Dictionary<string, object> mountBoth, Dictionary<string, object> mountRide, Dictionary<string, object> mountCart, Dictionary<string, object> mountBaby)
        {
            int lines = 0;
            void Report(string s) { d.Report += s + "; "; }
            SoundBlock both = SoundBlock.Parse(d.Sounds, "Sounds", d.FileNames, Report);
            both.AddConvention(d.FileNames, "Sounds", Report);
            lines += both.Count + SoundBlock.Parse(d.SoundsRide, "Sounds.Ride", d.FileNames, Report).Count + SoundBlock.Parse(d.SoundsCart, "Sounds.Cart", d.FileNames, Report).Count
                + SoundBlock.Parse(d.SoundsBaby, "Sounds.Baby", d.FileNames, Report).Count;
            lines += SoundBlock.Parse(Lines(mountBoth), "Mount.Sounds", d.FileNames, Report).Count + SoundBlock.Parse(Lines(mountRide), "Mount.Sounds.Ride", d.FileNames, Report).Count
                + SoundBlock.Parse(Lines(mountCart), "Mount.Sounds.Cart", d.FileNames, Report).Count + SoundBlock.Parse(Lines(mountBaby), "Mount.Sounds.Baby", d.FileNames, Report).Count;
            return lines;
        }
    }
}
