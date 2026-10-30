#!/bin/bash
# The whole release, one command (docs/release-checklist.md, sections 2, 3 and 6), after the versions are raised
# (`python tools/release-versions.py set ...`) and the editorial pieces are written (the CHANGELOG headings, docs/api.md's
# history entry, docs/nexus/<name>-<version>-changelog.md and, for the Workshop, -workshop-changenote.txt):
#
#   bash tools/release.sh
#
# 1. release-versions.py check   every line that repeats a version agrees; the editorial pieces exist
# 2. release-versions.py items   which DLLs this release raised, which stay released builds
# 3. build-dist.sh               the release build into dist/, the kept DLLs back to their committed bytes, check-dist
# 4. release-scans.sh            the offline scans
# 5. make-release-zip.py         the Nexus zips and workshop/<Item>/ folders of the raised items only; an existing zip
#                                in release/ is never overwritten
# Stops at the first failure. The game may be running (nothing is deployed). Afterwards: the F11 run and the uploads
# listed at the end; the dev builds in the game are untouched (build-dist builds with Deploy=false).
set -u
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
step() { echo; echo "######## $1"; }

step "1. the version lines"
python -I "$REPO/tools/release-versions.py" check || { echo "RELEASE STOPPED: fix the lines above"; exit 1; }
step "2. what this release carries"
python -I "$REPO/tools/release-versions.py" items || exit 1
step "3. the release build"
bash "$REPO/tools/build-dist.sh" || { echo "RELEASE STOPPED at the build"; exit 1; }
step "4. the release scans"
bash "$REPO/tools/release-scans.sh" || { echo "RELEASE STOPPED at the scans"; exit 1; }
step "5. the zips and the Workshop folders"
python -I "$REPO/tools/make-release-zip.py" || { echo "RELEASE STOPPED at the zips"; exit 1; }

step "Done. To hand off"
echo "- Nexus: each zip above to its page, with its docs/nexus/<name>-<version>-changelog.md as the file's note"
echo "- Steam: the game, Shift+F11, Upload on each item's tile (its folder is workshop/<Item>/)"
echo "- dist/README.md: the sizes paragraph; then commit dist/ with the release and tag it"
