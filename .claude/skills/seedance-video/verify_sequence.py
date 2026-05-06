"""Verify cross-phase consistency of a chained Seedance sequence.

Measures per-frame character bbox, centroid, and red-plume area on a uniform
magenta background, then reports drift at each phase transition.

Usage:
  py verify_sequence.py <frames_dir> <out_json> [--phases N]

Assumes frames are named <prefix>_frame_NNNN.png with prefix grouped per phase
(e.g. phase1_frame_0001.png ... phase1_frame_0097.png, phase2_frame_0001.png ...).
The script auto-detects phases by prefix.
"""
import sys
import os
import json
import re
from collections import defaultdict
import numpy as np
from PIL import Image

# Magenta background: roughly RGB (220-255, 0-60, 220-255). We treat anything
# not magenta as character pixels.
BG_R_MIN, BG_R_MAX = 200, 255
BG_G_MAX = 80
BG_B_MIN, BG_B_MAX = 200, 255

# Red plume: high R, low G, low B. Tighter than background red.
RED_R_MIN = 140
RED_G_MAX = 80
RED_B_MAX = 80


def is_background(rgb: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r >= BG_R_MIN) & (r <= BG_R_MAX) & (g <= BG_G_MAX) & (b >= BG_B_MIN) & (b <= BG_B_MAX)


def is_red(rgb: np.ndarray) -> np.ndarray:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r >= RED_R_MIN) & (g <= RED_G_MAX) & (b <= RED_B_MAX)


def measure_frame(path: str) -> dict:
    img = np.array(Image.open(path).convert("RGB"))
    h, w, _ = img.shape
    char_mask = ~is_background(img)
    red_mask = is_red(img) & char_mask
    ys, xs = np.where(char_mask)
    if len(xs) == 0:
        return {"empty": True}
    cx = float(xs.mean())
    cy = float(ys.mean())
    x0, x1 = int(xs.min()), int(xs.max())
    y0, y1 = int(ys.min()), int(ys.max())
    bbox_w = x1 - x0
    bbox_h = y1 - y0
    char_area = int(char_mask.sum())
    red_area = int(red_mask.sum())
    # Connected red components (rough plume count): use a cheap labeling via
    # scipy if available, otherwise just report area.
    red_components = count_components(red_mask)
    return {
        "cx_norm": cx / w,
        "cy_norm": cy / h,
        "bbox_w_norm": bbox_w / w,
        "bbox_h_norm": bbox_h / h,
        "char_area_norm": char_area / (w * h),
        "red_area_norm": red_area / (w * h),
        "red_components": red_components,
    }


def count_components(mask: np.ndarray) -> int:
    """Count connected red blobs above a min-size threshold (filters noise)."""
    if not mask.any():
        return 0
    try:
        from scipy.ndimage import label
        labeled, n = label(mask)
        sizes = np.bincount(labeled.ravel())
        # ignore background (label 0); count components with > 200 px
        return int((sizes[1:] > 200).sum())
    except ImportError:
        # crude fallback: split mask in half horizontally; if both halves have
        # >200 red px, we likely have 2 plumes
        h, w = mask.shape
        left = int(mask[:, : w // 2].sum())
        right = int(mask[:, w // 2:].sum())
        return int(left > 200) + int(right > 200)


def group_by_phase(paths: list[str]) -> dict[str, list[str]]:
    groups: dict[str, list[str]] = defaultdict(list)
    pat = re.compile(r"^(.*?)_frame_\d+\.png$")
    for p in paths:
        name = os.path.basename(p)
        m = pat.match(name)
        if not m:
            continue
        groups[m.group(1)].append(p)
    for k in groups:
        groups[k].sort()
    return dict(sorted(groups.items()))


def main(frames_dir: str, out_json: str) -> None:
    paths = sorted(
        os.path.join(frames_dir, f)
        for f in os.listdir(frames_dir)
        if f.lower().endswith(".png") and "_frame_" in f
    )
    if not paths:
        raise SystemExit(f"no frames in {frames_dir}")
    phases = group_by_phase(paths)
    print(f"detected {len(phases)} phases: {list(phases.keys())}")

    per_phase = {}
    for name, ps in phases.items():
        first = measure_frame(ps[0])
        last = measure_frame(ps[-1])
        # also sample mid frame
        mid = measure_frame(ps[len(ps) // 2])
        per_phase[name] = {
            "frame_count": len(ps),
            "first": first,
            "mid": mid,
            "last": last,
        }
        print(
            f"{name}: {len(ps)}f  cx={first['cx_norm']:.3f}->{last['cx_norm']:.3f}  "
            f"bbox_h={first['bbox_h_norm']:.3f}->{last['bbox_h_norm']:.3f}  "
            f"red_comp(first/mid/last)={first['red_components']}/{mid['red_components']}/{last['red_components']}"
        )

    transitions = []
    phase_names = list(phases.keys())
    for i in range(len(phase_names) - 1):
        a_name, b_name = phase_names[i], phase_names[i + 1]
        a_last = per_phase[a_name]["last"]
        b_first = per_phase[b_name]["first"]
        delta = {
            "from": a_name,
            "to": b_name,
            "dcx": b_first["cx_norm"] - a_last["cx_norm"],
            "dcy": b_first["cy_norm"] - a_last["cy_norm"],
            "dbbox_h": b_first["bbox_h_norm"] - a_last["bbox_h_norm"],
            "dbbox_w": b_first["bbox_w_norm"] - a_last["bbox_w_norm"],
            "dchar_area": b_first["char_area_norm"] - a_last["char_area_norm"],
            "dred_area": b_first["red_area_norm"] - a_last["red_area_norm"],
            "red_comp_from": a_last["red_components"],
            "red_comp_to": b_first["red_components"],
        }
        transitions.append(delta)
        print(
            f"  transition {a_name} -> {b_name}: "
            f"dcx={delta['dcx']:+.3f}  dcy={delta['dcy']:+.3f}  "
            f"dbbox_h={delta['dbbox_h']:+.3f}  dchar_area={delta['dchar_area']:+.3f}  "
            f"red_comp {delta['red_comp_from']}->{delta['red_comp_to']}"
        )

    # acceptance thresholds (normalized)
    thresholds = {
        "max_abs_dcx": 0.05,
        "max_abs_dbbox_h": 0.06,
        "max_abs_dchar_area": 0.04,
        "expected_red_comp": 1,
    }
    verdict = {"pass": True, "issues": []}
    for t in transitions:
        for key, limit in [("dcx", thresholds["max_abs_dcx"]),
                           ("dbbox_h", thresholds["max_abs_dbbox_h"]),
                           ("dchar_area", thresholds["max_abs_dchar_area"])]:
            if abs(t[key]) > limit:
                verdict["pass"] = False
                verdict["issues"].append(
                    f"{t['from']}->{t['to']}: |{key}|={abs(t[key]):.3f} > {limit}"
                )
        if t["red_comp_to"] != thresholds["expected_red_comp"]:
            verdict["pass"] = False
            verdict["issues"].append(
                f"{t['from']}->{t['to']}: red_comp at next-phase first frame = "
                f"{t['red_comp_to']} (expected 1)"
            )

    out = {
        "thresholds": thresholds,
        "phases": per_phase,
        "transitions": transitions,
        "verdict": verdict,
    }
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
    print(f"\nverdict: {'PASS' if verdict['pass'] else 'FAIL'}")
    for issue in verdict["issues"]:
        print(f"  - {issue}")
    print(f"wrote {out_json}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: verify_sequence.py <frames_dir> <out_json>")
    main(sys.argv[1], sys.argv[2])
