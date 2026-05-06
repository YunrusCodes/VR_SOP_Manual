"""Build a sprite sheet PNG and a preview GIF from a folder of numbered frames.

Usage:
  py stitch_sequence.py <frames_dir> <out_dir> <name>

Reads all *.png files in <frames_dir>, sorts by name, and produces:
  <out_dir>/<name>_sheet.png      sprite sheet, sub-sampled to ~60 cells, 240x240/cell
  <out_dir>/<name>_preview.gif    animated GIF, sub-sampled to ~120 frames at 24fps
"""
import sys
import os
import math
from PIL import Image

CELL = 240
SHEET_TARGET_CELLS = 60
GIF_TARGET_FRAMES = 120
GIF_FPS = 24
GIF_SCALE = 240


def load_frames(frames_dir: str) -> list[str]:
    paths = sorted(
        os.path.join(frames_dir, f)
        for f in os.listdir(frames_dir)
        if f.lower().endswith(".png") and "frame_" in f
    )
    if not paths:
        raise SystemExit(f"no frames found in {frames_dir}")
    return paths


def subsample(paths: list[str], target: int) -> list[str]:
    if len(paths) <= target:
        return paths
    step = len(paths) / target
    return [paths[int(i * step)] for i in range(target)]


def build_sheet(paths: list[str], out_path: str) -> None:
    cells = subsample(paths, SHEET_TARGET_CELLS)
    n = len(cells)
    cols = 10
    rows = math.ceil(n / cols)
    sheet = Image.new("RGBA", (cols * CELL, rows * CELL), (0, 0, 0, 0))
    for i, p in enumerate(cells):
        img = Image.open(p).convert("RGBA").resize((CELL, CELL), Image.LANCZOS)
        x = (i % cols) * CELL
        y = (i // cols) * CELL
        sheet.paste(img, (x, y))
    sheet.save(out_path, optimize=True)
    print(f"sheet: {n} cells, {cols}x{rows} grid, {sheet.size[0]}x{sheet.size[1]} -> {out_path}")


def build_gif(paths: list[str], out_path: str) -> None:
    frames_paths = subsample(paths, GIF_TARGET_FRAMES)
    frames = [
        Image.open(p).convert("RGB").resize((GIF_SCALE, GIF_SCALE), Image.LANCZOS).convert(
            "P", palette=Image.ADAPTIVE, colors=128
        )
        for p in frames_paths
    ]
    duration_ms = int(1000 / GIF_FPS)
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        optimize=True,
        disposal=2,
    )
    size_mb = os.path.getsize(out_path) / (1024 * 1024)
    print(f"gif: {len(frames)} frames @ {GIF_FPS}fps, {GIF_SCALE}x{GIF_SCALE}, {size_mb:.2f} MB -> {out_path}")


def main(frames_dir: str, out_dir: str, name: str) -> None:
    os.makedirs(out_dir, exist_ok=True)
    paths = load_frames(frames_dir)
    print(f"loaded {len(paths)} frames")
    build_sheet(paths, os.path.join(out_dir, f"{name}_sheet.png"))
    build_gif(paths, os.path.join(out_dir, f"{name}_preview.gif"))


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("usage: stitch_sequence.py <frames_dir> <out_dir> <name>")
    main(sys.argv[1], sys.argv[2], sys.argv[3])
