#!/usr/bin/env python3
"""
seedance.py - call ByteDance Seedance video-generation API (BytePlus / Volcengine Ark).

Submits a text-to-video or image-to-video task, polls until it finishes,
prints the resulting video URL, and (by default) downloads it locally.

Defaults target BytePlus (international, ap-southeast region) so this works
without a mainland China phone number. Switch to Volcengine mainland by
setting --base-url (or ARK_BASE_URL) and using a doubao-prefixed model id.

Auth:
    Set ARK_API_KEY in your environment (key from BytePlus or Volcengine
    console). Override per-call with --api-key.

Region presets:
    BytePlus (default):  https://ark.ap-southeast.bytepluses.com/api/v3
                         model ids: seedance-2-0-260128, seedance-1-5-pro-251215, ...
    Volcengine mainland: https://ark.cn-beijing.volces.com/api/v3
                         model ids: doubao-seedance-2-0-260128, doubao-seedance-1-5-pro-251215, ...

Usage:
    py seedance.py "PROMPT" [--image PATH_OR_URL] [--output VIDEO.mp4] [options]

Examples:
    # text-to-video on BytePlus (default)
    py seedance.py "A golden retriever running through a sunlit wheat field, cinematic" \
        -o retriever.mp4

    # image-to-video, local image (sent as base64 data URI)
    py seedance.py "the cat slowly turns its head and blinks" \
        --image image_cat_001.png -o cat_motion.mp4

    # explicit model + portrait + longer
    py seedance.py "a neon city street at night, slow dolly forward" \
        --model seedance-2-0-260128 --ratio 9:16 --duration 8 -o city.mp4

    # Volcengine mainland (cn-beijing)
    py seedance.py "..." --base-url https://ark.cn-beijing.volces.com/api/v3 \
        --model doubao-seedance-2-0-260128 -o out.mp4

Stdlib only (urllib + json). No third-party dependencies.

Note: model id strings carry version date suffixes (e.g. -260128) that
BytePlus rotates over time. Confirm the current id from the model card in
your BytePlus / Volcengine console if a request 404s on the model.
"""
from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_BASE_URL = "https://ark.ap-southeast.bytepluses.com/api/v3"
DEFAULT_MODEL = "seedance-2-0-260128"

VALID_RESOLUTIONS = {"480p", "720p", "1080p", "2K"}
VALID_RATIOS = {"16:9", "9:16", "4:3", "3:4", "21:9", "1:1", "adaptive"}
TERMINAL_STATUSES = {"succeeded", "failed", "expired", "cancelled"}


def http_request(method: str, url: str, api_key: str,
                 body: dict | None = None, timeout: int = 60) -> dict:
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            payload = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", errors="replace")
        sys.exit(f"HTTP {e.code} from {method} {url}\n{detail}")
    except urllib.error.URLError as e:
        sys.exit(f"Network error talking to {url}: {e.reason}")
    if not payload:
        return {}
    return json.loads(payload)


def image_to_data_uri(path: Path) -> str:
    mime, _ = mimetypes.guess_type(path.name)
    if mime is None:
        mime = "image/png"
    b64 = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{b64}"


def build_content(prompt: str, image: str | None) -> list:
    content = [{"type": "text", "text": prompt}]
    if image:
        if image.startswith(("http://", "https://", "data:")):
            url = image
        else:
            p = Path(image).expanduser().resolve()
            if not p.is_file():
                sys.exit(f"ERROR: --image path not found: {p}")
            url = image_to_data_uri(p)
        content.append({"type": "image_url", "image_url": {"url": url}})
    return content


def create_task(base_url: str, api_key: str, prompt: str, image: str | None,
                model: str, resolution: str, ratio: str, duration: int,
                watermark: bool, generate_audio: bool) -> str:
    tasks_url = f"{base_url}/contents/generations/tasks"
    body: dict = {
        "model": model,
        "content": build_content(prompt, image),
        "resolution": resolution,
        "ratio": ratio,
        "duration": duration,
        "watermark": watermark,
    }
    if generate_audio:
        body["generate_audio"] = True

    print(f"[create] POST {tasks_url}")
    print(f"  model={model}  resolution={resolution}  ratio={ratio}  duration={duration}s")
    print(f"  mode={'image-to-video' if image else 'text-to-video'}")
    sys.stdout.flush()

    resp = http_request("POST", tasks_url, api_key, body=body)
    task_id = resp.get("id")
    if not task_id:
        sys.exit(f"ERROR: create-task response missing id: {resp}")
    print(f"  task_id={task_id}")
    return task_id


def poll_task(base_url: str, api_key: str, task_id: str, max_wait: int) -> dict:
    url = f"{base_url}/contents/generations/tasks/{task_id}"
    deadline = time.time() + max_wait
    delay = 4
    last_status = None
    print(f"[poll] GET {url}  (max_wait={max_wait}s)")
    while True:
        resp = http_request("GET", url, api_key)
        status = resp.get("status", "unknown")
        if status != last_status:
            print(f"  status={status}")
            last_status = status
        if status in TERMINAL_STATUSES:
            return resp
        if time.time() >= deadline:
            sys.exit(f"ERROR: task {task_id} did not finish within {max_wait}s "
                     f"(last status: {status})")
        time.sleep(delay)
        delay = min(delay + 2, 15)


def download(url: str, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    print(f"[download] {url}\n        -> {dest}")
    with urllib.request.urlopen(url, timeout=300) as resp, open(dest, "wb") as f:
        while True:
            chunk = resp.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
    print(f"  saved {dest.stat().st_size:,} bytes")


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Generate a video via ByteDance Seedance API (BytePlus or Volcengine).",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__.split("Usage:", 1)[1] if "Usage:" in __doc__ else "",
    )
    ap.add_argument("prompt", help="Text prompt describing the video / motion")
    ap.add_argument("--image", "-i", default=None,
                    help="Optional reference image (local path, http(s) URL, "
                         "or data URI) for image-to-video")
    ap.add_argument("--output", "-o", default=None,
                    help="Where to save the resulting MP4. If omitted, only "
                         "the video URL is printed.")
    ap.add_argument("--base-url", default=os.environ.get("ARK_BASE_URL", DEFAULT_BASE_URL),
                    help=f"Ark API base URL. Default: {DEFAULT_BASE_URL} "
                         f"(BytePlus international). For Volcengine mainland use "
                         f"https://ark.cn-beijing.volces.com/api/v3 . "
                         f"Can also be set via ARK_BASE_URL env var.")
    ap.add_argument("--model", default=DEFAULT_MODEL,
                    help=f"Model id (default: {DEFAULT_MODEL}). On Volcengine "
                         f"mainland prefix with doubao- (e.g. doubao-seedance-2-0-260128). "
                         f"Use the -fast variant id for cheaper/faster runs.")
    ap.add_argument("--resolution", default="1080p", choices=sorted(VALID_RESOLUTIONS))
    ap.add_argument("--ratio", default="16:9", choices=sorted(VALID_RATIOS))
    ap.add_argument("--duration", type=int, default=5,
                    help="Clip length in seconds (4-15, default 5).")
    ap.add_argument("--watermark", action="store_true",
                    help="Include the Doubao watermark (off by default).")
    ap.add_argument("--audio", action="store_true",
                    help="Request generate_audio=true (Seedance 2.0).")
    ap.add_argument("--max-wait", type=int, default=600,
                    help="Max seconds to poll before giving up (default 600).")
    ap.add_argument("--api-key", default=None,
                    help="Override ARK_API_KEY env var.")
    args = ap.parse_args()

    if not args.prompt.strip():
        sys.exit("ERROR: prompt cannot be empty")
    if not (4 <= args.duration <= 15):
        sys.exit("ERROR: --duration must be between 4 and 15")

    api_key = args.api_key or os.environ.get("ARK_API_KEY")
    if not api_key:
        sys.exit(
            "ERROR: ARK_API_KEY not set.\n"
            "  BytePlus (international): https://console.byteplus.com/ark\n"
            "  Volcengine mainland:      https://console.volcengine.com/ark\n"
            "  Then either:\n"
            '    PowerShell:  $env:ARK_API_KEY = "sk-..."\n'
            '    bash/zsh:    export ARK_API_KEY=sk-...\n'
            "  Or pass --api-key on the command line."
        )

    base_url = args.base_url.rstrip("/")
    task_id = create_task(
        base_url=base_url,
        api_key=api_key,
        prompt=args.prompt,
        image=args.image,
        model=args.model,
        resolution=args.resolution,
        ratio=args.ratio,
        duration=args.duration,
        watermark=args.watermark,
        generate_audio=args.audio,
    )

    final = poll_task(base_url, api_key, task_id, max_wait=args.max_wait)
    status = final.get("status")
    if status != "succeeded":
        print(json.dumps(final, indent=2, ensure_ascii=False))
        sys.exit(f"task ended with status={status}")

    video_url = (final.get("content") or {}).get("video_url")
    if not video_url:
        print(json.dumps(final, indent=2, ensure_ascii=False))
        sys.exit("ERROR: succeeded response had no content.video_url")

    usage = final.get("usage") or {}
    print(f"[done] status=succeeded  tokens={usage.get('total_tokens', '?')}")
    print(f"  video_url: {video_url}")

    if args.output:
        download(video_url, Path(args.output).expanduser().resolve())

    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
