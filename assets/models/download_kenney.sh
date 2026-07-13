#!/usr/bin/env bash
set -euo pipefail

PROJECT="/data/source/game"
RAW_DIR="$PROJECT/assets/models/raw"
ORGANIZED_DIR="$PROJECT/assets/models/kenney"
LOG_FILE="$PROJECT/assets/models/download.log"

mkdir -p "$RAW_DIR" "$ORGANIZED_DIR"
echo "$(date '+%Y-%m-%d %H:%M:%S') [START] Kenney asset download" > "$LOG_FILE"

# Kenney CC0 asset packs with direct ZIP links
declare -A PACKS=(
  ["tanks"]="https://kenney.nl/media/pages/assets/tanks/d0bbede612-1677579063/kenney_tanks.zip"
  ["voxel-pack"]="https://kenney.nl/media/pages/assets/voxel-pack/a3a73d0ff7-1677662501/kenney_voxel-pack.zip"
  ["blaster-kit"]="https://kenney.nl/media/pages/assets/blaster-kit/261d80a716-1753959510/kenney_blaster-kit_2.1.zip"
  ["car-kit"]="https://kenney.nl/media/pages/assets/car-kit/1a312ec241-1775131960/kenney_car-kit.zip"
  ["building-kit"]="https://kenney.nl/media/pages/assets/building-kit/0de7aaa492-1743244741/kenney_building-kit.zip"
  ["nature-kit"]="https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip"
  ["space-kit"]="https://kenney.nl/media/pages/assets/space-kit/20874c75ac-1677698978/kenney_space-kit.zip"
)

DOWNLOAD_COUNT=0
FAIL_COUNT=0

for name in "${!PACKS[@]}"; do
  url="${PACKS[$name]}"
  zip_file="$RAW_DIR/kenney_${name}.zip"
  extract_dir="$ORGANIZED_DIR/${name}"

  # Skip if already extracted
  if [ -d "$extract_dir" ] && [ -n "$(ls -A "$extract_dir" 2>/dev/null)" ]; then
    echo "$(date '+%H:%M:%S') [SKIP] $name already extracted" >> "$LOG_FILE"
    continue
  fi

  # Download
  echo "$(date '+%H:%M:%S') [DOWNLOAD] $name from $url" >> "$LOG_FILE"
  if curl -sL --max-time 120 -o "$zip_file" "$url" 2>>"$LOG_FILE"; then
    size=$(stat -c%s "$zip_file" 2>/dev/null || echo "0")
    if [ "$size" -gt 10000 ]; then
      echo "$(date '+%H:%M:%S') [OK] $name downloaded ($(( size / 1024 ))KB)" >> "$LOG_FILE"
      
      # Extract
      mkdir -p "$extract_dir"
      unzip -q -o "$zip_file" -d "$extract_dir" 2>>"$LOG_FILE"
      echo "$(date '+%H:%M:%S') [EXTRACT] $name extracted to $extract_dir" >> "$LOG_FILE"
      
      # List contents
      find "$extract_dir" -type f \( -name "*.gltf" -o -name "*.glb" -o -name "*.obj" -o -name "*.fbx" -o -name "*.png" \) | head -10 >> "$LOG_FILE" 2>&1
      
      DOWNLOAD_COUNT=$((DOWNLOAD_COUNT + 1))
    else
      echo "$(date '+%H:%M:%S') [FAIL] $name too small ($size bytes)" >> "$LOG_FILE"
      FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
  else
    echo "$(date '+%H:%M:%S') [FAIL] $name download error" >> "$LOG_FILE"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
  
  sleep 1
done

# Summary
echo "" >> "$LOG_FILE"
echo "$(date '+%H:%M:%S') [SUMMARY] Downloaded: $DOWNLOAD_COUNT, Failed: $FAIL_COUNT" >> "$LOG_FILE"

# List all 3D model files found
echo "" >> "$LOG_FILE"
echo "=== 3D Model Files ===" >> "$LOG_FILE"
find "$ORGANIZED_DIR" -type f \( -name "*.gltf" -o -name "*.glb" -o -name "*.obj" -o -name "*.fbx" \) | sort >> "$LOG_FILE" 2>&1

echo "" >> "$LOG_FILE"
echo "=== Texture Files ===" >> "$LOG_FILE"
find "$ORGANIZED_DIR" -type f \( -name "*.png" -o -name "*.jpg" \) | wc -l >> "$LOG_FILE" 2>&1
echo "$(date '+%Y-%m-%d %H:%M:%S') [DONE]" >> "$LOG_FILE"
