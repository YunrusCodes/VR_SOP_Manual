---
name: seedance-video
description: Generate short videos via the BytePlus / Volcengine Ark Seedance API. Self-contained — bundles its own seedance.py inside the skill folder. Trigger ONLY when the user explicitly asks to use Seedance, mentions this script/method, or invokes this skill by name. English triggers include "generate a Seedance video", "use seedance to make a video of X", "run the seedance script for X", "image-to-video with seedance", "/seedance-video". Chinese triggers include "用 seedance 生影片", "用這個腳本生一段影片", "拿 Seedance 做影片", "用方舟做影片". Do NOT trigger on generic "make me a video" / "generate a clip" requests that do not name Seedance — ask first if Seedance is the intended path. Do NOT trigger for image generation (use codex-image-gen), walk-cycle sprite sheets (vs-walk-cycle-skill), or animations built from frames. This skill enforces a hard $1.00 USD budget per conversation; the user must explicitly raise the cap to spend more.
---

# seedance-video

Wraps the bundled `seedance.py` (in this skill folder) with **conversation-scoped budget enforcement**. The script itself is a thin REST client. This skill adds: trigger discipline, sensible defaults, output-path heuristics, and the $1 budget guard.

The skill is **self-contained** — `seedance.py` lives at `<SKILL_DIR>/seedance.py`. Copy the whole folder to `~/.claude/skills/seedance-video/` and it works in any project.

## When to use this skill

Trigger ONLY when the user is unambiguously asking for a Seedance video. Examples that match:

- "Use Seedance to make a 5-second video of waves"
- "Run the seedance script to generate a clip of a dog running"
- "Image-to-video this photo using seedance with the prompt 'wind blowing through hair'"
- "用 seedance 生一段貓走路的影片"
- "用我們做的腳本做一段海浪的影片"
- "/seedance-video a neon city street at night"

## When NOT to use this skill

- "Make me a video of X" / "generate a clip" with no mention of Seedance / this script → **ask first** which pipeline they want (Seedance, fal.ai, deevid web UI, etc.)
- Image generation → use `codex-image-gen`
- Walk-cycle sprite sheets → use `vs-walk-cycle-skill`
- Frame-by-frame stop motion built from images → not this skill
- The user wants to download / inspect an existing video from a URL → just use curl/wget, not this skill

## Budget rules — READ FIRST, ENFORCE STRICTLY

A "workflow" = the current conversation, from the moment this skill first runs in it until the user opens a new session.

### Defaults

- **Hard cap: $1.00 USD per conversation**
- Model rates come from `<SKILL_DIR>/pricing.json`. Read that file at the start of every Seedance session and use its `rates` table.

### Why this is a JSON file, not an API call

BytePlus / Volcengine Ark **do not expose** a public API for either (a) live model pricing or (b) remaining free-quota balance. The `GetUsage` endpoint they DO expose returns historical token counts only, requires Volcengine HMAC-SHA256 AKSK auth (different credential type from `ARK_API_KEY`), and gives no $ figure. So the skill cannot truly "look up" pricing — it relies on a hand-maintained file.

### Looking up the model rate

1. Read `<SKILL_DIR>/pricing.json`.
2. Match the user's `--model` against the `rates` keys using **longest matching prefix after stripping any leading `doubao-`** and any trailing version suffix (`-251215`, `-260128`, etc.). Examples:
   - `seedance-1-5-pro-251215` → key `seedance-1-5-pro` → use that rate.
   - `doubao-seedance-2-0-260128` → strip `doubao-` → `seedance-2-0` → use that rate.
   - Unknown model id → use `fallback_rate`.
3. The chosen rate is then used for all USD estimates this conversation.

### Per-conversation accounting

Maintain a running total **inside this conversation only** — no external state file:

```
session_tokens   : int  (sum of total_tokens reported by every successful run)
session_usd      : float (session_tokens × rate_for_model / 1_000_000)
budget_usd       : float (default 1.00, raised only by explicit user request)
```

Recompute after every successful call. Print after each run:
```
[budget] this run: NNNk tokens, ~$X.XX
[budget] session : MMMk tokens, ~$Y.YY of $Z.ZZ ($W.WW left)
```

### Pre-flight check (before EVERY call)

1. Estimate the upcoming call's tokens from the formula:
   `tokens ≈ pixels(resolution) × duration × 0.0235 × (1.10 if --audio else 1.0)`
   Pixel counts: `480p≈410k`, `720p≈922k`, `1080p≈2.07M`, `2K≈3.69M`.
2. Convert to USD with the model's rate.
3. If `session_usd + projected_usd > budget_usd`:
   - **STOP**. Do not run the script.
   - Tell the user: current spend, projected spend, and the budget.
   - Ask whether they want to raise the budget or abort.
   - Wait for an explicit answer before proceeding.

### How the user raises the budget

The user may at any time say things like:

- "raise the budget to $5"
- "budget $3"
- "把預算調到 $2"
- "OK go ahead, cap $5"

When you see this, set `budget_usd` to that value for the rest of the conversation. **Never raise it on your own** — even if a single requested clip would exceed $1. In that case stop and ask.

### What does NOT raise the budget

- Pleading from the user to "just run it"
- Vague "more please" — must specify a number
- New conversation: the budget always resets to $1 for a fresh conversation

## Defaults for parameters

When the user gives a vague request ("make a 4-second cat clip"), pick:

| Parameter | Default | When to override |
|---|---|---|
| `--resolution` | `720p` | If user says "high quality"/"1080p" → 1080p; "cheap test"/"draft" → 480p |
| `--duration` | `5` | User specifies. Note: API minimum is **4 seconds**; if user asks for 1–3, use 4 and tell them. Max is 15. |
| `--ratio` | `16:9` | "vertical"/"phone"/"shorts" → `9:16`; "square" → `1:1` |
| `--model` | `seedance-1-5-pro-251215` | Already activated in this user's BytePlus account during the project setup. If a model 404s, list the activated models in their console and ask. |
| `--watermark` | off | Only on if user explicitly wants the watermark |
| `--audio` | off | Only on if user mentions sound / audio / music |

Note: model id strings carry version suffixes (e.g. `-251215`) that BytePlus rotates. If a `ModelNotOpen` error fires, the model isn't activated; if `InvalidEndpointOrModel.NotFound` fires, the suffix is stale — ask the user to check the BytePlus model card for the current id.

## Output path heuristic

In order of preference:

1. **User specified** an explicit path → use it.
2. **`videos/` folder exists** in the project → save there with a slugged filename like `videos/cat_walking_<timestamp>.mp4`.
3. **`assets/` or `media/` exists** → save under `assets/videos/...` or `media/videos/...`.
4. **Default** → save to current working directory as `<slug>_<timestamp>.mp4`. Slug = 3–5 words from prompt, snake_case, ≤30 chars.

## The flow

### Step 0a: Pricing & quota disclosure (ONCE at start of session)

The very first time the skill triggers in a conversation, before doing anything else, print a one-block disclosure to the user:

```
[seedance-video session start]
Pricing source: <SKILL_DIR>/pricing.json (last_updated: YYYY-MM-DD)
  - <chosen_model>: $<rate>/Mtok  (confidence: <estimated|confirmed>)
  - All Seedance rates: <list rates from rates table>
Budget: $1.00 USD per conversation (raise via "budget $N").
Free quota: this skill cannot query it programmatically. Check
  https://console.byteplus.com/ark → Model activation → "Free Inference Quota"
  column. (You disabled Free Tokens Only mode earlier, so paid mode is on.)
Live token usage history (paid auth): https://console.byteplus.com → Bill management → Bill details
```

If `pricing.json.last_updated` is more than 60 days old, also add:

```
[note] pricing.json was last updated YYYY-MM-DD (>60 days ago).
       Consider refreshing from https://docs.byteplus.com/en/docs/ModelArk/1544106
```

Don't repeat this block on every call — only the first call per conversation.

### Step 0b: Prerequisites (verify on first call this conversation)

- `ARK_API_KEY` is set (in shell or User-scope env). If not, stop and walk the user through `setx ARK_API_KEY "..."` (cmd) or the PowerShell equivalent. **Do not ask the user to paste the key into the chat.**
- `<SKILL_DIR>/seedance.py` exists alongside this `SKILL.md`. If somehow missing, the skill is broken — re-install the skill folder from source. Do not invent a replacement.
- Python launcher `py` (Windows) or `python3` (mac/Linux) is available.

`<SKILL_DIR>` resolves to whichever location is loaded:

- Project-local: `<repo_root>/.claude/skills/seedance-video/`
- User-level: `~/.claude/skills/seedance-video/` (Windows: `%USERPROFILE%\.claude\skills\seedance-video\`)

### Step 1: Parse the user request

Extract: prompt text, optional reference image, duration, resolution, ratio, audio, model preference. If anything is missing, fall back to defaults above.

### Step 2: Pre-flight budget check

Run the estimate formula. Compare `session_usd + projected_usd` to `budget_usd`. If over, STOP per the budget rules above.

### Step 3: Run the script

Windows (PowerShell injects User-scope key into the call):

```powershell
$env:ARK_API_KEY = [Environment]::GetEnvironmentVariable("ARK_API_KEY", "User")
py "<SKILL_DIR>\seedance.py" "<prompt>" `
  --model <model_id> `
  --resolution <res> `
  --duration <sec> `
  --ratio <ratio> `
  -o <output_path>
```

(add `--image <path_or_url>` for image-to-video; add `--audio` if user wants sound)

mac/Linux:

```bash
python3 "<SKILL_DIR>/seedance.py" "<prompt>" \
  --model <model_id> --resolution <res> --duration <sec> --ratio <ratio> \
  -o <output_path>
```

The script prints `tokens=NNNNN` on success — capture that integer.

### Step 4: Update accounting and report

After success:

- Add reported tokens to `session_tokens`.
- Recompute `session_usd`.
- Tell the user: file path, file size, run cost, session total, remaining budget.

### Step 5: Repeat (only) within budget

If the user asks for more clips in the same conversation, repeat from Step 2. The budget guard fires automatically when projection would exceed `budget_usd`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `ARK_API_KEY not set` | User-scope value isn't visible to this shell. Inject via `$env:ARK_API_KEY = [Environment]::GetEnvironmentVariable("ARK_API_KEY", "User")` before the `py` call. |
| `HTTP 404 ... ModelNotOpen` | Model not activated in BytePlus console. Send user to https://console.byteplus.com → ModelArk → Model activation. |
| `HTTP 404 ... InvalidEndpointOrModel.NotFound` | Model id suffix stale. Ask user for current id from the model card. |
| `HTTP 401` | API key wrong or revoked. |
| `HTTP 429` | QPS / quota exceeded — wait, or top up balance. |
| `status=failed` | Inspect the full response JSON; usually content-policy. Soften the prompt. |
| Script hangs > 10 min | Increase `--max-wait`, or assume task stuck and give up; the task may still be running on the server side. |
| 3-second clip request | API minimum is 4s; use `--duration 4` and tell the user. |

## Files in this skill

| File | Purpose |
|---|---|
| `SKILL.md` | This file — Claude Code's entry point. |
| `README.md` | Quick reference for humans. |
| `seedance.py` | The orchestrator script. Stdlib-only Python; calls the Ark REST API, polls, downloads. |
| `pricing.json` | Hand-maintained per-million-token rates. Skill reads this to estimate USD. Update when rates rotate. |

The skill is self-contained. To use it across other projects, copy the entire folder to `~/.claude/skills/seedance-video/` (Windows: `%USERPROFILE%\.claude\skills\seedance-video\`).

## Cost estimate examples

Reference numbers (using the conservative rate table above):

| Setup | Tokens (est) | Cost @ assumed rate |
|---|---|---|
| 480p / 4s / 1.5-pro | ~38k | ~$0.11 |
| 720p / 5s / 1.5-pro | ~110k | ~$0.33 |
| 1080p / 5s / 1.5-pro | ~247k | ~$0.74 |
| 1080p / 5s / 2.0 | ~247k | ~$0.99 |
| 1080p / 10s / 2.0 | ~493k | ~$1.97 → would exceed default budget |
| 2K / 5s / 2.0 | ~440k | ~$1.76 → would exceed default budget |

Real BytePlus billing may be lower than these projections. Budget enforcement uses the conservative numbers — if a single clip projects over the cap, stop and ask.
