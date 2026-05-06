<!--
此檔是為**新 repo 根目錄**而寫的 onboarding 入口。
複製到新專案 (e.g. d:\Quest3_Inspection\) 根目錄後，刪掉這段註解即可。
-->

# Inspection Quest 3

Quest 3 + Python FastAPI 的智慧巡檢瀏覽工具。教師端把 CSV + 圖/影片放到後端 storage，學生端在 Quest 3 上即時瀏覽（純線上薄客戶端，不下載、不快取）。

> **第一次來？** 看完 [docs/spec.md](docs/spec.md) 就知道全貌（30 分鐘）。
> **AI 接手？** 直接讀 [docs/handoff.md](docs/handoff.md)。

---

## Repo 結構

```
.
├── api/                  Python FastAPI 後端（4 個 GET endpoints）
│   ├── main.py
│   ├── seed.py           生 placeholder jpg/mp4
│   ├── tests/
│   ├── storage/          {company}/{course}/{csv,Image/,Video/}
│   └── requirements.txt
├── unity/                Quest 3 Unity 6 專案（單一 App.unity）
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
└── docs/
    ├── spec.md           完整技術規格
    ├── handoff.md        給新工程師 / AI 的工作交接
    └── sample-data/      範例課程 CSV、預期 API 回應、預期解析結果
```

---

## 快速啟動（5 分鐘）

### 1. 啟後端

```bash
cd api
python -m venv .venv && . .venv/Scripts/activate     # Windows PowerShell: .venv\Scripts\Activate.ps1
pip install -r requirements.txt
python seed.py                                       # 為 storage/ 生 placeholder jpg/mp4
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

驗證：
```bash
curl http://localhost:8000/healthz
# {"status":"ok"}

curl http://localhost:8000/companies/acme/courses
# 應與 ../docs/sample-data/expected/list-courses.json 一致
```

> `seed.py` 需要 `ffmpeg` 在 PATH（Windows: `winget install Gyan.FFmpeg`；macOS: `brew install ffmpeg`）。

### 2. 開 Unity

- Unity Hub → 開啟 `unity/`（Unity 6000.x LTS）
- File → Build Profiles → 切到 **Android**（Quest 3 是 Android 平台）
- 編輯 `Assets/Settings/AppSettings.asset`：
  - `ApiBaseUrl`：開發機區網 IP，例 `http://192.168.1.10:8000`
  - `Company`：先用 `acme`
- 在 Editor 內按 Play 確認 ManualListView 抓得到課程清單

### 3. 部署到 Quest 3

```bash
# 確認 Quest 3 已開發者模式 + 用 USB 接上電腦 + 已授權 ADB
adb devices

# 在 Unity 內：File → Build And Run（會自動 install + launch）
```

戴上頭盔開 App，照 [docs/spec.md §10 驗收條件](docs/spec.md) 逐項打勾。

---

## 範圍（明確不在範圍）

❌ AR / 空間錨點 / 手勢標記（舊系統有，本次不做）
❌ TTS / 語音朗讀
❌ 下載 / 本地快取（純線上）
❌ 教師端編輯介面（教師直接放檔到後端 storage）
❌ 認證 / 登入
❌ 多語 UI（先寫死 zh-TW）

---

## 日常開發小抄

| 我想… | 做法 |
|---|---|
| 新增一門課程 | 在 `api/storage/{company}/` 建目錄、放 `{course}.csv`、`meta.json`、`Image/`、`Video/`，重啟 App 即可看到 |
| 修改步驟內容 | 改對應 csv，重啟 App（CSV 是即時抓的，不必重 build） |
| 砍課程 | 直接刪 `api/storage/{company}/{course}/` |
| 加新欄位 | 改 [docs/spec.md §5.1](docs/spec.md) → 改 `CsvParser` → 改 domain record → 改 UI |
| 對 API 行為有疑問 | 看 [docs/sample-data/expected/](docs/sample-data/expected/) |
| 對 CSV 格式有疑問 | 看 [docs/sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv](docs/sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv) |
| 對 Unity 結構有疑問 | 看 [docs/spec.md §7](docs/spec.md) |

---

## 找開發者問問題前，先檢查

- API 跑得起來嗎？`curl http://localhost:8000/healthz`
- Quest 3 連得到 API 嗎？在 Quest 瀏覽器打開上面網址測試
- AppSettings 的 IP 對嗎？開發機 `ipconfig` / `ifconfig` 確認
- Unity Console 有錯嗎？看 Window → General → Console
- Quest 端 logcat 有錯嗎？`adb logcat -s Unity`
