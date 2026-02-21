#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
URL="${1:-${CONTENT_ARCHIVE_URL:-}}"
TARGET_DIR="${2:-"$ROOT_DIR/src/Celeste.Android/ContentPackage"}"

if [[ -z "$URL" ]]; then
  echo "Usage: bash scripts/fetch-content.sh <content-archive-url> [target-dir]"
  echo "Or set CONTENT_ARCHIVE_URL environment variable."
  exit 1
fi

echo "[content] target: $TARGET_DIR"
rm -rf "$TARGET_DIR"
mkdir -p "$TARGET_DIR"

ARCHIVE_PATH="$TARGET_DIR/content.archive"
echo "[content] downloading: $URL"
curl -L --fail -o "$ARCHIVE_PATH" "$URL"

echo "[content] extracting archive"
python3 - "$ARCHIVE_PATH" "$TARGET_DIR" <<'PY'
import os
import sys
import tarfile
import zipfile

archive_path = sys.argv[1]
target_dir = sys.argv[2]

if zipfile.is_zipfile(archive_path):
    with zipfile.ZipFile(archive_path) as archive:
        archive.extractall(target_dir)
elif tarfile.is_tarfile(archive_path):
    with tarfile.open(archive_path) as archive:
        archive.extractall(target_dir)
else:
    raise SystemExit(f"Unsupported archive format: {archive_path}")

content_dir = os.path.join(target_dir, "Content")
if not os.path.isdir(content_dir):
    raise SystemExit(
        f"Archive extracted but '{content_dir}' was not found. "
        "Expected archive root to contain a 'Content/' folder."
    )
PY

rm -f "$ARCHIVE_PATH"
echo "[content] ready: $TARGET_DIR/Content"
