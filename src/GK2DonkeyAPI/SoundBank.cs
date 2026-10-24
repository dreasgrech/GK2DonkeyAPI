using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace GK2DonkeyAPI
{
    // Own sound clips as game sounds (docs/mount-sounds-design.md section 5). The game's AudioConfig keeps its sounds
    // in a public list, each a Sound of samples (clips) with a 3D preset type, a volume, a loop flag and a mixer
    // group; LazyAudio.PlaySound finds a sound by its id in that list and copies the preset onto a pooled player. An
    // entry made here with clips of our own (a pack's .ogg or .wav, a tone made in code) plays through exactly that
    // path: the pool, the distance rolloff, the mixer's Sounds volume, the pause, SoundHandler. Ids are namespaced
    // ("gk2.<owner>.<skin>.<event>") so they never meet the game's. The probe (the self-test "the probe: own clips as
    // game sounds", the dev Sounds tab's button) is what proves it in game; the rest of the sound system rests on it.
    internal static class SoundBank
    {
        internal sealed class Entry
        {
            internal string Id;
            internal string Owner;
            internal Sound Sound;
            internal AudioClip[] Clips;
            internal bool OwnsClips = true;  // false: a mod's own clips (the API), never destroyed here
        }

        internal sealed class Preset
        {
            internal int Type;
            internal string Name;
            internal float SpatialBlend, MinDistance, MaxDistance;
            internal string Rolloff;
        }

        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static bool groupWarned;

        internal static int Count => entries.Count;
        internal static IEnumerable<Entry> All => entries.Values;

        // The game's sound list; null before the audio system is up (the menu's first frames).
        internal static AudioConfig Config => LazySingletonSO<AudioConfig>.Instance;

        internal static Entry Find(string id) => id != null && entries.TryGetValue(id, out Entry e) ? e : null;

        // A sound of our own under id: the clips as its samples (one at random per play, never the same twice in a
        // row), the Default3D preset (the donkey's own in-world sounds use it), the in-world sfx mixer group (speech:
        // the dialogue voices' group), volume 1, no pitch variation on the samples (the rig applies its own through
        // SetPitch, for game ids and files alike: a variation here would be rolled a second time by Sample.Pitch).
        // An older entry under the same id is replaced, its clips destroyed.
        internal static Entry Register(string id, IList<AudioClip> clips, string owner, bool loop = false, bool speech = false, bool ownsClips = true)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("a sound needs an id");
            if (clips == null || clips.Count == 0) throw new ArgumentException($"sound '{id}': no clip");
            AudioConfig cfg = Config;
            if (cfg == null || cfg.sounds == null) throw new InvalidOperationException("the game's AudioConfig is not up yet");
            Unregister(id);
            var sound = new Sound
            {
                id = id,
                audioSettings3DType = AudioSettings3DType.Default3D,
                volume = 1f,
                panning = 0f,
                loop = loop,
                group = Group(speech),
                randomWithoutRepeat = true,
            };
            var kept = new List<AudioClip>(clips.Count);
            foreach (AudioClip c in clips)
            {
                if (c == null) continue;
                sound.samples.Add(new Sample { clip = c, volume = 1f, pitch = 1f, pitchVariation = 0f, panning = 0f, isEnabled = true, filename = c.name, weight = 1f });
                kept.Add(c);
            }
            if (kept.Count == 0) throw new ArgumentException($"sound '{id}': every clip is null");
            cfg.sounds.Add(sound);
            var e = new Entry { Id = id, Owner = owner, Sound = sound, Clips = kept.ToArray(), OwnsClips = ownsClips };
            entries[id] = e;
            return e;
        }

        // The entry is in the game's list (a config bound anew drops it): put back when gone. Called before a play.
        internal static void Ensure(Entry e)
        {
            AudioConfig cfg = Config;
            if (e == null || cfg == null || cfg.sounds == null) return;
            if (cfg.Get(e.Id) == null) cfg.sounds.Add(e.Sound);
        }

        internal static bool Unregister(string id)
        {
            if (id == null || !entries.TryGetValue(id, out Entry e)) return false;
            entries.Remove(id);
            try
            {
                AudioConfig cfg = Config;
                if (cfg != null && cfg.sounds != null) cfg.sounds.RemoveAll(s => ReferenceEquals(s, e.Sound) || s != null && s.id == id);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Sounds: '{id}' could not be taken out of the game's list: {ex.Message}");
            }
            if (e.OwnsClips)
            {
                foreach (AudioClip c in e.Clips)
                {
                    if (c != null) UnityEngine.Object.Destroy(c);
                }
            }
            return true;
        }

        internal static int UnregisterAll(string owner)
        {
            var ids = new List<string>();
            foreach (Entry e in entries.Values) if (e.Owner == owner) ids.Add(e.Id);
            foreach (string id in ids) Unregister(id);
            return ids.Count;
        }

        // The mixer group our clips play in: the group of the game's own in-world donkey sound (donkey_lips), or of
        // the dialogue voice (voice_donkey) for speech; without them, the mixer's sfx_inworld / speech group; none:
        // plain output, said once.
        internal static AudioMixerGroup Group(bool speech)
        {
            AudioConfig cfg = Config;
            Sound like = cfg?.Get(speech ? "voice_donkey" : "donkey_lips");
            if (like != null && like.group != null) return like.group;
            AudioMixer mixer = cfg != null ? cfg.audioMixer : null;
            if (mixer != null)
            {
                AudioMixerGroup[] gs = mixer.FindMatchingGroups(speech ? "speech" : "sfx_inworld");
                if (gs != null && gs.Length > 0) return gs[0];
            }
            if (!groupWarned)
            {
                groupWarned = true;
                Plugin.Log.LogWarning("Sounds: no mixer group found for the mod's own clips; they play outside the game's volume settings.");
            }
            return null;
        }

        // ------------------------------------------------------------------ the game's 3D presets (diagnostics)

        // The AudioSettings3DPreset components under the LazyAudio object (the dictionary LazyAudio keeps is private;
        // the components are public): one line each in the log when asked.
        internal static List<Preset> Presets(bool log)
        {
            var list = new List<Preset>();
            LazyAudio la = UnityEngine.Object.FindAnyObjectByType<LazyAudio>(FindObjectsInactive.Include);
            if (la == null) return list;
            foreach (AudioSettings3DPreset p in la.GetComponentsInChildren<AudioSettings3DPreset>(true))
            {
                AudioSource src = p.GetComponent<AudioSource>();
                if (src == null) continue;
                int type = (object)p.type != null ? p.type.value : -1; // Enumeration's == and != answer false against null: the plain object test
                var e = new Preset
                {
                    Type = type,
                    Name = type >= 0 ? Enumeration.GetNameOfStaticField<AudioSettings3DType>(type) : "?",
                    SpatialBlend = src.spatialBlend,
                    MinDistance = src.minDistance,
                    MaxDistance = src.maxDistance,
                    Rolloff = src.rolloffMode.ToString(),
                };
                list.Add(e);
                if (log) Plugin.Log.LogInfo($"Sounds: the game's 3D preset {e.Type} ({e.Name}): spatial blend {e.SpatialBlend:0.00}, distance {e.MinDistance:0.#}..{e.MaxDistance:0.#}, rolloff {e.Rolloff}");
            }
            return list;
        }

        // Whether LazyAudio plays from the same AudioConfig our entries go into (its field is private; a config bound
        // anew would be another object).
        internal static string ConfigBinding()
        {
            try
            {
                LazyAudio la = UnityEngine.Object.FindAnyObjectByType<LazyAudio>(FindObjectsInactive.Include);
                if (la == null) return "no LazyAudio object";
                AudioConfig theirs = HarmonyLib.Traverse.Create(la).Field("audioConfig").GetValue<AudioConfig>();
                AudioConfig ours = Config;
                return ReferenceEquals(theirs, ours) ? $"LazyAudio plays from the AudioConfig we add to ({(ours != null ? ours.sounds.Count : 0)} sounds)"
                    : $"LazyAudio's AudioConfig is NOT the one we add to (theirs {(theirs != null ? theirs.name : "null")}, ours {(ours != null ? ours.name : "null")})";
            }
            catch (Exception e)
            {
                return "LazyAudio's AudioConfig could not be read: " + e.Message;
            }
        }

        // ------------------------------------------------------------------ loading

        // A RIFF/WAVE file's samples as an AudioClip: PCM 8, 16, 24 or 32 bit or 32-bit float, 1 or 2 channels, any
        // rate. The whole file is decoded at once (a pack's sounds are a few seconds each). A bad file throws with
        // the reason.
        internal static AudioClip FromWav(byte[] bytes, string name)
        {
            if (bytes == null || bytes.Length < 44) throw new FormatException("not a WAV file (too short)");
            if (Ascii(bytes, 0) != "RIFF" || Ascii(bytes, 8) != "WAVE") throw new FormatException("not a WAV file (no RIFF/WAVE header)");
            int pos = 12;
            int format = 0, channels = 0, rate = 0, bits = 0;
            int dataAt = -1, dataLen = 0;
            while (pos + 8 <= bytes.Length)
            {
                string chunk = Ascii(bytes, pos);
                int len = BitConverter.ToInt32(bytes, pos + 4);
                int body = pos + 8;
                if (chunk == "fmt " && len >= 16)
                {
                    format = BitConverter.ToUInt16(bytes, body);
                    channels = BitConverter.ToUInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    bits = BitConverter.ToUInt16(bytes, body + 14);
                    if (format == 0xFFFE && len >= 40) format = BitConverter.ToUInt16(bytes, body + 24); // extensible: the sub-format's first word
                }
                else if (chunk == "data")
                {
                    dataAt = body;
                    dataLen = Math.Min(len, bytes.Length - body);
                    break;
                }
                pos = body + len + (len & 1);
            }
            if (dataAt < 0 || channels <= 0 || rate <= 0) throw new FormatException("WAV: no fmt or data chunk");
            if (format != 1 && format != 3) throw new FormatException($"WAV: format {format} is not PCM or float");
            if (format == 1 && bits != 8 && bits != 16 && bits != 24 && bits != 32 || format == 3 && bits != 32) throw new FormatException($"WAV: {bits}-bit {(format == 3 ? "float" : "PCM")} is not supported");
            int bytesPer = bits / 8;
            int frames = dataLen / (bytesPer * channels);
            if (frames <= 0) throw new FormatException("WAV: no samples");
            var data = new float[frames * channels];
            int at = dataAt;
            for (int k = 0; k < data.Length; k++, at += bytesPer)
            {
                switch (bits)
                {
                    case 8: data[k] = (bytes[at] - 128) / 128f; break;
                    case 16: data[k] = BitConverter.ToInt16(bytes, at) / 32768f; break;
                    case 24: data[k] = ((bytes[at] << 8 | bytes[at + 1] << 16 | bytes[at + 2] << 24) >> 8) / 8388608f; break;
                    default: data[k] = format == 3 ? BitConverter.ToSingle(bytes, at) : BitConverter.ToInt32(bytes, at) / 2147483648f; break;
                }
            }
            AudioClip clip = AudioClip.Create(name ?? "wav", frames, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static string Ascii(byte[] b, int at) => at + 4 <= b.Length ? Encoding.ASCII.GetString(b, at, 4) : "";

        // A file on disk (.ogg, .wav, .mp3) decoded by Unity, on a coroutine of the plugin's: done(clip, null) or
        // done(null, why). The proven path (tools/GK2AssetProbe, docs/external-assets.md 11).
        internal static Coroutine LoadFile(string path, Action<AudioClip, string> done)
        {
            if (Plugin.Instance == null)
            {
                done?.Invoke(null, "the plugin is not up");
                return null;
            }
            return Plugin.Instance.StartCoroutine(LoadFileRoutine(path, done));
        }

        private static IEnumerator LoadFileRoutine(string path, Action<AudioClip, string> done)
        {
            UnityWebRequest req;
            try
            {
                AudioType type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS
                    : path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? AudioType.MPEG : AudioType.WAV;
                req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            }
            catch (Exception e)
            {
                done?.Invoke(null, e.Message);
                yield break;
            }
            yield return req.SendWebRequest();
            AudioClip clip = null;
            string err = null;
            try
            {
                if (req.result != UnityWebRequest.Result.Success) err = req.error ?? "did not load";
                else
                {
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip == null) err = "no audio in the file";
                    else clip.name = Path.GetFileName(path);
                }
            }
            catch (Exception e)
            {
                err = e.Message;
            }
            finally
            {
                req.Dispose();
            }
            done?.Invoke(clip, err);
        }

        // Bytes that are not on disk (an embedded pack's .ogg) written once under BepInEx\cache so Unity can decode
        // them from a file; the path. Written again only when missing or of another length.
        internal static string CacheFile(string owner, string skin, string file, byte[] bytes, string sub = null)
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "cache", "GK2DonkeyAPI", "sounds", SafeName(owner), SafeName(skin));
            if (!string.IsNullOrEmpty(sub)) dir = Path.Combine(dir, SafeName(sub)); // the mount's files apart from the skin's own
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, SafeName(file));
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length) File.WriteAllBytes(path, bytes);
            return path;
        }

        private static string SafeName(string s)
        {
            var sb = new StringBuilder(s?.Length ?? 0);
            foreach (char c in s ?? "")
            {
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        // ------------------------------------------------------------------ a skin's or mount's files

        internal enum State { None, Loading, Ready, Failed }

        private sealed class Job
        {
            internal string Id, Owner, Skin, Key, Folder;
            internal FileSource Src;
            internal bool Speech, Loop, Cancelled;
            internal int Left;
            internal readonly List<AudioClip> Clips = new List<AudioClip>();
        }

        private static readonly Dictionary<string, Job> loading = new Dictionary<string, Job>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> failed = new Dictionary<string, string>(StringComparer.Ordinal); // id -> owner

        internal static State StateOf(string id)
        {
            if (id == null) return State.None;
            if (entries.ContainsKey(id)) return State.Ready;
            if (loading.ContainsKey(id)) return State.Loading;
            return failed.ContainsKey(id) ? State.Failed : State.None;
        }

        // Where a pack's sound files come from: a skin's own folder, or a mount folder's (MountPacks.cs). Name is the
        // bank's name for it ("saddleup.dragon/default", "mount-saddleup.dragon"), Label the log's ("skin '...'", "mount '...'").
        internal sealed class FileSource
        {
            internal readonly string Owner, Name, Source, Label;
            internal readonly Func<string, byte[]> ReadFile;

            internal FileSource(string owner, string name, string source, Func<string, byte[]> readFile, string label)
            {
                Owner = owner; Name = name; Source = source; ReadFile = readFile; Label = label;
            }

            internal string BankOwner => Owner + "/" + Name;
        }

        internal static string BankOwner(Looks.SkinDef d) => d.Owner + "/" + d.Id;

        internal static string IdFor(FileSource src, string key) => "gk2." + src.Owner + "." + src.Name + "." + key;

        // The files a layer's line names (the pack's sounds\ folder, or sounds\mount\ for its mount's: folder),
        // registered once as one game sound under the skin's id for that key; the id at once, the clips a few frames later (a pack on disk and an embedded .ogg
        // go through Unity's decoder; an embedded .wav is parsed here at once). StateOf says when it plays; a file
        // that doesn't load is left out with a warning, and with none left the entry fails and the layer below applies.
        internal static string ForSkin(Looks.SkinDef d, string key, string[] files, bool speech, bool loop, string folder = Looks.SoundsKey)
            => ForFiles(d.SoundFiles, key, files, speech, loop, folder);

        internal static string ForFiles(FileSource src, string key, string[] files, bool speech, bool loop, string folder = Looks.SoundsKey)
        {
            string id = IdFor(src, key);
            if (entries.ContainsKey(id) || loading.ContainsKey(id) || failed.ContainsKey(id)) return id;
            var job = new Job { Id = id, Owner = src.BankOwner, Skin = src.Label, Src = src, Key = key, Folder = folder ?? Looks.SoundsKey, Speech = speech, Loop = loop, Left = files.Length };
            loading[id] = job;
            foreach (string f in files) StartOne(job, f);
            return id;
        }

        // A code skin's or mount's own clips as one game sound (the API).
        internal static string ForClips(string owner, string skinId, string key, AudioClip[] clips, bool speech, bool loop)
        {
            string id = "gk2." + owner + "." + skinId + "." + key;
            if (entries.ContainsKey(id) || failed.ContainsKey(id)) return id;
            try
            {
                Register(id, clips, owner + "/" + skinId, loop, speech, ownsClips: false); // the mod's clips stay the mod's
            }
            catch (Exception e)
            {
                failed[id] = owner + "/" + skinId;
                Plugin.Log.LogWarning($"Sounds: '{skinId}': the clips for '{key}' could not be registered ({e.Message}); the layer below applies");
            }
            return id;
        }

        private static void StartOne(Job job, string file)
        {
            FileSource d = job.Src;
            try
            {
                string rel = job.Folder + file; // "sounds/x.ogg", "sounds/mount/x.ogg"
                if (!string.IsNullOrEmpty(d.Source))
                {
                    LoadFile(Path.Combine(d.Source, rel.Replace('/', Path.DirectorySeparatorChar)), (c, e) => Done(job, file, c, e));
                    return;
                }
                byte[] bytes = d.ReadFile?.Invoke(rel);
                if (bytes == null)
                {
                    Done(job, file, null, "not in the pack");
                    return;
                }
                if (file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    Done(job, file, FromWav(bytes, file), null);
                    return;
                }
                LoadFile(CacheFile(d.Owner, d.Name, file, bytes), (c, e) => Done(job, file, c, e));
            }
            catch (Exception e)
            {
                Done(job, file, null, e.Message);
            }
        }

        private static void Done(Job job, string file, AudioClip clip, string err)
        {
            if (clip != null && !job.Cancelled) job.Clips.Add(clip);
            else if (clip != null) UnityEngine.Object.Destroy(clip);
            else Plugin.Log.LogWarning($"Sounds: {job.Skin}: {SoundBlock.FolderText(job.Folder)}{file} did not load ({err}); left out");
            if (--job.Left > 0) return;
            loading.Remove(job.Id);
            if (job.Cancelled) return;
            if (job.Clips.Count == 0)
            {
                failed[job.Id] = job.Owner;
                Plugin.Log.LogWarning($"Sounds: {job.Skin}: no file of '{job.Key}' loaded; the layer below applies");
                return;
            }
            try
            {
                Register(job.Id, job.Clips, job.Owner, job.Loop, job.Speech);
                Plugin.Log.LogInfo($"Sounds: {job.Skin}: {job.Clips.Count} file(s) loaded for '{job.Key}'");
            }
            catch (Exception e)
            {
                failed[job.Id] = job.Owner;
                Plugin.Log.LogWarning($"Sounds: {job.Skin}: '{job.Key}' could not be registered ({e.Message}); the layer below applies");
            }
        }

        // A skin withdrawn or read again: its entries, its failures and its loads in flight go, so a changed file is
        // read anew on the next play.
        internal static int ForgetSkin(Looks.SkinDef d) => ForgetOwner(BankOwner(d));

        internal static int ForgetFiles(FileSource src) => ForgetOwner(src.BankOwner);

        private static int ForgetOwner(string owner)
        {
            int n = UnregisterAll(owner);
            var ids = new List<string>();
            foreach (var kv in failed) if (kv.Value == owner) ids.Add(kv.Key);
            foreach (string id in ids) failed.Remove(id);
            foreach (Job j in loading.Values) if (j.Owner == owner) j.Cancelled = true;
            return n;
        }

        // ------------------------------------------------------------------ the probe tone

        // A sine of seconds at hz as 16-bit mono WAV bytes (44.1 kHz), a short fade at both ends against clicks.
        internal static byte[] TestTone(float seconds, float hz, int rate = 44100)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
            int fade = Mathf.Min(n / 4, rate / 100);
            var data = new byte[44 + n * 2];
            void Str(int at, string s) { Encoding.ASCII.GetBytes(s, 0, 4, data, at); }
            void I32(int at, int v) { Array.Copy(BitConverter.GetBytes(v), 0, data, at, 4); }
            void I16(int at, short v) { Array.Copy(BitConverter.GetBytes(v), 0, data, at, 2); }
            Str(0, "RIFF"); I32(4, 36 + n * 2); Str(8, "WAVE");
            Str(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, 1); I32(24, rate); I32(28, rate * 2); I16(32, 2); I16(34, 16);
            Str(36, "data"); I32(40, n * 2);
            for (int k = 0; k < n; k++)
            {
                float env = fade > 0 ? Mathf.Min(1f, Mathf.Min(k, n - 1 - k) / (float)fade) : 1f;
                float v = Mathf.Sin(2f * Mathf.PI * hz * k / rate) * 0.5f * env;
                I16(44 + k * 2, (short)Mathf.RoundToInt(v * 32767f));
            }
            return data;
        }

        internal const string ProbeToneId = "gk2." + Plugin.Guid + ".probe-tone";

        // The dev Sounds tab's button: the tone registered once, played at the transform through the mod's player.
        internal static void PlayProbeTone(Transform at)
        {
            try
            {
                if (at == null) { Plugin.Log.LogInfo("Sounds: nothing to play the probe tone at"); return; }
                if (Find(ProbeToneId) == null) Register(ProbeToneId, new[] { FromWav(TestTone(0.5f, 440f), "probe-tone") }, Plugin.Guid);
                SoundHandler h = Sounds.PlayId(ProbeToneId, at, 1f, 1f);
                Plugin.Log.LogInfo(h != null ? $"Sounds: the probe tone plays at {at.name} ({ConfigBinding()})" : "Sounds: the probe tone did not play (see the warning above)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Sounds: the probe tone failed: " + e.Message);
            }
        }
    }
}
