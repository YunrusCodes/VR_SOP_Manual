"""
seed.py — 為 storage/ 目錄下的每門課程生成 placeholder jpg/mp4 檔。

讀取每個 `storage/{company}/{course}/{course}.csv` 的第 6 欄（Video Or Image），
找出所有 .jpg / .mp4 檔名；若對應的 Image/ 或 Video/ 子目錄下不存在該檔，就生成 placeholder：
  - jpg：1×1 像素灰色（用 Pillow）
  - mp4：5 秒、640×360、25fps、灰色純色（用 ffmpeg + faststart 確保可 seek）

需要環境：
    pip install Pillow
    ffmpeg 須能從 PATH 找到（Windows 可裝 winget install ffmpeg；macOS 用 brew）

用法：
    python seed.py                       # 預設用 ./storage
    python seed.py --storage ./api/storage
    python seed.py --force               # 已存在也覆蓋重生

放置位置：
    開發階段建議放在新專案的 api/seed.py，與 api/storage/ 並排。
"""

from __future__ import annotations

import argparse
import csv
import shutil
import subprocess
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("缺少 Pillow，請先 `pip install Pillow`。")


def find_media_in_csv(csv_path: Path) -> tuple[set[str], set[str]]:
    """從 CSV 第 6 欄抽出所有 .jpg / .mp4 檔名。回傳 (images, videos)。"""
    images: set[str] = set()
    videos: set[str] = set()
    with csv_path.open("r", encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))
    # rows[0] 是介紹文，rows[1] 是表頭，rows[2:] 才是步驟資料
    for row in rows[2:]:
        if len(row) < 6:
            continue
        media = row[5].strip()
        if not media:
            continue
        ext = media.rsplit(".", 1)[-1].lower()
        if ext in ("jpg", "jpeg"):
            images.add(media)
        elif ext == "mp4":
            videos.add(media)
        else:
            print(f"  ! 未知副檔名（忽略）：{media}")
    return images, videos


def make_placeholder_jpg(target: Path) -> None:
    img = Image.new("RGB", (1, 1), color=(200, 200, 200))
    img.save(target, "JPEG")


def make_placeholder_mp4(target: Path) -> None:
    if not shutil.which("ffmpeg"):
        raise SystemExit("ffmpeg 不在 PATH 中，請先安裝 ffmpeg。")
    cmd = [
        "ffmpeg", "-y",
        "-f", "lavfi",
        "-i", "color=c=gray:s=640x360:r=25:d=5",
        "-c:v", "libx264",
        "-preset", "ultrafast",
        "-pix_fmt", "yuv420p",
        "-movflags", "+faststart",   # 讓影片支援 HTTP Range / seek
        str(target),
    ]
    result = subprocess.run(cmd, capture_output=True)
    if result.returncode != 0:
        raise SystemExit(f"ffmpeg 失敗：{result.stderr.decode('utf-8', 'replace')}")


def seed_course(course_dir: Path, force: bool) -> tuple[int, int, int, int]:
    """回傳 (created_jpg, created_mp4, skipped_jpg, skipped_mp4)。"""
    csv_path = course_dir / f"{course_dir.name}.csv"
    if not csv_path.exists():
        return 0, 0, 0, 0

    images, videos = find_media_in_csv(csv_path)
    print(f"\n{course_dir.name}：CSV 引用了 {len(images)} 張圖、{len(videos)} 個影片")

    created_jpg = created_mp4 = skipped_jpg = skipped_mp4 = 0

    if images:
        (course_dir / "Image").mkdir(exist_ok=True)
        for name in sorted(images):
            target = course_dir / "Image" / name
            if target.exists() and not force:
                skipped_jpg += 1
                continue
            make_placeholder_jpg(target)
            created_jpg += 1
            print(f"  + Image/{name}")

    if videos:
        (course_dir / "Video").mkdir(exist_ok=True)
        for name in sorted(videos):
            target = course_dir / "Video" / name
            if target.exists() and not force:
                skipped_mp4 += 1
                continue
            make_placeholder_mp4(target)
            created_mp4 += 1
            print(f"  + Video/{name}")

    return created_jpg, created_mp4, skipped_jpg, skipped_mp4


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--storage", default="./storage", help="storage 根目錄（預設 ./storage）")
    parser.add_argument("--force", action="store_true", help="即使檔案已存在也重新生成")
    args = parser.parse_args()

    storage = Path(args.storage).resolve()
    if not storage.is_dir():
        sys.exit(f"找不到 storage 目錄：{storage}")

    print(f"掃描 {storage}")

    totals = [0, 0, 0, 0]   # cj, cm, sj, sm
    for company_dir in sorted(p for p in storage.iterdir() if p.is_dir()):
        for course_dir in sorted(p for p in company_dir.iterdir() if p.is_dir()):
            cj, cm, sj, sm = seed_course(course_dir, args.force)
            totals[0] += cj
            totals[1] += cm
            totals[2] += sj
            totals[3] += sm

    print(
        f"\n完成：建立 {totals[0]} jpg、{totals[1]} mp4；"
        f"跳過 {totals[2]} jpg、{totals[3]} mp4（已存在；--force 可覆蓋）。"
    )


if __name__ == "__main__":
    main()
