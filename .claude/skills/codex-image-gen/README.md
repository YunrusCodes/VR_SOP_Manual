# codex-image-gen

A minimal Claude Code skill that turns user image-generation requests ("draw me X", "畫一張 X") into a single Codex CLI `image_gen` call. One image per request, no orchestration, no multi-frame stitching, no quality verifier.

Use it for: anything that boils down to "produce one PNG file from a description".
Don't use it for: 4-frame walk-cycle sprite sheets (that's `vs-character-walk-cycle`), Mermaid diagrams, or matplotlib charts.

## Quick install

### Windows (PowerShell)

```powershell
cd <where-you-unzipped-this>
.\install.ps1
```

### macOS / Linux / WSL

```bash
cd <where-you-unzipped-this>
chmod +x install.sh
./install.sh
```

Both installers ask **user-level** (`~/.claude/skills/` — all projects trigger it) vs **project-local** (`<repo>/.claude/skills/` — only that repo). They auto-detect Python and verify codex CLI is on PATH.

## Prerequisites

| Requirement | Check (Windows) | Check (mac/Linux) |
|---|---|---|
| Python 3.9+ | `py --version` | `python3 --version` |
| Codex CLI authenticated | `codex --version` | `codex --version` |

This skill needs **no Python packages** — only the standard library. (Unlike the walk-cycle skill which needs PyYAML / Pillow / numpy.)

If `codex --version` fails, install from https://github.com/openai/codex and run `codex` once interactively to authenticate. The skill cannot do that step for you.

## Use it (the way the skill is designed for)

After install, just talk to Claude Code:

> Draw me a watercolor fox in a forest

> 畫一張穿太空服的貓、賽博龐克風格、橫幅尺寸

> Make this photo black and white  *[+ attached image]*

Claude Code will:
1. Detect the trigger and load this skill
2. Decide where to save the file (current working dir, `assets/`, etc — based on your project layout)
3. Pick a size (square / widescreen / portrait based on what you said)
4. Run `generate.py` in the background
5. Show you the result with a clickable path

## Use it manually

### Windows (PowerShell)

```powershell
# Text-to-image
py "$env:USERPROFILE\.claude\skills\codex-image-gen\generate.py" `
  "a watercolor painting of a fox in an autumn forest" `
  --output fox.png

# Edit mode (with reference image)
py "$env:USERPROFILE\.claude\skills\codex-image-gen\generate.py" `
  "make this person have a pirate hat and eyepatch" `
  --reference portrait.jpg `
  --output portrait_pirate.png

# Widescreen
py "$env:USERPROFILE\.claude\skills\codex-image-gen\generate.py" `
  "futuristic cyberpunk city skyline at night" `
  --output skyline.png `
  --size 1792x1024
```

### macOS / Linux

```bash
python3 ~/.claude/skills/codex-image-gen/generate.py \
  "a watercolor painting of a fox in an autumn forest" \
  --output fox.png

python3 ~/.claude/skills/codex-image-gen/generate.py \
  "make this person have a pirate hat and eyepatch" \
  --reference portrait.jpg \
  --output portrait_pirate.png
```

### Useful flags

| Flag | Effect |
|---|---|
| `--output PATH` (required) | Where to save the PNG |
| `--reference IMAGE` | Use edit mode with reference (image-to-image) |
| `--size SIZE` | `1024x1024` (default), `1536x1024`, `1024x1536`, `1792x1024`, `1024x1792` |
| `--quality QUALITY` | `low`, `medium`, `high` (default) |
| `--no-style-guard` | Skip the "must use image_gen, no procedural fallback" directive. Power-user only. |

## How it works

```
User prompt
    │
    ▼
generate.py
    │
    ├─ Snapshot ~/.codex/generated_images/  (mtime per existing ig_*.png)
    │
    ├─ Build short codex prompt with TOOL DIRECTIVE + your text
    │
    ├─ subprocess.run([codex, exec, --skip-git-repo-check, -C cwd,
    │                  --dangerously-bypass-approvals-and-sandbox, "-"],
    │                 input=prompt, text=True)
    │     └── codex calls image_gen → saves ig_*.png under ~/.codex/generated_images/
    │
    ├─ Diff after vs before, pick the newest ig_*.png by mtime
    │
    └─ shutil.copy2 to your --output path
```

That's the whole thing. ~150 lines of Python, no third-party deps.

## Why send prompt via stdin?

On Windows, the codex CLI is wrapped by an npm `.cmd` shim. When you pass a long multi-line prompt as the positional argv, cmd.exe's quoting rules truncate or mangle it — codex receives an empty or broken prompt and replies "I'm ready. What would you like me to do?". Sending via stdin (using `-` as the positional arg) bypasses the cmd boundary entirely and works identically on macOS/Linux.

This was a real bug in the first version of the walk-cycle skill — that's why this skill ships with the fix from day one.

## Folder layout (after install)

```
~/.claude/skills/codex-image-gen/      (or <repo>/.claude/skills/codex-image-gen/)
├── SKILL.md                # Claude Code reads this for triggering & flow
├── README.md               # This file
├── generate.py             # The orchestrator (the only thing you call)
├── install.ps1             # Windows installer
└── install.sh              # macOS/Linux installer
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| `codex` not found | Install from https://github.com/openai/codex; run `codex` once to authenticate |
| Exit code 49 from `python --version` (Windows) | Use `py` instead of `python` — the official Python launcher is `py` on Windows |
| Codex says "I'm ready. What would you like me to do?" | generate.py is out of date. Re-install or copy fresh `generate.py` over. |
| `codex exec` returns 0 but no new image | Quota exhausted; or codex isn't authenticated (run `codex` once interactively); or codex chose procedural fallback (rare with TOOL DIRECTIVE) |
| Reference identity lost after edit | The modification was too large for image_gen edit mode (>30% of visual area). Switch to text-to-image and describe key features of the reference in the prompt. |
| Chinese / non-ASCII path shown as garbage in codex log | Display-only issue from Windows codepage. Run `chcp 65001` once to clean up output. Skill still works correctly. |

## Cost

1 image_gen call per request, ~30-90s wall time. Typical Codex Plus tier: 50-200 images/day. Quality settings:

- `low` — fastest, lowest detail, lowest quota cost
- `medium` — middle ground
- `high` — default; sharp detail, higher quota cost
