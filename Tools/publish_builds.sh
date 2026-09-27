#!/bin/bash
# Publishes the current builds to the repository's GitHub Releases page (D-030), so they can be downloaded
# from GitHub without putting build output into Git history.
# Usage (from the repo root, after Tabletop > Build > All): bash Tools/publish_builds.sh ["short note"]
set -e
note="${1:-Test build}"
win="Builds/TabletopReels-Windows.zip"
ios="Builds/TabletopReels-iOS-Xcode.zip"
for f in "$win" "$ios"; do
  [ -f "$f" ] || { echo "Missing $f - build first (Tabletop > Build > All)."; exit 1; }
done
if [ -n "$(git status --porcelain -- Assets ProjectSettings Packages)" ]; then
  echo "Commit the project first: a published build should match a commit on GitHub."; exit 1
fi
sha="$(git rev-parse --short HEAD)"
tag="build-$(date +%Y%m%d-%H%M)-$sha"
notes="$note

Built from commit $sha.

**Windows:** download \`TabletopReels-Windows.zip\`, unzip, run \`Windows/TabletopReels.exe\`.

**iPhone / iPad:** download \`TabletopReels-iOS-Xcode.zip\` on a Mac, unzip, open \`TabletopReels/Unity-iPhone.xcodeproj\` in Xcode, pick your team under Signing & Capabilities, connect the device and press Run. The first build takes several minutes; let it finish ("Build stopped" means it was cancelled). On-screen stick and buttons for walking and talking; tap menus, reels, pieces and the lever."
gh release create "$tag" "$win" "$ios" --title "Test build $(date +%Y-%m-%d) ($sha)" --notes "$notes" --prerelease --target "$(git rev-parse HEAD)"
echo "Published $tag"
