#!/bin/bash
# The release build (docs/release-checklist.md, section 3): GK2DonkeyAPI.dll, GK2SaddleUp.dll and every external pack's
# DLL (packs/<Pack>/<Pack>.csproj, named by its AssemblyName: GK2SaddleUpDragonsMountPack.dll, GK2SaddleUpCrittersMountPack.dll) built with
# -p:ForDist=true (no debug directory, no source-folder marker) and -p:Deploy=false (the game may be running), copied
# into dist/, then tools/check-dist.py. Stops at the first failure. Build output: release/logs/build-dist.log.
#
#   bash tools/build-dist.sh
#
# Afterwards: bash tools/release-scans.sh, python tools/make-release-zip.py, then a dev build with the game closed
# (bin/ holds whichever was built last).
set -u
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="$REPO/release/logs/build-dist.log"
mkdir -p "$REPO/release/logs"
: > "$LOG"

build() { # project folder under the repo (src/<project>, packs/<Pack>), assembly name
  local proj="$1" asm="$2"
  echo "== $asm"
  echo "===== $proj ($asm)" >> "$LOG"
  # --no-incremental: after a dev build, a ForDist build of unchanged sources would reuse the dev output (seen 2026-10-13)
  if ! (cd "$REPO/$proj" && dotnet build -c Release --no-incremental -p:ForDist=true -p:Deploy=false >> "$LOG" 2>&1); then
    echo "BUILD FAILED: $asm (see $LOG)"
    exit 1
  fi
  local warn
  warn=$(grep -E "^ +[0-9]+ Warning\(s\)" "$LOG" | tail -1 | tr -dc '0-9')
  if [ "${warn:-0}" != "0" ]; then
    echo "WARNINGS in $asm: $warn (see $LOG); no warning ships"
    exit 1
  fi
  cp "$REPO/$proj/bin/Release/netstandard2.1/$asm.dll" "$REPO/dist/$asm.dll" || exit 1
  echo "   built, 0 warnings, copied to dist/"
}

build src/GK2DonkeyAPI GK2DonkeyAPI
build src/SaddleUp GK2SaddleUp
for pack in "$REPO"/packs/*/; do
  name="$(basename "$pack")"
  [ -f "$pack/$name.csproj" ] || continue
  asm="$(grep -o '<AssemblyName>[^<]*' "$pack/$name.csproj" | head -1 | cut -d'>' -f2)"
  build "packs/$name" "${asm:-$name}"
done
# The release rule (tools/release-versions.py items): a DLL whose version was not raised above the one committed in
# dist/ is a released build: its committed bytes go back into dist/ (a public version is never rebuilt; 2026-10-30:
# the Donkey Skin Pack's source held the unreleased Armored under its public 1.0.0). A raised or new one stays built.
echo "== the release rule"
ITEMS="$(python -I "$REPO/tools/release-versions.py" items)" || { echo "release-versions.py items FAILED"; exit 1; }
while read -r state dll ver rest; do
  case "$state" in
    kept)
      git -C "$REPO" checkout -- "dist/$dll.dll" || { echo "could not restore dist/$dll.dll"; exit 1; }
      echo "   $dll $ver: kept, the released build (its version was not raised)" ;;
    raised|new) echo "   $dll $ver: $state, this release's build" ;;
    *) echo "release-versions.py items said: $state $dll $ver $rest"; exit 1 ;;
  esac
done <<< "$ITEMS"
echo "== check-dist"
python "$REPO/tools/check-dist.py" || { echo "check-dist FAILED"; exit 1; }
echo "DIST BUILT"
