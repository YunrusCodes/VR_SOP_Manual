"""Debug: overlay plume / tip masks on a frame so we can see what they catch."""
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import label, binary_opening, binary_closing


def rgb_to_hsv(rgb):
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


def main(in_path, out_path):
    rgb = np.array(Image.open(in_path).convert("RGB"))
    hsv = rgb_to_hsv(rgb)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]

    # raw plume candidates (no closing yet)
    plume_raw = ((h <= 15) | (h >= 345)) & (s >= 180) & (v >= 110)
    H = plume_raw.shape[0]
    plume_raw[int(H * 0.75):, :] = False

    # closing kernel ~21px should merge tassel strands
    plume_closed = binary_closing(plume_raw, iterations=10)
    plume_closed = binary_opening(plume_closed, iterations=2)

    # tip candidates: bright + desaturated
    tip_raw = (v >= 240) & (s <= 25)
    tip_closed = binary_closing(tip_raw, iterations=2)

    overlay = rgb.copy()
    # plume_raw -> green tint
    overlay[plume_raw] = [0, 255, 0]
    # plume_closed (merged) -> magenta outline only (where closed but not raw)
    closed_only = plume_closed & ~plume_raw
    overlay[closed_only] = [255, 0, 255]
    # tip -> cyan
    overlay[tip_raw] = [0, 255, 255]

    Image.fromarray(overlay).save(out_path)

    # count blobs after closing
    plume_lbl, plume_n = label(plume_closed)
    plume_sizes = np.bincount(plume_lbl.ravel())[1:] if plume_n else []
    plume_big = [int(sz) for sz in plume_sizes if sz >= 500]

    tip_lbl, tip_n = label(tip_closed)
    tip_sizes = np.bincount(tip_lbl.ravel())[1:] if tip_n else []
    tip_kept = []
    for i, sz in enumerate(tip_sizes, 1):
        if not (200 <= sz <= 3500):
            continue
        ys, xs = np.where(tip_lbl == i)
        bw = max(1, xs.max() - xs.min())
        bh = max(1, ys.max() - ys.min())
        ar = max(bw, bh) / min(bw, bh)
        if ar < 1.3:
            continue
        tip_kept.append((int(sz), round(ar, 2), (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))))

    print(f"plume after closing: {plume_n} blobs total, {len(plume_big)} >= 500px (sizes: {plume_big})")
    print(f"tip   after closing: {tip_n} blobs total, {len(tip_kept)} kept (size+aspect filter)")
    for sz, ar, bb in tip_kept:
        print(f"  tip blob: size={sz} aspect={ar} bbox={bb}")
    print(f"overlay saved: {out_path}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
