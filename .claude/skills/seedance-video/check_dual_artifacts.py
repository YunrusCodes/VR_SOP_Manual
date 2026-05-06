"""Detect dual-plume / dual-tip artifacts in Seedance-generated frames.

Two specific failure modes Seedance has on asymmetric long objects (spear):
  - dual plume:  red tassel rendered on BOTH ends of the spear instead of just the tip
  - dual tip:    metallic blade rendered on BOTH ends of the spear instead of just one

Approach: per-frame connected-component counts on tight color masks,
restricted to the spatial region where each artifact can plausibly appear.

Usage:
  py check_dual_artifacts.py <frames_dir> <out_json>
"""
import sys
import os
import json
import re
from collections import defaultdict
import numpy as np
from PIL import Image
from scipy.ndimage import label, binary_opening


# Plume threshold: very saturated red (the plume is bright crimson, sash is duller).
PLUME_H_MAX = 15
PLUME_H_MIN_HIGH = 345
PLUME_S_MIN = 180
PLUME_V_MIN = 110
PLUME_MIN_PX = 200
# exclude bottom 25% of frame to filter the waist sash (also red but lower body)
PLUME_BOTTOM_EXCLUDE = 0.75

# Tip threshold: bright + nearly-desaturated metallic (silver).
TIP_V_MIN = 220
TIP_S_MAX = 35
TIP_MIN_PX = 150
TIP_MAX_PX = 3500
TIP_MIN_ASPECT = 1.3   # spear tip is elongated leaf shape


def rgb_to_hsv(rgb: np.ndarray) -> np.ndarray:
    """Pure-numpy RGB->HSV. Returns H in [0,360), S/V in [0,255]."""
    arr = rgb.astype(np.float32) / 255.0
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    maxc = np.max(arr, axis=-1)
    minc = np.min(arr, axis=-1)
    v = maxc
    delta = maxc - minc
    safe_delta = np.where(delta > 0, delta, 1.0)
    s = np.where(maxc > 0, delta / np.where(maxc > 0, maxc, 1.0), 0.0)
    rc = (maxc - r) / safe_delta
    gc = (maxc - g) / safe_delta
    bc = (maxc - b) / safe_delta
    h = np.where(maxc == r, bc - gc,
         np.where(maxc == g, 2.0 + rc - bc, 4.0 + gc - rc))
    h = (h / 6.0) % 1.0
    h = np.where(delta > 0, h, 0.0)
    return np.stack([h * 360.0, s * 255.0, v * 255.0], axis=-1)


def filter_components(mask: np.ndarray, min_px: int, max_px: int = None,
                      min_aspect: float = 0.0) -> list[tuple]:
    if not mask.any():
        return []
    labeled, n = label(mask)
    if n == 0:
        return []
    sizes = np.bincount(labeled.ravel())
    keep = []
    for lab_id in range(1, n + 1):
        sz = int(sizes[lab_id])
        if sz < min_px:
            continue
        if max_px is not None and sz > max_px:
            continue
        ys, xs = np.where(labeled == lab_id)
        x0, x1 = int(xs.min()), int(xs.max())
        y0, y1 = int(ys.min()), int(ys.max())
        bw = max(1, x1 - x0)
        bh = max(1, y1 - y0)
        aspect = max(bw, bh) / min(bw, bh)
        if aspect < min_aspect:
            continue
        keep.append({"size": sz, "bbox": [x0, y0, x1, y1], "aspect": round(aspect, 2)})
    return keep


def detect_plumes(hsv: np.ndarray) -> list[dict]:
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    mask = ((h <= PLUME_H_MAX) | (h >= PLUME_H_MIN_HIGH)) & (s >= PLUME_S_MIN) & (v >= PLUME_V_MIN)
    H = mask.shape[0]
    mask[int(H * PLUME_BOTTOM_EXCLUDE):, :] = False
    mask = binary_opening(mask, iterations=1)
    return filter_components(mask, min_px=PLUME_MIN_PX)


def detect_tips(hsv: np.ndarray) -> list[dict]:
    s, v = hsv[..., 1], hsv[..., 2]
    mask = (v >= TIP_V_MIN) & (s <= TIP_S_MAX)
    mask = binary_opening(mask, iterations=1)
    return filter_components(mask, min_px=TIP_MIN_PX, max_px=TIP_MAX_PX,
                             min_aspect=TIP_MIN_ASPECT)


def measure_frame(path: str) -> dict:
    rgb = np.array(Image.open(path).convert("RGB"))
    hsv = rgb_to_hsv(rgb)
    plumes = detect_plumes(hsv)
    tips = detect_tips(hsv)
    return {
        "n_plumes": len(plumes),
        "n_tips": len(tips),
        "plumes": plumes,
        "tips": tips,
    }


def main(frames_dir: str, out_json: str) -> None:
    paths = sorted(
        os.path.join(frames_dir, f)
        for f in os.listdir(frames_dir)
        if f.lower().endswith(".png") and "_frame_" in f
    )
    if not paths:
        raise SystemExit(f"no frames in {frames_dir}")

    pat = re.compile(r"^(.*?)_frame_(\d+)\.png$")
    per_phase: dict[str, list[dict]] = defaultdict(list)
    flagged_double_plume: list[str] = []
    flagged_double_tip: list[str] = []

    for path in paths:
        name = os.path.basename(path)
        m = pat.match(name)
        if not m:
            continue
        phase = m.group(1)
        idx = int(m.group(2))
        result = measure_frame(path)
        per_phase[phase].append({"idx": idx, **result})
        if result["n_plumes"] >= 2:
            flagged_double_plume.append(name)
        if result["n_tips"] >= 2:
            flagged_double_tip.append(name)

    summary = {}
    for phase, frames in per_phase.items():
        plume_counts = [f["n_plumes"] for f in frames]
        tip_counts = [f["n_tips"] for f in frames]
        plume_dual_frames = [f["idx"] for f in frames if f["n_plumes"] >= 2]
        tip_dual_frames = [f["idx"] for f in frames if f["n_tips"] >= 2]
        summary[phase] = {
            "n_frames": len(frames),
            "plume": {
                "min": min(plume_counts), "max": max(plume_counts),
                "median": int(np.median(plume_counts)),
                "dual_frame_count": len(plume_dual_frames),
                "dual_frame_pct": round(100 * len(plume_dual_frames) / len(frames), 1),
                "first_dual_idx": plume_dual_frames[0] if plume_dual_frames else None,
                "last_dual_idx": plume_dual_frames[-1] if plume_dual_frames else None,
            },
            "tip": {
                "min": min(tip_counts), "max": max(tip_counts),
                "median": int(np.median(tip_counts)),
                "dual_frame_count": len(tip_dual_frames),
                "dual_frame_pct": round(100 * len(tip_dual_frames) / len(frames), 1),
                "first_dual_idx": tip_dual_frames[0] if tip_dual_frames else None,
                "last_dual_idx": tip_dual_frames[-1] if tip_dual_frames else None,
            },
        }

    out = {
        "thresholds": {
            "plume": {"H_low<=": PLUME_H_MAX, "H_high>=": PLUME_H_MIN_HIGH,
                      "S>=": PLUME_S_MIN, "V>=": PLUME_V_MIN,
                      "min_px": PLUME_MIN_PX,
                      "exclude_bottom_pct": (1 - PLUME_BOTTOM_EXCLUDE) * 100},
            "tip": {"V>=": TIP_V_MIN, "S<=": TIP_S_MAX,
                    "min_px": TIP_MIN_PX, "max_px": TIP_MAX_PX,
                    "min_aspect": TIP_MIN_ASPECT},
        },
        "summary_per_phase": summary,
        "flagged_double_plume_frames": flagged_double_plume,
        "flagged_double_tip_frames": flagged_double_tip,
    }
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)

    print(f"\n{'phase':<8} {'frames':>6}  {'plume max/med':>14}  {'plume dual%':>11}  {'tip max/med':>12}  {'tip dual%':>9}")
    print("-" * 80)
    for phase, s in summary.items():
        print(f"{phase:<8} {s['n_frames']:>6}  "
              f"{s['plume']['max']:>5}/{s['plume']['median']:<4}    "
              f"{s['plume']['dual_frame_pct']:>9.1f}%  "
              f"{s['tip']['max']:>4}/{s['tip']['median']:<4}    "
              f"{s['tip']['dual_frame_pct']:>7.1f}%")
    print(f"\ntotal frames flagged for dual plume: {len(flagged_double_plume)}")
    print(f"total frames flagged for dual tip:   {len(flagged_double_tip)}")
    print(f"detail json: {out_json}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: check_dual_artifacts.py <frames_dir> <out_json>")
    main(sys.argv[1], sys.argv[2])
