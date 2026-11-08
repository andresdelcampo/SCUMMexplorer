# usage: maniac-all.sh <game folder> <output root>
cd "/d/Documents/Personal/Code Projects/SCUMMexplorer"
GAME="$1"; ROOT="$2"
EXE="Exe/WindowCheck/Release/net10.0-windows/WindowCheck.exe"
export SCUMM_EXPLORER_PROFILE_ROOT="$TEMP/scummx-wc-profile-$(basename "$ROOT")"
mkdir -p "$ROOT"
WINDOWCHECK_SCENE=3 timeout 900 "$EXE" "$GAME" "$ROOT/warm" "r:1" "w:150" "L" > "$ROOT/warm.log" 2>&1
grep "item scumm-" "$ROOT/warm/report.txt" | sed -E 's/.*item scumm-([0-9]+)-([0-9]+)-([0-9]+).*/\1,\2,\3/' > "$ROOT/scenes.txt"
run() {
  sc="$1"; out="$ROOT/${sc//,/-}"; mkdir -p "$out"; cp -r "$ROOT/warm/room-cache" "$out/"
  WINDOWCHECK_SCENE=45 timeout 900 "$EXE" "$GAME" "$out" "r:1" "w:3" "$sc" > /dev/null 2>&1
}
n=0
while read sc; do run "$sc" & n=$((n+1)); if [ $((n % 3)) -eq 0 ]; then wait; fi; done < "$ROOT/scenes.txt"
wait
echo done
