#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FMOD_STUDIO_DEB_URL="${1:-${FMOD_STUDIO_DEB_URL:-}}"
FMOD_PROJECT_ARCHIVE_URL="${2:-${FMOD_PROJECT_ARCHIVE_URL:-}}"
OUTPUT_DIR="${3:-"$ROOT_DIR/.fmod-generated-banks"}"
WORK_DIR="${FMOD_BANKS_WORK_DIR:-"$ROOT_DIR/.fmod-work"}"

if [[ -z "$FMOD_STUDIO_DEB_URL" ]]; then
  echo "Missing FMOD studio installer URL."
  echo "Usage: bash scripts/build-fmod-banks.sh <fmod-studio-deb-url> <fmod-project-archive-url> [output-dir]"
  exit 1
fi

if [[ -z "$FMOD_PROJECT_ARCHIVE_URL" ]]; then
  echo "Missing FMOD project archive URL (.zip/.tar)."
  echo "Usage: bash scripts/build-fmod-banks.sh <fmod-studio-deb-url> <fmod-project-archive-url> [output-dir]"
  exit 1
fi

echo "[fmod] work dir: $WORK_DIR"
rm -rf "$WORK_DIR" "$OUTPUT_DIR"
mkdir -p "$WORK_DIR" "$OUTPUT_DIR"

STUDIO_DEB_PATH="$WORK_DIR/fmodstudio-installer.deb"
PROJECT_ARCHIVE_PATH="$WORK_DIR/fmod-project.archive"

echo "[fmod] downloading studio deb"
curl -L --fail -o "$STUDIO_DEB_PATH" "$FMOD_STUDIO_DEB_URL"

echo "[fmod] downloading project archive"
curl -L --fail -o "$PROJECT_ARCHIVE_PATH" "$FMOD_PROJECT_ARCHIVE_URL"

echo "[fmod] extracting studio deb and project"
python3 - "$STUDIO_DEB_PATH" "$PROJECT_ARCHIVE_PATH" "$WORK_DIR" <<'PY'
import io
import os
import sys
import tarfile
import zipfile
from pathlib import Path


def extract_archive(archive_path: Path, target_dir: Path) -> None:
    if zipfile.is_zipfile(archive_path):
        with zipfile.ZipFile(archive_path) as zf:
            zf.extractall(target_dir)
        return

    if tarfile.is_tarfile(archive_path):
        with tarfile.open(archive_path) as tf:
            tf.extractall(target_dir)
        return

    raise SystemExit(f"Unsupported archive format: {archive_path}")


def parse_ar_members(path: Path):
    data = path.read_bytes()
    if data[:8] != b"!<arch>\n":
        raise SystemExit(f"Invalid .deb/.ar header: {path}")

    offset = 8
    while offset + 60 <= len(data):
        header = data[offset : offset + 60]
        name = header[:16].decode("utf-8", "ignore").strip().rstrip("/")
        size = int(header[48:58].decode("utf-8", "ignore").strip())
        body_start = offset + 60
        body_end = body_start + size
        yield name, data[body_start:body_end]
        offset = body_end + (body_end % 2)


def extract_deb_data(deb_path: Path, target_dir: Path) -> None:
    data_member = None
    for name, payload in parse_ar_members(deb_path):
        if name.startswith("data.tar"):
            data_member = (name, payload)
            break

    if data_member is None:
        raise SystemExit("Could not find data.tar.* in FMOD .deb")

    data_name, payload = data_member
    mode = "r:*"
    if data_name.endswith(".xz"):
        mode = "r:xz"
    elif data_name.endswith(".gz"):
        mode = "r:gz"
    elif data_name.endswith(".zst"):
        mode = "r:*"

    with tarfile.open(fileobj=io.BytesIO(payload), mode=mode) as tf:
        tf.extractall(target_dir)


studio_deb = Path(sys.argv[1])
project_archive = Path(sys.argv[2])
work_dir = Path(sys.argv[3])

studio_out = work_dir / "studio"
project_out = work_dir / "project"
studio_out.mkdir(parents=True, exist_ok=True)
project_out.mkdir(parents=True, exist_ok=True)

extract_deb_data(studio_deb, studio_out)
extract_archive(project_archive, project_out)

print("studio_root", studio_out)
print("project_root", project_out)
PY

FMOD_HOME="$WORK_DIR/studio/opt/fmodstudio"
FMOD_CLI="$FMOD_HOME/fmodstudiocl"

if [[ ! -x "$FMOD_CLI" ]]; then
  echo "FMOD CLI not found at $FMOD_CLI"
  exit 1
fi

FSPRO_PATH="$(python3 - "$WORK_DIR/project" <<'PY'
import os
import sys

root = sys.argv[1]
for dirpath, _, filenames in os.walk(root):
    for name in filenames:
        if name.lower().endswith('.fspro'):
            print(os.path.join(dirpath, name))
            raise SystemExit(0)

raise SystemExit(1)
PY
)"

if [[ -z "$FSPRO_PATH" || ! -f "$FSPRO_PATH" ]]; then
  echo "No .fspro file found in extracted FMOD project archive"
  exit 1
fi

echo "[fmod] fspro: $FSPRO_PATH"
SERIALIZATION_MODEL="$(python3 - "$FSPRO_PATH" <<'PY'
import re
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding='utf-8', errors='ignore')
match = re.search(r'serializationModel="([^"]+)"', text)
print(match.group(1) if match else 'unknown')
PY
)"
echo "[fmod] project serialization model: $SERIALIZATION_MODEL"

PROJECT_DIR="$(dirname "$FSPRO_PATH")"
pushd "$PROJECT_DIR" >/dev/null

export LD_LIBRARY_PATH="$FMOD_HOME/lib:${LD_LIBRARY_PATH:-}"
export QT_QPA_PLATFORM="${QT_QPA_PLATFORM:-minimal}"

echo "[fmod] building banks via fmodstudiocl"
BUILD_LOG="$WORK_DIR/fmod-build.log"
if ! "$FMOD_CLI" --platform minimal -build "$FSPRO_PATH" 2>&1 | tee "$BUILD_LOG"; then
  if grep -qi "Project is out of date and requires project migration" "$BUILD_LOG"; then
    echo "[fmod] ERROR: This FMOD project is from an older major version and must be migrated in FMOD Studio UI before CLI build."
    echo "[fmod] Open project in FMOD Studio 2.03.12, allow migration, Save Project, and upload the migrated project archive."
    exit 2
  fi

  if command -v xvfb-run >/dev/null 2>&1; then
    echo "[fmod] minimal mode failed; retrying with xvfb-run"
    if ! xvfb-run -a "$FMOD_CLI" --platform minimal -build "$FSPRO_PATH" 2>&1 | tee -a "$BUILD_LOG"; then
      if grep -qi "Project is out of date and requires project migration" "$BUILD_LOG"; then
        echo "[fmod] ERROR: This FMOD project is from an older major version and must be migrated in FMOD Studio UI before CLI build."
        echo "[fmod] Open project in FMOD Studio 2.03.12, allow migration, Save Project, and upload the migrated project archive."
        exit 2
      fi

      echo "[fmod] build failed; see log at $BUILD_LOG"
      exit 1
    fi
  else
    echo "[fmod] build failed and xvfb-run is unavailable"
    exit 1
  fi
fi

popd >/dev/null

REQUIRED_BANKS=(
  "Master Bank.bank"
  "Master Bank.strings.bank"
  "music.bank"
  "sfx.bank"
  "ui.bank"
  "dlc_music.bank"
  "dlc_sfx.bank"
)

echo "[fmod] collecting required banks"
python3 - "$PROJECT_DIR" "$OUTPUT_DIR" "${REQUIRED_BANKS[@]}" <<'PY'
import os
import shutil
import sys
from pathlib import Path

project_dir = Path(sys.argv[1])
output_dir = Path(sys.argv[2])
required = sys.argv[3:]

all_files = []
for dirpath, _, filenames in os.walk(project_dir):
    for name in filenames:
        if name.lower().endswith('.bank'):
            full = Path(dirpath) / name
            all_files.append(full)

missing = []
for bank_name in required:
    candidates = [path for path in all_files if path.name.lower() == bank_name.lower()]
    if not candidates:
        missing.append(bank_name)
        continue

    chosen = max(candidates, key=lambda p: p.stat().st_mtime)
    shutil.copy2(chosen, output_dir / bank_name)
    print(f"copied {bank_name} <- {chosen}")

if missing:
    raise SystemExit("Missing required banks after build: " + ", ".join(missing))
PY

echo "[fmod] banks ready at: $OUTPUT_DIR"
ls -la "$OUTPUT_DIR"
