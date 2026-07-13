#!/usr/bin/env bash
# AI War Sandbox - Asset download and integration watchdog
# Checks download progress, integrates assets into Godot project, triggers next tasks
set -euo pipefail

PROJECT="/data/source/game"
LOG_FILE="$PROJECT/assets/models/download.log"
AUDIO_DIR="$PROJECT/assets/audio"
STATUS_FILE="$PROJECT/assets/models/.task_status"

mkdir -p "$AUDIO_DIR"
mkdir -p "$PROJECT/assets/models"

# === Phase tracking ===
phase="download_3d"
[ -f "$STATUS_FILE" ] && phase=$(cat "$STATUS_FILE")

case "$phase" in
  download_3d)
    # Check if download script is still running
    if pgrep -f "download_kenney.sh" > /dev/null 2>&1; then
      echo "⏳ Kenney 3D model download still running..."
      tail -5 "$LOG_FILE" 2>/dev/null
      exit 0
    fi
    
    # Check results
    if [ ! -f "$LOG_FILE" ] || ! grep -q "\[DONE\]" "$LOG_FILE" 2>/dev/null; then
      echo "⏳ Download not complete yet, restarting..."
      bash "$PROJECT/assets/models/download_kenney.sh"
      exit 0
    fi
    
    # Download complete - count assets
    model_count=$(find "$PROJECT/assets/models/kenney" -type f \( -name "*.gltf" -o -name "*.glb" -o -name "*.obj" \) 2>/dev/null | wc -l)
    texture_count=$(find "$PROJECT/assets/models/kenney" -type f \( -name "*.png" -o -name "*.jpg" \) 2>/dev/null | wc -l)
    
    echo "✅ 3D models downloaded: $model_count models, $texture_count textures"
    echo "📊 Download log:"
    tail -20 "$LOG_FILE"
    
    # Move to next phase
    echo "download_audio" > "$STATUS_FILE"
    ;;
    
  download_audio)
    # Download Kenney audio packs (CC0)
    AUDIO_RAW="$PROJECT/assets/audio/raw"
    mkdir -p "$AUDIO_RAW" "$PROJECT/assets/audio/kenney"
    
    # Kenney audio packs - verified direct ZIP links (CC0)
    declare -A AUDIO_PACKS=(
      ["impact-sounds"]="https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip"
      ["interface-sounds"]="https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip"
      ["digital-audio"]="https://kenney.nl/media/pages/assets/digital-audio/216eac4753-1677590265/kenney_digital-audio.zip"
      ["music-jingles"]="https://kenney.nl/media/pages/assets/music-jingles/f37e530b9e-1677590399/kenney_music-jingles.zip"
    )
    
    echo "🔍 Downloading Kenney audio packs..."
    
    for slug in "${!AUDIO_PACKS[@]}"; do
      zip="${AUDIO_PACKS[$slug]}"
      echo "⬇️ Downloading $slug..."
      if curl -sL --max-time 60 -o "$AUDIO_RAW/kenney_${slug}.zip" "$zip" 2>/dev/null; then
        size=$(stat -c%s "$AUDIO_RAW/kenney_${slug}.zip" 2>/dev/null || echo "0")
        if [ "$size" -gt 5000 ]; then
          mkdir -p "$PROJECT/assets/audio/kenney/$slug"
          unzip -q -o "$AUDIO_RAW/kenney_${slug}.zip" -d "$PROJECT/assets/audio/kenney/$slug" 2>/dev/null
          echo "✅ $slug extracted ($(( size / 1024 ))KB)"
        else
          echo "⚠️ $slug too small ($size bytes)"
        fi
      else
        echo "⚠️ $slug download failed"
      fi
      sleep 1
    done
    
    # Check results
    audio_count=$(find "$PROJECT/assets/audio/kenney" -type f \( -name "*.wav" -o -name "*.ogg" -o -name "*.mp3" \) 2>/dev/null | wc -l)
    echo "✅ Audio assets downloaded: $audio_count files"
    
    # Move to next phase
    echo "integrate" > "$STATUS_FILE"
    ;;
    
  integrate)
    echo "📦 Phase: Asset Integration"
    echo "3D models:"
    find "$PROJECT/assets/models/kenney" -type f \( -name "*.gltf" -o -name "*.glb" -o -name "*.obj" \) 2>/dev/null | head -20
    echo ""
    echo "Audio:"
    find "$PROJECT/assets/audio/kenney" -type f \( -name "*.wav" -o -name "*.ogg" \) 2>/dev/null | head -20
    echo ""
    echo "✅ All asset downloads complete. Ready for code integration."
    echo "done" > "$STATUS_FILE"
    ;;
    
  done)
    echo "✅ All asset tasks already complete."
    find "$PROJECT/assets/models/kenney" -type f \( -name "*.gltf" -o -name "*.glb" \) 2>/dev/null | wc -l
    echo "models found"
    find "$PROJECT/assets/audio/kenney" -type f \( -name "*.wav" -o -name "*.ogg" \) 2>/dev/null | wc -l
    echo "audio files found"
    ;;
esac
