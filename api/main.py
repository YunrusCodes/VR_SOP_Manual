"""
Inspection Quest 3 — Python FastAPI backend.

Original spec (docs/spec.md §6) covers the four read-only GET endpoints used by
the Quest client. The admin frontend in Phase 1 adds structured-JSON read/write
plus media management on top, keeping the CSV files as the on-disk source of
truth so the existing VR client keeps working unchanged.

啟動：
    uvicorn main:app --host 0.0.0.0 --port 8000 --reload
"""
from __future__ import annotations

import json
import mimetypes
import os
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path

from fastapi import FastAPI, File, HTTPException, Request, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, JSONResponse, PlainTextResponse, RedirectResponse
from fastapi.staticfiles import StaticFiles

from csv_io import parse_csv, to_csv
from schema import Course, CourseMeta, CreateCoursePayload

STORAGE = Path(os.environ.get("STORAGE_DIR", Path(__file__).parent / "storage")).resolve()

app = FastAPI(title="Inspection Quest 3 API", version="1.1")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET", "POST", "PUT", "DELETE"],
    allow_headers=["*"],
)


def _err(code: str, message: str) -> dict:
    return {"error": {"code": code, "message": message}}


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    detail = exc.detail
    if isinstance(detail, dict) and "error" in detail:
        return JSONResponse(status_code=exc.status_code, content=detail)
    return JSONResponse(
        status_code=exc.status_code,
        content=_err("http_error", str(detail)),
    )


def _company_dir(company: str) -> Path:
    p = (STORAGE / company).resolve()
    if not str(p).startswith(str(STORAGE)):
        raise HTTPException(400, _err("bad_path", "invalid company"))
    if not p.is_dir():
        raise HTTPException(404, _err("company_not_found", company))
    return p


def _course_dir(company: str, course: str) -> Path:
    base = _company_dir(company)
    p = (base / course).resolve()
    if not str(p).startswith(str(base)):
        raise HTTPException(400, _err("bad_path", "invalid course"))
    if not p.is_dir():
        raise HTTPException(404, _err("course_not_found", f"{company}/{course}"))
    return p


_SLUG_RE = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")


def _safe_slug(name: str) -> bool:
    return bool(_SLUG_RE.fullmatch(name))


def _safe_filename(name: str) -> bool:
    return bool(name) and "/" not in name and "\\" not in name and ".." not in name


def _timestamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def _read_display_name(course_dir: Path) -> str:
    meta_path = course_dir / "meta.json"
    if meta_path.exists():
        try:
            meta = json.loads(meta_path.read_text("utf-8"))
            return meta.get("displayName", course_dir.name)
        except json.JSONDecodeError:
            pass
    return course_dir.name


def _backup_course(course_dir: Path) -> Path:
    """Snapshot the CSV + meta into `_backups/{timestamp}/` so any structured
    write is reversible. Returns the backup directory path even if the source
    files don't exist yet (creation will land on first write)."""
    csv_path = course_dir / f"{course_dir.name}.csv"
    meta_path = course_dir / "meta.json"
    backup_dir = course_dir / "_backups" / _timestamp()
    backup_dir.mkdir(parents=True, exist_ok=True)
    if csv_path.exists():
        shutil.copy2(csv_path, backup_dir / csv_path.name)
    if meta_path.exists():
        shutil.copy2(meta_path, backup_dir / meta_path.name)
    return backup_dir


def _write_meta(course_dir: Path, display_name: str) -> None:
    (course_dir / "meta.json").write_text(
        json.dumps({"displayName": display_name}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def _trash_media(course_dir: Path, kind: str, filename: str, target: Path) -> Path:
    """Move a media file into the course's per-deletion trash bucket. Returns
    the path the file was moved to so callers can log it / surface to the UI."""
    trash_dir = course_dir / "_trash" / "media" / _timestamp() / ("Image" if kind == "image" else "Video")
    trash_dir.mkdir(parents=True, exist_ok=True)
    dest = trash_dir / filename
    shutil.move(str(target), str(dest))
    return dest


def _clear_media_refs(course_dir: Path, filename: str) -> int:
    """If the course's CSV references `filename` in its media column, clear
    those rows (write a backup first). Returns how many rows were cleared."""
    csv_path = course_dir / f"{course_dir.name}.csv"
    if not csv_path.exists():
        return 0
    course = parse_csv(
        csv_path.read_bytes().decode("utf-8-sig"),
        course_dir.name,
        _read_display_name(course_dir),
    )
    cleared = 0
    for step in course.steps:
        if step.media.filename == filename:
            step.media.kind = "none"
            step.media.filename = None
            cleared += 1
    if cleared > 0:
        _backup_course(course_dir)
        csv_path.write_text(to_csv(course), encoding="utf-8")
    return cleared


@app.get("/healthz")
def healthz():
    return {"status": "ok"}


@app.get("/companies")
def list_companies():
    if not STORAGE.is_dir():
        return {"companies": []}
    out = []
    for p in sorted(STORAGE.iterdir()):
        if not p.is_dir() or p.name.startswith("_"):
            continue  # skip _trash and similar internal dirs
        out.append(p.name)
    return {"companies": out}


@app.get("/companies/{company}/courses")
def list_courses(company: str):
    base = _company_dir(company)
    courses = []
    for d in sorted(p for p in base.iterdir() if p.is_dir()):
        csv_file = d / f"{d.name}.csv"
        if not csv_file.exists():
            continue
        display_name = d.name
        meta_path = d / "meta.json"
        if meta_path.exists():
            try:
                meta = json.loads(meta_path.read_text("utf-8"))
                display_name = meta.get("displayName", d.name)
            except json.JSONDecodeError:
                pass
        courses.append({"name": d.name, "displayName": display_name})
    return {"company": company, "courses": courses}


@app.get("/companies/{company}/courses/{course}/csv")
def get_csv(company: str, course: str):
    csv_path = _course_dir(company, course) / f"{course}.csv"
    if not csv_path.exists():
        raise HTTPException(404, _err("csv_not_found", f"{course}.csv"))
    return PlainTextResponse(
        csv_path.read_bytes().decode("utf-8-sig"),
        media_type="text/csv; charset=utf-8",
    )


@app.get("/companies/{company}/courses/{course}/files/{kind}/{filename}")
def get_file(company: str, course: str, kind: str, filename: str):
    if kind not in ("image", "video"):
        raise HTTPException(400, _err("bad_kind", f"kind must be 'image' or 'video', got '{kind}'"))
    if "/" in filename or "\\" in filename or ".." in filename:
        raise HTTPException(400, _err("bad_filename", filename))

    folder = _course_dir(company, course) / ("Image" if kind == "image" else "Video")
    f = (folder / filename).resolve()
    if not str(f).startswith(str(folder.resolve())):
        raise HTTPException(400, _err("bad_path", "invalid filename"))
    if not f.is_file():
        raise HTTPException(404, _err("file_not_found", filename))

    media_type, _enc = mimetypes.guess_type(f.name)
    return FileResponse(f, media_type=media_type or "application/octet-stream")


# ---------------------------------------------------------------------------
# Admin endpoints (Phase 1)
# ---------------------------------------------------------------------------


@app.get("/companies/{company}/courses/{course}/structured", response_model=Course)
def get_structured(company: str, course: str) -> Course:
    cdir = _course_dir(company, course)
    csv_path = cdir / f"{course}.csv"
    if not csv_path.exists():
        raise HTTPException(404, _err("csv_not_found", f"{course}.csv"))
    csv_text = csv_path.read_bytes().decode("utf-8-sig")
    return parse_csv(csv_text, course, _read_display_name(cdir))


@app.put("/companies/{company}/courses/{course}/structured")
def put_structured(company: str, course: str, payload: Course):
    cdir = _course_dir(company, course)
    if payload.name != course:
        raise HTTPException(400, _err("name_mismatch", "payload.name must equal URL course"))
    _backup_course(cdir)
    (cdir / f"{course}.csv").write_text(to_csv(payload), encoding="utf-8")
    _write_meta(cdir, payload.displayName)
    return {"ok": True}


@app.post("/companies/{company}/courses", status_code=201)
def create_course(company: str, payload: CreateCoursePayload):
    base = _company_dir(company)
    if not _safe_slug(payload.name):
        raise HTTPException(
            400,
            _err("bad_name", "name must be 1-63 chars, lower-case alphanumerics, '-' or '_'"),
        )
    course_dir = base / payload.name
    if course_dir.exists():
        raise HTTPException(409, _err("course_exists", payload.name))
    course_dir.mkdir(parents=True)
    (course_dir / "Image").mkdir()
    (course_dir / "Video").mkdir()
    empty = Course(
        name=payload.name,
        displayName=payload.displayName,
        introduction=payload.introduction,
        steps=[],
    )
    (course_dir / f"{payload.name}.csv").write_text(to_csv(empty), encoding="utf-8")
    _write_meta(course_dir, payload.displayName)
    return {"ok": True, "name": payload.name}


@app.put("/companies/{company}/courses/{course}/meta")
def update_meta(company: str, course: str, payload: CourseMeta):
    cdir = _course_dir(company, course)
    _backup_course(cdir)
    _write_meta(cdir, payload.displayName)
    return {"ok": True}


@app.delete("/companies/{company}/courses/{course}")
def delete_course(company: str, course: str):
    cdir = _course_dir(company, course)
    trash_dir = STORAGE / "_trash" / company
    trash_dir.mkdir(parents=True, exist_ok=True)
    target = trash_dir / f"{course}-{_timestamp()}"
    shutil.move(str(cdir), str(target))
    return {"ok": True, "trashed_to": str(target.relative_to(STORAGE))}


@app.get("/companies/{company}/courses/{course}/files/{kind}")
def list_files(company: str, course: str, kind: str):
    if kind not in ("image", "video"):
        raise HTTPException(400, _err("bad_kind", f"kind must be 'image' or 'video', got '{kind}'"))
    cdir = _course_dir(company, course)
    folder = cdir / ("Image" if kind == "image" else "Video")
    if not folder.is_dir():
        return {"files": []}
    return {"files": sorted(f.name for f in folder.iterdir() if f.is_file())}


@app.post("/companies/{company}/courses/{course}/files/{kind}", status_code=201)
async def upload_file(
    company: str,
    course: str,
    kind: str,
    file: UploadFile = File(...),
):
    if kind not in ("image", "video"):
        raise HTTPException(400, _err("bad_kind", f"kind must be 'image' or 'video', got '{kind}'"))
    cdir = _course_dir(company, course)
    if not _safe_filename(file.filename or ""):
        raise HTTPException(400, _err("bad_filename", file.filename or "(empty)"))
    folder = cdir / ("Image" if kind == "image" else "Video")
    folder.mkdir(exist_ok=True)
    target = folder / (file.filename or "")
    content = await file.read()
    target.write_bytes(content)
    return {"ok": True, "filename": file.filename, "size": len(content)}


@app.delete("/companies/{company}/courses/{course}/files/{kind}/{filename}")
def delete_file(company: str, course: str, kind: str, filename: str):
    if kind not in ("image", "video"):
        raise HTTPException(400, _err("bad_kind", f"kind must be 'image' or 'video', got '{kind}'"))
    if not _safe_filename(filename):
        raise HTTPException(400, _err("bad_filename", filename))
    cdir = _course_dir(company, course)
    folder = cdir / ("Image" if kind == "image" else "Video")
    target = (folder / filename).resolve()
    if not str(target).startswith(str(folder.resolve())):
        raise HTTPException(400, _err("bad_path", "invalid filename"))
    if not target.is_file():
        raise HTTPException(404, _err("file_not_found", filename))
    trashed_to = _trash_media(cdir, kind, filename, target)
    cleared = _clear_media_refs(cdir, filename)
    return {
        "ok": True,
        "trashed_to": str(trashed_to.relative_to(STORAGE)),
        "cleared_refs": cleared,
    }


# ---------------------------------------------------------------------------
# Admin SPA (built artifact mounted last so its catch-all doesn't shadow API)
# ---------------------------------------------------------------------------

ADMIN_DIST = Path(os.environ.get(
    "ADMIN_DIST_DIR",
    Path(__file__).parent / "static" / "admin",
)).resolve()
if ADMIN_DIST.is_dir():
    app.mount("/admin", StaticFiles(directory=ADMIN_DIST, html=True), name="admin")

    @app.get("/", include_in_schema=False)
    def _root_to_admin():
        return RedirectResponse(url="/admin/", status_code=307)
