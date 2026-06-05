"""CSV ↔ structured-Course conversion.

The on-disk CSV layout is:
  row 0: single cell of introduction text
  row 1: 13-column header
  rows 2..: step data, in declared step order

Action columns hold either:
  - an integer step number → ExceptionAction(type='goToStep', step=N)
  - any other non-empty text → ExceptionAction(type='showMessage', text=...)
  - empty → no exception in that slot
"""
from __future__ import annotations

import csv
from io import StringIO
from typing import Optional

from schema import Course, ExceptionAction, ExceptionOption, Media, Step

IMAGE_EXTS = (".jpg", ".jpeg", ".png", ".webp", ".gif")
VIDEO_EXTS = (".mp4", ".mov", ".webm", ".m4v")

CSV_HEADER = [
    "Step Order",
    "Main Title",
    "Sub Title",
    "Step Name",
    "Description",
    "Video Or Image",
    "Next Step Indication",
    "Exception Handling Option 1",
    "Action After Selecting Handling Option 1",
    "Exception Handling Option 2",
    "Action After Selecting Handling Option 2",
    "Exception Handling Option 3",
    "Action After Selecting Handling Option 3",
]

NUM_COLS = len(CSV_HEADER)
EXCEPTION_COL_PAIRS = ((7, 8), (9, 10), (11, 12))


def detect_media(filename: str) -> Media:
    if not filename:
        return Media(kind="none")
    low = filename.lower()
    if low.endswith(IMAGE_EXTS):
        return Media(kind="image", filename=filename)
    if low.endswith(VIDEO_EXTS):
        return Media(kind="video", filename=filename)
    # Unknown extension — assume image so the file is still served.
    return Media(kind="image", filename=filename)


def parse_action(raw: str) -> Optional[ExceptionAction]:
    raw = raw.strip()
    if not raw:
        return None
    try:
        return ExceptionAction(type="goToStep", step=int(raw))
    except ValueError:
        return ExceptionAction(type="showMessage", text=raw)


def parse_csv(csv_text: str, course_name: str, display_name: str) -> Course:
    rows = list(csv.reader(StringIO(csv_text)))
    introduction = ""
    if rows and rows[0]:
        introduction = rows[0][0]
    steps: list[Step] = []
    for row in rows[2:]:
        if not row or not row[0].strip():
            continue
        padded = list(row) + [""] * max(0, NUM_COLS - len(row))
        try:
            order = int(padded[0].strip())
        except ValueError:
            continue
        media = detect_media(padded[5].strip())
        exceptions: list[ExceptionOption] = []
        for label_col, action_col in EXCEPTION_COL_PAIRS:
            label = padded[label_col].strip()
            action = parse_action(padded[action_col])
            if not label or action is None:
                continue
            exceptions.append(ExceptionOption(label=label, action=action))
        steps.append(
            Step(
                order=order,
                mainTitle=padded[1].strip(),
                subTitle=padded[2].strip(),
                name=padded[3].strip(),
                description=padded[4],
                media=media,
                nextStepIndication=padded[6].strip(),
                exceptions=exceptions,
            )
        )
    return Course(
        name=course_name,
        displayName=display_name,
        introduction=introduction,
        steps=steps,
    )


def to_csv(course: Course) -> str:
    out = StringIO()
    writer = csv.writer(out, quoting=csv.QUOTE_MINIMAL, lineterminator="\n")
    writer.writerow([course.introduction])
    writer.writerow(CSV_HEADER)
    for step in course.steps:
        row: list[str] = [
            str(step.order),
            step.mainTitle,
            step.subTitle,
            step.name,
            step.description,
            step.media.filename or "",
            step.nextStepIndication,
        ]
        for i in range(3):
            if i < len(step.exceptions):
                ex = step.exceptions[i]
                row.append(ex.label)
                if ex.action.type == "goToStep":
                    row.append(str(ex.action.step) if ex.action.step is not None else "")
                else:
                    row.append(ex.action.text or "")
            else:
                row.append("")
                row.append("")
        writer.writerow(row)
    return out.getvalue()
