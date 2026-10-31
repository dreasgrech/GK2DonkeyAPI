# Media

The pictures and GIFs of GK2 Saddle Up for every page that shows the mod: the Nexus Mods description, this repo's
README, store or forum pages. One file per subject, named by the subject and never by date, so a re-shoot replaces the
file and no page needs an edit. Hotlink the raw URL:

    https://raw.githubusercontent.com/dreasgrech/GK2DonkeyAPI/main/media/<file>

(the repo must be public for that). GIFs: half size, 0.1 s per frame, still backgrounds, as the showcase director's
`make-gif.sh` makes them (`docs/showcase-video.md`, "Automation"). Keep a file under about 5 MB; a bigger one goes to a
GitHub Release as an asset and is hotlinked from there. The working series folders (every frame, every take) stay in
`Pictures\GK2 Saddle Up shots` on the shooting machine, not here.

**A looping GIF from a take** (the Nexus thumbnail): `python tools/loop-gif.py <take.mp4>` cuts a loop out of an OBS
take and blends the seam, writing the GIF (560 wide, `--width` for another size) and an MP4 preview. Given `--start` and
`--end` (seconds) it cuts exactly there; without them it searches the take for the stretch whose end best matches its
start (`--min` / `--max` bound the length). `--fade N` blends the seam over N frames (25 per second), `--fade-before`
takes the blend from the frames just before the start, so nothing after the end shows (a cart slowing down after it),
`--crop W:H:X:Y` cuts the picture first. The thumbnail (a following camera, so the seam is a dissolve):

    python tools/loop-gif.py "2026-10-16 16-48-24.mp4" --from 2.0 --to 8.0 --start 3.0 --end 7.22 --fade 10 --fade-before --width 286

| File | Shows | Source |
|---|---|---|
| `donkey-comes.gif` | H: the donkey trots up and the keeper gets on | OBS take |
| `riding.gif` | riding across the graveyard | OBS take |
| `cart-loads.gif` | the cart's loads, one to six items, from the saddle | director: cargo series or "1..6, each" |
| `cart-six.png` | the cart loaded with six | director: F10 |
| `corpse-to-morgue.gif` | a body on the cart, the morgue door, the hand-over | OBS take |
| `zombies-on-cart.png` | zombies lying on the cart | F10, set up by hand |
| `chest-on-cart.gif` | the storage chest picked up, driven, set down | OBS take |
| `cart-parked.png` | the cart left standing with its load | director: F10 |
| `fight.gif` | the knight shooting from the donkey | OBS take |
| `colours.gif` | every customisable part changing | director: "All parts at once" |
| `colours-parts.gif` | one part at a time through its presets | director: "Look series" |
| `foals.gif` | the donkey with foals trailing it | OBS take |
| `gerry-riding.gif` | riding Gerry round the graveyard, his skulls trailing (7 s, 10 fps, 560 wide) | OBS take 2026-10-29 23-39-48, 0-7.0 s (`tools/loop-gif.py --start 0 --end 7.0 --fade 0 --fps 10`) |
| `sounds.mp4` | the cart over gravel and grass, with sound | OBS take |
| `settings.png` | the Mods menu page with the donkey preview (until 0.13; out of the page texts since 0.14.0) | F10 |
| `options-window.png` | the options window on Keys (0.14.0) | Steam screenshot 2026-10-30 23:45, cropped to the window (1818x1214) |
| `settings-button.png` | the game's Settings window with the GK2 Saddle Up options button above OK (0.14.0) | Steam screenshot 2026-10-31 11:25, cropped to the window (800x823) |
| `trailer.mp4` | the 30-60 s trailer | the edit script (not built) |

The packs' own pictures live in a folder each, with a README listing them: `donkey-skin-pack/` (GK2 Saddle Up: Donkey
Skins), `dragons-mount-pack/` (GK2 Saddle Up: Dragon).
