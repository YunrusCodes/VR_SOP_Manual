"""Extract the last frame of an MP4 to a PNG using imageio + bundled ffmpeg.

Usage:
  py extract_last_frame.py <input.mp4> <output.png>
"""
import sys
import imageio.v3 as iio


def main(video_path: str, out_path: str) -> None:
    last = None
    for frame in iio.imiter(video_path, plugin="pyav"):
        last = frame
    if last is None:
        raise SystemExit(f"no frames decoded from {video_path}")
    iio.imwrite(out_path, last)
    print(f"wrote {out_path}  shape={last.shape}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: extract_last_frame.py <video.mp4> <out.png>")
    main(sys.argv[1], sys.argv[2])
