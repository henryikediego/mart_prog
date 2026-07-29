#!/usr/bin/env bash
# =============================================================
# MART_PROG — folder scaffold
# Run this from the ROOT of your Unity project (the folder that
# contains Assets/ and ProjectSettings/) AFTER creating the
# project in Unity Hub.
#
#   cd /path/to/MART_PROG
#   bash scaffold_structure.sh
#
# It only creates empty folders (with a .gitkeep so Git tracks
# them). Unity generates the .meta files the next time it has
# focus. Nothing here is destructive.
# =============================================================
set -euo pipefail

if [ ! -d "Assets" ]; then
  echo "ERROR: no Assets/ folder here. Run this from the Unity project root."
  exit 1
fi

DIRS=(
  "Assets/_Project/Scenes"
  "Assets/_Project/Scripts/Core"
  "Assets/_Project/Scripts/Dashboard"
  "Assets/_Project/Scripts/AR"
  "Assets/_Project/Scripts/Modules"
  "Assets/_Project/Scripts/Quiz"
  "Assets/_Project/Scripts/Menu"
  "Assets/_Project/Prefabs/Dashboard"
  "Assets/_Project/Prefabs/Modules"
  "Assets/_Project/Prefabs/Quiz"
  "Assets/_Project/Data/Quizzes"
  "Assets/_Project/Data/Modules"
  "Assets/_Project/UI/Fonts"
  "Assets/_Project/UI/Sprites"
  "Assets/_Project/Materials"
  "Assets/_Project/Models"
)

for d in "${DIRS[@]}"; do
  mkdir -p "$d"
  touch "$d/.gitkeep"
  echo "  created $d"
done

echo ""
echo "Done. Open Unity so it generates .meta files, then commit."
