#!/bin/bash
# Plays the scenes the user named (12 November 2026) in the real window, one WindowCheck per scene, three at a time.
# usage: play.sh <game folder> <output root> <prefix commands> <scene>...   (scene: room,object,verb in decimal, or "intro")
# Each scene is watched 45 seconds; pictures at 4.5, 9, 18 and 27 seconds (the intro: every 3 seconds for 60).
cd "/d/Documents/Personal/Code Projects/SCUMMexplorer"
GAME="$1"; ROOT="$2"; PREFIX="$3"; shift 3
EXE="Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe"
mkdir -p "$ROOT"
run() {
  sc="$1"; out="$ROOT/${sc//,/-}"; mkdir -p "$out"
  if [ "$sc" = "intro" ]; then
    timeout 900 "$EXE" "$GAME" "$out" g:60 > "$out.log" 2>&1
  else
    # shellcheck disable=SC2086
    WINDOWCHECK_SCENE=45 timeout 900 "$EXE" "$GAME" "$out" $PREFIX "$sc" > "$out.log" 2>&1
  fi
}
n=0
for sc in "$@"; do run "$sc" & n=$((n+1)); if [ $((n % 3)) -eq 0 ]; then wait; fi; done
wait
echo done
