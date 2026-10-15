# Cargo review: every load, one by one, with a verdict that is kept

Built 2026-10-14 (dev tools only). Code: `src/GK2DonkeyAPI/CargoReview.cs` (the store and the fingerprint,
scene-free), `src/GK2DonkeyAPI/Dev/TuningWindow.Review.cs` (the Cargo tab's review strip), the review keys in
`Plugin.cs` (`[Debug]`), the offline test `tests/PackerScan --review`. The marks live in the repo's `cargo-review.cfg`
(next to `cargo-layout.cfg`; not embedded, not shipped).

## Why

The coverage tools ([cargo-coverage.md](cargo-coverage.md)) turned the 1,715 loads into a few dozen contacts and a
"Tune next" ladder: tune each contact once, in its smallest load. That settles the values. It does not say that every
load *looks* right: a tuned contact can still meet a neighbour badly in a load nobody has looked at. The user asked
(2026-10-14) for a way to look at every single load, mark it, stop and continue another day, and never lose the work.

## What it does

A **review strip** on the Cargo tab, between the whole-layout line and "Tune this load", folded by default:

```
[-] Review every load    312 of 1,715 reviewed · 290 ok · 19 wrong · 3 later · 4 to re-check
[progress bar: ok green, wrong and re-check orange]
Items: [1 7/7] [2 28/28] [3 84/84] [4 120/210] [5 0/462] [6 0/924]   Show: all | unreviewed | wrong | to re-check | later | ok
With:  [log] [flour sack] [tall crate] [low crate] [slab] [corpse] [zombie]
[<] [>] [Next unreviewed] [Next wrong] [Next to re-check] [Load it]
Load 313 of 1,715: 2 × log + slab + corpse · 0 untuned · unreviewed
[Looks right] [Wrong] [Later] [Clear]   Why: [floats] [sinks] [overlaps] [off the bed] [wrong spot] [other]
Note: [________]   [x] load as I step   [x] next after a verdict
Keypad4/Keypad6 step, Keypad1 ok, Keypad3 wrong, Keypad2 later (while this tab shows and no text field has the focus).
```

- **The walk** is the analysis's load order: by number of items, then by class. That is the tuning ladder (one item,
  then pairs, then the layers above), so an ok that freezes lower relations comes before the loads built on them.
  The filters narrow it: number of items, classes the load must contain (tick several: all of them), and which marks to
  show. Stepping keeps the current load when it is still in the walk, else lands on the nearest after it.
- **Load as I step** (on): every step puts the load on the cart as a test load; the first `>` of a session loads the
  load the strip stands on instead of moving past it (the lone corpse at position 1 was skipped that way on the first
  run, 2026-10-15), (`TestLoads.Put`: real items, a corpse
  with its organs, a zombie made the game's way; the cart must hold nothing of yours, the strip says so otherwise).
  The verdict buttons work only while the cart shows exactly this load (the same classes as a multiset).
- **Looks right** = "Everything here looks right" (every contact of the load still on the fallbacks is frozen as it
  lies) + the mark **ok** with the **fingerprint** of the load's tuning taken right after the freeze. With *next after a
  verdict* the walk moves to the next unreviewed load and loads it.
- **Wrong** = the mark **wrong** with the ticked reason tags and the note. The load stays on the cart: tune it in the
  boxes below, then Looks right turns it ok. The wrong loads are a filter (*Show: wrong*) and `Next wrong` walks them.
- **Later** = the mark **later** (you saw it, you will come back): counted as reviewed, with its own filter (*Show:
  later*) so the walk can end and the laters are found again; the walk moves on. Like the other verdicts it needs the
  load on the cart.
- **Clear** removes the mark.
- **The ladder's Accept buttons** ("Accept this load", "Accept and next" under *Tune this load*) freeze the same way
  and record the ok in the review too, so an accepted load is never left unreviewed; "Accept and next" then follows
  the contact ladder, not the review walk, which stays where it is (asked 2026-10-15).
- **Where you stopped** is kept (`position =` in the file): the next session opens the strip on that load.
- **Keys**, `[Debug]` cfg: `ReviewPrevKey` / `ReviewNextKey` (Keypad4 / Keypad6), `ReviewOkKey` (Keypad1),
  `ReviewWrongKey` (Keypad3), `ReviewLaterKey` (Keypad2). Polled only while the Cargo tab shows with the strip open
  and no text field has the keyboard focus; the numpad nudges (`CargoNudgeUp/Down`, + and -) are untouched.

## The fingerprint: why an ok can be trusted later

A load's look depends on the stored values of its contacts, and tuning a contact later moves every load that uses it.
So an **ok** is an ok of exactly this tuning: the mark carries a short hash (SHA-1, 16 hex digits) of

- every contact of the load (`CargoCoverage.Load.Keys`): for an *on* contact the pair's top relation (tuned flag, both
  places), for *beside* its beside relation, for *alone on a pair* the item's `onpair` offset and its set flag, for
  *alone* the `alone` offset and its flag; the pair's `mode` too when the load has two items (that is what a two-item
  cart shows);
- for a contact still on the fallbacks, the fallback settings it is placed with (the coverage signature without the
  layout version: floor, gap, overhang, nestle, layer shift, bed);
- the class offsets (`along/up/across`, `top_*`) of every class in the load.

When the layout or the fallbacks change (the coverage signature), the panel recomputes every load's fingerprint (1,715
hashes, a few milliseconds) and an ok whose stored fingerprint differs becomes **to re-check**: the progress line
counts them, *Show: to re-check* lists them, `Next to re-check` walks them. An undo after an ok makes it to re-check as
well: correct, something it depended on moved. Looking at it again and pressing Looks right records the new fingerprint.

The offline test checks the discrimination: a 1 cm change to `log on log` changes exactly the 77 loads that use that
contact and no other; a change to the log class offset exactly the 792 loads with a log.

## The file

`src/GK2DonkeyAPI/cargo-review.cfg`, written 0.75 s after the last change (as the layout file is), on the machine with
the sources only (elsewhere the marks last the session and the strip says so):

```
# GK2 Donkey API cargo review: every load the cart can carry, looked at one by one in the Cargo tab (docs/cargo-review.md).
# <classes, sorted> = ok | wrong | later, then | date | fingerprint of the load's tuning when marked | reason tags | note.
position = corpse+log+log+slab

corpse+log = ok | 2026-10-14 22:40 | 3f9a2c1e77b0d4a1 |  |
log+log+slab = wrong | 2026-10-14 22:41 | 8b0c... | floats,overlaps | the slab's end hangs over the second log
```

One line per marked load, in the analysis's order, so a diff reads top to bottom by size and a session's work shows
as a block of added or changed lines. Keys are the classes' **readable names** sorted (`corpse+log+log`), never the
representative item ids: the sheet picks the first item of a class in the game's list, which a game update can change;
the names don't. The file is the user's work product: it is committed with the layout.

## Safety and the frozen view

- Nothing here touches cargo of the player's: a test load only ever replaces a test load (`TestLoads`), and the strip
  shows the refusal when the cart holds something else.
- Verdicts and loads are disabled while the layout compare (`CargoLayout.Comparing`) is on.
- The strip follows `DrawCargoTab`'s rule: position, state and counts are read before any button is drawn; a click's
  effect shows from the next pass (`reviewFpSig = null` makes the next pass recompute the states).

## Tests

`tests/PackerScan -- --review` (in `tmp/release_scans.sh`): every load has its own key, keys are the readable names
sorted, fingerprints are deterministic, a contact's value changes exactly the loads using it (and a class offset
exactly the loads holding the class), the file's text reads back unchanged, marks are written in the loads' order,
tags, notes (a bar in a note is replaced) and the saved position come back, an ok keeps its fingerprint. 12 checks.

**To see in game** (not yet, built 2026-10-14 22:25): mark three loads ok, restart, they are still ok and the strip
opens where it stopped; tune a contact one of them uses, it flips to re-check and `Next to re-check` reaches it; undo,
it flips back; mark one wrong with a tag, `Next wrong` finds it; the file's diff after the session shows only the lines
touched; the keys step and mark, and do nothing while the note field has the focus.

`tests/PackerScan -- --review-check`: the repo's `cargo-review.cfg` against the layout as it is now: every ok of a
fully tuned load must carry the fingerprint the layout gives today (the game wrote it, the scan recomputes it: the two
paths agree and nothing was edited since); the ones that differ are listed, as the panel lists them under
"to re-check". Run after a session to see what the next session will ask to look at again.

## Build log

- 2026-10-14 22:25: built (store, strip, keys, test), dev build deployed. The dist must be rebuilt before the release
  (the Dev folder ships in the DLL), with check-dist and the scans.
- 2026-10-15, the first in-game run (15 ok marks, 46 loads stepped): the strip, the file and the position resume work.
  Found and fixed on the way: (1) the first `>` of a session moved past load 1 before anything was on the cart (the
  lone corpse was skipped); (2) an ok's fingerprint read "tuned or not" from the analysis, which is recomputed half a
  second after the accept, so an ok taken right after freezing a load carried the fallbacks and flipped to re-check
  on its own (4 of the 15 marks, the corpse pairs: they ask for one more Looks right each); now the entries decide;
  (3) a verdict made in the last 0.75 s before quitting was not written: the quit hook and the per-frame save now
  flush the review too; (4) the offline test loaded the real file under its own marks once that file existed.
- 2026-10-15, hardening before the long run: (1) `CargoPacker.AlgorithmVersion` goes into every fingerprint, so a change
  to how loads are built (same stored values, another look) flags every ok; (2) the seat-on-shapes switch is part of
  the fallbacks; (3) the file carries `fingerprint-version`; when the code's format moves on, the strip offers one
  press to re-stamp the ok marks (the user vouching that the layout is unchanged), so a format change never costs the
  review; (4) the file is written to a temp file and swapped in, never torn by a crash mid-write. Test 15/15.
- 2026-10-15, colours while reviewing: the tint rule compared placed items by reference (a struct: every lone on-top item
  was blue); fixed. Then the layer shading (30% toward white per layer) washed a top item's orange out on the pale corpse
  sprite, and two shades of one orange read as the same: loads of three or more now tint one colour per item (blue,
  orange, green, magenta, yellow, cyan by place in the load), the same colour on the cart and in the panel's names; one
  and two items keep blue / orange by role, which the pair box and Alone are built on. The verdict and jump buttons are
  coloured (green ok, red wrong, yellow later / re-check). The ladder's Accept buttons record the review's ok too.
- 2026-10-15 evening, **fingerprint v3 and one-pair offsets:**
  - `PackerScan --review-check` read 38 of 61 oks as to re-check, and none of the 38 matched the layout at any commit
    since the morning. Every one was a load of two or more classes; every one-class load matched. Cause: the
    fingerprint listed the class offsets in the analysis's class order, and the game numbers the classes differently
    from the scan (log before corpse offline, corpse first in the game). In the game the marks were consistent; offline
    they could never be checked, and a game update that added an item could have reordered them in the game too.
  - **v3** lists the class offsets in id order (`FingerprintVersion = 3`). The file was brought over by
    `PackerScan --review-migrate --write`, which checks every ok against every ordering of the old class section and
    re-stamps only an ok whose stored fingerprint one of them reproduces. All 61 were proven unchanged, so nothing was
    re-stamped blindly; an unproven ok would have kept its old fingerprint and shown as to re-check. The old file is
    kept outside the repo. Afterwards `--review-check`: 61 of 61 match.
  - **Probe:** the panel logs `[review] at <load>: stored X, now Y <- <text>` for each load it puts on the cart, and the
    same line with `ok` for each ok; `--review-check --verbose` prints the scan's line for each stale ok, so a future
    mismatch can be compared field by field instead of guessed at.
  - **One-pair offsets** (`[onpair <item> on <a>+<b>]` in `cargo-layout.cfg`) go into the fingerprint of every load the
    item and the pair fit in (a superset, never a miss). With none, the text is exactly the v3 text, so adding the
    feature re-flagged nothing. PackerScan `--review` checks 4: none changes nothing; one for low crate on two low crates
    changes exactly the 120 loads with three or more low crates; the packer moves only the lone crate and only on that
    pair (not on two corpses); the entry reads back.
