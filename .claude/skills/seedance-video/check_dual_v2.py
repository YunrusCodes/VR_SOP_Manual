"""Detect dual-plume / dual-tip by clustering red-plume pixels spatially.

Rationale: the plume is a multi-strand red tassel that fragments into many
small connected components, so simple blob-counting fails. Instead, we
collapse red-plume pixels onto X-axis density, find the cluster centers,
and count distinct plume clusters separated by a non-trivial gap.

For dual-tip: Seedance's failure mode mirrors the whole spear, so a frame
with 2 plumes mirrored across the body centroid almost certainly also
has 2 tips. We report dual-tip == True iff dual-plume is detected AND
the two clusters are roughly equidistant from the body centroid.

Usage:
  py check_dual_v2.py <frames_dir> <out_json>
"""
import sys
import os
import json
import re
from collections import defaultdict
import numpy as np
from PIL import Image
from scipy.ndimage import label, binary_closing


# Tight plume color: bright crimson (excludes duller waist sash).
PLUME_H_MAX = 12
PLUME_H_MIN_HIGH = 348
PLUME_S_MIN = 190
PLUME_V_MIN = 130

# Magenta background filter (so we can compute character centroid).
BG_R_MIN, BG_G_MAX, BG_B_MIN = 200, 80, 200

# Min number of red pixels in an X-bucket to count as "plume present".
X_BUCKET_PX = 8           # bucket width in pixels
MIN_BUCKET_RED = 25       # red pixels in a bucket above which we consider it active
MIN_GAP_BUCKETS = 12      # contiguous empty buckets between clusters to call them distinct
MIN_CLUSTER_BUCKETS = 3   # cluster must span at least this many active buckets
EXCLUDE_BOTTOM = 0.78     # skip waist sash entirely


def rgb_to_hsv(rgb: np.ndarray) -> np.ndarray:
    arr = rgb.astype(np.float32) / 255.0
    r, g, b = arr[..., 0], arr[..., 1], arr[..., 2]
    maxc = np.max(arr, axis=-1)
    minc = np.min(arr, axis=-1)
    v = maxc
    delta = maxc - minc
    safe = np.where(delta > 0, delta, 1.0)
    s = np.where(maxc > 0, delta / np.where(maxc > 0, maxc, 1.0), 0.0)
    rc = (maxc - r) / safe
    gc = (maxc - g) / safe
    bc = (maxc - b) / safe
    h = np.where(maxc == r, bc - gc,
         np.where(maxc == g, 2.0 + rc - bc, 4.0 + gc - rc))
    h = (h / 6.0) % 1.0
    h = np.where(delta > 0, h, 0.0)
    return np.stack([h * 360.0, s * 255.0, v * 255.0], axis=-1)


def char_centroid_x(rgb: np.ndarray) -> float:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    bg = (r >= BG_R_MIN) & (g <= BG_G_MAX) & (b >= BG_B_MIN)
    char = ~bg
    ys, xs = np.where(char)
    if len(xs) == 0:
        return rgb.shape[1] / 2
    return float(xs.mean())


def cluster_red_x_axis(red_mask: np.ndarray) -> list[dict]:
    """Project red pixels onto X-axis, find clusters separated by gaps.

    Returns list of {x_center, x_min, x_max, pixel_count}.
    """
    H, W = red_mask.shape
    # bucket along X
    n_buckets = W // X_BUCKET_PX
    counts = np.zeros(n_buckets, dtype=int)
    col_sums = red_mask.sum(axis=0)  # per-column red pixel count
    for b in range(n_buckets):
        x0 = b * X_BUCKET_PX
        x1 = x0 + X_BUCKET_PX
        counts[b] = int(col_sums[x0:x1].sum())
    active = counts >= MIN_BUCKET_RED

    # find contiguous active runs separated by >= MIN_GAP_BUCKETS empty buckets
    clusters = []
    i = 0
    while i < n_buckets:
        if not active[i]:
            i += 1
            continue
        # start of a cluster
        start = i
        while i < n_buckets and active[i]:
            i += 1
        end = i  # exclusive
        if end - start < MIN_CLUSTER_BUCKETS:
            continue
        # peek ahead: if next active bucket is within MIN_GAP_BUCKETS, merge
        # actually: build cluster, then merge later
        clusters.append({"start": start, "end": end})

    # merge adjacent clusters whose gap is small
    merged = []
    for c in clusters:
        if merged and (c["start"] - merged[-1]["end"]) < MIN_GAP_BUCKETS:
            merged[-1]["end"] = c["end"]
        else:
            merged.append(dict(c))

    out = []
    for c in merged:
        x0 = c["start"] * X_BUCKET_PX
        x1 = c["end"] * X_BUCKET_PX
        px = int(counts[c["start"]:c["end"]].sum())
        out.append({"x_min": x0, "x_max": x1, "x_center": (x0 + x1) // 2, "px": px})
    return out


def measure_frame(path: str) -> dict:
    rgb = np.array(Image.open(path).convert("RGB"))
    H, W, _ = rgb.shape
    hsv = rgb_to_hsv(rgb)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    plume_mask = ((h <= PLUME_H_MAX) | (h >= PLUME_H_MIN_HIGH)) & (s >= PLUME_S_MIN) & (v >= PLUME_V_MIN)
    plume_mask[int(H * EXCLUDE_BOTTOM):, :] = False
    plume_mask = binary_closing(plume_mask, iterations=3)

    clusters = cluster_red_x_axis(plume_mask)
    n_plumes = len(clusters)

    # dual-tip proxy: 2 clusters roughly mirrored about character centroid
    cx = char_centroid_x(rgb)
    dual_tip = False
    if n_plumes == 2:
        a, b = clusters
        d_a = abs(a["x_center"] - cx)
        d_b = abs(b["x_center"] - cx)
        # require similar distance and on opposite sides
        if (a["x_center"] - cx) * (b["x_center"] - cx) < 0:
            ratio = min(d_a, d_b) / max(d_a, d_b)
            if ratio > 0.45:
                dual_tip = True
    return {
        "n_plumes": n_plumes,
        "dual_plume": n_plumes >= 2,
        "dual_tip_proxy": dual_tip,
        "clusters": clusters,
        "char_cx": cx,
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

    for path in paths:
        name = os.path.basename(path)
        m = pat.match(name)
        if not m:
            continue
        phase = m.group(1)
        idx = int(m.group(2))
        r = measure_frame(path)
        per_phase[phase].append({"idx": idx, "name": name, **r})

    summary = {}
    flagged_dual_plume = []
    flagged_dual_tip = []
    for phase, frames in per_phase.items():
        dp = [f for f in frames if f["dual_plume"]]
        dt = [f for f in frames if f["dual_tip_proxy"]]
        summary[phase] = {
            "n_frames": len(frames),
            "dual_plume_count": len(dp),
            "dual_plume_pct": round(100 * len(dp) / len(frames), 1),
            "dual_plume_first_idx": dp[0]["idx"] if dp else None,
            "dual_plume_last_idx": dp[-1]["idx"] if dp else None,
            "dual_tip_count": len(dt),
            "dual_tip_pct": round(100 * len(dt) / len(frames), 1),
            "dual_tip_first_idx": dt[0]["idx"] if dt else None,
            "dual_tip_last_idx": dt[-1]["idx"] if dt else None,
        }
        flagged_dual_plume.extend(f["name"] for f in dp)
        flagged_dual_tip.extend(f["name"] for f in dt)

    out = {
        "summary_per_phase": summary,
        "flagged_dual_plume_frames": flagged_dual_plume,
        "flagged_dual_tip_frames": flagged_dual_tip,
    }
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)

    print(f"\n{'phase':<8} {'frames':>6}  {'dual_plume':>14}  {'first..last':>14}  {'dual_tip':>11}  {'first..last':>14}")
    print("-" * 90)
    for phase, s in summary.items():
        dp_range = f"{s['dual_plume_first_idx']}..{s['dual_plume_last_idx']}" if s['dual_plume_first_idx'] else "-"
        dt_range = f"{s['dual_tip_first_idx']}..{s['dual_tip_last_idx']}" if s['dual_tip_first_idx'] else "-"
        print(f"{phase:<8} {s['n_frames']:>6}  "
              f"{s['dual_plume_count']:>4} ({s['dual_plume_pct']:>5.1f}%)  "
              f"{dp_range:>14}  "
              f"{s['dual_tip_count']:>4} ({s['dual_tip_pct']:>5.1f}%)  "
              f"{dt_range:>14}")
    print(f"\ntotal dual-plume frames: {len(flagged_dual_plume)}")
    print(f"total dual-tip frames:   {len(flagged_dual_tip)}")
    print(f"detail json: {out_json}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: check_dual_v2.py <frames_dir> <out_json>")
    main(sys.argv[1], sys.argv[2])
