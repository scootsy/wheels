#!/bin/bash
# Runs the Windows build in self-check mode: it starts a match by itself, saves screenshots
# of what the real player renders, logs render diagnostics, and quits.
# Usage (from the repo root): bash Tools/selfcheck_build.sh [output-folder]
out="${1:-Logs/BuildSelfCheck}"
rm -rf "$out"; mkdir -p "$out"
abs="$(cd "$out" && pwd -W 2>/dev/null || pwd)"
./Builds/Windows/TabletopReels.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 \
  -logFile "$abs/player.log" -tabletopSelfCheck "$abs" &
pid=$!
for i in $(seq 1 90); do
  grep -q "SelfCheck done" "$out/player.log" 2>/dev/null && break
  sleep 1
done
sleep 2
kill $pid 2>/dev/null
taskkill //IM TabletopReels.exe //F >/dev/null 2>&1
grep "\[Tabletop\]" "$out/player.log"
grep -iE "exception|shader.*(not supported|error)" "$out/player.log" | head -5
ls "$out"
