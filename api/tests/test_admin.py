"""Tests for the admin/structured endpoints added in Phase 1."""
from __future__ import annotations

import io
import json
from pathlib import Path

from fastapi.testclient import TestClient


def test_get_structured_basic(client: TestClient):
    r = client.get("/companies/edu/courses/solar-system/structured")
    assert r.status_code == 200
    body = r.json()
    assert body["name"] == "solar-system"
    assert body["displayName"] == "太陽系巡禮"
    assert body["introduction"].startswith("歡迎來到")
    assert len(body["steps"]) >= 6
    step1 = body["steps"][0]
    assert step1["order"] == 1
    assert step1["name"] == "太陽"
    assert step1["media"]["kind"] == "image"
    assert step1["media"]["filename"] == "sun.jpg"


def test_get_structured_exception_action_types(client: TestClient):
    """Step 7 has a goToStep action (=3); step 6 has showMessage actions."""
    body = client.get("/companies/edu/courses/solar-system/structured").json()
    by_order = {s["order"]: s for s in body["steps"]}

    moon = by_order[7]
    assert any(
        ex["action"]["type"] == "goToStep" and ex["action"]["step"] == 3
        for ex in moon["exceptions"]
    )

    spacecraft = by_order[6]
    assert any(
        ex["action"]["type"] == "showMessage" and ex["action"]["text"]
        for ex in spacecraft["exceptions"]
    )


def test_put_structured_writes_csv_and_meta(client: TestClient, storage_dir: Path):
    body = client.get("/companies/edu/courses/solar-system/structured").json()
    body["displayName"] = "太陽系巡禮(修訂)"
    body["steps"][0]["name"] = "太陽（恆星）"
    r = client.put("/companies/edu/courses/solar-system/structured", json=body)
    assert r.status_code == 200

    csv_path = storage_dir / "edu" / "solar-system" / "solar-system.csv"
    csv_text = csv_path.read_text("utf-8")
    assert "太陽（恆星）" in csv_text

    meta = json.loads((storage_dir / "edu" / "solar-system" / "meta.json").read_text("utf-8"))
    assert meta["displayName"] == "太陽系巡禮(修訂)"


def test_put_structured_creates_backup(client: TestClient, storage_dir: Path):
    body = client.get("/companies/edu/courses/solar-system/structured").json()
    client.put("/companies/edu/courses/solar-system/structured", json=body)
    backups = list((storage_dir / "edu" / "solar-system" / "_backups").iterdir())
    assert backups, "expected at least one backup folder"
    snapshot = backups[-1]
    assert (snapshot / "solar-system.csv").exists()


def test_put_structured_name_mismatch_rejected(client: TestClient):
    body = client.get("/companies/edu/courses/solar-system/structured").json()
    body["name"] = "different-name"
    r = client.put("/companies/edu/courses/solar-system/structured", json=body)
    assert r.status_code == 400
    assert r.json()["error"]["code"] == "name_mismatch"


def test_create_course(client: TestClient, storage_dir: Path):
    r = client.post(
        "/companies/edu/courses",
        json={"name": "test-create", "displayName": "測試新增課程", "introduction": "Hello"},
    )
    assert r.status_code == 201

    course_dir = storage_dir / "edu" / "test-create"
    assert (course_dir / "test-create.csv").exists()
    assert (course_dir / "meta.json").exists()
    assert (course_dir / "Image").is_dir()
    assert (course_dir / "Video").is_dir()

    body = client.get("/companies/edu/courses/test-create/structured").json()
    assert body["introduction"] == "Hello"
    assert body["steps"] == []


def test_create_course_bad_slug(client: TestClient):
    r = client.post(
        "/companies/edu/courses",
        json={"name": "BAD NAME!!", "displayName": "x"},
    )
    assert r.status_code == 400
    assert r.json()["error"]["code"] == "bad_name"


def test_create_course_already_exists(client: TestClient):
    payload = {"name": "test-dup", "displayName": "dup"}
    r1 = client.post("/companies/edu/courses", json=payload)
    assert r1.status_code == 201
    r2 = client.post("/companies/edu/courses", json=payload)
    assert r2.status_code == 409
    assert r2.json()["error"]["code"] == "course_exists"


def test_update_meta(client: TestClient, storage_dir: Path):
    client.post(
        "/companies/edu/courses",
        json={"name": "test-meta", "displayName": "舊名稱"},
    )
    r = client.put(
        "/companies/edu/courses/test-meta/meta",
        json={"displayName": "新名稱"},
    )
    assert r.status_code == 200
    meta = json.loads((storage_dir / "edu" / "test-meta" / "meta.json").read_text("utf-8"))
    assert meta["displayName"] == "新名稱"


def test_delete_course_moves_to_trash(client: TestClient, storage_dir: Path):
    client.post(
        "/companies/edu/courses",
        json={"name": "test-delete", "displayName": "to-delete"},
    )
    r = client.delete("/companies/edu/courses/test-delete")
    assert r.status_code == 200
    body = r.json()
    assert body["ok"] is True
    assert not (storage_dir / "edu" / "test-delete").exists()
    trashed = storage_dir / body["trashed_to"]
    assert trashed.exists()
    assert (trashed / "test-delete.csv").exists()


def test_list_files(client: TestClient):
    r = client.get("/companies/edu/courses/solar-system/files/image")
    assert r.status_code == 200
    assert "sun.jpg" in r.json()["files"]


def test_upload_and_delete_file(client: TestClient, storage_dir: Path):
    client.post(
        "/companies/edu/courses",
        json={"name": "test-files", "displayName": "file-tests"},
    )
    fake_jpg = b"\xff\xd8\xff\xd9"  # minimal JPEG bytes
    r = client.post(
        "/companies/edu/courses/test-files/files/image",
        files={"file": ("upload.jpg", io.BytesIO(fake_jpg), "image/jpeg")},
    )
    assert r.status_code == 201
    target = storage_dir / "edu" / "test-files" / "Image" / "upload.jpg"
    assert target.exists() and target.read_bytes() == fake_jpg

    r2 = client.get("/companies/edu/courses/test-files/files/image")
    assert "upload.jpg" in r2.json()["files"]

    r3 = client.delete("/companies/edu/courses/test-files/files/image/upload.jpg")
    assert r3.status_code == 200
    body = r3.json()
    assert body["ok"] is True
    assert body["cleared_refs"] == 0  # no steps reference it
    # File is no longer in active storage but should live in _trash.
    assert not target.exists()
    trashed = storage_dir / body["trashed_to"]
    assert trashed.is_file() and trashed.read_bytes() == fake_jpg


def test_delete_file_clears_step_references(client: TestClient, storage_dir: Path):
    """Deleting a media file referenced by a step should
      - move the file to course-local _trash (recoverable)
      - rewrite the CSV with that step's media cleared
      - back up the prior CSV
    The test owns its course so it's not coupled to whatever's currently in
    solar-system / cell-mitosis.
    """
    name = "test-clear-refs"
    # 1. Create empty course
    r = client.post("/companies/edu/courses", json={"name": name, "displayName": "ref-clear"})
    assert r.status_code == 201
    course_path = f"/companies/edu/courses/{name}"

    # 2. Upload a fake image
    fake_jpg = b"\xff\xd8\xff\xd9"
    client.post(
        f"{course_path}/files/image",
        files={"file": ("step-pic.jpg", io.BytesIO(fake_jpg), "image/jpeg")},
    )

    # 3. Add a step that references it
    course = client.get(f"{course_path}/structured").json()
    course["steps"] = [
        {
            "order": 1,
            "mainTitle": "M",
            "subTitle": "",
            "name": "step using picture",
            "description": "x",
            "media": {"kind": "image", "filename": "step-pic.jpg"},
            "nextStepIndication": "",
            "exceptions": [],
        }
    ]
    assert client.put(f"{course_path}/structured", json=course).status_code == 200

    # 4. Delete the media — should trash + clear the step's reference
    r = client.delete(f"{course_path}/files/image/step-pic.jpg")
    assert r.status_code == 200
    body = r.json()
    assert body["cleared_refs"] == 1
    trashed = storage_dir / body["trashed_to"]
    assert trashed.is_file() and trashed.read_bytes() == fake_jpg

    after = client.get(f"{course_path}/structured").json()
    assert after["steps"][0]["media"]["kind"] == "none"
    assert after["steps"][0]["media"]["filename"] is None

    backups = list((storage_dir / "edu" / name / "_backups").iterdir())
    assert backups, "expected CSV backup before clearing refs"


def test_upload_bad_filename(client: TestClient):
    r = client.post(
        "/companies/edu/courses/solar-system/files/image",
        files={"file": ("../escape.jpg", io.BytesIO(b"x"), "image/jpeg")},
    )
    assert r.status_code == 400
    assert r.json()["error"]["code"] == "bad_filename"


def test_csv_roundtrip_preserves_structure(client: TestClient, storage_dir: Path):
    """Read → write same payload → read again → identical structured data."""
    body1 = client.get("/companies/edu/courses/cell-mitosis/structured").json()
    r = client.put("/companies/edu/courses/cell-mitosis/structured", json=body1)
    assert r.status_code == 200
    body2 = client.get("/companies/edu/courses/cell-mitosis/structured").json()
    assert body1 == body2
