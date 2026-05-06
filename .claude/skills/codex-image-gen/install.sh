#!/usr/bin/env bash
# codex-image-gen skill installer (macOS / Linux / WSL)
#
# Usage (from the unzipped skill folder):
#     ./install.sh                       # interactive
#     ./install.sh --scope user          # install to ~/.claude/skills/
#     ./install.sh --scope project       # install to $PWD/.claude/skills/
#     ./install.sh --scope project --project-dir /path/to/repo
#     ./install.sh --skip-deps           # skip dependency check

set -euo pipefail

SKILL_NAME="codex-image-gen"
SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

scope="ask"
project_dir="$PWD"
skip_deps="0"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --scope) scope="$2"; shift 2 ;;
        --project-dir) project_dir="$2"; shift 2 ;;
        --skip-deps) skip_deps="1"; shift ;;
        -h|--help)
            sed -n '3,12p' "$0"; exit 0 ;;
        *) echo "Unknown arg: $1" >&2; exit 2 ;;
    esac
done

echo "=== codex-image-gen installer ==="
echo "Source: $SRC_DIR"
echo

# --- 1. Dependency checks --------------------------------------------------

if [[ "$skip_deps" == "0" ]]; then
    echo "[1/3] Checking dependencies..."

    PY=""
    for cand in python3 python py; do
        if command -v "$cand" >/dev/null 2>&1; then
            PY="$cand"
            break
        fi
    done
    if [[ -z "$PY" ]]; then
        echo "ERROR: Python 3 not found. Install Python 3.9+ first." >&2
        exit 1
    fi
    echo "  Python: $PY ($("$PY" --version))"

    if ! command -v codex >/dev/null 2>&1; then
        echo "ERROR: codex CLI not found on PATH." >&2
        echo "       Install from https://github.com/openai/codex first." >&2
        exit 1
    fi
    echo "  Codex CLI: $(codex --version 2>&1 | head -1)"
else
    echo "[1/3] Skipping dependency checks"
fi

# --- 2. Pick target --------------------------------------------------------

echo
echo "[2/3] Choosing install location..."

if [[ "$scope" == "ask" ]]; then
    echo
    echo "Where should the skill be installed?"
    echo "  [1] User-level    ($HOME/.claude/skills/$SKILL_NAME)"
    echo "      All your projects can trigger it. Recommended for personal use."
    echo "  [2] Project-local ($project_dir/.claude/skills/$SKILL_NAME)"
    echo "      Only that project can trigger it. Good for team-shared repos."
    echo
    while true; do
        read -p "Choice (1 or 2): " choice
        case "$choice" in
            1) scope="user"; break ;;
            2) scope="project"; break ;;
            *) echo "Please enter 1 or 2." ;;
        esac
    done
fi

if [[ "$scope" == "user" ]]; then
    target="$HOME/.claude/skills/$SKILL_NAME"
elif [[ "$scope" == "project" ]]; then
    target="$project_dir/.claude/skills/$SKILL_NAME"
else
    echo "ERROR: --scope must be 'user' or 'project'" >&2
    exit 2
fi

echo "  Target: $target"

# --- 3. Copy files ---------------------------------------------------------

echo
echo "[3/3] Copying files..."

if [[ -d "$target" ]]; then
    echo "  Target exists. Removing old version."
    rm -rf "$target"
fi
mkdir -p "$target"

(cd "$SRC_DIR" && tar --exclude='install.ps1' --exclude='install.sh' \
                     --exclude='__pycache__' \
                     -cf - .) | (cd "$target" && tar -xf -)

echo "  Copied to: $target"

# --- 4. Smoke test ---------------------------------------------------------

if [[ "$skip_deps" == "0" ]]; then
    echo
    echo "Running smoke test (generate.py --help)..."
    if "$PY" "$target/generate.py" --help >/dev/null 2>&1; then
        echo "  Smoke test PASSED"
    else
        echo "  Smoke test FAILED" >&2
    fi
fi

echo
echo "=== Install complete ==="
echo
echo "To use the skill in Claude Code, just ask for an image:"
echo '  "Draw me a watercolor fox in a forest"'
echo '  "Generate a cyberpunk city skyline, widescreen"'
echo '  "Make this photo black and white"  (with attached image)'
echo
echo "Or run the orchestrator manually:"
echo "  python3 $target/generate.py 'a watercolor fox' --output fox.png"
