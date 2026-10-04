#!/bin/sh
# Plays the sample of catalog starts (sample.json) through the real MainWindow (SceneShots), six scenes per process, on the English folder.
# usage: run-sample.sh <output folder name under english-catalog-20261004>
cd "$(dirname "$0")/../.."
base="$PWD/.verification/english-catalog-20261004/$1"
mkdir -p "$base"
python -c "import json; print('\n'.join(json.load(open('.verification/english-catalog-20261004/sample.json'))))" | tr -d '\r' > "$base/sample.txt"
n=0; batch=0; set --
while IFS= read -r line; do
  set -- "$@" "$line"; n=$((n+1))
  if [ $n -eq 6 ]; then
    out="$base/b$batch"; mkdir -p "$out"
    timeout 400 Exe/SceneShots/Debug/net10.0-windows/SceneShots.exe "$PWD/Games/loom ega en" "$out" 30 4000 "$@"
    batch=$((batch+1)); n=0; set --
  fi
done < "$base/sample.txt"
if [ $n -gt 0 ]; then out="$base/b$batch"; mkdir -p "$out"; timeout 400 Exe/SceneShots/Debug/net10.0-windows/SceneShots.exe "$PWD/Games/loom ega en" "$out" 30 4000 "$@"; fi
echo done > "$base/finished"
