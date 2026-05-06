# 智慧巡檢系統 — Quest 3 重寫規格

> 本文是 **「給人看的設計文件 + 給 AI 實作的契約」**。
> 看完這份文件，你不需要讀舊的 C# 原始碼，就能：
>
> - 知道這個系統要做什麼、流程如何運作
> - 把系統用任何語言/框架重新實作出來
> - 驗證新版實作功能是否齊全
>
> **範圍**：本文 **不包含 AR / 空間錨點 / 手勢標記功能**。原系統的 AR 部份留待後續另寫規格。
> **目標平台**：Meta Quest 3（Android-based VR / passthrough MR）。
> **後端**：本文同時設計一個輕量 Python API，取代已報廢的舊後端，讓新場景能離線端到端跑通。

---

## 1. 一句話介紹

**這是一個讓現場操作員戴 Quest 3 觀看「圖文/影片步驟教學」的工具。** 教學內容由教師端用 Excel 撰寫、上傳到後端；學生端在 Quest 上即時瀏覽（不下載、不快取），按步驟順序操作或依「例外狀況按鈕」分支。

> 原系統還有「AR 標記貼在實體機械上」的功能，本次重寫先做純 UI 部份。

---

## 2. 名詞表

| 名詞 | 意義 |
|---|---|
| **Company（公司）** | 最上層命名空間，一個公司可以有多門課程。登入時決定。 |
| **Course（課程）** | 一份完整的教學手冊，對應一個 CSV 檔。例：「引擎室巡檢」、「煞車保養」。 |
| **Step（步驟）** | 課程中的一個單元。包含主標題、副標題、步驟名、描述、影片或圖片、可選的例外處理選項。 |
| **Title（標題節點）** | UI 樹狀導覽結構的節點。Step 會被掛到 Main Title → Sub Title → Step 的三層樹下。 |
| **Exception Option（例外處理選項）** | 步驟下方的分支按鈕。最多 3 個。按下去要嘛跳到指定步驟，要嘛只是顯示一段說明文字。 |
| **Next Step Indication（下一步提示）** | 某些步驟的描述底下會掛一句「請走到下一個位置」之類的引導文字。 |
| **Manual List（手冊清單）** | 課程選單畫面。列出公司目錄下所有課程，每門課程只有一顆「進入」按鈕。 |
| **Teacher / Student** | 兩種使用者角色。本次重寫暫只做 Student（瀏覽）端。Teacher 端涉及 AR 標記，留待後續。 |

---

## 3. 系統架構總覽

```
┌──────────────────────────┐  HTTPS (always on)  ┌────────────────────┐
│   Quest 3 App            │ ◄─────────────────► │  Python API        │
│   (Unity, C#, 單一場景)   │   不快取、即時抓     │  (FastAPI)         │
│                          │                     │                    │
│  App.unity:              │                     │  GET /courses      │
│   ├ ManualListView       │                     │  GET /csv          │
│   ├ CourseView           │                     │  GET /image/{name} │
│   └ LoadingOverlay       │                     │  GET /video/{name} │
└──────────────────────────┘                     └────────────────────┘
                                                          │
                                                          ▼
                                                   ┌────────────────┐
                                                   │ ./storage/     │
                                                   │   {Company}/   │
                                                   │     {Course}/  │
                                                   │       *.csv    │
                                                   │       Image/   │
                                                   │       Video/   │
                                                   └────────────────┘
```

**設計決策（重要）**：
- **單一 Unity 場景** + 多個 UI View（用 `SetActive()` 切換），而非多場景。
- **純線上薄客戶端**：App 不下載、不快取、不存任何檔。每次需要資料就向 API 即時請求。
  - 課程清單：開 App 時 `GET /courses`
  - 進入課程：`GET /csv` 即時解析
  - 圖片：`UnityWebRequestTexture.GetTexture(url)` 直抓
  - 影片：`videoPlayer.url = "<api>/.../video/<name>"` HTTP streaming
- **假設網路穩定**：不做離線降級、不做 retry/backoff、不做斷線通知。連不上就讓使用者看到原生例外即可。
- **沒有下載 / 更新 / 刪除按鈕**。ManualListView 每張卡片只有一顆 [進入] 按鈕。

**端到端流程**：
1. 教師端把 `{Course}.csv`、圖片、影片放到後端 `/{Company}/{Course}/` 目錄即生效，無發布概念。
2. 學生戴上 Quest 開 App：唯一場景 `App.unity` 載入，預設顯示 `ManualListView`，呼叫 `GET /courses` 拉清單，期間 `LoadingOverlay` 蓋住畫面。
3. 學生按「進入」：App 呼叫 `GET /csv` 抓課程內容、即時解析，切到 `CourseView`，期間 `LoadingOverlay` 蓋住。
4. 看步驟時：圖片/影片用 API URL 直接載入，無本地檔。

---

## 4. 使用者流程（純 UI 端）

### 4.1 Manual List 流程

```
[啟動 App]
    ↓
[App.unity 載入] → LoadingOverlay 顯示「載入課程清單…」
    ↓ 呼叫 GET /companies/{company}/courses
[ManualListView 顯示課程卡片清單]
    ↓
每張卡片只有一顆 [進入] 按鈕（外加課程名）。
    ↓
[按 進入] → LoadingOverlay 顯示「載入課程…」
         → 呼叫 GET /companies/{company}/courses/{course}/csv
         → 解析成 Course 物件
         → 切到 CourseView
```

### 4.2 CourseView 流程

```
[CourseView 顯示]
    ↓ 已經有 Course 物件（在 ManualList 階段抓好）
[依 step.order 排序，顯示第 1 個 step]
    ↓
玩家可以：
  - 點 [上一步] / [下一步] → 在 step 列表上前後移動
  - 點例外處理按鈕 → 跳到指定步驟，或顯示說明文字
  - 點 [← 課程清單] → 回 ManualListView
    ↓
進入某個 Step 時：
  - 顯示 MainTitle ▸ SubTitle ▸ StepName 麵包屑、Description
  - 若 media 是 video → videoPlayer.url 設成 API 串流網址、Play
  - 若 media 是 image → UnityWebRequestTexture 抓 → 顯示
  - 若 media 是 none → 隱藏媒體區
  - 顯示該 step 的例外處理按鈕
```

---

## 5. 資料模型

### 5.1 CSV 格式（教師端用 Excel 編輯）

CSV 第一列是課程簡介（單欄），第二列是表頭，第三列起是步驟資料。

```
"本課程教你如何巡檢引擎室..."                       ← 第 1 列：課程簡介（任意文字）
Step Order,Main Title,Sub Title,Step Name,Description,Video Or Image,Next Step Indication,Exception Handling Option 1,Action After Selecting Handling Option 1,Exception Handling Option 2,Action After Selecting Handling Option 2,Exception Handling Option 3,Action After Selecting Handling Option 3
1,引擎室,啟動前,檢查機油,"打開機油蓋，確認油位在 MIN-MAX 之間",A1.jpg,,油位過低,3,油位過高,,污濁,5
2,引擎室,啟動前,檢查冷卻液,"觀察副水箱液位",A2.mp4,,,,,,,
3,引擎室,例外,補機油,"加 5W-30 機油至 MAX 線",,,,,,,
...
```

**欄位說明**（位置固定，表頭僅供人類閱讀）：

| # | 欄位 | 型別 | 必填 | 說明 |
|---|---|---|---|---|
| 1 | Step Order | int | ✅ | 步驟編號，從 1 開始遞增。 |
| 2 | Main Title | string | ✅ | 大分類名稱，如「引擎室」。 |
| 3 | Sub Title | string | ❌ | 小分類名稱，可空字串（表示直接掛在 Main Title 下）。 |
| 4 | Step Name | string | ✅ | 步驟名稱，如「檢查機油」。 |
| 5 | Description | string | ✅ | 步驟描述，顯示在 UI 上。允許多行（用引號包起來）。 |
| 6 | Video Or Image | string | ❌ | 媒體檔名，副檔名決定播放方式：`.mp4` → 影片，`.jpg` → 圖片，空字串 → 無媒體。 |
| 7 | Next Step Indication | string | ❌ | 下一階段的引導文字（例如「請移動到副駕駛側」）。原系統用此分割 AR 空間段落，本次純 UI 版只做提示顯示用。 |
| 8 | Exception Handling Option 1 | string | ❌ | 例外按鈕文字。 |
| 9 | Action After Selecting Handling Option 1 | string \| int | ❌ | 動作。**整數 → 跳到該 Step Order**；**文字 → 顯示這段文字**。 |
| 10–13 | Exception Option 2/3 + Action 2/3 | 同上 | ❌ | 同上，最多 3 組。 |

### 5.2 解析規則

- **跳脫**：欄位含逗號或換行時用雙引號包起來；雙引號本身用 `""` 表示。
- **編碼**：UTF-8。從 API 抓回後若有 BOM (`﻿`) 必須剝除。
- **表頭驗證**：直接以欄位位置（index 0–12）解析，**不依賴表頭文字**。但解析時應驗證欄位數量為 13；不足或過多須記 warning。
- **空字串 vs null**：CSV 中沒填的欄位都是空字串 `""`，新版實作應將空字串視為 null。

> 📂 **具體範例**：[sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv](sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv) — 9 步驟、刻意涵蓋 SubTitle 有/無、媒體 image/video/none、例外按鈕 0/1/3 個、goto/message 兩種 action。配 [sample-data/expected/engine-room-inspection.parsed.json](sample-data/expected/engine-room-inspection.parsed.json) 看 CSV 解析後的 `Course` 物件長什麼樣。

### 5.3 內部 JSON Schema（CSV 轉換後）

App 把 CSV 轉成 JSON 存在本地 `{Course}.json`。建議 schema：

```json
{
  "schemaVersion": 1,
  "courseName": "引擎室巡檢",
  "introduction": "本課程教你如何巡檢引擎室...",
  "steps": [
    {
      "order": 1,
      "mainTitle": "引擎室",
      "subTitle": "啟動前",
      "name": "檢查機油",
      "description": "打開機油蓋，確認油位在 MIN-MAX 之間",
      "media": { "kind": "image", "fileName": "A1.jpg" },
      "nextStepIndication": null,
      "exceptions": [
        { "label": "油位過低", "action": { "kind": "goto", "step": 3 } },
        { "label": "油位過高", "action": { "kind": "message", "text": "" } },
        { "label": "污濁",     "action": { "kind": "goto", "step": 5 } }
      ]
    }
  ]
}
```

`media.kind` 為 `image` / `video` / `none`。
`action.kind` 為 `goto`（含 `step` 欄）或 `message`（含 `text` 欄）。
判斷規則：action 字串能 parse 成 int 則為 `goto`，否則為 `message`。

---

## 6. Python API 設計

### 6.1 技術選型

| 項目 | 選用 |
|---|---|
| 框架 | **FastAPI**（async、auto OpenAPI、型別友善） |
| 伺服器 | **uvicorn** |
| 儲存 | 本機檔案系統 `./storage/`（後續可換 S3，介面不變） |
| 認證 | **本次不做** |
| CORS | 全開（開發階段） |
| 部署 | `uvicorn main:app --host 0.0.0.0 --port 8000`，Quest 用區網 IP 直連 |

### 6.2 後端目錄結構（API 直接讀的本機檔）

```
storage/
└── {company}/                    ← 例：acme
    └── {course}/                 ← 例：engine-room-inspection
        ├── {course}.csv          ← 教師上傳的 Excel 匯出檔
        ├── meta.json             ← (可選) {"displayName": "引擎室巡檢"}
        ├── Image/
        │   ├── A1.jpg
        │   └── ...
        └── Video/
            ├── A2.mp4
            └── ...
```

教師端如何把檔案放進來不在本次規格範圍（直接 scp / 共用資料夾 / 之後再做上傳介面都行）。

### 6.3 Endpoints

只有 4 個 GET，不做版本比對、不做 manifest、不做上傳。

#### `GET /healthz`
健康檢查。回 `{ "status": "ok" }`。

#### `GET /companies/{company}/courses`
列出公司底下所有課程的「最小資訊」。

**Response**:
```json
{
  "company": "acme",
  "courses": [
    { "name": "engine-room-inspection", "displayName": "引擎室巡檢" },
    { "name": "brake-maintenance",      "displayName": "煞車保養" }
  ]
}
```

實作：掃 `storage/{company}/*` 目錄，回傳每個含 `{name}.csv` 的子目錄。`displayName` 從 `meta.json` 讀，無則 fallback 為 `name`。

#### `GET /companies/{company}/courses/{course}/csv`
取得課程的 CSV 內容。

**Response**: `text/csv; charset=utf-8`，body 為 CSV 原始內容（不剝 BOM；客戶端負責）。

#### `GET /companies/{company}/courses/{course}/files/{kind}/{filename}`
取得圖片或影片。`kind` ∈ `image` / `video`。

**Response**: binary stream。
- `image/jpeg` 或 `image/png`，依副檔名。
- `video/mp4`，**必須支援 HTTP Range**（讓 VideoPlayer 能 seek）。

### 6.4 錯誤格式

統一格式：

```json
{ "error": { "code": "course_not_found", "message": "..." } }
```

HTTP status：404（找不到）、400（參數錯）、500（內部錯）。

### 6.5 Python 實作骨架（給實作者起手用）

```python
# api/main.py
from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse, PlainTextResponse, Response
from fastapi.middleware.cors import CORSMiddleware
from pathlib import Path
import json, os, mimetypes

STORAGE = Path(os.environ.get("STORAGE_DIR", "./storage")).resolve()
app = FastAPI()
app.add_middleware(CORSMiddleware, allow_origins=["*"])

def _course_dir(company: str, course: str) -> Path:
    p = STORAGE / company / course
    if not p.is_dir():
        raise HTTPException(404, {"error": {"code": "course_not_found", "message": f"{company}/{course}"}})
    return p

@app.get("/healthz")
def healthz():
    return {"status": "ok"}

@app.get("/companies/{company}/courses")
def list_courses(company: str):
    base = STORAGE / company
    if not base.is_dir():
        raise HTTPException(404, {"error": {"code": "company_not_found", "message": company}})
    out = []
    for d in sorted(p for p in base.iterdir() if p.is_dir()):
        if not (d / f"{d.name}.csv").exists():
            continue
        meta = {}
        meta_path = d / "meta.json"
        if meta_path.exists():
            meta = json.loads(meta_path.read_text("utf-8"))
        out.append({"name": d.name, "displayName": meta.get("displayName", d.name)})
    return {"company": company, "courses": out}

@app.get("/companies/{company}/courses/{course}/csv")
def get_csv(company: str, course: str):
    csv_path = _course_dir(company, course) / f"{course}.csv"
    if not csv_path.exists():
        raise HTTPException(404, {"error": {"code": "csv_not_found"}})
    return PlainTextResponse(csv_path.read_text("utf-8-sig"), media_type="text/csv")

@app.get("/companies/{company}/courses/{course}/files/{kind}/{filename}")
def get_file(company: str, course: str, kind: str, filename: str):
    if kind not in ("image", "video"):
        raise HTTPException(400, {"error": {"code": "bad_kind"}})
    folder = _course_dir(company, course) / ("Image" if kind == "image" else "Video")
    f = folder / filename
    if not f.exists() or ".." in filename:
        raise HTTPException(404, {"error": {"code": "file_not_found"}})
    media_type, _ = mimetypes.guess_type(f.name)
    return FileResponse(f, media_type=media_type or "application/octet-stream")
```

> `FileResponse` 自動支援 HTTP Range，影片 seek 可正常運作。

### 6.6 開發者測試資料

直接使用本 repo 的 [docs/sample-data/storage/](sample-data/storage/) — 已備好兩門課程：

- `acme/engine-room-inspection/` — 主案例，9 步驟、含各種 edge case
- `acme/brake-maintenance/` — 簡短第二筆，確認 list 端點不會回 hardcoded 一筆

新 session 在 API 專案建立時可直接：
```bash
cp -r ../CarInspectionData/docs/sample-data/storage ./storage
# 自行補上 1×1 jpg / 短 mp4（檔名見 storage/acme/engine-room-inspection/Image/README.md）
```

預期回應對照在 [docs/sample-data/expected/](sample-data/expected/)：
- [list-courses.json](sample-data/expected/list-courses.json) — `GET /companies/acme/courses` 應回的內容
- [engine-room-inspection.parsed.json](sample-data/expected/engine-room-inspection.parsed.json) — `CsvParser.Parse` 應產生的 `Course` 結構

`pytest` 應至少測：healthz、list courses（與 expected/list-courses.json 比對）、get csv、get image、404、HTTP Range。

---

## 7. Quest 3 Unity App 結構

### 7.1 單一場景 + 多 View

整個 App 用一個場景 `App.unity`。換頁就是切換 RootCanvas 下不同的 UI 子樹（`SetActive`），沒有場景切換。

```
App.unity
├── Persistent (空 GameObject 容器)
│   ├── XR Origin (XR Rig)            ← Meta XR 標準
│   ├── EventSystem                    ← 配 XRI UI Input Module
│   ├── Lighting (Directional Light)
│   └── Services
│       ├── AppBootstrapper            ← Awake 建構服務、注入到 Views
│       └── AppRouter                  ← 控制 View 切換（純邏輯 MonoBehaviour）
└── RootCanvas (World-Space Canvas, 跟頭部 lazy-follow)
    ├── ManualListView                 ← default active
    ├── CourseView                     ← default inactive
    └── LoadingOverlay                 ← 跨 View 共用，預設關閉
```

設計重點：
- **沒有 Boot 場景/View**：App 啟動就 spawn `App.unity`，預設顯示 `ManualListView`，初始化期間用 `LoadingOverlay` 蓋住。
- **服務在場景生命週期內只活一次**：不需要 `DontDestroyOnLoad`、不需要 ScriptableObject 傳遞 session。
- **沒有 DownloadCoordinator / LocalCache**：純線上薄客戶端，沒有要協調的下載。

### 7.2 技術棧（Quest 3）

| 領域 | 選用 | 取代原本的 |
|---|---|---|
| XR SDK | **Meta XR All-in-One SDK** + **OpenXR Plugin** | MRTK 2.x |
| 互動 | **XR Interaction Toolkit (XRI)** + Ray Interactor | MRTK Buttons / ObjectManipulator |
| UI 框架 | **uGUI on World-Space Canvas** + TMP | MRTK Canvas + ScrollingObjectCollection |
| 影片 | Unity **VideoPlayer**（`source = Url`，HTTP streaming，`url` 直接設 API 網址） | （同） |
| 圖片 | Unity **RawImage** + `UnityWebRequestTexture.GetTexture(url)` | （同） |
| 語音 | **不做**。Description 純文字顯示。 | Crosstales RTVoice |
| 儲存 | **無**（純線上） | UWP `StorageFile` |
| 網路 | `UnityWebRequest`（CSV 用 `DownloadHandlerBuffer`，不寫檔） | （同） |
| JSON | **System.Text.Json** 或 **Newtonsoft.Json** | 自寫 parser 廢棄 |
| 設定 | 一個 `ScriptableObject` (`AppSettings`) | PlayerPrefs 散落 |

### 7.3 領域模型（C# 類別骨架）

純資料 record，**不依賴 UnityEngine**，放在獨立的 asmdef 裡，方便寫單元測試：

```csharp
public sealed record Course(
    string Name,                       // 課程目錄名（API 用）
    string DisplayName,                // 給人看的名稱
    string Introduction,
    IReadOnlyList<Step> Steps);

public sealed record Step(
    int Order,
    string MainTitle,
    string SubTitle,                   // null 表示無子分類
    string Name,
    string Description,
    Media Media,                       // 用 Media.None 表示無媒體，永不為 null
    string NextStepIndication,         // null 或 ""
    IReadOnlyList<ExceptionOption> Exceptions);

public abstract record Media
{
    public sealed record None() : Media;
    public sealed record Image(string FileName) : Media;
    public sealed record Video(string FileName) : Media;
}

public sealed record ExceptionOption(string Label, ExceptionAction Action);

public abstract record ExceptionAction
{
    public sealed record GoToStep(int Step) : ExceptionAction;
    public sealed record ShowMessage(string Text) : ExceptionAction;
}

public sealed record CourseSummary(
    string Name,
    string DisplayName);
```

### 7.4 服務介面（依賴注入用）

只有一個服務：API 客戶端。沒有 cache、沒有 repository、沒有 download、沒有 TTS。

```csharp
public interface ICourseClient
{
    Task<IReadOnlyList<CourseSummary>> ListCoursesAsync(CancellationToken ct);
    Task<Course> GetCourseAsync(string courseName, CancellationToken ct);   // 抓 CSV、解析、回傳

    string GetImageUrl(string courseName, string fileName);                  // 給 UnityWebRequestTexture
    string GetVideoUrl(string courseName, string fileName);                  // 給 VideoPlayer.url
}
```

`CourseClient` 內部組成：HTTP client + `CsvParser`（CSV → `Course`）。

### 7.5 Persistent root：服務組合

`AppBootstrapper.Awake` 是唯一的 composition root：

```csharp
public sealed class AppBootstrapper : MonoBehaviour
{
    [SerializeField] AppSettings settings;
    [SerializeField] AppRouter router;
    [SerializeField] ManualListView manualList;
    [SerializeField] CourseView courseView;
    [SerializeField] LoadingOverlay overlay;

    async void Awake()
    {
        var http   = new HttpClient { BaseAddress = new Uri(settings.ApiBaseUrl) };
        var csv    = new CsvParser();
        var client = new CourseClient(http, settings.Company, csv);

        manualList.Init(client, router, overlay);
        courseView.Init(client, router);

        router.ShowManualList();

        overlay.Show("載入課程清單…");
        try { await manualList.RefreshAsync(); }
        finally { overlay.Hide(); }
    }
}
```

`AppRouter` 管 View 切換：

```csharp
public sealed class AppRouter : MonoBehaviour
{
    [SerializeField] ManualListView manualList;
    [SerializeField] CourseView courseView;

    public void ShowManualList()
    {
        courseView.gameObject.SetActive(false);
        manualList.gameObject.SetActive(true);
    }

    public void ShowCourse(Course course)
    {
        manualList.gameObject.SetActive(false);
        courseView.Bind(course);
        courseView.gameObject.SetActive(true);
    }
}
```

### 7.6 ManualListView 元件樹

```
ManualListView (RectTransform, 1200×800 px)
├── Header          (TMP - "選擇課程")
├── ScrollRect
│   └── Viewport
│       └── Content (Vertical Layout Group, Content Size Fitter)
│           └── (動態填入 CourseCard prefab)
└── EmptyState      (TMP - "目前沒有課程"，預設關閉)
```

`CourseCard` prefab（極簡，只一顆按鈕）：
```
CourseCard (Horizontal Layout Group)
├── Title         (TMP - course.DisplayName)
└── EnterButton   ("進入")
```

行為：
- `EnterButton` →
  ```
  overlay.Show("載入課程…")
  try   { var course = await client.GetCourseAsync(name, ct);
          router.ShowCourse(course); }
  finally { overlay.Hide(); }
  ```

### 7.7 CourseView 元件樹

```
CourseView (RectTransform, 1200×800 px)
├── TopBar
│   ├── Breadcrumb         (TMP - "引擎室 ▸ 啟動前 ▸ 檢查機油")
│   ├── StepCounter        (TMP - "3 / 18")
│   └── BackToListButton   ("← 課程清單")
├── ContentArea (Horizontal split)
│   ├── LeftColumn (60% 寬)
│   │   ├── StepName       (TMP, large)
│   │   ├── DescriptionScroll
│   │   │   └── Description (TMP, multi-line)
│   │   └── NextIndication (TMP, italic, 可選)
│   └── RightColumn (40% 寬)
│       └── MediaPanel
│           ├── ImageView   (RawImage)         ← 互斥
│           └── VideoView   (RawImage + VideoPlayer 寫到 RenderTexture)
├── ExceptionLayer (Horizontal Layout Group, 底部, 動態填入)
└── NavBar
    ├── PrevStepButton ("← 上一步")
    └── NextStepButton ("下一步 →")
```

- 沒有「回上一層」「回首頁」按鈕。Breadcrumb 只是顯示用，不可點。
- 要跳到任意步驟，按 `BackToListButton` 回 ManualList。

### 7.8 Step 載入時的副作用

`CourseView.GoTo(int order)`：

1. 更新 Breadcrumb / StepCounter / StepName / Description / NextIndication。
2. 依 `step.Media`：
   - `Image(file)` → `var tex = await ImageLoader.LoadAsync(client.GetImageUrl(course, file), ct)`、`ImageView.texture = tex`、VideoView 關閉、ImageView 開啟。
   - `Video(file)` → `videoPlayer.url = client.GetVideoUrl(course, file)`、`Play()`。ImageView 關閉、VideoView 開啟。
   - `None` → MediaPanel `SetActive(false)`。
3. 清空並重生 `ExceptionLayer` 子節點，每個 Exception 一顆按鈕。
4. `PrevStepButton.interactable = order > 1`；`NextStepButton.interactable = order < steps.Count`。

`ImageLoader.LoadAsync` 是個小工具：包 `UnityWebRequestTexture.GetTexture(url).SendWebRequest()`，回傳 `Texture2D`。

切換 step 或離開 CourseView 時，要 `videoPlayer.Stop()` 並把舊 `Texture2D` 丟掉避免洩漏（`UnityEngine.Object.Destroy(oldTex)`）。

### 7.9 Step 導覽行為

| 操作 | 行為 |
|---|---|
| `[下一步]` | `GoTo(current + 1)` |
| `[上一步]` | `GoTo(current - 1)` |
| 例外按鈕 (`GoToStep`) | `GoTo(action.Step)` |
| 例外按鈕 (`ShowMessage`) | `LoadingOverlay` 借用為訊息浮層，顯示 `action.Text`，按關閉 → 不改變當前 step |
| `[← 課程清單]` | `videoPlayer.Stop()`、清掉當前 image texture、`router.ShowManualList()` |

### 7.10 場景建構指南（給新 session 的 Unity MCP 用）

新 Claude Code session 在 Quest 3 專案上實作時，可用 Unity MCP 的 `Unity_RunCommand` 透過 `UnityEditor` API 直接建構 `App.unity`。下面是建議的執行步驟（每一步都是一個 MCP `Unity_RunCommand` 呼叫的單元）：

**步驟 1：建立空場景**
```csharp
var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
EditorSceneManager.SaveScene(scene, "Assets/Scenes/App.unity");
```

**步驟 2：建立 Persistent root**
```csharp
var persistent = new GameObject("Persistent");
new GameObject("Lighting").transform.SetParent(persistent.transform);
// XR Origin: 用 Meta XR menu (GameObject > XR > XR Origin (Action-based)) 加進場景
// EventSystem: GameObject > UI > Event System；Standalone Input Module 換成 XRUIInputModule
var services = new GameObject("Services");
services.transform.SetParent(persistent.transform);
services.AddComponent<AppBootstrapper>();
services.AddComponent<AppRouter>();
```

**步驟 3：建立 RootCanvas 與三個 View**
```csharp
var canvasGo = new GameObject("RootCanvas", typeof(Canvas), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
var canvas = canvasGo.GetComponent<Canvas>();
canvas.renderMode = RenderMode.WorldSpace;
canvas.transform.localScale = Vector3.one * 0.001f;   // 1 px ≈ 1 mm
canvas.transform.position = new Vector3(0, 1.5f, 1.5f);
// 加 RectTransform sizing、lazy-follow（自寫小元件）
// 在 canvas 底下分別建 ManualListView / CourseView / LoadingOverlay 三個子 GameObject
```

**步驟 4：建 prefab**
建議放 `Assets/Prefabs/UI/`：
- `CourseCard.prefab`（標題 + 進入按鈕）
- `ExceptionButton.prefab`（標題；onClick 由生成端動態 wire）

**步驟 5：寫一份 `AppSettings.asset`**
```csharp
var settings = ScriptableObject.CreateInstance<AppSettings>();
settings.ApiBaseUrl = "http://192.168.1.10:8000";
settings.Company    = "acme";
AssetDatabase.CreateAsset(settings, "Assets/Settings/AppSettings.asset");
```

**步驟 6：用 SerializedObject 把所有 `[SerializeField]` 的引用串起來**
```csharp
var bootstrapper = services.GetComponent<AppBootstrapper>();
var so = new SerializedObject(bootstrapper);
so.FindProperty("settings").objectReferenceValue   = settings;
so.FindProperty("router").objectReferenceValue     = services.GetComponent<AppRouter>();
so.FindProperty("manualList").objectReferenceValue = manualListViewInstance;
so.FindProperty("courseView").objectReferenceValue = courseViewInstance;
so.FindProperty("overlay").objectReferenceValue    = loadingOverlayInstance;
so.ApplyModifiedProperties();
```

**步驟 7：`EditorSceneManager.SaveScene` 收尾**

> ⚠️ 新 session 實作時要先確保所有 MonoBehaviour script 已存在（編譯通過）才能 `AddComponent<T>()`，否則 Unity 會丟 missing reference。建議先跑一輪 `AssetDatabase.Refresh()` 與 `CompilationPipeline.RequestScriptCompilation()`。

> 場景建構完成後可呼叫 `Unity_SceneView_CaptureMultiAngleSceneView` 拍幾張 Scene view 截圖回報，方便人類驗收結構是否合理。

---

## 8. 設定（Configuration）

新增 `Assets/Settings/AppSettings.asset`（ScriptableObject）：

```csharp
[CreateAssetMenu(fileName = "AppSettings", menuName = "Inspection/AppSettings")]
public sealed class AppSettings : ScriptableObject
{
    public string ApiBaseUrl = "http://192.168.1.10:8000";  // 開發機 IP
    public string Company    = "acme";
    public bool   VerboseLog = true;
}
```

執行時 `AppBootstrapper` 讀這個 asset，注入到 `CourseClient`。

> **不要**像舊版那樣把 URL、CompanyName 散落寫死在多個檔案。

---

## 9. 不要做的事（從舊系統學到的反模式）

新實作請主動避開以下舊系統的問題：

1. ❌ **`async void` 滿天飛**。除了 Unity 事件本身（如 `Awake`），所有非同步方法回傳 `Task` 或 `UniTask`。
2. ❌ **靜態鏈結串列指標**（舊版 `StepInterface.HeadStepPointer`、`TailStepPointer`）。Step 用 `IReadOnlyList<Step>` 並依 `Order` 排序即可，前後切換靠 index ± 1。
3. ❌ **靜態 UI handler 引用**（舊版 `JsonUiStruct.RecentCommandHandler`）。改用建構子注入或 `[SerializeField]` 拉引用。
4. ❌ **PlayerPrefs 當資料庫**。所有狀態進記憶體；PlayerPrefs 只用來存「上次看到第幾步」這類 trivial 偏好（可選）。
5. ❌ **手寫 JsonParser**。用 `System.Text.Json` 或 Newtonsoft。但本專案根本沒在用 JSON parsing（CSV → 直接到 domain），所以也無需引入。
6. ❌ **多場景做純 UI 切換**。VR 中場景 reload 體驗差；用單一場景 + 多 View `SetActive`。
7. ❌ **`Task.Run(() => unityApiCall())`**。Unity API 必須在主執行緒；非同步 I/O 用 `await UnityWebRequest.SendWebRequest()` 即可。
8. ❌ **`try { ... } catch { return null; }` 吞例外**。捕獲後至少 `Debug.LogException`，並回傳明確的失敗訊號（`Result<T>` 或拋 wrapper exception）。
9. ❌ **CSV 表頭硬編碼覆蓋**。新版以欄位位置 (index) 為準，表頭文字僅供人類閱讀。
10. ❌ **拼錯字的型別名 (`DownlLoadManager`)**。新版命名請過 review。
11. ❌ **大量 `Debug.Log`**。包一層 `Logger.Verbose(...)`，由 `AppSettings.VerboseLog` 控制。
12. ❌ **本地快取 / 下載**。本次規格刻意不做。圖/影片直接走 API URL，不要中間 cache 一層。

---

## 10. 驗收條件（功能對等的最低標）

新版交付時應通過以下手動測試：

### 10.1 ManualList
- [ ] 啟動 App，LoadingOverlay 顯示載入文字。
- [ ] LoadingOverlay 消失後，從 API 拉到的所有課程都以卡片呈現。
- [ ] 每張卡片顯示 `displayName` 與一顆 [進入] 按鈕（沒有其他按鈕）。
- [ ] 後端新增/移除課程資料夾後，重啟 App 清單會跟著變。

### 10.2 CourseView
- [ ] 按 [進入] → LoadingOverlay 顯示載入課程文字 → 切到 CourseView 第 1 個 step。
- [ ] Breadcrumb 正確顯示「MainTitle ▸ SubTitle ▸ StepName」（無 SubTitle 時省略中間段）。
- [ ] StepCounter 正確顯示「current / total」。
- [ ] Description 完整顯示（多行不截斷）。
- [ ] step.media 是 image 時正確顯示 jpg；是 video 時自動 streaming 播放；是 none 時 MediaPanel 隱藏。
- [ ] 影片支援拖時間軸（Range request 正常）。
- [ ] [下一步] / [上一步] 順/逆走完所有 step；鏈頭/鏈尾按鈕正確 disable。
- [ ] 例外按鈕 (goto) 正確跳到目標 step。
- [ ] 例外按鈕 (message) 顯示文字浮層，關閉後當前 step 不變。
- [ ] [← 課程清單] 停影片、回 ManualList。

### 10.3 Python API
- [ ] `GET /healthz` 回 200 + `{"status": "ok"}`。
- [ ] `GET /companies/acme/courses` 回課程清單（含 displayName）。
- [ ] `GET /.../csv` 回 200，`Content-Type: text/csv`。
- [ ] `GET /.../files/image/A1.jpg` 回 200，`Content-Type: image/jpeg`。
- [ ] `GET /.../files/video/A2.mp4` 支援 `Range: bytes=0-1023` → 206 Partial Content。
- [ ] 不存在的 course / file → 404 + 錯誤格式 JSON。
- [ ] `seed.py` 能一鍵生成測試 storage。
- [ ] `pytest`：healthz、list courses、get csv、get image、404、Range。

---

## 11. 開放問題（人類需要決策）

實作前請業主或專案負責人回答以下問題：

1. **公司識別**：使用者選擇公司的方式？（A）寫死在 AppSettings；（B）App 內提供切換 UI。**建議：先 A，需要時再加 B。**
2. **影片快取行為**：Quest 上 VideoPlayer 預設會在記憶體緩衝整支影片；長片可能 OOM。要不要設 `controlledAudioTrackCount`、`skipOnDrop`、或限制檔案大小？**建議：先不處理，影片 < 50MB 即可。**
3. **多語**：UI 文字（「進入」「下一步」等）是否多語？**建議：本次硬編 zh-TW 即可。**
4. **錯誤畫面**：API 連不上時要顯示什麼？（規格說「假設網路穩定」，但實務上仍會斷）**建議：最小做法是顯示一行紅字 + [重試] 按鈕；不做更精緻的處理。**

---

## 12. 給 Claude Code 實作 session 的提示

> 📂 開始前請務必先看 [sample-data/README.md](sample-data/README.md) — 用具體範例理解整個資料流，比讀規格快。

新 session 開始時建議的工作順序：

1. **先做 Python API**（本機跑得起來、`seed.py` 生測試資料、curl 能拉 csv/jpg/mp4）。pytest 通過 §10.3。
2. **建 Unity 專案骨架**（Quest 3 build target、Meta XR All-in-One SDK、XRI、TMP、Newtonsoft.Json）。
3. **實作純資料層**（`Course`、`Step`、`Media`、`ExceptionAction` 等 record + `CsvParser`），用 EditMode 單元測試驗證 CSV → domain 轉換。
4. **實作 `CourseClient`**（HTTP wrapper），用真實 API（步驟 1 啟動的）測 ListCourses、GetCourse 兩條路徑。
5. **建 App.unity 場景**（依 §7.10 用 Unity MCP 建構），先做 ManualListView 跑通：API 拉清單 → 顯示卡片 → 點進入 → 切到 empty CourseView。
6. **實作 CourseView**：Step 切換、Breadcrumb、媒體載入（image / video）、例外按鈕。
7. **手動 QA**（依 §10 驗收條件逐項打勾）。
8. **打 APK 到 Quest 3** 實機驗證（Editor 模擬與實機常常有差，特別是 video streaming 與 World-Space Canvas 互動）。

每階段做完都應有可 demo 的成果，不要把所有東西做到一半才測。

> ⚠️ **此規格刻意不指定 namespace、檔案名、資料夾結構**，由實作者依新專案規範決定。功能對等即可。

---

## 13. 參考：原系統哪裡找什麼（給人類查證用）

> **正常情況下，看完這份文件就不用再翻舊 code。**
> 但如果新版實作出現「某個邊界情境似乎漏了」，可以對照下表回頭驗：

| 行為 | 舊原始碼位置 |
|---|---|
| CSV 解析（含引號跳脫） | `Assets/Scripts/Intelligent inspection/DownloadManager/TextDownload.cs` `GetCSV` |
| BOM 剝除 | `Assets/Scripts/Intelligent inspection/Interface UI/HandleUI.cs` `TappedReadText` |
| Step 結構建立 | `Assets/Scripts/Intelligent inspection/Interface UI/JsonUiStruct.cs` `UiStructure.AddStep` |
| 例外按鈕 goto vs message 判斷 | `Assets/Scripts/Intelligent inspection/Interface UI/JsonUiStruct.cs` `ExceptionHandlingOption` |
| 媒體載入（圖/影片） | `Assets/Scripts/Intelligent inspection/Interface UI/HandleLayer.cs` `PlayVidOrShowImg` |

---

**End of spec.**
