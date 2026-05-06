#!/usr/bin/env python3
"""
codex-image-gen: single-image generator via Codex CLI's built-in image_gen tool.

Usage:
    py generate.py "PROMPT TEXT" --output PATH [options]

Options:
    --output / -o PATH       Required. Where to save the resulting PNG.
    --reference / -r IMAGE   Use Codex's image_gen edit mode with this image
                             as reference. Prompt becomes a modification spec.
    --size SIZE              One of: 1024x1024 (default), 1536x1024, 1024x1536,
                             1792x1024, 1024x1792.
    --quality QUALITY        low | medium | high (default).
    --no-style-guard         Skip the "must use image_gen, no procedural
                             fallback" directive. Use only if you know what
                             you are doing.

Exit codes:
    0  - image saved successfully
    1  - codex exec failed, no new image found, or unrecoverable error
    2  - bad arguments

The script:
    1. Builds a short Codex prompt (text-to-image OR edit-with-reference).
    2. Snapshots ~/.codex/generated_images/ before running codex.
    3. Runs `codex exec --dangerously-bypass-approvals-and-sandbox -`
       and pipes the prompt via stdin (works around the Windows npm shim
       argv-length / cmd.exe quoting issue).
    4. Diffs the codex image directory after, picks the newest ig_*.png.
    5. Copies it to --output and prints the path.

Dependencies: Python 3.9+, codex CLI on PATH.
No third-party Python packages required.
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

CODEX_IMG_DIR = Path.home() / ".codex" / "generated_images"

VALID_SIZES = {"1024x1024", "1536x1024", "1024x1536", "1792x1024", "1024x1792"}
VALID_QUALITIES = {"low", "medium", "high"}


# ---------------------------------------------------------------------------
# Prompt builders
# ---------------------------------------------------------------------------

TOOL_GUARD = """\
TOOL DIRECTIVE - NON-NEGOTIABLE:
Use the built-in `image_gen` tool (gpt-image-2). FORBIDDEN: procedural
pixel-drawing code (Python/PIL/struct/zlib), SVG, web downloads, ASCII art,
or any non-image_gen path. The output MUST come from the image_gen tool.
After image_gen succeeds, print the absolute path of the generated file
and exit.
"""


def build_text_to_image_prompt(prompt: str, size: str, quality: str,
                                style_guard: bool) -> str:
    parts = []
    if style_guard:
        parts.append(TOOL_GUARD)
    parts.append(f"Generate ONE image with these specs:")
    parts.append(f"  Size:    {size}")
    parts.append(f"  Quality: {quality}")
    parts.append("")
    parts.append("PROMPT:")
    parts.append(prompt.strip())
    return "\n".join(parts) + "\n"


def build_edit_prompt(prompt: str, reference: Path, size: str, quality: str,
                      style_guard: bool) -> str:
    parts = []
    if style_guard:
        parts.append(TOOL_GUARD)
    parts.append("Use image_gen in EDIT mode with this reference image:")
    parts.append(f"  Reference: {reference}")
    parts.append(f"  Size:      {size}")
    parts.append(f"  Quality:   {quality}")
    parts.append("")
    parts.append("MODIFICATION:")
    parts.append(prompt.strip())
    parts.append("")
    parts.append("Keep everything in the reference image identical EXCEPT what")
    parts.append("the modification describes. If the modification is large or")
    parts.append("vague (>30% of the visual area), the reference may lose")
    parts.append("identity - in that case generate from scratch using the")
    parts.append("reference as visual inspiration only.")
    return "\n".join(parts) + "\n"


# ---------------------------------------------------------------------------
# Codex integration
# ---------------------------------------------------------------------------

def list_codex_images() -> dict:
    if not CODEX_IMG_DIR.exists():
        return {}
    return {p: p.stat().st_mtime for p in CODEX_IMG_DIR.rglob("ig_*.png")}


def find_codex_executable() -> str:
    exe = shutil.which("codex")
    if not exe:
        sys.exit(
            "ERROR: `codex` CLI not found on PATH.\n"
            "Install instructions: https://github.com/openai/codex\n"
            "After install, run `codex --version` to confirm it works."
        )
    return exe


_SESSION_RE = re.compile(r"session id:\s*([0-9a-f-]+)", re.IGNORECASE)


def run_codex(prompt_text: str, work_dir: Path) -> Path:
    """Run codex exec, return path of the newest generated ig_*.png.

    To avoid races when multiple instances run in parallel, we parse the
    `session id: <uuid>` line that codex prints early in its run and then
    look ONLY in `~/.codex/generated_images/<session_id>/` for the new file.
    Each codex invocation writes into its own session subfolder, so once we
    know our session id, we cannot collide with peers.
    """
    codex = find_codex_executable()

    cmd = [
        codex, "exec",
        "--skip-git-repo-check",
        "-C", str(work_dir),
        "--dangerously-bypass-approvals-and-sandbox",
        "-",
    ]

    print(f"[codex exec] generating image (prompt {len(prompt_text)} chars via stdin)")
    print(f"  cwd: {work_dir}")
    sys.stdout.flush()

    # Pipe stdout so we can parse session id; mirror to our stdout for visibility.
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        bufsize=1,
    )
    proc.stdin.write(prompt_text)
    proc.stdin.close()

    session_id: str | None = None
    captured: list[str] = []
    for line in proc.stdout:
        captured.append(line)
        sys.stdout.write(line)
        if session_id is None:
            m = _SESSION_RE.search(line)
            if m:
                session_id = m.group(1).strip()
    proc.wait()
    if proc.returncode != 0:
        sys.exit(f"codex exec returned {proc.returncode}")

    if session_id is None:
        # Fall back to legacy newest-file behavior so the script still works
        # if the codex output format changes. Print a warning so we notice.
        print("[warn] could not parse session id from codex stdout; falling back to newest ig_*.png")
        cands = sorted(
            CODEX_IMG_DIR.rglob("ig_*.png"),
            key=lambda p: p.stat().st_mtime,
            reverse=True,
        )
        if not cands:
            sys.exit(f"ERROR: no ig_*.png found under {CODEX_IMG_DIR}.")
        return cands[0]

    session_dir = CODEX_IMG_DIR / session_id
    if not session_dir.exists():
        sys.exit(
            f"ERROR: codex session {session_id} produced no image folder.\n"
            f"  Expected: {session_dir}\n"
            "  Possible causes:\n"
            "  - codex skipped image_gen for procedural fallback (rare with TOOL DIRECTIVE)\n"
            "  - codex CLI is not authenticated; run `codex --version` interactively first\n"
            "  - codex quota is exhausted\n"
        )
    cands = sorted(session_dir.glob("ig_*.png"), key=lambda p: p.stat().st_mtime, reverse=True)
    if not cands:
        sys.exit(f"ERROR: codex session {session_dir} contains no ig_*.png.")
    return cands[0]


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(
        description="Generate one image via Codex CLI's image_gen tool",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  py generate.py \"a watercolor painting of a fox in a forest\" -o fox.png\n"
            "  py generate.py \"make this cyberpunk\" -r portrait.png -o portrait_cyber.png\n"
            "  py generate.py \"\" -o out.png --size 1792x1024  # widescreen\n"
        ),
    )
    ap.add_argument("prompt", help="Text describing the image (or modification if --reference)")
    ap.add_argument("--output", "-o", required=True, help="Output PNG path")
    ap.add_argument("--reference", "-r", default=None,
                    help="Optional reference image for image_gen edit mode")
    ap.add_argument("--size", default="1024x1024",
                    choices=sorted(VALID_SIZES))
    ap.add_argument("--quality", default="high",
                    choices=sorted(VALID_QUALITIES))
    ap.add_argument("--no-style-guard", action="store_true",
                    help="Skip the 'use image_gen, no procedural fallback' directive")
    args = ap.parse_args()

    if not args.prompt.strip():
        sys.exit("ERROR: prompt cannot be empty")

    output = Path(args.output).expanduser().resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    reference = None
    if args.reference:
        reference = Path(args.reference).expanduser().resolve()
        if not reference.exists():
            sys.exit(f"ERROR: reference image not found: {reference}")
        if not reference.is_file():
            sys.exit(f"ERROR: reference is not a file: {reference}")

    style_guard = not args.no_style_guard
    if reference:
        codex_prompt = build_edit_prompt(args.prompt, reference, args.size,
                                         args.quality, style_guard)
        print(f"[mode] edit-with-reference  ({reference.name})")
    else:
        codex_prompt = build_text_to_image_prompt(args.prompt, args.size,
                                                   args.quality, style_guard)
        print(f"[mode] text-to-image")

    print(f"[output] {output}")
    print(f"[size] {args.size}  [quality] {args.quality}")
    print()

    src = run_codex(codex_prompt, work_dir=output.parent)

    shutil.copy2(src, output)
    print()
    print(f"Done. Image saved: {output}")
    print(f"  source:  {src}")
    print(f"  size:    {output.stat().st_size:,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
