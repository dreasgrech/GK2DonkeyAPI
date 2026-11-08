# Mounts left in the world

The user, 2026-11-05: "start working on a new feature which is togglable with an option in our global options menu
where if enabled, we leave summoned mounts along with their foals in the game instead of despawning them. So if im
riding mount a and then summon mount b from the journal, we will summon and ride mount b but leave mount a spawned at
that spot in the world and obviously have it persisted too. This will make the world much more lively."

This page is what was found, what was decided and built, and what is left to look at in the game.

**Status, 2026-11-05 morning** (built and deployed through the night; nothing committed; the version bump is the user's):

- **The first F11 (13:06): 1946 passed, 2 failed**, both in "another mount chosen while riding"; four runs of that
  scenario measured them (CHANGELOG, Development):
  - the test mount's distance was read in the frame the donkey came, before its step was drawn (a test fault: read
    two frames on now, passing);
  - after Drive the cart, the donkey left wasn't stepped aside: every spot was ruled out by the views the hitch and the
    unhitch had just let go, which stood in the world till the frame's end (an older API fault of every hitch, unhitch
    and removal; docs/pooled-parts.md). Fixed 15:49 (a view let go is switched off at once); the scenario now checks
    no view let go stands round him in the switch's frame. **Then the whole F11 (16:06): 1949 passed, 0 failed, 0
    warnings.** Next: the hand list (section 5).
- **Since then (the user's test, 2026-11-05 afternoon): off sends the mounts left home.** Three mounts out, the option
  turned off, they stayed; now they go home (section 3), one ridden by hand once he is off it, a load with the
  option off too. The row's text says so (11 languages) and "Pick it again in the journal". Hand items 6, 8 and 11.
  **The whole F11 on it (16:54): 1957 passed, 0 failed, 0 warnings.**
- **The user's test, 2026-11-05 evening: a left mount picked from afar comes without its babies.** All mounts called
  inside the house, the donkey ridden out (its babies came through the door), then the dragon and the rat picked
  outside: their babies stayed inside. The cause is the API's call (`ComeTo` moves the mount alone, and a baby waits for
  a ridden parent that never stands). The same was fixed for doors on 2026-10-24. Investigated in
  [foals-called.md](foals-called.md). Built the same evening (section 8 there): a called mount's babies out of reach
  come with it, in or out, as through a door; the safe spot too. Whole F11 green 19:19 (1978/0/0).
- **First F11.** New: three scenarios named "mounts left in the world" (two in "API, side file, carts,
  colours", one in "Flying") and Saddle Up's row check in the integration tests. Every other scenario runs with the
  switch held to the old way (`HeldForTests`), so green there means the old path is intact, not that the feature ran.
- **Then the hand list (section 5) with the option on** (Options, Mount, "Leave your mounts in the world"). Two things
  are meant to be so:
  - at a journal switch you are on foot for a moment (the dismount sound) before the new mount is under you: the one
    you had is left, not changed into the new one;
  - with the option off (and no mount left from when it was on) everything is as before the feature.
- **The choices** taken without you are in section 3; each is yours to change.
- **The review passes** (two fresh readers each time, whole files; the tally in section 6): passes 1 to 8 each found
  something to fix (fixed, each deployed); 9, 10 and 11 were clean; 12 found one more (a third party's skin of a
  pack's mount with the pack away; fixed, deployed 07:31), so the clean count started again; pass 13 was clean but a small fix it pointed at (old short ids) came after it, so the count starts at 14.
  **Passes 14 to 18 were clean on the final code: five in a row** (the code as deployed at 08:07; only this page,
  the CHANGELOG's player note and the hand list's wording changed after).
- **The nits** of the last three passes (not must-fix, not fixed) are in section 7, for you to judge.
- **Texts to look at** (a reader's notes, not changed): the row says "Call it again and it comes back to you", meaning
  choose it again in the journal (the ride key calls only the journal's mount); and the button's verb in fr, pl, ko
  and ru reads "send back" or "let go" (their descriptions say home).
- `tools/l10n-sync.py --check` shows the four new Saddle Up keys until a dev run dumps `en.csv` again.

## 1. How a mount was called before

- **One mount out.** The player's companion (`MountCompanion`, Saddle Up's `Mounts.Companion`) has one mount in the
  world: the instance under the key `donkey`, its babies under `foal`, `foal2` ... `foal5`, the cart under `cart` while
  it stands on its own.
- **Another mount was a reskin.** The Mounts panel's Ride and Drive the cart close the panel, then make the mount chosen
  his (`ReleasePanel(commit)` sets the companion's skin, then `ApplyMountOwn` and `ApplyLook`), then call it
  (`UseFromPanel`). Another mount was another skin on the same instance, so the mount ridden turned into the one chosen
  under the rider, and its babies with it. Nothing despawned: the instance changed what it is. That is why the old mount
  seemed to vanish.
- **Each mount's own settings** were already per mount (`MountOwn`: its name, babies, volumes) and per skin
  (`MountLook`: the colours). A mount left out keeps those.

## 2. What was built

### The rule

- **One of each mount, at most.** The companion keeps *families*: a mount with its babies. The current one keeps the keys
  `donkey` and `foal` ... (so every save and every mod reading them is as before). Each mount the player had and left
  stands under keys of its own, `left:<mount id>` and its babies `left:<mount id>#foal`, `#foal2` ...
  (`MountCompanion.LeftKey`, `LeftFoalKey`). Found again after a load by those keys (`OnGameReady`).
- **On (`LeaveMountsInWorld`).** When another mount is chosen (the panel's Ride and Drive the cart, or anything that sets
  `Skin` to a skin of another mount), the family out:
  - gets off the keeper (a ride ends where it is: `Mounts.Dismount`);
  - leaves its cart standing where it is, if it pulls his (the cart is never taken along: the next mount fetches it).
    Another mod's cart it pulls stays with it;
  - goes under its left keys, its babies with it. Their follow switches (the keeper on foot, indoors) are cleared, so
    they stay with their parent;
  - steps aside when it stands where the keeper is (he was on it, or by it), so the mount called comes onto a spot of
    its own (`StepAsideFrom`, below);
  - is kept with the save as his mount is (`KeepWithSaves`).

  The mount chosen is then the one left earlier, if there is one: it is his again (under `donkey` and `foal` ...) and
  comes as a called mount comes (`ComeToKeeper`). Otherwise a new one is spawned at the call, as before.
- **Off (the default).** The mount he has changes into the one chosen, under him, as before, babies and cart with it.
  The one exception is a mount left while the option was on: it is taken back when its mount is chosen (one of each
  mount), and the one out goes home instead. Its babies go with it; any cart it pulls is left standing first, so no cart
  and no load is lost. Since the user's call of 2026-11-05 afternoon, no mount stays left while the option is off:
  switched off, or a save loaded with it off, the mounts left go home as the button sends them, one he rides by hand
  once he is off it (the companion's tick, `SendLeftHomeWhileOff`). So the exception above is a left one he rides by
  hand and then picks in the journal: it is his again, and the one out goes home.
- **At a load** (`OnGameReady`), a save whose mount out isn't the one chosen (a save from before the choice) is handled
  as a switch, except that nothing goes home there, on or off: the one out is left instead. With the option off, the
  mounts left then go home (the tick, above): the save's mount out is never the one sent.
- **A call refused where he stands** (indoors, busy) after the switch: the mount chosen is spawned for the call (at the
  scene's origin, to be placed by its come to the keeper) and, refused, goes again rather than stand there with its
  babies (review pass 5: with the mounts left every new mount's first call spawns one; before, only the first call
  ever did, and left it there). The one left stays. With the option off and no mount left it stays where it was
  spawned, as before (review pass 6: off is the old way exactly).
- **Drive the cart refused after the switch** (the armour on, a fight): the mount he rode is left and the cart can't
  come, so the mount chosen comes for a ride instead (`UseFromPanel`, the frame the switch got him off). Before, the
  mount changed under him and he rode on: the same end, a mount under him and the cart refused. A refusal that comes
  only once the cart is on its way is the cart's own rule: with more on it than the mount chosen pulls, the cart comes
  over and stands unhitched beside him, with the bubble saying why (the user, 2026-10-27: "still bring the cart to him
  but dont hitch it"); he is on foot then, beside the mount chosen.
- **In the air** (flying), the ride can't end at once: the dismount lands the mount first (`Flight.HoldForLanding`, the
  "land first" bubble). So the choice waits: the mount ridden stays what it is, and once it is down (checked four
  times a second) the switch is made and the panel's call that came with it (Ride, Drive the cart) is made then
  (`TakeDeferred`). Before review pass 1 the flying mount changed into the one chosen in the air and nothing was left.
  The journal opened and closed again without Ride while it lands keeps the waiting choice and its call; a load while
  it waits makes the choice his at once (the page has written it to the cfg already). When the page gives the waiting
  choice again and it can be made now (the landing has let him off), it is made and its call kept, made at the next
  tick once no panel holds the controls. The panel's Ride or Drive pressed
  while he flies a mount that isn't his (a left one ridden by hand) waits for the landing the same way, instead of
  being refused.
- **Two of one mount** come from rare paths only: a pack that was away at a load, a leave that failed (its key taken by a
  record that couldn't come back). (A left one that can't be taken back because the key `donkey` is held by such a record
  can't be replaced either: the spawn needs the same key; the call says the mount could not be spawned.) At the next switch away from it the one out
  goes home instead of being left, and the one left stays. When leaving fails (its key taken, its cart wouldn't come
  off) and the mount chosen has one left, that one goes home: one of each mount stays the rule. Never at a load: there
  nothing goes home (the one out is left, or, its left key taken, changed in place into the chosen mount, so two of
  one mount can stand until the next switch).
- **A baby without a key** (one whose key was refused at a switch got none rather than keep another family's) is still
  its parent's child, marked with its slot in its data (`strayslot`): at a load it takes that slot back, else an empty
  one, and the slot's key (`TakeStrayBabies`), so the family doesn't get a baby too many. Only a marked one: a mod's own
  unkeyed baby of the same parent stays the mod's.
- **A scene loading** (a door's frame, `Mounts.IsGameReady` false) with a mount out: another mount chosen then waits for
  the game to be ready, as one chosen in the air waits for the landing (with the option on or mounts left; off with none
  left it changes in place, as before). The wait also holds while the Mounts panel is open again.
- **A companion made in the middle of a game** (a mod making its own late; Saddle Up makes its in Awake) finds its
  mounts without switching any, and takes its mod's first skin in place, as before the left mounts.
- **A skin of the same mount** (Hellfire for the donkey) is still put on the mount out, where it stands, on or off.

### What a left mount follows

- **Live from the journal:** its name, its number of babies (born or sent off at the next second's check), and its colours
  (the look of the skin it wears, `LookChanged` for that skin; `Looks.LoadedReady` for its lightness anchors).
- **Its skin stays the skin it was left in** until it is chosen again (decided 2026-11-05 with the advisor: the panel's
  Skin row of a mount not chosen reaches no instance; `ApplyMountRows` only gives the companion the chosen mount's).
  Chosen again, it wears that mount's Skin row then.
- **Its babies** stay with it (their follow switches off), each its own as before (the baby salt is in each parent's
  data, so a left mount keeps its babies' sizes and shades; a new mount gets a salt of its own).
- **A mount whose pack is away** (not installed this time) is left exactly as it is: its babies, name and colours stay
  until the pack is back (the journal has no rows for it, and its own settings would be the companion's first ones). The
  same for its colours while only its skin's pack is away.

### The cart

- **Left standing** by the mount driven when another is chosen (above), keyed `cart`, his.
- **Fetched from wherever it is, for a mount of his:** the cart key or Drive the cart brings his cart. When it stands
  hitched to another of his mounts (one left in the world, ridden there by hand and the cart called; or his own mount
  standing with it while he rides one he left), it is unhitched there first and brought (`BringCart`), never a second
  cart. For another mod's mount it is as before (a cart of its own comes): a cart left again by another mod's mount
  is that mod's (an unhitched cart is its donkey's owner's), so his loaded cart must never go onto one (review pass 5).

### Stepping aside

The mount called comes to where a ride would put it: under the keeper, who stands where the mount he got off stands. So
the left one steps aside (`Mount.Instance.StepAsideFrom`, in IdleShuffle.cs beside `StepOutIfInside`):

- **Where it goes:** 1.8 world units first, then 2.3, then 1.4, until a spot fits; a mount drawn wider than the donkey
  (its standing frame's width, `EffectScale`, up to twice) steps that much further and keeps that much more room. The
  order is ahead of its nose (it leads, the one called stands behind it facing the same way, and a cart brought to that
  one stands behind it in turn), the two ahead diagonals, away from and towards the camera, the two back diagonals, and
  behind last. Two dragon-wide bodies side by side need about 1.3 apart, so 1.8 clears them. (Behind its tail came first
  until review pass 4, and second until pass 8: the cart brought for Drive the cart stands right there, so a mount
  already left ahead sent the next one into the cart's spot.)
- **Once per switch:** the panel's switch steps it, and its call of the mount in the same frame would see the body still
  drawn on the old spot and step it again (review pass 4: 3.6 instead of 1.8, the second clear of nothing); a mount that
  stepped in the last few frames isn't stepped again (`SteppedAsideJustNow`).
- **Each spot is checked as `StepOutIfInside` checks one:**
  - ground, at most 0.6 higher or lower;
  - nothing keeper-blocking at its body or root;
  - for a rideable mount, its whole footprint free;
  - on the keeper's part of the walkable mesh, in a clear line from him;
  - at least 1.6 from the keeper (`CalledMountRoom`);
  - not crowding another instance: every instance, not the frame's list, since the cart just left is new this frame;
  - 1.5 clear of where the cart it just left was drawn (that cart has no view yet to tell);
  - not near another instance's cart either (its cart's body, `CartPoint`).
- **Its body** is where its view draws it (measured as `ComeTo` measures it), not only the shuffle's own measure, which
  never runs while idle shuffle is off. A step or turn under way is stopped first, and it shuffles round its new spot.
- **Not with a cart on** (another mod's cart it keeps): the cart would move with it unchecked, and nothing shuffles a cart.
- **A left one ridden by hand** that the keeper gets off because he calls another from the journal steps aside too; his
  cart on it is left standing first (as at a switch), another mod's stays on it (and it doesn't step).
- **The ride and cart keys too:** when his mount is called to him (`UseNow`), a mount of his left where he stands (one
  he just got off, or walked up to) steps aside, clear of the one that came; his cart on it is left standing first
  (review pass 7), another mod's keeps it from stepping.
- **When none fits,** it stays and the log says so.
- **No pop on screen** from the journal: the panel covers the screen while this happens and closes onto the new picture.
  The step is seen where nothing covers it: the ride and cart keys' call, a switch made after a landing, a load (just
  after the fade), and a mod setting `Skin` itself.

### The option

- **Saddle Up**, options window, Mount section, after "Keep your mount with the save": **"Leave your mounts in the
  world"**, off by default (cfg `LeaveMounts`). The description says it: "When you call another mount, the one you had
  stays where it is with its babies, kept with the save like your mount. Call it again and it comes back to you. Your cart is
  left standing beside it."
- **"Send your other mounts home"**, a button under it (greyed while none is out): every mount left goes home with its
  babies (`SendLeftAway`). A cart one of them pulls is left standing first (his own is his cart again, another mod's
  stands on as its own), so no cart and no load is lost. The way back from a crowded world without choosing each mount
  in turn.
- **Translations:** the rows' labels and descriptions in all ten languages, in each file's own words for mount, babies,
  cart and save. Every character is one the game draws in that language (`tools/l10n-glyphs.py`): the first Korean
  wording's 둡 was not, so its sentence was reworded.
- **On an older API** (before this build) the row is left out with a warning (`LeaveMountsRow`, NoInlining, as ToneRows).
  NeedsApi rises at the release's version bump (the user's).

### The API

- `MountCompanion.LeaveMountsInWorld` (bool, off).
- `MountCompanion.LeftMounts`: the donkeys of the mounts left, one per mount; each one's babies are its `Children`.
  Made again only when they change (the button's greying reads it twice a second).
- `MountCompanion.SendLeftAway()`: the mounts left go home (above); returns how many.
- The keys above, for a mod reading the side file or `Mounts.InstancesOf`.
- `SendAway` is the current mount's, as before; `SendLeftAway` is the left ones'.

## 3. Choices taken, for the user to change

- **Off by default.** The world of a player who updates stays as it was until they turn it on.
- **A left mount ridden by hand (E) doesn't become the chosen one.** The journal still chooses; the ride key calls the
  chosen mount. Riding one by hand is riding it, as any world donkey. Making the mount ridden the chosen one would mean
  the API changing the page's Mount and Skin rows from a ride: possible, not built.
- **Turning the option off sends the mounts left home** (the user, 2026-11-05 afternoon: "switching the option from
  the options should unsummon the other mounts you're not using right now"), with their babies, as the button does (a
  cart one pulls is left standing). One he rides by hand goes once he is off it (his choice), and a save loaded with
  the option off sends its mounts left home too (his choice: off means only his mount is out). The companion's tick
  does it, four times a second, held in F11 runs and behind the Mounts panel (`SendLeftHomeWhileOff`).
- **Its skin stays** until it is chosen again (above).
- **A left mount's sound volumes** (voices, steps, babies' steps) are the mount out's: they are one setting for the
  mount you have, and a left one's volume rows in the journal reach it when it is chosen again. Its name, babies and
  colours are its own. Giving it its own volumes would need them as per-mount values, as its colours are: not built
  (review pass 18 noted it).
- **Flying:** choosing another mount in the air waits for the landing (above), then switches. Not refused.
- **A load never sends the mount out home** (above); with the option off, the mounts left go home after it (above).
- **Another mod's mount ridden, his cart called:** a cart of its own comes, as before (his own stays where it is): his
  loaded cart on another mod's mount would be that mod's once left (review pass 5). So with the option off and no
  mount left, nothing differs from before the feature.
- **"Send your other mounts home"** skips a left one he is flying (no landing forced on him); it goes at the next press.

## 4. Compatibility

- **The side file** keeps the left keys as any key (a plain string per instance; `docs/persistence.md`). An older API
  reads them as instances of the owner with keys it doesn't know: they stand where they were, and nothing calls them.
- **Saddle Up's cfg** gets one new line, `LeaveMounts`, in `[2. Mount]`; nothing moved or retired.
- **The current mount's keys** are unchanged (`donkey`, `foal` ..., `cart`).

## 5. Tests

**In the API's self-tests (F11, "API, side file, carts, colours")**, through the run's own companion and a mount of the
test's own (a code mount with a temp pack's skin, as "mount from code" makes one):

- **"mounts left in the world: another mount chosen while riding ..."**
  - riding the donkey with two babies, the test mount chosen: the donkey stays, off the keeper, under `left:donkey`, its
    babies under its left keys with their follow switches off, stepped 1.6 or more aside;
  - the test mount comes and is ridden (a new one, `donkey`), the donkey not inside it, the new one's own baby;
  - the donkey chosen again: the same instance (its id) is his again, its babies under `foal` again, the test mount left
    with its baby;
  - the journal's babies (2), name and coat colour reach the left one, the coat not on the one ridden;
  - the cart: driven, the test mount chosen, the cart left standing (`cart`) by the donkey and not under it, fetched by
    the test mount's Drive, one cart all along;
  - the donkey left ridden by hand and the cart called: taken off the test mount, still one cart;
  - the donkey chosen while riding it: his again, the ride and the cart go on;
  - the test mount left, ridden by hand, takes the cart; the mounts left sent home: it goes, its cart left standing (one
    cart), the donkey stays his.
- **"mounts left in the world: off ..., a load ..., the side file ..."**
  - off: the donkey left earlier comes back and the test mount goes home with its baby;
  - off with none left: the mount out changes under the rider, as before;
  - a load whose mount out isn't the chosen one (on): it is left; found again by its key at the next load, its baby too;
  - the side file's text keeps the left key and the baby's parent.
- **"mounts left in the world: a save and a load the whole way ..."** (2026-11-05 evening; the user: "regarding
  persistency with the new option to keep mounts spawned, are we handling that?"), with "Keep your mount with the save"
  on:
  - the test mount drives the cart, then the donkey is chosen, so the test mount is left with its baby, its name and its
    skin, and the cart stands by it;
  - the side file is written for the run's owner into a temp folder, with only its own id counter, so no player mod's
    counter is put back;
  - all five are taken away as a load does: the donkey, the test mount, a baby each, and the cart;
  - the file goes through the side file's own restore (`Mount.RestoreFromFile`), then the companion's load step runs;
  - each comes back with its id, key, scene, place, facing, parent, name and skin;
  - the donkey and the cart are his again, and the test mount is left, with its baby under its left key, in its skin and
    with its name;
  - no baby is added, and there is one cart;
  - picked again, that same test mount comes and is ridden.

  Not in it: the game's own save and load (sleep, quit to the menu, load). Hand items 4 and 11 cover those.
  **Green in the whole F11, 2026-11-05 18:21** (1969 passed, 0 failed, 0 warnings): 12 of 12, "5 of 5 donkey(s)/cart(s)
  back", no warning in its span.

**In "Flying": "mounts left in the world: another mount chosen in the air ..."**: a flier of the test's own ridden and
taken off, the donkey chosen and Ride pressed in the air: the flier still ridden, his, in its own skin; once landed, the
donkey comes and is ridden, the flier left where it landed in its own skin.

**Every other scenario is held to the old way** (`MountCompanion.HeldForTests`, set by the runner for the run): the
Mounts panel's scenarios pick a mount on the player's own companion and put it back, and with his option on that would
leave his mount in the world (review pass 1). Only the two scenarios above free their own companion
(`TestFamiliesFree`).

**In the integration tests (Saddle Up):** "mounts left in the world: Saddle Up's row": the row is in the options window's
Mount section, off by default, and reaches the companion both ways (put back); the button under it is there, greyed
while no mount is left. The player's own mounts are not
switched by the tests.

**By hand, in the morning** (turn the option on first; three mounts: the donkey, Larry and the dragon):

1. Ride the donkey, open the journal, pick Larry, Ride. Larry comes under you. The donkey stands a step ahead, with its
   babies, and doesn't walk after you.
2. Pick the donkey again and Ride: the same donkey comes back to you (its babies with it), and Larry stays a step aside
   from where you left him.
3. Drive the cart with the donkey, pick Larry and Drive the cart. The cart is left by the donkey, then comes to Larry.
4. Save, load. Every mount left is where it was, with its babies.
5. Change a left mount's babies, name or colours in the journal: the one in the world follows (the colours of the skin
   it wears: leave its Skin row as it is, a left mount keeps its skin until it is chosen again). Its sound volumes
   don't follow (section 3).
6. Turn the option off (Options, Mount): within a moment the donkey left in 3 goes home with its babies; Larry, under
   you, stays with the cart. Turn it back on.
7. Pick the donkey and Ride, so Larry is left (the cart standing beside him). Get off the donkey (the ride key), walk
   up to Larry and press E: you ride him. The ride key gets you off him; pressed again, it calls the journal's mount
   (the donkey), which comes beside him (Larry steps aside, in view: no journal covers it here).
8. Press E on Larry again and, riding him, turn the option off: he stays under you. Get off: he goes home with his
   babies, the cart stays standing. Turn the option back on.
9. Pick the dragon and Ride, fly, open the journal, pick the donkey and Ride: the dragon lands first (a "land first"
   bubble, once or twice), then the donkey comes and you are on it, the dragon left where it landed (a step aside, in
   view).
10. Options, Mount, "Send your other mounts home": the dragon goes, its babies with it.
11. Pick Larry and Ride, so the donkey is left, and save. Turn the option off (the donkey goes home), then load that
    save: the donkey comes back with it and goes home again at once; Larry stays yours.

## 6. Review passes

Each pass: two fresh readers, the whole files (not the diff), the user's sentence and this page, nothing of mine.
A pass is clean when both say "MUST FIX: none" (a reachable trigger and a wrong outcome: a mount, baby or cart lost
or doubled, a stuck state, a wrong skin); nits are logged, not counted.

| Pass | Found (must fix) | Fixed |
|---|---|---|
| 0 (mine) | the cart's unhitch dressed the mount being left in the new skin | `letting`, the switch before the skin |
| 1 | flying: the mount changed in the air, nothing left; a failed leave made two of a mount; the panel scenarios would leave the player's own mount out in F11; the step aside read the body a frame early, unmeasured offsets, a step under way; a pack away lost a left mount's babies and name | the wait for the landing, GoHome, `HeldForTests`, the step's checks, packs away left as they are |
| 2 | the journal closed again while landing lost the call; a load lost the waiting choice; a test's fallback check; the hold's lifetime; a left one ridden by hand with the cart | kept, taken at the load, reset at game-ready, the cart left standing first |
| 3 | a call alone lost on a journal closed again; a load could send a mount home; the ride key overlapped mounts | kept, a load always keeps (`atLoad`), the key path steps aside |
| 4 | the double step at every journal switch (hidden by a test frame); the cart's spot behind the left one; Drive refused after the switch; a load took a mod's own unkeyed baby | once per switch, ahead first, ride instead, `strayslot` |
| 5 | a waiting call outlived a later call; his loaded cart onto another mod's mount became that mod's; a refused call left a new mount at the scene's origin; the journal reopened during the last of a landing lost the call; two vacuous test checks | calls replace calls, the cart only for his own mounts (off is as before exactly), the fresh spawn goes, the call kept, the checks given something to check |
| 6 | the removal of a refused call's new mount ran with the option off too (off must be the old way); second reader: none | gated on the option or mounts left; the test clean-up drops a waiting choice; the flying scenario counts its mounts gone |
| 7 | first reader: none (9 nits logged); second: the ride and cart keys brought his mount into a left one that pulled his cart (it never stepped, a cart on) | the key path leaves his cart standing first, then the step, as the journal path does; "by him" is asked before the unhitch rebuilds its view |
| 8 | first reader: none (12 nits logged); second: with ahead taken by a mount left there earlier, the next left one stepped behind, where the cart brought for Drive stands | behind last in the order; the send-away key calls off a call waiting for a landing |
| 9 | none, both readers (11 and 9 nits logged) | clean: 1 of 5. The hand list says to turn the option back on after item 6 (a reader found item 8 would read as a bug with it off) |
| 10 | none, both readers (8 and 9 nits logged; one with saves, loads, doors and the journal's flow first) | clean: 2 of 5. The hand list reordered for three mounts (a reader found items 7 and 8 had nothing to work on as written) |
| 11 | none, both readers (9 and 5 nits logged; one with babies, looks, skins and packs first) | clean: 3 of 5. Hand items 2 and 6 say Ride; item 9's cart clause qualified |
| 12 | second reader (the existing F11 scenarios, one by one): none, every one predicted to pass; first (several mods at once): a third party's skin of a pack's mount, the pack away, counted as the donkey, so a load changed his mount into a donkey instead of leaving it | `FamilyMountOf` keeps the mount an id names when that mount isn't registered; the clean streak (3) starts again |
| 13 | none, both readers; both noted the pass-12 change read a short, pre-prefix mount part ("crab/x", old dev saves) as an unknown mount | not counted (the code changed after it): `FamilyMountOf` asks `Catalog.FindShort` too and gives the mount's own id; the streak starts at 14 on this code |
| 14 | none, both readers (12 and 7 nits logged) | clean: 1 of 5 on the final code. The doc says what a load does with two of one mount |
| 15 | none, both readers (9 and 8 nits logged; one as an adversarial player: doors, fights, sleep, saves, the menu, options mid-ride) | clean: 2 of 5 |
| 16 | none, both readers (11 and 6 nits logged; one on robustness: exceptions, re-entrancy, cost with 20 families of 5 babies, Saddle Up on an older API) | clean: 3 of 5 |
| 17 | none, both readers (9 and 9 nits logged; one on the side file and the game's lifecycle: slots, new game, the title, packs away, saves mid-switch) | clean: 4 of 5. The Drive paragraph in section 2 says what a refusal once the cart is on its way does |
| 18 | none, both readers (6 and 10 nits logged; one against the mod's other features: cargo, chest, corpses, zombies, babies indoors, flying, speeds, looks, KeepWithSaves off, other companions; one as the player through the hand list and the texts) | clean: 5 of 5. Hand items 5 and 7 reworded (the worn skin's colours; get off Larry before E), the volumes in section 3 |

## 7. Nits from passes 16 to 18

The readers' notes below the must-fix bar, deduplicated (earlier passes' nits are not listed here). None loses or
doubles a mount, baby or cart in normal play; each is yours to call.

**A player could notice:**

1. A left mount and its babies sound at the volumes of the mount you have (section 3).
2. A left mount still pulling another mod's cart shows that cart's lantern by the current skin's lantern row.
3. His cart hitched to a left mount ridden by hand keeps its old paint until it is unhitched.
4. "Send your other mounts home" also sends home a left mount whose pack is away (drawn as a donkey, no journal row).
5. With the option off, choosing a left one sends the mount out home, and its babies come with new sizes and shades
   at its next call (the babies' variety goes with it). The same for Larry in hand item 7 (a new Larry after item 6).
6. In the air: the "land first" bubble is said twice in one frame (once or twice on screen, by the game's bubble queue); a landing the game refuses keeps the
   choice waiting for a later landing, possibly somewhere else; each journal close during the wait says the bubble
   again and re-arms the landing.
7. Drive the cart refused once the cart is on its way leaves him on foot beside the mount chosen (section 2).
8. The ride key beside a left mount pulling his cart leaves the cart standing (review pass 7's design).
9. The send-away key removes the companion's standing cart, which with the option on is usually the one left beside a
   left mount, perhaps far away (its load is set down there, as the key always did).
10. A mount drawn twice the donkey's width moves about 3.6 when it steps aside: more a jump than a step.
11. A switch indoors: the babies of the mount left stop following at the next check but stay on screen until he
    leaves the room (as "Babies come indoors" switched off inside, docs/foals-indoors.md test 6).
12. A choice waiting for a landing is made behind an open options window.

**Rare or mod-only:**

13. `left` is case-insensitive but instance keys are compared ordinal: two spellings of one mount id (a cfg skin id
    cased differently while its pack is away) would overwrite one family entry.
14. A left key held by a dormant or failed record: "Send home" skips that one, leaving that mount later changes it in
    place, and `UpdateFoals` retries its baby's spawn once a second with a log line each time.
15. `TakeStrayBabies` with all five baby slots full leaves a marked stray as an extra baby (needs a refused key first).
16. A mod's companion made mid-game with no Skin set: its first journal pick changes in place even with the option on.
17. `TakeDeferred` clears the waiting call before `TakeSkin`; an exception there loses the call (the skin is retried).
    The switch itself isn't atomic (nothing in it throws today).
18. `ApplyAll` re-applies every left family at each fresh spawn: a one-frame burst at the first call of a new mount
    (not measured).

**Code:**

19. In `SwitchFamilies` and `LetGo` a local `keep` hides the `keep` field (KeepWithSaves); `leave` would be safer.
20. Saddle Up's `catch (MissingMethodException)` around the row logs a warning on a mixed install, and once Saddle
    Up's NeedsApi rises it would hide a real missing member: drop the try at the version bump.
21. The cart's `SetKey(OwnerId, CartKey)` result is ignored at the unhitch (as before the feature).
