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
| `options-window.png` | the options window on its Mount tab (0.16.0): the five tabs, Leave your mounts in the world on with Send your other mounts home under it, the Mount Behaviour and Sound headings | Steam screenshot 2026-11-06 00:35, box 392,132-2168,1308 (the window's frame, probed pixel by pixel; 0.14.0's window, a different size, was 372,112-2190,1326), 560 wide (`tools/crop-shot.py` in GK2QOLMods) |
| `settings-button.png` | the game's Settings window with the GK2 Saddle Up options button above OK (0.14.0) | Steam screenshot 2026-10-31 11:25, box 880,306-1680,1129 (the window with a 24 px margin), 560 wide (`tools/crop-shot.py` in GK2QOLMods) |
| `journal-donkey.png` | the Mount Journal on the Donkey (Default skin, its cart, the Name, Skin and Babies rows); with `colours-donkey.png` in the old colours editor's place (`Donkey-Cart-Colors-Editor.png`, outdated 2026-10-31) | Steam screenshot 2026-10-31 14:54, box 236,134-2328,1306 (the window's frame, the packs' box), 560 wide (`tools/crop-shot.py` in GK2QOLMods) |
| `colours-donkey.png` | the Colours window on the Donkey (0.16.0, every row named by its part): a custom Charcoal coat, a Blue harness, the Muzzle Rose, Legs, mane and ears Pink, Eyes on Game default; custom Rust paint, Black wheels, Silver hubs | Steam screenshot 2026-11-06 00:36, box 492,92-2068,1348 (the window's own outline, the packs' box, unchanged), 560 wide (`tools/crop-shot.py` in GK2QOLMods) |
| `leave-mounts.png` | Leave your mounts in the world (0.16.0): the yard with the Armored donkey, Gerry and the dragon standing with their babies, and the Critters pack's snail and worm (unreleased; the user: "i want to show the unreleased there"; the page says they come later), the keeper on foot among them | Steam screenshot 2026-11-06 00:28, box 520,150-2300,1150 (clear of the HUD; the two rats in the bottom-left corner left out, as they sit by the HUD), 560 wide (`tools/crop-shot.py` in GK2QOLMods) |
| `trailer.mp4` | the 30-60 s trailer | the edit script (not built) |

The packs' own pictures live in a folder each, with a README listing them: `donkey-skin-pack/` (GK2 Saddle Up: Donkey
Skins), `dragons-mount-pack/` (GK2 Saddle Up: Dragon).

## Steam Workshop gallery

The pictures of each Workshop item's image gallery (Add/Remove Images on the item's page) are in
`workshop/<Item>/gallery/`, beside the item's `Thumbnail.jpg`. The gallery opens a picture in a viewer about 1500 px
wide, so the 560-wide page pictures came out blurred there (2026-10-31); a gallery picture is the page picture's
subject cut at the screenshot's own pixels, never scaled: `python tools/workshop-gallery.py <screenshot> <box> <Item>
<name>` (a JPEG under 1 MB, quality 95; `--overwrite` to replace one). The screenshots are the Steam screenshots of
the game (2560x1440, `userdata\<account>\760\remote\4358690\screenshots\`), found again by matching each page
picture against them: the boxes of the pack pictures are their READMEs' (an exact match), the 2026-10-14/15
Saddle Up ones were cut by hand at full size and found at their own scale in their first commit. Same subjects as the
page pictures of the same name.

The gallery takes up to 2 MB a picture (the user, 2026-10-31), and the page GIFs are 2.8 to 19 MB but the cargo
cycle's 1.1 MB: a following camera changes every pixel every frame, so 2 MB holds 1.5 to 3 s of one at 560 wide.
gifsicle's lossy mode saved 10-40% on them, fewer colours 15-30%. Three fit with little loss, made from the page
GIFs (gifsicle 1.97, the Windows build linked from lcdf.org/gifsicle; ffmpeg); the others stay in the descriptions,
with the item's YouTube videos for motion in the gallery:

- `GK2SaddleUp/gallery/donkey-comes.gif`: `gifsicle -O3 --lossy=150` (1,933,789 bytes, every frame).
- `GK2SaddleUp/gallery/riding-cart.gif`: `gifsicle -O3 --lossy=200` (1,899,777 bytes, every frame).
- `GK2SaddleUp/gallery/donkey-cart-colors-cycle.gif`: ffmpeg `fps=10/3`, 128 colours, `paletteuse=dither=none`,
  then `gifsicle -O3` (1,939,938 bytes: two colour sets of three, 0.3 s each; lossy does nothing on flat colours).
- `cargo-sixitems-cycle.gif` goes up as it is.

**Thumbnails** (`workshop/<Item>/Thumbnail.jpg`, the item's preview, sent by the game's uploader): square, since
Steam's tiles are square and letterbox anything else (the first, 16:9 ones had bars; the packs' were also their
560-wide page pictures scaled up). Cut from the screenshot at full size (`tools/crop-shot.py ... --width 0` in
GK2QOLMods), then `tools/workshop-thumbnail.py <cut> <Item>` (1024 wide; `--width 900` for the skin pack's 900 box,
so it is not scaled up), 2026-10-31:

| Item | Screenshot, box | Shows |
|---|---|---|
| `GK2SaddleUp` | `20261016173738_1.jpg`, box 1197,0-2297,1100 | the keeper riding the donkey, the cart loaded (chest, barrel, log) |
| `GK2SaddleUpDragonsMountPack` | `20261029172811_1.jpg`, box 1110,66-2350,1306 | the keeper on the path and the dragon (`dragon-keeper.png`'s shot) |
| `GK2SaddleUpDonkeySkinPack` | `20261028013218_1.jpg`, box 993,115-1893,1015 | the keeper riding the Hellfire donkey, a Hellfire baby (`hellfire-riding.png`'s shot) |

| File | Screenshot, box | Size |
|---|---|---|
| `GK2SaddleUp/gallery/Foals.jpg` | `20261014211544_1.jpg`, box 310,140-1645,908 | 1335x768 |
| `GK2SaddleUp/gallery/ZombieWorkstation.jpg` | `20261015191446_1.jpg`, box 836,271-1838,859 | 1002x588 |
| `GK2SaddleUp/gallery/journal-donkey.jpg` | `20261031145438_1.jpg`, box 236,134-2328,1306 | 2092x1172 |
| `GK2SaddleUp/gallery/colours-donkey.jpg` | `20261031145654_1.jpg`, box 492,92-2068,1348 | 1576x1256 |
| `GK2SaddleUp/gallery/GK2ModFramework-Mods.jpg` | `20261015194751_1.jpg`, box 510,217-2048,1223 | 1538x1006 |
| `GK2SaddleUp/gallery/options-window.jpg` | `20261030234548_1.jpg`, box 372,112-2190,1326 | 1818x1214 |
| `GK2SaddleUp/gallery/settings-button.jpg` | `20261031112533_1.jpg`, box 880,306-1680,1129 | 800x823 |
| `GK2SaddleUpDragonsMountPack/gallery/dragon-flying.jpg` | `20261029173110_1.jpg`, box 264,110-1964,1066 | 1700x956 |
| `GK2SaddleUpDragonsMountPack/gallery/dragon-keeper.jpg` | `20261029172811_1.jpg`, box 860,110-2560,1066 | 1700x956 |
| `GK2SaddleUpDragonsMountPack/gallery/babies-dragon.jpg` | `20261029173949_1.jpg`, box 641,110-2341,1066 | 1700x956 |
| `GK2SaddleUpDragonsMountPack/gallery/journal-dragon.jpg` | `20261029172822_1.jpg`, box 236,134-2328,1306 | 2092x1172 |
| `GK2SaddleUpDragonsMountPack/gallery/colours-dragon.jpg` | `20261029172919_1.jpg`, box 492,92-2068,1348 | 1576x1256 |
| `GK2SaddleUpDonkeySkinPack/gallery/hellfire-riding.jpg` | `20261028013218_1.jpg`, box 340,110-2040,1066 | 1700x956 |
| `GK2SaddleUpDonkeySkinPack/gallery/royal-parade-riding.jpg` | `20261028013225_1.jpg`, box 420,110-2120,1066 | 1700x956 |
| `GK2SaddleUpDonkeySkinPack/gallery/journal-hellfire.jpg` | `20261028013244_1.jpg`, box 236,134-2328,1306 | 2092x1172 |
| `GK2SaddleUpDonkeySkinPack/gallery/journal-royal-parade.jpg` | `20261028013238_1.jpg`, box 236,134-2328,1306 | 2092x1172 |
| `GK2SaddleUpDonkeySkinPack/gallery/colours-hellfire.jpg` | `20261028022007_1.jpg`, box 492,92-2068,1348 | 1576x1256 |
| `GK2SaddleUpDonkeySkinPack/gallery/colours-royal-parade.jpg` | `20261028022141_1.jpg`, box 492,92-2068,1348 | 1576x1256 |
