#!/bin/sh
# usage: run.sh <name> <seconds> <interval ms> <catalog id>   (one process per scene: a running scene blocks the next start)
cd "$(dirname "$0")/../.."
mkdir -p ".verification/setup-placer-20261003/screens/$1"
Exe/SceneShots/Debug/net10.0-windows/SceneShots.exe "$PWD/Games/loom" "$PWD/.verification/setup-placer-20261003/screens/$1" "$2" "$3" "$4"
