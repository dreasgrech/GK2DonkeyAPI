#!/bin/bash
# Every offline check of this repo before a release (docs/release-checklist.md, section 2), no game launch needed.
# Run after tools/build-dist.sh: the manifest check reads the release DLL in bin/. Each check's output goes to
# release/logs/release-scans.log; the summary lists each with its exit code. Exits 1 if any check failed.
#
#   bash tools/release-scans.sh
set -u
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="$REPO/release/logs/release-scans.log"
mkdir -p "$REPO/release/logs"
: > "$LOG"
GAME="$(sed -n 's:.*<GameDir[^>]*>\([^<]*\)</GameDir>.*:\1:p' "$REPO/Directory.Build.props" | head -1)"
MANAGED="$GAME/GraveyardKeeper2_Data/Managed"
fails=0
total=0

run() { # folder, label, command...
  local d="$1" label="$2"
  shift 2
  echo "===== $label" >> "$LOG"
  (cd "$d" && "$@" >> "$LOG" 2>&1)
  local rc=$?
  echo "----- exit $rc ($label)" >> "$LOG"
  total=$((total + 1))
  if [ $rc -eq 0 ]; then echo "ok    $label"; else echo "FAIL  $label (exit $rc)"; fails=$((fails + 1)); fi
}

T="$REPO/tests"
run "$REPO/tools/DepManifest" "DepManifest build" dotnet build -c Release
run "$REPO" "DepManifest verify GK2DonkeyAPI.dll" dotnet tools/DepManifest/bin/Release/net8.0/DepManifest.dll verify "src/GK2DonkeyAPI/bin/Release/netstandard2.1/GK2DonkeyAPI.dll" "$MANAGED"
run "$REPO" "check-dist" python tools/check-dist.py
run "$REPO" "layering (the API names no mod, Saddle Up no pack; the dist DLL too)" python -I tools/layering-scan.py --dist
for t in CecilScan-DonkeyAPI ApiSurface DefaultsScan SkinsScan FrameworkScan NoLinqScan; do
  run "$T/$t" "$t" dotnet run -c Release
done
run "$REPO" "canonical skin.cfg layout" python -I tools/skins/canonical_cfg.py --check
for d in "$REPO"/src/*/assets/mounts/*/skins/*/ "$REPO"/packs/*/gk2mounts/*/skins/*/; do  # built in, then the external packs (tools/skins/skinroots.py)
  run "$REPO" "check_skin $(basename "$(dirname "$(dirname "$d")")")/$(basename "$d")" python -I tools/skins/check_skin.py "$d"  # <mount id>/<skin>: every mount's own look is "default", a donkey skin "<mod>.<name>" (donkey/saddleup-donkey-skins.hellfire)
done
run "$T/PackerScan" "PackerScan" dotnet run -c Release
run "$T/PackerScan" "PackerScan --shapes" dotnet run -c Release -- --shapes
run "$T/PackerScan" "PackerScan --real --thorough" dotnet run -c Release -- --real --thorough
run "$T/PackerScan" "PackerScan --review" dotnet run -c Release -- --review
run "$T/PackerScan" "PackerScan --review-check" dotnet run -c Release -- --review-check
# the review marks to look at again in game (informational: an ok whose tuning changed since)
grep -E "to re-check:|ok marks:" "$LOG" | sed 's/^/      /'
echo "$((total - fails)) of $total checks green (log: $LOG)"
[ $fails -eq 0 ] || exit 1
