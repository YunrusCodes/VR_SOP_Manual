# seedance-video skill

Wraps [seedance.py](seedance.py) (bundled in this folder) with a $1 budget guard. Self-contained — copy this whole folder to `~/.claude/skills/seedance-video/` to use it in any project.

## What it does

When the user explicitly asks for a Seedance video, this skill:

1. Picks sensible defaults (720p / 5s / 16:9, model `seedance-1-5-pro-251215`).
2. Estimates the token cost before sending.
3. Tracks cumulative spend across the whole conversation.
4. **Stops at $1.00 USD per conversation** unless the user raises the cap.
5. Runs the script, downloads the MP4, reports cost.

## Trigger phrases

The skill only fires when the user explicitly invokes Seedance / this script. Examples:

- "Use Seedance to make a video of …"
- "Run the seedance script for …"
- "用 seedance 生影片 …"
- "/seedance-video …"

It deliberately does NOT trigger on plain "make me a video" — Claude will ask which pipeline first.

## Raising the budget

The user controls the cap. Examples that work:

- "raise the budget to $5"
- "budget $3"
- "把預算調到 $2"

The skill never raises its own cap, even if a single requested clip would exceed it.

## Prerequisites

- `ARK_API_KEY` set in user-scope env (BytePlus or Volcengine API key)
- Seedance model activated in the BytePlus / Volcengine ModelArk console
- Python 3.9+ on PATH (`py` on Windows, `python3` on mac/Linux)
- The orchestrator script at `<repo>/scripts/seedance.py`

## Files

- `SKILL.md` — instructions Claude Code reads
- `seedance.py` — the REST orchestrator (stdlib-only Python)
- `README.md` — this file

## Direct CLI use (without Claude)

You can also invoke `seedance.py` directly without going through Claude / the skill — the budget guard is agent-side, not script-side, so the script just runs whatever you ask:

```powershell
$env:ARK_API_KEY = "ark-..."
py .claude\skills\seedance-video\seedance.py "a cat walking, side view" --resolution 720p --duration 4 -o cat.mp4
```
