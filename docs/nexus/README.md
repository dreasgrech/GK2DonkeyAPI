# Nexus Mods page

`saddleup-description.txt` is the mod page's description for Nexus Mods (game `graveyardkeeper2`), in Nexus's
BBCode. Paste it into the description editor's BBCode view as it is. Written 2026-10-14 for Saddle Up 0.9.0 / API 0.29.0.

**Media** lives in the repo's `media/` folder (its README lists the files by subject), hotlinked by raw GitHub URL;
the same files serve every other page too.

**Placeholders** are the `{{ ... }}` lines: each names the screenshot, GIF or video that goes there. Replace the line
with `[img]URL[/img]` (Nexus hosts the image once it is uploaded to the mod's Images tab; a GIF is an `[img]` too) or
`[youtube]VIDEO_ID[/youtube]`, or delete the line. `{{ GITHUB_URL }}` is the repo's public URL once it exists.

**Nexus BBCode in use:** `[b] [i] [size=N] [center] [line] [list] [list=1] [*] [url=] [img] [youtube]`. Nexus has no
heading tag that renders everywhere, so headings are `[size=5][b]`. Nexus strips unknown tags rather than showing
them, so the `{{ }}` lines render as plain text if one is forgotten: search for `{{` before saving the page.

**Taking the pictures:** the showcase director (`tools/GK2ShowcaseDirector`, dev-only, F9) sets a shot up and takes
it: time of day and weather, HUD off, the keeper at a saved spot, Saddle Up's donkey and cart brought and placed,
the cart loaded (six logs, mixed, corpse, random), items dropped along a route, coat and paint variants in a loop, F10 to
capture into `Pictures\GK2 Saddle Up shots`; a cargo series and a look series write same-crop frames with a
`make-gif.sh` for the page's GIFs. Details in `docs/showcase-video.md` ("Automation"). Shoot in a save kept
for it. GIFs and the trailer: OBS and ffmpeg, scripts not yet written.

**Keep in step with:** the key table and the settings in `src/SaddleUp/README.md`, the game build in
`dist/README.md`, and the BepInEx version in the game folder (5.4.23.5 as of 2026-10-14).
