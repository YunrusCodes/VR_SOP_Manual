import os
import shutil
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

EXPECTED_DIR = ROOT.parent / "docs" / "sample-data" / "expected"


@pytest.fixture(scope="session")
def storage_dir(tmp_path_factory: pytest.TempPathFactory) -> Path:
    tmp = tmp_path_factory.mktemp("storage")
    src = ROOT / "storage"
    for company_dir in src.iterdir():
        if not company_dir.is_dir():
            continue
        shutil.copytree(company_dir, tmp / company_dir.name)

    course = tmp / "acme" / "engine-room-inspection"
    (course / "Image").mkdir(exist_ok=True)
    (course / "Image" / "engine-hood.jpg").write_bytes(b"\xff\xd8\xff\xd9")
    (course / "Video").mkdir(exist_ok=True)
    (course / "Video" / "startup.mp4").write_bytes(b"\x00" * 4096)
    return tmp


@pytest.fixture(scope="session")
def client(storage_dir: Path) -> TestClient:
    os.environ["STORAGE_DIR"] = str(storage_dir)
    if "main" in sys.modules:
        del sys.modules["main"]
    import main  # type: ignore
    return TestClient(main.app)
