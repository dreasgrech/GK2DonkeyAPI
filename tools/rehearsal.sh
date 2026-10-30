#!/bin/bash
# The release rehearsal of docs/release-checklist.md section 5, as a player's game folder:
#   bash tools/rehearsal.sh stage-a            dev plugins, cfgs and the dev marker aside; the release zips extracted (a fresh install)
#   bash tools/rehearsal.sh capture-a          A's fresh cfgs and log copied aside (the fixture is made from them)
#   bash tools/rehearsal.sh stage-b <su.cfg>   B: the given Saddle Up cfg of the previous release in config (the upgrade),
#                                              with the API cfg rehearsal A wrote; both checked first (below)
#   bash tools/rehearsal.sh restore            everything back, the saves compared before and after
#   bash tools/rehearsal.sh stage-pack [Item]  the Workshop content (workshop/<Item>/, default GK2SaddleUp) as a local pack in
#                                              Mods\Code, the item's deployed plugin folders aside (section 11 of the report)
#   bash tools/rehearsal.sh restore-pack [Item] twice: the pack folder removed, then (after one game launch) the dev builds back
#   bash tools/rehearsal.sh stage-aside [Item] the item's deployed plugin folders aside only, for a Workshop subscription test
#                                              (then subscribe, launch; afterwards unsubscribe, launch once, restore-pack)
# Every mode refuses while the game runs. The versions and the aside folder's name are at the top.
#
# The cfg checks (stage-b; the user, 2026-10-29: "make sure this never happens again"): a cfg that goes into the game
# folder live must have the API's [General] Enabled = true and MountWgoId = npc_donkey (a fixture has every value off
# its default, the kill switch among them: the API did nothing in game and no key worked, 2026-10-29), and Saddle Up's
# MountsKey, RideKey and CartKey must be keys a keyboard has (a fixture's are on the numpad). A file that fails is not
# staged.
set -e
SADDLEUP_ZIP="GK2-Saddle-Up-0.13.0.zip"
PACK_ZIPS="GK2-Saddle-Up-Dragons-Mount-Pack-1.0.0.zip GK2-Saddle-Up-Critters-Mount-Pack-1.0.0.zip GK2-Saddle-Up-Donkey-Skin-Pack-1.0.0.zip"
ASIDE_NAME="rehearsal-aside-$(date +%Y-%m-%d)"

REPO="$(cd "$(dirname "$0")/.." && pwd)"
# the game folder as the builds have it (Directory.Build.props GameDir), the saves under the user's profile
G="$(cygpath -u "$(sed -n 's/.*<GameDir[^>]*>\([^<]*\)<\/GameDir>.*/\1/p' "$REPO/Directory.Build.props" | head -1)")"
[ -d "$G" ] || { echo "no game folder at $G (Directory.Build.props GameDir)"; exit 1; }
B="$G/BepInEx"
ASIDE="$B/$ASIDE_NAME"
SAVES="$(cygpath -u "$USERPROFILE")/AppData/LocalLow/Lazy Bear Games/Graveyard Keeper 2"
DEVPLUG="GK2DonkeyAPI GK2SaddleUp GK2SaddleUpCrittersMountPack GK2SaddleUpDragonsMountPack GK2SaddleUpDonkeySkinPack GK2DonkeyAPI.IntegrationTests GK2ShowcaseDirector GK2PerfRunner GK2AssetProbe PerformanceHog"
CFGS="dreas.gk2.donkeyapi.cfg dreas.gk2.saddleup.cfg"

running() { [ "$(tasklist | grep -ic graveyard)" != 0 ]; }
if running; then echo "the game is running: close it first"; exit 1; fi

# the value of a key inside one section of a cfg (the section's plain name, any number before it)
cfg_value() { # file section key
  awk -v sec="$2" -v key="$3" '
    /^\[/ { s = $0; sub(/^\[[0-9]+\. /, "[", s); in_sec = (s == "[" sec "]"); next }
    in_sec && index($0, key " = ") == 1 { sub(key " = ", ""); print; exit }' "$1"
}

check_api_cfg() { # a dreas.gk2.donkeyapi.cfg that is about to go live
  local v
  v="$(cfg_value "$1" General Enabled)"
  if [ "$v" != "true" ]; then echo "REFUSED: $1 has [General] Enabled = ${v:-missing}: the API would be off in game (a fixture's file?)"; return 1; fi
  v="$(cfg_value "$1" General MountWgoId)"
  if [ "$v" != "npc_donkey" ]; then echo "REFUSED: $1 has [General] MountWgoId = ${v:-missing} (a fixture's file?)"; return 1; fi
  echo "ok   API cfg: Enabled = true, MountWgoId = npc_donkey ($1)"
}

check_saddleup_cfg() { # a dreas.gk2.saddleup.cfg that is about to go live
  local k v
  for k in MountsKey RideKey CartKey; do
    v="$(cfg_value "$1" Keys "$k")"
    case "$v" in
      Keypad*|"") echo "REFUSED: $1 has [Keys] $k = ${v:-missing}: not a key a keyboard has (a fixture's numpad keys? set L/H/G in a copy first)"; return 1 ;;
    esac
  done
  echo "ok   Saddle Up cfg: MountsKey = $(cfg_value "$1" Keys MountsKey), RideKey = $(cfg_value "$1" Keys RideKey), CartKey = $(cfg_value "$1" Keys CartKey) ($1)"
}

extract() {
  local z
  for z in "$SADDLEUP_ZIP" $PACK_ZIPS; do
    unzip -o -q "$REPO/release/$z" -d "$G" -x modinfo.json  # the zip's modinfo.json is for a Mods\Code install; not next to the exe
    echo "extracted $z"
  done
}

case "$1" in
  stage-a)
    [ -e "$ASIDE" ] && { echo "$ASIDE exists: restore first"; exit 1; }
    mkdir -p "$ASIDE/plugins" "$ASIDE/config" "$ASIDE/saves"
    for p in $DEVPLUG; do [ -e "$B/plugins/$p" ] && mv "$B/plugins/$p" "$ASIDE/plugins/" && echo "aside plugins/$p"; done
    for c in $CFGS dreas.gk2.dev; do [ -e "$B/config/$c" ] && mv "$B/config/$c" "$ASIDE/config/" && echo "aside config/$c"; done
    cp -p "$SAVES"/*.gk2donkeyapi.dat* "$ASIDE/saves/" 2>/dev/null || true
    (cd "$SAVES" && sha1sum Steam_* > "$ASIDE/saves/sha1-before.txt")
    extract
    echo "STAGED A: release zips, no cfgs, no dev marker, no dev plugins"
    ;;
  capture-a)
    mkdir -p "$ASIDE/after-a"
    for c in $CFGS; do cp -p "$B/config/$c" "$ASIDE/after-a/"; done
    cp -p "$B/LogOutput.log" "$ASIDE/after-a/"
    check_api_cfg "$ASIDE/after-a/dreas.gk2.donkeyapi.cfg" || echo "   (A's own API cfg failed the check: look at it before B)"
    echo "captured A's cfgs and log into $ASIDE/after-a"
    ;;
  stage-b)
    SU="$2"
    [ -n "$SU" ] && [ -f "$SU" ] || { echo "stage-b needs the previous release's Saddle Up cfg: bash tools/rehearsal.sh stage-b <dreas.gk2.saddleup.cfg>"; exit 1; }
    [ -f "$ASIDE/after-a/dreas.gk2.donkeyapi.cfg" ] || { echo "no $ASIDE/after-a/dreas.gk2.donkeyapi.cfg: run stage-a, play, capture-a first"; exit 1; }
    check_api_cfg "$ASIDE/after-a/dreas.gk2.donkeyapi.cfg"
    check_saddleup_cfg "$SU"
    cp "$ASIDE/after-a/dreas.gk2.donkeyapi.cfg" "$B/config/dreas.gk2.donkeyapi.cfg"
    cp "$SU" "$B/config/dreas.gk2.saddleup.cfg"
    check_api_cfg "$B/config/dreas.gk2.donkeyapi.cfg" > /dev/null
    check_saddleup_cfg "$B/config/dreas.gk2.saddleup.cfg" > /dev/null
    echo "STAGED B: $(basename "$SU") as Saddle Up's cfg, A's API cfg (same release DLLs)"
    ;;
  restore)
    [ -d "$ASIDE" ] || { echo "nothing aside"; exit 1; }
    mkdir -p "$ASIDE/after-b"
    for c in $CFGS; do [ -e "$B/config/$c" ] && cp -p "$B/config/$c" "$ASIDE/after-b/"; done
    cp -p "$B/LogOutput.log" "$ASIDE/after-b/" || true
    for p in GK2DonkeyAPI GK2SaddleUp GK2SaddleUpCrittersMountPack GK2SaddleUpDragonsMountPack GK2SaddleUpDonkeySkinPack; do rm -rf "$B/plugins/$p"; done
    rm -f "$G/modinfo.json"  # a stray one from a hand extract of a zip into the game folder
    for p in $DEVPLUG; do [ -e "$ASIDE/plugins/$p" ] && mv "$ASIDE/plugins/$p" "$B/plugins/" && echo "back plugins/$p"; done
    for c in $CFGS dreas.gk2.dev; do [ -e "$ASIDE/config/$c" ] && mv -f "$ASIDE/config/$c" "$B/config/$c" && echo "back config/$c"; done
    (cd "$SAVES" && sha1sum Steam_* > "$ASIDE/saves/sha1-after.txt")
    if diff -q "$ASIDE/saves/sha1-before.txt" "$ASIDE/saves/sha1-after.txt" >/dev/null; then echo "saves identical"; else echo "SAVES CHANGED:"; diff "$ASIDE/saves/sha1-before.txt" "$ASIDE/saves/sha1-after.txt" || true; fi
    echo "RESTORED (copies kept in $ASIDE)"
    ;;
  stage-pack|restore-pack|stage-aside)
    # The Workshop content rehearsed without Steam (docs/mods-window-and-workshop.md section 11): workshop/<Item>/ (from
    # tools/make-release-zip.py) copied into Mods\Code\<Item>\ in the save data folder, the game's own copy of the mod;
    # the item's deployed plugin folders go aside so the game's copy is the only one. restore-pack is two runs: the
    # first removes the pack folder and asks for one game launch (the game's patcher deletes the files it copied and
    # forgets them; the dev builds put back BEFORE that launch would be deleted by it), the second puts the dev builds back.
    ITEM="${2:-GK2SaddleUp}"
    W="$REPO/workshop/$ITEM"
    PACK="$SAVES/Mods/Code/$ITEM"
    PASIDE="$B/pack-aside-$ITEM"
    [ -d "$W" ] || { echo "no $W: python tools/make-release-zip.py --only ... first"; exit 1; }
    ID="local:$(sed -n 's/.*"id": *"\([^"]*\)".*/\1/p' "$W/modinfo.json")"
    FOLDERS="$(ls "$W/BepInEx/plugins")"
    STATE="$B/GK2.WorkshopSync/state.json"
    if [ "$1" = stage-pack ] || [ "$1" = stage-aside ]; then
      [ -e "$PASIDE" ] && { echo "$PASIDE exists: restore-pack first"; exit 1; }
      [ -e "$PACK" ] && { echo "$PACK exists already"; exit 1; }
      mkdir -p "$PASIDE/plugins"
      for p in $FOLDERS; do [ -e "$B/plugins/$p" ] && mv "$B/plugins/$p" "$PASIDE/plugins/" && echo "aside plugins/$p"; done
      if [ "$1" = stage-aside ]; then echo "ASIDE: $FOLDERS out of plugins (in $PASIDE); subscribe on Steam and launch"; exit 0; fi
      mkdir -p "$SAVES/Mods/Code"
      cp -r "$W" "$PACK"
      echo "STAGED PACK: $PACK (id $ID; the uploader's files ride along and are ignored)"
      echo "launch: the Mods window lists it, LogOutput.log has [GK2.WorkshopSync] Copied lines for: $FOLDERS"
    else
      if [ -d "$PACK" ]; then
        rm -rf "$PACK"
        echo "removed $PACK. Now LAUNCH THE GAME ONCE and close it (its patcher deletes the copies in plugins\\ and forgets them), then run restore-pack again"
        exit 0
      fi
      for p in $FOLDERS; do
        if [ -f "$STATE" ] && grep -q "plugins/$p/" "$STATE"; then
          echo "$STATE still records plugins/$p/ as an item's copy (a local pack or a Workshop subscription): remove the pack or unsubscribe, launch the game once, close it, then run restore-pack again"; exit 1
        fi
      done
      for p in $FOLDERS; do
        # the patcher deletes the files it copied and leaves their empty folders behind: those go; a folder with files refuses
        if [ -d "$B/plugins/$p" ] && [ -z "$(ls -A "$B/plugins/$p")" ]; then rmdir "$B/plugins/$p" && echo "removed the empty leftover plugins/$p"; fi
        [ -e "$B/plugins/$p" ] && { echo "$B/plugins/$p is still there with files (the game's copy?): not restoring over it"; exit 1; }
      done
      for p in $FOLDERS; do [ -e "$PASIDE/plugins/$p" ] && mv "$PASIDE/plugins/$p" "$B/plugins/" && echo "back plugins/$p"; done
      rmdir "$PASIDE/plugins" "$PASIDE" 2>/dev/null || true
      echo "RESTORED the dev builds ($ID stays in Mods\\code_mods.json as a harmless decision)"
    fi
    ;;
  *) echo "usage: stage-a | capture-a | stage-b <saddleup.cfg> | restore | stage-pack [Item] | restore-pack [Item]"; exit 1 ;;
esac
