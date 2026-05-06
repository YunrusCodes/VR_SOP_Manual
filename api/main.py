"""
Inspection Quest 3 — Python FastAPI backend.

依 docs/spec.md §6 設計。4 個 GET endpoints：
  - GET /healthz
  - GET /companies/{company}/courses
  - GET /companies/{company}/courses/{course}/csv
  - GET /companies/{company}/courses/{course}/files/{kind}/{filename}

啟動：
    uvicorn main:app --host 0.0.0.0 --port 8000 --reload
"""
from __future__ import annotations

import json
import mimetypes
import os
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, JSONResponse, PlainTextResponse

STORAGE = Path(os.environ.get("STORAGE_DIR", Path(__file__).parent / "storage")).resolve()

app = FastAPI(title="Inspection Quest 3 API", version="1.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET"],
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


@app.get("/healthz")
def healthz():
    return {"status": "ok"}


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
