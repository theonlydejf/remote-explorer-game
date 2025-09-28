#!/usr/bin/env bash
set -euo pipefail

function usage() {
  cat <<EOF
Usage: $0 [options] [RID1 RID2 ...]

Options:
  -h, --help                 Show help message
  -o DIR, --output DIR       Set root build directory (instead of default 'build/')
  --self-contained           Bundle .NET runtime (trimmed single file)
  --framework-dependent      Only include app + dependencies (default)
  --postfix POST             Use explicit string instead of today's date for ZIP postfix

Default RIDs (if none specified):
  win-x64 linux-x64 osx-x64

Example:
  $0 -o out_dir --self-contained --postfix v1.0 win-x64 osx-arm64
EOF
}

# Default values
MODE="framework-dependent"
POSTFIX=""
BUILD_DIR="build"
DEFAULT_RIDS=(win-x64 linux-x64 osx-x64)

# Parse options
while (( $# )); do
  case $1 in
    -h|--help) usage; exit 0 ;;
    -o|--output)
       BUILD_DIR="$2"; shift 2 ;;
    --output=*)
       BUILD_DIR="${1#*=}"; shift ;;
    --self-contained)
       MODE="self-contained"; shift ;;
    --framework-dependent)
       MODE="framework-dependent"; shift ;;
    --postfix)
       POSTFIX="$2"; shift 2 ;;
    --postfix=*)
       POSTFIX="${1#*=}"; shift ;;
    --*) echo "🚫 Unknown option: $1"; usage; exit 1 ;;
    *) break ;;
  esac
done

# Remaining arguments: RIDs
if [ $# -eq 0 ]; then
  RIDS=("${DEFAULT_RIDS[@]}")
else
  RIDS=("$@")
fi

# Determine postfix
if [[ -n "$POSTFIX" ]]; then
  SUFFIX="$POSTFIX"
else
  SUFFIX=$(date +%Y-%m-%d)
fi

# Build paths
ROOT="$BUILD_DIR"
BIN="$ROOT/bin"
PKG="$ROOT/packages"
EXPLORER_BIN="$BIN/explorer-game"
LESSON_BASE="$BIN/lesson-exec"

echo "🧹 Cleaning output directory: \"$ROOT\"..."
rm -rf "$ROOT"
mkdir -p "$EXPLORER_BIN" "$PKG"

echo "⚙️ Building explorer-game (Release)..."
dotnet build explorer-game/explorer-game.csproj -c Release -o "$EXPLORER_BIN"

echo "🐍 Building Python wheel (pyproject.toml in repo root)…"
if [[ -f "pyproject.toml" ]]; then
  python -m build --wheel --outdir "$PKG" .
  # Place the wheel next to the DLLs so it’s zipped together with them
  cp "$PKG"/*.whl "$EXPLORER_BIN"/
else
  echo "⚠️  No pyproject.toml in repo root — skipping Python build."
fi

echo "📦 Packaging explorer-game artifacts into lib-${SUFFIX}.zip (DLLs + .whl)…"
(
  cd "$EXPLORER_BIN"
  shopt -s nullglob
  zip -r "../../packages/csharp-lib-${SUFFIX}.zip" ./*.dll ./*.whl
  shopt -u nullglob
)

echo "🚀 Publishing lesson-exec in $MODE mode for RIDs: ${RIDS[*]}"

for RID in "${RIDS[@]}"; do
  OUT="$LESSON_BASE/$RID"
  echo "🔧 dotnet publish for ${RID}..."
  if [ "$MODE" = "self-contained" ]; then
    dotnet publish lesson-exec/lesson-exec.csproj \
      -c Release -r "$RID" \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:PublishTrimmed=true \
      -p:TrimMode=CopyUsed \
      -p:PublishReadyToRun=false \
      -o "$OUT"
  else
    dotnet publish lesson-exec/lesson-exec.csproj \
      -c Release -r "$RID" \
      --self-contained false \
      -o "$OUT"
  fi

  echo "📦 Zipping lesson-exec-${RID}-${SUFFIX}.zip..."
  (
    cd "$OUT"
    zip -r "../../../packages/lesson-exec-${RID}-${SUFFIX}.zip" ./*
  )
done

echo "✅ Build complete!"
echo
echo "👉 Explorer-game package: $PKG/lib-${SUFFIX}.zip"
echo "👉 Lesson-exec packages:"
count=0
for pkg in "$PKG"/lesson-exec-*-"${SUFFIX}.zip"; do
  if [[ -e "$pkg" ]]; then
    printf "   ➜ %s\n" "$pkg"
    count=$((count + 1))
  fi
done
[[ $count -eq 0 ]] && echo "   (none found)"