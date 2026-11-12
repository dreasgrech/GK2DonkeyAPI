# Changelog

Every public release of the Nexus download (GK2 Saddle Up with GK2 Donkey API bundled), newest first. Each entry
lists what changed since the previous public release. While working, every change is added under **Unreleased**
straight away; at release that heading becomes the version and date (docs/release-checklist.md, section 6).

- **For players**: what someone playing notices (fixes, features, settings, performance).
- **For mod authors**: changes to the Donkey API's public surface (docs/api.md has the full history per API version).
- **Development**: tools, tests and docs in this repository, not in the download.

## Unreleased

Development:

- **The release runs whole when `dist/` was committed before the zips** (2026-11-12): the release rule
  (`tools/release-versions.py items`) has a fourth state, *pending* (the version committed in `dist/`, in no committed
  release zip, its player note written). `build-dist.sh` ships the committed DLL when the sources build to the same
  code (new `tools/same-build.py`: every byte but the build identity) and stops naming `--rebuild` when they do not;
  `make-release-zip.py` zips pending items and leaves an identical zip on a rerun. Found at 0.17.0, where `release.sh`
  said "nothing to release". `release.sh --rebuild <DLL> --overwrite` passes both on (a pending DLL whose sources
  changed after the commit, and its zip not yet uploaded); `--overwrite` rewrites only a zip whose files changed. `tools/rehearsal.sh` no longer excludes a `modinfo.json` the zips stopped carrying
  (unzip's "excluded filename not matched" caution), and any number of rehearsals runs a day: the staged one's aside
  folder (date and time) is named in `BepInEx\rehearsal-staged` until `restore` (a second one the same day collided:
  renamed "-first" by hand on 2026-11-01, refused on 2026-11-12).

## 0.17.0 (GK2 Donkey API 0.45.0), 2026-11-11

For players:

- **The cart key no longer leaves the cart unhitched beside you** (2026-11-12): riding close to a wall, a fence or a
  building, G could bring the cart over and leave it standing there unhitched, because the seat behind it was no place
  for the keeper. Now nothing moves: the cart stays where it was and the keeper says there's no room for it here.
- **Skins drawn over the donkey sit exactly on it** (2026-11-11): Royal Parade, Armored and the other skins over the
  donkey were drawn a sixth of a pixel too low, which showed thin slivers of the donkey at their top edges in the
  colours window (a white line in Royal Parade's cloth corners, a brown one under Armored's collar strap). The Mount
  Journal's and the colours window's pictures are drawn on the mount's own pixel grid, so no thin line of the coat
  under a skin shows at its edges in some frames.
- **Sounds in the Mount Journal and the colours and options windows** (2026-11-11): they open and close with the
  game's window sounds, and their buttons, lists and tabs click and hover like the game's, with the mouse or a
  gamepad.
- **The Mount Journal pauses the game like the game's windows** (2026-11-11): while it, the colours window or the
  options window is open, the game is paused, the rain and other world sounds are muffled, and the world behind is
  dimmed, as with a table's or a zombie's window.
- **A new colours window** (2026-11-10): no more colour dropdowns. Pick a part on the left and colour it from one
  palette on the right, or step through its colours with the arrows. Every colour has its name, in your language. Original is the part as the skin draws it. The
  lightness slider is always there, greyed out when it does nothing. The colours you already chose stay exactly as
  they are; a named colour like Brown or Dark wood shows by its name until you pick another.
- **Ride another mount without getting off first** (2026-11-10): riding next to another mount you could ride, "Ride"
  shows over it and the interact key takes you onto it. The one you were on stays where you got off, with its cart.
- **Load on cart only when facing the cart** (2026-11-10): with an item in your hands, "Load on cart" shows only when
  you face the cart. Facing away, the key sets the item down in front of you, as the game does.
- **The colours window shows the mount larger** (2026-11-10): the cart is in the picture only on the Cart tab.
- **Colour rows stay in their column** (2026-11-10): setting a colour to Custom in the colours window moved other rows
  from one column to the other.
- **The donkey's own voice in the Mount Journal** (2026-11-10): picking the donkey after another mount played that other
  mount's call.
- **Smoother on slower PCs** (2026-11-09): changing a setting in the Mount Journal or the options window no longer
  writes the settings file at every step of a slider (it is written a moment after you stop). Mounts standing near
  you, riding, jumping and flying make less work and less memory garbage each frame, and mounts left in another
  place than yours rest until you are there. A small memory leak with mounts calling each other is fixed. Putting
  items on the cart and taking them off costs less, and so do mounts coming back into view (riding through a herd,
  going through a door).
  Browsing the Mount Journal again no longer builds every mount's look again, and building a look makes far less
  memory garbage (the dragons most). Picking a mount in the Mount Journal is quicker, the first opening of the journal
  in a session too, and babies finding their way around things make less memory garbage.
- **The skull is Larry** (2026-11-08): Saddle Up's skull mount is called Larry, as in the game. Gerry was the skull
  in the first game. Your skull keeps his colours, babies and settings, and a name you gave him yourself stays.
- **The colours window has tabs** (2026-11-08): Mount, the worn skin's own parts on a tab named after the skin, and
  Cart. Each tab's rows fill both columns, so a skin with many parts no longer makes one long list. A skin without
  parts of its own shows the two tabs. (Saddle Up's two colour headings became the tabs Mount and Cart, their words
  the options window's tabs', already translated.)
- **A baby with no room beside its parent stands still instead of jumping** (2026-11-06): beside a parent standing
  against a wall or a fence (the house door), a baby that couldn't take a step was put back on its own spot every few
  seconds, in view, and the log filled with its lines. It stands there now until its parent moves, and still trots off
  to a free place when there is one (docs/foals-catch-up-loop.md).
- **Babies placed by their parent get their own spot again** (2026-11-06): coming through a door, called with their
  mount, or caught up, a baby behind a parent that doesn't idle about was always put one spot over. Its own spot
  counted as taken by the parent.
- **No more hoofsteps in the pause menu** (2026-11-07): opening the pause menu or a table's window while riding, the
  mount walked on the spot with its hoofsteps until you moved again. It stands now.
  Babies and standing mounts stand still too while the game is paused, instead of running in place.
- **Ride and Drive the cart keys work in the Mount Journal** (2026-11-07): pressing H or G (or the keys you set) in the
  journal closes it and calls the mount you picked, as its Ride and Drive the cart buttons do.
- **A skin that isn't installed fits its row in every language** (2026-11-07): when a mount's skin comes from a pack
  you removed, the Mount Journal's Skin row shows the skin's name, greyed, and the Stats card says "Skin: Not
  installed". The row said "<name> (not installed)", which was cut off in Russian and Polish.
- **The keeper sits still on a flying mount** (2026-11-07): flying along on the dragon, he bobbed up and down as if it
  were walking. He now rides steady and rises and sinks with its wing beats. On the ground nothing changes.

For mod authors:

- **The colours window draws a colour choice as swatches** (2026-11-10): a `Choice` whose next row is its `Color`
  (custom colour) and which has a value named `Custom` is drawn as one part: Game default (the choice's first value),
  the 24 `PaletteColor`s (Custom with that colour) and the value it had if it was another. Nothing saved changes; a
  choice made otherwise stays a dropdown.
- **A variant's tone rows in its own order** (2026-11-10): a skin built on its mount's default lists the tones it
  declares (`[Tone.*]`, `MountSkinDef.Tones`) in its own order, a default's it re-declares too, then the default's it
  doesn't mention. Before, the default's came first and a re-declared one kept the default's place
  (`SkinTones.Merge`; docs/skins.md).
- **API 0.45.0: `MountCompanion.Ride()` and `Drive()`** (2026-11-07): the ride and cart keys' actions (`Use(Ride)`,
  `Use(Cart)`). Given to a page's `Key` as they are (`pressed: me.Ride`), the key also works in the Mount Journal of that
  companion's page, as the journal's Ride and Drive the cart buttons (`KeyActions` recognises the delegate;
  `MountsPanel.CallFromKey`). Not with the colours window or the options on top, and the cart key not for a mount with
  no Drive button. Every other key still waits while the journal is open. Saddle Up's two keys use them, so it needs
  API 0.45.0.
- **API 0.45.0: the tuning key `BobInFlight`** (2026-11-07; docs/rider-motion.md "In flight"): false (the default),
  the rider's walk motion (bob, hands, head) stops while the mount shows its skin's own flying pictures; true keeps it.
  The game's walk went on under those pictures whenever a direction was held, and the rider bobbed with it. Like every
  rider motion key it is the mount's to set (`mount.cfg` `[Tuning.Ride]`, `MountDef.TuningRide`).
- **A skin over the donkey with colours of its own under a region gets a custom colour's lightness from them**
  (2026-11-08; docs/mount-colours.md "A skin worn over the donkey"): a custom colour's brightness is its value over
  the art's average, and for a skin over the donkey that was always the donkey's own. A skin that draws its own
  harness (`[Colors] Harness = ...`) and hides the donkey's had its shades all lifted to white, one flat colour (the
  Jester in pink: the donkey's harness averages 0.35, its own 0.58). Those colours count now; a skin naming none
  under a region, and every released skin, keeps the donkey's. Self-test "colours: a skin over the donkey keeps the
  donkey's regions" checks both.

Development:

- **The cart key's hitch seat asked before the cart moves** (2026-11-12): `MountCompanion.BringCart` asks
  `WhyHitchSeatBlocked` (the hitch's own seat check, now one function) where `ComePlace` would put the cart, and
  refuses with `say.no_room_here` before anything moves (a cart spawned for it is removed again). Self-test
  `CartSeatTests.SeatBlocked` (Core; the dev switch `TestHitchSeatBlocked` blocks the hitch seat only). Found in
  rehearsal A of 0.17.0, just outside a door.
- **Hellfire's rump fill keeps the body's own pixels** (2026-11-12, `tools/skins/hellfire/hellfire_proto.py`
  `fill_rump`): the stretch it repaints in coat, from the last coat pixel to the rump's curve, measured 1-6 pixels
  on 489 rows over all 44 frames (the rump's edge) and 12 or 19 on 36 rows (the belly's bottom edge in the standing
  pose's 33 frames, the hind legs in walk 04: the user's straight line through the legs); nothing between. A
  stretch wider than `RUMP_EDGE_MAX` (8) is left as drawn. Regenerated: 34 frames differ from the shipped pack,
  in those strips only (the first try, filling only outside the body's edge, also kept the old outline inside
  the moved rump edge in 40 frames; diffed, reverted). Donkey Skin Pack 1.3.1.
- **Three F11 results of 2026-11-12 made to measure** (the full run: 2114 passed, 2 failed, 1 warning): the stuck
  report's shut-in baby has its pen where the screen shows it (a fixed 3.5 from the keeper was off screen with the
  camera zoomed in by Grave Zoom at 400%: measured, the pen at x 2680 of 2560, so the baby rightly caught up out of
  sight; now 3.5, 3 or 2.6 as the view allows, else skipped and said), and a failure prints the keeper, the camera and
  the screen points; and every run is at the game's own zoom now (`Dev/TestZoom.cs`: a zoom mod held off for the
  run, the player's zoom back at its end; docs/self-tests.md); "foals keep up off screen" counts its own babies'
  catch-ups (`LastCatchUpWhy` names the reason), no longer every mount's (`Mount.CatchUpCount`); a jump sent back to
  its take-off spot judges that spot as a landing too, in the dev log line (a spinning wheel by a weaving loom in the
  home yard: refused 0.08 from where the keeper had stood).
- **Our windows' sounds** (2026-11-11, docs/mounts-panel-design.md "Sounds"): the game's ids read from its prefabs
  (`LazyWindow.openSoundId`/`closeSoundId`, `LazyButton.on*Sound`): `win_open`/`win_close` for windows, `gui_hover` +
  `gui_click` for buttons, `gui_hover_light` + `gui_click` for list cells, `gui_hover_light` + `tab_click` for tabs.
  New `UiSounds` (the plays, an id looked up once, a missing one skipped with one warning) and the `UiSound` component
  on each control (`OwnButton` takes the kind; the gamepad's hover and click in `RowControls`). The journal's `Close`
  and `CloseColours` take `sound` (one sound per change of window; Ride or Drive over a game window: the game's close
  only, `GameWindowAside.HasUnder`). Self-tests: the panel and options tests check one sound per open and close, a
  hover per kind, a greyed control silent and a tab's click (`SelfTest.UiSoundChecks`). `tools/ui-sounds-scan.py`
  reads the ids from the prefabs again after a game update (in docs/release-checklist.md).
- **Our windows modal as the game's** (2026-11-11, docs/mounts-panel-design.md "Modal"): the game's own parts, called
  for ours (they aren't in its window stack): `MainGame.PauseGame`/`UnpauseGame` held with the controls in
  `GameWindowAside` (`PausedByUs`; let go only when no game window wants the pause), a postfix on
  `InWorldSfxFilterController.ShouldEnable` for the muffle (`InWorldSfxFilterController_ShouldEnable_Patch`, sorted in
  game-code.txt), and `UiKit.Dim` behind each window with the Settings window's `BgShadow` look. Self-tests: the panel
  and options tests check paused, muffled (the game's answer and the mixer level) and dimmed while open, and the game as
  before after closing.
- **Royal Parade's cloth follows the back in the walk** (2026-11-11, the user's video with a Snow coat):
  `tools/skins/royal-parade/drawn_once.py` `hug_back` moves the cloth's top band (outline, gold trim, pink highlight)
  in each walk and lips frame to lie on the back as on the standing frame, the red under it growing or shrinking by as
  much: walk 01, 07, 08 (the coat showed above the cloth, `stamp.py moves`: "below"). Raised only: lowering where the
  cloth lay a pixel over the back (04-06, and 08 once widened) showed nothing more and notched its top edge. Then
  `widen_back`: the back edge (trim, outline, the rear fringe's end) moved 2 columns back, column 60 drawn three
  times, so the rump strip behind the cloth (the donkey's Outline line moving with the stride, brown in walk 01, 04,
  05, 08) is covered in every frame; 41 of the 44 frames changed (Donkey Skins pack CHANGELOG). Then `round_back`:
  the back top corner cut along the rump as the Jester's cloth is (its top rows ending at 61, 63, 64), repainted in
  the cloth's layers (outline outside each step, gold inside, the pink under the gold where it faces up); the cloth
  off the body 3-17 pixels a frame where the square corner was 8-22.
- **Measurement: a skin over the donkey against the donkey** (2026-11-11, Measurements group,
  `MeasureTests.SkinOffset`, BepInEx\GK2-skin-offset.tsv): for Royal Parade and Armored, every frame shown in the world
  and in the journal's picture, how far the skin's pixel grid sits from the donkey's on screen (art pixels across, up
  and along the view), with the frame's trim offset and the overlay's local position: the user's slivers of a quarter
  pixel at the cloth's corners and under Armored's collar strap, measured before any fix. Found: the pixel grids
  line up across (the canvas origins 0.051 apart is the frame's trim fraction, as the donkey's own pixels are), and
  the skin sat 0.16 art px too low on screen in every frame, in the world and the journal alike: `OverLift` along the
  part's facing under the 53-degree camera. The over skin's lift now runs along the view (`Follow(..., alongView:
  true)`, as the front layer's), the same depth gap with nothing moved on screen; the measurement reports the grids'
  offset (0: on it). Then, with the grids on each other (0 in the world, the journal and the colours window) and every
  picture sampled by point, the colours window's saved pictures (the measurement writes them,
  BepInEx\GK2-skin-offset\) showed the coat under Armored as runs of one picture pixel at every third column in walk
  03, 05 and 08: the picture's pixel centres on the art pixels' edges (Detail 3, the camera framed on the drawn bounds
  and moved by half pixels), where the two layers rounded apart. `PreviewStage.SnapToArt` moves the camera before each
  render so the donkey's art pixel edges fall on the picture's pixel edges (`PreviewStage.CanvasWorld`, the
  measurement's too).
- **The crowd test waits for the world's looks** (2026-11-11): its counters read colour textures 60 -> 58 because the
  world's own snail look was being built again (its mount woke when the keeper came back) just as they were counted.
  Before both counts it now waits until every Active world mount wears its look (`Looks.Rig.Settling`; an Idle view
  is frozen off screen and builds its look only when it wakes), and names any still settling after 10 s.
- **Four changes from the user's list** (2026-11-10, docs/mounts-panel-design.md, docs/instances.md):
  - the colours window's two columns are balanced on each unit's first row as the mount and skin show it
    (`MountsPanel.TabColumns`): the custom colour and lightness rows a choice opens weigh nothing, so no value moves
    the split. The self-test sets every choice of every tab to Custom and compares the column map (`TestColourColumns`,
    `TestCustomAll`);
  - the colours window asks the stage for the mount alone on a tab without a row the page marked with `CartRow`
    (new `Row.CartPart`), with the cart on the cart's tab; a page that marks none keeps the cart everywhere. The mount alone
    is drawn up to 4 per art pixel there (`CAloneScale`; at 3, the cap the cart picture keeps, the donkey alone was no
    bigger than with its cart: 224x147 in the 440x236 stage, the first F11). The colours
    picture is sized to a whole number of screen pixels per art pixel (`Fit(screenWhole)`, `ScreenPixelsPerUnit`), not
    of the panel's units: the dragon, 119x78 art in a stage with room for 2.82, was rounded down to 2 (the donkey 4;
    the user's screenshots), now 5 screen pixels at 2560x1440 (2.49 units). The colours
    window's stage is 256 tall (was 236): the dragon fits at 3 there, as in the journal (2.5 at 236). The colours
    window shows only a photo of the look it colours (`coloursLook`): the last mount's photo in the alone view showed
    under another mount's title for the 2 s its alone picture was first framed (the Measurements screenshots); the
    stage is empty until its own is whole, another tab of the same look keeps the photo shown. The photo
    shown is held until the other view has the look (`ShownPicture`: the colours window's image while it is open). The
    self-test checks the view on every tab;
  - from the saddle: `InstanceControls.FindFromSaddle` offers "Ride" by a mount as on foot (hands empty; not in the air,
    not from the old summon; a free cart in reach stays the hitch's). `RideFromSaddle` asks
    `Instance.WhyNotRideFromSaddle` (RideFromWorld's checks without the ride he is on) before getting him off, then
    `RideFromWorld` as on foot (seat and reach safety unchanged). Driving the cart, the key by a mount now rides it
    instead of setting the cart's top item down;
  - loading on foot only facing the cart: `InstanceControls.FacesCart`, the cart body's nearest point within 90 degrees
    of `PlayerData.Direction` (over the body counts); "Cart is full" too. New scenario "from the saddle onto another
    mount ... loading only facing the cart".
- **Performance pass, measuring first** (2026-11-09, docs/performance.md "The pass of 2026-11-09"): the profiler
  times the frame's steps that had no part: `Companion` (MountCompanion.TickAll), `HeadWatch`, `Doors` (foals
  indoors, the morgue hand-over), `Scenes` (the by-scene lists and the frame's snapshot) and `Warm` (translations,
  looks and sounds read ahead, sheets built, the art checks); `Perf.PhaseModMsPerFrame`/`PhaseKbPerFrame`/
  `PhaseCollections` for a scenario's own table. New integration scenario "the mount crowd" (Garbage group,
  `tools/GK2DonkeyAPI.IntegrationTests/Crowd.cs`): every shipped look with babies and carts, lanterns lit at night,
  uncapped; 12 then 36 in view, 120 more scattered, ridden through; a row per phase appended to
  `BepInEx\GK2-crowd.tsv`; then the made-and-destroyed counters checked back. Four code reviews of the frame's paths
  since 2026-10-16, findings in the doc. Fixed from them (the agreed cut, rows of the doc's table):
  1, `Looks.Rig.TickAbilities` passes the hook's arguments to lambdas that capture nothing (a closure a frame per
  active view gone); 2, `DeferredSave` (new): the journal's and options' row controls set the value at once and write
  the cfg 1.5 s after the last change, half a second after a window closes, and at quit; 3, `Sounds.TestVoiceTimes`
  filled only while the self-tests run (it grew all session and held removed instances); 6, the jump and flight trace
  lines built only with `JumpTrace.On`, the blocker described once per search instead of per closer candidate;
  13, the cart-rear ground probe of the ridden frame (read only by the dev button's `LogGeometry`) only with the dev
  tools; 14, `IsAttackState` keeps its answer for the last state class (`Type.Name` allocates); 16, an instance in
  another scene than the keeper is Idle (frozen, view kept), not Active (`Tiers.cs`; the panel's reason "in another
  scene than the keeper"). Not done: 20's once-a-frame cargo refresh (only the dev test load changes several items in
  a frame; a player's single item needs the refresh made cheaper, held). Measured before and after in the user's runs
  (F11 2050/0/0 after; 36 mounts in view 2.58 -> 0.11 KB of garbage a frame; a slider drag 22 cfg writes -> 1; the
  suites ~15% lower). The crowd scenario puts the keeper back before its leak checks and compares only with the
  world's views back. tools/GK2PerfRunner works with Performance Hog 1.3.0 (it takes the report Performance Hog writes
  itself, `RecordingResult.ReportPath`; 1.3.0's `ReportWriter.Write` takes the recording).
- **The cart's item cost** (2026-11-09, docs/performance.md "The cart's item cost, step by step"): a dev-only probe,
  `Dev/CargoCost.cs` (a `[cargo-cost]` line per item on or off, with the `[perf]` logging), split it. The loaded and
  empty cart bodies are found once per view (`Cargo.Rig.FindCartBodies`) instead of a walk by name of every
  transform under the cart on each change (2,343 with six items aboard: 0.2-0.9 ms); `ActivateFor` sets the layer
  and switches colliders off on the active parts only (`MakeView` did all when the copy was made). An item on 2.3-2.5
  -> 1.75-2.0 ms, off 1.0-1.2 -> 0.2-0.4 ms; what is left is the game's own `DropViewAtomMesh.Activate` (1.4 ms, it
  goes through the ~370 item meshes of the copy). F11 2046/0/0.
- **The door with babies, traced** (2026-11-09, docs/performance.md): a door starts the dev `[pick]` trace ("the
  keeper went through a door"), and the babies' spot search is a profiler part of its own (`Perf.S.Slot`). Sixteen
  doors with one and five babies: the search stays under 0.5 ms; the frames after a door are the mounts' views made
  again, one a frame (2.4-3.7 ms each), inside the door's fade. No change to the door itself.
- **Views coming and going cost less** (2026-11-09, docs/performance.md "The views investigation"): a dev-only views
  probe (`Dev/ViewCost.cs`, `[view-cost]` lines: every path a view takes, each step's time and allocation) and the
  views survey (integration scenario, Garbage group) measured them all first. Idle views are frozen instead of
  switched off (`Lod.IdleSwitchesOff` false: measured uncapped with 74 idle, switching off saved no frame time and
  cost every view coming back 0.53-0.57 ms and 15-17 KB, against 0.03 ms frozen). A view taken from the pool starts
  its animator without the head (the layer emptied before it is switched on, as for an idle view) and skips the
  second restart in `PrepareParts` (0.5 ms and 8 KB a view).
- **The journal's picture and the standard zoom** (2026-11-09, docs/performance.md): the journal's picture is
  rendered only when it can have changed (`PreviewStage.NeedsRender`: a new walk frame, look, framing or tuning, at most
  1/24 s apart), 25 photos a second instead of about 176, the mod 0.63 -> 0.22 ms a frame while it is open; its pose
  reads the animator's parameters once (5 KB a frame of garbage gone). A dev-only steps trace (`Dev/StepTrace.cs`,
  `[steps]` lines) on the journal's opening and each pick, and the journal survey (integration scenario, Garbage group).
  The measurement scenarios and the perf runner's suites run at 100% zoom (`StandardZoom`: Grave Zoom, when installed,
  set for the run and put back as it was); the views survey finds its idle spot from the camera's scale.
- **Skin builds: less garbage, the journal's looks kept** (2026-11-09, docs/performance.md "Skin sheets let go and built
  again"): measured first (each build's garbage by step in its line; `BuildSurvey` comparing a PNG per picture with one
  ready-made sheet: the sheet decoded in one frame, 10.5 ms and twice the garbage, not taken; every build line says how
  often the skin was built this session). Pictures are decoded into one kept texture and a reused array
  (`Looks.DecodePngInto`): the decode was the dragon's 15.7 of 16 MB and the Mecha's 31.5 of 33. A variant not drawn
  over its default no longer decodes the default's picture too. The journal keeps the looks it showed 5 minutes after
  it closes (`Looks.KeepPanelHoldsAWhile`; the user's play: browsing again built 14 looks, about 106 MB, in 5 minutes).
  PNGs decode to ARGB32 here: `DecodePngInto` puts each pixel's bytes in RGBA order from a kept `int[]` (the first
  version read RGBA32 only and changed nothing; checked against Unity's own conversion pixel for pixel). After: the
  Mecha 32.8 -> 1.3 MB a build, the critters and Royal Parade 1.8 -> 0.2, Armored 2.3 -> 0.6; Hellfire 6.0 -> 4.1 and
  Larry 6.2 (their own paths, left). F11 2054/0/0.
- **More of the review's rows** (2026-11-09, docs/performance.md "More of the review's rows, fixed"): the sounds'
  resolve joins its keys once (`SoundBlock.Key`, `SoundWho`); the last sound's text is said when read; `IsRegistered` a
  flag (`Instance.InRegistry`); the base frames' scan waits for a loaded game; the profiler times only for the dev
  panel, a measured phase or the self-tests (`Perf.Timing`); the companion keeps each baby's looks per family and reads
  the catalog's entry.
- **The last four rows: Hellfire, Larry, the babies' routes, the journal's rows** (2026-11-10, docs/performance.md
  "The last four, fixed"): Hellfire's erase reads the game's frame into a shared frame (`LooksRecipes.ReadFrameShared`:
  a kept texture and canvas) and writes into one array kept per skin, instead of a new read-back and a copy of it for
  every picture; Larry's read-backs go through the same shared frame, and its scaled and empty pictures into arrays kept per producer
  (`LooksGame`). Recipes another mod registers still get a frame of their own (`ReadFrame`). A baby's route search
  borrows its grid from the baby (`FoalRouteSearch.Grid`: up to 64 x 64 cells, about 73 KB kept per baby that has
  searched, cleared per search) and its four tests are made once per baby, instead of 30-72 KB and four closures new
  on every search. The journal's rows are kept on a pick and pointed at the new mount's rows of the same shape
  (`RowControls.Bound`, `Shape`, `Rebind`; kind, label, range, greying and column the same): a pick no longer
  destroys and makes the rows' controls (2.4-3.8 ms and 300-360 KB). Opening the journal still builds them once (the
  panel is made on each open). A key or button row is never kept (its clash or its word is fixed when built). The
  journal survey checks that picks keep the rows and that every kept row reads the picked mount's own setting.
  After (F11 2054/0/0): a pick 0.63-0.89 ms and 8-12 KB in all (56 rows kept, 0 made over 7 picks); Hellfire 4.1 ->
  0.4-0.5 MB a build, Larry 6.2 -> 0.2-0.3 MB.
- **The review's small rows** (2026-11-10, docs/performance.md "The small rows, fixed"): row 11, the flight's "is
  this one of our views?" for every cast hit keeps the views in use as a set (made again when a view or the instances
  change: `Instance.ViewsChanged`) and looks the hit's Wgos up in it, instead of walking every instance with
  `IsChildOf`; `ObjectTop` reads its cache before looking for the Wgo. Row 15, `Looks.StandIn` and the new
  `FindWanted` (`Find(StandIn(id))`) keep their answer per id until a skin or mount is registered or withdrawn
  (`Looks.RegistryVersion`): a skin that isn't installed took the slow path 2-3 times a frame per mount. Row 17,
  `KeyCapture.ClashKept`: each key row's clash kept until a key row's key or the language changes (compared without
  allocating on every call), for the options window and the journal (`Clash` itself unchanged). Row 22: the journal's
  gamepad callback made once (a closure every frame, the journal closed too); `KeeperMask` once a frame per layer;
  `DropCartIfRefused` waits for its retry before making its words; `AllSlotsLut` reads the materials into a kept
  list; the feature rules copied once a poll instead of once per feature; a behaviour override marks each mount's
  colours once instead of every mount's once per mount; the colours' "attached" line built only with room in the
  log budget (`Mount.DiagRoom`). Not taken: row 19, the lantern's writes (see the doc). The cart and ride keys start
  the dev `[pick]` trace (a 25.8 ms frame was seen once on the cart key). F11 2057/0/0. Riding through the herd: 7-16 KB
  a frame (53.6 on 2026-11-09), skin builds no longer among it.
- **The proof run** (2026-11-10, docs/performance.md "The proof run"): three runner suites, dev panel off, against
  2026-11-09's: worst frames walk 3.40 -> 0.71 ms, idle 3.84 -> 0.67, hitch 17.45 -> 14.48; garbage 50-90% lower
  (52-195 B a frame); memory flat. The milliseconds a frame read higher with 18-19 mounts in the save against 14: not
  comparable. F11 2057/0/0 with the icon warm-up and the LUT fix.
- **The journal's icons made ahead** (2026-11-10, docs/performance.md): `MountIcons.TickWarm` (the `Warm` part) makes
  one mount's list icon a frame, 3 s after a game is loaded, again when a mount or skin is registered or withdrawn; the
  first opening made them all in one frame (19-24 ms and 2.5-3 MB: a scan of every sprite in memory per game sprite's
  name, the packs' pictures decoded). A game sprite not found while warming isn't remembered as missing.
- **Colours: no 128 KB copy per LUT** (2026-11-10): `Colors.BuildLut` copies the identity table into one kept array
  instead of cloning it for every LUT built (124-136 KB a view made with colours, and a colour change).
- **Dev step traces for one-off frames** (2026-11-10, docs/performance.md): a mod's key (`KeyActions`, over 2 ms),
  the companion's frame and the ridden frame during a pick trace (over 3 ms) write `[steps]` lines; the cart and ride
  paths (`Use`, `BringCart`, `Hitch`, `RideFromWorld`, the ridden tick, the seat check, `Mount.Say`) and the colours
  attach (views probe) mark their steps. `StepTrace.Begin` returns whether it began; `StepTrace.End(minMs)`.
- **The other skins' colour rows biggest part first too** (2026-11-08; the Donkey Skin Pack's CHANGELOG): judged on
  the pictures, a part that sits on another right after it. Royal Parade: Saddle cloth, Trim and fringe, Emblem,
  Crown, Jewels. Armored: Body armour, Skirt, Helmet, Plume, Brass (trim over both helmet and armour, last).
  Hellfire: Horns, Lava cracks, Tail, Tail tip, Glowing eyes. Mecha was in order already (Armour, Gold trim, Horns and
  claws); the Red Dragon has one part. `make_armored.py` and `make_hellfire.py` write the new order (`--cfg` equal to
  the pack's); `row_sizes.py` gives a part its own colours over a `[Colors]` listing of them.
- **The Jester's colour rows largest part first** (2026-11-08, the user: "descending order of the largest, most
  visible part", the donkey's rows first): its parts' rows are Caparison, Caparison second colour, Cap front half,
  Cap back half, Cap band, Bells (a skin's part rows follow its `[Tone.*]` sections' order in skin.cfg), by each
  part's largest single piece, not its pixels added up (eight bells outnumbered a cap half's pixels, each a speck:
  "the bells should be after the cap since they are smaller on screen"), one thing's rows together (the cap band
  under the cap's halves). New `tools/skins/row_sizes.py <skin
  folder>` measures it and says the order.
- **The Jester's harness is its own and the Harness row colours it** (2026-11-08, `tools/skins/jester/`, no pack;
  docs/mount-colours.md 7.6; the user: the collar "stayed red and the original harness underneath, which should be
  invisible ... is getting the actual cream colour"): the collar band's shades are under `[Colors] Harness` (the
  Collar row is gone); the band runs from the crest to the chest and tapers to a point with the bell under it (its
  own shape and colours: a point of its own colours, at another angle, read as detached and stayed red), edged in the
  donkey's outline colour, and the donkey's own strap is hidden in every frame (drawn over as the neck and chest), so
  none of it shows (16 px did standing, up to 59 turning). Drawing the strap over in the harness colours instead read
  as the donkey's harness, broken at the chest under the bell (the user).
  The collar's points are all the harness's now, the front view's three too (their two rows, Collar points and
  Collar points, second colour, coloured only the turn, which the colours window doesn't show).
  `check_skin.py`: a region all covered whose row colours the skin's own listed colours is no longer a warning.
- **F11 "skins: no leaks over spawn and remove" no longer depends on luck** (2026-11-08): a skin is put on by an
  active donkey only (within 15 of the keeper or on screen), and the test scattered its 40 over 30 m; in one run all
  14 snail wearers of cycle 2 fell outside, the snail skin never loaded and the check failed. The first snail and rat
  of each cycle now stand 3 and 6 from the keeper, and the line says each skin's active wearers.
- **The Jester's cloth chequered** (2026-11-08, `tools/skins/jester/draw_jester.py`, no pack; the user's boxes and
  marks on walk 02: "the marked regions have repeated colours"): the cloth's part carried forward to the strap had
  the middle column's colours, so burgundy and navy repeated beside the harness. Now, from the front: under the
  collar's burgundy band (the top row's first check) the bottom row is navy (strap to 41); then a check of navy over
  burgundy (to 49, reaching 2 columns into the cloth so its top shows in the standing frames too); then burgundy over
  navy, and navy over burgundy at the rear. The collar's rear point (navy, over the front check) is gone; its front
  point stays burgundy (navy, it ran into the navy under the band). The hem has four points, evenly spaced (42, 49, 55,
  62), alternating navy and burgundy, each with its bell (there were three, with a gap at the front). The split moved
  57 -> 58, the folds to 54 and 62. The front check has a fold down its middle (46) through both
  rows, as the others ("arent they missing a split in the middle"), and one at 42. `skin.cfg`: "chequered".
- **Mecha's tail tip steady while walking** (2026-11-08, `make_creatures.py`; docs/dragon-references.md 17; the user:
  "black spots ... flicker while walking", walk 04 and 08): the tail rings' fixed angles came from a resting tail built
  with the walk's bob, so they took three angles through the walk; now from the tail without it. And the brown under
  the tail ends at a fixed length under the last ring (it came and went under the tip, 4 to 9 px a frame). Mecha's
  pictures only; the Red Dragon is unchanged.
- **The Armored Dragon is named Mecha** (2026-11-08, the Dragons pack; docs/dragon-references.md 16): its long name
  didn't fit the Mount Journal's Skin row in the F11 test language, and fitted German and Russian with no room to
  spare. `skin.cfg`, the 11 language files (Mecha, Меха, メカ, 메카, 机甲) and the generator's creature table; the id stays
  `saddleup-dragon.dragon/armored`.

- **The rider readout, `[rider]` lines** (2026-11-07, `RiderReadout.cs`; dev, off by default, the Flying tab): flying
  the dragon, the keeper rocks as he does walking (the user). Per frame, at the camera's pre-cull: the picture shown
  and the game's sprite under it, the keeper's state, the walk bob, the seat, the lift, which LateUpdate ran first (the
  mount's placing or the seat), and the rider's hip against the skin's seat row as drawn; a summary every 2 s. For the
  investigation in docs/rider-motion.md "In flight", before a fix. The user's run (22:08): the mount was always placed
  before the seat; in the air the rider's only movement on the mount was the walk bob (±1.5 px) from the game's walk
  under the flying pictures.
- **Rider motion per mount in the F11 panel** (2026-11-07, `Dev/TuningWindow.Mounts.cs`): the Mounts tab has a "Rider
  motion on this mount" section (Bob, BobInFlight on a flying mount, BobScale, BobLagFrames, HeadLagFrames,
  HeadSettle, HandsSteady, HandsSwayPx), saved into the mount's `mount.cfg`; the Riding tab has the player's
  BobInFlight. The legs of a skin without its own seat row hang from the back's resting height on its flying pictures
  (`RiderLeg.Update`'s `still`).
- **Integration test "rider: still on the dragon's flying pictures ..."** (2026-11-07, group Flying): the first test
  flight with the keeper walking (the game's stick held, the body held by the flight): the hidden walk runs, the
  rider's walk motion is 0 on the pictures, back with the dragon's own BobInFlight, and the bob on the ground.
- **The Armored Dragon's gold trim shows** (2026-11-07, `make_creatures.py`; the user, on the colours window: "what is
  that gold trim item?" then "make the gold trim visible"): its Gold trim row had 12 rivets, 1 in view on the ground.
  A plate's rim is now gold even where it meets the hide (the line colour took it, so the chest and hip plates' rims
  never showed), the neck's plates have one, and the helm's crest is gold; the row takes the three golds (A4800B
  CFA90F F1D274). Rigid parts only (the old greave and helm rims blinked as they turned).
- **Metal wings for the Armored Dragon** (2026-11-07, `make_creatures.py`; the user: "pierced metal instead of like a
  tear in a cloth ... isnt its wings made of metal?"; picked: "lets settle with the armored darkened version"):
  `DRAGON_ARM_WINGMAT` leather | steel | blackened (the shipped default: the wing's surface in the armour's darkest
  steels, its stretch lines plate seams) and `DRAGON_ARM_HOLES` tears | eyelets (the default) | punched (a hole in a
  bevelled steel ring, a stamp on a whole
  pixel, clear of the fingers). On the spread wing (the user: "then its pointless to have them when walking") each hole
  sits at its panel's roomiest point that frame (between two fingers, farthest from both and from the edge: a fixed
  spot fell into the saw-blade's notches), a 2x2 with its glint and shadow, a pin for a frame where the panel narrows:
  three holes on the near wing in every frame of the beat and the glide (one a pin in the frame the wing turns
  edge-on). `DRAGON_ARM_SEE` (on by default) makes the holes see-through (the user: "so its actually a hole"): their
  pixels left out of the wing (the sky, the body or the other wing shows), a steel rim round them, and the frame's
  outline pass and pocket fill leave them empty. The pack's Armored Dragon is this version (with the gold trim): its
  Wings row is gone (the wings are Armour), Armour takes the darkest steel, and the row's line is out of the 11
  language files. After a fresh review, each spread-wing hole aims at a fixed point of its panel and takes the nearest
  cell where a whole 2x2 fits (they slid on the glide's still wing and changed size), 4 px from the others (two had
  merged into one slot). The studies and the game's test copy are gone. The preview page showed the game's export of the
  skin (an F11 run with the leather pack) over the repo's pictures: that stale export folder removed; the next F11
  run writes it again.
- **The hillside jump test allows a hitch's catch-up** (2026-11-07, `Dev/SelfTest.Yard.cs`, docs/flying.md "A hitch,
  not a cut"): it failed on 4.5 px in a 10 ms frame right after a 27 ms one, the drawn mount catching up the two
  physics steps that slow frame ran. The cap is 4 px a physics step now: times the steps the longer of the frame and
  the one before spans (a steady rate keeps 4 px; a jump cut, 17 px and more, still fails).
- **The translations scenario makes a skin not installed itself** (2026-11-07, `Dev/TranslationTests.cs`,
  docs/missing-skin.md 9): it measured that row only when the player's cfg happened to hold one (it failed on a deleted
  test skin). Now every language's layout pass gives each mount's Skin row a made-up skin and measures the journal
  again, and the test-language pass checks the row shows the name alone and the Stats card "Skin: Not installed"; the
  value is put back after each. `MountSettingsPage.SkinNotInstalled` (internal) says which id that is.
- **A new dragon skin or a new dragon mount: the study** (2026-11-07, `docs/dragon-references.md`): the user's nine
  dragon references read close up and at the players' size, what a skin and a mount can each change, the costs, the
  ranked skin candidates and a recommendation. Nothing drawn yet. The user picked an armoured skin; after the first
  roughs ("not that different from the default") a second direction: `make_creatures.py armored-dragon <folder>`
  draws an Armored Dragon study from the dragon's own generator (`dragon(style="armored")`: steel plate, leather
  wings with a steel edge and claws, curled horns, a mace tail). It is in no pack, and the Red Dragon's pictures
  are unchanged (237 of 237 equal). Since then: the plates as pieces of their own over the scales, the game knight's
  steel (its 9019_ armour in the MainCharacter atlas), head and wing shapes as switches (`DRAGON_ARM_HEAD`,
  `DRAGON_ARM_WING`), and the wing's detail; a test version (head greathelm, wing saw-blade) as a loose folder in the
  game install only, `gk2mounts\saddleup-dragon.dragon\skins\saddleup-dragon.armored\` (docs/dragon-references.md 11).
  A second frame-by-frame pass (docs/dragon-references.md 12): always four neck plates (the lowest came and went
  with the walk's bob), the neck one piece with the head in flight too, no gold rim on the greaves, rivets counted
  from their plate, and half pixels rounded the same way on every row (the helm's breathing holes jumped a row);
  after an independent review, no rivets on the wing's forearm (gold blinking in flight), always four tail rings at
  fixed places, and the wing's arm held in the glide (its steel edge crawled). Then, walking, the neck and head
  ride the body's bob (they slid against the chest: "not attached"), and the blade on the neck with them. Seen in
  game and **into the Dragons pack** (the user: "works in game", 2026-11-07): `skins\armored\` of
  `saddleup-dragon.dragon` (the pack's own changelog says it for players), its words in the pack's 11 language files;
  the generator's head and wing default to the picked ones and a plain run draws it with the others (no longer only
  when named). The loose game copy and the study folder are gone (the page shows the pack's skin).
- **`tools/skins/frame_check.py`** (2026-11-07): frame-by-frame checks of a skin's pictures, read-only. `closeup`
  enlarges a clip's frames with the main and front layers apart, `shift` tells a part that only moved from one that
  changed, `attach` measures a part's move against the body's, and `gold` counts a colour's pixels per frame.
  Then `sweep` (every frame pair of every clip with the motion taken out, block by block: each group of pixels still
  changed, with what it moved, a part that moved 0 or 1 px yet changed marked SHIMMER) and `heat` (the same as
  heatmaps, four clips a row).
- **The Armored Dragon's polish pass** (2026-11-07, docs/dragon-references.md 14; the user: "make sure its consistent
  between frames ... polish the armored skin as much as possible before releasing"): the tail's rings and the legs'
  greaves drawn as stamps (the direction snapped to 15 and 22.5 degree steps, the anchor on a whole pixel, the
  greaves lit from above like every other plate), so a ring or greave that turns a few degrees keeps its pixels.
  After an independent review: the plates' corners on a 1/64 px grid (a floating-point tie gave the helm's crest an
  extra cell for one frame of the climb), no notch in the spread wing by the body, and a grass pocket shut in between
  the near wing and the body drawn as a shadow. Then (the user: the tail's "white lines ... moving a lot") the
  rings on the ground at one fixed angle each, the resting tail's (snapped, the walk's sway flipped them between
  steps), the tail's own angle in the air, and the legs' bones from whole-pixel ends. The pack's other mounts'
  pictures unchanged (a plain
  `make_creatures.py` run changes only the Armored Dragon's).
- **The skins preview page reads a mount's variants as the game does** (2026-11-07, `tools/skins/skin_preview_html.py`):
  a skin built on its mount's default takes the default's canvas, seat and layer when it leaves them out (before,
  the Armored Dragon study showed as a 96 px donkey skin). The study's card is
  `tools/skins/creatures/out/study/armored-dragon/` (git-ignored), one card with its loose copy in the game.
- **A self-test for the journal's ride and cart keys** (2026-11-07, `Dev/SelfTest.PanelRide.cs` `PanelRideKeys`, group
  Looks): on a test page of its own over the test companion, so the player's mount is never called. The integration
  test of Saddle Up's keys over the panel now also checks that its ride and cart keys are the journal's buttons.
- **The game's stop works through the riding pose** (2026-11-07, `Patches.cs` `PlayerPhysicalBody_StopMoving_Patch`):
  `PlayerPhysicalBody.StopMoving` asks the keeper's animator whether he walks, and while mounted that shows the riding
  pose, so it never stopped him: `PlayerData.charState` stayed Walk and the donkey mirroring it walked. Its callers are
  the controls taken (`PlayerController.HandleControlChange`: every modal window, which also pauses the game), a path's
  end (`OnPathComplete`) and `Flow_StopGoTo`. Mounted, the stop now reads `charState`. The game's pause stops only its
  update manager, and Unity's clock runs on, so a standing donkey or a baby whose step was held kept its walk clip:
  `StandState` is Idle while paused (`IdleShuffle.GamePaused`, shared with the shuffle's and the hops' holds). New
  self-test "the game paused mid-walk" (Sounds group): it opens the pause menu over a ride with the stick held.
- **The self-tests hold the game's own story** (2026-11-06, `Dev/GameQuiet.cs`): while a run goes on, no quest trigger
  fires (`GlobalEventsSystem.FireTrigger`) and no zone runs its script (`GDZone.ExecuteEvent`). The world zones its walks
  visit are taken off the visited list again. Patched only for the run. The yard's jump test had slid the keeper into
  the morgue's story zone: the event `Event_33_Base_Complain` took his controls (14 tests refused), completed
  `33_base_complain_morgue` and started `42_crossroad_nun_donkey`, in memory only.
- **A parent that doesn't shuffle no longer takes its baby's slot** (2026-11-06, `Foals.cs` `Taken`): its body was read
  with `BodyAt`, whose offset is measured only while the shuffle runs, so it stood at its root, 0.44 from the baby's
  own slot. Without the offset, its view's body point (`BodyPoint`, as the lead's own Body). Found by the new no-room
  test, which skipped at four spots: "its own slot has another donkey's body on it".

- **The wedge's ways out go somewhere** (2026-11-06, `Foals.cs`): a rejoin leaves out a target where the baby stands
  (`PickReachableSlot`'s `skipWithin`, the arrival distance); a rejoin resets the wedge count, so a new wedge takes three
  fresh failed picks over 8 s; a wedged baby's catch-up asks `FreeSlot` first and, put back where it stands, it stands
  without room instead (not lost, said once for the spot). The wedge's lines are at most six per baby.
- **The stuck report leaves out babies still by design** (2026-11-06, `FoalHops.cs`): idle ones, and asleep ones whose
  parent `AsleepTick` doesn't go to (`AsleepGoesTo`, one test for both). Its line names the parent's level and real
  view: "the lead has its view" was `Lead.Here`, its view or its scene loaded.
- **Self-tests** (Levels and views): "foals: a baby with no room beside its parent stands there" (a U of temporary
  walls) and "foals: the stuck report leaves out a baby still by design" (an asleep pair, and an active baby shut in a
  pen still reported).

## 0.16.0 (GK2 Donkey API 0.44.0), 2026-11-05

For players:

- **A mount whose chosen skin isn't installed is still itself** (2026-11-05): with the skin's pack removed (or a skin a
  pack renamed), the mount turned into a plain donkey: drawn as one, ridden as one, its babies too. It wears its own
  look now and keeps your choice for when the skin is back. The donkey's own skins are as before.
- **A mount you call brings its babies** (2026-11-05): picked in the journal or called with H or G from far away (the
  house while you are in the yard), its babies came only once you got off it and left it standing. They come with it
  now. Back to the last safe spot while riding brings them too.
- **The options have five tabs** (2026-11-05): Keys, Mount, Cart, Safe spot and Freeplay. Mount Behaviour and Sound
  are in the Mount tab now, under their own headings, and keep your settings. The Mount tab's button is called Mount
  Journal, like the window, and "Send your mount away" comes right after the keys that call your mount.
- **Leave your mounts in the world** (2026-11-05; a new option in the Mount section, off by default). When you
  call another mount, the one you had stays where it is with its babies, and is kept with the save. Pick it again in
  the journal and that same one comes back to you. Your cart is left standing beside it; G or Drive the cart brings it
  to the mount you have. "Send your other mounts home" (a button under it, shown while the option is on) sends every
  mount you left home with its babies; a cart one of them pulls stays standing. Turn the option off and the mounts you left go home too (one you are
  riding goes once you get off it).
- **Translation fixes** (2026-11-05): the colour names in Brazilian Portuguese, the custom colour row in Japanese,
  the Outline row in Korean. All three are texts new in this version, so no translation a player has seen changed
  (the 0.15.0 zip's language files against these, 2026-11-05): not in the player notes.
- **The outline has a colour row of its own** (2026-11-04): Outline, on the donkey and every donkey skin. Game default
  is the game's border, so nothing changes until you pick a colour. The coat no longer colours the line under the
  belly and the nose.
- **Legs and mane no longer colours the donkey's eye and nostril** (2026-11-04): the eye stays as drawn and the
  nostril takes the Muzzle colour, on the plain donkey and every donkey skin.
- **Hellfire has one row for its eyes** (2026-11-04): its glowing eyes cover the donkey's, so the Eyes row coloured
  nothing; it is gone, and Glowing eyes colours them.
- **A coat colour you pick shows as itself on Hellfire** (2026-11-04): Hellfire darkens the donkey's coat to ash, and
  a colour on top of the ash stayed dark (Cream came out dark brown). A colour you pick now replaces the ash; Game
  default keeps it, and Dark and Black darken it. The same holds for the Jester's harness (a skin still
  in progress, not in this release).
- **The donkey's Muzzle row colours its nose** (2026-11-04): it was "Muzzle and belly" and also reached a few dots on
  the face and a line along the belly; the dots follow the coat now, and the line is the outline's. On every donkey
  skin.
- **Zombies on the cart during Halloween** (2026-11-04): from 24 October to 7 November the game gives some
  zombies a Halloween head. Such a zombie on the cart showed as an icon (and a warning in the log); it now lies there
  with its Halloween head, as on the ground.
- **A skin can have colours of its own** (2026-11-03; the user, on the Jester: "harness color should only control
  the color of the harness and then the skin can introduce its own color tones depending on what it needs"). A skin
  names the parts of its drawing a player colours on their own (a cap, a cloth, a plume), as many as it needs, and
  each gets a dropdown of its own in the Colours window, labelled with the part's name: Game default, Red, Gold,
  Green, Blue, Purple, Pink, Brown, Black, White, or Custom with its lightness, kept per skin like the rest of the
  window. Coat, Harness and Second tone keep meaning the mount's own parts. Self-test "colours: a skin's own tones"
  (the Skins group); docs/mount-colours.md section 7, docs/skins.md "The skin's own colours: tones".
- **Hellfire, Royal Parade and Armored have their parts as dropdowns of their own, and every saved look stays as it
  was** (2026-11-04; the user: "his old customization will automatically be converted to our new system as much as
  possible so that from his own perspective, it would look like nothing or almost nothing changed but now there's
  more customization available"), every part its own ("every part is named and is able to be coloured"). Royal
  Parade: Saddle cloth, Crown, Trim and fringe, Emblem, Jewels. Armored: Helmet, Plume, Body armour, Skirt, Brass.
  Hellfire: Horns, Glowing eyes, Lava cracks, Tail, Tail tip. A part that was coloured by Harness or Second tone
  takes the colour saved for that row once, on the first start after the update, so it looks as before, and is a part
  of its own from then on (below: every row of the Colours window names a part). The parts that were drawn as they are (Royal Parade's crown, trim, emblem
  and jewels, Armored's helmet, body armour and brass) start as drawn. The skins also read as before on an older GK2
  Donkey API. docs/mount-colours.md 7.5 and 7.9, docs/second-tone.md 8.
- **Hellfire's colours come out exactly as drawn** (2026-11-04): a skin over the donkey with more than 48 colours
  went through the game's colour cube, which rounds every colour to 32 steps and, with a harness colour set, turned
  a few of the donkey's outline and coat pixels under the skin that colour. The exact colour swap takes up to 96
  colours now, enough for every skin over the donkey with all its parts coloured (Royal Parade, Armored and Hellfire).
- **The colours window shows the mount at once** (2026-11-04): its picture could stay empty for a moment after it
  opened (MountsPanel.OpenColours now asks for the picture at the next frame, not at the 0.2 s picture timer).
- **Every row of the Colours window names a part of the look** (2026-11-04; the user, on "Second tone": "it's not
  self explanatory about what it refers to since the skin now defines every part itself"; docs/second-tone.md). No
  row says "Coat colour" or "Second tone" any more: the donkey's read Coat, Muzzle and belly, Harness, and two new
  ones, Legs mane and ears and Eyes; the dragon's Scales and wings, Belly, Horns and spikes (a part of its own), Eyes and claws; the rat's
  Fur, Ears nose paws and tail, Eyes; the snail's Body, Shell, Eyes; the worm's Skin, Mouth, Eyes; Gerry's Bone. A part
  the look doesn't have has no row (it was greyed). The dark parts and the eyes can be coloured for the first time;
  the outline round a mount is never recoloured.
  A part that used to follow another row (Royal Parade's saddle cloth the harness, Armored's plume and skirt and
  Hellfire's lava the second tone) is a part of its own: on the first start after the update it takes the colour
  you had saved for that row, so it looks as before, and from then on that row colours only what it names. Saved
  colours stay where they were.
- **A part you give a colour takes each baby's own shade** (2026-11-04; the user: "ideally yes unless it's a
  performance cost we can't afford"). A baby already wore its own shade of its parent's coat, second tone and dark
  parts; a skin's part given a colour of its own (a saddle cloth set to Gold) now takes that same shade on each baby,
  so a baby reads as one animal, its trappings with its coat. A part on Game default stays as drawn, as the harness
  does. Nothing more to draw: a baby has colours of its own already.
- **The start-up check of the game's code covers the pathfinding too** (2026-11-03; the user's go after the 1.011.1
  audit). The API queries the game's A* pathfinding directly (the safe ground, the mount): 29 members, which the
  check could not see, and 1.011.1 recompiled that assembly. Its members are now in the dependency manifest like
  the game's own, and its module id is counted in the fast path and in the cached verdict's key, so an update that
  changes only the pathfinding is compared too. Manifests without the new `module-mvid` line read as before.
  `docs/dependency-check.md`, "Scopes"; self-test "the start's check of the game's code" (the core group).
- **The donkey's art is checked against the art the skins were drawn on** (2026-11-03). Once a session, with a mount
  out in a loaded game, the API reads back the game's 48 donkey frames, as the skins' builds do, one a frame. It
  compares them with the fingerprints of the frames every skin was measured and drawn on (`donkey-frames.txt`, from
  the asset probe's export of 2026-10-17). Every frame the same: one info line. Otherwise one warning names the
  changed, missing and new frames; a skin drawn over the donkey may not line up in those until a re-audited build.
  It changes nothing else. The design doc had described a key check that was never built (`docs/skins-design.md`
  section 11, now what exists). Self-test "the donkey's art" (the Skins group). In game 2026-11-03 22:42: the 48
  frames the same as the list (the probe's export and the API's read agree byte for byte), read in 338 ms over 48
  frames with no slow frame; F11 1799 passed, 0 failed.
- **Audited against game 1.011.1** (Steam build 26404424, 2026-11-03; `docs/game-update-26404424.md`): a hotfix of the
  zombie assistants' autocrafts and two save fixers. None of the 866 game members the API uses changed, so every
  build made for 1.011 (the released 0.13.2, 0.14.0 and 0.15.0 among them) runs on 1.011.1 as it is, with one info
  line at start: no re-release is needed. The changed art is 49 scenery sprites, none of the donkey's. The audited
  build moved in the READMEs, the release checklist, `tools/release-versions.py` and the probe's pin.
- **A jump over things no longer puts you back where you jumped from for nothing** (2026-11-02, found by the
  self-tests: over the conveyor's debris in the home yard, carried off 3.9 onto open ground and sent back). The
  landing asked the game's walk grid for a way from the take-off, but that grid only covers a 4.4 square round the
  keeper; a take-off further away was looked up at the grid's edge, there a lone cell among the debris, joined to
  nothing. Where the grid holds no cell of the take-off's own, its answer no longer counts and the keeper's own
  ground decides (here it had said he walks off: 0.8 to the take-off's ground). Jumps within the grid are judged as
  before. Self-test "jump: a take-off past the game's walk grid"; docs/flying.md "The conveyor's debris". F11:
  1787 passed, 0 failed, the conveyor's debris jumped and landed.

For mod authors:

- **A skin id that names no installed skin, its mount installed, wears that mount's own default** (2026-11-05;
  docs/missing-skin.md). Before, it fell back to the donkey in eleven places: the drawing, the instance's mount, the
  companion's `MountId`, its look and the events that re-apply it, a left one's look, `PinnedBy`, the page's skin rows,
  the spawn's cart rule, the warm sheet and the record's label. Each now looks up `Looks.StandIn(id)`. The saved id stays
  where it is stored (the companion's `Skin`, each instance's `Skin`, the cfg, the side file). The donkey's own skins, a
  skin of a mount that isn't installed and a set-aside skin are as before.
- **`ComeToKeeper` brings the mount's babies** (2026-11-05; docs/foals-called.md): every baby of it in another scene,
  or farther than `ShuffleFollowMaxDistance` from where it now stands, is placed by it, as the door does for a ridden
  mount's (a baby waits for its parent to stand, and a mount called for a ride never does). The ones within reach trot
  over as before. `Mounts.ReturnToSafeSpot` while riding does the same for the ridden mount's.
- **`MountSettingsPage.Button` takes `visible:`** (2026-11-05): the button shows only while it returns true, as a
  Toggle's `visible` (a button with nothing to do while another row is off). The signature without it is kept for mods
  built against it, every parameter required, so no call is ambiguous (`tests/ApiSurface` snapshot updated).
- **A donkey view let go no longer stands in the world till the frame's end** (2026-11-05; docs/pooled-parts.md).
  Restored for the game's pool, its colliders and navmesh cuts were live until `Destroy` at the frame's end, so any
  check later in that frame (a hitch right after an unhitch, a spot judged for the keeper) found an obstacle no mount
  owns where it stood. The view is switched off at once now.
- **`MountCompanion.LeaveMountsInWorld`** (off), **`LeftMounts`** and **`SendLeftAway()`** (2026-11-05; docs/leave-mounts.md): another
  mount chosen (`Skin`, the Mounts panel's Ride and Drive the cart) leaves the one out where it stands with its babies,
  under the keys `left:<mount id>` and `left:<mount id>#foal` ..., kept with the save; chosen again, that one is the
  companion's again (`donkey`, `foal` ...). One of each mount at most. Off, no mount stays left: switched off, or a save
  loaded with it off, the mounts left go home as `SendLeftAway` sends them (one ridden by hand once he is off it; that
  one, chosen while he rides it, is the companion's again and the one out goes home). The cart key fetches the companion's cart from another of its own mounts that
  has it (a left one, or its current one standing with it while a left one is ridden), never a second cart; for another
  mod's mount ridden it is as before.
- **The donkey's outline is a region, `Outline`** (2026-11-04; docs/mount-colours.md 7.15, docs/skins.md): its row
  comes with `ToneRows` (`Outline<suffix>`, `CustomOutline<suffix>`; `MountLook.Outline`, an addition), named
  "Outline" on the donkey and every skin over it; `[Region.Outline]` `row = none`, `name =` and `[Base.Outline]` as
  for the other regions. A skin's own line art joins it with the new `[Region.<region>]` `colours =` (read as a
  `[Colors]` line; an API without the region skips the section, where `[Colors] Outline =` would make it refuse the
  skin). `Mounts.ColorRegions` lists Outline after Harness; 42422c left Coat for it.
- **A skin can take a colour row away** (2026-11-04; docs/skins.md, docs/mount-colours.md 7.13): skin.cfg
  `[Region.<region>]` `row = none` gives a region no row (a part the skin covers or doesn't want coloured); it is drawn
  as the look has it, and a colour a player saved for it is kept but not applied. A variant takes its default's away
  unless it names the region. Code skins: `MountSkinDef.RegionsWithoutRow`, and `MountSkinDef.RegionNames` for the
  names a pack gives with `name =` (both additions).
- **A skin's own tones** (2026-11-03; docs/api.md "A skin's own tones"): skin.cfg `[Tone.<id>]` (`name`, `colours`,
  `follows`) and `MountSkinDef.Tones` (`MountSkinTone`: id, name, a list of colours; `MountSkinTone.Following` for one
  whose Game default follows a mount region); `MountSkinInfo.Tones`; `MountLook.Tone(id)`, a `MountTone` (`Color`:
  `ToneColor`, `Custom`); `MountSettingsPage.ToneRows(section, skinId, suffix, companion)` inside `PerSkin`, keys
  `Tone.<id><suffix>`, `CustomTone.<id><suffix>`. A colour is in one place only (a tone or a `[Colors]` region; a
  following tone's colours may stay listed under its region too): a tone naming a colour another place names, an id
  other than lower case letters, digits and `-`, or a `follows` that is no mount region is left out and said in the
  skin's report, and the skin loads without it. Additions only, so the next API version is a minor
  (`tests/ApiSurface` snapshot updated). An older API ignores the section: a following tone listed under its region
  too keeps colouring as that region there, any other is drawn as drawn over the donkey (Coat in a skin's own
  colours); say `needs =` the API with tones only where that isn't good enough. Saddle Up gives every skin's tones
  their rows (one line after its Second tone rows; it names no tone).
- **A look names its parts** (2026-11-04; docs/api.md "A look's names for its parts", docs/skins.md): skin.cfg
  `[Region.<region>]` `name =` names a mount region's part (translated as `skin.<skin id>.region.<region>.name`); a
  variant takes its default's, a skin over the donkey the donkey's (the API's words). `MountSettingsPage.RegionRows`
  labels a page's region rows by those names and leaves them out where the look has none of the region; `ToneRows`
  also makes Dark and Eye rows where the look names them (`MountLook.Dark`, `MountLook.Eye`, `MountTone`s). A tone with
  `follows =` is a part of its own now: `ToneRows` gives it the player's saved value of the region once (while the
  file holds none of its rows), the engine no longer pairs it with the region; the "As the ..." choices are gone.
  Additions only (`tests/ApiSurface` snapshot updated).
- **A skin over the donkey keeps the exact colour swap up to 96 pairs** (2026-11-04; 64 that morning, 48 before):
  the donkey's 42 and the skin's named colours, so up to 54 colours of a skin's own parts with every part set; past
  it the colour cube. Parts drawn in one ramp need shades of their own (a colour is one tone's only):
  `tools/skins/part_palette.py` finds each part the nearest ones the checks take (docs/skins.md).

Development:

- **The missing-skin fallback** (2026-11-05 night; found in rehearsal B, where the 0.15.0 fixture's made-up dragon skin
  drew the dragon as a donkey; the user: "let's tackle this bug ... as part of the same major update"; docs/missing-skin.md):
  - An audit of every skin lookup by id found eleven places falling back to the donkey; all eleven now look up
    `Looks.StandIn`.
  - The rig's set-aside recheck is untouched, since the stand-in covers only an id that names no skin.
  - Tests: the scenario "a mount whose saved skin isn't installed ..." (the rule's cases, then live: that mount in its
    own look, its baby too, the choice kept, and the donkey's missing skin still the plain donkey), and the integration
    scenario on Saddle Up's dragon row.
  - The whole F11 on it (22:50): 1998 passed, 0 failed, 0 warnings (the 0.16.0 fixture read by both upgrade tests too).
  - The user's hand test then found a twelfth place: the rig's check of a sheet still building compared it with the saved
    id, so a slow stand-in (Gerry's, 44 frames) was let go at every refresh and built again, the journal's picture
    stuck. Fixed (`Find(StandIn(stillWant))`); every read of a saved `Skin` value checked again; the integration
    scenario "a slow stand-in ... is worn though its rig is refreshed every frame".
  - The whole F11 on it (23:20): 2000 passed, 0 failed, 0 warnings.
- **The 0.16.0 release** (2026-11-05; the user's picks: 0.16.0 / API 0.44.0, both packs as 1.2.0, Critters held):
  - Saddle Up's API minimum is 0.44.0, and its out-of-line calls for an older API (`ToneRows`, `RegionRows`,
    `LeaveMountsRow`) are gone.
  - `release.sh` green; rehearsal A and B passed. B was over the 0.15.0 fixture: 189 of 189 values kept, the six moved
    ones in Mount, and the cart with 4 items back.
  - The fixture `tests/fixtures/released/0.16.0/` is made. `tools/page-carryover.py` now knows ToneRows' Dark, Eye and
    Outline rows, and `tools/rehearsal.sh` puts a loose `plugins\gk2mounts\` dev folder aside (A loaded an unreleased
    skin from it; its rows were taken out of the fixture).
  - docs/release-checklist.md section 5 has the run.
  - **What is public, checked again** (2026-11-05 night, the user: "be very diligent regarding what releases were public
    and which werent"). Steam's GetPublishedFileDetails for the three Workshop items: Saddle Up and the Dragons Mount
    Pack last uploaded 2026-11-01 23:23 and 23:24, their sizes exactly the 0.15.0 and 1.1.0 zips' files and their
    modinfo.json (3,373,565 and 622,258 bytes); the Donkey Skin Pack 2026-10-30 15:15, its 1.0.0 DLL and modinfo.json
    (72,838; 1.1.0 would be 118,512); the descriptions on Steam those staged that night. Nexus: the user's word
    (2026-11-01, "i uploaded everything except the updated donkey skin pack"). So the notes run from Saddle Up 0.15.0,
    the Dragons Mount Pack 1.1.0 and the Donkey Skin Pack 1.0.0; Critters was never uploaded.
  - **The notes checked line by line** against the 0.15.0 zip: its language files (no translation a player has seen
    changed, so the translation-fix line left the notes), its texts (the row is "Legs, mane and ears"; the Muzzle row
    no longer reaches the belly) and its colour code (Hellfire 1.0.0 named 13 colours over the donkey's 42, past the
    exact swap's 48: on the cube for every 0.15.0 player, so the harness-colour pixels were a public bug and stay in
    the note). The Workshop texts, untouched for 0.16.0 until then, brought in step with the Nexus ones; the Saddle Up
    Workshop changenote names the missing-skin fix; Saddle Up's README's Skin and Colours rows.
  - **The pages' new pictures** (2026-11-06): eight screenshots, cut on each window's own frame into the 560-wide
    page pictures and the Steam gallery's full-size JPEGs (the options window is a new size: 392,132-2168,1308). The
    leave-mounts picture shows the unreleased critters too (the user: "i want to show the unreleased there"), and its
    paragraph says they come in the Critters Mount Pack later.
  - **Rebuilt with the missing-skin fix** (2026-11-06): the user's commit `e16dd41` removed the first zip (never
    uploaded); `build-dist.sh --rebuild GK2DonkeyAPI` (only the API new), 27 of 27 scans green, the zip written again
    (1,191,828 bytes); rehearsals A and B passed again (B: the dragon with its missing saved skin ridden as the
    dragon), the saves identical; docs/release-checklist.md section 5.
- **A called mount's babies** (2026-11-05 evening; the user: "from ourside i spawned mount b but its babies stayed in
  the house"; investigated in docs/foals-called.md first, "lets investigate this a bit more").
  - The log: the dragon's babies 145 away, asleep, "following" a ridden parent. They came out only once he picked the
    rat and the dragon stood; the rat's stayed inside to the end.
  - The cause: `ComeTo` moves the mount alone, and the catch-up waits for an unridden parent to stand. It's the
    2026-10-24 door bug without a door.
  - The fix: `Instance.BringFarBabies` (the door's own `FollowParentThroughDoor` for each baby out of reach), called by
    `ComeTo` and by the safe spot's arrival check.
  - Tests: the scenario "a called mount brings its babies" (the journal, the ride key, a baby within reach not moved),
    and a baby in "Back to the last safe spot".
  - Seen on the way, left for later: a catch-up loop on a blocked slot, and the stuck report firing on asleep babies.
  - The whole F11 on it (19:19): 1978 passed, 0 failed, 0 warnings.
- **A save and a load of the mounts left, the whole way, in F11** (2026-11-05 evening; the user: "regarding
  persistency with the new option to keep mounts spawned, are we handling that?"). The new scenario "mounts left in the
  world: a save and a load the whole way" (docs/leave-mounts.md section 5):
  - the test mount is left with its baby, name and skin, the cart standing by it;
  - the side file is written for the run's owner into a temp folder, with its own id counter only;
  - all five mounts are taken away, then restored by `Mount.RestoreFromFile` and the companion's load step;
  - each must be back as saved, found as left, with no baby added and one cart, and the same one comes when picked
    again.

  The game's own sleep-and-load stays hand items 4 and 11. The whole F11 on it (18:21): 1969 passed, 0 failed, 0
  warnings.
- **Saddle Up's options regrouped** (2026-11-05; the user: "we have a lot of tabs on the side when a lot of the
  options can be consolidated into single tabs instead of split, like mount and mount behaviour"; review and choices in
  docs/options-regroup.md). Seven tabs to five: Mount Behaviour's four rows and Sound's two are the Mount section's,
  under `Heading`s of those names, after its journal rows (no journal row follows a heading). Measured on paper first:
  10 rows and 2 headings take 424 of the column's 445 px. Each moved key has a `MovedKey` for every old place (Mount
  Behaviour and Behaviour, or Sound); `tools/page-carryover.py`: 0 lost over the five fixtures. The journal button is
  labelled Mount Journal, "Send your other mounts home" has `visible:` the Leave row, SendAwayKey follows CartKey. The
  language files by a one-off script (no new words): `heading.mount_behaviour` and `heading.sound` take
  the retired `section.*` lines' values in every language, `panel.mount_journal` the file's own `MountsKey.label`, the
  moved rows' notes say Mount, in the dump's order (`l10n-sync --check` shows the 4 renamed keys until the next dev
  run's dump). Tests: the integration scenario "options: Saddle Up's five tabs" (the tabs in order, the Mount tab's
  rows and headings, the journal button's label, the keys' order, and drawn with every row shown, the Mount tab without
  a scrollbar), the Leave row's test checks the button shows only with the row on, the StandingSounds test looks in
  Mount; the options Smoke test says each section's height against its room. All scans green. The next release needs
  a fixture of this cfg (docs/release-checklist.md 3b): six keys changed section, the sections after Colours renumbered.
  The whole F11 on it (17:55): 1957 passed, 0 failed, 0 warnings; `l10n-sync --check` "same" after the run.
- **Mounts left in the world** (2026-11-05; the user: "leave summoned mounts along with their foals in the game
  instead of despawning them"; docs/leave-mounts.md). `MountCompanion` keeps a family per mount (the current one and
  the ones left; the look, the babies and the name applied per family, the current one's path as before), switches them
  where the skin is taken (`TakeSkin` -> `SwitchFamilies`: off the keeper, his cart unhitched, the left keys, the babies'
  follow switches off), finds them again at a load by their keys (a save's other mount left, with the option on), and
  the mount left by the keeper steps aside for the one called (`Mount.Instance.StepAsideFrom`, IdleShuffle.cs: checked as
  `StepOutIfInside` checks a spot, clear of the cart it just left). Saddle Up's row `LeaveMounts` (Mount, after Keep
  your mount with the save) and the button "Send your other mounts home" under it, in all eleven language files (the Korean reworded once: 둡 isn't in the game's Korean
  text). Three new self-tests, two in "API, side file, carts, colours" (the switch, babies, adoption, the journal reaching a
  left mount, one cart; off, a load, the side file) and one in "Flying" (a choice in the air waits for the landing), and an integration check of Saddle Up's row. ApiSurface updated
  (+3); the version bump is the user's. Review pass 1 (two fresh readers): a choice in the air now waits for the
  landing (it changed the flying mount in place before), a load never sends a mount home, a mount whose pack is away is
  left as it is, the step aside measures the body on the view, stops a step under way and keeps off carts, and the
  runner holds every other scenario to the old way (the panel's scenarios would have left the player's own mount out).
  Review pass 2: the journal reopened while a choice waits keeps it and its call, a load takes the waiting choice, a
  call while flying a left mount by hand waits too, babies without a key take their slots at a load, `Foals` stays
  one live list for the companion's life (the current family's slots), the adoption comes before the cart check.
  Review pass 3: a call alone waiting for a landing is kept through the journal closed again, a load never sends a
  mount home in any branch, the wait for a landing is for his own left mounts only (another mod's mount refused as
  before), a left one he stands by steps aside when the ride key calls his mount too, a wider mount steps further.
  Review pass 4 (a reader walking the morning's hand list through the code): the left one stepped aside twice at every
  journal switch (the switch, then its call in the same frame, the body still drawn on the old spot), so once per
  switch now and the self-test calls in the same frame as the panel does; ahead of its nose first (a cart brought to
  the new mount stands behind it); Drive the cart refused after the switch brings the chosen mount for a ride. Its
  second reader: a load took a mod's own unkeyed baby of the companion's mount into a slot (only a baby the companion
  unkeyed is marked and taken back now, into its own slot), a companion made mid-game switched nothing but its first
  skin is in place, a choice during a scene load waits for the game, the wait holds while the panel is open again, the
  step aside can't break a switch, the babies' variety applied as before, `LeftMounts` read-only. Review pass 5: a
  waiting call no longer outlives a later call made at once, and the cart is fetched only for the companion's own
  mounts (on another mod's mount his loaded cart would have become that mod's), so the option off is as before exactly;
  a call refused where he stands no longer leaves the mount spawned for it at the scene's origin; the journal reopened
  at the end of a landing keeps the waiting call; the babies' follow switches are on in the test before it checks they
  are cleared. Review pass 6: that removal only with the option on or mounts left (off is the old way exactly).
  Review pass 7: the ride and cart keys leave his cart standing off a left mount by him before it steps aside (it
  never stepped with a cart on, and his mount came into it). Review pass 8: behind comes last in the step's order
  (with ahead taken by a mount left there earlier, the next stepped where the cart brought for Drive stands); the
  send-away key calls off a call waiting for a landing. Passes 9 to 11 clean (no must-fix from either reader). Review
  pass 12: a third party's skin of a pack's mount, the pack away, was counted as the donkey (its def falls back to it),
  so a load changed his mount into a donkey instead of leaving it; the mount its id names is kept now. Pass 13
  clean, its note taken after it (a short mount part from before the prefixes, "crab/x", is the registered mount's).
  Passes 14 to 18 clean: five in a row on the final code (docs/leave-mounts.md section 6; the last three passes'
  nits, below the bar and not fixed, in its section 7). The first F11 (13:06): 1946 passed, 2 failed, both in "another
  mount chosen while riding". The test mount's distance was read in the frame the donkey came, before its step was
  drawn (0.00; the check now reads it two frames on, and logs both). After Drive the cart, the donkey left wasn't
  stepped off the cart's spot (0.42 from the cart's body), and no step was even tried. The companion now logs why a
  mount doesn't step aside, and the test logs where the donkey and the keeper are before the cart switch; the cause
  is to be measured in the next run. The second run (13:53): the distance passes. The cart switch came in the frame
  of the hitch (the donkey's view not ready yet), and this time the step was tried and found no free spot: the run's
  spot is 0.24 from an obstacle, and the dismount's safety had put the donkey onto the keeper's spot, the cart's.
  Two checks after it failed because of that (the keeper's seat for the cart there was inside the obstacle). "Found no
  free spot" now lists each spot tried and what ruled it out. The third run (14:12): every spot was ruled out by a
  `NavMeshCutSphereCustom` round the keeper, one of the donkey prefab's navmesh cuts, always at the same offset from
  a mount's own spot: the view swapped out for the cart. `ReleaseView` turns a view's cuts back on for the game's
  pool before `Destroy`, which waits for the frame's end. That is older than this feature (every hitch, unhitch and
  removal); the self-test listed the colliders round the keeper at five moments of the cart switch, with whose they
  are. The fourth run (14:36): at the start none of a donkey view; in the cart switch's frame six (an `Obstacle`
  capsule and two navmesh cuts each of the view the hitch let go and of the one the unhitch let go, on the keeper's
  spot); none two frames on. The refused hitch was the same in one call: the cart taken off the test mount let its
  view go, and the hitch right after saw it (not one lingering, as first read). Reviewed again before the fix: every
  navmesh cut the logs name sits at a view let go in that frame (the prefab's two at its root +0.40 and -0.45);
  `Blocks()` leaves out views in use only (`IsModView`); every view let go passes through `DestroyDonkey`.
  **Fixed:** `DestroyDonkey` switches the view off right after `ReleaseView`, as `ViewPool.Park` already did (the
  parts keep the prefab's state; idle views are destroyed switched off already). The test checks that no view let go
  stands round the keeper in the cart switch's frame; the probes and the "stepped a moment ago" line came out;
  docs/pooled-parts.md has the row. `LeaveMountsTests` is listed with the dev classes in game-code.txt (its check
  reads the game's `Wgo`). The whole F11 on the fix (16:06): 1949 passed, 0 failed, 0 warnings; no `[pool]` leftover.
  **Off, no mount stays left** (the user, 2026-11-05 afternoon: three mounts out, the option turned off, they stayed; "switching
  the option from the options should unsummon the other mounts you're not using right now"; his two choices taken: one
  ridden by hand goes once he is off it, and a save loaded with the option off sends them home too). The companion's
  tick (four times a second, held in F11 runs and behind the Mounts panel) sends the mounts left home as
  `SendLeftAway` does while the option is off; one ridden is left till he is off it. The off scenario checks each way
  (switched off, ridden by hand, a load). The row's description: "Pick it again in the journal" (it said "Call it
  again", which read as the ride key) and "Turn this off and the mounts you left go home", in all 11 languages, each
  with its own file's words (the journal's name; ja, ko and zh_cn only with characters our files already have).
  The whole F11 on it (16:54): 1957 passed, 0 failed, 0 warnings.
- **A review of the words since 2026-11-01** (2026-11-05: two fresh reviewers, the colour choices, the parts' and
  the skins' part names in ten languages, against the game's own texts; ru, tr and fr clean). Fixed: pt-br colour
  choices masculine (a part's row has no feminine noun to agree with), ja `{0}の色（カスタム）`, ko Outline 외곽선 (테두리
  was also Royal Parade's trim), the snail's shell de Schneckenhaus and ja カラ, Royal Parade's trim pt-br Galão e
  franjas and zh_cn 饰边和流苏. Every new character is in the game's own text of its language. Left as they are: the
  optional style points (Gemas for jewels, katakana for the dragon's parts and the like).
- **The released fixtures' per-skin looks set off their defaults** (2026-11-05; docs/compatibility.md 4).
  `tools/fixture-flip.py` never flipped a key with a `/` (a per-skin key holds the skin id), so the 0.13.0 and 0.15.0
  Saddle Up fixtures' 46 and 69 per-skin rows stayed at their defaults and the upgrade scenario couldn't see a lost
  per-skin colour. The pattern takes `/`; run again over a fixture it keeps the header; both fixtures flipped (only
  those rows changed).
- **The review page against the export of every skin** (2026-11-05; the first export of all nine: every skin 0 px
  differ, docs/sprite-tools.md). `skin_preview_html.py` takes the frames the game built whole from the export's
  manifest (its guess took Armored's overlays, covering most of the donkey, for whole frames: 10525 px "differ", the
  donkey's legs, ears and tail under them), and counts nothing for a skin with no pictures of its own (Gerry's game
  frames: 76824 px were the whole export against nothing). A study card installed in the game takes the installed
  copy's id, so it is compared with the game's export of it (the Jester's card had none; compared, it showed its study
  folder a day older than the installed copy: re-staged).
- **The dev panel's footer under the settings tabs only** (2026-11-05; the user: "why is that on the tests tab"): the
  differing settings, the session values, "Make these the mod defaults", "Reset this style to the mod defaults" and the
  right-click hint are left off Tests, Performance, Instances and Mounts (`TuningWindow.HasSettingsRows`).
- **Every skin exported at once** (2026-11-05; the user: "that button only appears per mount so i cant batch and we
  need something that will export all of them"; docs/sprite-tools.md). `Looks.ExportEverySkin`: every registered skin
  held and built (as for a wearer), written with `ExportBuilt`, let go; a skin set aside, one whose base frames aren't
  in memory and a game-source one before the game is loaded left out and said. The Mounts tab's button is "Export
  every skin as built"; every F11 run writes the export too (Measurements: "measure: every skin of every mount exported
  as built").
- **The Outline region** (2026-11-04; the user: "set its color by itself as part of an individual option", "lets keep
  for donkey for now and game default being game's border"; docs/mount-colours.md 7.15). `Colors.cs` `Outline`
  (1e190e 301f0f 42422c; the donkey's pairs 43), `MountRegionKeys` six, `UiWords.RegionOutline` in 11 languages (the
  Legs, mane and ears note without "with its outline"), `SkinRegions.DonkeyWord`, `MountLook.Outline`,
  `MountCompanion.ApplyLook` (`OutlineKeys`: the donkey, the paper, the babies the parent's), `ToneRows` after Dark
  and Eye. `SkinRegions.Read` takes `colours =` through `Looks.AddRegionColours`. Hellfire `[Region.Outline]`
  `colours = 1E0E0C`, Armored `colours = 281B26` (`make_hellfire.py`, `make_armored.py`; the pictures reproduced).
  SkinsScan (six regions, the `[Region.*]` colours counted with the named ones), `check_skin.py`, `face_pixels.py`,
  `part_map.py`, `skin_preview_html.py`; `tests/ApiSurface` updated. Tests: `OutlineAsDrawn` (the Outline and Coat
  rows), `MuzzleNoseOnly`, the regions scenario.
- **row = none** (2026-11-04; docs/mount-colours.md 7.13): `SkinRegions.Read` (`row`), `SkinDef.RegionsWithoutRow`,
  `SkinRegions.HasRow` (the nearest of the look and its defaults decides; a name brings a row back), no `NameTerm` and
  no `Declared` name for such a region; `RegionRows` hides its rows; `Colors.Rig`: no map in `NamedMaps`, only the
  skin's `[Base.<Region>]` on the donkey's regions under it. `MountSkinDef.RegionNames` / `RegionsWithoutRow`
  (`tests/ApiSurface` updated). SkinsScan and `check_skin.py` take `row = none` (refused with a name); `check_skin.py`
  warns when a skin over the donkey covers a region of the donkey's in every frame. Hellfire `[Region.Eye]`
  `row = none` (`make_hellfire.py`). Tests: the regions scenario (a third temp skin) and the integration check
  "Hellfire gives the donkey's eye no row".
- **The eye's line and the nostril repainted** (2026-11-04; the user: "it looks sloppy otherwise", "yes nostril
  should be the same as muzzle"; docs/face-pixels.md, docs/mount-colours.md 7.14). They were the dark fill's own
  colours (292316, 352e1e), so "Legs and mane" reached them. `FacePixels.cs`: the pixels of `face-pixels.txt`
  (embedded; made by `tools/skins/donkey/face_pixels.py` from `rig.json` and the asset probe's export, 178 on 44
  frames) copied into the game's character atlas on the GPU after the donkey-art check, a frame a tick, read back;
  repainted again when the frames come back on new pages. `Looks.ReadFrame(..., FacePixels.Read)`: the builds read
  them repainted, `DonkeyFrames` and the export as drawn. `Looks.ForgetGameFrames`: the found frames looked for again
  when their sprites are gone (`DonkeyFrames` uses it too: it retried on the unloaded sprites until then). `Colors.cs`
  Muzzle + 362d1e (the donkey's pairs 41), 292319 in no region. The asset probe's export puts the two colours back.
  `face_pixels.py --check` in the release scans. The probe (`Dev/AtlasPatchProbe.cs`; its findings in
  docs/face-pixels.md section 6) replaced by `Dev/FacePixelsTests.cs` (the Looks group and after the run) and the
  integration check "colours: the donkey's eye and nostril repainted".
- **Every after-the-run scenario runs** (2026-11-04): `SelfTest.Run` took the group's first scenario alone (a run with
  a second one in the group showed "158 of 159"); now every one in order, the world's check last.
- **The donkey's Muzzle its nose; a picked colour over a skin's pre-colour** (2026-11-04; the user on Hellfire's
  Cream coat and its green dots; docs/mount-colours.md 7.12). `Colors.cs`: Muzzle "abb298 858f6c 75755a a9a7a2" (the
  nose, the mouth chewing and grazing), the face's dots 858e6b and the belly's line 42422c under Coat (858e6b and the
  nose's 858f6c two channels a step apart, outside the exact swap's 0.004); `UiWords.RegionDonkeyMuzzle` "Muzzle" in
  11 languages. A skin's `[Base.<Region>]` is skipped where the player's map colorizes (`!(on && map.HasColorize)`):
  a shade (brightness, saturation) and a baby's step stay on the skin's look, a colour replaces it. Integration
  scenarios "colours: the donkey's Muzzle row reaches its nose only ..." and "colours: a colour picked for a part a
  skin pre-colours replaces the pre-colour ..."; docs/skins.md "Recolouring the donkey under the skin".
- **The outline is the drawing's line** (2026-11-04; the user: "I dont like that the mane is that partial outline";
  docs/mount-colours.md 7.11): measured on the game's donkey frames, its Dark's 1e190e and 301f0f are the outline (on
  the silhouette's edge, never in the fill) and are in no region now (`Colors.cs`: Dark "Donkey mane, legs, ears and
  hooves", the donkey's pairs 40; both in colour-cube cells of their own). Hellfire's horns' outline 1E0E0C out of its
  `[Colors] Dark` (`make_hellfire.py`, the pack reproduced). The dragon: its Dark its outline only, by hex in
  `colors.json` (no `[Region.Dark]`, no row), its horns and spikes `[Tone.horns]` "Horns and spikes" (the 11
  translations moved from `region.dark` to `tone.horns`), its body's deepest shade and its wings' ribs its Coat.
  A baby shades the parent's Dark only where its look names one (`MountCompanion.ApplyLook`: an unnamed Dark is the
  outline, which its shade reached on every baby until then).
  Integration scenario "colours: the outline is the drawing's line: no colour row reaches it"; docs/skins.md and
  docs/making-a-mount.md (its example names its parts) say to keep line art out of every row. The `NamedMaps` comment
  in `Colors.cs` no longer describes the follow pairing removed the same day.
- **A probe of the game's Halloween head swap** (2026-11-04; docs/game-update-26404424.md "Halloween"): the
  scenario "measure: the game's Halloween head swap on the zombie pictures" (`Dev/HalloweenProbe.cs`, the
  Measurements group, sorted `dev` in game-code.txt) lists the zombie pictures the keeper's and a ground drop's atoms
  hold and what copies of them draw for heads 1050 and 1051, the swap forced off and on by a prefix on
  `HalloweenHeadSkinOverride.Resolve` for the scenario only. The table (read from the game's asset) swaps zombie head
  1051 for 1060; the ground drops' atom (`BigItem.prefab`) carries the 1060 pictures, so the game draws it, and only
  the cart's name check (against the unswapped id) warned and drew the icon instead. Fixed in `Cargo.cs`: any lying
  zombie the game switched on in the view is right (not by the game's event class: `Cargo` is core in the dependency
  gate). `Dev/HalloweenSwap.cs` (the forced swap, shared) and the scenario "cargo: a zombie with the game's Halloween
  head on the cart" (`Dev/ZombieHeadTests.cs`, a test zombie with head 1051 on a test cart, the swap off and on;
  `Cargo.Rig.TestForgetViews` for it).
- **Tones, the checks and tools** (2026-11-03): `tests/SkinsScan` and `tools/skins/check_skin.py` read `[Tone.*]`
  (the id, the keys, the colours in the pictures, one place per colour, not a donkey colour under an over skin) and
  fail a tone colour that shares a colour-cube cell with another colour of the look (`Colors.Rig.LutIndex`; both
  proven on a probe pixel one step from the donkey's coat). `tools/skins/canonical_cfg.py` keeps `[Tone.*]` among the
  colour sections. The Jester (`tools/skins/jester/`, no pack) has its motley's two colours as tones, "First motley
  colour" and "Second motley colour", in place of `[Colors]` Harness and Muzzle; the Harness row reaches the
  donkey's strap only. The API's 13 new words in all 11 `Languages/` files (18 with the follow words of the fourth
  review).
- **`tools/page-carryover.py` reads the shipped skins from their folders** (2026-11-03): its hand list lacked Armored,
  so the 0.15.0 fixture's 23 Armored values read as lost and the tool exited 1; it also knows `ToneRows`. All five
  fixtures land every value.
- **SkinsScan's pack checks count** (2026-11-03): they printed "FAIL" without counting, so the packs' `Languages\`
  folders (2026-11-01) showed three FAIL lines under an ALL PASS; `Languages\` is a pack's, and a pack failure fails
  the scan.
- **Tones, after a code review** (2026-11-04; 10 findings, each checked against the code): a variant's tone of its
  default's id without a name of its own keeps the default's name and translation (it was labelled by its id), and
  one with only a `name` renames it (it was dropped for having no colours); a skin's tones go with their owner's
  overrides (`Mounts.ClearTuning`, `InstanceTuning.Reset`), so a skin put on that instance later by another mod
  doesn't find the old colours; a baby not in its parent's skin (a plain donkey) takes no tones; a variant that
  takes palette colours from its default says once that its tones are ignored; SkinsScan uses the donkey's colours
  only under a skin over the donkey (not over another mount's default), and a variant's default's colours keep the
  default's places there and in `check_skin.py` (a shade beside the same tone's was read as a clash); one hex reader
  and one lightness rule for the regions and the tones (`MountCompanion.Hex`, `LightnessAgainst`: a bad custom tone
  colour is said once, as a region's is); the tone lightness reads the skin's set without sorting a copy per apply.
  Not changed: a variant whose default registers after the page is shown gets its inherited tones' rows at the next
  start, as every late registration (docs/mount-colours.md 7.4).
- **Tones, after a second review** (2026-11-04, over the commit 3b1dcde; 9 findings, each checked): a code skin's tone
  without a name is unnamed as a pack's is (`MountSkinTone` named every tone by its id, so a code variant lost its
  default's name); another owner's tones are neither replaced nor cleared by a companion (said once), as an override
  is; tones are kept with the skin they were made for and colour only that skin, so a skin changing under them
  takes none of another's; the tone lightness averages the colours the loaded skin draws (a listed colour no
  picture has no longer counts); a palette skin's tones aren't read, so a colour they share with its ignored
  `[Colors]` no longer refuses the skin; SkinsScan and `check_skin.py` read a colour list given on several lines as
  the loader does (they kept the last line, so a colour on an earlier one passed both and the game refused the
  skin; `[Colors]` had the same gap); SkinsScan checks a variant's colours against its default's tones with no tones
  of its own; `ToneRows` finds its three rows by their keys instead of their place in the list. Not changed: the rows
  follow a skin re-resolved after the page (a late default, the dev reload) at the next start (docs/mount-colours.md
  7.4). Each change has its self-test check, and both checkers were proven on a split colour list.
- **Tones and the custom colours, after a third review** (2026-11-04, xhigh, over 3b1dcde and the working tree; 12
  findings, each checked): **the custom colours' lightness on a skin in its own colours, and the tones', anchored to
  the wrong art after a sheet was built** (an old gap the tones made visible): the companion re-applies the look on
  `LoadedReady`, but a sheet is worn from the next frame, so `Looks.LoadedOwnColours` (which waited for "worn")
  returned nothing in that frame and the custom colours stayed anchored to the donkey's art, the tones to their
  lists, until the next change; it asks for the colours sorted now (`Loaded.ColoursSorted`), the frame the event
  fires in (self-test check at the event). The re-apply also runs for the Mounts panel picture's skin. A tone row's
  English label carries its id, so a pack's tone named like the page's picture row no longer makes the page refuse
  its rows. A default's tone name that only its variants show is in the default's English file. `MountSkinInfo.Tones`
  is made when asked for (a `MountSkinInfo` is made at every `Find`, the rows' checks among them, every frame the
  colours window is open). One `HasColours` and one `Colors.AverageValue` for the regions and the tones.
  `tools/page-carryover.py` works out each shipped skin's tones as the loader does (a variant's merged with its
  default's, none left with no colours, none on a palette skin). SkinsScan reads a tone's id in lower case, as the
  loader does (`[Tone.Cloth]` failed). `check_skin.py` checks a variant's colours against the frames it takes from its
  default (proven on a scratch variant of the dragon: the committed checker said all good, this one names the
  clash). Not changed: a variant taking palette colours from its default without saying so, with a colour under
  both its `[Colors]` and a tone, is refused: one colour named twice is the author's mistake, and the message
  says so; tones keep their own small store on the instance (with the owner and skin rules) rather than tuning
  keys, which must be known settings (`InstanceTuning.Find`).
- **Tones, after a fourth review, and `follows`** (2026-11-04, xhigh; 12 findings, each checked). A preset's
  lightness stops where the tone's lightest shade reaches white (Gold, White, Pink and Red flattened the Jester's
  lit shades into one); the regions' custom colours keep their rule (players' saved looks). A tone nobody set and that
  follows nothing isn't paired on a skin over the donkey, so giving a skin tones doesn't move it to the colour cube
  (Royal Parade's crown would have made it 49). `MountSkinInfo.Tones` is made once per resolved skin and shared (it
  held the skin's internal record before). A colour list of separators only names nothing, and adds no region. A code
  skin's bad `Regions` reached the caller of `Mounts.Looks.Register` as an `ArgumentException` (taken back in the
  sixth review: it is the `FormatException` of 0.43 again).
  `MountSkinTone` keeps its id in lower case, as the loader does. `check_skin.py` fails a tone colour that is the
  donkey's own (SkinsScan did); SkinsScan checks a sheet skin's tones (they were skipped) and a variant's tones on its
  default's palette colours. The self-test's path check takes the player's `SpriteMethod`. The English dump leaves
  out a tone name only a palette variant would show. **`follows`** (the user's go the same day): a tone's first choice
  can follow a region (`follows = Harness`, `MountSkinTone.Follows`), coloured by that region's own map, so a part
  converted from a region keeps every player's saved look; its colours may stay under the region in `[Colors]` for an
  older API (the loader moves them); the region's custom colour lightness counts the following tones' colours; the
  region is in `MountSkinInfo.Regions`. 5 new API words in all 11 languages (the "As the …" choices). The three
  released skins converted (Donkey Skin Pack: 8 part names in all 11 languages; `make_hellfire.py`, `make_armored.py`
  write the same cfgs, checked equal). Self-test checks for each; SkinsScan and `check_skin.py` read `follows` and
  the compatible listing; `check_skin.py` counts only the colours paired by default.
- **Tones, after a fifth review** (2026-11-04, xhigh, with players' saved looks as a focus; 11 findings, each
  checked): the exact swap holds 64 colours (`Colors.PaletteMaxColours`, 48 before): a player giving Royal Parade's
  crown or Armored's steel a colour moved the skin to the cube, where two pairs of the donkey's own colours share a
  cell and took the harness colour; the shader's loop has no cap (docs/colors.md), a pixel reads up to 64 palette
  texels instead of 48 (a perf look in the next in-game run). A variant's tones are new records at every resolve:
  its own `follows` counts when it gives no name, and nothing of an earlier resolve stays on its declared tones
  (`NameFromParent` says which skin translates a row's name; `SkinTones.NameShown` for the English dump). A
  following tone with no colours adds no region. `MountSkinTone.Follows` comes with a factory (`MountSkinTone.Following`) and is read only
  (the listed tones are shared). The checkers join only colour lists (a tone's `follows` and `name` keep their last
  line, as the loader). `Looks.Loaded.ColoursReachedBy` is the colours of a region with the tones that follow it
  (the region's anchor). Documented: a variant listing a following tone's shades under the region says `follows`
  itself; Saddle Up's `NeedsApi` goes to the API with tones at release (docs/release-checklist.md). Not changed: an
  empty `Harness =` names no harness now and greys a row that coloured nothing (what docs/mount-colours.md 2.2 said
  it meant); `ValuesOf` per apply costs a few microseconds and runs on changes, not per frame.
- **Tones, after a sixth review** (2026-11-04, xhigh; 14 findings, none on the main path, each checked; with all
  first choices the three converted skins colour every part as before, confirmed). The 64 limit is a skin over the
  donkey's only (`PaletteMaxColoursOver`): a skin in its own colours keeps 48, so one of 49 to 64 colours (a third
  party's, Gerry's read from the game) keeps the path and the look it had. Backwards compatibility over earlier
  rounds' tidiness: `Register` throws the `FormatException` of 0.43 for a bad `Regions` entry again (not an
  `ArgumentException`), and a `[Colors]` line naming no colour makes its region again, with 0.43's error texts. A
  tone the loader can't take (a colour named twice, a bad id, a bad `follows`) is left out and said in the skin's
  report, the skin loads (a pack's typo in the new section never costs a player the skin; it also ends the
  palette-variant refusal of the third and sixth reviews). `MountSkinTone.Following(region, id, name, colours)` in
  place of a constructor overload a region string could slip into as a colour. Custom without a usable colour is the
  tone as drawn, not its region. The watch self-test waits for the sheet worn (`LoadedOwnColours` is a frame
  earlier since the third review). One averaging loop for the regions and the tones. Not changed: Saddle Up's
  `NeedsApi` goes up with the version bump (the release checklist); a variant with pictures of its own gets rows
  for its default's tones that may colour nothing (its sheet isn't known when rows are made, and rows are bound
  once); the palette path's cost for a herd in Hellfire is for the in-game perf run; tones keep their own small
  store and the "(tone id)" English label (both explained in docs/mount-colours.md 7.3, 7.4).
- **Tones, after a seventh review** (2026-11-04, xhigh; 9 findings, each checked; nothing in it changes a player's
  saved look). The in-game integration test of Royal Parade's colours looked for its reds under `[Colors] Harness`,
  where the conversion took them from: it reads the cloth tone now (follows Harness, its four reds), and a harness
  colour still has to change all four. Saddle Up asks the API for the tone rows through a small wrapper that catches
  `MissingMethodException`: with a GK2 Donkey API from before tones its colours window keeps every other row and the
  log says once why the parts' rows are missing (until the version bump raises `NeedsApi`, the release checklist).
  A Custom tone colour's lightness is no longer capped at the tone's lightest shade reaching white (the presets
  still are): capped, its lightness row did nothing past that point on a light tone. `MountSkinTone` keeps a copy of
  the colours it is given, so a mod reusing its array can't change a registered tone. The tone reader takes its
  colours with the `[Colors]` reader's own helpers (`Looks.ColourTokens`, `IsHexColour`). `check_skin.py` holds a
  skin over its default (another mount's) to 48 colours, as the game does (64 is a skin over the donkey's only).
  SkinsScan's colour-path line counts a skin over the donkey by its pairs (the donkey's colours, its `[Colors]` and
  following tones' always, the others while set) against 64, the rest by the pictures' colours against 48; the
  three docs that still said a colour named twice refuses the skin now say the tone is left out. The palette path's
  cost for 49 to 64 pairs stays for the in-game perf run.
- **Tones, after an eighth review** (2026-11-04, xhigh; 11 findings, none on the main path, each checked; with all
  first choices the converted skins colour every part as before). The exact swap gives a texel the replacement of a
  source colour within 0.004 of it, and one step in one channel is 0.0039: SkinsScan and `check_skin.py` now fail a
  tone colour one step from a colour of another place (the cube check alone missed such a pair in two cells; the
  shipped skins have none, and `check_skin.py` shows other places' pairs for the eye). Both checkers read a variant as
  the game builds it: `check_skin.py` gives its default's compatible listing to the tone (a false cube clash before),
  SkinsScan reads the default's tones after its `[Colors]` as the loader does, and both take only the default's
  pictures the variant doesn't draw itself (SkinsScan took them all, so a stale tone colour passed). `MountLook` finds
  a tone by its id as `Tone` keeps it (lower case). One table holds each preset's word and colour
  (`SkinTones.Preset`; the choices are built in `ToneColor`'s order, and a self-test checks it) in place of two lists
  kept in step by hand. Setting a tone value it already has no longer applies the look again (the rows set theirs
  about four times a bind). A tone's colours go into the skin as checked (`Looks.RegionColours`), not through the
  `[Colors]` reader a second time, which could throw. The self-test bounds the over skin's colours by 64, its path's
  limit. The release checklist says the `ToneRows` wrapper goes with the `NeedsApi` bump. Not changed: SkinsScan's
  `follows` check already ignores case (its region list is a case-blind set); a tone given a colour is that colour on
  every baby, as the harness is (on its first choice a following tone takes each baby's shade of its region): whether
  it should take the baby's shade is a look question for the user (docs/mount-colours.md 7.3).
- **Tones, after a ninth review** (2026-11-04, xhigh; 8 findings, all low, each checked; nothing in it changes a
  player's saved look). `MountSkinTone`'s colours are a list (`IEnumerable<string>`, the constructor and
  `Following`): with a params list, a call that left out the name took the first colour for it and compiled; it
  doesn't compile now (unreleased API, so no overload kept). The tones `MountSkinInfo` lists say whether the skin
  gave the name (`MountSkinTone.Listed`): one fed back into a `MountSkinDef` keeps its meaning (an unnamed or
  inherited name stays the default's). A custom tone colour applies the look only while the tone is on Custom (each
  row's bind set it once more for every tone), and a palette skin's tones give no values (`ValuesOf`, as `InfosOf`
  and the rows). `canonical_cfg.py` keeps a section given twice as one with both blocks' lines, as the loader reads
  it (a `[Tone.<id>]` in two blocks lost its first). `check_skin.py` and SkinsScan give a variant's unnamed colours
  of its default the default's Coat, as the game does (the one-step check took them for two places). The mod
  authors' entry above and docs/mount-colours.md 7.8 say what the tones do now (left out, not refused; `needs =`
  only where wanted; the tones' own place check; 18 words).
- **Tones, after a tenth review** (2026-11-04, xhigh; 9 findings, low to medium, each checked; the main paths held).
  `follows = Coat` on a skin over the donkey keeps no earlier look (a colour named nowhere there was drawn as it is,
  never Coat): docs/skins.md says so, `check_skin.py` warns and SkinsScan says it in an INFO line (a part meant to
  follow the coat is a fair design, so the loader takes it). A tone's rows are greyed once the skin, loaded, draws
  none of its colours (`SkinTones.Draws`; a variant redrawing every frame over its default's tone had live
  dropdowns that coloured nothing), live until it is loaded. `check_skin.py` takes a variant's default's front
  pictures too, as the game does (SkinsScan already did); proven on a probe. Section 7.8 of docs/mount-colours.md
  lost a duplicated line and got its blank line back (a slip of the ninth round's edit). Not changed:
  `LoadedOwnColours` meaning colours sorted (every caller checked: each wants the colours, or reads after the skin is
  worn); the cube's two cells of the donkey's own colours (a player on `SpriteMethod = Lut`, or a skin over 64: the
  outline and coat pixels there take the harness colour; older than tones, in 7.3, open); the 64-colour palette's
  cost (the in-game perf run); the four skin.cfg readers (each does its own job, SkinsScan's must be C#; the probes
  run through both checkers); `OwnFollows` on a resolved tone (what that skin said, as its comment defines).
- **Tones, after an eleventh review** (2026-11-04, xhigh; 12 findings, none a break in a saved look, each checked).
  Saddle Up's `NeedsApi` and `[BepInDependency]` minimum go from 0.40.0 to 0.41.0: it has called 0.41's looks per
  skin (`PerSkin`, `LookOf`, `SkinHasRegion`) since 0.13.0, so an API of 0.40 got through the gate to a missing
  method instead of the "too old" message (the bump to the API with tones stays the release's). A tone's choice row
  defaults to the look's value when `ToneRows` is called, as the API's other rows do (a mod's own default set first
  is kept). `SkinTones.Draws` takes a sheet built before the skin was resolved again as not known (rows live).
  `MountSkinTone.Following` throws `ArgumentException` for a region that is none of the five (the region and the id
  swapped compiled and the tone was only left out in the report) and takes the region in any case. One tone id rule
  (`SkinTones.NormalId`) for `MountSkinTone` and `MountLook.Tone`. The rows' greying through `EnableWhen`, not a copy
  of it. SkinsScan checks a `[Tone.<id>]` given twice once. The colour-path comment in `Colors.cs` names the over
  limit. Not changed: a followed region's rows stay live when the player cuts every part following it loose (they
  hold the colour "As the harness" goes back to); `ToneRows` finding its rows by key (the lightness row by the
  `LightnessKey` that `Color` names it with, and the self-test checks its label); the "(tone <id>)" in the English
  label (the window draws the name); the tones' list and maps in `NamedMaps` (a rebuild, after a colour change, already
  makes a map per region).
- **Tones, the first in-game run** (2026-11-04, the user's F11: 1829 passed, 2 failed, 0 warnings). The two failures
  were the Fonts check: the Japanese font lacks 摺 鍮 鞍 and the Chinese 鞍 褥, all in the Donkey Skin Pack's new part
  names. Each is reworded with characters the game's own text in that language draws (its strings tables) or the run
  found in the font: ja 草摺り → 草ずり (the same word, the kanji in kana), 真鍮 → 黄銅 (brass, the other word for it),
  鞍敷き → くら敷き; zh_cn 鞍褥 → 饰布 (decorative cloth). The tones scenario's test donkey spawns as the self-test's
  own: as a made-up mod's it drew the "not a loaded plugin's GUID (a typo?)" warning, the run's only one not announced
  by its test. `tools/l10n-glyphs.py` (new) lists the characters of our translations the game's own text never draws,
  to look at before the in-game Fonts check. The run again: 1831 passed, 0 failed, 0 warnings; every font has every
  character of our translations.
- **Babies' shades of the parts, the Jester's six parts, the colour swap's cost** (2026-11-04; the user's answers:
  the babies "ideally yes unless it's a performance cost we can't afford", the Jester "yes so that we start making
  use of our new multi colour tone system"). `SkinTones.ValuesOf` takes a baby's look and shades each set tone as
  `MountCompanion.Shade` shades a coat's colorize colour: its hue turned and saturation scaled (`FoalLooks.ShiftHex`),
  its brightness stepped (`FoalLooks.StepBrightness` and `IsAlike`, shared with it; `FoalLooks.For` draws nothing new,
  so every baby keeps its colours and temper); a tone is colorized whole, so its colour carries the hue and the
  saturation. A preset stays under its cap (`SkinTones.BrightnessCap`), Custom takes the whole step; `SkinTones.Same`
  compares the saturation and hue too.
  The Jester (still loose, in no pack): six tones, `cap-front`, `cap-back`, `collar`, `collar-points`, `caparison`,
  `caparison-second`, in place of `motley1` and `motley2`; the cap's and the collar's shades one to four steps off the
  caparison's, each in cube cells of their own (`tools/skins/part_palette.py`, new); `check_skin.py` passes. With
  every part set it is 71 pairs, so `Colors.PaletteMaxColoursOver` 64 -> 72 (and `check_skin.py`'s copy; SkinsScan
  reads it) until the measured cost says otherwise; `Colors.TestPaletteMaxOver` is the measurement's own. Two
  measurements (the Measurements group): every donkey skin's colours window as a picture, the dev panel hidden, with
  its part rows in words (GK2-panel-shots, GK2-colours-rows.tsv); and the colour swap's cost, 21 donkeys at their
  size and twice as big, frame times uncapped, as the game draws them, at the donkey's own pairs, 64, 72, 96 and on
  the cube (GK2-palette-cost.tsv). The tones scenario checks a baby's shade. The user's run: 1833 passed, 0 failed,
  0 warnings; every converted part's first choice reads "As the ..." and the Jester's six rows are live
  (GK2-colours-rows.tsv); no cost measurable from 42 to 96 pairs, nor on the cube, the frame not the GPU's on that
  machine (docs/performance.md "The colour swap's cost"). The colours window's pictures wait for the picture drawn
  since (Royal Parade's first was empty: settled came a few frames before it).
- **Named regions** (2026-11-04; the user: "ok lets begin" on docs/second-tone.md 8). `SkinRegions` (new) reads
  `[Region.<region>]` and finds a look's name for a region's part (its own, its default's, the donkey's under a skin
  over it: `UiWords.RegionCoat`, `RegionDonkeyDark`, `RegionDonkeyMuzzle`, `RegionEyes`, `RegionHarness`); `Texts`
  dumps the declared names. `MountSettingsPage.RegionRows` (labels by `Row.LabelOf`, hides by `Shown`/`Visible`, as
  `CartRow`; `Row.OwnLabelToo` keeps the row's own label in the page's English file, the words a look naming none of
  the region draws: the first F11, with every shipped look naming its Coat, had dropped Saddle Up's `Coat.label`,
  `CustomCoat.label` and `CustomCoatLightness.label` and warned on them in its ten translations), `RegionPartRows` in `ToneRows` for Dark and Eye; `SkinTones.RegionValues` colours a region as a part
  (`Colors.ArtRange`, the art's average and lightest); the companion applies `DarkKeys`/`EyeKeys` to the mount, the
  paper and the babies (the babies' dark shade starts from the parent's dark colour now). Following tones:
  `MountSettingsPage.CarryOver` (the file's snapshot `fileSections`, `InFile`, the region's keys RegionRows was
  given, `NearestPart`); `SkinTones.Choices()` without the follow word, the five `ToneFollow*` words out (and their
  translations); `Colors.Rig.NamedMaps`, `Loaded.ColoursReachedBy` and `MountSkinInfo.Regions` no longer count a
  following tone with its region. Saddle Up calls `RegionRows` for its three rows (inside the `ToneRows` guard). Data:
  the mounts' names in their cfgs; the snail's eyes and the worm's eyes and mouth their own regions
  (`tools/skins/creatures/colors.json`, keyed by the mounts' current ids now, and a `hex` rule in
  `skin_colors.py`; the worm's mouth drawn 42121A, a cube cell apart from its outline: `make_creatures.py`); Hellfire's and the Jester's Dark named "Legs and mane". Every name in 11 languages, the CJK ones
  checked with `l10n-glyphs.py` (11 rewordings to characters the game's own text draws). Checkers: `canonical_cfg.py`,
  `check_skin.py`, SkinsScan take `[Region.*]` (one of the five, `name =` only) and no longer count a following tone
  as paired; on a look on the colour cube whatever the player sets (over the exact swap's limit) both fail two
  regions' colours in one cube cell, not only a tone's (the worm's mouth shared its outline's; proven on that art). Tests: the scenario "colours: a look's names for its parts" (`Dev/RegionTests.cs`: three pages on one
  file prove the carry-over once), the tones scenario and the Royal Parade integration check for the new rules; the
  colours-window measurement over every skin of every mount, every colour row as drawn. `tools/skins/part_map.py`:
  the donkey under an over skin only, a look's unnamed colours as its Coat.
- **Every donkey skin's parts their own** (2026-11-04; the user: "now lets make sure that all the donkey skins are
  using the new multi tone system where every part is named and is able to be coloured"; docs/mount-colours.md
  7.10). `tools/skins/part_map.py` (new) paints each part's pixels one colour and the ones no row reaches white: it
  found Royal Parade's jewels without a row and five rows covering two parts each. Royal Parade: Crown, Trim and
  fringe, Emblem, Jewels beside the Saddle cloth; Armored: Helmet, Plume, Body armour, Skirt, Brass (`steel` is
  `armour` and `helmet`); Hellfire: Horns, Glowing eyes, Lava cracks, Tail, Tail tip (its outline under Dark alone);
  the Jester: Cap band and Bells. Parts that shared a ramp got shades of their own from `part_palette.py` in the
  generators (`make_royal.py`, `make_armored.py`'s `HELM_PAL` and `PLUME_PAL` and the helmet's mouth fill,
  `draw_jester.py`; Hellfire's parts had colours of their own, its pictures unchanged). Each generator was first
  proven to reproduce the pack (Royal Parade but one pinhole pixel `stamp.py` fills since) and each change proven a
  recolour only: every new picture, its colours mapped back, the old one pixel for pixel. A split part of one that
  followed a region follows it too, listed under it for an older API. With every part set: Royal Parade 56,
  Hellfire 55, Armored 70, the Jester 78 pairs, so `Colors.PaletteMaxColoursOver` 72 -> 96 (and `check_skin.py`).
  The pack's new names in all 11 languages (a split part keeps its half of the reviewed translation), checked with
  `l10n-glyphs.py`. The user's run: 1833 passed, 0 failed, 0 warnings; the 23 part rows as intended in
  GK2-colours-rows.tsv (each split part of a following one "As the ..." too); every font has every character; the
  dump's `en.csv` is the hand-written one, line for line.

- **`tools/skins/donkey_frames.py`** (2026-11-03): `make` writes `src/GK2DonkeyAPI/donkey-frames.txt` (fingerprints,
  no art) from the asset probe's PNGs; `check` compares it with them, or with a dev build's own read of the game
  (`BepInEx\GK2-donkey-frames\`, its PNGs and list), pixel by pixel where a frame differs. `tools/game-asset-diff.py`
  gained a line on the donkey's art (its 0301_ sprites, the character atlases). `tests/CecilScan-DonkeyAPI` compares
  the manifest over every scope it names, `tools/check-dist.py` also refuses a pathfinding reference in a DLL without
  a manifest, and the QOL repo's copies of the shared check are byte-identical again.
- **`tools/game-asset-diff.py`** (2026-11-03): the art and data half of a game-update audit, beside
  `tests/GameDiff`: every asset file the update changed (the old ones read from the game folder's git), compared
  object by object; the game's version before and after (what a new save is stamped with); and the balance changes
  that are not just regenerated quest-node ids. `docs/release-checklist.md` names it with GameDiff. And
  `tests/GameDiff` (both repos' copies) now compares every game assembly whose bytes changed, not only
  Assembly-CSharp and LazyBearTechnology: the API uses the A* pathfinding too, which this update recompiled.
- **MSB3277 marked as the game's own in `GK2DonkeyHerd`, `GK2DonkeyTechGate` and `GK2DonkeyProbe`** (2026-11-03),
  as the API and the other tools already do: the three warned on every build.
- **Armored redrawn, in review** (2026-11-01; the user held the Donkey Skin Pack's 1.1.0 back: "a lot more detail and
  exquisiteness ... professional while matching the game's aesthetic"). `tools/skins/armored/make_armored.py`
  rewritten: the steel and brass are the game's own guards' (sampled from their helmets and breastplates: a warm grey
  with a purple lean, shaded round), always steel (the user's choice; no longer the Second tone); the plates are
  shaped and lit by one light from the front and above, then given rolled rims, rivets and brass trims; a face
  plate with an eye guard, a brass boss on the brow and a short plume (hand-drawn, chosen from variants on the
  standing head), a breast plate (with the shoulders), a rump plate (with the hip) and between them five steel
  lames (the user's pick over a padded caparison, both shown on the page), over a skirt with a dagged hem; the tail
  stays clear of the body's armour (it runs down the rump's back edge) and wears its own guard of steel rings over
  its upper half, the tuft free (the user's choice after a ribbon bow and a steel ball on the tip were tried and
  dropped), fitted to every frame's own tail; the hip every hip-hung piece follows is measured by matching the
  standing frame's hindquarters on the frame (anatomy.body()'s hip read the turned head's far ear as the back's
  top in turn 02-03, 2-4 columns forward of a rump that had not moved, and was a column off where the tail swings
  in some walk, graze and lips frames; Royal Parade's cloth hangs from that measure too); the plume
  on the brow behind the boss; the breast plate's top runs down into its front (a square corner there did not look
  real); skirt and plume in the harness colour. Every rigid piece is drawn once
  on the standing frame and moved with its part of the body
  (no shimmer). The breast collar is the seam in front of the middle (cut along the strap and the chest's outline
  in every frame), so the strap stays in view in the harness colour. Found on the user's videos and marked
  screenshot of the page (2026-11-02): the donkey's body grows and shrinks 2-4 pixels in the walk (the shoulders
  move, the hip stays), and every steel middle that tried to take it as one plate looked wrong (a plate edge
  sliding over another, "bugging"; a plate growing at its front or sliding under the rump plate, "stretched
  out"); the caparison stretches as cloth does, the lames each shift a pixel at most. On the head anything that
  stands out read as a third, armoured ear: three neck lames on the crest, and the forehead spike pointing up or
  straight ahead; the neck plates and the spike the user chose were dropped, the brow has a brass boss.
  The frames that rotate the head get the face plate turned onto the frame's ear-to-muzzle line (graze 03-05),
  moved by the eye (turn 02) or their own drawings (turn 03-06), and the plates stay behind a head or neck that
  comes down in front of them.
  New: `tools/skins/armored/preview.py` and `Preview Armored.cmd`, the review loop (out\ only, the skins preview page
  with the cut as "Armored (in progress)" beside the shipped one); `tools/skins/game_sprites.py`, the game's
  character sprites as style references (presets, export, palette; it refuses to write into the repo);
  `handover.py --pack <folder>` (a cut not yet in the pack) and `--ref` repeatable, a game sprite at the rows' 3x.
  Written into the pack (pictures, skin.cfg, the pack's changelog entry) and deployed 2026-11-02; the user looked at
  it in game the same day. The pack's README, docs/skins.md, the inventory, the looks design, the media texts, the
  1.1.0 release notes, the zip and the Workshop folder still describe or hold the first cut until the user approves.
- **Armored: an armet instead of the face plate, in review** (2026-11-02; the user, after the in-game look: the face
  plate was "lacklustre", "fill the donkey's face more ... like a knight armet type helmet but more ornate", with
  armet references). `make_armored.py`: the chanfron, its rivets and the brow's boss replaced by a closed helmet in
  the same steel: a skull over the forehead and the poll (the ear comes out behind it), a gilt brow, the sight
  over the eye (the eye's white shows through it, and the lips frames' blink with it), a beaked visor over the nose
  with breaths and a brass rosette where it pivots on the cheek, a roped gilt rim along the jaw. The skull lies flush
  with the head's own top line (a first round dome standing above it and upright at the front was "that bulge near
  the brain area", the user's marked screenshot), the plume's holder on its top edge, a gilt comb along that edge
  (down the middle from the front; the user's pick over the plain skull, both were cards on the page; the option is
  gone again, the page has one card "Armored (in progress)"). The visor is closed over the whole muzzle in every view:
  the nose's tip and the lips first left out under the beak drew the donkey's olive outline round the helm's mouth
  ("a border ... which doesn't really match the helm's colour", the user's next marked screenshot). Where a frame's
  own mouth moves past the helm (chew 02-03, lips 04/06/08-10, turn 02, the lowered head, turn 05's muzzle),
  `sealed()` gives those few donkey pixels next to the helmet its outline (its dark steel inside), so no olive shows
  in any frame (the standing frame has none to seal). Graze 03-05 no longer push the head's art through the majority-vote warp
  alone (it broke the one-pixel brow, sight and rim into dots): `helmet_turned()` turns the helmet's shape and shading
  and draws its lines (Bresenham through the turned pixel centres), breaths and rosette again on the turned head.
  Turn 03 (three quarters) and turn 04-06 (the front) have new drawings of the helmet. Found on the way: in turn 04-06
  the front lame was drawn over the donkey's muzzle (in the shipped cut too); the lames and the skirt now stay behind
  the head where it is turned towards us (turn 03-06; turn 04, 05, 06 give 15, 11, 11 lame pixels back to the muzzle;
  every other frame's body is the shipped pictures' pixel for pixel, checked). The skin's
  description says "a plumed knight's helmet". Approved by the user on the page (2026-11-02, "have this one replace
  the other one so we see it in game"): written into the pack (`--cfg`: pictures, skin.cfg, the pack's changelog
  entry) and deployed (the DLL checked: all 45 files byte for byte, the new description, not the old); the README,
  the docs, the media texts, the 1.1.0 notes, the zip and the Workshop folder still wait for the in-game verdict.
  The preview page still showed the old cut after the pack write (the user; nothing had rebuilt it): rebuilt, and
  `preview.py` now leaves a study the pack already holds off the page (it is the page's own Armored card then, so
  running the review loop after a pack write gives the right page with no repeated card).
- **Armored: the grazing frames drawn by hand, in review** (2026-11-02; the user on the page: "regarding the graze
  animation, an extra gray part is appearing"). Measured: in graze 03-05 the donkey opens its jaw to the grass and the
  jaw hung 2-3 px below the helmet turned with the standing head; `sealed()` painted that strip flat dark steel (24 px
  a frame, under the gilt rim). The user's pick of two fixes: graze 03 and graze 04/05 have their own drawings
  (`GRAZE_HELMET`). A first drawing on the turn's landmarks, closed over the open jaw, read as "the helm is expanding
  when he's bowing down his head" (the user): measured, its pixels were the standing helmet's (162/170 against 158),
  but the donkey's own lowered head is foreshortened (it turns towards us: the forehead stands tall over a short face,
  the eye level with the muzzle, at (25, 53) and (27, 57), not where the turn put it), so a full-size helmet stood
  over a smaller head. Now drawn on the frame's own head and flush with its outline: a narrow tall skull with the
  comb from the ear down to the gilt brow, the sight over the frame's own eye, the visor over the muzzle with the beak
  to the left, the rosette, the breaths, the roped rim along the jaw (117/120 px); the plume's holder moved onto it.
  `helmet_turned()` and its line drawing are gone, and the grazing frames are not sealed (it shut the eye). The user
  also: a bit of jaw showing is fine, an expanding helm is not; in the chew animation "a part of the nose is appearing
  over the armor" in some frames: measured, chew 03 and lips 04/06 lift the nose's top a pixel (the eye stays), and
  `sealed()` drew that pixel as an outline bump; the visor's top-front edge and the forehead line just above the brow
  are now a pixel fuller in every frame (4 pixels), so the lifted nose stays under the steel and nothing is sealed at
  the nose (the jaw's seal in chew 02 stays: the user, "the jaw part looks good"). Only out\ changed; the pack waits
  for the user's look.
- **Armored: turn 02's skull, in review** (2026-11-02; the user: "in frame 2 of turn_02, we can partially see the
  donkey's head unlike in the other frames"). Measured: turn 02 (the head starting to turn towards us, the helmet
  moved by the eye) shows more forehead, 1-2 px above and in front of the skull's edge, and `sealed()` had darkened
  two of those pixels. `TURN2_HELMET`: the standing helmet with its own skull rows (31-36), the outline on turn 02's
  forehead and the gilt comb inside it; the rest of the helmet and the plume as before. Only turn 02 changes. Then
  the user again ("part of the mount or that black something is still visible and all other frames are ok"): turn
  02's muzzle hangs a pixel further forward and a row lower than the standing visor, and `sealed()` painted those 10
  pixels flat dark under it; its own visor rows (40-45 in HELMET's coordinates) now close over them with the steel
  shaded on, the roped rim along the new bottom edge, and nothing is sealed there.
- **Armored: the grazing, nose and turn 02 fixes in the pack** (2026-11-02, the user: "make it in the game and replace
  existing"): `make_armored.py --cfg` wrote them (40 of the 44 pictures change: every side view for the visor's
  fuller top-front edge, graze 03-05, turn 02; turn 03-06 as before; skin.cfg unchanged), check_skin all good, the pack
  built and deployed with the game closed, the deployed DLL holds all 45 files byte for byte, and the preview page,
  rebuilt, shows them as the pack's Armored (the review card left off: the pack holds the same cut).
- **The skins preview page keeps its settings** (2026-11-02, the user: "make all settings in skins-preview.html
  persistable"): `skin_preview_html.py`'s page saves the clip, the view, the scale, the facing, night, the rider, a
  paused frame and which mounts are hidden in the browser's storage on every change, and restores them on load and
  after a rebuild (a clip the page no longer has keeps the default; no storage, the defaults). Checked in headless
  Chrome on a copy of the page: seeded settings restored (graze frame 3 paused, strips, scale 5, mirrored, night,
  rider, a hidden mount), and clicks saved.
- **The donkey skins' turn frames, in review** (2026-11-02, the user on the preview page). Both generators reproduced
  the pack exactly before the change, so only these frames differ:
  - Hellfire, "some black artifact appears between the horns while it's turning and while it's turned": measured,
    `fill_between_horns` fills the gap between the horns with the mane's tuft (made so in 2026-10-19, the gap
    flickered in the walk), 3-5 columns in the side views, but 7-15 where the head faces us (turn 04-06), where
    about 20 tuft pixels a frame went into the empty air: a black block. A first fix by the gap's width (rows wider
    than 5 left alone) kept turn 03's block (the user: "still exists in turn_03"): its gap is 4-5, as the standing
    frame's, but there the tuft is a flat-topped square standing between two upright horns, not hair tapering between
    leaning ones; I had seen it in my own render and taken it for the side views' tuft. Now `NO_TUFT` names turn
    03-06, left as the donkey has them; those four frames change.
  - Royal Parade, "the crown seems offsetted badly in turn_03": the eye rule (2 columns behind the eye) put the crown
    behind the head, where three quarters show the eye on the face's far side; turn 03 (`BETWEEN_EARS`) puts it
    between the ears as the facing frames do, on the head's top.
  - Armored, the plume in turn 03 "looks quite bad": a four-pixel stick by the near ear; now a bushy plume between
    the side view's and the front's, its holder in the skull's top middle.
  - Royal Parade, found by a sweep of every frame (not reported): in lips 04-05 the donkey blinks, no eye is found,
    and the forehead rule lifted the crown 4 rows off the head; in the clips where the head only moves (walk, ear,
    chew, lips) a shut eye is now the standing eye moved with the head (`MOVED_HEAD`).
  - Found by a fresh reviewer (a subagent given the page's frames and the user's complaints, not the fixes'
    explanations), all fixed on the user's "fix the found issues ... polished as much as possible":
    - Armored graze 02: the chin hung 2 px under the visor and the poll showed at the plume's foot (`EXTRA_SEAL`);
      cutting the chest plate behind the lowered head left 1-6 px bits in the neck (`drop_scraps`: pieces under 8 px
      go; only graze 02 shows a change). Turn 02: the helmet moves 2 px onto the ear, which peeked out at the plume's
      tip and down its left side; the plume a pixel left and up there (`PLUME_SHIFT`), the ear behind it as in turn 01.
    - Hellfire: the tuft between the horns 2 rows above their base instead of 4 (`TUFT_ROWS`; it stood up as a dark
      nub; still the same in every frame, so the walk's flicker stays fixed: every side view changes a little); the
      glowing eyes in graze 03-05 (the eye is a light pixel there that no rule finds: its place in `EAR_FRAMES`, the
      glow only, the body's box as built: 5 px a frame change); turn 03's far ear lying back over the neck, a flat dark
      slab behind the far horn, erased (`FAR_EAR`; the horn settles onto the head below it).
    - Royal Parade graze 03-05: the crown's front end overhung the lowered head's edge by a pixel; never past the
      head's front there now.
  - A second fresh review, the gate before the packs, found what the first round's fixes left, all fixed:
    - Royal Parade: graze 03-05's crown was still a pixel out on the forehead's slope (the clamp now takes the head's
      front on the crown's four lower rows, not two); turn 03's crown jumped 3 columns left of turn 02 and back
      (`TURN_NUDGE` 2: its left edge goes 33, 35, 34, 34 through turn 01-04).
    - Armored: erasing (docs/skins.md "Erasing from the donkey") instead of sealing where a frame's own pixel pokes
      out: graze 02's chin under the visor, the forehead's outline over the dome in lips 04-06 (the forehead lifts a
      pixel), turn 02's ear strip down the plume (`POKE_ERASE`: 15 pixels in 5 frames, built whole in the game);
      sealed in the helm's outline those stood as bumps. Lips 05 keeps the helm where lips 04 has it (`HEAD_MOVE`: its
      ear base rises a row but its jaw doesn't, and the helm showed a row more throat for one frame). Graze 02's
      9-pixel chest-plate corner hanging in the air before the neck goes (`drop_scraps`: a cut piece mostly off the
      donkey goes too).
    - Hellfire: turn 03's last far-ear pixel by the rear horn, and in turn 04-06 the head's top corner beside each
      horn's base (a pixel that stood out like a brim), erased.
  Reviewed on the preview page as "Hellfire (in progress)", "Royal Parade (in progress)" and "Armored (in progress)"
  beside the shipped ones; the gate's re-check: every finding fixed, nothing worse, nothing new. Then written into
  the pack on the user's yes (2026-11-02 16:51: Armored 6 pictures and its skin.cfg note, Hellfire all 44, Royal
  Parade 6; the packs' pictures equal the reviewed studies byte for byte), check_skin all good on each, built and
  deployed with the game closed (the DLL holds all three skins' 45 files byte for byte), the page rebuilt from the
  packs. The pack's 1.1.0 notes have a "Hellfire and Royal Parade polished" line.
- **`tools/skins/page_frames.py`** (2026-11-02): the review of a skin change looks at the page's own pictures (cut out
  of skins-preview.html: the composites with the skin's erase marks and its own [Base.*] recolour, exactly what the
  user sees), a row per skin, a clip's frames or all 44, zoomed, any part of the canvas; it refuses to write into the
  repo (the pictures hold the game's art). Made after a review on separate composites (no recolour) missed what the
  page showed.
- **Armored: the skirt and the plume follow the Second tone** (2026-11-02, the user in the colours window: "the
  harness colour should just colour the harness normally ... the thing on his head and the armor trim at the bottom
  should be controlled by the second tone; right now the second tone doesn't do anything on this skin"). Its
  skin.cfg's `[Colors]` names the cloth's five reds `Muzzle` (the Second tone row) instead of `Harness`; the harness
  colour now reaches only the donkey's own harness, as on the plain donkey. The description says "follow your second
  tone". Pictures unchanged (44 of 44 equal the pack's); check_skin all good (the five as Muzzle); written into the
  pack (`--cfg`), built and deployed with the game closed, the deployed DLL holds the new `[Colors]` line and not the
  old one.
- **Armored: the plume sways lightly in the walk; the skirt keeps its front** (2026-11-02, the user: "make that red
  thing sway a little bit while the donkey is moving ... nothing should really be 100% static while moving"). Only
  the plume's top tuft moves, a column back and a column forward once per walk cycle, each held two frames
  (`SWAY` 0 1 1 0 -1 -1 0 0, `TILT`, `plume_tilted()`); the rest is held by the holder (the user: "it moves from the
  top because ... the bottom part is attached to the helment and the top part isnt"). The tilt changes only in a frame
  where the head steps the other way, so the tip never jumps two columns on screen (the loop too), and the outline is
  closed where a moved row leaves the cloth bare. Four tries went before it, each seen by the user: the bush bending a
  column each way four times a cycle (deployed, then "wiggling at the top like some jelly thing"), the plume leaning
  back over one joint low in the stem ("looks like its being ofsetted"), a stiff two-column tilt ("goofy"), and the
  whole bush tilting on the stem (shown beside this one; the user picked this). The skirt ("the front red cloth part
  is sometimes disappearing while walking ... like in frame 2 walk_02"): under the breast collar the cutter measured the
  chest's front down to a fixed row, and in walk 02 and 06 (the body a row up) that row was already the belly's curve,
  3 columns back, so the skirt's front corner was cut 3 columns short; the rows now stop where the front steps back
  more than a column (`CHEST_STEP`), so those two frames match the other six. Walk 02, 03, 05 and 06 change against
  the last commit (01, 04, 07 and 08 hold the plume upright, as drawn). A gate review found no stray pixels, gaps or
  new contact with the ear or helmet, the skirt fixed and nothing else changed; check_skin all good; written into the
  pack, built and deployed with the game closed, the deployed DLL holds all 45 files byte for byte.
- **Armored: the whole harness in view** (2026-11-02, the user's marked screenshot of the chest: "missing
  part of the harness in all animations"). The plates covered 66 of the harness's 115 pixels standing (the strap's
  shaded underside and its end under the chest: `strap_of()` sees only its lit orange). `harness_of()` takes the
  API's own Harness colours (`Colors.cs`, through `skin_preview_html.donkey_regions()`); the lames, the skirt, the
  rump plate and the tail guard leave it in view and the strap's shadow falls beside all of it; the breast plate keeps
  its edge over the strap's front outline (a plate only in front
  of the whole strap emptied in 11 frames, in the walk it would come and go). `drop_scraps()` then takes the slivers
  the strap's end cuts off the skirt's and the lames' fronts, not counting what the head covers anyway (a gate review
  of all 44 frames found a 6-px lame stub kept in turn 05/06, 12 more of it under the helmet). That review found the
  strap whole and in one piece in every frame and nothing coming and going. The user looked on the preview page; written
  into the pack (check_skin all good), built and deployed with the game closed, the deployed DLL holds all 45 files
  byte for byte.
- **Armored: walk 08's bit of cloth in front of the strap gone** (2026-11-02, the user: "in the walking
  animation, walk_08 is the only frame where that marked bit of red cloth is shown"). In every walk frame the skirt
  is drawn forward to about x 32 at the chest's foot and the strap's end cuts that front off; in walks 01-07 it
  leaves 2-4 pixels, dropped as scraps (`drop_scraps`, under 8), in walk 08 (the strap a column further back, the
  chest filling the corner) 12, kept. The skirt now keeps its main piece only (`main_piece`, on `pieces()` split out
  of `drop_scraps`): it ends at the strap in every frame. Measured first: of all 44 frames and the five body layers
  only walk 08's skirt was ever more than one piece; the new cut differs from the pack in those 12 pixels alone
  (check_skin all good, phase checks ok). The user looked on the preview page ("ok looks good"); written into the
  pack (only walk 08's picture changed), built and deployed with the game closed, the deployed DLL holds all 135
  files byte for byte.
  The same evening's two checks: the helmet over 2-14 harness pixels in turn 02-06 is the strap's edge passing
  behind the helmet's back (in front of the neck: right, the strap whole in every frame); the cloth's five reds are
  named Muzzle (the Second tone) and used by the skirt and the plume alone, no colour of the skin is one of the
  donkey's regions' (18 colours: the exact swap), no half-transparent pixel.
- **Hellfire's cracks where the walk has them, Royal Parade's cloth steady** (2026-11-03, the user on
  the preview page: "the hellfire one, the red lines on his back are moving all over the place and the royal parade,
  the red rug is being pulled back", then "none of the other animations red lines match the good ones in the walking
  animation"). Both in the pictures since the skins were made (the 10-28 commit's frames have them the same); in the
  turn the donkey's body behind the neck is pixel for pixel the standing frame's, only the head and neck move.
  - Hellfire: the cracks are scaled into a torso box that starts 10 columns past the eye. The eye sits 2-6 columns
    further back when the head isn't reaching forward as in the walk, so the standing pose's box came out 22 wide
    where the walk's are 27-28 (its front edge 4 columns back, 2 rows lower), graze 02-05 and lips 04-05 differed
    again, and through the turn it moved with the head (left edge 44-48, width 22-26, height 14-21). Every clip but
    the walk now copies walk 01's crack pixels, placed by the body (`make_hellfire.crack_pixels`: the rump-matched
    hip and the back over the rump; walk 01 starts the walk from the standing pose with the same hip and back) and
    put where the frame has coat (`stamp_cracks`; the strap stays in front): every crack pixel of those 33 frames is
    one of walk 01's (44 of its 45, the 45th under the strap), against 19-34% before. A first cut drew the lines
    into walk 01's box placed by the body; the gate review found it 43 of 45 and 2 pixels more in graze 04-05,
    where the lowered neck uncovers coat under the strap. The walk and the head frames byte for byte as before. The
    rump's fill and the devil's tail's root in turn 02-06 take the standing frame's box (`STILL_BODY`,
    `standing_box()` in hellfire_proto.py), not the moving one: the tail no longer flips up in turn 02, 03, 05 and
    06 and back in 04.
  - Hellfire's rump in lips 04-08 (the user: "why is the donkeys ass growing larger in hellfire lips animation?"):
    the donkey lifts its tail there and the rump's edge just above it moves back 1-2 columns (x 66, 67 in lips 06,
    against 65 standing); the rump fill drew its curve from that edge, a column wider over 12 rows (two in lips 06).
    Those frames now set the standing frame's curve (`STILL_RUMP`, `make_hellfire.rump_curve`, `fill_rump(...,
    curve_from)`); the devil's tail's root follows. Checked over every frame but the walk, behind x 56 against the
    standing frame: what the skin itself makes differ went from 8-76 pixels (graze 02-05, lips 04-08, turn 02-06) to
    none; what is left (2-6 pixels in lips 04-09) is the donkey's own frame.
  - Royal Parade: the cloth hangs from the hip, and anatomy.body()'s hip read the turned head's far ear as the
    back's top in turn 02-03 (63 and 61 for a rump at 65: the cloth's rear edge 2 and 4 columns forward). Armored's
    rump-matched hip (make_armored.body_of: the standing frame's hindquarters laid over the frame) is now the shared
    `anatomy.body_by_rump()` (Armored byte for byte as before), and turn 02-03 hang the cloth from it
    (`HIP_FROM_RUMP`): those 2 frames change. A first cut hung every frame from it (17 changed): the gate review found
    walk 04-06 a column forward of a rump that doesn't move, the hem stepped in walk 04 and 06, a corner poking out
    in graze 02-05, and no measure tried (body(), the rump match, the rump's outline by rows) agreed to the pixel on
    the walk and lips 04-08; those 15 stay as shipped until the body's points are verified once.
  Both generators reproduced their packs exactly before the change. On the preview page as "Hellfire (in progress)"
  and "Royal Parade (in progress)" beside the shipped ones, the gate reviews' findings fixed; the user's yes ("ok make
  all fixes in game"): written into the pack (Hellfire 33 pictures, Royal Parade 2, equal to the reviewed studies byte
  for byte; check_skin all good), built and deployed with the game closed, the deployed DLL holds all 135 files byte
  for byte, the page rebuilt from the packs.
- **`tools/skins/consistency.py`, and why the skins kept slipping** (2026-11-03, the user: "we are not making good
  enough progress with constant mistakes and artifacts and sloppiness"; docs/sprite-tools.md "Catching what the user
  caught"). Read only, for any skin drawn over the donkey: (1) on every pixel behind the neck where the plain donkey is
  the same as in the frame most like it, the worn skin must be the same too (the walk left out unless `--walk`: the
  body bobs and 21 of the 24 walk frames were listed); (2) a piece of the skin with no skin pixel near it in either neighbour
  frame of its clip is listed; `--maps` writes the listed frames with the pixels boxed (outside the repo only). On the
  skins as committed before that night's fixes it lists all four of the user's complaints (walk 08's 12-px bit, Hellfire's
  turn, graze and lips, Royal Parade's turn); on the packs after them Hellfire none, Royal Parade 4 frames (the 15
  kept as shipped), Armored 5 (graze 02-05's lames seam and hem, lips 04's tail guard, fitted to the lifted tail), for the user to judge. The
  investigation behind it: the three generators find the body with rules of their own and carry 15 tables of frames
  named by hand where a rule failed; one set of body points for all of them (`rig.json`) and a page to check them
  once are proposed (built since: the next entry).
- **The donkey's body points, and a page to check them** (2026-11-03, the user: "yes build the body points and the
  page because any tool that can help us accelerate this work is vital"; docs/sprite-tools.md "Catching what the user
  caught" 1 and 3). `tools/skins/donkey/rig.json`: per frame the hip, back, withers, chest, tail, both ear bases, the
  eye (and the second one where the face looks at us), the poll and the muzzle, with the SHA-1 of the frame they were
  measured on; `anatomy.rig(key)` reads it (and says when the game's frame changed). `tools/skins/donkey/rig.py`:
  `build` fills it in (a frame with an earlier one's body behind x 50 copies its rump points, 20 of 41; a clip whose
  rump reads one shift takes one hip, the walk 65 in all eight with its row riding the bob, where the per-frame pick
  alternated 65 and 66; Hellfire's approved eye and ear tables; the frames where the measures disagree flagged with each
  answer, 16, and Claude's pass on them as a note each, see docs/sprite-tools.md),
  `check` (rump points moving between neighbour frames, changed frames), `sheet` (the points drawn, outside the repo),
  `page` (`BepInEx\GK2-asset-probe\rig.html` from `rig_page.html`: flagged frames first, the frame zoomed over the
  standing one, drag or arrow keys, the measures as rings to pick, the clip played with its points, every point's
  offset per frame, Save as a download) and `import` (checks a saved file, lists each change, writes `rig.json`). The
  page renders headless; import listed a moved point and a ticked frame, refused a broken point, and put the original
  back byte for byte. (Read by Hellfire and Royal Parade since: the next entry.)
- **Hellfire, Royal Parade and Armored on the body points** (2026-11-03, the user: "continue"; docs/sprite-tools.md "Catching
  what the user caught" 1). First Claude's pass over the 16 flagged frames, each decision a `note` in `rig.json`
  shown on the page (`checked` stays the user's): the walk's rump is the standing curve moved up and down with the
  bob, never sideways, so its hip is 65 in every frame (the first measure read the lift on the slanted curve as a
  column and said 66: `rump_move` now matches the curve up or down from its own top row); walk 08's whole rear
  outline sits a column back, held at 65 so nothing hung from it jumps for one frame; graze 02-03 65; lips 04-08 65
  (the rump's top bulges a column as the tail lifts, the body stays). `rig.py build` now keeps a frame with a note
  as a body later frames can copy (it lost graze 04-05's copy once) and rebuilds the same file.
  - Hellfire: the cracks' anchor is the rig's hip; a frame whose rig rump is the standing frame's takes the standing
    frame's rump fill and tail row (replacing `STILL_RUMP` and `STILL_BODY`; the fill leaves the edge above the tail
    as drawn where it already is the curve's, which it had rewritten in 26 frames); the horns stand on the rig's ear
    bases (replacing `HORN_FRAMES`, `EAR_FRAMES`' horns, the eye's offset and the skull's top). The pack byte for
    byte the same.
  - Royal Parade, in review: the cloth hangs from the rig's hip (`HIP_FROM_RUMP` gone): walk 03, 04, 07, 08, graze
    02-05 and lips 04-08 change, the cloth now on columns 47-63 in every frame of those clips (shipped 47-48, 49 in
    lips 06); `consistency.py` lists none of its frames where it listed 4. Reviewed on the preview page as "Royal
    Parade (in progress)"; the user took the drawn-once cut instead (`stamp.py` below), written into the pack
    2026-11-03.
  - Armored, in review: `body_of` takes the rig's hip, where the rump match said 64 in walk 04-06 for a rump at 65:
    those three frames change (its rump plate, rearmost lames, skirt rear and tail guard a column back); the phase
    checks ok. Reviewed on the page as "Armored (in progress)" (the user: "i cant spot the differences between the
    armored ones so i guess the tool one is good"), written into the pack 2026-11-03.
- **Draw once: `tools/skins/stamp.py`** (2026-11-03 night, the user: "continue working on the rig tool and any other
  tools that can help us create skins/mounts in a more straight forward way to minimize guessing"; docs/sprite-tools.md
  "Catching what the user caught" 4).
  - **What it does:** a skin over the donkey is drawn as a few pictures and placed into all 44 frames by `rig.json`'s
    points, with no rule of its own that finds the body:
    - `body.png` is moved as one, by the hip and the middle move of the back line under it;
    - `body_drape.png` is moved column by column;
    - `head.png` and three head views follow the ear bases;
    - `<piece>@<frame>.png` is for a single frame.
  - **The commands:**
    - `build`: the checks, then a card on the preview page;
    - `moves`: where the body's top is a pixel off the back, and where a pattern would step, before anything is drawn;
    - `template`: an `.aseprite` drawing surface in the skin's git-ignored `out\`;
    - `export`: the layers back into the pieces;
    - `watch`: export and build on every save.

    `export` leaves a piece changed outside the template as it is and names it. `template` refuses to overwrite a
    drawing without `--force`, and to write where git would take it.
  - **`new_skin.py <skin> --stamp --pack <Pack>`** scaffolds such a skin in `tools/skins/<skin>/`: `skin.cfg`,
    `.gitignore`, `Watch <Name>.cmd` and the template. The pack's folder is made only when it ships.
  - **`ase_write.write`** takes a layer's opacity.
  - **Proved on Royal Parade**, cut into pieces from its in-progress cut and staged as "Royal Parade (drawn once)":
    - the crown is identical to the generator's in all 44 frames;
    - the cloth is one shape in every frame with no slide;
    - `check_skin.py` all good, `consistency.py` 0 frames;
    - the gate review's three findings (all from the column moves and the strap below) are gone.
- **What drawing the Jester asked of the tools** (2026-11-03 night, the user: "try it out by creating a new donkey skin
  and create a full medieval jester skin ... so we can see how they help us and how we can improve them"; each was a
  scratch script or a hand step first; docs/sprite-tools.md "Catching what the user caught" 4 and 5):
  - **`stamp.py look`**: the pieces worn in chosen frames without a build, zoomed, with a grid, coordinates and the
    rig's points, at 1x and 3x. About a second, where a build and a page cut take 20-60 s.
  - **The donkey's moving parts under a head piece:** `moves` lists the frames, and the template's `moving (guide)`
    layer shows them in magenta (`stamp.moving()`: the ears' flicks, the blinks, the mouth, which a cap hides in some
    frames and not others).
  - **`tools/skins/pixgrid.py`**: pixel art as text, a palette and grids at canvas coordinates. `render` makes a
    piece; `dump` writes any picture's box as a grid, which is how the donkey's outline was read before drawing
    against it.
  - **`tools/skins/shapes.py`**: tapered strokes, outlines, lighting and a bell, shared (out of the Jester's drawing
    script, byte for byte the same pieces).
  - **`canonical_cfg.py <skin.cfg>`** rewrites one cfg by path (a source outside the packs).
  - **`stamp.py build` prints the checker's warnings** (only its verdict before: "1 thing to look at" and no thing).
  - **`new_skin.py --stamp`'s next steps:** `look`, the guide, and `[Colors]` by hand (`skin_colors.py` only walks
    the packs).
  - **`stamp.py`'s build split into `read_source` and `frame_of`**, which `look` shares; the pack the same.
  - **`shapes.pose`**: a shape drawn on one frame turned and moved onto another by two rig points (the poll and the
    muzzle), then drawn again, so a tilted head's piece keeps whole outlines (graze 02-05: -10 to -41 degrees).
  - **The moving-parts guide compares the outline only**, and only in the clips whose head is the standing one moved.
    Compared colour by colour, the walk's head, shaded differently all over, came out as one moving blob.
  - **The template takes the source's one-frame drawings** (the Jester's posed caps): a frame each,
    its layer that drawing; the frames it was made with are kept (`out/pieces.canvases.json`) and read by the export.
  - **`stamp.py build` names an erase that clears the harness** (the collar strap's orange): the Jester's ears-off
    variant cleared the strap's top in graze 02-05 once; checked with one planted over it.
  - **`stamp.py views`**: the head piece in every frame by numbers (size, box, middle), each clip's neighbours
    compared against both the poll's move and the face's: what the Jester's reviewers measured by hand, as a command.
  - **`stamp.py look -`** shows the bare donkey's frames with coordinates and points (tonight's scratch scripts);
    `views` is a report and never fails; **`tools/skins/royal-parade/drawn_once.py`** writes the drawn-once card's
    source from the generator's frames and compares the two (it lived in the scratchpad before).
  - **Moving head pieces with the face, tried and taken back:** the third review's turn 04 step led to moving by the
    eyes and the muzzle instead of the ear bases (the two agree in every frame but turn 04 and the turned heads). The
    fresh review of the Royal Parade drawn-once card then found its crown flicking a pixel left in turn 04, where the
    face moves sideways and the forehead doesn't. stamp moves by the ear bases again; the Jester's turn 04 has its own
    drawing, the same pixels under either rule.
- **The Jester, in progress** (2026-11-03 night; docs/sprite-tools.md "Catching what the user caught" 5): a new
  donkey skin made with the tools above, `tools/skins/jester/`. Not in a pack, staged as "Jester (in progress)".
  - **The pieces:** a harlequin caparison (red and yellow diamonds 7 across, a yellow piping, three dags with a gold
    bell each), and a parti-coloured two-pointed fool's cap with bells, the donkey's own ears rising behind it.
  - **The cap's frames:**
    - its side view, low on the forehead so the ear rises whole behind it;
    - a three-quarter view, a front view (turn 04-06) and a grazing view (graze 04, used in 03-05);
    - the side cap posed by the rig onto turn 02 and graze 02, which tilt 9-10 degrees.
  - **Colours:** the reds Harness, the yellows the Second tone.
  - **Checks:** `check_skin.py` all good and `consistency.py` 0 frames.
  - **Six fresh gate reviews**, their findings fixed (the sixth on both cards):
    - the first: the brim strand, the ear in a hook, graze 02 -> 03's change of hat, turn 03's front view, turn 04's
      point;
    - the second: the posed cap floating on the graze's upright ear, turn 03's flat band, the ears reading as lumps,
      a bell against the strap, the cloth reading as one band.
    - the third: turn 03's cap too tall, turn 04's cap a pixel low (now its own drawing), the ear's tip a pompom
      and a wart, the graze cap thinner, the cloth reading as spots.
    - the fourth: turn 03's yellow half standing up, the graze cap a headband with a ribbon (now the front cap's
      shape on the skull's top), the hem's dags lost to the outline (now tabs with points), a dark band cutting the
      lower diamonds, the bells and turn 04's cap stepping (turn 04 takes the front cap as drawn).
    - the fifth: the brim's lone end pixel, the yellow diamonds' flat tips, the front bell on the collar; its ear
      finding is the choice below.
    - the sixth (both cards): graze 02's notch behind the cap, ear stubs beside the front cap, a nub on the graze
      cap's peak, the ear clip frozen under the cap (the yellow point now lifts as the ear flicks), turn 03 a row
      high. These last fixes were checked with `look`, the page cuts and the erase check, not by a seventh review.
  - **A second card, "Jester (ears under the cap)"** (`draw_jester.py --ears-off`): the same skin with the donkey's
    ears cleared above the cap along the moving-parts guide, for the user to choose (the ear behind the cap read as
    a lump in ear 02-04 in three reviews; on a forehead cap that is a choice, not a bug).
  - **Left:** the second review's lips 04-06 (the rig's ear points from Hellfire's table move in 05 only, the head's
    top in 04 and 06 a pixel up in part).
  - **The second design** (2026-11-03, the user on the two cards: "it looks like parade one with a messed up hat right
    now, so not amazing"; docs/sprite-tools.md "Catching what the user caught" 5, part 12). One card, "Jester"; the
    two first cards are gone.
    - **The cap is the silhouette:** a fool's hood with two points arching up where the ears were (the ears cleared
      under it), a bell on each. In the walk the tips trail the head's bob by a pixel. Graze 03-05 have the front hood
      at full size, tilted with the head.
    - **A collar behind the jaw:** a blue band with red and blue points hanging from it and a bell on each, the same
      object in every view (a U under the chin from the front). Tried before it: a collar along the whole strap ("a
      scarf"), ruffs of radiating points ("flags", "a bow tie").
    - **The cloth lost Royal Parade's tells:** quartered red and blue, no gold trim along its top, its rear edge and
      its hem cut into points, the hem's bells above row 60 (at hoof height they read as extra feet).
    - **The donkey's own strap is tinted the motley's red** under the skin (`[Base.Harness]`), where its leather
      showed between the pieces. The player's harness colour still reaches it on top.
    - **Colours:** the red is Harness, the blue the Second tone; the hat's yellow band, the outline and the bells are
      drawn as they are.
    - **Four fresh blind reviews** (`page_frames.py --blind`, the pair with Royal Parade): each named the jester at
      once and read the two as "a matched court pair (king and fool)". The first found the first design's cloth was
      Royal Parade's block recoloured. Their findings on the cap, the graze, the hem, the collar's shape and stray
      pixels were fixed.
    - **Left for the user:** the collar's read from the side (the last review: "a bell strap hanging from the hat"),
      the graze hat's tilt, the stepped brim of turn 02 and graze 02, and whether the head-only frames
      (`hed_static_down`) double the collar over a walking body in the game.
    - check_skin all good, consistency 0, no erase on the harness or below the poll.
  - **The third design** (2026-11-03; the user on the second: "it looks like a 5year old drew it, compare it to the
    others", "the parts of the skin look like stickers"). Measured against the shipped skins first: 34% of the
    second design's pixels were near-black outline (Royal Parade 0%), it drew 717 px (Royal Parade 311), its fills
    were flat pure red and blue, and every piece moved rigidly. It was drawn with no references. It is redrawn from the
    user's references (a reenactment fool's hood, a manuscript fool, jesters on donkeys, donkeys in costume) in the
    shipped skins' construction:
    - **colours:** deep burgundy (Harness) and navy (the Second tone), five shades each, every edge in its own
      colour's darkest (no black), gold only on the band and the bells;
    - **the cap:** smaller, its points shaded like soft tubes, its band curving round the head and its top dipping
      between the points (a straight band under a flat top read as "an anvil on his head", the user);
    - **the collar:** laid along the donkey's own strap, which fits the neck in every frame, three belled points over
      the shoulder (a band round the throat across the strap read as a second strap);
    - **the cloth:** quartered, shaded light at the top to dark under the belly, soft folds, a pointed hem with two
      small bells;
    - **motion in every clip:** each hanging part (the cap's tips, the collar's points, the hem's points, the cloth's
      lower half in the walk) on its own path, a pixel a step, the walk's on a pendulum through five places (the bells
      had toggled between two: "more framerate", the user), the ear flick kicking the cap's back point, the chew bobbing
      the bells, the lips clip swaying slowly, the graze and the turn trailing the head;
    - **two sketches first** on the page ("Jester sketch A" and "B", the cloth halved or quartered), the user's pick
      taken into the third design.
- **`stamp.py build` marks consistency lines in frames with a body drawing of their own** ("its own body drawing:
  the skin's motion"): the Jester's hem sways in the lips and graze clips while the body stands still, which the
  check rightly lists; the mark says where the motion is deliberate, so the line is read, not taken for a slip.
- **A collar piece in `stamp.py`: `neck.png`** (2026-11-03, for the Jester's second design; before it, "nothing can sit
  on the neck").
  - **It moves with the collar strap**, by the rig's chest point (the breast collar's lowest point). Measured first:
    in the walk the strap moves 2-4 px forward of the body as one piece, and the chest point's move lays the
    standing frame's strap on 47-50 of its 50 pixels in every frame but seven.
  - **Where the strap bends** (graze 02-05, where the neck lowers, and turn 04-06, where the breast collar turns to
    us; 29-42 of 50) the piece is left out unless drawn there (`neck@<frame>.png`), and the build says so
    (`STRAP_BENDS`).
  - It is laid between the body and the head; `moves` reports it; the template has a neck layer and frames for the
    bends.
- **`shapes.curve` and `shapes.polygon`**: a smooth path bent towards its middle points (a jester's point arching
  over and drooping), and a polygon filled (a hood's outline, corner by corner, where a stroke can only round it).
- **`shapes.pendulum`, `shapes.hanging` and `shapes.swing`**: a hanging part's swing over a clip (out of the
  Jester's drawing, which now uses it; its pieces the same), the parts of a picture that hang free (by a test of the
  pixel: a fringe's tassels, a hem's point), and each of those moved by its own. **"Royal Parade (drawn once)"
  sways** with them (the user: "it just looks a little bit too staticey, maybe we just need to add very very slight
  animation to it"):
  `drawn_once.py` writes a cloth of its own for each walk and lips frame, where stamp places `body.png` there, its
  hanging bits swinging a pixel either way, over the stride and slowly over the lips clip: the front fringe's three
  tassels together, the point's tassel and the rear fringe's end together a frame behind them. Only the
  tassels' ends had moved at first, too little to see (a fresh review: "it reads as a stiff cloth riding the body");
  moving the point's bottom edge too slid its red middle off the red above, and tassels a frame apart closed into a
  block, and the rear end a frame behind the point's tassel made the gap between them blink. The cloth's edges, its
  fleur and the crown as they were; the other 24 frames unchanged. `check_skin.py` all
  good; `consistency.py` lists the 11 lips frames where the bits hang off their resting place, each marked as the
  skin's own motion.
- **A front layer in `stamp.py`** (the user, on the Jester in the game: "we need to implement the hat the same way we
  implemented the wing of the dragon so that the hat appears on top of the rider, not underneath"; docs/front-layer.md):
  a source's `stamp.cfg`, read by stamp alone (the game reports a `skin.cfg` key it doesn't know), says `[Stamp]
  front = head`, and `build` writes those layers' colours into the pack's `front\` pictures, which the game draws in
  front of the rider, and leaves them in the main picture too. Taken out of it at first, the donkey under the cap
  showed through: its ears over the cap on the foals, and lines on it in the walk (the user, in the game). Nobody
  riding, the game lays the front picture as little above the body as the main one (`OverLift`), so the two tie in
  the depth test, and ridden it can sit a fraction of a pixel off; under it there must be its own pixels. Their erase
  pixels are in the main picture only (a front picture can't erase), every frame the layers draw in gets its front
  picture, and the template's preview layer shows both. The pieces stay where they
  are, so `look`, `views`, `moves` and the template work as before.
  - **The Jester's cap is in front of the rider** (`tools/skins/jester/stamp.cfg`): 44 front pictures; nobody riding,
    every frame is the same as before, pixel for pixel. With the page's rider on the seat row the cap covers 28-40 of
    his 702 pixels in the walk (2 on his head, in walk 05), 67-79 standing and in the idle clips, 8-21 of them the
    front of his face (the navy point's bell, columns 50-51), 3-28 grazing and 39-56 turning. The collar stays behind
    him (23 of its 110 pixels under his arm).
  - **`page_frames.py`** draws a skin's front layer over each frame (it showed the main picture alone), and
    `--rider` puts the page's rider between the two, as its Rider button does.
- **The Jester's cap smaller** (the user, a marked screenshot: "the jester hat is too big from the back and that
  exceeds the donkeys head from the back, so i think it needs to be scaled down"). Measured against the donkey's
  head alone (the game's `hed_static_down`): the cap was 28 px long over a 22 px head, its back 3-8 px past the head's
  back at the poll (rows 34-36), the band running on behind the head. `draw_jester.smaller`: every view's cap 0.8 as
  tall, its points 0.8 as long and thick; the side cap also 0.8 as long, about the brow, so the band ends on the head's
  back (+2, -1, -3 px at rows 35-37) and the cap is 24 px long. The front and three-quarter caps keep their width,
  the head's (0.9 as wide, the three-quarter cap left the far ear lying back in view). A fresh review then found the
  graze cap (the front cap tilted down with the head) still as wide as the front one, its navy half 8-12 px behind
  the poll over the neck and the collar strap's loop: it is its own now (`DOWN`), 0.65 as wide, its middle moved two
  columns forward onto the skull (the lowered head's ear bases are 4 px apart, 11 facing us).
  - **The side cap ends at the poll** (the user, on both cards, their purple mark again: "cant u see they both extend
    to the harness at the back?"). Scaled, its back still ran to x 45-47, against the collar strap's top (45, 39): the
    first measure took the head sprite's back outline, which reaches the strap, for the head's back. Its navy half's
    back now comes down from the crown at x 42 (the poll is at (41, 34)), the band ends there, and the navy point rises
    from the crown and hangs back over the ears, its bell at x 43-44, rows 31-34; behind the cap the head's back and
    then the strap show. The three-quarter and front caps are as wide as before (the far ear lies back under the
    three-quarter one).
  - **Then narrower and centred over the head** (the user: "now align its center better but also make it a bit smaller
    width wise even"): cut at the poll, its middle sat 2 px before the middle of the head's top (the forehead at x 30
    to the head's back at 45) and the burgundy point hung far over the face. `narrowed`: 0.85 as long about its
    middle, two columns back: the body x 32-43 (11 px, was 13), the band's front end off the brow (the top of the
    forehead shows in front of it), the navy bell over the ears at x 44-46, rows 30-33, above the strap's top.
  - **Then into the box the user marked** (orange over a walk frame of the page's card, the cap as it was in blue:
    "your hat is aligned in the blue box but im thinking more around the orange box"): `fitted` takes the side cap
    from x 28-46, rows 26-38 (its bells too) into x 32-44.5, rows 25-35.5 on the standing frame, its points' thickness
    with it: a smaller cap sitting on the head's top, both points rising from it, the band at rows 33-36. The
    three-quarter, front and graze caps are not changed yet, so the cap grows as the head turns to us.
  - **The collar burgundy** (its band; its points navy, burgundy, navy): the user, on the page, "the cap still looks
    huge, extending the back". The collar's band was navy, under the cap's navy back point: the two read as one blue
    shape about 25 rows tall, the cap running on down the neck. Burgundy, the collar is the strap's own colour (the
    skin tints the strap burgundy) and the navy stops at the cap's gold band. A card "Jester (cap 70%)" beside it on
    the page, the cap smaller still, for the user's pick, removed once the cap's back was cut at the poll.
- **The Jester's cap placed by hand in Aseprite** (`tools/skins/jester/place.py`, `Place Jester.cmd`; the user: "make it
  work by layer in aseprite so that i try moving it by hand for one image and see how that works"; docs/sprite-tools.md
  "Placing a piece by hand"): `out/place.aseprite` has a frame per view of the cap (standing, turn 03, turn 05, graze
  04) with the cap on a layer of its own; a save is measured (the cap layer's box against the one it was made with)
  into `placement.json`, a move and a scale per view, which `draw_jester.py` applies to the cap's shapes, so the cap
  is drawn again clean at that place and size in every frame drawn from that view; then the build and the page card.
  Tested with a drag written into the file: (2, 1) on the side frame moved the side cap by (2, 1), no other view. The
  first placement: the side cap 3 px forward and 1 px up (the user's blue box: "align it more towards the front of his
  head like my blue box").
- **The Jester's cloth reaches the harness** (the user, a screenshot with the bare shoulder filled in: "can you extend
  the cloth on the donkeys back to go right under the harness itself? ... so that none of the donkeys back is shown
  due to the cloth, and extend the bells appropriately too"). Measured first: the cloth moves with the body and the
  collar strap with the neck, and in the walk the strap runs 2-4 px ahead of the cloth and up to 2 lower (stamp's
  neck and body moves), so a strip of the shoulder up to 5 px wide showed there (standing, only the strap's dark back
  edge and a wedge over the hem). `draw_jester.tucked`: every side frame's cloth its own (body@ for all 41 frames but
  turn 04-06), carried forward on each row to just behind that frame's own strap (its orange) down to the hem, its
  colours and quarters continued from an inner column, edged where it meets the donkey; one more hem point (x 45)
  and a bell under every point, four (two before). In turn 04-06 too (the donkey facing us; left out at first,
  the chest showed between the front collar and the cloth: "now fix the turn 04-06 chest gap too"): the strap
  still runs down the near shoulder there, and the cloth carried to it fills the strip to the front collar.
  - **A dark bar inside it, fixed** (the user: "many frames have horrible artifacts, very bad artifacts, like walk_04",
    marked; "Is the process we wrote not good enough to be able to spot these artifacts?"). The walk's and the lips'
    sway move the cloth's lower rows a column; `tucked` filled to the unswayed front, so the gap left at the old
    front edge counted as outside and both its sides were outlined in the darkest: a bar 2 px wide, 4-5 rows tall in
    walk 04-06, and a 2 px line at the new part's top in 35 frames. Each row now fills to its own front. Not caught
    because the zoomed look covered the standing frame and walk 03, the two where the hem does not sway, and no check
    looked for it. The new part has a fold of its own (x 42, ten columns before the cloth's first, a shade darker), as
    the user asked ("needs another one of those vertical cuts on the extended part to make it like the rest"); at
    x 42 it lay under the collar's bells, so it is at 47 (the cloth's vertical lines are 5 apart), moving with its
    row's sway. Then a second at 42 (the user: "that part of the cloth needs another seem because it looks too wide
    to not have a seem": in the walk the part from its front to 47 ran 6-10 px without a line); a stretch of a fold
    showing under 2 rows outside that frame's collar is left out, so neither leaves a lone pixel beside the collar's
    points (with 3, a fold showing 2 rows between the collar and the hem's band was left out: "some seems are
    missing", walk 05). Both show in every side frame but walk 07 (42 under the collar there) and turn 04-06 (42 under
    the front collar).
  - **What a fresh review of every frame then found, fixed:** the hem's bells were placed by the swing's numbers and
    hung a row under their tips where the swing lifted a point (a gap; more of them with a bell under every point):
    each now hangs from its point's lowest pixel; points a frame apart swung into each other and their bells touched,
    so the hem's points swing together (the new front one too, and with the hem's sway); the donkey's back poked 1-2 px
    above the cloth's top behind the collar in the walk (the back line's bumps), now covered; the new part had a
    second outline against the strap's own dark edge (a seam 3 px thick in graze 02-03), now the strap's edge alone.
  - **The collar: two bold points instead of three thin ones** (fanned at 70 and 118 degrees, 8 px long, 5.5 wide):
    three thin points (4 wide, 4.5 long) hanging nearly the same way from a band about 10 px long had their bells
    touching in 36 of 38 frames, stacked into one gold bar in graze 05, and the band hid all but 2-5 px of each, so a
    point shrank to a stub or vanished as it swung (walk 08, graze 04-05). Measured over every frame's collar
    (pixels showing per point, tips' moves, bells touching): three fanned still left a point under 4 px or two bells
    touching; two bold ones show 6 px or more in every frame and no bells touch. `collar` also turns its points
    with the band where a pose turns it (the graze), swings each across itself on a pendulum's arc (straight across
    the frame, a slanted point swung out of shape in walk 02 and 06) and hangs each bell from its point's tip pixel.
    The front-view collar (turn 04-06) keeps three points, fanned (125, 90, 55 degrees, 5.5 long): its bells ran
    together in turn 04-05.
  - **A third fresh review, fixed:** the hem's points were drawn again as a triangle to each frame's swung tip, so
    they lost pixels and changed shape (dark nubs in walk 05, short in lips 06-09) and the swing's lift shortened them;
    each is now its shape at rest, shaded at rest, sheared row by row by its swing (`hem_point`), no lift, and one
    colour by its middle (the point over the cloth's split had changed colour as it swung across it). The collar's
    points swing without the lift (a bell hopped in walk 04), and the band is shaded with the points as solid under
    it (its underside shade ran as a dark bar across the navy point's top in walk 01, 07, 08 and lips 01, 11-13).
- **`stamp.py build` fills pinholes**: a pixel neither the skin nor the donkey draws, all four neighbours drawn (one
  the skin's), is filled (the donkey's darkest neighbour colour where it has one, else the skin's) and said in a note:
  the Jester's walk 03 and 07 had one under the cloth's front corner, its front collar one beside a point.
- **`stamp.py build` finds an outline left inside a piece**: in every frame, each piece's runs of two or more of a
  `[Colors]` ramp's darkest along a row or a column, with lighter shades of that ramp at both ends and that ramp
  on both sides across the run (a dark bar crossing one colour of the piece: a piece is outlined at its edge in its
  darkest, its seams and folds a shade lighter). On the Jester's broken build it lists the bar in walk 04-06 and the
  line in 35 frames; on the fixed one, nothing. A single pixel, a notch on an edge (the hem between two points) and a
  line against another colour (a point against its bell) are the drawing's own and not listed.
- **The preview page shows a study installed in the game once** (`skin_preview_html.py`): the Jester's loose copy in
  `BepInEx\plugins\gk2mounts` (put there to be seen in the game) made a second "Jester" card; an in-game skin with a
  study's mount and name is now left out and the study's card says "also in the game, a loose copy".
- **No front picture for the head frames** (`stamp.py` leaves them out, `check_skin.py` warns about them). The
  Jester's cap showed thin dark stripes at night, only while the donkey stood idle and only at some stops (the user:
  "it only happens in the idle animation, never when grazing or running and also always at night"). Measured with
  the readout below: while idle the game draws the head part (`hed_static_down`, the standing frame's own head) over
  the standing body, and the head's front picture sat 0.0003 nearer in camera depth (0.001 at the same point on the
  screen) than the body's, inside the depth buffer's step (0.004 already tied, docs/front-layer.md section 2): the
  two caps fought row by row, lit a shade apart at night (by day alike, so unseen). The walk and the graze draw no
  head part. The dragon has no head-frame front pictures, so it never had two. The body's front picture holds the
  same pixels, and the head's blink is in the eyes, not the cap: the Jester has 41 front pictures now, its other
  pictures the same.
- **The front layer readout says each layer's depth** (dev, the Tuning panel's Flying tab, "Front layer readout",
  off by default): while ridden, on the ground too, twice a second for 40 lines a part (the body's and the head's),
  `[occlusion] depths`: the camera-space depth, facing, sorting and material of the donkey's part, the skin's
  picture, its front picture and the rider's sprites, and the camera's clip planes. The Jester's cap showed thin dark
  stripes across it while ridden (the user's screenshot), two layers tying in the depth test; this said which two
  and by how much, before anything was changed (above).
- **`page_frames.py --blind`**: the rows titled A, B ... instead of the skins' names, for a fresh reviewer asked
  "which is a jester, and what tells you so?" beside a shipped skin. Six reviews of the Jester's first design had asked
  only whether its pieces slide; the first blind one found its cloth was Royal Parade's block recoloured.
- **The `[Colors]` comment `new_skin.py` and `skin_colors.py` write** says what an unnamed colour does by the skin's
  layer: kept as drawn on an over skin (docs/skins.md), the Coat on a replace skin. It said Coat for every skin.
- **The withers is behind the collar strap** (2026-11-03 night).
  - **The cause:** `body()`'s collar column is the strap's dark rear edge in all 44 frames, running from above the
    back (3C2A14, rows 39-44 standing). `rig.json` took its top as the withers, and stamp's back line took the
    strap's top as the back's, so the cloth's front column dropped 2-3 px off its neighbours in the walk and graze.
  - **The fix:** the withers is the first column behind the strap (`rig.py measure`; `rig.json`'s 41 body frames
    imported). The back line starts there.
  - **A new warning:** `stamp.py build` names a body pixel drawn on the strap, in front of the withers and above the
    back. Royal Parade's cloth runs its outline 3 px up the strap, which stands as a post wherever the strap moves
    forward.
  - No generator reads the withers; the rig page was rebuilt.
- **A one-run probe for a walled-in landing, taken out again** (2026-11-02; F11 1785/1, the same in the rerun: the yard
  test's jump at the ruins sent back to the take-off spot). The trace: the run started 1.1 east of the yard's box
  between two rock walls the test doesn't know, went over a knee-high stone and came down in a gap between the ruin
  wall, a linden's trunk and two stones; the walk-away test said walled in (4 steps of 0.25), nothing back along the
  path fitted. Its 0.25 steps with his whole body tested at each can miss a slot narrower than a step and his body
  (the gap looked 0.42 wide, his body is 0.30 deep), so it was not known whether the gap is closed. For one F11 the
  landing audit (`Mount.Instance.LandAudit`, only with `Debug.JumpTrace` on) ran the same spread at 0.10 and 0.05
  where it said walled in, and printed the game's walk grid as a map; once it had answered (closed: the next entry)
  it was taken out again, nothing of it left. docs/flying.md "The yard's ruins".
- **The yard test's runs start inside the yard, their run-up clear** (2026-11-02). The finer spreads answered the
  probe above (F11 22:03, the same jump): the gap by the ruins is closed (walled in at 0.10 and 0.05, and the game's
  walk grid shows it as an island ringed by blocked cells), so the landing rule was right and the test was not.
  `ApproachTo` (Dev/SelfTest.Yard.cs) now starts a run only inside the yard's box and sweeps his body along the run
  up to the thing's face (`RunBlockedBy`): anything that stops him on it but the thing itself rules that side out,
  and the "no side with room" line says what each side had against it. docs/flying.md "The yard's ruins" and the
  problem/test table.
- **`tools/jump-audits.py`** (2026-11-02): the jump probe's landing audits as one table (how far from the take-off,
  whether the game's walk grid held the take-off, whether the NPCs' mesh vouches, the grid's way, the verdicts), read
  only. Its first read (114 audits): the take-off off the grid in 39, the grid's cell for it 0.3-2.7 away, and the
  one landing that cell decided was the conveyor's false "no way" (docs/flying.md "The conveyor's debris").

## 0.15.0 (GK2 Donkey API 0.43.0), 2026-11-01

(The Armored skin and the skin pipeline work ship with the Donkey Skin Pack's 1.1.0, released with this; the Dragons
Mount Pack's 1.1.0 carries its translations. The Critters Mount Pack stays unreleased.)

For players:

- **The key that opens the Mount Journal is called Mount Journal** in the options (it said "Mounts panel", the
  window's old name; 2026-10-31, the open item). Its description and the Mount section's Mounts button say "Opens
  the Mount Journal." The key itself (L, cfg key MountsKey) is unchanged.
- **Translations, step 1** (2026-10-31; a player asked for an English file to translate): the options window, the
  Mount Journal and its colours window are drawn in the game's language when a translation is installed. Each mod
  ships its English as a spreadsheet file, `Languages\en.csv`, beside its DLL; a translator copies it to `de.csv`,
  `fr.csv`, ... (the game's language ids) and changes its value column. A line not translated, or whose English changed since, shows in
  English. The README's Translations says how; `[Translations] Report` (the API's cfg) writes the lines still to
  translate.
- **Translations, steps 2 and 3** (2026-11-01): the keeper's thought bubbles (why something can't be done), the
  prompts on foot ("Ride", "Hitch the cart", "Load on cart", "Take from cart", the chest's two), the crossroads
  notification (while it is still the shipped text) and the mounts' and skins' names and descriptions (the journal,
  the Skin list, the colours window's title) are translatable too: the API's in its `en.csv`, Saddle Up's Gerry in
  Saddle Up's, each pack's mounts and skins in the pack's own `Languages\en.csv`. Key names stay English.
- **French** (2026-11-01, the user: "create the french translation for the saddle / donkey api mod so we can see it
  and test it"): GK2 Saddle Up and GK2 Donkey API in French when the game is in French: the options, the Mount
  Journal and its colours window, the keeper's bubbles, the prompts on foot, the crossroads notification
  (`Languages\fr.csv` beside each DLL, 147 and 144 lines). The game's own French words where it has them (Gardien,
  corps, Carrefour, Morgue, coffre, lanterne); the cart is "charrette", the donkey "âne". The packs' mount and skin
  names in French too: each pack's own `fr.csv` (11 lines).
- **Every language the game has** (2026-11-01): German, Spanish, Brazilian Portuguese, Russian, Polish, Turkish,
  Japanese, Simplified Chinese and Korean besides French, for GK2 Saddle Up, GK2 Donkey API and the three packs, in the
  game's own words for its things (docs/localization.md section 10). Not yet read by native speakers.
- **The GK2 Saddle Up options button follows a language change at once** (2026-11-01, the user in the 0.15.0
  rehearsal): switched in the open Settings window, its words, its font and its width are the new language's on that
  frame; it kept the last language's font and width, sometimes its words, until the mouse went over it.
- **Chinese and Korean words the game's fonts can't draw are replaced** (2026-11-01, the first in-game font check):
  6 Chinese characters in 8 lines (the snail is now 巨螺, the wheel hub 轮心) and 4 Korean ones in 10 lines ("load on
  cart" is now 수레에 올리기); the rest of each line is as it was.
- **Translation files survive their translators' mistakes** (2026-11-01; the user: "expect EVERY TYPE OF STUPID
  mistake"): a file Excel saved as plain "CSV" (not UTF-8), as "Unicode Text", or opened and saved again with garbled
  letters (also Cyrillic, Polish and Turkish ones, in some lines only); a file of two encodings; a reordered header;
  a key typed wrong, or a line typed with spaces instead of commas; a stray or unclosed quote; `{ 0 }` or `｛０｝`; `fr-FR.csv`, `french.csv`,
  `fr.csv.txt`; letters lost as "?", Mac Excel's
  garbling, error cells in any language, `{{0}}`, an index column, a comma inside a text without quotes; and more
  (docs/localization.md section 9). A file must be saved as "CSV UTF-8" (or Excel's "Unicode Text"): any other
  encoding (Excel's plain "CSV", "CSV (MS-DOS)", "CSV (Macintosh)") isn't read, and the log says to save it again. What can't be read shows English and is named in the log,
  never a text that isn't in the file: a line without its English column, or with the columns swapped, shows English;
  garbled letters, a mistyped key, letters lost as "?" are left out and named, never repaired; a copy (`fr - Copy.csv`)
  is never read; a translator's out-of-date line never hides the shipped translation. A file without en.csv's header
  row, or with it translated, isn't read (the log says to copy it), and under the header a line that is neither a key's
  nor a `#` comment stops the reading there (the log names it).
- **Four sentences say it differently**, so that every language can say them (2026-11-01, the user's yes):
  - the journal's Cargo is a number ("4", or "2 of 4" while a mod limits the cart), and the Cargo cart ability reads
    "Pulls a cart (cargo 4)." (no more "1 items");
  - a mount that pulls fewer things than are on the cart: "Too much on the cart (6 of at most 4). Take some off
    first." (it said "Bella pulls 4 items at most. Take 2 off the cart first.");
  - a mount whose skins are installed but not the mount: "Skins for this mount are installed (...), but not the mount
    itself: install the mod that adds it to ride it.", for one skin or many;
  - the safe-spot key's refusal "No safe spot of this scene still holds." without the coordinates and collider names,
    which go to the log's `[safe-spot]` line.
- **Armored**, a third donkey skin in GK2 Saddle Up: Donkey Skins (2026-10-30, the pack's 1.1.0): plate barding
  drawn over the donkey (a plumed chanfron with an eye hole and a forehead spike, a chest plate, riveted flank lames
  with a brass hem over red padding, a crupper); the steel follows the Second tone, the plume and the padding the
  harness colour.

For mod authors:

- **A settings page is translatable** (2026-10-31; docs/api.md "Translations"): `Languages/<id>.csv` beside
  the DLL (or a translator's `BepInEx/plugins/<folder>/Translations/<GUID>/`), keyed by the row's cfg key without its
  mount or skin part (`.label`, `.desc`, `.choice.<i>`, `.button`), `section.<slug>`, `page.title`; looked up when
  drawn; the English passed to the page stays the source and the cfg's. No public API change.
- **A mount's and a skin's words are translatable** (2026-11-01; docs/api.md "Translations"): `mount.<mount
  id>.name`, `.description`, `.ability.<ability id>.name` and `.description`, `skin.<skin id>.name` and
  `.description`, in the `Languages/` of the mod or pack that registered them (its GUID's). Drawn in the journal, the
  Skin list and the colours window. The registered English stays the data: `MountInfo.Name`, the cfg, the logs.
- **`MountResult.Reason` stays English** (2026-11-01): the bubble shows the API's own reasons in the player's
  language, found by their English; a mod's own reason is shown as it is. Two of the API's reasons are worded
  differently (the cart overload and the safe spot, above).

Development:

- **Release 0.15.0's own** (2026-11-01): `tests/fixtures/released/0.15.0/` (the zip's first-run cfgs, every value off
  its default, and `defaults.cfg`); `tools/build-dist.sh --rebuild <DLL>` rebuilds a DLL committed in `dist/` but never
  published (`release-versions.py published <DLL>` finds the committed release zip that holds it and refuses a public
  one): `dist/` had been committed mid-pipeline, then the options button was fixed.
- **The in-game Translations runs** (2026-11-01, the user's F11; 350/10/14, then 359/3/3, then 362/0/3): the files
  scenario's keys have their dot (round 11's rule read a dotless one as none of the mod's lines); the Keeper's bubble is
  found by its text without tags (the game types a bubble in with a transparent colour tag inside it); the prompt's
  label facts are said (font, style's font, box, lines, clipping), and TextRoom judges a wrapping text that takes one
  line in a label that doesn't clip by its lines (a CJK line is taller than the game's 12-high prompt label, drawn over
  its edges: the "cut" in ja, zh_cn and ko was the measurement's).

- **Translations, step 1 built** (2026-10-31; the plan agreed with the user, docs/localization.md section 7):
  - `Texts.cs`: folders, the two readers (`key = value` with `# en:`, CSV with the separator from its header and
    the game's quoting), the language chain, the stale-English rule, the cache, the log line and the warnings, the
    test language (`Debug.TestLanguage`) and the translators' report.
  - `UiWords.cs`: the API's 60 words.
  - `MountSettingsPage.Row`: `Owner`, `TextKey`, `LabelWord`, `ButtonWord`.
  - Drawn through the lookup: the options window, the row controls, the journal and its colours window, the key
    clash lines (with "None" for an unbound key, the one key word translated: key names stay English), and the
    Settings-window button, which retitles itself on a language switch.
  - The English templates: written by a dev run to `BepInEx/GK2-l10n/<guid>/`, copied into
    `src/<Plugin>/Languages/` by `tools/l10n-sync.py` (its `--check` in `tools/release-scans.sh`), deployed by
    `DeployToGame.targets` and shipped by `tools/make-release-zip.py`. The release rule reads DLL names from `.dll`
    entries only.
  - Self-tests `TranslationTests.Files` and `.Windows`, in a group of their own, Translations (the user: not under Skins
    and effects; docs/self-tests.md). F11 runs in English with the test language off.
  - `game-code.txt`: `Texts` warned, `TranslationTests` dev.
  - After the first dev start and a review (docs/localization.md section 7):
    - the palette's names left out of the templates (310 lines down to 142);
    - context hints on the API's templates;
    - the Settings-window button retitled whenever the language changed since its drawing;
    - the game's language read in a method of its own;
    - line endings ignored by `l10n-sync.py --check`;
    - `<nobr>` taken out;
    - the Skin list's "(not installed)", "(set aside)" and "(none)" translated;
    - a key used in two sections kept apart;
    - the heading test made real;
    - the bad-template warning quiet in the test;
    - `.csv` values trimmed;
    - no zip with uncommitted translation files.
  - The long-text rule, after the first F11 of the Translations group (37/0/0): at 1.6 times only buttons and a
    dropdown's face ran short, so `UiKit.OwnButton` labels shrink to 80% before their "…". English that fits keeps
    its size. `TranslationTests.Windows` checks it: under the test language every one-line text that shrinks fits at
    its smallest size (`TextRoom.Measure` returns them).
  - One test session collects every measurement still open (the user: "collecting as much data as possible when
    testing so we don't do one thing at a time"):
    - the windows scenario measures the room in English, then in the test language, with the descriptions;
    - `TranslationTests.German` ships a German translation (a `de.csv` with a stale line, a `;`-saved `de.csv`) and
      reads the windows back;
    - `TranslationTests.Fonts` switches the game through every language (not saved, the player's own back) and
      records our windows' font against the game style's, with the characters of each language's own words
      (`GK2-l10n-fonts.tsv`);
    - a new self-test group, Measurements: `MeasureTests.LampFraming` measures the journal picture with the lamp lit,
      dark and off for every donkey skin, for the framing decision (`GK2-lamp-framing.tsv`;
      `PreviewStage.TestDrawnRect`, `Lantern.Rig.LampRenderers`). After its first run it also photographs without the
      glow alone (`TestHide.Glow`) and reports the lamp's parts (`TestLampReport`): the noon stage kept the glow lit
      for "dark", and Hellfire drew a lamp part while the stage said no lamp.
  - The font our windows read: the Settings row's text style is applied for the language now before its font is
    copied (the game restyles only open windows on a language switch, so a closed Settings window could hold the
    last language's font).
- **Review rounds** (2026-11-01; docs/localization.md section 11): every language proofread until two rounds in a row
  changed nothing; an independent code review of the reader in rounds (18 findings, then 14, 19, 12, 6, 15, 9, 7, 12,
  6 and 10; the user ended them after the eleventh) and its fixes, each first a failing catalogue case (63 to 231); under
  a header every line is one of the mod's, a `#` comment or blank (any other line stops the reading, said; a key damaged
  by one keystroke or a row cut by a typed line break costs its own line); a file is read from en.csv's header row on (a
  file without one isn't read: read by its columns' places, a translation tool's context column showed as the
  translation), and a section pasted under a header in other words isn't read either; a file with its english column before the translation is
  refused (a comma typed into a text there can't be told) and a row with text past its header's columns left out; a
  header's columns matched by en.csv's own names only (a notes column named "Notes (translator)" was read as the
  translation): a header in other words, or naming a column otherwise, isn't read (said: copy en.csv's header row); a table is read again when its owner's English grows (a
  mount or skin registered late: `Texts.EnglishStamp`, `Looks.Count`); every remaining way to a wrong text was a repair of the
  translator's file, and the repairs are off behind four switches kept compiled for the user's decision
  (`TranslationFiles.ReadCodePages` and `RepairGarbled`, `Texts.RescueByEnglish` and `JoinSeparator`; the 17 generated
  pages stay; docs/localization.md section 11); and
  `tests/TranslationScan` sweeps every shipped file, every shipped line alone and every UTF-8 file with one line in a
  code page, saved every wrong way (including Excel in a `;` country, in one cell and as Excel splits and pads it), every
  shipped row with one edit (no other key may fall to English), every file's header renamed, reordered and added to
  (some 123,000 reads in all), and with
  `GK2_L10N_CORPUS` the game's own tables (85 reads of 9,473 lines, checked against a text made without the mod's
  cleaning, a line left out unsaid failing): never a text the file hasn't; reading a file measured (its timing line): the repair's tries through every page threw
  in the strict UTF-8 decoder for each correct line (7.3 ms for the slowest file on .NET, more on Mono, in the frame a
  language is read ahead); UTF-8 is checked without a throw now (1.7 ms; all 55 files 29 ms); the Layout scenario measures every language in its own font (every options
  section, every description-box text, the Defaults question, the journal and the colours window of every mount), and
  a new scenario the game's own thought bubble and prompt on foot with each language's longest sentence and prompt;
  `l10n-sync.py` reads a file as the mod does and counts lines still the English apart; `tests/TranslationScan` refuses
  a stale DLL; both fail on a shipped line the mod would leave out; `SelfTest.Drive` disposes the scenarios it unwinds (their cleanup ran only on success).
- **The nine other languages** (2026-11-01; docs/localization.md section 10): written by one agent a language through a
  validating writer (every key, every `{n}`, the measured room, wide characters counted double), the game's own terms
  looked up in its strings tables; 50 files, `tests/TranslationScan` and `l10n-sync.py --check` clean. The Fonts self-test
  also checks every character of our translations in each language's font (zh_cn and ko use some the game's own text
  doesn't).
- **Translator mistakes** (2026-11-01; docs/localization.md section 9): `TranslationFiles.cs` finds, decodes, parses
  and cleans a translation file forgivingly; `LegacyText.cs` holds the Windows code pages 1252, 1250, 1251, 1254
  (written by `tools/l10n-codepages.py` from Python's codecs; the game's Mono has no I18N assemblies); `Texts.Load`
  and `Take` check each file against the owner's English (a key found again by its English; a line without its English
  left out; swapped columns or the english column translated too said, never repaired); keys case-insensitive;
  `Texts.Fill`: a translation that lost a `{n}` shows the English (`Word.MayDropValues` for the two noun sentences);
  warnings at most 10 a file; the load can't throw. The catalogue of 231 mistakes is `TranslationTests.Mistakes` (the
  Translations group) and
  `tests/TranslationScan` (offline, in `tools/release-scans.sh`; also reads every shipped `Languages/*.csv`, no warning
  allowed; `InternalsVisibleTo("TranslationScan")`). A game in `pt` reads `pt-br.csv` after its own `pt.csv`.
  - The side effects: `Texts.TickWarm` reads the files ahead (one mod a frame, a game's start and a language change);
    the journal closes when the language changes under it; the named prompt is made once per name and language.
- **The French translation and its check** (2026-11-01): `src/GK2DonkeyAPI/Languages/fr.csv` and
  `src/SaddleUp/Languages/fr.csv`, written from each `en.csv` (its key, english and note columns as they are), every
  line's `{0}` kept and the tight texts within their measured room (the lantern's "Warm (game default)" 1.9x, "Mount
  Behaviour" and "Drive the cart" 2.1x: `GK2-text-room.tsv`). `tools/l10n-sync.py` reports every shipped translation
  against the English (translated, missing, English changed since, keys gone) and its `--check` fails on a line whose
  `{0}`, `{1}` don't match the English's (the mod would show the English); French: 147 of 147 and 144 of 144.
- **Translations: CSV only** (2026-11-01; the user: "scrap the txt files and lets stick to csv only. less to
  maintain."): `Texts` reads `<id>.csv` alone (the `.txt` reader, its `# en:` comments and `\n` escapes gone); the
  English template is `en.csv` alone, with a fourth column, `note` (the context the `.txt`'s comments carried: where a
  text shows, what its `{0}` is; the reader ignores it); the translators' report is `<id>.missing.csv` in the same
  columns. `DeployToGame.targets`, `make-release-zip.py`, `release-versions.py` and `l10n-sync.py` (which names a
  `.txt` left in a `Languages/` folder as not read) take `.csv` only. The repository's `en.csv` were made from the
  `en.txt` beside them before those went (the next dev start's `--check` compares with the code's). Self-tests: the
  files' fixtures are three CSVs (commas with the note column, tabs, `;` with a BOM) and the API's template copied as a
  `de.csv` reads back as a whole translation; the German, bubbles and names tests write `de.csv`.
- **Translations, steps 2 and 3 built** (2026-11-01; the user: "lets do a lot so we get this translation finished
  and ready"; docs/localization.md section 8):
  - `UiWords`: `Term` (owner, key, English) under `Word`; the keeper's 71 fixed sentences and 6 templates
    (`say.*`), 8 prompts (`prompt.*`) and the crossroads notice. The code returns a word's English
    (`UiWords.SayCartFull.English`) and `Word.Make(args)` for a template, which remembers the last 8 sentences it
    made so the bubble finds them; a `Term` value goes into the reason in English and into the bubble translated.
  - `Mount.Say` shows `UiWords.Shown(text)`. `Mount.Instance.NotTheCartIndoors` is a property,
    `WhyDefinitionTouchesTheSave` returns a word, `Looks.Who` the mount's name as a `Term`.
  - `Catalog.NameTerm`, `ShownDescription`, `AbilityName`, `AbilityDescription` and `Looks.NameTerm`,
    `ShownDescription`, drawn in the journal (list, header, description, abilities, waiting skins), the colours
    window's title and the Skin list (`Texts.Choice`).
  - `Texts.EnglishOf` lists an owner's mounts and skins (a mount's own look's name left out: it shows as "Default");
    `Texts.Owners()` (the pages', the API's, every loaded plugin that registered a mount or a skin) and the dev run
    writes each one's English; the file's header names a pack by its plugin name.
  - The babies slider's "None" is the API's word; `Corpses.NotificationShown`.
  - `tools/l10n-sync.py --check` fails on a bubble or a prompt said with a literal in the API's game code.
  - Self-tests `TranslationTests.Bubbles` and `.PackNames` (the Translations group); `.Windows` checks every mount's
    name, description and abilities in the journal's entries; the cargo checks (`SelfTest.Speeds`, `.Freeplay`)
    read the number.
  - A comment in `PreviewStage` named a pack's skin (the layering scan): reworded.
  - After the first run (2026-11-01, 98/0/0): the English templates synced (the API 147 lines, Saddle Up 144, and the
    packs' first, `packs/<Pack>/Languages/`); a skin's description goes into the English file only where a window
    draws it (its mount has none of its own: 8 lines no one would ever see left out); `TranslationTests.PackNames`
    puts the player's chosen mount back in its finally; `Term.Format` translates a `Term` value too.
  - The lamp framing probe writes the stage's state with each measurement (`PreviewStage.TestCartState`): Hellfire's
    picture keeps its no-lamp framing while the lamp draws, and the next run says why. It also records the stage at
    the moment it used to measure, then waits (10 s at most) until the look is worn and framed for what the stage
    draws, counting the frames, and measures after (`Looks.Rig.TestSettleReport`, `PreviewStage.TestShownSettling`):
    the probe moved on after 6 frames, and a look still building keeps its last photo. Its run (00:30) settled it:
    Hellfire's sheet was still building (22 frames), and once worn its picture was framed for the lamp like the others;
    no change to the stage.
  - `tools/release-versions.py check` notes a kept item (a released build) whose `Languages/` differs from its released
    zip's: a translation added between releases ships only with a raised version (docs/release-checklist.md).

- **`docs/localization.md`: translating Saddle Up, investigated** (2026-10-31, two passes; a player asked for an
  English file others can translate; the user: "investigate the decisions we need to make and the findings"): the
  game's 11 languages (read out of `resources.assets`: 11 tables of 9,474 lines; fonts of their own only for ja,
  zh_cn, ko) and its community language packs (always a new language); an inventory of what a player sees (234
  different texts in normal play: Saddle Up 102, the API 126 in 22 files, the packs 6); the findings checked (plugins
  start before the game loads its language, so text is looked up when drawn; our windows rebuild on open; how much
  longer the game's own labels run per language; 60 glued sentences; words that are also data; the cfg stays English
  by itself; the Workshop copier carries `.csv` files); nine decisions with the evidence and a recommendation each.
  Decided by the user: our own files (D1) and translations both bundled and translators' own (D3). For the format (D2)
  the user asked for more data. Four formats were put through translator mistakes with each one's reader (the game's
  CSV reader ported from the decompile, the game's own Newtonsoft.Json):
  - one stray quote cost CSV 9 of 12 lines;
  - European Excel's `;` cost CSV everything;
  - JSON refused the whole file at any quote or comma slip;
  - `key = value` lost at most one line.

  The recommendation is revised to `key = value` with the English as a comment, CSV also accepted.

  Three other GK2 mods were read (decompiled), for how they translate:
  - WeeklyOverview has its 10 translations typed into its code, with a font check for Japanese, Korean and Chinese;
  - Work Faster and Keeper's Longer Days keep JSON files in the GK2 Mod Framework's folder and need the Framework.

  None gives translators a file that works without the Framework.

  How often our English changes per public release, read from git: small releases 0 to 16 lines, feature releases 20
  to 90. 0.12.0 changed 22 rows' English and moved 22 rows to other sections, so translation keys leave the section
  out; the 46 keyed rows are unique. 20 real lines were written in each of the four formats for the user to compare,
  and the format choice is framed by how the first translator works. Nothing built.
- **Text room in the self-tests** (2026-10-31, the translation investigation's room finding): `Dev/TextRoom.cs`
  measures, where the self-tests already show them, every text of the options window (each section, and every row's
  description in the description line), the journal (per mount) and the colours window. The measure is how many times
  longer a text can get and still fit its box: TMP lays out the text grown with its own words, at full size and, for a
  text that shrinks, at its smallest. It writes `BepInEx\GK2-text-room.tsv` and an INFO line per window. It only
  measures (no check, no change), through two test-only accessors (`OptionsWindow.TestRoot` / `TestDescriptionBox`,
  `MountsPanel.TestRoot`).
- **Full-size pictures for the Workshop galleries** (2026-10-31; the user: the gallery blows the 560-wide pictures
  up): `tools/workshop-gallery.py` cuts a box out of a Steam screenshot at its own pixels into
  `media/workshop/<Item>/gallery/` (a JPEG under 1 MB). Every gallery picture's screenshot was found again in the
  game's Steam screenshots (the packs' by their README boxes, the four older Saddle Up ones by matching their first
  commit), and 17 were cut: the three items' gallery pictures plus `colours-dragon`, `options-window` and
  `settings-button`; `media/README.md` lists each one's screenshot and box.
- **The Mount Journal and the Colours window on the Saddle Up pages** (2026-10-31; the user: the colours editor
  picture is outdated): two new screenshots of the Default donkey, cut with the packs' boxes, `media/journal-donkey.png`
  and `media/colours-donkey.png`, take the old editor's place in the Nexus description, and their full-size cuts its
  place in the Workshop gallery (the gallery folder's `Donkey-Cart-Colors-Editor.jpg` removed). The Workshop
  description never showed it. `media/Donkey-Cart-Colors-Editor.png` stays while the live Nexus page does.
- **GIFs for the 2 MB gallery** (2026-10-31): three page GIFs squeezed under Steam's 2 MB gallery limit with little
  loss (`donkey-comes` and `riding-cart` by gifsicle's lossy mode, the colours cycle at 128 colours and two sets of
  three) into `media/workshop/GK2SaddleUp/gallery/`; the rest are 3-10x over even so and stay in the descriptions
  only. `media/README.md` records how each was made.
- **Square Workshop thumbnails** (2026-10-31, before the items went public; the user: the 16:9 previews had bars in
  Steam's square tiles): the three `media/workshop/<Item>/Thumbnail.jpg` are square cuts of the original screenshots
  (1024, the skin pack's 900 at its own pixels; the packs' were 560-wide page pictures scaled up), restaged into
  `workshop/<Item>/` with `--no-zip` (zips untouched). `tools/workshop-thumbnail.py`: 1024 by default, says when a
  picture is not square, and `--width` works (its value was taken for a third argument).
- **The packs' "The Saddle Up mod" section** (2026-10-31; the user): the Dragons and Donkey Skin pages, Workshop and
  Nexus, say the pack needs GK2 Saddle Up and that Saddle Up gives you the donkey mount, and show `riding-cart.gif`
  there; both Workshop folders restaged (`--no-zip`).
- **Settings pictures on the Saddle Up Workshop page** (2026-10-31; the user): `settings-button.png` and
  `options-window.png` under Settings, as on Nexus, paid for by the Install section's first line (step 1 says the
  same: nothing to copy, no BepInEx to install); 7,927 of Steam's 8,000 bytes, restaged (`--no-zip`).
- **A picture per pack on the Saddle Up Workshop page** (2026-10-31; the user): the "Mount and Skin Packs" section
  shows `dragon-flying.gif` and `royal-parade-riding.png` as the Nexus page does (two paragraphs instead of the
  list), paid for by text the page says twice: Gerry's "the journal is also where..." (the L key line), the
  Unsubscribe paragraph's "Your saves still load without it" (Safe for your save), "Gerry the skull" and "subscribed
  like this mod" in the packs intro; 7,971 of 8,000 bytes, restaged (`--no-zip`).
- **`docs/nexus/saddleup-description.txt` deleted** (2026-10-31; the user): the 2026-10-14 draft of the Saddle Up
  page, stale since the page texts moved to `media/` (it still said game v1.010 and promised a separate DonkeyAPI
  page); `docs/nexus/README.md` names the three `media/*_Nexus.txt` texts instead, and the done "Publishing (Nexus
  Mods)" item left `docs/open-items.md`.
- **Steam's description limit is a hard one** (2026-10-31; docs/mods-window-and-workshop.md section 12): the Saddle Up
  Workshop text with everything cut for the limit put back (11,064 characters) failed the whole upload
  (`k_EResultInvalidParam` at submit), where the 7,880-character text went up. `tools/make-release-zip.py` refuses a
  text over 8,000 UTF-8 bytes now, before it clears the Workshop folder, instead of a note.
- **Workshop change notes: player notes only, unwrapped** (2026-10-31, before the items went public): the packs'
  1.0.0 notes on Steam came from their developer changelogs (source folders, zip names, "(the user, ...)");
  `tools/make-release-zip.py` stops now when an item has no player note (`docs/nexus/donkey-skin-pack-1.0.0-workshop-changenote.txt`
  written), and joins each paragraph and bullet into one line, since Steam shows every line break and the notes'
  wrapping broke each bullet mid-sentence. The page texts and the 0.14.0 notes say Mount Journal (the window's
  title) and Send your mount away (the option's name).
- **The rehearsal skips a pack that was never released** (2026-10-30, at 0.14.0): `tools/rehearsal.sh stage-a` stopped
  (set -e) on the missing Critters zip after the dev setup had gone aside; a pack zip not in `release/` is now left out, said.
- **The skin pipeline, one step easier per skin** (2026-10-30, with Armored; the user: "refine and standardize our
  skin creation pipeline so that it gets easier with every skin"): the donkey's anatomy that every skin drawn over it
  needs is one shared module, `tools/skins/donkey/` (`anatomy.py`: `body()` with the hip, collar and back line,
  `belly()`, `head_delta()`, `top_outline()`, `run_bottom()`, the measured `anchors.json` and `horn_bases.json` with
  their scripts, `contact.py`, `zoom.py`, all moved out of `tools/skins/royal-parade/`; Royal Parade regenerates
  byte-identical through it); `tools/skins/handover.py <skin> [--ref <picture>]` renders the hand-over pictures of
  step 5 (the poses at 1x, 3x and 8x with and without the rider, the reference under them) for any skin in one
  command; `review.motion_check()` reports every piece's box against its anchor per frame (the "one piece with the
  body" measure of docs/front-layer.md section 5) and `review.phase_check()` fails a repeating pattern (rivets, a
  checkered hem) that repeats from the canvas instead of the piece's anchor (it crawls along the piece as the anchor
  shifts between frames; Armored's first cut did), both run by the Armored generator; the scaffold's sidecar
  (`<skin>.gk2export.txt`, which carries this machine's paths) is git-ignored. docs/sprite-tools.md "Making a skin".

## 0.14.0 (GK2 Donkey API 0.42.0), 2026-10-30

For players:

- **Saddle Up's own options window** (2026-10-30, GK2 Donkey API 0.42.0; the user: GK2 Mod Framework isn't on the
  Steam Workshop, so every player gets the same options): the game's **Settings** window (in the pause menu and the
  main menu) has a **GK2 Saddle Up options** button, and the Mounts panel an **Options** button. The sections are on
  the left, their options on the right, and a line at the bottom says what the option under the mouse (or the
  gamepad's focus) does. Every option has a control of its own: switches, number buttons, sliders, dropdowns, and key
  boxes: click one and press the key, with Shift, Ctrl or Alt, or one of those on its own (Climb's Left Shift and
  Descend's Left Ctrl can be set now); Backspace sets no key, Esc cancels, and a key another option, another mod or
  the game already uses is said under it. **Defaults** sets the section shown back to its defaults, after asking. The
  Mounts button opens the Mounts panel over the options, and Escape steps back one window at a time. The journal's
  buttons line up with the mount list and its picture: Options under the list, Customize under the picture, Drive the
  cart and Ride on the right; the journal's Done button is gone (its X and Escape close it, keeping what was chosen).
  The buttons that act on your mount or the keeper work in a game only (greyed in the main menu). With GK2 Mod
  Framework installed, Saddle Up's **Settings** button in its Mods menu opens the same window (the framework's own
  page of every setting is gone). The "Showing" pages of the game's Settings window are gone too; your settings are
  kept as they were (the same cfg, the same lines).
- **Two keys on one key** (2026-10-30, GK2 Donkey API): of two of your keys on the same key, the more specific acts
  alone: with Send your mount away on Ctrl + G, Ctrl + G no longer drives the cart too.
- **Typing in another text box no longer presses the mod's keys** (2026-10-30, GK2 Donkey API): a letter typed in
  a text box of another mod or of the game (e.g. a search box on the Tech Tree) was also read as the key on that
  letter, so typing an L opened the Mounts panel. The keys only read the keyboard while no text box has the cursor,
  as they already did for the Mounts panel's own boxes.
- **The Mounts panel over any game window** (2026-10-30, GK2 Donkey API): opened over the Character window (the
  Tech Tree, the map, the quests), the inventory, a chest or most other game windows, the panel left that window
  running under it, and Ride or Drive the cart left it open. It is now set aside as the pause menu always was:
  Escape brings it back as it was, Ride and Drive the cart close it.

For mod authors:

- **A settings page shows in the options window** (GK2 Donkey API 0.42.0): `MountSettingsPage` is unchanged (every
  method as before), but its rows that aren't look rows go into the API's options window, opened from a button
  named after the page's title in the game's Settings window and from the Mounts panel. Key rows are set there
  (they showed read-only in the Settings window), text rows are text boxes, info rows read again, a `Heading` over
  plain rows is a heading. A `Preview` row is no longer drawn (the Mounts panel shows the mount); calling it still
  sets the panel's companion. The "Setting a key" note row is gone. Two key rows with the same key, either with a
  `pressed:` action, are marked as a clash. With GK2 Mod Framework installed, the page's entry in its Mods menu has
  one setting, a button opening the options window, and its Settings button opens the window straight away (a
  Harmony prefix on the framework's private `ModsMenuWindow.OpenSettingsPage(RegisteredMod)`, found by its exact
  signature; without it, the one button). The API keeps its own entry (no settings).

Development:

- **The options window and the parts it shares with the Mounts panel** (2026-10-30; docs/options-window.md): the
  user, "no compromise on controls" and no partial classes. `OptionsWindow` (the window), `SettingsButtons` (its
  button in the game's Settings window, a copy of the window's OK button as the game's own Mods window makes its
  buttons), `RowControls` (every row control, one instance per window, with the new key box, button row and info
  line), `KeyCapture` (setting a key, a lone modifier included), `UiKit` (the game's look and the window pieces),
  `GameWindowAside` (the game window under ours, the controls, one Escape a frame). The Mounts panel uses them
  (`MountsPanel.Controls.cs`, a partial, is gone); its journal, colours window and way back work as before.
  `FrameworkLink` now registers the one-button entry and points the Settings button at the window. Gone:
  `MenuLabels`, `ModsWindow`, `LookPreview`, `SettingsRows`. Self-tests: "settings: the options window" (every
  option our own control in
  its section, a switch, a key set and in words and a clash, the line at the bottom, Defaults asked; with the
  framework installed, the page in its Mods menu and its Settings button pointed at the window) and "the
  options window's way back" (the Settings window's button, the Mounts button and back, the journal's Options
  button and back, Ride from the journal over them).
- **The options among other mods** (2026-10-30; the user: "hundreds of other mods out there ... the better can
  reinforce our code"; docs/options-window.md §12): the Settings window's OK button is copied from the window's own
  field, not the first button found, and the copy is cleaned of other mods' components before it wakes; a watchdog
  adds the button again while the Settings window is on top (another mod's handler that throws stops the game's
  window event before ours); a key row names a key another mod's cfg or the game's own bindings already use; a game
  window opened over ours makes the options window and the Mounts panel step aside until it closes. Self-test "the
  options window among other mods".
- **Keys that overlap** (2026-10-30; docs/options-window.md §14): our keys ignore keys held with theirs (walking
  keys, the climb's Shift), so one press could do two of them: of two of our keys on one main key, the more
  specific now acts alone (Ctrl + G over G: Ctrl + G no longer also drives the cart); and a key row says when
  another key overlaps it, "Clashes with X (Ctrl + G)": a key of ours on a lone Shift, Ctrl or Alt that goes off on
  the way into another, another mod's combination that does ours too, the game's key held with more.
- **Self-tests for both findings of 2026-10-30** (the user: "make sure we have tests written for these problems"):
  "keys: the mods' keys wait while a text box of any window has the cursor" (a TextMeshPro box, a Unity UI box, a
  selected button; through `KeyActions.WaitReason`, the one check the key poll makes), and a third part of the
  panel's way back, over the Character window on the Tech Tree (it caught the window-under gap: 2 FAILs before the
  fix).
- **The release zips are in git** (2026-10-30): `release/*.zip` is tracked, only `release/logs/` is ignored. While the
  folder was ignored, the old zip tool overwrote the public 0.13.1, Dragons Mount Pack 1.0.0 and Donkey Skin Pack
  1.0.0 zips in place and the rebuilt copies were deleted; 0.13.1 was regenerated byte-identical to the Nexus file,
  the two pack zips come back from their Nexus downloads.
- **One command for a release** (2026-10-30, after 0.13.2): `bash tools/release.sh` runs the version check, the
  release items, the dist build, the scans and the zips in order and stops at the first failure. The release rule
  (`tools/release-versions.py items`): a DLL whose version is not raised above the one committed in `dist/` keeps its
  committed bytes (`build-dist.sh` restores them) and gets no zip (`make-release-zip.py` picks the raised items
  itself); a raised pack needs its `docs/nexus/<name>-<version>-changelog.md`. This replaces the hand steps of
  0.13.2 (a `git checkout` of the unreleased pack DLLs, a hand-picked `--only`).

## 0.13.2 (GK2 Donkey API 0.41.2), 2026-10-30

For players:

- **On the Steam Workshop** (2026-10-30; `docs/mods-window-and-workshop.md`): GK2 Saddle Up, the Dragons Mount Pack
  and the Donkey Skin Pack are Workshop items as well; subscribed, the game installs and updates them and its
  main-menu Mods window lists them with their version and an Enable/Disable switch (from the item's `modinfo.json`:
  id `dreas.gk2.saddleup`, name, version). The zip is the same plugin files and installs into the game folder as
  always, without `modinfo.json` (nothing reads it there); installed that way the mod is not listed in the game's
  Mods window, which shows only what the game installed itself. The
  game's `Mods\Code` folder, which its README_MODDING.txt calls "Testing locally", is left out of the player texts.
- **A mount's own getting-on and getting-off sounds play whole** (2026-10-30): the donkey's snorts start 0.42 s in
  (their silence skipped), and a pack's own clip for the event inherited that start. The rat's and the snail's
  squeaks are shorter than 0.42 s, so every ride-style mount and dismount seeked past their end and Unity logged
  "an invalid seek position" in Player.log (14 lines in a self-test run; no exception, the clip still played); the
  dragon's growl lost its first 0.42 s. A layer's own sound now starts at 0 unless its line says `start`, and a start
  at or past a clip's end is dropped with one warning. New self-test "a pack's own clip and the base's start offset"
  reads the Unity log back while the clips play.
- **Saddle Up's "Allow mounting with the cart indoors" row** (and the API's `DismountIndoors` text) no longer says
  the cart can get stuck on furniture: it has no collider indoors, it rolls through furniture and door frames, which
  only looks odd (the user, 2026-10-30).
- **GK2 Saddle Up: Dragons Mount Pack 1.0.1** ships alongside: its plugin's name (the log, the mod lists) is the
  public one and its zip carries `modinfo.json`; nothing else changed in it. The Donkey Skin Pack stays 1.0.0
  (Armored is in the next one).

Development:

- **The packs' plugin names are their public names** (2026-10-30, with each pack's next version): "GK2 Saddle Up:
  Dragons Mount Pack", "GK2 Saddle Up: Critters Mount Pack", "GK2 Saddle Up: Donkey Skin Pack" in `[BepInPlugin]`,
  the log and the framework's Mods menu, the same as the zips' `modinfo.json` gives the game's Mods window; the ids
  are unchanged. (The user, on the Dragon tile: "the name is actually Dragons Mount Pack, not Dragon".)
- **One item folder for Nexus and the Steam Workshop** (2026-10-30; `docs/mods-window-and-workshop.md`, with a lab run
  of the game's own sync library on our zips): `tools/make-release-zip.py` writes each download as the game's code-mod
  contract wants it (`modinfo.json` + `BepInEx/plugins/<Mod>/<Mod>.dll`), the zip for Nexus and the same bytes as
  `workshop/<Item>/` (git-ignored) for the game's Shift+F11 Workshop creator, with `Thumbnail.jpg`
  (`media/workshop/<Item>/`, made by `tools/workshop-thumbnail.py`), `_description.bbcode` (the new
  `media/..._Workshop.txt` texts in Steam's BBCode, drafted from the Nexus ones with a subscription Install section and
  `{{ WORKSHOP_URL_* }}` placeholders until the items exist) and `_changenote.txt` (the changelog's "For players"
  entry). `--only saddleup` writes one item (a public pack version is never rebuilt). Decided once, for both
  channels: the Saddle Up item carries the API (bundle); the game deletes a file two items share when either goes.
  Since then: `make-release-zip.py` refuses to overwrite a zip that exists in `release/` (the public 0.13.1 zip was
  overwritten in place and regenerated that afternoon; a released version is never rebuilt), `--no-zip` stages an
  item without a zip; `tools/release-versions.py check` (run first by the zip tool) verifies every line that repeats a
  version number and `set` moves the mechanical ones; `tools/rehearsal.sh stage-pack` / `restore-pack` / `stage-aside`
  rehearse the Workshop content as a local pack or a subscription (report sections 11 and 12: both passed in game,
  and the three Unlisted Steam items exist).

## 0.13.1 (GK2 Donkey API 0.41.1), 2026-10-30

For players:

- **Audited against game 1.011** (Steam build 26297960, 2026-10-30; `docs/game-update-26297960.md`): 11 of the 839
  game members the API uses changed, none went missing, nothing in the code had to change; the deployed 0.41.0
  stopped itself on 1.011 (three core patch classes touch changed members) until this rebuild, so this
  release is the one 1.011 players need. The game now ships BepInEx and a Workshop sync patcher, and has a
  building move of its own; the report says what that means for the zips and the Nexus install steps. No code
  changed: 0.13.1 / 0.41.1 are 0.13.0 / 0.41.0 rebuilt with the 1.011 manifest. Move Building (the QOL repo) is
  retired: the game moves buildings itself now.

Development:

- **The pack zips hold the DLL alone** (2026-10-29): `tools/make-release-zip.py` no longer puts a pack's README.md
  and CHANGELOG.md beside its DLL (nothing reads them in the game; Saddle Up's zip never carried its own). The packs'
  1.0.0 zips of 2026-10-28 were the ones with them. `check-dist.py` still wants both files in the pack's source folder.

## 0.13.0 (GK2 Donkey API 0.41.0), 2026-10-29

For players:

- **Colours per skin** (2026-10-28, the day the Donkey Skin Pack went out): everything in the Colours window is kept
  for the skin you are customizing, not for the mount: coat, harness, second tone, the cart's paint, wheels, hubs,
  the lantern's light and whether the lantern shows and stays lit by day. A fiery skin keeps its reds, the plain
  donkey its brown, and switching skins brings each one's colours back. The window's title names the skin. What you
  had set in 0.12 goes to the skin you were wearing (the mount's other skins start at the game's defaults).
- **The Mount Journal's picture is centred whether or not the lantern shows** (2026-10-29): the picture's framing is
  kept per look and was taken with the lamp on its pole; shown without the lamp (its toggle off, or where the lamp
  isn't allowed) the mount sat low and left of the middle. The framing knows about the lamp now, and the picture
  shows the lantern setting of the skin you are looking at, not of the one you wear.

For mod authors:

- **API 0.41.0 (breaking): looks per skin.** `MountLook` is one skin's (`SkinId`): the colours and the lantern toggles;
  `MountCompanion.LookOf(skinId)`. The mount's own (`Name`, the babies, the volumes) is `MountOwn`, `OwnOf(mountId)`.
  `MountSettingsPage.PerSkin` builds a row set per skin (a mount's first skin on the mount's keys, the others on
  `<key>_<skin id>`), `SkinHasRegion` for a row's `EnableWhen`, `Row.SkinId`; `CatchUp` adds a late skin's rows; a 0.12
  file's per-mount values move to the skin the mount wore. The companion's pass-through properties are unchanged.
  docs/per-skin-looks.md, docs/api.md.

Development:

- Self-test "looks per skin" (group Skins and effects); `tools/page-carryover.py` knows the shipped skins' key sets;
  the API surface snapshot regenerated for 0.41.0.

## 0.12.0 (GK2 Donkey API 0.40.0), 2026-10-28

For players:

**The mounts are here.** 0.10.0 had them built in but switched off; this version switches them on. In short:

- **Six mounts to ride**, picked in the Mounts panel (**L**, or the Mount section's Mounts button):

  | Mount | Download | Riding | Cart | Its thing |
  |---|---|---|---|---|
  | Donkey | GK2 Saddle Up | Normal | 6 items | the all-rounder; skins Hellfire and Royal Parade (GK2 Saddle Up: Donkey Skins) |
  | Gerry | GK2 Saddle Up | Fast | 4 items | the game's talking skull, three times his size; he talks |
  | Rat | GK2 Saddle Up: Critters | Very fast | 2 items | the courier: quick, a light cart |
  | Worm | GK2 Saddle Up: Critters | Slow | 4 items | a quiet hauler |
  | Snail | GK2 Saddle Up: Critters | Very slow | 6 items | a slow, full cart |
  | Dragon | GK2 Saddle Up: Dragon | Slow | no cart | **flies**: over water, fences, cliffs and roofs |

- **Each mount pulls its own load** (the user, 2026-10-27): the faster it is, the less its cart holds. A cart holds
  as many items as its mount carries. Switching to a mount that pulls fewer than the cart holds leaves the cart
  standing with its load, and it can't be hitched until you take the extra off ("Gerry pulls 4 items at most. Take 2
  off the cart first."). The cart key still brings it over to your mount, unhitched, so you can unload it there. The Mounts panel shows each mount's speed and cargo.
- **Skins:** the donkey has Hellfire (horns, a devil's tail, glowing eyes, lava cracks; its ring of fire comes in a
  later update) and Royal Parade (a saddle cloth and a crown; the cloth follows your harness colour), a download of
  their own: **GK2 Saddle Up: Donkey Skins**. Other mods and
  skin packs can add mounts and skins (a folder `gk2mounts\<mount>\` with a `mount.cfg` and `skins\<skin>\`;
  docs/making-a-mount.md).
- **Everything is kept per mount:** its colours (coat, harness, second tone, the cart's paint, wheels, hubs and
  lantern), its name, its babies and its voice, all set in the Mounts panel. Your 0.10.0 donkey keeps everything it
  had.
- **Babies look like their parent:** a dragon's young are little dragons, Gerry's are skulls their own size, each
  baby a little different from its siblings.
- **Every mount jumps** (Space); the dragon takes off with a second press in the air. Not in a fight, where Space is
  your attack. The silent ones (the snail, the worm) are silent by choice; the dragon's, the rat's and Gerry's own
  get-on, get-off and answer sounds come later.

Everything else in this version, in detail:

- **The donkey's skins are a download of their own** (the user, 2026-10-27): Hellfire and Royal Parade are **GK2 Saddle
  Up: Donkey Skins** (`GK2SaddleUpDonkeySkinPack.dll` in `BepInEx\plugins\GK2SaddleUpDonkeySkinPack\`, the zip
  `GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip`), a pack like the Dragon and the Critters, which needs GK2 Saddle Up. Saddle Up
  itself brings the donkey and Gerry. Every pack is its own download: there is no "All Mounts" zip. Without the pack, a
  donkey that wore one of its skins shows the game's donkey, and the choice comes back with the pack. (The skins were
  inside Saddle Up only in versions never released, so nothing a player has changes.)
- **Freeplay, a section at the end of the settings** (the user, 2026-10-27: for players who don't want the mounts'
  differences): "All mounts carry max cargo" (every cart holds 6 items, whatever mount pulls it) and "All mounts have
  the same speed" (every mount, the donkey too, at the speed you pick: Very slow, Slow, Normal, Fast or Very fast, the
  Mounts panel's own words; Normal is the donkey's). Both off by default, both take effect at once, even while you
  ride, and the Mounts panel's cards show what your mounts carry and how fast they go with them on.
- **The worm is a touch slower** (1.6 times walking speed, was 1.75): every mount's speed is now one of five set
  speeds, the ones the Mounts panel names (the worm was already called Slow, and Slow is the dragon's 1.6).
- **A mount or skin whose pack is gone is listed by its name** (2026-10-27; seen with the Donkey Skins pack removed): the
  Skin and Mount dropdowns show "Hellfire (not installed)" for a choice nothing installed provides, not its id
  ("donkey/saddleup-do..." cut short in the journal). The choice is kept and comes back with the pack, as before.
- **A parked mount leaves its cart as soon as your choice says so** (2026-10-27): choose a mount that can't pull the cart
  (the dragon) while your mount stands parked with its cart far away, and the cart is left standing there at once,
  where the mount stands. Before, the mount kept the cart until you next came near, and changed in front of you.
- **No cut at a jump's touch-down any more** (2026-10-27; the self-test caught it on a hillside): landing from a running
  jump, the mount and rider could be drawn a step ahead for one frame and pulled back the next, up to about 5 px
  along and 3.5 px up a slope, on roughly a third of landings. The body was put down where it already was, and the
  game's put teleports the drawn body past its smoothing. A put within a hair of where the body is no longer happens.
- **The babies react, not mirror** (the user, 2026-10-27): when you jump on their parent they jump a quarter of a second
  to about a second later (the lively ones sooner), never in the same moment; and when it calls, each baby answers by
  its own chance (the lively more often), so it isn't always the whole family.
- **A mount picked in the Mount Journal calls out** (the user, 2026-10-27): its own voice, as a reaction to the click;
  picking another cuts the last one short. The window is called "Mount Journal" now, its colours button "Customize", and
  the mounts' pictures sit centred in their boxes (the rat sat up by its name).
- **The call key waits its turn** (the user, 2026-10-27: pressed fast it made a cacophony): V does nothing while your
  mount's call or its babies' answers are still sounding, and presses are at least half a second apart.
- **The colours window's right column is whole** again: its dropdowns and last colour square ended under the scrollbar.
- **The Mounts panel looks like one of the game's own windows** (the user, 2026-10-27: "make it look more official"):
  the game's tan title bar with its ornaments and a close button carries "Mounts" (the count of mounts and the band it
  took are gone), the frame's corners sit on the window's edge with no margin of ours past them, and the buttons, titles
  and headings are in the game's bold font (`small_font_bold`, as its buttons; ours were the plain one). The gap under
  the description and the one beside the list are closed. The colours window has the same title bar.
- **Three sound sliders per mount** (the user, 2026-10-27): Voices volume (its calls and its babies' answers), Steps volume (its steps,
  the cart, the bell, its wings) and Baby steps volume, in place of one "Voice and steps". Your old value is the Voices one now.
- **An Abilities card in the Mounts panel** (the user, 2026-10-27): under Stats, each ability with its icon and a line
  about it: Flying (the mount's own picture in the air) and Cargo cart (the game's own cart wheel, in your colours, with
  how many items it holds), and any a mod adds. The Stats card is tighter, with a rule under its heading.
- **Every mount and skin has a line of its own in the Mounts panel** (the user, 2026-10-27: "an interesting 'lore'
  sentence or something funny"), in place of notes about its pixels, its cart and its colour regions.
- **Gerry's babies answer him** (the user, 2026-10-27): a baby whose mount has no answer sound of its own answers with
  its mount's voice, higher by its size and a little different for each baby (before, Gerry's were silent). The
  worm, which has no voice, stays quiet.
- **The keeper names your mount when it refuses** (the user, 2026-10-27): "Bella pulls 4 items at most" with a name
  given, else the mount's own ("Gerry pulls 4 items at most", "Dragon can't pull the cart."); before, "a skull", "a dragon".
- **The Critters are the Rat, the Snail and the Worm** (the user, 2026-10-27: no "Giant" in their names; their ids are
  the same, so nothing in a save changes).
- **"Behaviour" is "Mount Behaviour"** in the settings, and the "Status: Active" line at the top of Keys is gone (the Mods
  list says whether the mod is working). Your values carry over.
- **Babies always look like their parent** (the user, 2026-10-27): "Babies are the same mount and skin" is gone; off,
  it made a dragon's babies plain baby donkeys. A 0.10.0 player who had it off now sees their babies in the donkey's
  skin. The line leaves the cfg.
- **The bell is the cart's** (the user, 2026-10-27): the bell key rings only while you drive the cart.
- **Two settings are gone** (the user, 2026-10-27: "i dont think we need them"): "Calling your mount leaves the cart
  parked" (always so now: the ride key brings your mount alone and a parked cart stays where it is) and "Bell sound
  when setting off" (no bell when the cart sets off). Their lines leave the cfg.
- **No more Cart capacity setting** (the user, 2026-10-27: "we shouldnt have a Cart Capacity option now because ... each
  mount can have its own value"): a cart holds what its mount carries (the donkey and the snail 6, Gerry and the worm 4,
  the rat 2). A Cart capacity you had set no longer limits your carts; its line leaves both cfg files.
- **The safe spot has its own section** at the end of the settings: "Bring me back if I fall out of the world", the
  "Back to the last safe spot" key and button (before, spread over Keys and Behaviour; the "Last safe spot"
  coordinates line is gone).
- **Every setting's text is short and plain** (the user, 2026-10-27: "keep them simple ... read with a skim"): whole
  sentences, no "None = no key". "Bray sounds" is "Mount calls" and "Say why mounting is refused" is "Say why you can't
  get on" (the cfg keys are the same).
- **The mount packs' files are named like the rest** (the user, 2026-10-27): `GK2SaddleUpDragonsMountPack.dll` in
  `BepInEx\plugins\GK2SaddleUpDragonsMountPack\` and `GK2SaddleUpCrittersMountPack.dll` in `BepInEx\plugins\GK2SaddleUpCrittersMountPack\`, beside
  `GK2DonkeyAPI` and `GK2SaddleUp` (never released under the old names; the mounts' ids are unchanged).
- **L opens the Mounts panel** (the user, 2026-10-27: "we cant not have a default hotkey for this"): the Mounts
  panel key is the first row of Keys and is L by default (it had no key; the Mount section's button still opens it). L
  is free in the game's own keys; typing a name or a search in the panel doesn't open or close it.
- **Each mount has its own name, babies and voice, set in the Mounts panel** (the user, 2026-10-26: "make the amount
  of babies be saved per mount"; "users need to set their mount names from inside here"). The journal page lists, for
  the mount picked: its Name (a text box: type it there, no more editing the cfg), its Skin, Babies (0 to 5), Babies
  follow you on foot, Babies come indoors, and Voice and steps (its sounds'
  volume); the babies rows only for a mount that has babies. They are in the same order on every mount. Lantern and
  Lantern lit by day moved into the colours window's Cart colours, per mount too. These left the options pages (Sound
  and Cart lose a row each; the Babies section is gone). What you had set carries over to the donkey; the other mounts
  start from it.
- **New controls in the Mounts panel** (the user, 2026-10-26: the "< >" arrows are "way too limiting"): a switch for
  on/off, a row of number buttons for the babies, a slider with its % for the volume, a dropdown with a scrolling list
  for the Skin and every colour preset, a text box for the name. The switches and the slider are drawn like the rest
  of the panel (its dark boxes with the faint edge, the knob and handle the game's red button face). The labels line
  up with the stats card's and are no longer cut off on the left; in the colours window each column has its labels
  on the left and its controls and colour squares on the right; the four buttons at the bottom have one gap between
  them.
- **Babies hop** (the user, 2026-10-26: "make the babies jump occasionally as part of their movement"): now and then
  in a step, every few seconds while they trot after you or their parent, and on the spot while they stand about. The
  hop is their mount's own jump, small. Each hop "holds the key" for its own time: mostly little hops, now and then a
  big jump. When you jump on their parent, the babies following it mostly jump too, each a moment after you, usually
  a big one. `ShuffleFoalHopChance` (0.2; 0: no hops) and `ShuffleFoalJumpAlongChance` (0.75) in the API's cfg.
- **Each baby is its own** (the user, 2026-10-26: "they all look alike ... Persisted randomness though so that they
  wont constantly change"): its own size (a little bigger or smaller than its mount's babies), its own shade of the
  parent's colours (lighter or darker, warmer or cooler, more or less saturated, the second tone and the dark parts
  such as the dragon's wings too; on a near-black coat some are charcoal and some have a red or wine cast), its own
  temperament (lively ones roam further, step faster, rest less and hop more; calm ones stay close and rest), its own
  trot, walk and voice. The same baby after every load; the donkey's babies come back the same after a while on the
  dragon. `ShuffleFoalVariety` in the API's cfg (1; 0.5 subtle, 2 strong, 0 all alike).
- **The babies' answers to a call sound less mechanical** (the user, 2026-10-26: they answered "sequentially one after
  the other with no variation at all"): a new order every call, uneven gaps (now and then two at once, sometimes a
  pause), and each voice a touch higher or lower.
- **Babies no longer stop following you for good** (the user, 2026-10-26: "sometimes they just stop following you
  completely after a while"): a baby following you on foot that fell behind, with its parent left far off, was frozen
  by the performance levels, and nothing woke it to follow again. A baby with you now always stays awake.
- **Babies left with a parent far away no longer wait there for good**: when their parent stood so far off that it
  was put to sleep (no picture), the babies took it for somewhere else and waited where they were until it woke.
  They now go to it, or catch up with it out of sight.
- **Switching to a mount with another baby size keeps the babies** (Gerry's are a third the size of the donkey's):
  they change size where they stand instead of vanishing and new ones appearing beside the parent.
- **Babies get stuck less** (docs/foals.md "Hops, getting stuck, and stopping for good"): a way round an obstacle is
  now one the baby can really walk (it was planned through grid centres and then refused, and the baby gave up at
  once); a refused way is planned again from where it stands; a lost baby looks for a way round from further off;
  one wedged where it stands still tries to rejoin; one lost for 10 s out of sight is brought to you even while you
  walk about. A baby that hasn't got nearer you for 20 s says why in the log (`[foal] ... has not got nearer`): send
  that line if one still gets stuck.
- **The cart key no longer brings the cart to a mount that can't pull it** (the user, 2026-10-26: "any mount that
  doesnt have the ability to pull the cart shouldnt bring it with g"): with the dragon (or any mount whose `cart =
  false`), G says it can't pull the cart and leaves the cart where it stands; before, the cart came to you and was
  refused only then.
- **The mounts are downloads of their own** (the user, 2026-10-26): GK2 Saddle Up itself ships the donkey and Gerry;
  the Dragon (GK2 Saddle Up: Dragon), the rat, snail and worm (GK2 Saddle Up: Critters) and, since 2026-10-27, the
  donkey's Hellfire and Royal Parade (GK2 Saddle Up: Donkey Skins) are packs to add, each a folder for
  `BepInEx\plugins\` and a download of its own.
- **Kept donkeys and carts are never lost because something went wrong** (docs/multi-mod-loading.md): after an error
  took them away, the side file is left as it was instead of being rewritten or deleted by the next save; a side file
  older than its save is kept aside instead of being overwritten; the last one is kept as a backup; one that can't
  be read whole no longer loses the rest.
- **The Mounts panel lines up** (the user, 2026-10-25): no "Skin" heading over the Skin row, which sits under the
  stats card now; the mount's picture uses the whole height; in the colours window the custom colour squares and
  their labels line up with the other rows, and the headings sit on the left with a line under them.
- **No flash of the new mount when leaving the dragon in the Mounts panel** (the user: "that mount appears large and
  without a cart and then the cart appears"): the dragon's picture stays until the new mount's is ready, then it
  shows whole with its cart.
- **No cart options for a mount that can't pull one** (the user, 2026-10-25: "for mounts that cant pull the cart, the
  drive the cart button still appears in teh journal and there's still cart customization"): with the dragon picked,
  the Mounts panel has no Drive the cart button and its colours window no Cart colours. Your cart colours for it are
  kept; every mount that pulls the cart shows both as before.
- **The dragon jumps like a dragon** (the user, 2026-10-25: "the dragon feels like a plompling rock with the jump
  ... maybe it can have something akin to takeoff and landing kinda thing so it doesnt look like a super mario
  thing"): it gathers itself, pushes off on its wings, holds them open at the top and sinks onto the ground braking,
  then folds them, on its own take-off and landing pictures, with a flap. Higher and slower than the donkey's hop
  (36 px in a second against 28 px in under half). The other mounts jump as before.
- **The babies' answers are as loud as their parent's call** (the user, 2026-10-25: "i still cant hear the babies
  ever replying"; the log showed every answer playing, about 10 dB under the call once the babies' smaller size and
  their distance were counted). The dragon's babies answer with a lower growl too, not a chirp.
- **The snail squelches as it slides along** (the user, 2026-10-25: "sound those slime sounds while walking"):
  three more squelches from the slime recording, two a stride, as quiet as the donkey's hooves on grass. It was
  silent while moving.
- **The babies answer after their parent's call, so you hear them** (the user, 2026-10-25: "are we sure the foals
  are replying at all?"). They did answer, every time, but half a second after the call began, and the dragon's
  roars last two to four seconds: every answer played under the roar. Now they answer one after another once the
  call has ended. Gerry's lines are level with the donkey's bray too (they were 7 dB under it).
- **The new mount sounds sit at the game's own loudness** (the user, 2026-10-25: "the mouse squeaks sound so much
  louder than the dragon ones ... we should use the ingame sounds obviously"). Measured as the ear hears it (LUFS),
  every sound added today played about 10 dB over the game's donkey, and the rat's squeaks 3 dB over the dragon's
  roar: the game plays its own sounds at a volume of their own (0.375 for the bray), ours went in at full. Now each
  mount's cry is as loud as the donkey's bray, getting on and off as its lip smack, the babies' answers 6 dB under
  the bray, and the dragon's flight sounds keep their place against its roar.
- **New key: Call out (V)** (the user, 2026-10-25: "the donkey would bray, the dragon would roar"). While mounted,
  your mount gives its own cry on demand: the donkey brays, the dragon roars, the rat squeaks, Gerry talks; its
  babies answer. The snail and the worm have no voice and stay silent. The cries that come by themselves now and then
  are unchanged (the Bray sounds row switches those off; the key still works).
- **The dragon, the rat and the snail have sounds of their own** (the user's recordings, 2026-10-25:
  "can we fill some missing slots with our new sounds?"). The dragon roars with three growls (it used the zombie's
  cry pitched down), growls as you get on and off, thuds as it touches down after a flight, and its babies answer
  with the growls pitched up. The rat squeaks (it used the spitter's cry pitched up), squeaks as you get on and off,
  and its babies answer with small squeaks. The snail squelches as you get on and off (it used the fishing blop), its
  babies with a small squelch. Gerry's getting on and off and the worm's and Gerry's babies are still silent.
- **The dragon flies, for everyone** (the user, 2026-10-25: "make flying not dev only"). Riding it plain, outdoors:
  Space jumps, Space again in the air takes off, Left Shift climbs, Left Ctrl descends, Space while flying lands. It
  flies over water, fences, cliffs and roofs and comes down only where you can stand ("Nowhere to land here." while it
  can't); a save, H or a door lands it first. Every mount jumps with Space (hold for a higher jump, over what is lower
  than the jump). Not in a fight, where Space is your attack. Four rows in Keys: Jump, Fly, Climb (hold) and Descend
  (hold); Climb and Descend are new. Flying was built as a dev-only experiment since 2026-10-22; what is still to see in
  game is listed in docs/open-items.md ("Flying for players").
- **The dragon pulls no cart any more** (the user, 2026-10-25: "remove the ability for the dragon to haul the cart").
  Its card in the Mounts panel says "Can't pull the cart" and its Cargo "no cart"; G on the dragon (or Drive the
  cart in the panel) says "A dragon can't pull the cart.", and a cart hitched when your mount becomes the dragon is
  left standing where it is. What each mount is for beyond that is being decided (docs/next-update.md).
- **The Effect section is gone, and so is the effect in the Mounts panel** (the user, 2026-10-25: "remove all the
  stuff we're exposing regarding the effects"; particle effects will come back on specific skins, designed anew). The
  Effect, Effect shown and four toggle rows, the "Comes with" line and the effect half of the panel's
  "Colours and effect..." window (now "Colours...") are removed; the Hellfire skin no longer says it comes with its
  fire. The six Effect lines and the GK2 Donkey API's seven `[Effects]` lines leave your cfg files at the first start
  (no other setting changes).
- **The mount's picture in the Mods menu sits in its row again** (the user, 2026-10-25: "the mount is being offsetted
  out of the panel"). GK2 Mod Framework 0.1.24 moved a row's field from the row's middle to its top; the picture was
  centred on the field, so it stood over the section's heading. It is now level with the row's middle, across from
  the field.
- **Picking a mount in the Mounts panel no longer freezes the game for a moment** (the user, 2026-10-24: "when
  selecting the dragon from the mount journal, the game freezes for some frames"). The picture is framed on what the
  mount draws over two seconds of its walk: twenty photos, each read back from the graphics card, all in one frame (40
  to 60 ms and 19 to 26 MB a pick; the dragon's picture is the biggest). Now one photo a frame (the last picture stays
  until the new one is framed, so it never visibly resizes), each look's framing is remembered (picked again: framed at
  once), and one read-back buffer per picture replaces a new texture and array per photo.
  After that a micro freeze stayed (the user, 2026-10-25: "its the dragon which is doing the micro freeze when
  selecting it from the journal"), traced frame by frame: the click's frame took 13 to 20 ms against 8.3 (the cfg
  file written whole, 3.5 to 4.7 ms; the dragon's 15.6 MB sheet made and its first six pictures, 4.5 to 5; the picture
  re-posed, 2 to 4.5), the frame the dragon appeared up to 20 ms, and Gerry's and Hellfire's builds 17 to 44 ms a
  frame for eight frames (their pictures are read back from the game's sprites through the graphics card). Now the cfg
  file is written once the picking pauses or the panel closes; a look builds for 2 ms a frame, its sheet in a frame of
  its own; a finished look is worn from the frame after its upload; and every look the panel showed stays ready while
  it is open, so picking it again shows it at once. A first pick still had one slow frame (the dragon 25 ms: Unity
  zero-filling its new 15.6 MB sheet, 14.7 ms; Gerry 16 to 19 ms: every sprite in memory looked through for his frames,
  6.5 ms, and his first picture). The sheet is made without the fill (every cell is written anyway), Gerry's frames are
  found once a session (the game-ready check finds them; looked for again only when one is gone), his rest picture is
  measured in a frame of its own, and nothing of a build runs in the frame it was asked for.
- **Mounts scale better: switching, sounds and the panel's pictures** (docs/mount-switching-performance.md). The
  panel's picture of a look is framed once ever, not once a session: the framings are kept in
  `BepInEx\cache\GK2DonkeyAPI\framings.txt` under the look's pictures and the game's version, and a changed look is
  measured again. The picture's edge check (a whole read-back) runs for 5 s after a framing or a setting change instead of
  every second. A mount's own sound files load one a frame, are checked on disk once a session, and stay compressed in
  memory (they were all decoded in the frame the world's mount turned into the dragon: 15.6 ms of a 60 ms frame); a
  mount shown in the Mounts panel starts loading its sounds then. The looks the panel keeps ready are capped at 64 MB of
  sheets, the least recently shown going first. Closing the panel writes the chosen mount half a second later, not in
  the closing frame (that frame was 21 to 24 ms with the 4 ms write in it).
- **The babies of the mount you ride come through a door with it** (the user, 2026-10-24: "i selected gerry when
  inside the house and rode him out, and the babies didnt follow me outside"). They stayed: the house and the yard are
  one place to the game, so they waited for their parent to stand still out of reach, and a ridden parent never does.
  Now the ridden mount's babies near the door are placed beside it on the other side while the screen is black, in or
  out.
- **A skin of a mount is built on its default** (the user, 2026-10-24: "all other skins are built on top of it";
  docs/skin-layering-plan.md, docs/skins.md "Variants: built on the default"). Every skin of a mount but the donkey
  starts as a copy of that mount's `default` look: the pictures it doesn't draw, and its settings it doesn't give
  (seat, offsets, effect, colours, description, portrait, the skin's tuning and sounds), come from the default and
  follow it when it changes, so a new look can be one picture and a few lines. `layer = over` on such a skin draws it
  over the default (holes where its erase colour is, its pixels on top), the way Hellfire is drawn over the game's
  donkey. The donkey's own skins (Hellfire, Royal Parade) are exactly as before; Saddle Up's mounts ship only their
  defaults, so nothing a player has looks different. The loaders take each mount's `default` first; the Mounts tab
  (F11) says what a skin takes from its default and saves only its own values.

- **Packs are sorted by mount: a folder per mount, its skins inside it** (the user, 2026-10-24: "i want to see things
  sorted by mount ... a skin can only belong to one mount ... where are the sounds? Shouldn't we have some root mounts
  folder"). A pack is now `gk2mounts\<mount>\` (players: under `BepInEx\plugins\` or a mod's folder there; Saddle Up:
  `src\SaddleUp\assets\mounts\`): `mount.cfg` (the mount: `[Mount]`, `[Stats]`, `[Tuning]`, `[Tuning.Ride]`,
  `[Tuning.Cart]`, `[Sounds]`, `[Sounds.Ride]`, `[Sounds.Cart]`, `[Sounds.Baby]`, plain names), its `sounds\` (the
  mount's files, for every skin of it) and `skins\<skin>\` (each look: `skin.cfg`, its pictures, `front\`, its own
  `sounds\`). The donkey's folder holds skins only (Hellfire and Royal Parade: `mounts\donkey\skins\`; since 2026-10-27 the Donkey Skins pack's `gk2mounts\donkey\skins\`). Before, a mount was a `[Mount]` block inside its default skin's `skin.cfg`
  (the "declaring skin", the joiner and Default rules) and its sounds sat in that skin's `sounds\mount\`: a mount's file
  named after its event was also the skin's own, and the dragon's flight sounds loaded twice. Now a `skin.cfg` holds
  no `[Mount*]` section (said in its report), a skin's mount is its folder's, a mount folder without `mount.cfg` adds
  its skins to a mount registered elsewhere, two `mount.cfg` of one id meet the skins' version rule, and a pack mount
  goes with its last skin. The Mounts tab (F11) saves the mount's rows into `mount.cfg`. Nothing a player has changes (mounts and packs were off in every release). Tools (`skinroots.py`, `canonical_cfg.py`
  for both files, `new_skin.py --mount`, the generators, the preview page by mount), `tests/SkinsScan` (a pass per
  mount folder) and the self-tests (the temp packs, the id rules for mount packs, the sound folders) follow the tree;
  the embedded resources are `GK2SaddleUp.mounts.<mount>\...` and the dev write-back root `GK2MountsSourceDir`.
- **Every mount has one default look, `skins\default\`; skins are named within their mount; mount ids carry their
  mod's prefix** (the user, 2026-10-24: "every default skin directory called 'default' ... a mount must only have one
  and only one default skin"; "I dont think the skin ids being global is a good idea ... other mods adding more mounts";
  "saddleup specifically would have its own modifier"; docs/mount-id-collisions.md). A mount's id is `<mod>.<name>`:
  Saddle Up's are `saddleup.dragon`, `saddleup.rat`, `saddleup.snail`, `saddleup.worm`, `saddleup.gerry` (their
  folders too); an id without a prefix is refused, only the game's `donkey` is plain, so two mods' dragons are two
  mounts and a saved one never turns into another mod's. A skin's full name is `<mount>/<skin>`
  (`saddleup.dragon/default`, `donkey/saddleup.hellfire`): its folder's name, within its mount (`skin.cfg` has no `id =` any more),
  so two mounts may each have a `fire`. A mount's own look is its `skins\default\` (`[Mount] default_skin` is gone): a
  pack `mount.cfg` without one is refused with its skins, a folder adding skins to another mod's mount may not bring a
  `default`, the donkey has none (its own look is the game's). A skin added to a mount another mod owns (every
  donkey skin, a folder without `mount.cfg`, a code skin on another's mount) is named `<mod>.<name>` or refused, on
  your own mount any name: Saddle Up's donkey skins were `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`
  (since 2026-10-27 the Donkey Skins pack's `donkey/saddleup-donkey-skins.hellfire` and `.royal-parade`; the old names
  `hellfire` and `royal-parade` find nothing now). The settings rows hold the full names (`Mount =
  saddleup.dragon`, `Skin_saddleup.dragon = saddleup.dragon/default`); a dev's cfg or save from before that names a mount (`dragon`) finds its mount's default and is rewritten; the
  looks' names of the mount folders (`red-dragon`, `grey-rat`, `garden-snail`, `earth-worm`: a few hours old, never
  released) still find their mount's default (the user: "also fix in the old names thing"), and a per-mount settings row
  under the mount's old id (`Coat_dragon`) carries its value to the new one (`Coat_saddleup.dragon`) when that has none.
  (2026-10-27: those old looks' names and the rows' carry-over went with the API's rename tables, below for mod
  authors; a mount's short name, `dragon`, still finds its default.)
- **Hellfire's first spawn after a start has its holes again**: the companion's skin is built ahead at the main menu,
  before the game has the donkey's own frames (0 of 44 found), and Hellfire (drawn over the donkey, erasing its ears
  under the horns) was built without them ("its erase colour can't clear the donkey", 2026-10-24 log). A skin built
  from the donkey's frames (an over skin that erases, a recipe) now waits until the frames are in memory (looked for
  every 3 s, no log line until found).
- **The dragon walks like a heavy beast** (the user, 2026-10-24: "his legs ... too dinky", "walks like a lady", "like
  someone modelling stilettos on a catwalk", "a stick stuck up his arse"; the reference's Walking studied, docs/flying.md
  "The walk, studied"): thicker legs with no thin ankle, the forelegs built like the hind (a shoulder mass, a thick upper
  arm and forearm, a short wrist), broad flat paws, the hind feet flat (no tiptoe), long low strides (25 px a cycle,
  was 17), the far legs apart and darker (the four feet were on one line), the shoulders and haunch
  rocking in turn with the folded wings, the head trailing the body a little and the motion running up the neck. The
  seat under the rider moves as before. The flying frames keep their poses with the thicker legs. On the ground no leg
  goes below the paws' row any more (frames 2, 4, 6 and 8 put one 1-2 px under the ground). Standing and baby mounts
  walking about now follow their mount's stride too (the donkey's unchanged).
  In the air the forelegs keep their earlier shape, only thicker: the heavy build (a shoulder mass, three bones) is
  for the legs on the ground only (in flight it folded into one lump under the chest that changed shape between the
  beat's, the glide's and the climb's frames and hid the far paw: the user, "the front leg is jumping from one place to
  the other"). Still open: the steps against the ground speed (the user: "the legs pacing dont match with the speed
  we're moving"); the stride was set from the donkey's frames, not measured: the dev stride probe (`Dev/StrideProbe.cs`,
  `[stride]` log lines) measures it in game first: every walking mount (ridden, standing, babies, with a cart), a row
  per walk cycle in `BepInEx/GK2-strides/`: the clip and the animator's speeds, the multiplier set, the speed keys, the
  cycle's time, the ground covered in world units and the picture's art px, the body frames shown and each footstep
  with the frame it played on. The F11 group **Walk** ("the walk survey") drives it: every mount ridden and with the
  cart, steered right and left by the test (the keeper's stick answered for the run only), a standing dragon and a baby
  left to shuffle near. First reading (the dragon, by hand): 287 art px a cycle in 0.91 s, the drawn steps cover 25.
  The survey (198 cycles): at `WalkCycleSpeed` 1.5 every mount covers the same ground a cycle, about 190 art px (the
  game divides the multiplier by 1.5: the clip plays at the mount's speed multiplier), whatever its speed; the dragon's
  1 (set earlier the same day from a guess) made it 278 px in 0.91 s, its legs at half the donkey's pace for its speed;
  and its footsteps (two a cycle, frames 1 and 5) missed two of its four paw landings (frames 1, 3, 5, 7). **Fixed:** the
  dragon's `[Mount.Tuning] WalkCycleSpeed` line is gone (1.5, as every mount: 0.61 s a cycle riding, 0.81 with the
  cart) and its step sounds four a cycle, one on each paw's landing (the donkey's timing). Every footstep was within
  0.02 of a cycle of its frame: the timing itself is exact.
- **Standing mounts and babies now make their sounds near you**: a parked donkey grazes and smacks its lips at the
  mount sounds' volume, calls now and then (three times rarer than the one you ride, and never two voices within four
  seconds in the scene), its babies answer it one after another, a baby's steps are higher and softer, a herd standing
  in the yard is heard as the ridden one is (within the game's hearing distance). Saddle Up's Sound section has the
  switch, "Standing mounts and babies sound" (on). The mount you ride sounds exactly as before. The API cfg's `[General]
  StandingSounds` (on) turns it off, which also hands a standing donkey's lip smack back to the game (its own
  volume, as before); the dev Sounds tab has the switch.
- **Never lost.** If you ever fall out of the world (under the ground, into nothing, far below it) you are put back on
  the last ground you stood on, with "Back on solid ground." (the game itself has no protection: off the ground the
  keeper falls for ever, and a door places him without a check). Saddle Up's Behaviour section has the switch ("Bring
  me back if I fall out of the world", on), a button "Back to the last safe spot" for when you are stuck, and the Keys
  section a key for it (none by default). Never during a door, a cutscene or a dialogue; standing under a bridge is
  fine. docs/never-lost.md.
- **Fixed: the mod's keys no longer act while the Mounts panel is open.** H, G, the bell and the send-away key
  pressed in the panel or its colours window got you on and off your mount behind it; now they wait until the
  panel is closed, as the game's own keys do. The Mounts panel key still closes it.
- **The mouse wheel scrolls the Mounts panel and the colours window anywhere on them**, not only by dragging the
  scrollbar: over the mount list it scrolls the list, elsewhere on the panel the rows on the right; on the colours
  window, its rows.
- **Fixed: Ride and Drive the cart in the Mounts panel no longer get you off.** Pressed while riding, they ended the
  ride (as the keys do for the way you're riding). Now you stay on: pick another mount and press Ride, and you ride
  on as the new one; Drive the cart brings the cart, Ride while driving leaves it standing; on someone else's
  donkey, yours comes and you get on it.
- **Each mount has its own speed now.** How fast you ride belongs to the mount you pick, and the cart slows every
  mount the same way (to three quarters of its riding speed, as it always did the donkey). The Mounts panel shows it:
  "Speed: Fast" (against the donkey), and a new stat, "Cargo: 6 items", what its cart carries (since 0.12 it is what
  the cart holds, and the Cart capacity setting is gone). The donkey is as fast as ever (twice walking
  speed riding, 1.5 times with the cart); the rat is very fast, Gerry a little faster, the dragon and the worm a
  little slower and the snail slowest. **The Riding speed and Cart speed rows are gone**, and a Faster or Fastest you
  had picked no longer applies: the mounts set the speeds now. Their old lines leave your settings file by themselves.
- **The colours window draws the mount as big as the Mounts panel does** (it was smaller there).
- **Fixed: a mount picked in the Mounts panel no longer shows small or half-changed for a moment** (its cart's
  colours first, then the new mount in the old one's frame and spacing): the picture keeps the last one until the
  new mount is ready, then changes in one go.
- **Picking a mount in the Mounts panel no longer changes your mount.** The list shows it; Ride or Drive the cart
  makes it yours. Done (or Escape) keeps the mount you had; a skin or colours picked for a mount stay for when you
  pick it.
- **The mount pictures are always lit as outdoors at midday**, not tinted by the hour (tan at dusk, blue at
  night) or the room, and never washed by the game's fog, its lamps' light or its cloud shadows.
- **New: babies follow you on foot.** A new row, "Babies follow you on foot", on: off your mount, outdoors, your
  babies come with you instead of staying by it (turn it off for the old way). They trot after you,
  and catch up out of sight when you're far ahead; when you get on a mount they go back to yours. Into buildings
  they still come only with "Babies come indoors". The babies rows have a section of their own now, Babies, under
  Mount; your values are the same.
- **Fixed: the settings page's picture of your mount kept the donkey** after a mount drawn from the game's own
  pictures (Gerry) became available again a scene later, while your mount in the world already showed it. The
  picture (and the Mounts panel's) now follows at once, as it does for a skin added, replaced or removed.
- **New: mounting indoors** (a player asked). Behaviour has two new rows, both off, so nothing changes until you
  turn them on: "Allow mounting indoors" rides into houses, the church and the morgue and lets you get on inside;
  "Allow mounting with the cart indoors" (only with the first on) brings the cart in too, a corpse on it staying
  on it. With the cart not allowed, driving through a door leaves the cart standing at the door with its load and
  you ride on inside; at the morgue you get off inside carrying the corpse, your mount in there with you. A door
  with a loading screen still ends the ride. The cart can get caught on furniture and door frames inside. Where the cart isn't
  allowed (indoors, in a fight), the cart key now says so without calling your mount over first.
- **Fixed: a loaded cart crept back behind a long mount.** With the rat (or the dragon, which pulled the cart then) hitched, every item put on
  the cart moved the whole cart one more hitch gap away; a loaded cart made again (through a door, switching from
  riding to the cart) came back too far as well. The cart keeps its place now. The donkey has no gap, so it never
  showed there.
- **The Mounts panel is a mount journal now.** The list of your mounts on the left with a picture of each; on the
  right the chosen one walking with its cart, a card with its speed, whether it pulls the cart, its babies and how
  many skins it has, and its Skin and Effect rows. "Colours and effect..." opens a second window with the mount's
  picture and every colour row, a custom colour picked from 24 coloured squares. Ride and Drive the cart call the
  mount straight from the panel; a search box appears when you have more than eight mounts; Escape steps back and
  never opens the pause menu. Hellfire and Royal Parade now come with Saddle Up (nothing changes for you; since
  2026-10-27 they are a download of their own, GK2 Saddle Up: Donkey Skins).
- **Binding a key no longer presses it.** Setting the Mounts panel key in the Mods menu opened the panel on the
  spot, and the ride and cart keys had the same flaw (binding G hitched): the key you pressed to set the binding
  counted as a press of it. A key just bound now waits until it has been let go.
- **Where the panel lives** (Saddle Up 0.12.0): the Mounts button in the Mount section, or a key of your choosing
  (Keys: Mounts panel, none by default). The Mount, Skin, effect and colour rows are no longer in the Mods menu or
  the Settings window: they are chosen in the panel, so the settings page stays the same size however many mounts
  you install. Your saved choices are the same cfg entries as before. The "Choose colours" bar is gone into it.
- **A mount's legs match its speed.** The walk animation's rate per unit of speed was the donkey's constant on
  every mount; it is a value now (`WalkCycleSpeed`, 1.5 = the donkey's), so a mount with a longer stride, or a
  scurrying one, sets its own in its file.
- **A mount asleep far away frees its pictures** and takes them again when it wakes; a skin's canvas is at most
  256 (a 512 one held 49 MB). A mount built from the game's own character frames (Gerry) is checked when the game
  is up: if a game update took its frames away, the log says which, the mount is left out of the lists and your
  donkey comes instead of an invisible mount.
- **A mount can't take your settings unseen any more.** A mount that sets its own speed or sounds (its own file
  says so) used to win over your Speed, Brays and Bell rows while you rode it, and the rows did nothing; now such a
  row is left out of the menu for that mount and your value still applies to every other one. A mount from a mod
  may set its speed, seat, sounds and the like, never your behaviour switches, the lantern, your skin, effect or
  colours. A mod's ability can't take your Effect choice either: a value belongs to whoever set it (below).
- **Two copies of one skin: the newer wins.** A skin pack dropped into `plugins` and the same skin in a mod's
  DLL (an old folder copy left behind, say): the higher version is the one you get, a tie goes to the DLL, and
  the log names the loser and its folder so you can delete it. Before, the folder always won, silently.
- **Every mount and skin carries a description** (the dragon, the rat, the snail, the worm, Gerry, Hellfire,
  Royal Parade), shown in the Mounts panel.
- **The five mounts are Saddle Up's.** The Red Dragon, the Rat, the Snail, the Worm and Gerry
  moved out of GK2 Donkey API into GK2 Saddle Up (five skin packs in its DLL, registered with the API at start,
  the same way anyone else's mod ships a mount). Nothing changes for you: the same download, still switched
  off until the next update. Since the same night the donkey's two skins, Hellfire and Royal Parade, and the Hellfire
  effect are Saddle Up's as well (since 2026-10-27 the Donkey Skins pack's): the API ships the donkey alone, as the backend. A mount from any
  mod now shows in Saddle Up's settings whatever order the mods loaded in (below, for mod authors).
- **The dragon's belly has its own colour**: the Second tone row (below). A green coat had left a gold belly that
  no row could reach; for an afternoon the belly followed the Coat, now it
  is a tone of its own again, set apart from the scales. Its plate seams are every four rows instead of three.
- **A "Second tone" colour row** under the mount colours: the donkey's muzzle, the rat's tail, ears and nose, the
  snail's shell, the dragon's belly, Hellfire's lava (the API's Muzzle region), with Pale, Rose, Brown, Green, Blue, Dark and Custom
  (each preset measured to move every one of those parts by a clear step: a pink on the rat's pink and a brown
  on the snail's shell showed nothing at first); it greys on a mount whose look
  has no such part (the worm). Every drawn part of every skin a player may want to colour is now on
  the page: Coat is the body, Harness the tack, Second tone the rest of the skin; bone, horns, outlines and eyes
  stay as drawn. A colour row for a part the chosen mount has no such part of (Harness and
  Second tone on the worm) is greyed in the Mounts panel for that mount instead of sitting there doing
  nothing (since 0.12 the colour rows are the panel's, not the menus').
- **The Tests tab folds its groups**: each group's header carries its count, its last results and the Run group
  button, and a click on the name unfolds the scenarios (60 scenarios made the tab a long scroll).
- **The settings picture's cart moves to the new mount's hitch gap at once** when the Mount row changes. It slid
  into place up to a second later: the picture's paper mount never counted its
  tuning re-merges, so the picture only picked the gap up on its once-a-second fallback. The preview self-test
  measured it (the gap 54 frames after the skin) and now guards it.
- **The cart no longer sinks into a hill behind the mount.** Its ground is sampled under the cart body, up to a
  unit and more behind the mount's root, and at the edge of a raised path that sample lands on the level below:
  0.77 lower by the Inquisition Base rocks under the dragon's cart, within the
  slope limit, so the cart followed it down into the drawn ground. A target that jumps by more than 0.15 between
  two frames without the cart having swapped sides is a ledge, not a slope (walking moves the sample 0.12 a
  frame at most): it is held off and logged, and the cart keeps the mount's level until the sample agrees again,
  which it does once the mount has walked the step itself. Before, the jump was logged and applied.
- **The settings page regrouped** (Saddle Up 0.11.0): seven sections in a new order, each holding one kind of thing.
  Keys. Mount: its picture (with the cart now), Name, Mount, Skin, the babies rows, and "Keep your mount with the
  save" (was in Behaviour). Colours: the picture, then every colour under two headings (since 0.12 in the Mounts panel), "Mount
  colours" (coat and harness, the old Donkey section) and "Cart colours" (paint, wheels, wheel hubs, lantern light,
  from Cart). Effect. Behaviour: the two speeds, "Say why mounting is refused", "Shuffles while standing". Cart: "Cart
  capacity", "Zombies ride on the cart", "A storage chest rides on the cart", "Carry the corpse into the morgue",
  "Donkey leaves corpses at the crossroads" and "Calling your mount leaves the cart parked" (all were in Behaviour),
  then "Lantern" and "Lantern lit by day". Sound. The Donkey section is gone. Every value carries over: a moved row
  reads its old place on the first start (the log says which) and the old lines are cleaned out of the cfg.
- **The Mounts panel** puts the colour rows under the same two headings, "Mount colours" in the left column and "Cart
  colours" in the right, with the Skin row above the first and Effect below the second (0.12; 0.11's colours window did the same with the Mount row too).

For mod authors:

- **Saddle Up's donkey skins are the worked example of a skins-only pack** (`packs/GK2SaddleUpDonkeySkinPack/`, 2026-10-27;
  docs/making-a-mount.md "Built in or external"): a folder `gk2mounts\donkey\skins\<skin>\` with no `mount.cfg` (skins
  added to a mount another mod owns, here the game's donkey), embedded in a small plugin of its own,
  `GK2SaddleUpDonkeySkinPack.dll` ("GK2 Saddle Up: Donkey Skins", `dreas.gk2.saddleup.donkeyskins`, 1.0.0), needing the
  API 0.40.0 and with a soft dependency on Saddle Up (it loads after it), like the Dragon and the Critters. Its ids
  carry the pack's prefix (its name in lower case, dashes free): `donkey/saddleup-donkey-skins.hellfire` and
  `donkey/saddleup-donkey-skins.royal-parade` (were `donkey/saddleup.hellfire` and `donkey/saddleup.royal-parade`,
  never released). Hellfire's particle effect moved with the skin: `saddleup-donkey-skins.hellfire`, registered by the
  pack (was `saddleup.hellfire`, registered by Saddle Up); still shown nowhere until effects on skins are designed.
  Skins registered after Saddle Up's settings page was built are still offered in its donkey Skin row.
- **The API keeps no table of any mod's old ids** (the user, 2026-10-27: the API knows nothing of Saddle Up, and Saddle
  Up knows nothing of its packs): the rename tables `Catalog.renamedMounts`, `Looks.oldDonkeySkins` and `oldLookNames`,
  `Effects.oldNames` and `MountSettingsPage.RenamedMountKey` are gone, so the never-released ids `saddleup.dragon`,
  `saddleup.rat`, `donkey/hellfire`, the effect `hellfire` and the like find nothing now. An id a mod renames is that
  mod's to carry over. A short name still finds a mount's default (the name after its prefix when only one mount has
  it: `dragon`). The API's comments, log lines and messages name no mod (an effect id's example is `yourmod.fire`).
- **Freeplay's Behaviour keys** (`[General]`, all off/Normal): `FreeplayMaxCargo` (every cart the most cargo,
  `Mounts.Catalog.MostCargo`, 6), `FreeplaySameSpeed` with `FreeplaySpeed` (a new `SpeedBand`: every mount, the donkey
  too, at that band's speed, the cart's times the donkey's ratio). `MountInfo.Cargo`, `Speed`, `CartSpeed` and
  `SpeedWord` report what is in effect with them on. `Mounts.Catalog.SpeedWords` gives the panel's words in
  `SpeedBand`'s order, from the same one table the words and the bands come from (labels for a `SpeedBand` choice).
- **A mount's speed is one of the API's words** (the user, 2026-10-27: "These definitions should not be in the
  individual pack mods. They should exist in the api"): `[Mount] speed = Fast` (any case; `VeryFast` too), and
  `MountDef.Speed` is a `SpeedBand?` (it was a `float?` since 0.36.0, never released). The API says what each is: Very
  slow 1.25, Slow 1.6, Normal 2, Fast 2.25, Very fast 2.75 times walking speed. A number in `speed` is refused with a
  report (the donkey's applies), and so is a `SpeedMultiplier` in a mount's tuning, with a warning.
- **`Mounts.MountsEnabled` is always true** (it was false in 0.10.0's download): your mounts, skins and effects
  register and are offered to players. The switch behind it, the dev key `Debug.MountsPreview`, is gone (the user,
  2026-10-27: the mounts are mods of their own now, so which ones a player has is which packs they installed). The
  property stays for mods written against 0.10.0. A registration before the API has started still throws, saying to
  add `[BepInDependency]`.
- **A mount's `cargo` is enforced** (`MountDef.Cargo`, `[Mount] cargo`, 1 to 6, `MountInfo.Cargo`): its cart holds
  that many, or the player's `CartSlots` when smaller, re-applied when the mount under a hitched cart changes; never
  fewer slots than the cart holds. Until now it was shown in the Mounts panel only. A mount that leaves `cargo` out
  carries 6, as before.
- **Per-mount companion settings** (2026-10-26): `MountLook` (`companion.LookOf(mountId)`) has the mount's own
  `FoalCount`, `FoalsFollowOnFoot`, `FoalsComeIndoors`, `FoalsWearSkin`, `Name`, `SoundVolume`, `Lantern` and
  `LanternByDay`; the companion's properties of the same names read and set the current mount's (unchanged for a mod
  with one mount). A mount change applies its own name, babies, volume and lantern. `MountSettingsPage.FoalRows` makes
  its four rows per mount, as look rows (in the Mounts panel's journal, not the menus). `Toggle` and `Text` take
  `look: true` like `Choice`: a look `Text` row is a text box in the panel. The panel's journal orders its rows the
  same on every mount: a text row first, the Skin second, the rest in the order the page made them.
- **A mount from a mod loaded after Saddle Up gets its colour rows in their place** (found with our own pack DLLs,
  which load after Saddle Up, 2026-10-26): the settings page's catch-up appended a late mount's rows at the end, so in
  the Mounts panel its Coat, Harness and Muzzle rows sat under "Cart colours" and it had no "Mount colours"; now each
  builder's rows for it follow that builder's rows for the mounts before it (`MountSettingsPage.RunPerMount`).
- **Our mounts are worked examples of a pack without a DLL**: `packs/GK2SaddleUpDragonsMountPack/` and `packs/GK2SaddleUpCrittersMountPack/` in
  the repo, each exactly the folder a player puts into `BepInEx\plugins\` (docs/making-a-mount.md "Built in or
  external"). Their ids changed with the move (`saddleup-dragon.dragon`, `saddleup-critters.rat/.snail/.worm`; never
  released).
- **GK2 Donkey API 0.40.0: content from many mods** (docs/multi-mod-loading.md, docs/making-a-mount.md "Living next to
  other mods"). `MountHandle.Remove(ownerId)` (another mod's instance is refused) and limits on `SetData`. An effect's
  id is `<mod>.<name>` (Hellfire's is `saddleup-donkey-skins.hellfire`, the Donkey Skins pack's) and meets the version rule; a code skin or effect
  that loses it no longer throws into your Awake; a handle withdraws only what it registered; the first mod's sound for
  an event stays. A mount.cfg may say `needs =` / `format =`; one whose id isn't its folder's is refused; ids are ASCII,
  at most 64, no dot at an end. A newer pack of a mount brings its pictures with its facts (the old pack's skins go).
  Skins of a mount that isn't installed are offered nowhere. Pictures are checked from their header (a frame at most
  its canvas, a portrait 512x512), sounds at most 16 MB, no NaN, no file outside the pack. Register in Awake or Start.
- **Cart rows follow the mount (API 0.39.0)**: `MountSettingsPage.CartRow(section, key)` marks a row as the cart's: it
  shows only while its mount pulls the cart (`MountSettingsPage.MountPullsCart`). Saddle Up's eight cart colour rows
  use it. The Mounts panel hides Drive the cart for a mount that can't pull one.
- **Each mount its own jump (API 0.39.0)** (the user, 2026-10-25: "the arc the donkey makes while jumping looks silly
  for a dragon ... first lets make sure that we have the ability to specify per mount"): `mount.cfg` `[Jump]`
  (`height`, `tap_height`, `hold_min`, `time`, `rise_share`, `hang`, `crouch`, `crouch_time`, `squash`) and
  `MountDef.Jump` (`MountJump`, every value `float?`); a key left out is the default jump's (the donkey's: 28 px held,
  16 tapped, 0.45 s in the air). `MountInfo.Jump` (filled) and `MountInfo.OwnJump`. `[Jump]` in a `skin.cfg` is
  ignored and reported. A jump takes its mount's shape at the press. Two more keys: `fall_ease` (0 to 1: the fall
  braked to a soft touch-down) and `wings` (`MountJump.Wings`: the skin's flying frames as a small take-off and
  landing). The dragon's `mount.cfg` sets them; Saddle Up needs API 0.39.0.
- **Flying and jumping are on for everyone (API 0.38.0)**: `Mounts.FlyingEnabled` is true unless the dev Flying tab
  switched it off (before, it needed the dev tools unlocked). New Behaviour keys `ClimbKey` (Left Shift) and
  `DescendKey` (Left Ctrl): `KeyboardShortcut`s held in the air, the main key alone counts (a modifier can be the key;
  the game's Settings window can't capture a lone modifier, the Mods menu and the cfg can). The dragon's card says
  "Flies". Saddle Up needs 0.38.0 (its Climb and Descend rows set the keys); its own version stays 0.12.0 (unreleased).
  Dev-only still: the per-frame `[flight-probe]` lines (off) and the jump probe's files (the dev tools).
- **Nothing picks or shows a particle effect any more (API 0.37.0)**; making one stays (the user, 2026-10-25:
  "the ability would be available from the api and then we will consume it from the skin consumer"; how a skin
  comes with its effect is being designed). Kept: `Mounts.Effects` (`Effects`, `Find`, `EffectsChanged`, `Register`,
  `Unregister`, `GameParticles`), `MountEffectDef`, `MountEffectPart`, `MountEffectEmitter`, `MountEffectPlacement`,
  `IMountEffectSource`, `MountEffectInfo`: a registered effect is listed and ready, and shows nowhere yet. Removed:
  `MountEffects.NoneId`, `FromMount`, `IsNone`; `MountEffectWhen`; `Mounts.EffectSettings`;
  `MountSettingsPage.EffectRows`; `Effect` and `EffectWhen` on `MountCompanion`, `MountDef`, `MountInfo`,
  `MountSkinDef` and `MountSkinInfo`; the instance tuning keys `Effect` and `EffectWhen` (a side file holding them
  drops them on load); the API cfg's `[Effects]` section; the pack keys `effect =` and `effect_when =` in skin.cfg
  and mount.cfg (an old file's lines are reported as unknown keys). This reverses, for choosing an effect, the
  2026-10-17 rule that the player's effect is chosen through the API (docs/mount-looks-design.md). Saddle Up needs
  0.36.0 or newer as before (it calls nothing removed; 0.38.0 since the flying switch-on, above).
- **Skins built on their mount's default** (see For players): `MountSkinInfo.BuiltOn` (the default's id, or `null`)
  and `MountSkinInfo.OverDefault` (drawn over that default; `Over` stays "over the game's donkey"). A code skin's
  sentinels now mean "the default's" on such a skin: `SeatRow` -1 with `SeatFromPicture` false, `Offset` zero and
  `CartOffset` null, `Effect`, `Description`, `Portrait` and `Regions` null, the canvas 96x96 and pivot 48,48;
  `Effect = "none"` is none. A pack says "not the default's" with `seat = none`, `effect = none`, `portrait = none`.
  `Over` on a code skin of a mount other than the donkey is refused (drawn in its place, said in its report). A
  resolved sound's layer can be `"default"`.
- **`Mounts.Catalog.Register` no longer throws when another mod has the id**: the version rule decides (the higher
  version; at a tie code or a DLL over a loose folder; else the first stays), the loser gets a warning and a handle
  that does nothing; a code mount meets packs by the same rule (a newer `mount.cfg` replaces it, an older one is
  refused with its skins); code mount and code skin clips are banked under their mod. An embedded pack whose
  `mount.cfg` is refused now drops its skins as a loose folder does (docs/mount-id-collisions.md "Decided").
- **Mount ids are `<mod>.<name>`, skin ids `<mount>/<skin>`** (see For players; docs/skins.md, docs/making-a-mount.md):
  `Mounts.Catalog.Register` refuses an id without a prefix; `MountSkinDef.Id` is the name within `Mount` ("default" for
  your mount's own look) and the registered skin's `Id` (`MountSkinInfo.Id`, the "Skin" tuning value) is
  `<Mount>/<name>`; a recipe is a donkey skin (`donkey/<id>`). A code skin on a mount another owner registered and
  every donkey skin and recipe are named `<mod>.<name>` (`Mounts.Looks.RegisterRecipe(Guid, "example.sepia", ...)` is
  `donkey/example.sepia`): without the prefix `Register` / `RegisterRecipe` throw an `ArgumentException`; a code skin
  on your own mount takes any name. `Mounts.Looks.Find` takes the full name and a clear short one. No signature
  changed.
- **The dev Sounds tab's events are grouped by status**: what is playing on top, then its own, the donkey's, loading,
  MISSING, silent by choice and no sound, each group under its coloured heading, the catalogue's order inside one.
- **The flaps follow the drawn wing beat from the start of the climb** (the user, 2026-10-24: the flapping began
  late and missed the first flaps): only the airborne beats sounded, once the climb had settled at the top, and the
  launch's three surges fired three flaps in 0.32 s that blurred into one; and a session's first flaps were silent
  while their files decoded. Now a flap plays on every beat the picture shows, from the climb's first beat (after the
  crouch and the take-off frames) through the flight to the dive, and a skin's sound files start loading as soon as a
  mount wears it. The take-off files have no silence at their ends (their 60 ms fades only; checked).
- **No footsteps in the air** (the user, 2026-10-24: "when we're flying, the mount step is also being played"): the
  view's animator runs its walk clip under the flying frames whenever the keeper steers, and every footfall of it
  played a step; heard once a mount's sounds played at the ground point under it. Off the ground (a flight or a jump)
  no footfall, wheel or grazing sound plays now. The flying self-test steers in the air and checks no step plays.
- **Flying sounds are heard at full strength, babies' steps are toned down** (the user, 2026-10-24: "the takeoff
  sounds are still barely audible compared to the landing sounds"; "the baby steps are much louder than all our other
  sounds"). Measured first: the game's listener stays at ground level by the keeper's body (it stays on the ground
  while flying), and a mount's sound followed its drawn height: a take-off was heard at 87% at its start, 30% a quarter
  second on and 12-14% from half a second (7 units up), a landing the other way round (14% up to 93%). A mount's
  sounds now play at the ground point under it (a sound anchor of the rig's own, following it), so the rider hears
  his mount as he flies; on the ground nothing changes. A baby's steps: every other footfall (its legs cycle 1.7
  times faster than a grown one's and several follow at once), and a baby's volume falls with its size as size^1.5
  (0.46 at 0.6, was 0.77). Note: the player's own `[Ride] HoofVolume` here is 3 (the default 1.3), which every step
  is scaled by.
- **The dev Sounds tab shows what is missing and what is playing, in colour**: each event's name and source in its
  colour (red MISSING: the donkey's own sound, silent on this mount until its pack gives one; green its own, from the
  mount or the skin; white the donkey's; blue silent by choice; yellow loading; grey no sound), a key above the table,
  and a cyan ▶ with "playing" while an event's sound plays ("played 12s ago" after). The Mounts tab's sound rows
  take the same colours. A [sound] log line per play gives the sound's place, its distance from the game's listener and
  what it is heard at over its life (the flying events always, everything with the tab's switch): the measurement for
  the lift-off sounds heard much quieter than the landing ones.
- **The dragon takes off and lands with sounds of its own**: six sounds the user supplied, trimmed of their silence
  (0.05 to 0.25 s in front, 0.1 to 0.43 s behind), levelled to the same loudness without clipping and made mono OGG, in
  the dragon skin's `sounds` folder: four lift-offs (`takeoff_1.ogg` to `takeoff_4.ogg`, its `[Mount.Sounds] takeoff`)
  and two landings (`land_1.ogg`, `land_2.ogg`, its `land`, from the start of the dive); one at random per flight,
  never the same twice in a row, shared by every dragon skin. Its voice stays the game's growl placeholder.
  Then its wing beats and glide: the user's two flapping clips held 2 and 3 wing beats, cut at their quiet dips into
  five single flaps (`flap_1.ogg` to `flap_5.ogg`, the `flap` event, one per 0.6 s beat in step with the picture,
  volume 0.6), and a glide whoosh (`glide_1.ogg`, with the second landing clip, which came in the glide folder too; cut
  when the glide ends). The always-on flight trace leaves the flaps out (one every 0.6 s). The first clip's two beats overlap (the
  first one's tail still at -35 dB when the second starts): `flap_1` is cut at the second beat's onset (0.81 s) with a
  fade over the whole tail, not 50 ms earlier with a short fade (it cut out audibly, the user). The first sound files the mod ships. The trimming tool,
  for the next batch: `trim_sounds.py <folder> <out> [prefix]` (kept with the work copies in
  `C:\ClaudeCodeTemp\GK2-dragon-sounds`, with WAV previews and a README of what was cut).
- **The donkey's own sounds stay the donkey's** (docs/mount-sounds.md "The layers"): a mount other than the donkey
  no longer inherits the bray, the lip smack, the chewing or the getting-on and getting-off snorts riding plain; they
  are silent on it until its pack gives them a sound (a new creature is silent by default where it matters). The
  neutral sounds stay everyone's: the step by the ground, the cart's, the bell, the cart style's ladder. The snail's,
  the worm's and Gerry's old volume pins are gone. **What a skin is missing** is said in three places: one log line
  per skin when the game is ready, the first line of the Mounts tab's "Sounds of this skin", and an INFO line per
  built-in in `tests/SkinsScan`.
- **The herd and the babies' own sounds** (docs/mount-sounds.md; phase 3 of the sound system): the `answer` event
  (a baby's reply to its parent's call, the donkey's voice pitched up by its size unless given one), `[Sounds.Baby]`
  and `[Mount.Sounds.Baby]` blocks (`MountSkinDef.SoundsBaby`, `MountDef.SoundsBaby`, `MountInfo.SoundsBaby`) for the
  babies' own sounds over the grown one's. Self-test "herds and babies" (group Sounds).
  Found by that test: the idle call's timer was armed once, at an instance's first tick, with the interval of that
  moment (a plain donkey's 40 s), and a skin worn later with its own interval never re-armed it; the rig now re-arms
  whenever the resolved interval changes, ridden or standing. The sound self-tests drive the standing switch through
  Saddle Up's row, since its session override masks the cfg entry.
- **The sounds API** (docs/mount-sounds-design.md, phase 1 step 4): `Mounts.Sounds` with the catalogue of events,
  `Resolved` (what plays on an instance and from which layer), `Set` (a mod's own sound over everything, a game id
  or its own clips, back on Dispose), `Play` and `Stop`, the `Played` event; `MountSkinDef.Sounds` and
  `MountDef.Sounds` (and the per-style pair) for skins and mounts registered in code; `MountInfo.Sounds`. The Mounts
  tab (F11) edits a pack's sound lines: one row per event for the skin's block and for the mount's, the line's
  source and its volume, with what the ridden mount plays and from which layer next to it; the auto-save writes
  them to the skin.cfg. Self-test "the API" (group Sounds).
- **A skin pack brings its own sounds** (docs/mount-sounds.md, the author's page; the design in docs/mount-sounds-design.md): `.ogg` or `.wav` files in the pack's `sounds\` folder, named for their event by convention
  (`voice_1.ogg`, `voice_2.ogg`, `step.grass_1.wav`) or lined up in a `[Sounds]` block of skin.cfg (`voice = a.ogg,
  b.ogg`, `voice.volume = 0.8`, `step.grass = step_grass`, `lips = none`, `step = ground`; `[Sounds.Ride]` and
  `[Sounds.Cart]` for one style), and a mount's own in `[Mount.Sounds]` (and its style sections) of the declaring
  skin's cfg, shared by every skin of the mount. A skin's line wins over its mount's, a style block over the
  both-styles one, and the player's switches and volumes scale whatever won. The files are loaded when first needed
  (an embedded `.ogg` through a cache file under BepInEx\cache) and registered as game sounds, so they play through
  the game's own player and follow its Sounds slider; a missing or bad file is named once and the layer below
  applies. The Mounts tab's auto-save keeps the sections; `tests/SkinsScan` and `canonical_cfg.py` know them.
  Self-test "a pack's own sounds" (group Sounds).
- **Each mount's own speed and cargo** (breaking, pre-release): `MountDef.Speed` (`float?`, times the keeper's
  walking speed, 0.5 to 4; null = the donkey's, the API cfg's `SpeedMultiplier`) and a pack's `[Mount] speed =`, over
  a `SpeedMultiplier` in the mount's tuning; with the cart it is times the donkey's cart-to-riding ratio (1.5 / 2 as
  shipped). `MountDef.Cargo` / `[Mount] cargo =` (`int?`, 1 to 6, null = 6): what its cart carries, a stat only for
  now. `MountInfo.Speed`, `CartSpeed` (the derived one), `SpeedWord` (Very slow, Slow, Normal, Fast, Very fast against
  the donkey's 2), `Cargo`. A `[Mount.Stats]` or `MountStat` line named Speed or Cargo is left out (the API writes
  those). **Removed:** `MountCompanion.RideSpeed`,
  `CartSpeed`, `SpeedLabels`, `SpeedLabelsFor` and the `MountSpeed` enum. New `MountSettingsPage.RetiredKey(section,
  key)`: a setting dropped on purpose leaves the player's file at Publish and the upgrade check counts it as retired,
  not lost (Saddle Up's RideSpeed and CartSpeed). The built-in mounts' speeds: rat 2.75, Gerry 2.25, dragon 1.6,
  worm 1.75, snail 1.25, every one cargo 6; SkinsScan wants both on every built-in mount and refuses a
  `SpeedMultiplier` in its tuning. docs/skins.md, docs/api.md.
- **Babies on foot:** the Shuffle key `ShuffleFollowKeeperOnFoot` (off in the core; a companion sets it on its own
  foals only), `MountCompanion.FoalsFollowOnFoot`, and `FoalRows` adds `FoalsFollow` ("Babies follow you on foot").
  The row defaults to the companion's `FoalsFollowOnFoot` (Saddle Up sets it on first; the core stays off).
  `MovedKey` takes several old places for one key, a call each (before, the last call won): Saddle Up's babies name
  both Mount and Donkey. docs/foals.md "Following the keeper on foot".
- **The Behaviour key `FlyIndoors`** (API 0.36.0, off): on, a flying mount may take off and fly indoors, as a lift
  only; off, no take-off indoors and a flight through a building's door lands there. Saddle Up keeps it off, with no row.
- **Saddle Up needs GK2 Donkey API 0.36.0** (was 0.35.0: it uses `CartIndoors`, `Mounts.Catalog` and registers
  Hellfire's effect itself); an older API gets the clear "too old" message instead of failing part-way.
- **`CartIndoors`** (a Behaviour key, off): with `DismountIndoors` off, the cart comes indoors only with it on; off,
  a cart ride through a building's door leaves the cart standing at the door (an unhitch, `Unhitched` raised) and
  goes on as a ride; at the morgue with a corpse the keeper gets off inside and takes it into his hands
  (`Dismounted` with "morgue"). The cart is refused inside ("Not the cart indoors."). A summoned mount
  (`TryMount`), or a cart that can't be left there, gets off at the door as before.
  Before, `DismountIndoors` off let the cart in too: set both for that. Saddle Up no longer pins `DismountIndoors`:
  its new rows set both (docs/indoors.md).
- **The API ships no skin** (the user, 2026-10-21: the backend, not bloated with skins). Hellfire, Royal Parade and the
  Hellfire effect moved from GK2 Donkey API to GK2 Saddle Up (`src/SaddleUp/assets/skins`, the effect registered in
  Saddle Up's Awake through the public API, the same definition). Same ids, so a player's `Skin_donkey = hellfire`
  still resolves; the owner is Saddle Up's GUID now. The self-tests borrow Royal Parade's pictures from Saddle Up's
  DLL; the skins scan reads the shipped effect ids from Saddle Up; the generators write into Saddle Up's folder; a
  new skin goes to Saddle Up by default (`new_skin.py`). (2026-10-27: the two skins and the effect moved on into the
  Donkey Skins pack under its prefix; `hellfire` no longer resolves; the generators and `new_skin.py --pack` write
  there, Development below.)
- **docs/making-a-mount.md**: the one-page guide to a mod's own mount, both shapes (a pack embedded in a DLL with
  one line in Awake; a mount in code with an ability), what the API does for it unasked, and the repo's own build
  steps; linked from the READMEs, api.md, skins.md and mounts-as-mods.md.
- **One word for one thing: a mount is a mount** (API 0.36.0; the user, 2026-10-21: the code said "kind"). Every
  name that meant the mount says mount now: `MountDef`, `MountInfo`, `Mounts.Catalog` (`All`, `Find`, `Register`,
  `MountsChanged`), `MountHandle.Mount`, `MountEffects.FromMount`; inside, the registry is `Catalog` with its
  `Entry` per mount, and every `kind` in a name, a comment or a log line is `mount`. The settings rows' type
  is `RowKind` (Toggle, Choice, Key...), so the word has one meaning left. In a skin.cfg the colour mode is
  `colours = own|palette` (was `kind =`; not public yet, the seven cfgs and the tools follow). Cargo's item
  kinds and the dependency manifest's member kinds are other things and keep their word. A pre-1.0 breaking
  change for mods on the API (docs/api-design.md); Saddle Up and the example rebuilt on it.
- **The Mounts panel is the API's**: `MountSettingsPage.PanelButton(section, label, description)` (`LookButton`
  is the same) and `OpenPanel()` for a key of your own. Rows marked `look: true` (the Mount and Skin rows, the
  colours, the effect) are no longer handed to the Mods menu, the game's Settings window or the menu labels: the
  panel shows them for the chosen mount, built on choice (thirty installed mounts cost thirty list entries, not
  thirty sets of rows). The panel reads the registry: a mount's and skin's `description`, `portrait` (drawn under
  the mount's name), `[Mount.Stats]`, its cart, babies, speed and effect, and its abilities by `Name` and `Description`.
- **`WalkCycleSpeed`** (`[Ride]` / `[Cart]`, 1.5): the walk clip's rate per unit of walking speed, a mount key;
  **the canvas is at most 256**; a mount asleep releases its sheet (`Looks.Rig.Sleep`) and sheets build from the
  API's own tick, so a sheet whose wearers all sleep no longer stalls; **game-source skins are verified at
  game-ready** (`Looks.VerifyGameSources`: the frames the `[Game]` section names must be there, else the skin is set
  aside with a warning; memory and the sprite atlases are searched together; no frames at all refuses the sheet
  so the donkey shows instead of an invisible mount).
- **A value on an instance belongs to the owner that set it** (API 0.35.0, docs/mounts-foundation-2.md 2.1):
  `handle.Tuning.Set` by another owner is refused (the value in effect is returned, `TrySet` says so, one Info
  line), `Clear` takes the owner (`Clear(ownerId, key)`) and clears only its own; the mount's own values (the mount's
  merge) stay anyone's to replace, and return when cleared. The session layer (`Mounts.Tuning(style).Set`,
  `Behaviour.Set`...) follows the same rule. What a mount may pin through `[Mount.Tuning]` or a code mount's
  `Tuning` is now a rule: the ride and cart families (speed, seat, pose, rider motion, cart geometry, sounds) and
  the shuffle's values, never a behaviour switch, the lantern or a look key (refused, said once). An ability's
  values are set under an owner the API gives it (`MountAbility.Owner`, "<the mount's owner>.ability.<mount id>")
  and are cleared when the instance stops being that mount, whether the ability cleared them or not. A page row
  can say which tuning key it sets (`MountSettingsPage.TuningKey`, `PinnedBy`): it is left out of the Mods menu
  and the game's Settings window while the companion's chosen mount pins that key. Overrides in the side file whose
  owner isn't loaded are set aside and written back, not applied. The example ability gallops instead of
  burning (an effect is the player's choice; a mount that burns declares `effect = hellfire`).
- **Ids and a mount's facts** (docs/mounts-foundation-2.md 2.2): the skin whose id is the mount's (its Default)
  owns the `[Mount]` declaration and re-declares it over a pack that declared the id first (the first pack's skin
  joins); a later declaration that differs says its facts and tuning are not used; the same skin id from two
  places: the higher `version` wins, a tie goes to the DLL over a folder, the log names the loser; each
  `gk2skins` folder is an owner of its own, scanned in name order; an id from another mod without an author
  prefix (`author.name`) is noted once; a reload whose `[Mount] id` changed withdraws the old mount; the Mounts
  tab saves the mount's rows into the declaring skin's file only, and stamps the file so the auto-reload doesn't
  read it back.
- **The pack format for the Mounts panel** (docs/mounts-foundation-2.md 3): `[Skin] description` and `portrait`
  (a PNG next to the frames), `[Mount] description` and `portrait`, a `[Mount.Stats]` section of lines shown as
  given, `MountAbility.Name` and `Description`; on the API `MountSkinDef.Description` / `Portrait`,
  `MountDef.Description` / `Portrait` / `Stats` (`MountStat`), `MountSkinInfo.Description` / `HasPortrait`,
  `MountInfo.Description` / `HasPortrait` / `Stats` / `AbilityInfos` (`MountAbilityInfo`). `[Skin] needs =
  <API version>`: an older API refuses the pack with the reason instead of drawing it with the keys it doesn't
  know left out; a `format` that is not a number is refused; a `[Game]` line that names no frame key is reported.
  The scan requires a description on every shipped skin and mount.
- **A mount or skin registered in any plugin's Awake reaches a settings page** (API 0.34.0, docs/mounts-as-mods.md
  4). `MountSettingsPage.Publish` now shows the page the frame after it is called, when every plugin's Awake has
  run: the Mount and Skin rows and the per-mount colour rows (`MountRows`, `PerMount`) are made for the mounts
  registered meanwhile too (BepInEx orders plugins by their dependencies only, and a mount mod can't ask to load
  before the mod that owns the page). Before, a mount from a mod loaded after Saddle Up had no rows for the whole
  session and no restart changed it. Self-tested: a pack declaring a mount after the rows were built gets its
  Skin row and coat row at the catch-up; every page shown this session has a Skin row for every mount.
- **A set skin or effect that isn't installed is an Info line, not a warning** ("its mod or pack isn't loaded;
  the choice is kept and comes back with it"): a player who removed a mount mod has nothing to fix.
- **Shipping a mount in a mod**: Saddle Up is the worked example (docs/mounts-as-mods.md): its five mounts are
  packs embedded by one csproj line and registered by one `Mounts.Looks.RegisterEmbedded` call in Awake, no other
  code; a loose `gk2skins\<id>\` folder under `plugins\` needs no code at all. A dev build may record the packs'
  source folder as the DLL's `GK2SkinsSourceDir` assembly metadata, and the F11 Mounts tab then writes a skin's
  tuned values back into its skin.cfg as it does for the API's own.
- **Skin packs: an erase colour for skins drawn over the donkey** (docs/skins.md "Erasing from the donkey"). A
  pixel of `erase = FF00FF` (magenta by default for `layer = over`; `none` switches it off) makes the donkey
  transparent there, so an over pack can take the ears off under horns or the tail off under another. Such a picture
  is built whole when first worn (the donkey's frame from the game with the holes, the picture's pixels on top) and
  drawn in the body's place; pictures without it stay plain overlays. Nothing of the game's art goes into the pack.
  The pack checker and the review page honour it; a self-test wears a pack that erases the ears. The step towards
  every skin being a pack (Hellfire next: the user, "for the concept of a mount skin we should do this drawing-over
  thing").
- **The skin tools, one pipeline** (the plan's M6; docs/sprite-tools.md "Making a skin: the one pipeline"):
  `tools/skins/new_skin.py` scaffolds a skin or a mount (the cfg in the canonical layout, a generator stub, the
  Aseprite sidecar, the scan); every generator writes its strips, preview and sheet through one module
  (`tools/skins/review.py`: the front layer composited, the rider on the seat row), so the four creatures have strips
  too; the colour rules of `skin_colors.py` are data (`tools/skins/creatures/colors.json`), the front layer counts and
  Gerry's `[Colors]` comes from the game's export; the Aseprite watcher splits the exported sheet into the built-in's
  per-frame PNGs; the checker knows the front layer and `source = game`, reads the donkey only under an over skin,
  judges lone pixels over what the game draws beneath (Hellfire's one-pixel lava crack), says a kept donkey colour
  and the colour cube instead of warning, and returns an exit code, so it runs in the release scans on every
  built-in; the scan reads pixels (the seat row must be drawn at the pivot column of the standing frame, the erase
  colour is refused in a replace skin, a sheet is a whole number of cells, the `[Game]` map's targets are frame
  names); the review page draws the front layer in front of the rider, the rider on the seat row with the skin's
  offset and the seat line, and the card's facts (seat, hitch gap, babies, colour regions); the Gerry scripts take
  the prefix and find the bundle by content; the creature generator wrote into `tools/src/...` since its move (fixed).
  Wording: the Riding tab says "mount"; the `Debug.MountsPreview` description, the csproj's skins comment and the
  export button's tooltip say what is there now.
- **After the review of the parity work** (three independent reads of the diff, 2026-10-20): the side file's record
  label read the owner column instead of the key, so every kept mount was labelled "donkey" in the log (the data was
  right); the Gerry self-test still expected "gerry" as the noun; a refused unhitch of a mount that became a no-cart
  mount was tried again every tick with two log lines each (now once a second, said once, and run after the rig's own
  work); the layered value a hitch reads took the wanted skin's geometry while the drawing applied the worn one (both
  worn now); a mount without babies of its own gave its `foal_scale` to plain baby donkeys (0.6 now); spawning a
  mount by name that has no skin spawned a donkey (refused); the noun reaches every refusal and log sentence; a code
  skin's sprite colours copied its texture once per sprite (once per texture); the settings picture read the hitch
  gap from the layers every frame (when they change); a page's grey condition composes with a row's own, and Saddle
  Up greys the custom harness row with the harness row. Tools: the checker counts magenta in a replace skin (it
  never did), survives a cfg the canonical writer refuses, says that a front picture can't erase; the canonical
  writer refuses `[Mount.Tuning]` without `[Mount]` (it dropped it) and leaves an empty `[Tuning]` out; the colours
  writer no longer eats the comment of the section after `[Colors]`; the shared review module clears erase holes at
  the right place on a bigger canvas, lays the sheet out by frame order, and puts the rider where the skin's own
  `RiderOffsetPx` / `SeatAdjustPx` put him (the page too, and it re-places him when the scale changes); the watcher
  splits by the sidecar's columns and removes a front picture whose cell is empty; the scan says which PNG it cannot
  read instead of aborting, wants RGBA, and checks the order among the three skin tuning sections; `make_hellfire.py
  --cfg` keeps the effect keys; a fresh creature cfg is canonical; Royal Parade's generator writes its strips through
  the shared module (its Aseprite sidecar keeps naming its `out\pack`: the generator is its source); `new_skin.py`
  takes its flags in any order and refuses `--over` with `--mount`.
- **After the second run** (2026-10-20): two findings in the game, both fixed. The cart-style seat sat 6 px higher
  than the ride seat on every drawn creature: a declared seat row still added the player's own `SeatAdjustPx`, whose
  mod default is -6 riding and 0 with the cart. A declared seat now ignores the player's own value (only the skin's
  own `[Tuning] SeatAdjustPx` or a mod's override nudges from it, in full), and the built-in rows moved by those 6
  (rat and worm 52, the snail 55, the dragon 68; the donkey's own seat is row 52 on the 96 canvas; the scaffold, the
  review tools and the example say 52), so the riding look is exactly as calibrated and the cart look matches it. The
  dragon's near wing (the front layer) was drawn behind the rider in the world: it was sorted above him but not lifted
  past his sprite's plane, and the game draws by distance; the front picture is now lifted past the nearest of the
  keeper's sprites (measured each frame in the body part's space) by `Looks.Rig.FrontLift`, 0.05 world units (2.5
  art pixels; the first lift was a fifth of a pixel, which tied in the depth buffer and flickered his shirt through
  the wing while he moved, seen 2026-10-20; the front-layer test now moves him 40 frames and counts the pixels lost
  at the shipped lift and at the old one). A scenario watches the settings picture after a mount switch frame by
  frame (when the worn skin flips, when the stage applies the new hitch gap, when the picture is reframed) and
  fails if the gap lags the skin by more than two frames: the user saw the cart take many frames to settle
  (2026-10-20). Another wears every built-in creature with the Coat colorized and checks that every Coat colour
  changed and no colour of another region did (the audit of every skin's regions is in docs/mount-colours.md). The cart readout's rule was wrong, not the gaps:
  the rat's and the dragon's declared gaps (judged by eye) leave their carts about 10 px further from the body than
  the donkey's, and the snail, worm and Gerry stand further still without any gap (narrower bodies), so the rule
  asserted is "no cart closer to its body than the donkey's"; the three declare no gap and their cfgs say why
  (docs/seat-investigation.md 9, with the measured table). The Riding tab no longer offers the player's nudge on a
  declared seat. (Third run: Gerry sat at 41.5 px instead of 25.5: a skin's own nudge is an override on the instance,
  which the global override flag did not see; the seat reads the instance's override owners too, and the readout
  now guards Gerry's 25.5. Fourth run: all green but the "pictures loaded and freed" scenario, which assumed nobody
  wears the rat: the player's own mount was the rat, whose one sheet is shared; it now picks a built-in creature
  nobody wears.)
- **The dragon's wings are folded** (the user, 2026-10-20, after riding it: the raised near wing, drawn in front
  of him, hid the rider), drawn as the user's reference sheets draw a resting dragon: the wrist is a tall peak above
  the back with the thumb claw hooking towards the head, the fingers run down from the peak as red lines, and the
  membrane hangs between them like a curtain to the ground behind the hindquarters, scalloped along its hem, wine
  against the body's red and outlined. Both wings root at the shoulder, where wings attach and where the rider
  sits: the peak stands above the shoulder and behind his head, the curtain hangs from it back over the flank, its
  lower edge along the belly line with the belly band and both hind legs in view under it, its tip pointing back
  along the tail over the rump (the blue references' long wedge; the user's mark), nothing on the ground
  (the references tuck the wing so when the dragon walks; a curtain to the ground, the big idle pose's, had to
  swallow the hind legs and sat in the drawn ground, and once its hem was pinned to the ground row under a
  bobbing, swaying peak the whole wing jiggled, the user's video 2026-10-20: now every point of the wing takes
  the body's bob and nothing sways), nothing of the wing forward of the peak. The whole
  method, the game's placing of the layer, the rider's measures on the canvas and the recipe for the next mount
  are in docs/front-layer.md. The tail is redrawn as the references trail it: tapering steadily from the hips to
  the ground, lying low with its tip on the feet's row, tapering on into a spike that ends two columns in from
  the canvas edge (the old tail kept one thickness to a kink up into an arrowhead that was widest at its far end
  and ran past the edge: a sausage with a cut-off tip; a diamond spade in between read as a hanging lantern), and
  its spines seated on the drawn top edge, two columns wide
  like the ridge's (placed from the curve's radius they floated a row above the edge and read as notches with a
  hole under each). The whole near wing is the front layer, over the
  rider wherever the two overlap: he sits inside the fold with the top of his head, part of his face and the
  reins arm out of it. (Tried on the way and rejected: a cut that kept his chest in view by drawing the membrane
  behind him there, and a one-layer wing shaped around him, which grew a lobe under his seat that stood out
  like a thumb when nobody rode.)
  The far wing is the same fold four pixels back and five up (the perspective), drawn behind the
  body, which hides all but its peak and the top of its curtain behind the near one's. (The user's corrections on
  the way: a curtain at the tail, one too high, a far wing of its own shape, and one "back on his ass" when the root
  sat at the mid-back.) (Eight drafts went before it: flat folds and fans along the flank read as a plank, a
  line, a blanket, a flag, a hand fan; the reference read at pixel size was what was missing; the dragon is compared
  with the reference crop at one scale before each hand-over now.) The front-layer readback takes a curtain that
  hangs behind the rider (the order is judged on the body). Two readouts joined the self-tests after that look: the rider's hip as a row of
  the worn skin's canvas (asserted on the dragon's declared 68, which the review strips draw him on) and every cart
  part's shift along the hitch direction (a rigid cart: the far wheel seen beside the near one in the Mods picture
  is to be judged with it). The settings picture no longer moves a cart part's mesh child a second time (the far
  wheel's child is named like its part; the world's cart skipped them already).
- **After the first in-game run of the new tests** (2026-10-20): three probes were wrong, the game was right. The
  cart readout asked a cart part's transform for its renderer (the mesh is a child, as the mount code reads it); the
  front-layer readback counted every drawn pixel, so "the body with the rider" could never be smaller than "the body
  alone" (it now compares the overlap pixel by pixel at one camera spot: whose colour shows); the over-colours test
  classed the scene-lit gold as "red" at dusk (it now checks that the lit red colour is gone, whatever class the
  light gives the gold).
- **The tests nobody had** (the plan's M5, docs/mounts-parity-matrix.md §6): the dragon's front layer read back
  in front of the rider (where they overlap, the pair shows the keeper's colour over the body overlay and the wing's over the keeper); Hellfire and Royal Parade
  on the built-ins themselves (whole frames with the erase marks, the ash coat, the cloth's reds under the harness
  colour, both walking); the snail's own rider offset on its wearer; the cart-style seat and the hitch gap of every
  mount (M2's readout).
- **The code that only knew the donkey** (the plan's M4, docs/mounts-parity-matrix.md §2): the rider's leg overlay
  (NearLeg) hangs from the skin's declared seat as the torso does, not from the donkey's back line; a baby spawned
  without a size is its parent mount's own (`SpawnFoal`'s default), and plain baby donkeys are 0.6 whatever the
  parent is; `Mounts.Spawn` with a cart as a no-cart mount is refused, and a hitched mount that becomes one leaves
  its cart standing where it is (the user's decision; before, the cart stayed hitched as data); `[Base.*]` on a
  replace skin and `[Colors]` on a palette skin are named in the skin's report instead of ignored silently; a mount
  may say `noun = ` ("a skull can't pull the cart"; Gerry says skull) and the save file's and the dormant list's
  labels say the mount instead of "donkey"; a mount with no skin is not offered; `MountSpawn.Mount` spawns a mount
  by name in its Default skin. The API records say more: `MountSkinInfo.Regions` (the colour regions the player's
  colours reach), `MountInfo.EffectWhen`, `Noun` and its tuning, `Skins` with the Default first; a code skin
  can declare its colour regions, an erase colour, a base recolour and the picture seat (`MountSkinDef.Regions`,
  `Erase`, `BaseRegions`, `SeatFromPicture`), and a readable sprite's colours join the look (an unreadable one is
  said once). `MountSettingsPage.EnableWhen` and `MountHasRegion` grey a colour row for a region a mount never has.
  Sounds, phase one: every sound key is documented as a mount's to set; the snail and the worm are silent (bray,
  hoofsteps, lip smack off), Gerry's lip smack is off; the scan reads every bound key (the helper-bound ones were
  invisible to it). Self-tests: the two cart-rule doors, the noun, the silent snail and skull, the baby's default
  size, the leg overlay on the snail's seat row, the snail's regions.
- **A skin comes with its effect; the ring fits the creature** (the plan's M3; the user's decision 9.5 (a)): a
  skin may say `effect = ` / `effect_when = ` (`MountSkinDef.Effect` / `EffectWhen` for code), shown when the
  player's Effect row says "As the mount or skin" (its default; the entry was "As the mount"): the skin's first,
  else the mount's. Hellfire says `effect = hellfire`, so a player who picks the skin sees the horns and the fire
  together without opening the Effect section. The effect ring's radius scales by the worn skin's standing-frame
  width over the donkey's 46 px (the rat's 85: almost twice the ring; an over skin or a code-sprite skin keeps
  1.0), measured at the sheet build. The Mounts tab has the skin's effect row; the scan and the layout know the
  two keys; `make_hellfire.py` writes the built-in's cfg once (`--cfg` rewrites it), so a regeneration keeps the
  hand-written lines. Self-test: Hellfire lit under the default row, Royal Parade not, the rat's ring 1.48.
- **The Mounts tab tunes the skin** (the plan's M2): a "The rider's seat on this skin" row (the picture row, the
  picture top, or none; right-click = none) and the rider, leg and cart pixel rows per style under "Rider on this
  skin" (a value already in `[Tuning]` is changed there, a new one goes into the style's section; right-click = not
  set), saved by the same auto-save into the skin's file; the mount's group keeps the speed, NearLeg and its
  other keys. The Riding tab shows the pixel nudge alone while riding a skin that declares its seat, and says why.
  The settings picture places the cart at the mount's hitch gap (the dragon's and rat's cart stood at the
  donkey's distance) and shows no cart for a mount that cannot pull one. Creature facts (the mount's cart rule,
  babies, effect, abilities) follow the wanted skin at once while the drawing's geometry follows the worn one, so
  a rider on the old picture never takes the new picture's numbers. A "cart readout" self-test measures, for every
  built-in mount with a cart, the distance from the body's rear edge to the cart body and the cart-style seat,
  and asserts the rat's and dragon's declared gaps; the snail's, worm's and Gerry's gaps are declared from its
  numbers next.
- **The skin owns its drawing's geometry** (API 0.33.0; the user's decision of 2026-10-20, docs/mounts-parity-matrix.md
  9.1): a skin's `[Tuning]` has `[Tuning.Ride]` and `[Tuning.Cart]` beside it for one style, merged after it and
  before nothing (the merge order is in docs/mounts-design.md §2; `Looks.LayeredValue` reads the same order for the
  hitch); `MountSkinDef.TuningRide` / `TuningCart` for code skins. The three pixel values that sat on mounts moved
  to their skins: the snail's `RiderOffsetPx` into `[Tuning.Ride]`, Gerry's `SeatAdjustPx` into `[Tuning]`, the rat's
  and the dragon's `HitchGapPx` into `[Tuning.Cart]`; the creature generator writes a creature's gap; the scan
  refuses a picture key under `[Mount.Tuning*]`, a key in `[Tuning]` and a style section, `SeatOffset` on a skin
  with a seat row, an empty section, and places the sections after `[Game]`. The Mounts tab's Save writes the three
  sections with their comments and regenerates the `seat` line with its remark. `MountSkinInfo` says the seat row,
  the picture seat, a front layer and a game source. Self-tests: Gerry's value comes from its skin and the mount
  holds no pixel key after a reload; a temp pack's `[Tuning.Ride]` value is the riding value on the wearer.
- **A mount's seat is its skin's row now** (API 0.32.0; the user: "ok let's go for it since it's part of the
  foundation for more mounts"; docs/seat-investigation.md §8). A skin may say `seat = <row>`, the canvas row (from
  the top) the rider's hip sits on; `MountSkinDef.SeatRow` for code skins. The rider sits there in every frame for
  every player, whatever their AutoSeat setting (the picture path Gerry uses, generalised, with the walk bob), and
  the mount's `SeatAdjustPx` nudges from it, so one declaration gives every player the same seat; before, a mount's
  `SeatOffset` reached only AutoSeat-off players and its `SeatAdjustPx` only AutoSeat-on ones. The rat and the worm
  say 46 (the donkey's own seat on the 96 canvas, measured in game: the first guess of 49 sat 3 px low, with
  both modes already equal), the snail 49 (3 px lower, its `SeatOffset` gone; a stray `SeatAdjustPx = -7` from a panel test, committed by accident, hid one pixel in the second run and is gone), the dragon 62 (the 128 canvas); the creature generator writes the row; the scan refuses a row outside the
  canvas and a mount that sets `SeatOffset`; the example mod's checker says 46. The game's own donkey art declares
  no row: a default player's seat is untouched (the readout self-test asserts 6.6 px). The Mounts tab says when a
  skin declares its row.
- **A built-in's reload and undo on the Mounts tab never took** (the user, 2026-10-20: "I changed torso down to
  -4 and pressed undo and it stayed yellow"; the note said "Not reloaded: Value cannot be null. Parameter name:
  path1"): the reload stamps the pack's folder for the auto-reload watch, and an embedded built-in has no folder,
  so it threw before the file was read and the mount kept the panel's value. The stamp is 0 for a built-in or a
  code skin now; the Gerry self-test reloads a built-in from the repo's cfg; the Undo and Reload buttons log
  their outcome, so a failure is in the log and not only in a five-second note. Undo is one step per gesture now:
  saves within four seconds of each other on the same file share the earlier restore point (a slider dragged in
  two moves with a pause took two presses).
- **The mount definitions brought to one shape** (the user: "take a look at the mounts and how we're defining
  them so that we have the same parity"; the investigation is `docs/mounts-foundation-audit.md`). Every built-in
  `skin.cfg` is in one layout now, written by `tools/skins/canonical_cfg.py` and refused otherwise by
  `tests/SkinsScan` and the release scans: `[Skin]`, `[Game]` for a game creature, the colour sections, and the
  `[Mount]` block last with id, name, author, version, cart and foals always (foal_scale, effect, effect_when when
  not the default), every tuning key in one section. Gerry's dead `SeatAdjustPx = 10` is gone (its one value, -16,
  in `[Mount.Tuning]`); the dragon and the worm say `cart` and `foals`; every mount has an author and a version.
  The loader: a `[Mount]` without a name takes its id (sentences and the dev spawn row use the mount's label, so a
  nameless mount never says "donkey" or throws); a built-in skin's mount is built in (`MountInfo.IsBuiltIn`);
  author and version survive a reload and count for "the same declaration"; unknown `[Mount]` keys, a bad
  `foal_scale` and a bad `effect_when` are named in the skin's report like every other section's. The Mounts tab's
  Save keeps the comments inside the mount sections (above a key, on its line, before and after the keys) and
  writes the block in the file's order. SkinsScan checks every `[Skin]` value the loader reads by name (layer, mount,
  seat, source, erase, the canvas range), the section order, `[Base.*]` regions and knobs, the `[Mount]` key list and
  values (foal_scale 0.2-1 and not the default, effect a built-in id with its effect_when, `Off`/`Night`/`Always`),
  a key set in both `[Mount.Tuning]` and a style section, an empty tuning section, a `[Mount.Tuning]` without
  `[Mount]`; probed with a wrong cfg (11 findings). Self-tests: Gerry's one seat value and its built-in mount with
  author and version; the pack round trip keeps four comments in the mount sections and the author; an ability that
  throws is off after its first tick while the mount's other goes on. Docs: `effect_when` takes `Off`, not "Never";
  `pivot` and the `[Mount]` author/version documented; the donkey's own block written out next to the five; the
  Mount list and "Default" rule; the stale "Saddle Up's Donkey rows", recipe and blue-dragon lines in api.md,
  reference.md, mount-colours.md, mounts-design.md and skins-design.md marked or fixed. The skin tooling is all
  under `tools/skins/` now (`creatures/make_creatures.py`, `gerry/`, `skin_colors.py`, `skin_preview_html.py`,
  `pngcolors.py`). Open: a mount's seat with the player's AutoSeat off (audit 3.1): `docs/seat-investigation.md`
  has the three seat paths, what every built-in gets under the shipped defaults (the snail's `SeatOffset` works for
  a default player and is lost with AutoSeat on; a mount's `SeatAdjustPx` does nothing for a default player unless
  the skin says `seat = picture`), the options and the compat constraint; a readout self-test ("seat: the rider's
  height on every built-in mount, AutoSeat off and on") logs the seat in pixels for each of the eight looks in both
  modes; docs/reference.md's seat defaults corrected to the shipped values (`SeatOffset` 0.22 both styles,
  `RiderOffsetPx` -6 / -8). Measured in game 2026-10-20: every look sits at 6.6 px with AutoSeat off and 11.5 px
  with it on (the player's two modes disagree by 5 px on the plain donkey), the snail at 3.0 px off and 11.5 on
  (its seat lost in auto mode), Gerry at 25.5 px in both (the picture seat). The seat code is unchanged; the user
  decides. The whole "Skins and effects" group passed in game the same day (16 scenarios, the three warnings the
  tests announce themselves).
- **The creature generator on the same pipeline** (the user: "so now all the skins are on the same level?"):
  `tools/creature_sprites.py` writes the rat, snail, worm and dragon straight into `assets/skins/<name>/` (one name
  as the argument redraws one; the probe's `test\skins\` output and the Blue Dragon entry are gone); all 85 + 3 x 44
  pictures came out byte-identical. Every built-in skin now comes from a generator in the repo writing into its
  `assets/skins/<id>/` folder; Gerry alone has no pictures of its own.
- **Skin leftovers cleared** (the user: "check dirs to see if there's anything obsolete ... so we keep parity"):
  `tools/skin_recipes.py` (the retired recolour recipes' prototype) and `horn_bases_raw.json` are gone from the
  repo; `contact.py` lost `load_raw` / `raw_to_game_shift` and `hellfire_proto.py` reads the probe's frames, so no
  tool reads the old top-left-aligned canvas any more (`horn_bases.py` measures the Hellfire horn bases from the
  Hellfire pack's own bone pixels: 44 of 44 frames the same as the moved v14 positions). Off the dev machine: the
  probe's `test\skins\` experiments, the example mod and the `worm-pack` test pack in the game's plugins, the
  Hellfire review rounds, the raw frame exports, the starter `.aseprite`. `pngcolors.py`'s note names its real user.
- **One pipeline for every skin** (the user: "we need to make them the same since they will both be included",
  "once we set up this pipeline we can keep following it to create more skins for other mounts too"): Royal Parade
  is a built-in pack like Hellfire now, `src/GK2DonkeyAPI/assets/skins/royal-parade/` with `skin.cfg` by hand and
  a PNG per frame written by `tools/skins/royal-parade/make_royal.py` (`--no-assets` for a dry run); the sheet
  form, the `packs/` folder and the copy into the game's `gk2skins\` are gone (the loose copy there was removed:
  two skins with one id are refused). The pipeline is written down in `docs/sprite-tools.md` "Making a skin: the
  one pipeline": generator folder, pack folder, checker + scan + review page, build and deploy, the in-game export.
  `tools/skins/check_skin.py` reads a PNG per frame as well as a sheet. The headless scan passes 17 of 17 with
  Royal Parade among the built-ins.
- **Hellfire is a pack now, not code** (the user: "for the concept of a mount skin we should do this drawing-over
  thing"): `src/GK2DonkeyAPI/assets/skins/hellfire/`, generated by `tools/skins/hellfire/make_hellfire.py` from the
  reviewed prototype: the prototype runs over each of the donkey's frames on the game's canvas and the pack keeps
  only what it changed (horns, the devil's tail, the rump fill, the cracks, the glowing eyes) plus erase marks where
  it took the ears and the tail off; the ash coat is the pack's `[Base.Coat]` recolour. `LooksHellfire.cs` (614
  lines) is gone; the recipe machinery stays for mods (the self-test registers a recipe of its own through the API).
  Same colour regions as before (the cracks follow the muzzle colour, horns and tail the dark parts, the glow the eye).
  Proven the same: `tools/skins/hellfire/compare_hellfire.py` puts every code-drawn frame next to the pack over the
  donkey with a difference picture and count; 44 of 44 identical with the prototype's coat (the devil's tail's sway
  per walk frame matched to the reviewed frames), and with the game's coat rule only coat shades differ: the rule's
  knobs were fitted to the code's darkening formula over the donkey's 14 coat colours (Brightness 0.41, Colorize
  FF4747 at 0.6), the largest difference left 5 of 255 on one channel. The ear remnant the user saw between the
  horns on the page was the horns' own outline, drawn the same by the code; the head pixels are identical.
  What did differ frame to frame (the user: "in some frames there's a gap between the horns, in others it's
  filled") was the mane's tuft that stood between the ears: one column in the standing frames, a block three wide
  where the head bobs (walk 3/4/7/8, the turn). Erasing it left a hole in his head (the user: "it should be
  filled"), so the generator now draws the same mane tuft between the horns in every frame: the four rows above the
  horns' bases, in the donkey's own outline and mane shades (region Dark, so the player's colour reaches them as it
  reaches the mane), 326 pixels added and nothing erased; the compare page shows exactly those 326 pixels as the
  only difference from the code's output, checked on a zoom of all 44 heads through the review page's path.
- **Skin packs: a recolour of the donkey under an over skin** (docs/skins.md "Recolouring the donkey under the
  skin"): `[Base.Coat]`, `[Base.Eye]` ... sections with the player's own knobs (Brightness, Saturation, Colorize,
  Tint, Overlay ...), applied before the player's settings and holding even with them switched off. An ash coat
  under horns without a pixel of the game's art. The erase self-test's pack declares a blue coat and the readback
  checks it.
- GK2 Donkey API 0.31.0: `MountSettingsPage.Heading(section, label, description = "")`, a heading row inside a
  section (a line with no value); in the look bar the look rows after it in that section stand under it in a group
  of their own. A `Color` row whose colour key has a `MovedKey` carries the alias to its `<key>Lightness` key by
  itself, so one `MovedKey` per colour is enough. The look bar deals its groups to the column that leaves the left
  closest to half the rows (before: to the left until the left held half, which with one big section put nearly
  everything there). A row may not share a picture row's (`Preview`) label: the picture is found on screen by it,
  and a heading with the same label got a picture of its own; the page refuses the row with a clear message.

Development:

- **The packs are `GK2SaddleUpDragonsMountPack`, `GK2SaddleUpCrittersMountPack` and `GK2SaddleUpDonkeySkinPack`**
  (the user, 2026-10-28): one name each for the DLL, its plugin folder, its source folder under `packs/`, its csproj,
  namespace and resource prefix, and its zip (`GK2-Saddle-Up-Dragons-Mount-Pack-<v>.zip`, `...-Critters-Mount-Pack-`,
  `...-Donkey-Skin-Pack-`). The packs' GUIDs, display names and mount, skin and effect ids are unchanged: a pack's id
  prefix is its GUID's part after `dreas.gk2.` (`saddleup-dragon.`), which SkinsScan and `tools/skins/skinroots.py`
  now read from its Plugin.cs instead of taking the folder's name.
- **The self-test's colour read-backs are lit at noon, and its panel scenario keeps the player's cart** (2026-10-27,
  a group run at night with the donkey and cart chosen): `RenderAlone` holds `PreviewStage.Midday` (now internal) for
  its render, so the over-skin colours and the front-layer probe read the same by night (the lit red read as blue, the
  keeper's dark colours collapsed into the wing's); the panel scenario's two-frame commit of another mount picks one
  that pulls the cart when the companion's does (the cart rule would leave the player's cart at the parked spot);
  the end-of-run world comparison counts a mod's foals per owner instead of by id (a mount switch remakes the extra
  ones).
- **The integration tests are a plugin of their own** (the user, 2026-10-27: the API knows nothing of Saddle Up):
  `tools/GK2DonkeyAPI.IntegrationTests/` (`GK2DonkeyAPI.IntegrationTests.dll`, `dreas.gk2.donkeyapi.integrationtests`)
  holds the in-game self-tests of Saddle Up and its packs. Dev-only and never shipped: its build doesn't deploy unless
  given `-p:Deploy=true` (game closed). At its start its scenarios join the API's runner (`SelfTest.AddScenarios`) and
  run from F11's Tests tab in the same groups, under the same start and finish; the API grants it its internals by
  name only (`src/GK2DonkeyAPI/Dev/AssemblyInfo.cs`, `InternalsVisibleTo("GK2DonkeyAPI.IntegrationTests")`). The API
  keeps 95 scenarios of its own; the plugin holds 33: 20 moved whole (the shipped skins and mounts: on, off, leaks,
  asleep, Gerry, the seat and cart readouts, the colours, the over skins, the effect hook, the front layer, the
  preview gap, "built in or external", cargo per mount, Saddle Up's Jump and Fly rows, the dragon's sound files, the
  garbage and walk surveys) and 13 split off mixed scenarios (Saddle Up's rows for babies, indoors, Freeplay, the safe
  spot and standing sounds, its keys over the panel, the upgrade of Saddle Up's released cfg; our mounts' speeds and
  flags, the dragon's jump, a standing snail's sounds, Saddle Up's babies' temperaments; the Donkey Skins pack's skins
  unlayered). The API's own use temp mounts and skins instead of our packs (the round trip a temp skin with a hitch
  gap, the settings rows a temp mount with babies and no cart, the skin pack the API's own test picture). The helpers
  `M`, `Sk`, `MissingPack` (which knows the Donkey Skins pack too) and `ShippedMounts` are the plugin's. The run holds
  `StandingSounds` (like Freeplay) and gives it back at the end. The plugin's "built in or external" checks the skins
  come from `GK2SaddleUpDonkeySkinPack.dll`, the effect is the pack's, Saddle Up's DLL has no donkey skin, Saddle Up's
  donkey Skin row offers both though they registered after its page was built, and the old ids find nothing.
  docs/self-tests.md.
- **The layering scan** (`tools/layering-scan.py`, in `tools/release-scans.sh` with `--dist`): fails on any name of
  Saddle Up or its packs in `src/GK2DonkeyAPI` (Dev included), on any pack's name in `src/SaddleUp`, and on such names
  in the API DLL's strings. The API's rename tables, comments and texts that named Saddle Up's content are gone or
  made generic for it.
- **The donkey's skins as the Donkey Skins pack** (`packs/GK2SaddleUpDonkeySkinPack/`: `gk2mounts/donkey/skins/<skin>/`, no
  `mount.cfg`; the study was docs/donkey-skins-pack.md): `tools/skins/new_skin.py` refuses a skin for a mount Saddle Up
  doesn't own (the donkey) into Saddle Up and asks for `--pack` (`GK2SaddleUpDonkeySkinPack`, or a new pack); a pack that
  exists gives its own prefix (read from its ids). The generators (`tools/skins/hellfire/make_hellfire.py`,
  `compare_hellfire.py`, `tools/skins/royal-parade/horn_bases.py`, `make_royal.py`) write into
  `packs/GK2SaddleUpDonkeySkinPack`. SkinsScan reads the shipped effect ids from every `src/*/Plugin.cs` and
  `packs/*/Plugin.cs` and checks a pack's effect carries the pack's prefix. `make-release-zip.py` names a pack's zip
  with dashed words (`GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip`) and writes no bundle: the All Mounts zip is gone (the user:
  "We dont need this combined packs files, wont be shipping them. ill ship individuals"). `check-dist.py`: Saddle Up's
  built-in content is Gerry.
- **SkinsScan reads the speed words from the API's table** (Catalog.cs `SpeedBands`) and fails a pack whose `speed` is
  not one of them; the shipped `mount.cfg`s and `new_skin.py`'s template say a word.
- **The hillside jump's probe** (Dev/SelfTest.Yard.cs, `Mount.Instance.TestGroundProbe`): the test records every frame
  of the jump (the body raw and smoothed, the smoothing mode, the sprite offset, the sample point, every measured height
  and the branch taken, the lift, the position before and after rounding) and writes them to the log when a step went
  past 2.5 px; its message names which condition failed and the frame's time. The yard's lines say the body's
  smoothing after each landing. The probe is a static the test alone sets; the mod never does.
- **Self-test "freeplay: max cargo and one speed for every mount"**: the words' one table (each band's speed reads back
  as its own word, riding and with the cart, the five numbers the shipped mounts', a word read in any case and a number
  refused), max cargo on the catalog, MountInfo, the panel's card and a hitched cart's
  slots (and back), the one speed ridden, driven and changed while riding (the donkey and a test mount, and back to
  their own), Saddle Up's Freeplay rows (the last section, the dropdown's words the API's, hidden with the switch off,
  each row driving its key). The whole run holds Freeplay off, so a player's Freeplay can't change what the other
  scenarios check.
- **The UI probe** (`Debug.UiProbe`, off by default; Dev/UiProbe.cs): when the Mounts panel is built it writes
  `BepInEx\GK2-ui-probe.txt`, the game's Settings window tree (rects, sprites and their slicing, fonts), the dialog
  button and the loaded buttons' fonts. It found the panel's face under the frame and the plain font on its buttons.
- **`General.CartSlots` is session-only**: bound in a config that is never written (`Plugin.sessionOnly`), its line
  retired from `dreas.gk2.donkeyapi.cfg`; a mod's `Mounts.Behaviour.Set("CartSlots", n)` still lowers every cart for
  its session (the Mounts panel then reads "2 of 6 items (a mod limits the cart to 2)"). Saddle Up retires its
  `CartSlots` under Cart and Behaviour. The 0.12.0 fixture made before this is set aside, to be captured again at the
  release rehearsal.
- **Pack DLL names come from the csproj** (`<AssemblyName>GK2<Pack>`): `build-dist.sh`, `make-release-zip.py` and
  `check-dist.py` read it (the repo folder `packs/<Pack>/`, the namespace, the resource prefix and the ids keep the
  pack's name). The self-tests "Built in or external" and the dragon's embedded sounds expect the GK2 names; the
  speeds test checks the card at Cart capacity 6 and 2.
- **The 0.12.0 fixture** (`tests/fixtures/released/0.12.0/`): the two cfgs the release zip's first run wrote, every
  value set off its default with the new `tools/fixture-flip.py`, `defaults.cfg`, and a side file (to be replaced
  by one with a parked loaded cart). The release rehearsal (first start and the upgrade over 0.10.0's files) is in
  docs/release-checklist.md section 5.
- **The jump probe is off by default** (the user, 2026-10-27: "these files are still being created"): its trace files
  under `BepInEx\GK2-jumps\` were written for every ridden jump whenever the dev tools were unlocked; now only with
  the new dev key `Debug.JumpTrace` on. Players never had it.
- **The mounts gate is removed:** `Debug.MountsPreview`, `Plugin.MountsOn`, `Mounts.KeptOut` and every "switched off"
  text (Saddle Up's description, its "Other mounts and skins" row, the Mount and Skin rows' descriptions, the log
  lines) are gone; the built-ins always register. A dev cfg's `MountsPreview` line leaves the file at start (one
  "retired [Debug] line" log line). The self-test "mounts off" is gone, and the skips "the mounts are off" with it.
- **Self-tests:** "cargo: each mount carries its own" (a cart switched donkey, rat, Gerry, donkey: 6, held at 4 with
  four aboard, 2, 4, Cart capacity 2 wins, 6, nothing dropped); the speeds test checks each built-in's cargo.
- **Built in or external** (docs/making-a-mount.md): the repo's `packs/<Pack>/` laid out as `BepInEx\plugins\<Pack>\`
  (the dragon in `GK2SaddleUpDragonsMountPack`, the rat, snail and worm in `GK2SaddleUpCrittersMountPack`, ids by the pack:
  `saddleup-dragon.dragon`, `saddleup-critters.rat`; moved out of `src/SaddleUp/assets/mounts/`), each a small plugin
  of its own with its `gk2mounts\` embedded (`<Pack>.csproj`, `Plugin.cs`: `dreas.gk2.saddleup.dragon`,
  `dreas.gk2.saddleup.critters`, needing the API 0.40.0; one file, so an update leaves no file of an older version
  behind; first tried as plain folders linked into the game as junctions, the same day), deployed by its build,
  built into `dist/` by `build-dist.sh` (the Mounts tab saves into the repo through `GK2MountsSourceDir`); `skinroots.py` and every tool and scan walk both kinds (`kind()`, `pack_of()`), SkinsScan checks a pack's
  prefix, its `needs`, docs, project and version; `new_skin.py --pack` (with the plugin); NoLinqScan scans `packs/`; `make-release-zip.py` writes a zip per pack (and,
  until 2026-10-27, an All Mounts bundle), `check-dist.py` checks the packs; a dev-only rename table
  (`Catalog.renamedMounts`, gone 2026-10-27) healed the old ids in a dev world and cfg; the self-tests' `M()`/`Sk()`
  (the integration tests' since 2026-10-27) map the new ids, the dragon's sound test reads its pack, and a new
  scenario "built in or external"; `.gitattributes` marks the art and sounds binary.
  After the first run with the packs (1222 of 1223): the Mounts panel test waits for the dragon's picture to be whole
  before switching (read from its pack folder it took longer than the fixed 30 frames); the hostile-files test builds
  its rooted path at run time (a `C:\...` literal sat in the DLL and check-dist refused it). dist/ rebuilt (the API
  0.40.0, Saddle Up 100 KB without the four mounts), the pack zips built and read back; the docs that still placed
  the four inside Saddle Up's DLL brought to the packs, dated notes on the design docs.
  The over-skin colour tests (an over skin keeps the donkey's regions, the erase colour) take their own colours only
  (`OwnLook`): the player's chosen mount's colours reached their test donkeys (a Red harness and a Black coat under the
  test's blue, with the dragon chosen); the Mounts panel test says which of its cart checks failed.
  The run's own checks (the world's own donkeys unchanged, no test donkey left behind) are a scenario of their own in
  a group "After the run", the last of every run, so a failure there has a red header (it showed under all-green ones).
- **Content from many mods** (docs/multi-mod-loading.md, investigated and built 2026-10-26): five new self-tests
  (Never lost: the held and set-aside side file, records carried forward, handle ownership; Skins and effects: hostile
  files, no throw between mods, order and identity, versions and ids); the "ids" scenario follows the new replace
  rule; SkinsScan refuses NaN and Infinity, file names outside a pack and an effect id without its prefix;
  `canonical_cfg.py` knows `needs` and `format` in `[Mount]`; tests/ApiSurface records sealed, abstract, virtual, base
  types, interfaces and nested types and marks a breaking addition.
  After the first run (1202 passed, 3 failed): the pack-sounds test expects the new `|`-joined file sound id; the
  dragon's jump waits for its pictures to be built (the jump began while they built, its first frame the walk); the
  herd test waits up to 30 s for a second standing call (1.5 to 4.5 s apart, put off by a step, never within 4 s: one
  call in 12 s happened). `Plugin.Version` is 0.40.0 (the csproj alone had it).
- **Each mount's jump in the dev tools**: the Mounts tab's "Its jump" rows (saved into the mount's `mount.cfg`,
  right-click back to the default's), the Flying tab's rows renamed "The default jump", the jump's log line and trace
  say whose shape it was. A new self-test in the Flying group ("jump: each mount its own"); `canonical_cfg.py` and
  `tests/SkinsScan` know `[Jump]` (its key order and ranges; refused in a skin.cfg); `tests/ApiSurface` snapshot
  updated (additions only). docs/flying.md "Each mount its own jump".
- **The sound coverage counts a babies' answer** (`Sounds.Coverage`, `tests/SkinsScan`): `answer` plays on babies
  only, so a `[Sounds.Baby]` line now covers it in the start-up log's line, the Mounts tab and the scan; before, every
  mount that gave its babies an answer was still listed as missing it. The new files are named after their events in
  each mount's `sounds\` (`voice_1.ogg`, `mount_1.ogg`, `answer_1.ogg`, ...), picked from the trimmed candidates in
  `C:\ClaudeCodeTemp\GK2-mount-sounds\` (its OVERVIEW.md says which file is which).
- **Surveyed: the user's new mount sounds** (docs/next-update.md section 4; trimmed candidates outside the repo in
  `C:\ClaudeCodeTemp\GK2-mount-sounds\`, with `trim_whole.py`, which cuts only the silence at the ends, never a gap
  inside a chatter). Three dragon growls, a landing thud, a wing beat, eleven rat squeaks, six snail squelches;
  nothing wired in yet.
- **Investigated: what the next update still needs, how the mounts differ in play, and which sounds they lack**
  (docs/next-update.md; the user, 2026-10-25: "see what is actually remaining ... every mount needs to have something
  which makes it useful"). Nothing built but the dragon's cart: its dead `[Tuning.Cart]` and the generator's gap for
  it are gone, and the five self-tests that hitched the dragon (the side-file round trip, the panel's card, the cart
  readout, the preview gap, the walk survey) use the rat or check the refusal.
- **Investigated: the donkey's two skins as a mod of their own** (docs/donkey-skins-pack.md; the user, 2026-10-25:
  "investigate ... GK2 Saddle Up Donkey Skins Pack"). Nothing built: the runtime needs no change; the open
  points are Hellfire's effect, the self-tests' sample picture and the release scripts. (Built 2026-10-27: the Donkey
  Skins pack, above.)
- **The API retires cfg keys too** (`Plugin.DropRetired`, the analogue of `MountSettingsPage.RetiredKey`): a
  released key this version no longer binds leaves the player's file at the start, saved once, and the upgrade
  self-test counts it as retired, not as a value lost (first use: the seven `[Effects]` keys of 0.10.0). The effect
  self-tests drive the rig through a test-only `Rig.TestWant`; how an effect shows (trail on, round the mount only,
  in fights, not on babies, night below 0.35) is fixed in Effects.cs until the skin design. The dev panel's two
  effect editors, SkinsScan's and canonical_cfg.py's effect keys and page-carryover.py's EffectRows went with them;
  the public API snapshot updated (19 lines fewer).
- **No LINQ in anything that runs in the game** (the user, 2026-10-25: "get rid of ALL our LINQ usage ... and disallow
  ALL LINQ usage going forward"; every call allocates garbage). The last uses are gone (the settings page's clearing
  key, the chest's zone walk and dock check, the dependency manifest's sort and lookups, the dev chest probe and
  self-tests). The build refuses it from now on: `Directory.Build.targets` adds the banned-API analyzer to every
  netstandard2.1 project, with `BannedSymbols.txt` banning `System.Linq.Enumerable`, `Queryable` and
  `ParallelEnumerable`, as error RS0030 at the line that uses it. `tests/NoLinqScan` checks every built DLL for any
  reference into `System.Linq` and runs with the release scans. The .NET console tests and tools may still use it.
- **The probe audit** (the user, 2026-10-25: "check if we also have any hanging probes running which can skew our
  performance or even worse, we ship them by mistake"). Every dev tool is behind the dev tools' lock (the sources on the
  machine or the marker file; a player's [Debug] keys bind into a config that is never written), and the dev window
  and the perf logging default to off. Three things still ran for a player and are dev only now: the step-by-step
  diagnostic lines (a shuffle's turns and frames, a pose, a wake, a foal's route search: their strings were built on
  every call even when the 4-a-second budget dropped them; the lines a player's report needs, a spawn or a foal stuck,
  stay), the `[pick]` trace (only with the perf logging), and the Mounts panel picture's set-up dump (some 45 lines).
  Saddle Up and the other mods were checked too: nothing runs for a player that shouldn't (Owned Count's item dump is
  a Debug switch, off by default).
- **A look built ahead for a mount made from the game's own character (Gerry) waits for the game to be loaded**: at
  the main menu his sprites aren't in memory, so every sprite was scanned, the look refused with an exception and set
  aside, then offered again after the load, at every start with Gerry chosen (Performance Hog, 2026-10-25 13:43).
- **The dev stride probe is off until a survey turns it on** (the walk survey, the garbage survey's probe phase). On
  whenever the dev tools were, it appended a row to its CSV every walk cycle of every walking mount, each a file opened
  on the main thread: 1033 times in a 17 s ride on Gerry with his babies, 5 to 11 ms a time (Performance Hog,
  2026-10-25 13:38: 84 frames of 5 ms or more, all marked "file write"). Every dev measurement carried it, and it was
  the sound tick's 2.5 ms.
- **A skin's "loaded" line says what building it cost**, step by step: the sheet made, the pictures made (files read,
  decoded), put in the sheet, uploaded, sprites, colours; the frames it took, the most work in one frame, the game's
  slowest frame meanwhile, and the memory allocated. It was added to measure the hitch when the dragon is picked again
  in the Mounts panel (the user, 2026-10-25) before choosing a cure.
- **A pick in the Mounts panel is traced** (`[pick]` lines): the 120 frames after the click, every frame over 12 ms or
  with the mod's work over 2 ms, with what the mod did in it and what happened then (a look's build started or done, the
  picture showing the new look). The pick itself, the panel's rows built again, the panel's frame and its pictures are
  measured parts now (Pick, PickRows, PanelTick, Preview), and the pick's own two halves (PickApply: the Mount row's
  handlers; PickSave: the cfg file written). The once-a-second slow-frame line and the summary could not show these
  frames. The click's work is counted in the frame it happened in (it ran before the frame's bookkeeping and was counted
  in the frame before). Closing the panel is traced the same way (the world's mount turning into the chosen one), and
  a sound's play (SndLookup, SndPlay) and a sound file's load (SndStart, SndDecode) are measured parts, and inside a
  sound rig's tick an event's resolve (SndResolve), a whole emit (SndEmit) and the ground check (SndSurface). The play
  call was under 0.5 ms; the 2.5 ms in the sound tick every few frames is elsewhere, these find it. The self-test of an
  embedded pack's .wav waits for its turn in the one-a-frame queue instead of expecting it at once. The dev-only stride
  probe inside the sound tick is a measured part (DevProbe), and every 10 s the log has the garbage each part made over
  those 10 s (`[perf] garbage over the last N frames, KB a frame ...`; with the dev panel on), to find where the mod's
  20 to 50 KB a frame comes from. A self-test, "the garbage survey" (group Garbage), runs the cases by itself: on foot
  standing and walking, riding a dragon with two babies walking and standing, the dev stride probe on, the Mounts panel
  picking; one `[garbage]` line per phase with the garbage and time by part. The mods' keys polled each frame (before
  the API's tick, so its garbage went uncounted) are a measured part (Keys) and count in the mod's garbage. The survey
  hides the dev window while it runs: its first run found the window itself made 43 to 61 KB of the 49 to 68 KB a frame.
- **Less garbage from following babies**: a foal built its leader's name ("its parent dreas.gk2.saddleup#1") five times
  a frame for log lines that rarely run; now made only when a line reads it, and a mount's full id is made once. It was
  most of the mod's steady garbage (about 2 KB a frame of 2.1 with the dev window left out, the garbage survey).
  Then the mods' keys: each key's binding was turned into text every frame to see whether it changed (0.22 to 0.44 KB
  a frame, half of what was left); a key now hears its own setting change. Key combinations are compared and their
  modifiers checked without LINQ (the flight's jump and fly keys three times a ridden frame, the safe-spot key every
  frame).
- **The safe-spot self-test waits for the last return's arrival check** before it buries the keeper for "the watchdog
  off": the check runs two physics steps after a return and judges where he is then, so a step that moved him inside
  that window was sent to an older spot and counted as a watchdog return (1 of 1112 failed, 2026-10-24; timing only).
- **Skin layering's tools and tests**: `LooksInherit.cs` (what a skin gives, its default, the resolve), the producers
  split out of `Acquire` (`MakeProducer`, the variant's sheet in `VariantProducer`); `tests/SkinsScan`,
  `tools/skins/check_skin.py` and `tools/skins/canonical_cfg.py` read a variant against its `../default` (its canvas,
  the pictures it takes, its colours; `canonical_cfg.py --check` notes a line that only repeats the default's); four
  new F11 scenarios in the "Skins and effects" group (a variant built on its default, one drawn over it, a variant's lifecycle, a
  code mount against the version rule); the sounds scenario's second skin now takes its default's sounds (layer
  `"default"`); the default-looks scenario's cleanup withdraws the variants before the defaults.
- **The hillside jump test, run and corrected** (the flying experiment): the streaming fix found the slope and both
  jumps landed; its two failures were the test's own: the downhill start had been placed at the foot's height inside
  the ramp (a 17 px jolt as the physics pushed the body out), now both starts sit on the ground's surface; and a
  24-degree hillside climbs 3.2 px a physics step by itself, so the cap is the ramp rule's 4 px, not the flat yard's
  2 (docs/flying.md "The hillside, run"). The embedded-pack sound test waits for its graze line's own bank entry.
- **The flying and jumping sound events** (docs/mount-sounds.md; phase 2 of the sound system): `takeoff`, `flap`
  (the launch's kicks, then every wing beat airborne, not while gliding), `glide` and `fly` (stoppable: loops through
  the glide and the flight, cut at the way out and on the ground), `land`, `touchdown` (the thud), `landed`,
  `refused_takeoff`, `jump`, `jumpland` (the squash, or a jump in place coming down), `perch`. All silent unless a
  mount's or skin's sounds give them one. Self-test "the flying events" (group Sounds).
- **The route the built-in skins' sounds take is proved**: the API's DLL carries a test pack of its own
  (`assets\testskin`, never registered at start) with an `.ogg` and a `.wav`; the self-test "an embedded pack's
  sounds" registers it, decodes the `.ogg` through the cache file under BepInEx\cache, parses the `.wav` in place,
  plays a looping event and stops it. "A pack's own sounds" now also changes a file on disk, reads the pack again and
  hears the new file. What to hand over for the creatures' own sounds: OGG Vorbis or WAV, mono, 44.1 or 48 kHz, a few
  seconds, in the skin's `sounds` folder, named for the event (`voice_1.ogg`, `voice_2.ogg`, `flap_1.ogg`).
- **First sounds for the built-in creatures, from the game's own ids** (`[Mount.Sounds]` in the five skin.cfgs, to
  audition on the Mounts tab): the dragon growls (the zombie's attack cry pitched down), treads heavily (the stone
  step, two a cycle) and breathes at the grass (the furnace bellows); the rat squeals (the spitter's cry pitched
  up), patters (six light steps a cycle) and nibbles (the food sound); the snail is silent but for a wet blop getting
  on and off; the worm rustles as it drags (two a cycle), eats dirt (the shovel) and slaps wetly getting on and off;
  Gerry speaks in his own dialogue voice (the game's "garry" clips, in the speech channel) and clatters on bone steps
  (Larry's). The snail's, the worm's and Gerry's older `[Mount.Tuning]` pins stay. These are first picks by name:
  what to keep is the user's ear's call; a real roar and a real squeak want files of our own. The game's 311 sound
  ids with their clips are listed in `C:\ClaudeCodeTemp\GK2-music\sound_ids.txt` (`dump_audiocfg.py` there).
- **A sound rig per mount instance** (docs/mount-sounds-design.md, phase 1 step 2): `MountSounds.cs` (one static
  set of timers for the ridden mount) is now `Sounds.cs`: every instance has a `Sounds.Rig` with its own timers and
  animator reading, so the ridden one, a standing one in the Active tier and a foal all sound; every event is a row
  of the catalogue `SoundEvents.cs` (`step` with its surface variants, `wheel`, `graze`, `lips`, `voice`, `mount`,
  `dismount`, `hitch`, `unhitch`, `cargo`, `bell`, and the silent-by-default `setoff`, `stop`, `ear`, `turn`,
  `refused`, `sent`, `called`), resolved per instance into a `SoundSpec` (the source, volume, pitch, start, the
  event's own numbers, the layer it came from: the player's `[Ride]` / `[Cart]` keys are the base layer; a mount's
  and a skin's blocks come in the next step). Every rig entry point binds its instance, so a mount's pins reach a
  standing wearer too (the snail stays silent standing); `Resolve` binds as well, since the dev tab and the tests
  call it directly and its half-second cache must never hold an unbound reading (found by the test 2026-10-24). The lips animation event finds its instance through a view
  map (`Mount.ByAnimation`) instead of the ridden view only; a tier change to Idle stops a rig's loops. The dev
  Sounds tab lists every event of the ridden mount with its resolved source and layer and a Play button; `Perf.S.Sounds`
  measures the rigs. Self-test "the donkey unchanged" (group Sounds): every event resolves to the keys of both
  styles, the API's voice and bell play the game's sounds, standing and foal under the switch, the snail's pins.
- **Own sound clips as game sounds (the probe for a sound system of our own)**: `SoundBank` registers a clip of the
  mod's own (a `.wav` parsed by the mod, a `.ogg` or `.wav` file decoded by Unity) as an entry of the game's
  `AudioConfig` under a namespaced id, so it plays through the game's own pooled player with its 3D preset and the
  in-world mixer group (the Sounds slider, the pause). Self-test "the probe" (group Sounds), the dev Sounds tab's
  "Play the probe tone at the mount" button; the design, every mount sound event and the plan in
  docs/mount-sounds-design.md. The game-code map (game-code.txt) now sorts the never-lost classes (warned) and
  the flight's terrain and work-state patches (dev), which the Cecil scan had been failing on.
- **The dragon's legs redrawn** (mounts and skins are still off for players in this version): long, muscled legs that
  step properly when walking, paws with gold claws, the body 6 px higher on them (its seat row 62).
  The skin preview page takes `--study <folder>` to show a skin being drawn next to the shipped one.
- **The dragon flies with its wings spread** (the flying experiment): 14 new frames drawn by the creature generator
  (the wing beat, the take-off, the landing), played while the dragon is in the air; the skin scans accept them.
  The wing beat is now eight frames in which the body moves part after part, not all at once (it "danced"): the
  wing tips trail the arm, the head rides steady a moment behind the body, the legs stay tucked, each swaying a little
  on its own (all four in step paddled like a swimming dog), the tail whips from root to tip (docs/flying.md "The beat in time").
  In the air the near wing now covers the rider as the folded wing does on the ground: the whole near wing is in
  front of him wherever it crosses him (before, the wing went behind him wherever he sat). The raised wings
  are a little lower, so their tops are no longer cut off by the edge of the picture. In flight the front layer is
  now lifted towards the camera along its line of sight: the lift along the sprite's own axis had moved it 3 art px
  down on the screen, so the two halves of the wing didn't meet round the rider. A dev readout (`[occlusion]` log
  lines) reports where the front layer and the rider's sprites really are in the dragon's picture.
  The spread wings are now drawn like the folded ones: the same dark wine membrane and dark edge, and thin red veins
  fanning from the wrist as densely as the fold's (they had few veins, a lighter purple and thick bones). The far wing
  now rises from the dragon's back instead of floating a few pixels above it.
  The dragon now glides: flying along in one direction for a while, it sometimes stops beating and holds its wings
  open for a moment, held low over the rider's legs, flexing gently in the air and the body rising and sinking with them. It eases into the glide
  out of an upstroke and leaves it with a downstroke back into the beat. Stopping, turning or climbing ends the
  glide. The Flying tab has the timings and a "Glide now" button.
- **The gliding dragon's head nods gently** instead of holding perfectly still.
- **Never lost, for mods** (API 0.36.0): `Mounts.ReturnToSafeSpot(reason)`, `Mounts.LastSafeSpot`, the event
  `ReturnedToSafeSpot`, the Behaviour keys `FallGuard` (on) and `ReturnKey` (None), and a button row of a mod's own on
  its settings page, `MountSettingsPage.Button(section, label, description, onClick, buttonText, enabled)`.
- **The Tests tab's group titles in colour** (dev): white with no result, green when every scenario run passed, yellow
  when one warned, red when one failed; a run resets the groups it runs. A new scenario jumps at the real things of the
  player's yard (`World/yard(Clone)`): every built thing and fence from the side with room, and a slide along the
  fence, each landing on the ground, never underground, never sent back.
- **The jump probe** (dev): every jump traced into `BepInEx/GK2-jumps/` (the newest 30), frame by frame and physics
  step by physics step, with a landing audit at each touch-down (every rule of the landing check on its own, the game's
  walk grid from the take-off spot, every collider near the spot). The file's name says how the jump ended.
- **Jump and Fly keys, Space both** (dev-only for now, like flying): Saddle Up's Keys section has a Jump and a Fly
  row, shown only while flying is on. With one key for both (the default): a press jumps on every mount, a second
  press in the air takes off on a mount that flies (the wings take over from the jump, the camera eases up), a press
  while flying lands. With two keys each does its own (Fly takes off from the ground). Space does nothing for the
  mount during a dialogue, a menu, a fight in armour (it is the game's attack there) or while a key is being set in
  the Settings window, and a key just rebound counts only once let go. The keys are whole combinations (Ctrl+Space is
  Ctrl+Space). The API has them as the Behaviour keys `JumpKey` and `FlyKey` (0.36.0), and for mods
  `Mounts.Jump()`, `TakeOff()`, `Land()`, `Mounts.FlightState`, the event `FlightChanged` and `Mounts.FlyingEnabled`.
- **A mount says whether it flies and jumps**: `[Mount] flies = true` and `jumps = false` (written only when not the
  default), `MountDef.Flies` / `Jumps`, `MountInfo.Flies` / `Jumps`; the Mounts tab has the toggles and the panel the
  lines (while flying is on). The dragon flies by its flag: no code asks for the mount named dragon any more.
- **In the air the game's E and F prompts are off** (dev-only flying): the keeper's body stays on the ground under the
  flying mount, so the game offered the things under it (the smelter's F and contents drawn over the dragon), and F
  held there ended the ride as the work state and put the keeper down on that spot from the air, the mount left parked
  up there. Now a flight pauses the game's interactions (its own ladder pause; a jump doesn't), the work state is
  refused while flying, and a ride ended in the air parks the mount on the ground under where it flew. The game's own
  bubbles (a station's ready output, a quest mark) are screen-space UI drawn over everything in the world, the keeper on
  foot included, and stay so over a flying mount (an order shift and a fade were tried and taken out: docs/flying.md).
  Self-test "in the air" (group Flying); docs/flying.md "In the air: no prompts, no work state, parked on the ground,
  the bubbles".
- **The mount's jump feels like a platformer's** (dev-only, Space while riding plain; the keeper on foot doesn't jump):
  a short crouch, a quick rise, a moment at the top, a heavier fall and a squash on landing. Hold the key for the full
  height (28 px), flick it for a hop (16 px). The camera no longer bounces with a jump, and the jump is the same at any
  frame rate. **The mount jumps over things**: the keeper's body leaves the ground for the jump, passes over what is
  lower than the jump and is stopped by what is taller; it lands only where a flight could (never somewhere he'd be
  locked in). A jump keeps the speed you had and steers a little in the air, never faster than the mount really
  rides on the ground (no surge along a wall you were pressed against, none from running into a jump); a jump that
  comes down over something built (a workbench) carries on off it the way it was going, and lands past it; a jump
  lands beside built things where the keeper can stand (it was sent back there) and on a bridge's deck; no more
  jolts at the start and end of a jump on a slope or a bridge; one that can't touch down where it ends lands at the nearest good spot back along its way, the take-off
  spot only as the last resort, and says nothing (no bubble). Invisible walls stop a jump. The Flying tab has rows for each part; a new self-test
  "jump: the mount's arc" checks the arc and jumps live over a low wall and against a tall one.
- **The flying dragon's neck is shorter and steadier.** In the air its neck was long for its body and the head bobbed
  against the body on every wing beat (like a dancer's head); the neck is a fifth shorter and the head now moves
  with the body, a moment behind it.
- **The dragon shows climbing and diving.** Climbing, it arches its neck up and its tail hangs low; descending, or
  coming down to land, it reaches its head forward and down and its tail rises; it eases in and out of each.
- **Your mount appears as itself at once.** The first time you called it in a session, you saw the game's donkey for
  a moment before it turned into your mount (its pictures were being prepared, a few a frame). The mount you have
  chosen is now prepared in the background beforehand, and again whenever you choose another.
- **The dragon's tail is no longer cut off** at the edge of its picture while flying, and its folded near wing
  sits the same whether ridden or not (it moved a few pixels when the rider got on or off, and at take-off and landing).
- **Flying: no more hops over trees and ledges.** Flying low over a wood, or climbing out from under a ledge's edge,
  the dragon no longer jumps up 12-20 pixels at once: it rises smoothly to its height over what is under it, and is
  never drawn inside it.
- **Flying: two jolts gone.** Flying over a raised piece of ground (a cliff's top) no longer lifts the dragon about
  12 pixels in a jolt; it keeps its height in the world. And the start of a landing no longer drops it a few pixels in
  one frame: the body's up and down through the landing frames now carries on from the wing beat's.
- **Flying: landing by your home.** Landings near buildings were refused as "Nowhere to land here." though the
  ground was open: the check asked the game's navigation mesh for its NPCs, which buildings cut into pieces. A
  landing the mesh refuses is now judged by the keeper's own ground, by the game's own rules for where he can walk:
  allowed when he could walk from it to where he took off or it is open ground, refused on a rock top, a ledge or a
  closed pocket. The judging is spread over a few frames while the dragon hovers, so pressing land never freezes the
  game. A dev readout (`[flight-walk]` log lines) shows each judgement (docs/flying.md "Refused landings").
  The `[occlusion]` readout is now off unless switched on in the Flying tab; the landing's checks are timed in the
  `[perf]` lines (`Landing`, `LandGrid`).
- **The drawn ground, read from the game's terrain** (the flying experiment, docs/flying.md): the tiles of every
  terrain mesh read just before the game frees them, the highest drawn level at any point, checked on the Flying
  tab against the walkable ground; the flight's floor, walls and landings use it (no landing inside a rock table); a whole-map survey and a map drawing tool (tools/flying/map_survey.py).
- **The tuning keys moved to keypad 8/2 (torso) and 4/6 (legs sideways)**, off PageUp/PageDown and Home/End,
  which the Grave Zoom mod uses for its zoom.
- **A flying experiment (the tuning window's new Flying tab; docs/flying.md)**: the ridden mount (the dragon, or
  any mount with a switch) takes off, flies at a height with a hover, climbs and descends on keys and lands, the
  camera rising with it; a jump on the same lift. Free flight from a clear height: the keeper's body leaves the
  game's physics and flies over water, fences and low things, bumps into houses and trees taller than the
  flight, and comes down only where he can stand (a save or a
  dismount lands him first). Session values; on by default only where the dev tools are
  unlocked, never for players (with it off nothing changes).
  Landing cuts fixed: the body no longer flashes from its old spot for a frame when a landing sets it down (its
  smoothing is off for two physics steps), and the camera moves with the set-down instead of snapping to the body
  (which threw away its sideways trail: the view jumped across the screen). The API references the game's
  Cinemachine.dll for it. The flight's bubbles ("Nowhere to land here.", "Land first.") stay 1.5 s, not about 5
  (the Flying tab's "Bubble time").
- **The three pack scenarios clean up whatever stops them** (mount from a pack, skin pack, skin pack auto-reload;
  left from the first review pass): their donkeys and carts, the temp pack and the mount it declared, the temp folder
  and the auto-reload switch go in a `finally`, so a check that throws or a stopped run leaves nothing behind for
  the scenarios after it.
- **Self-test "speed: each mount's own"** (Looks group): the words at the band edges, every built-in mount's
  speed and cargo, Saddle Up's retired rows and its file, a code mount ridden (2.5, over a 1.1 in its tuning) and
  driven (2.5 times the donkey's ratio), the donkey at the cfg's speed, the panel's card (Speed, Cargo); the format
  scenario reads `speed` / `cargo` and leaves a `Speed` stat line out. `tools/page-carryover.py` reads `RetiredKey` calls.
- **`tools/skins/creatures/make_creatures.py` runs again:** the mount rename ("kind" to "mount") had put a real
  line break inside its `[Mount]` template string, a syntax error; fixed, and the template writes `speed` and
  `cargo` (as does `tools/skins/new_skin.py`).
- **The run makes its own starting state, wherever it is started** (2026-10-21: a run started in a house failed 29
  times, every ride "Not indoors.": the move to the test spot by SetPosition kept the house's light). Before the
  first scenario: the keeper free (waited for up to 15 s; a script or conversation that doesn't let go is the one
  case it doesn't start, and it says so), out of the armour view, outside every building (the remembered spot only
  when it's in none; else a spot in the nearest container zone that is in none), the light outdoor. At the end his
  spot, his armour view and his own light come back.
- **Indoors self-tests** (group Indoors): the two switches under the game's indoor light, and a real door (the
  game's own teleport into one of the scene's buildings) riding and driving, and back out (docs/self-tests.md).
  Safe from any start: the start's own light preset put back by name, only buildings whose zones are visited and
  wait for no quest, the player's foals left out of the test's doors, the keeper always brought back out
  (docs/indoors.md).
- **The first F11 run of the night** (2026-10-21): the sprite manifest check demanded a frame literally named
  `bdy_static_left`, which Gerry maps to his walk frame as the rules allow, so Gerry was set aside at game-ready
  and every Gerry scenario failed from that (three readouts, the game-creature scenario, the new heal check). The
  manifest is the frames the `[Game]` map names; the build keeps its own rest-picture fallbacks. The heal scenario
  runs after the Gerry scenarios now. Everything else in the group passed (25 of 29), the core group's new
  set-aside-lines scenario passed. The second run, the full set: 697 passed, 0 failed, 0 warnings, 1 skipped;
  the panel's fit measured the same a frame later (the nested layout settles in one pass). The mount-pin
  refusal warning said itself twice (keyed on the skin, before and after one was worn): once per mount and key.
- **The self-tests clean up after a scenario that threw**: between scenarios the runner withdraws any skin or mount a
  test owner left registered, puts the pack-watch switch back and deletes the temp pack folders, and says so in the
  log (a passing scenario leaves nothing). Before, a throw in the mount-pack, skin-pack or auto-reload scenario left
  its pack registered for the rest of the run.
- **The Mounts panel, second design** (2026-10-21, docs/mounts-panel-design.md; the user: a game's mount journal,
  not a wall of rows): a window of a fixed size that fits 1080p without shrinking; a card list on the left that
  scrolls with the wheel, each card with the mount's icon (its portrait, else its standing picture, made for
  every mount automatically); the chosen mount's portrait, name and description at the top right; its picture
  with the cart up to 3x on a dark stage; one line of what it does; and its rows under tabs (Skin, Colours,
  Effect), so only one tab's rows are on screen; a custom colour's rows appear when the preset is Custom instead
  of sitting there greyed. Done and Cancel as before. Later: colour swatches, a search box, a Call button.
- **The Mounts panel on a gamepad**, the game's way (its windows read first): the d-pad and stick move the focus
  through the list, the rows and the buttons, Left and Right step a focused setting's value as in the game's
  Settings window, Select presses, Back steps back like Escape. A focus frame like the game's marks what is focused;
  the focused row is kept in view. The mod's rows in the game's own Settings window step with Left and Right on a
  gamepad too now, as the game's rows do (they answered only the shoulder keys). The search box is mouse and
  keyboard only.
- **The Colours and Effect sections leave the Mods menu**: their picture and their "In the Mounts panel" lines were all
  that was left there, so the sections are gone from the menus (the cfg keeps them, as before). With the mounts
  switched off the Effect section's "coming in the next update" line stays.
- **Escape and the way back, done properly** (the user, 2026-10-21: opened while playing, Escape showed the pause
  menu; pause, Mods, Saddle Up, Mounts, colours must step back through every one). Measured in the game's and the Mod
  Framework's code first. The panel takes the player's controls while open, as the game's own windows do (the UI
  flag: a ride goes on), so the game's keys (Escape's pause menu, movement, E, the inventory) don't act under it,
  and it consumes Escape. The window it was opened from is not closed any more: it waits underneath exactly as it
  was, switched off, and comes back when the panel closes. So Escape steps back one level at a time: the search box,
  the colours window to the journal, the journal to the window under it (the Mods menu on Saddle Up's page, the
  Settings window on its page, the pause menu) or to the game, and from there that window's own Escape carries on.
  Done steps back the same way; Ride and Drive close everything and go into the game. Before, the panel closed the
  Mods menu through the framework's toggle, which reopens the pause menu and forgets where it came from.
- **A review of the journal's code** (one reader, the panel files whole): the list now scrolls to the chosen mount when
  the panel opens (it was laid out while hidden); Escape in the search box leaves the box instead of closing the
  journal; the pause menu is closed again a frame later too (the game's and the mod's Updates run in no fixed
  order); the pictures sit at whole-number scales (the stage is point-filtered: a fraction shimmered); a missing icon
  sprite is not searched for again per card; the scroll maths counts the filtered list; the Speed stat reads the
  Default skin's pin first; stale Cancel comments gone. To probe in game: typing in the search box must not walk
  the keeper or open the inventory (the game reads its own keys).
- **The Mounts panel, third cut: the journal and a colours window** (2026-10-21; the user: the second cut had a lot of
  empty space and squashed the customisation). The stage sits beside a stats card (Speed, Cargo, Skins since 2026-10-22) at
  one size on every mount; under them the Skin and Effect rows; the colours and the effect's four toggles have a
  window of their own ("Colours and effect...") with the mount's picture at its top and both colour columns at full
  height. The list is flat journal rows (the chosen one in red, hover brightens) with a scrollbar; the tabs are gone.
  No Cancel buttons: Done keeps, and Escape steps back (the colours window to the journal, the journal closed) without
  reaching the game's pause menu. In the colours window a custom colour is a row of the 24 named colours as squares
  (the chosen one framed, a click picks) instead of a "< Chestnut >" dropdown; its lightness row stays. Ride and
  Drive the cart buttons on the journal call the chosen mount as the keys do (the panel closes first). A search box
  above the list once it holds more than eight mounts; the mods' keys wait while it has focus.
- **A third review pass** (four readers: the second pass's own fixes, the panel and the registry read whole
  rather than as a diff, the tests and release data). Fixed: a skin with a `[Mount]` block is that mount's own
  skin whatever its `mount =` line says (before, renaming the block's id on a reload withdrew the new mount and
  kept the stale one, and removing the block kept the mount alive); a game-source skin whose frames were not
  there was worn as a blank sheet (the player's colours off the donkey, the skin's geometry on it): set aside
  now, the donkey shows, checked again while chosen and once a minute for every set-aside skin nobody wears,
  offered again when the frames arrive; the ten-second recheck never ran on its own (the rig did not stay
  dirty); an ability could take a look key (skin, effect, colours) while it was free (refused now, held or
  free); a data-only unhitch of a sleeping carted mount used the cfg's hitch gap, not the skin's; a set-aside
  skin still answered "can't pull the cart" and drew the paper without its cart; a colour or mount change while
  the same skin was building threw the half-built sheet away and started over; a skin registered late (or
  again) was never worn by an instance already wanting it; a free cart carried an ability's cart keys; the pack
  watch could reload a withdrawn or replaced skin held by a sleeper; set-aside skins sat in the dev lists and
  the save's record noun; a replaced skin's portraits were forgotten after its declaration was cleared (a
  joiner's stand-in leaked); the panel's follow-the-mount rebuild ran where a throw dismounts the rider (its own
  try now); with the mounts gate off the six effect rows were live in the panel with nothing to pick (shown
  nowhere); a custom colour's lightness row escaped the region greying (the worm's harness); opening the panel
  from the main menu asked a keeper who isn't there to speak (a log line); the panel's list did not follow a
  skin or mount registered, reloaded or withdrawn while open; one picture failure silenced the follow check; a
  page without `Preview()` had a blank picture; "Has babies, 0.4 of its size" and the set-aside line reworded;
  the sleep scenario counted every sheet (other sleepers and wakers move the count; it watches its own now). The
  fit-to-screen writes its measure to the log (two lines per open) so the nested layout's settling can be read
  before anything is changed there. `MountSkinInfo.IsAvailable` / `SetAsideWhy` added (API surface updated).
  Three more scenarios so the automated set covers what only eyes did: the Mounts panel itself (opens, fits the
  screen, lists, a pick follows, Cancel, the key toggles, the button brings forward), a game-source skin set
  aside and healed, the side file's set-aside lines. From the fourth reader: the skins scan let a canvas up to 512 through (the loader refuses past 256; it reads
  `MaxCanvas` now), refused the shuffle keys a mount may pin and let the lantern's through (refused at the
  merge), and refused `none` in `[Game]` (documented, honoured by the loader); docs/api.md and two code comments
  still said the first declaration wins and late skins need a restart; the CHANGELOG said the dragon has no
  second tone; the Saddle Up README lacked the panel marker on seven colour rows and the two Info rows; the
  settings scenario's mount count did not honour set-aside skins; the ownership scenario's session-layer check
  assumed nobody holds `HeadSettle`. Then two gaps in the pass's own edits: a healed (or newly set-aside) game-source
  skin now marks every rig dirty, else a wearer that had given up stayed a donkey until a door; the
  still-building shortcut compares the skin's definition, not its id, so a replaced or reloaded skin is switched
  to. Left for the release step: dist/README.md (rebuilt with the dist), the
  CHANGELOG's For players block still carries development asides to move under Development, `needs =` in the
  shipped cfgs (none has it: decide), try/finally in the mount-pack, skin-pack and auto-reload scenarios.
- **A second review pass** (four readers again, over the first pass's fixes, the end-to-end cycles, every other
  self-test scenario under the new rules, the docs and the panel against Unity). Fixed: the sheet was freed on
  every view rebuild, so an unhitch or a door rebuilt the skin over a dozen frames with the plain donkey showing
  meanwhile (freed on the sleep tier only now); a set-aside skin still governed the instance's mount (its tuning,
  babies' size, noun, abilities) while the donkey was drawn (the donkey comes instead, in mount too), and the
  "offered again" heal for it could never run (a set-aside skin someone has chosen is checked again every ten
  seconds); a renamed mount's old mount was never withdrawn on reload (the new mount shadowed it in the loop); a
  removed mod's set-aside value was dropped from the side file when the mount's merge or an ability held the key;
  a withdrawn skin left the mount it declared behind (withdrawn with it unless another skin declares it; the
  mount-pack scenario says so now); a joiner's stand-in portrait was never forgotten; a winner without a `[Mount]`
  block left the loser's mount pointing at it; the panel never drew a portrait (drawn under the mount's name now),
  said "times the donkey's" for a speed that is times walking speed, left the right side empty for a set-aside
  mount (it says why now), rebuilt its list from inside the clicked paging button (a frame later now), measured
  its nested layout once (fitted again a frame later), could lose its dim marker to a CanvasGroup the copied
  button carries, and let its lines wrap inside a fixed-height row; the Mounts button closed the panel like the
  key (it brings it forward, the menus out of the way); the four effect toggles stayed in the Mods menu without
  their Effect row (the panel's now); Saddle Up's Colours and Effect sections were empty in the menus (a line in
  each says where the rows went); a sleeper whose view is destroyed rather than parked (with a cart, mounted)
  kept its sheet (let go of either way); a
  Note row's visibility key named a Button; the round-trip scenario's rule for unwritten values lagged the
  writer's. Listed for later: a code mount against a loose pack's same-id mount (docs/open-items.md).
- **A full review pass over the night's work** (four readers over the diff, every finding checked against the
  code; the user: "this was big and complex"). Fixed: a removed mod's saved values counted as loaded when its GUID
  extended a loaded one (now exact GUIDs, and an ability owner only with its mod); an ability's values were saved
  into the side file and never forgotten on the first attach (session-only now, forgotten before Attached); the
  version rule replaced a skin but left the mount's declaration with the loser (the winner's own skin re-declares);
  the sprite-atlas copies of a game-source check lived to the frame's end, where a build in the same frame could
  take them for the game's own (destroyed at once); the Mounts tab's save was read back by the auto-reload (the
  watch is stamped too); a reload dropped the live mount off the def, so a renamed mount was never withdrawn from the
  second reload on; the panel drew the page's Mount dropdown inside itself and its right side did not follow a
  choice made there or elsewhere (the list is the picker, the right side follows the chosen mount); old rows
  were measured by the layout before their deferred Destroy (off first); a row a mount pins stayed live in the
  game's own Settings window (left out there too); a late mount's remembered skin was lost to the default at the
  catch-up; a mod's heading over plain rows vanished from the menus with the look rows (only a heading over look
  rows is the panel's); the Mounts key reopened the panel instead of closing it; loose packs counted as first
  party for the prefix note; the portrait cache never forgot a skin; 1.0 and 1.0.0 were different versions; an
  unavailable Default was still offered; an aside line could overwrite another mod's live value on the next load;
  one ability object in two mounts; and in the tests a ride's arrival unchecked, a scenario without a cleanup, a
  near-tautological count. Listed for later: the Mounts tab's save overwrites a joiner's own [Mount] block with
  the live mount's facts (dev-only, no shipped joiner).
- **The look bar became the Mounts panel** (`LookBar.cs` -> `MountsPanel.cs`); Idle rigs are skipped before the
  cameras; a `Set` of the same value by the same owner is a no-op; `Perf.S.LooksTick` measures the rigs' tick;
  the skins scan says which colour path each skin takes (the exact swap, or the cube over 48 colours: the snail
  and the worm). Saddle Up 0.12.0 on API 0.35.0; the API surface snapshot updated.
- **Foundation, built** (docs/mounts-foundation-2.md 6, the decisions of the night): the ownership rule, the id
  rule and the format are in (above), with four new self-tests ("tuning: a value belongs to the owner that set
  it", "ids: the mount's own skin owns its declaration; the same skin id twice, the newer wins", "format:
  description, portrait, stats, ability names, needs, a bad format, unknown [Game] keys", "skin: kept through a
  hitch, freed asleep far away, worn again awake", both sleep branches) and the settings scenario's panel checks; the cargo,
  morgue and corpse scenarios take a key over through the dev-only `ClearAny` instead of a silent Set). The seven
  cfgs carry descriptions; the generators write one into a new cfg. The Mounts panel follows.
- **The foundation for mounts from many hands, investigated** (docs/mounts-foundation-2.md): three code
  read-throughs with the sharpest claims checked by hand. Five findings that change the design (a value on an
  instance has no owner rule, the last writer wins silently; ids and a mount's facts go to whoever comes first,
  and a loose pack comes before every mod; the settings page grows 22 cfg keys per mount, which the user's
  Mounts panel absorbs; every mount rides the donkey's skeleton, a fast mount's legs at the donkey's stride;
  skin sheets have no cap and sleeping instances keep theirs), fifteen smaller fixes with lines, and the
  decisions left. One line fixed on the way: a reloaded embedded skin kept its DLL (`SourceAssembly`), so the
  Mounts tab can save Saddle Up's mounts after a reload.
- **Mounts as separate mods, investigated and done** (docs/mounts-as-mods.md): the runtime needed nothing (every
  mount is a pack the API already loads from a folder or another mod's DLL; no shipped code names a mount); what
  stood in the way is fixed above (the load order, the Info line) and here: the five mount folders moved from
  `src/GK2DonkeyAPI/assets/skins/` to `src/SaddleUp/assets/skins/` (Saddle Up's csproj embeds them, its Awake
  registers them: the content in the player mod, the backend in the API; a third project was tried first the
  same evening and folded back in, the user: "have them be in Saddle Up with the backend all working in the
  api"); the tools and scans walk every `src/*/assets/skins` through `tools/skins/skinroots.py`
  (`canonical_cfg.py`, `skin_colors.py`, `new_skin.py` with `--mod <project>`, the review page,
  `make_creatures.py`'s default, `tests/SkinsScan`, the `check_skin` loop in `release-scans.sh`). The sixteen
  self-tests that name the five mounts keep their subjects; the readout and Coat scenarios say "every shipped
  mount"; the Gerry test expects its mount from a pack, not built into the API; the temp packs' sample picture
  comes from Royal Parade now. Saddle Up needs API 0.34.0.
- `tests/fixtures/released/0.10.0/`: the player files as 0.10.0 wrote them, captured from the release zip's
  rehearsal run (checklist step 3b had been skipped at the release): the two cfgs with the keys new in 0.10.0 set off
  their defaults, 0.30.0's `defaults.cfg`, a real format-2 side file. The upgrade self-test runs over 0.9.1 and
  0.10.0 now.
- `tools/page-carryover.py`: an offline dry run of the page's carry-over, reading the rows and `MovedKey` aliases
  out of Saddle Up's Plugin.cs and checking an older cfg key by key (the fixtures by default, any cfg as an
  argument). Run it before the in-game upgrade self-test whenever the page's sections or keys move.
- The skin tooling is in the repo now (it lived in `C:\ClaudeCodeTemp\GK2-aseprite`, which a commit never saw):
  `tools/skins/` (the pack checker, the watcher, the two Aseprite menu scripts, the .aseprite writer, `gamedir.py`
  for the game folder from Directory.Build.props), `tools/skins/royal-parade/` (the generator and its measurements,
  outputs in a git-ignored `out\`) and `packs/royal-parade/` (the pack's source: `skin.cfg` and the generated
  `sheet.png`, copied into the game by the generator; moved again the same night, see "One pipeline for every
  skin" below). Only the game's frames stay outside, read from the asset
  probe's folder. Verified: the generator run from the repo writes a sheet identical to the game's, the measurement
  scripts reproduce their data files. Hellfire's reviewed Python prototype (the 14 rounds of 2026-10-18/19, the
  rules that `LooksHellfire.cs` carries in C#) is kept as `tools/skins/hellfire/hellfire_proto.py`.
  `docs/skins-inventory.md`: every skin, what it is made of, where its source, its tooling and its game-side files
  live, and what stays outside the repo.
- Royal Parade (the test over-skin): its generator makes the cloth from the donkey's frames and
  writes the pack into the game itself, no drawing step (docs/sprite-tools.md "Generated, no drawing"). The cloth is
  one shape measured on the standing frame, hung from the hip in every frame with each column on that frame's back
  line, so it rides the walk's bob and the graze's dip without changing size (the first cut, measured per frame,
  jumped in the graze clip). The generator writes a frame strip per animation (`strips\`) to look at before the game.
  Then the root of every placement fault: the generator's donkey frames (`harness_frames\*.raw`) had every frame's
  top-left corner at one spot, while the game stands each frame on one ground row, 2 to 17 px apart per frame; the
  generator, the checker and the crown anchors now use the asset probe's frames, the game's own placement
  (docs/sprite-tools.md "The canvas, found wrong").
- **The review page shows what the game built, not its own guess** (the user: "make sure what we see on those pages
  is the same we see in game"): the dev panel's Mounts tab gained "Export the loaded skins as built"
  (`Looks.ExportBuilt`): every skin worn since the start, its pictures read back from the texture the game draws
  from (after the erase compose, whole frames and all), and the donkey's frames as the game reads them, to
  `BepInEx\GK2-asset-probe\game-skins\`. The page prefers that export, says so on the card, and counts the pixels
  where its own composite would have differed (0 is the expectation; a number is a divergence to look at). It also
  gained an "all 44 frames" view: every picture of every skin, named, for a frame-by-frame look.
- `tools/skin_preview_html.py` rebuilt as the skin review page (the user: "we will use it a lot"): every skin the
  API knows from its real sources (the game's donkey frames, the built-in skins in the repo, the packs in the game,
  recipes drawn to pictures such as Hellfire), grouped by mount with the mount's own look first, an over skin shown
  composited over the donkey, the old test folders collapsed at the end; a sticky bar with the clip, a frame
  scrubber and step buttons (arrows, space), a frame-strip view (every frame of the clip side by side), scale, facing,
  night, rider. Output: `BepInEx\GK2-asset-probe\skins-preview.html` (local only: the game's art).
- Self-test "colours: a skin over the donkey keeps the donkey's regions" now reads back what the world draws: the
  overlay renderer through its real material and shader, photographed into a texture, with the swap on, with it
  off (the same picture lit by the scene, dark at night: colours are judged against that render, never against the
  picture's own values) and together with the body part. Royal Parade was seen in the world without its red while
  the Mods picture had it; this check tells the two paths apart. The player's own mount's worn skin is read back
  the same way, for the log.
- **Fixed: a skin drawn over the body (`layer = over`) sorted at the body part's own order**, so the two coplanar
  sprites left the depth test to decide and the overlay showed only outside the body's silhouette in some frames
  (the gold hem below the belly and the border above the back, the red cloth gone: the user's video of 2026-10-19
  17:08). Found by the readback above (the overlay drawn alone was whole; with the body it was not). The over
  overlay now sorts one above the part, the front layer two above when no keeper rides. Not in any public release:
  the skins are switched off for players until the next update.

## 0.10.0 (GK2 Donkey API 0.30.0), 2026-10-19

For players:

- **Only the donkey, for now.** This version's settings have a Mount row and a Skin row, and both hold one
  choice: the donkey, drawn as the game draws it. The other mounts (a dragon, a giant rat, a giant snail, a giant
  worm, Gerry the skull), the skins (Hellfire, skin packs) and the particle effects are built and in this download,
  but switched off until the next update, which switches them on; the settings say so in two "Coming in the next
  update" rows. Nothing you have changes: your donkey, cart, colours and saves carry over as before.
- **Fixed: with GK2 Mod Framework 0.1.19 or newer, Saddle Up's page in the Mods menu was "Faulted"** ("OnRegister
  failed: AmbiguousMatchException", reported 2026-10-19). The newer framework added a second `AddEnum`, and the page
  looked it up by name. It now calls the one it means directly; checked against framework 0.1.16, 0.1.19 and 0.1.23.
  The mod itself kept working; only its settings page in the Mods menu was missing.
- **A Mount section and an Effect section.** Right after Keys: your mount's picture, its Name, the Mount and Skin
  rows (the donkey alone in this version, see above), then the babies rows ("Babies", "Babies come indoors",
  "Babies are the same mount and skin"); after it an Effect section ("Effect": None or As the mount, with the rows
  for when and how it shows; nothing to show in this version). Your Behaviour, Donkey, Cart and Sound settings stay
  as they were, though their sections moved down in the cfg (the old copies are cleaned out of the file on the first
  save); the Name and the babies rows live in the Mount section now (your values move with them).
- **Mount, not donkey.** Saddle Up's settings say "mount" wherever yours may not be a donkey ("Ride your mount",
  "Send your mount away", "Keep your mount with the save", "Mount and cart sounds") and "babies" for the young
  ("Babies", "Babies come indoors", "Babies are the same mount and skin", "Babies have it too"); the Name and the
  babies rows live in the Mount section now (your values move with them); the Donkey section keeps the donkey's own
  coat and harness colours. The keeper's refusals name the mount ("This dragon already pulls a cart", "That is a
  cart without a mount", "Your mount is nowhere to be found"). The F11 panel's Mounts tab says "mount" instead of
  "creature". The game's delivery donkey is still a donkey.
- **The colours window stays put.** "Choose colours" now shows the picture in a slot of fixed size, the rows in two
  columns by section (your mount on the left, the cart on the right) with a heading each, and the custom colour and
  lightness rows always in place, greyed until the preset is Custom. Nothing moves when a value changes (before,
  those rows came and went and the whole stack shifted under the cursor), and a window taller than the screen is
  scaled down whole instead of running off the top. The picture no longer keeps the previous mount's lantern where
  it hung (the preview copy is undressed before it is dressed again), so the mount sits centred.
- **A side file this version can't read is kept, not lost.** If a save's `<save>.gk2donkeyapi.dat` is of a format
  this version doesn't read (a newer one, carried over by Steam Cloud from another machine), it is set aside as
  `<save>.gk2donkeyapi.format<N>.dat` and the log says so; before, the next save deleted or overwrote it, and a
  parked cart's load with it. Updating from 0.9.1: every setting, the custom colours and the kept donkeys and carts
  carry over (docs/compatibility.md).

Built in this version, switched on in the next update (players don't see any of it yet; the Development notes
say how it is switched off):

- **Mounts and skins.** A mount (Donkey, Giant Rat, Giant Snail, Giant Worm, Dragon) has skins (the donkey: the game's
  own and Hellfire; the dragon: its own look): the Mount row lists every mount, the Skin row that mount's skins (cfg
  keys Mount, Skin_<mount>, FoalsWearSkin, Effect...). A mount carries its own seat for the rider and its rules (a mount
  can refuse the cart), and mods can give one abilities. The "Effect" row gains "As the mount".
- **Skins.** Your donkey can be drawn as something else: the giant rat, the giant snail, the giant worm and a red and a blue dragon (their wings raised above the rider) are built in
  (our own art). Donkey settings: "Skin", a list to pick from (the donkey, the built-in skins, every pack installed;
  in the Mods menu, the game's Settings window and the colours bar), and "Babies are the same mount and skin". The skin is
  kept with the save. Skins from other mods come later.
- **Hellfire, a skin of the donkey that changes the drawing, not the colours:** horns where the ears were, a long
  devil's tail with a spade where the tail was, eyes that glow, lava cracks across an ash coat. Drawn from the
  donkey's own frames the first time it is worn, a few frames at a time, so every clip has it and nothing of
  the game's art ships. The earlier five recolour-and-pattern skins (Zebra, Ghost, Ember, Golden, Shadow)
  are gone: the colour settings do that now. Blue Dragon is gone the same way: a blue dragon is the red one
  with a custom coat.
- **Skin packs.** A skin from anyone, no code needed: a folder `gk2skins\<id>\` with a `skin.cfg` and a PNG per donkey
  frame, dropped into `BepInEx\plugins` (or into any mod's folder there). The log says what it found: pictures that
  match no frame, frames still to draw. A pack that goes missing leaves the donkey as the game draws it, and the choice
  is kept for when it comes back (the list shows it as "not installed"). How to make one: docs/skins.md. A pack can be
  one sheet PNG instead of a file per frame.
- **Gerry.** The game's talking skull as a mount (Mount: Gerry): his own frames out of the game, three times his
  size, hopping along with the jaw chattering, a somersault for an idle; cart and babies allowed. A skin pack can do
  the same with any character of the game (`source = game`, docs/skins.md): no pictures to draw, just the
  character's preset prefix and which frame goes where. A mount's babies have their own size (`foal_scale` in the
  mount file, the Mounts tab's "Babies' size"; Gerry's are his own size, the donkey's foals stay 0.6), and Saddle
  Up's babies take it within a second. The rider sits on Gerry's skull and rides its (much reduced) hop: a skin may
  say `seat = picture` (the rider's seat is the picture's own top, per frame) and a game-creature skin `lift` (how
  much of the creature's jump is kept). The Mounts tab says when the player's AutoSeat (off by default) makes the
  torso rows moot, and lets a mount set the torso height players actually use; the Instances tab's "Log drawing"
  writes every renderer around a mount into the log. The Mounts tab saves by itself, like the Cargo tab: a moment
  after the last change the values are in the skin's file (and the mount's declaring files), with the time shown,
  "Undo the last change" to put a file back, and no Save button to forget.
- **The cart sits behind a longer mount.** A mount file can set `HitchGapPx` (the whole cart, shafts included, that
  many art pixels further behind than behind the donkey); the dragons set 24, so their body isn't drawn inside the
  cart. A cart left standing stays where it was drawn, and a mount taking a cart stands its gap ahead of it. The F11
  panel's Mounts tab has the row for the cart style; the "hitch and unhitch" self-test checks it on both facings and
  across leaving and taking the cart.
- **Hellfire.** Donkey settings: "Effect" (None or Hellfire: the game's own lamp fire in a ring round your donkey, with
  its sparks and a trail of flames that burn out behind it; mods can add other effects), "Effect shown" (never, at
  night, always), and whether it leaves the trail, goes round the cart too, shows in fights and on the baby donkeys.
  Kept with the save. Never indoors. (First build 2026-10-17 19:36: the Effect list had no "None" entry because an empty id can't be a dropdown choice, so a pick never stuck and the cfg stayed empty; the id is `none` now and a cfg holding "" takes the row's default.)
- **Your colours are kept per mount.** The coat, harness, paint, wheel, hub and lantern-light choices belong to the
  mount you set them on: a green dragon and a brown donkey, each as you left it, and the cart's paint with each.
  Switching the mount brings its own colours back. The donkey's values from before are its own unchanged; every
  other mount starts with the game's colours. Every mount's default skin is listed as "Default".
- **Your colours on every mount.** Coat colour, harness colour and the custom colours with their lightness now
  recolour any mount the same way they recolour the donkey: a dragon's scales, the rat's fur, the worm's skin.
  Each built-in creature says which of its colours are its outlines, its second tone (the dragon's belly, the
  rat's tail and ears, the snail's shell) and its eye, so those follow the matching settings and the rest is the
  coat. A creature without a harness ignores the harness rows. Before, the colour rows did nothing on any mount
  but the donkey. (docs/mount-colours.md)
- **The settings picture changes mount in one step.** Picking another mount or skin re-frames the picture at once,
  on what the new mount draws over two seconds of its walk (a hopping skull included); before, the new mount was
  drawn inside the old one's frame until the once-a-second edge check caught it, then framed on one frame the hop
  overshot, so the picture changed size two or three times in the first second (the video of 2026-10-18 21-07). The
  picture's texture is resized in place, so the displays never go blank between refreshes.
- **A mount asleep far away is still that mount.** Its kind (what its babies' size, its name in messages and its
  effect follow) came from the skin drawn on it, and a mount asleep out of sight has nothing drawn, so it counted
  as a donkey: Gerry's babies stayed donkey-sized (0.6) until he was called and woke (seen 2026-10-18 indoors, four
  big skulls). The kind now follows the mount's skin setting whether or not it is drawn; the self-test "game
  creature" checks a sleeping Gerry.

For mod authors:

- **The mounts, skins and effects API is in this build but switched off** (`Mounts.MountsEnabled`, false in Saddle
  Up 0.10.0's download): `Mounts.Kinds.Kinds` lists the donkey alone, `Mounts.Looks.Skins` and `Mounts.Effects.Effects`
  are empty, and `Register`, `RegisterRecipe`, `RegisterFolder` and `RegisterEmbedded` on them return a handle that
  does nothing (0 for the counts) with one log line per owner, nothing thrown. Register in Awake as usual and check
  `MountsEnabled` to tell your player; the next update switches it all on with no change on your side. The rest of
  0.30.0 below describes the API as it works then.
- **A skin worn over the donkey keeps the donkey's colours** (`layer = over` in its own colours): before, the
  worn skin's colours replaced the donkey's whole colour map, so the donkey under a saddle cloth lost the player's
  coat and harness colours. Now the donkey's regions stay, the colours the skin's `[Colors]` names join them, and
  the colours it doesn't name are kept as drawn (not Coat). A named colour the donkey also has is the donkey's, said
  once in the log. docs/skins.md, docs/mount-colours.md.
- **A skin is the drawing, a mount the creature** (docs/mounts-design.md §1): a skin's `[Tuning]` / `MountSkinDef.Tuning`
  takes only the picture-dependent keys (seat, torso, legs, hitch gap, depth, centring) and refuses the rest with one
  log line; the per-skin foals flag is gone (`skin.cfg foals`, `MountSkinDef.Foals`, `MountSkinInfo.Foals`, the
  `foals` argument of `RegisterRecipe`): whether foals wear a skin follows its mount's `Foals` and the player's
  `FoalsWearSkin`. `HitchGapPx` is a tuning key of the cart style (docs/reference.md); `Looks.LayeredValue` gives a
  creature's value for a style it isn't in yet.
- `MountSettingsPage`: a section moved to another place on the page (its cfg number changes) keeps a player's values:
  a key the cfg lacks under its section takes the value under the same name with another number (of several old
  copies the fullest, then the highest-numbered), and the old copy is dropped from BepInEx's orphans so the file holds
  each value once; `MovedKey(section, key, oldSection, oldKey)` does the same for a key moved to another section or
  name (docs/api.md, "Sections keep your order"); self-test "settings: a moved section keeps a player's values".
  `MountRows` lost its `legacySection` argument (it served one development cfg). `MountRows` is the Mount and Skin
  dropdowns; the babies rows are `FoalRows(section, companion)` (Foals, FoalsIndoors, FoalsWearSkin); `Preview`'s
  default label is "Your mount". Refusal texts name the instance's mount (`Instance.Noun`, internal). Skin packs:
  `source = game` with a `[Game]` section (prefix, scale, key lines) makes a skin out of another game character's
  frames (docs/skins.md); a skin.cfg's other sections survive the Mounts tab's Save. `MountSpawn.Skin`:
  the spawned one wears that skin (and is that mount) from the first frame, as `handle.Tuning.Set(owner, "Skin", id)`
  would; a handle's description says "dragon with cart" rather than "donkey with cart".
- `MountCompanion.Skin` and `FoalsWearSkin`; the instance tuning key `Skin` (`handle.Tuning.Set(owner, "Skin", id)`,
  saved with the instance; "donkey" or "" is the game's own look); `Mounts.Looks` (`Skins`, `Find`, `Report`,
  `MountSkinInfo`); the settings row kind `MountSettingsPage.List` (a pick list of ids with labels, read when first
  shown); skins from mods: `Mounts.Looks.Register` (a `MountSkinDef` with an `IMountSkinSource`), `RegisterRecipe` (a rule
  over the donkey's own frames, `MountSkinFrame`), `RegisterFolder`, `RegisterEmbedded`, `Unregister`, `FrameKeys`,
  `SkinsChanged`; docs/api.md "Skins"; examples/GK2DonkeySkins. API 0.30.0, Saddle Up 0.10.0 (needs 0.30.0).
  `IMountSkinSource.TryGetFrame` gives a `Sprite` (from any texture of yours), not a `Texture2D`.
- Mount kinds (docs/mounts-design.md): what is ridden as a creature type against a skin (how it is drawn). `Mounts.Kinds`
  (`Kinds`, `Find`, `Register` with a `MountKindDef`: the rider's seat for it as values of existing tuning keys for
  both styles or one, `Cart`, `Foals`, a default `Effect`, `Abilities`; `Unregister`, `KindsChanged`), `MountAbility`
  (Attached / Detached / Mounted / Dismounted / Tick hooks, one object per kind), `MountHandle.Kind`,
  `MountSkinDef.Mount` (a skin names its kind; `Foals` nullable), a pack's `[Mount]` and `[Mount.Tuning*]` sections (no
  code), `MountEffects.KindId` ("As the mount"), `MountSettingsPage.MountRows` and `EffectRows` (the rows in one call
  each; Saddle Up uses them). The built-in creatures are kinds declared by their own skin.cfg.
- A skin's front layer: pictures drawn in front of the rider (`front/` in a pack, `sheet_front`, `MountSkinDef.FrontSource`),
  sorted above the keeper's body while he rides, as the leg overlay is; the dragons' near wing uses it.
- A skin's own rider settings and placement: `MountSkinDef.Tuning` (a pack's `[Tuning]` section: existing tuning keys
  set on the wearer as overrides owned by the skin while worn, cleared when it comes off, never saved) and
  `Offset` / `CartOffset` (`offset` / `offset_cart`: the pictures moved on the donkey, riding and hitched).
- Effects: `Mounts.Effects` (`Effects`, `Find`, `Register` with a `MountEffectDef` of parts, sources and emitters,
  `Unregister`, `GameParticles` for the game's own particle systems, `EffectsChanged`), `IMountEffectSource`,
  `MountEffectEmitter` (ring, body, trail), `MountEffectWhen`; the instance keys `Effect` and `EffectWhen`,
  `MountCompanion.Effect` / `EffectWhen`, `Mounts.EffectSettings`; `MountSettingsPage.Toggle` takes `visible`;
  docs/api.md "Effects". Hellfire is defined through this API.

Development:

- Self-test fixes from the first full run with the mounts on after the gate (2026-10-19): "settings: the Mount, Skin,
  babies and effect rows" left the dragon chosen before its per-mount checks that assume the donkey (a test bug since
  it was written); "recipe: built from the donkey's frames" can't watch the build when a donkey of the world already
  wears Hellfire (said as INFO now); "colours: the player's regions" reads the as-drawn hue only by day (at night the
  preview is blue-lit: an open item). The pack auto-reload compared against the files at its first check rather than
  as read, so a change made before the switch went on was missed: fixed, the scenario is green.
- `tests/fixtures/released/0.9.1/dreas.gk2.saddleup.cfg` gained the six `Custom*` palette keys (CustomCoat = Plum ...)
  that 0.9.1 wrote and the reconstruction had left out, so the upgrade self-test covers the custom colours too. The
  0.9.1 files were run live through the 0.10.0 build on 2026-10-19 (release-checklist.md section 5): 0 lost of 485 values.
- **How the mounts are switched off for players**: the dev key `Debug.MountsPreview` (off by default; a player's
  cfg has no [Debug] keys, so it answers false there). Off, `Looks.RegisterBuiltIns` and `Effects.RegisterBuiltIn`
  don't run (nothing but the donkey kind exists, so every list and the colours per mount hold the donkey alone) and
  the public API keeps registrations out (`Mounts.KeptOut`: a handle to nothing, one info line per owner); a skin
  that is set but missing is an info line, not a warning, while off. On, everything as before. The F11 panel's Debug
  tab has the switch (a change takes the next start) and the log's first lines say which state the game is in.
  Self-tests: a `SKIP` state (counted apart from warnings; the panel shows it), the scenario "mounts off: only the
  donkey offered, registrations kept out", and the eleven scenarios that need the built-ins skip while off (run the
  suite with the key on for them). The flip for the next release is one change: the key's default to true, or the
  key gone and the two `RegisterBuiltIns` calls unconditional.
- `tests/FrameworkScan` check 5: no GK2.Framework member looked up by name (a newer framework's overload makes such
  a lookup throw); it fails on the 0.9.1 DLL and passes on the fix.
- **Skin packs reload by themselves while drawing** (dev only, `Debug.SkinPackAutoReload`, off by default; a switch
  under the Mounts tab's "Reload from the file"): a pack in use is read again a second or two after its files change
  on disk. With the watcher (`watch_skin.py`, every save in Aseprite exported, checked and previewed as GIFs), a
  round is draw, save, look. Self-test "skin pack: read again by itself when its files change (dev switch)".
- **Sprite tools** (docs/sprite-tools.md): after Hellfire took 14 rounds of frames, skins move to a draw-and-look
  loop in Aseprite, built from source for our own use. Two Aseprite scripts, "GK2 Export skin" (every visible layer
  but the donkey reference into the pack's sheet.png, an optional check run after) and "GK2 Part to all frames" (a
  part redrawn once, sent to the other frames at each frame's own place), and a draft generator for the Royal Parade
  skin (a caparison and a crown; the crown placed from the reviewed Hellfire horn positions). Self-test "colours: a
  skin over the donkey keeps the donkey's regions".
- The F11 panel's Instances tab spawns any mount with any of its skins ("Mount and skin" cycler above the buttons;
  "with its cart" and the family button greyed with the reason when the mount refuses the cart or has no babies; the
  babies wear the parent's skin; rows name the mount and skin). Its wording says mount, not donkey.
- Tests of the mounts and skins system (docs/self-tests.md): a headless `tests/SkinsScan` (every built-in skin.cfg
  parses as the loader reads it, all 44 pictures per skin at the canvas size, the front layer, a skin's `[Tuning]`
  holds picture keys only, a mount's tuning keys exist, a mount declared by several skins is declared the same way;
  in `release-scans.sh`); self-tests "settings: the Mount, Skin, babies and effect rows" and "skins and kinds: the
  changed events"; "hitch and unhitch" checks the hitch gap on both facings and across leaving and taking the cart;
  "kept-with-save text" keeps the skin id and leaves the mount's own values out; "settings: a moved section keeps a
  player's values" covers several old copies, `MovedKey` and the orphan cleanup; the pack tests announce the warning
  they cause; "mount kind from a pack" carries `foal_scale` through the pack, the save and the reload, and "game
  creature" checks the seat on the skull's own picture and the babies' size per mount (Gerry 0.33, donkey 0.6).
  The eight looks scenarios are in docs/self-tests.md now. docs/gerry-mount.md: Gerry's sprites (73
  frames, 96 canvas, a hopping skull) and three ways to make him a mount.
- Hellfire is `LooksHellfire.cs` (a recipe over the donkey's frames in top-down canvas coordinates; its pieces placed
  from the eye, the rump, the ear and the torso's box; prototyped in Python over exported frames first). The five
  recolour recipes, Blue Dragon and their tests are gone; `Colors.RegionSourceColours` gives a recipe the donkey's
  coat and outline colours; a code recipe can declare colour regions (`SkinDef.ColorRegions`).
  The game's Hellfire was checked against the prototype over all 44 frames, at the game's own canvas offset: the same
  pixels. To get there, everything that rounds now rounds the same way in both and relative to its own box (half up;
  the game's half-to-even and absolute positions moved a crack by a pixel with the frame's place on the canvas), in
  double precision, and the torso's edge is measured again after the tail is gone in the head-down frames. The hand
  tables (ears, horns, eyes) are relative to the drawn area, the horns sit on the eye as in the ear-twitch frames.
- `MountSettingsPage.PerMount(rows)`: a row set per registered mount (the donkey's keys plain, the others suffixed
  `_<mount>`), the chosen mount's shown (`Row.Shown`), a row's own condition apart (`Row.Enabled`: the look bar
  greys instead of hiding); `MountCompanion.LookOf(mount)`, `MountId` and the `MountLook` type; the companion's
  `Coat`, `Harness`... are the current mount's. The self-test "settings: the Mount, Skin, babies and effect rows"
  covers it. `Looks.DefaultSkinOf`: a mount's default skin, labelled "Default".
- Colours per look (docs/mount-colours.md): a skin's colours are collected as its sheet is built and sorted by
  region from its `[Colors]` section (loader, SkinsScan with a PNG reader of its own); the colour engine maps the
  worn look's colours per sprite group and uses the colour cube over 48 colours; the lightness anchor is per look
  and a companion re-applies its look when the skin's sheet is whole; self-test "colours: the player's regions on
  a mount in its own colours" (the settings picture read back). The `[Colors]` lists of the five picture skins.
- **Compatibility with released versions** (docs/compatibility.md): the four stores a player owns and the rule per
  store (page keys and enum names permanent, sections free to move, `MovedKey` for moved keys; API cfg names
  permanent; every released side-file format stays readable). `tests/fixtures/released/0.9.1/`: the player files as
  the public release wrote them (reconstructed from commit 7ac9a6c), with every value off its default; later versions
  are captured at release (checklist 5, steps 3b and 3c). The self-test "upgrade: a released version's player files
  are all read" runs Saddle Up's cfg through the live page's own carry-over (`MountSettingsPage.Upgrade`: every old
  line lands on a row and reads as its type), checks every old API key is still bound, reads the side file through
  the real reader, and the set-aside of an unreadable one. The release checklist's "nothing shipped" bullet is
  replaced by this check. Open: changed defaults never reach existing players (BepInEx writes every value); the
  stamp-and-table mechanism is described, decision pending. The live run with the 0.9.1 file (2026-10-18) found the
  last row's old line still in the cfg: BepInEx saves inside every Bind, before the page drops the old copy. A page
  now holds the mod's save-on-set while it binds and writes the file once at Publish (`FinishBinding`; one write on
  a first start instead of one per row, as the API's own cfg already did), and logs how many old lines it left out.
- Looks.cs: the skin rig (docs/skins-design.md step 1): overlays under bdy/hed, the hide re-asserted per frame, the apply
  in Camera.onPreCull, attach/detach at the view's lifecycle points, refcounted pictures; the self-test "skin: on, off,
  removed"; the built-in skins under src/GK2DonkeyAPI/assets/skins (tools/creature_sprites.py writes them).
- Step 2: the pack loader (Looks.RegisterFolder/RegisterPack/Unregister/Validate), the self-tests "skin pack: a folder
  read, validated, worn, withdrawn" and "skins: no leaks over spawn and remove".
- Step 5: the public registration API (a mod's textures stay its own; the same owner replaces, another is refused).
- Step 4: recipe skins (LooksRecipes.cs: the frames read back from the character atlas onto the 96x96 canvas, a pixel
  rule per recipe, 6 frames a tick, the switch whole); the self-test "recipe: built from the donkey's frames, worn whole".
- Step 3: the List row across FrameworkLink (AddDropdown with the ids; MenuLabels writes the labels), SettingsRows and
  the look bar; docs/skins.md for pack authors.
- Step 6: Effects.cs, the particle effects system (templates copied from the game's particle systems by material, the
  materials held through Addressables; a holder per instance that follows the body; ring, body and trail emitters;
  the night test on the `_SunLight` shader global, no game member; the trail's pieces pooled and replayed in place,
  nothing made or destroyed while walking), Hellfire registered through the public API; the
  self-test "effect: hellfire lit, follows, out, gone with the donkey"; `Perf.S.Effects`.
- The layers kept apart on the panel: rows the ridden creature sets are orange and locked on the Riding tab (tooltip
  says where to tune them), orange in a donkey's edit mode (drag = that donkey's own, right-click = the creature's
  back), owners named plainly; a cleared instance key returns to the creature's value (mods' Clear too).
- The tuning panel's Mounts tab (TuningWindow.Mounts.cs): a creature's own values edited live and saved into its
  skin.cfg (Looks.SaveCfg / ReloadCfg, Kinds.Touch / Redeclare); the creature generator leaves an existing skin.cfg alone.
- Kinds.cs: the mount kind registry (code and pack declarations, the donkey kind), the kind resolved on the skin with a
  version counter, the tuning merge per style on the skin rig, the cart rule in TryMount / Hitch / SwitchStyle, the
  abilities ticked on the rig (`Perf.S.Abilities`); self-tests "mount kind from a pack" and "mount kind from code".
- Looks.cs reworked for scale: a `SkinBase` record (the donkey's keys, parts, canvas, pivot, ppu, sprite prefix,
  atlas, back line; everything reads it), one sheet texture per skin filled a few frames a tick from any source (PNGs,
  a sheet PNG, a recipe; `Loaded.Produce`), sprite sources for code; the rig's parts as slots per base part; the
  self-tests count one texture per skin. The per-frame apply allocates nothing (Perf.S.Looks shows bytes).

- Investigation: can the mods bring art from outside the game (another animal, flames, fiery steps)?
  docs/external-assets.md, and tools/GK2AssetProbe (dev only, never shipped; Shift+F7) to answer it in game.
- Design draft for "Looks" (Hellfire, skins in an open folder format, sounds of a skin, an optional "Earn it"):
  docs/mount-looks-design.md. Not built.
- The skinning system designed in full against the code (lifecycle, drawing, formats, persistence, the public API, the
  settings row, validation, game updates, tests, build order): docs/skins-design.md. Not built.
- tools/skin_recipes.py (with tools/pngcolors.py): prototype "recipe" skins made from the donkey's own frames, for
  judging in game with the asset probe's test skins (local previews, never shipped).
- tools/skin_preview_html.py: a local HTML page animating every test skin next to the plain donkey (every clip, both
  facings, day or night); it embeds the game's frames, so it stays on the dev machine.
- The asset probe 0.2.0: the window in tabs (Skins, Fire, My files, Capture, Game, Old tests), and a Capture tab that
  films the donkey and its rider for 4 s, played on the preview page; and "Export the rider": the keeper alone
  (rider.png), which the preview page can lay over every skin.
- tools/creature_sprites.py: a giant worm, a giant snail (with a saddle), a giant rat and a dragon in two palettes, red and blue (128 canvas: body on the seat line, a bat wing raised over the back so it shows above the rider, a scalloped membrane, spines, belly plates; from the user's reference sheets, our own drawing) drawn by code (our own art, no game pixels), a picture for
  every donkey frame, fitted to the donkey's seat and ground; and a high-wing dragon recipe (dragon_high) whose wings
  show above the rider.

## 0.9.1 (GK2 Donkey API 0.29.1), 2026-10-16

For players:

- **Smoother frames while playing.** The Mods menu's donkey picture and its lists looked for the Mods window four times
  a second while you played, even with the menu closed: each look took 1-2 ms, so a frame of 2-3 ms extra four times a
  second. It now looks only at the main menu or while the game is paused (the only times the window can be open).
- **No memory held by parked carts.** A cart left standing (H while driving) and hitched again was kept in memory
  afterwards (about 50 objects each, up to about 30 carts). It is now let go at once.
- **Two logs stacked on the cart** line up better: the top log is placed slightly differently along the cart.

For mod authors:

- No API change. `Mounts.ApiVersion` is 0.29.1; mods asking for 0.28.0 or 0.29.0 load as before.

Development:

- Profiling with Performance Hog (a per-mod profiler for GK2, used on the dev machine only): docs/performance-hog.md.
- tools/GK2PerfRunner (dev only, never shipped): drives the keeper round a recorded loop and runs scripted profiling
  suites (on foot, riding, the cart with six items, standing, calling and leaving the cart), several in a row, with a
  count of the API's caches to find leaks. tools/perfhog-report.py reads the reports (`--suite`, `--series N`).
- The showcase director (dev only) opens with Shift+F9 now (F9 and Ctrl+F9 are Performance Hog's), and no longer
  floods the log with the game's "Cannot find instance of EnvironmentEngine" at the main menu.
- Results and baseline numbers: docs/performance.md.

## 0.9.0 (GK2 Donkey API 0.29.0), 2026-10-16

The first public release (sources as committed in `6be69a2`).
