"""Extract every frame of an MP4 to a directory of PNGs.

Usage:
  py extract_all_frames.py <input.mp4> <output_dir> <prefix>

Files are written as <output_dir>/<prefix>_frame_NNNN.png with 4-digit zero-padded
indices starting at 0001.
"""
import sys
import os
import imageio.v3 as iio


def main(video_path: str, out_dir: str, prefix: str) -> None:
    os.makedirs(out_dir, exist_ok=True)
    count = 0
    for idx, frame in enumerate(iio.imiter(video_path, plugin="pyav"), start=1):
        out_path = os.path.join(out_dir, f"{prefix}_frame_{idx:04d}.png")
        iio.imwrite(out_path, frame)
        count = idx
    print(f"{prefix}: wrote {count} frames to {out_dir}")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("usage: extract_all_frames.py <video.mp4> <out_dir> <prefix>")
    main(sys.argv[1], sys.argv[2], sys.argv[3])
