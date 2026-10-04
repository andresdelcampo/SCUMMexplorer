#!/bin/sh
# One SceneShots process per scene (a long scene would block the next start in a shared process), English folder.
# usage: run-each.sh <output folder name> <seconds cap> <id>...
cd "$(dirname "$0")/../.."
base="$PWD/.verification/english-catalog-20261004/$1"; cap="$2"; shift 2
mkdir -p "$base"
i=0
for id in "$@"; do
  out="$base/s$i"; mkdir -p "$out"
  timeout $((cap + 90)) Exe/SceneShots/Debug/net10.0-windows/SceneShots.exe "$PWD/Games/loom ega en" "$out" "$cap" 10000 "$id"
  i=$((i+1))
done
echo done > "$base/finished"
