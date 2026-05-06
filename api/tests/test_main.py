import json
from pathlib import Path

from fastapi.testclient import TestClient

from .conftest import EXPECTED_DIR


def test_healthz(client: TestClient):
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_list_courses_matches_expected(client: TestClient):
    r = client.get("/companies/acme/courses")
    assert r.status_code == 200
    expected = json.loads((EXPECTED_DIR / "list-courses.json").read_text("utf-8"))
    body = r.json()
    assert body["company"] == expected["company"]
    assert sorted(body["courses"], key=lambda c: c["name"]) == sorted(
        expected["courses"], key=lambda c: c["name"]
    )


def test_list_courses_unknown_company(client: TestClient):
    r = client.get("/companies/no-such-co/courses")
    assert r.status_code == 404
    assert r.json()["error"]["code"] == "company_not_found"


def test_get_csv(client: TestClient):
    r = client.get("/companies/acme/courses/engine-room-inspection/csv")
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/csv")
    assert "Step Order" in r.text
    assert "engine-hood.jpg" in r.text
    # BOM should be stripped on read
    assert not r.text.startswith("﻿")


def test_get_csv_unknown_course(client: TestClient):
    r = client.get("/companies/acme/courses/no-such-course/csv")
    assert r.status_code == 404
    assert r.json()["error"]["code"] == "course_not_found"


def test_get_image(client: TestClient):
    r = client.get("/companies/acme/courses/engine-room-inspection/files/image/engine-hood.jpg")
    assert r.status_code == 200
    assert r.headers["content-type"] == "image/jpeg"
    assert len(r.content) >= 4


def test_get_image_not_found(client: TestClient):
    r = client.get("/companies/acme/courses/engine-room-inspection/files/image/nope.jpg")
    assert r.status_code == 404
    assert r.json()["error"]["code"] == "file_not_found"


def test_bad_kind(client: TestClient):
    r = client.get("/companies/acme/courses/engine-room-inspection/files/audio/x.wav")
    assert r.status_code == 400
    assert r.json()["error"]["code"] == "bad_kind"


def test_traversal_blocked(client: TestClient):
    r = client.get(
        "/companies/acme/courses/engine-room-inspection/files/image/..%2F..%2Fmeta.json"
    )
    assert r.status_code in (400, 404)


def test_video_range(client: TestClient):
    url = "/companies/acme/courses/engine-room-inspection/files/video/startup.mp4"
    r = client.get(url, headers={"Range": "bytes=0-1023"})
    assert r.status_code == 206
    assert r.headers["content-type"] == "video/mp4"
    assert len(r.content) == 1024
    assert r.headers["content-range"].startswith("bytes 0-1023/")
