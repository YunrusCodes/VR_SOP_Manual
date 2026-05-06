---
name: codex-image-gen
description: Generate ANY single still image via Codex CLI's built-in image_gen tool (gpt-image-2). Trigger this skill whenever the user asks for an image to be drawn, generated, created, made, painted, illustrated, rendered, or designed. Triggers in English include "draw me X", "draw a/an X", "generate an image of X", "create a picture of X", "make me an image showing X", "render X", "illustrate X", "paint X", "design a logo/poster/cover for X", "give me a picture of X". Triggers in Chinese include "畫一張 X 的圖", "幫我畫 X", "生成一張 X 的圖", "做一張 X 的圖", "設計一張 X". Also handles image EDITING — when the user provides a reference image (attached file, path, or URL) AND asks to modify it (e.g., "change this to cyberpunk style", "add a beard to this person", "make this black and white"), use edit mode by passing --reference. Do NOT trigger this skill for: 4-frame walk-cycle sprite sheets (use vs-character-walk-cycle instead), diagrams/flowcharts (use mermaid or other diagram tools), or chart/data visualizations (use matplotlib/recharts). For everything else that boils down to "produce one PNG file from a description", use this skill.
---

# codex-image-gen

This skill turns user image-generation requests into a single Codex CLI `image_gen` call and saves the result somewhere the user can find. It's a thin wrapper — no orchestration, no multi-frame stitching, no quality verifier. Just: **prompt in, PNG out**.

## When to use this skill

The user wants ONE image. Trigger phrases include:

- "Draw me a watercolor fox"
- "Generate an image of a futuristic city skyline"
- "Make me a poster for a sci-fi movie"
- "Design a minimalist logo with a mountain"
- "畫一張穿太空服的貓"
- "幫我做一張 cyberpunk 風格的橋"

Also trigger for **image editing** — user provides a reference image (uploaded, attached, or a path) AND a modification request:

- "Make this photo black and white"
- "Change the background of this image to a beach"
- "Add sunglasses to this person"
- "把這張圖改成宮崎駿風格"

## When NOT to use this skill

- 4-frame walk-cycle sprite sheets → use `vs-character-walk-cycle` instead
- Diagrams / flowcharts / architecture diagrams → use Mermaid or specialized diagram tools
- Charts / graphs / data visualizations → use matplotlib / recharts / d3
- Multi-image workflows (storyboards, mood boards) → call this skill multiple times, but each call still produces ONE image

## Prerequisites (verify on first use)

| Requirement | Check command (Windows) | Check command (mac/Linux) |
|---|---|---|
| Codex CLI | `codex --version` | `codex --version` |
| Python 3.9+ | `py --version` | `python3 --version` |

The skill needs no Python packages — only the standard library and the codex executable on PATH.

If `codex --version` fails, install from https://github.com/openai/codex and run `codex` once interactively to authenticate. The skill cannot complete that step on the user's behalf.

## The flow you (Claude Code) should follow

### Step 1: Decide the output location

The user picked "Claude Code decides" when installing this skill, which means: use your judgment based on the situation.

Heuristics, in order:

1. **User specified a path** (e.g., "save it to assets/hero.png", "put it in ./art/") → use that exact path.
2. **A clearly relevant folder exists in the project** (e.g., `assets/`, `images/`, `art/`, `public/img/`, `static/images/`) → save there with a descriptive filename.
3. **Project looks game-related** (Unity/Unreal/Godot detected, or a `Sprites/` folder exists) → save under that.
4. **Default fallback** → save to current working directory with filename `image_<short_slug>_<timestamp>.png`. The slug is 3-5 words from the prompt, snake_case, ≤30 chars total.

When in doubt, pick option 4 (cwd) and tell the user where it landed. Do not silently scatter images across random folders.

### Step 2: Decide the size

| User intent | Use size |
|---|---|
| Square / icon / avatar / generic / not specified | `1024x1024` (default) |
| Wide / banner / hero image / "landscape" / "horizontal" / 16:9 | `1792x1024` |
| Tall / portrait / poster / phone wallpaper / "vertical" | `1024x1792` |
| Slightly wide (3:2 photo, screenshot) | `1536x1024` |
| Slightly tall | `1024x1536` |

If the user says specific pixel dimensions (e.g., "1280x720"), pick the closest valid size and tell the user the actual size delivered.

### Step 3: Run the orchestrator

**Windows (PowerShell or cmd):** use `py`.

```powershell
py "<SKILL_DIR>\generate.py" "<prompt>" --output "<output_path>" [--size 1024x1024] [--reference <ref_image>]
```

**macOS / Linux:** use `python3`.

```bash
python3 <SKILL_DIR>/generate.py "<prompt>" --output <output_path> [--size 1024x1024] [--reference <ref_image>]
```

`<SKILL_DIR>` resolves to one of:
- User-level Windows: `$env:USERPROFILE\.claude\skills\codex-image-gen\`
- Project-local Windows: `<repo_root>\.claude\skills\codex-image-gen\`
- User-level macOS/Linux: `~/.claude/skills/codex-image-gen/`
- Project-local macOS/Linux: `<repo_root>/.claude/skills/codex-image-gen/`

**Important on Windows**: use `py`, NOT `python`. The `python` alias is often missing depending on how Python was installed; `py` is always present after a standard Python 3 install.

### Step 4: Run in the background and monitor

The codex call takes ~30-90 seconds. Run it in the background (Claude Code's bash tool supports `run_in_background`). When it finishes, the script prints `Done. Image saved: <path>` — that's your cue to report back.

If the script fails (non-zero exit), read its stderr and report the error verbatim plus a one-line guess at what to do (e.g., "codex quota exhausted — wait for refresh or upgrade tier").

### Step 5: Show the user the result

Once the file exists, present it to the user. If you have a `present_files` or similar tool that renders inline, use it. Otherwise just give the absolute path with a `computer://` URL so they can click to open.

## Edit-mode rules (when user provides a reference image)

Pass the reference path via `--reference`. The script will tell codex to use image_gen edit mode and treat the user's prompt as a modification spec.

**Important**: image_gen edit mode preserves the reference's identity well for SMALL changes (style swap, add a small element, color shift). For LARGE changes (~30%+ of the visual area, redesigning the subject), reference identity gets diluted and the result diverges from the source. If the user asks for a large change, either:

1. Warn them and proceed anyway, OR
2. Suggest going text-to-image with a description that includes elements from the reference

Don't auto-fall-back without telling them.

## Useful generate.py flags

| Flag | Effect |
|---|---|
| `--output PATH` (required) | Where to save the PNG |
| `--reference IMAGE` | Use edit mode with this reference |
| `--size SIZE` | One of 1024x1024 / 1536x1024 / 1024x1536 / 1792x1024 / 1024x1792 |
| `--quality QUALITY` | low / medium / high (default high) |
| `--no-style-guard` | Skip the "use image_gen, no procedural fallback" directive. Only use if you trust the user knows what they're doing. |

## Troubleshooting

| Symptom | Fix |
|---|---|
| `codex` not found | Install from https://github.com/openai/codex, then `codex` once interactively to authenticate |
| Exit code 49 from `python --version` (Windows) | Use `py` not `python` — the official Python launcher is `py` on Windows |
| Codex says "I'm ready. What would you like me to do?" | Old generate.py; if you see this, your local copy is out of date — re-install or copy fresh generate.py |
| Output file is < 5 KB and looks programmatic | Codex skipped image_gen for procedural fallback. Re-run; the TOOL DIRECTIVE in the prompt should normally prevent this. |
| codex quota exhausted | Wait for next billing cycle, upgrade tier, or use a different image source |
| Reference identity lost after edit | The modification was too large for edit mode (>30% of visual area). Switch to text-to-image and include the reference's key features in the prompt. |

## Files in this skill

| File | Purpose |
|---|---|
| `SKILL.md` | This file (Claude Code's entry point) |
| `generate.py` | The single-image orchestrator |
| `README.md` | Quick install + use guide |
| `install.ps1` / `install.sh` | Deployment scripts |

## Cost estimate

- 1 image_gen call per request, ~30-90 seconds wall time
- Roughly 1 codex "image generation" of quota per call (varies by codex tier)

For a typical Claude Pro user via Codex CLI, expect 50-200 images/day depending on tier.
