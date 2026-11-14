#!/bin/bash
# Indy 3's scenes that matter, each in its own real window (WindowCheck), three at a time, after one warm-up that fills the plan cache.
# usage: bash run.sh <game folder> <output root> <scene>...   (scene: room,object,verb in decimal, or "intro")
cd "/d/Documents/Personal/Code Projects/SCUMMexplorer"
GAME="$1"; ROOT="$2"; shift 2
EXE="Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe"
export SCUMM_EXPLORER_PROFILE_ROOT="$TEMP/scummx-wc-profile-$(basename "$ROOT")"
mkdir -p "$ROOT"
[ -d "$ROOT/warm/room-cache" ] || WINDOWCHECK_SCENE=3 timeout 900 "$EXE" "$GAME" "$ROOT/warm" "r:1" "w:120" > "$ROOT/warm.log" 2>&1
run() {
  sc="$1"; out="$ROOT/${sc//,/-}"; mkdir -p "$out"; cp -r "$ROOT/warm/room-cache" "$out/" 2>/dev/null
  if [ "$sc" = "intro" ]; then
    timeout 900 "$EXE" "$GAME" "$out" g:60 > "$out.log" 2>&1
  else
    WINDOWCHECK_SCENE=60 timeout 900 "$EXE" "$GAME" "$out" "$sc" > "$out.log" 2>&1
  fi
}
n=0
for sc in "$@"; do run "$sc" & n=$((n+1)); if [ $((n % 3)) -eq 0 ]; then wait; fi; done
wait
echo done
