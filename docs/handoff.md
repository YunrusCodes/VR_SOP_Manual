# 交接給新 Claude Code session 的工作指示

> 這份文件是給**下一個** Claude Code session（在 Quest 3 新專案上）讀的。
> 人類會把它當作 onboarding 文件 paste 給新 session，或在新 session 第一句話就指它「先讀這份再動手」。

---

## 0. 一句話任務

**用 Quest 3 + Python FastAPI，依 [spec.md](spec.md) 重寫智慧巡檢瀏覽工具。要求：純線上薄客戶端、無 AR、無 TTS、無下載、無快取。**

---

## 1. 必讀檔案（依此順序）

1. [spec.md](spec.md) — 完整規格（約 750 行，30 分鐘可讀完）
2. [sample-data/README.md](sample-data/README.md) — 範例資料說明
3. [sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv](sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv) — 主範例 CSV
4. [sample-data/expected/engine-room-inspection.parsed.json](sample-data/expected/engine-room-inspection.parsed.json) — CsvParser 應產生的結果
5. [sample-data/expected/list-courses.json](sample-data/expected/list-courses.json) — `GET /courses` 應回的內容

讀完後你應該能回答：
- 系統架構是什麼？（單場景 + 多 View + 純線上 API）
- 4 個 API endpoints 各是什麼？
- 一個 step 的 13 個 CSV 欄位代表什麼？
- 例外按鈕的 goto vs message 怎麼判斷？
- 為什麼**沒有**下載/更新/刪除按鈕？

---

## 2. 不要做的事（從 spec.md §9 重申）

- ❌ AR / 空間錨點 / 手勢 — 不在範圍
- ❌ TTS / 語音朗讀 — 砍掉
- ❌ 下載 / 本地快取 — 砍掉，純線上
- ❌ 教師端編輯介面 — 不在範圍（教師直接放檔到後端 storage）
- ❌ 認證 / 登入 — 不做
- ❌ 多場景做 UI 切換 — 用單場景多 View

如果 spec 跟你訓練裡的「Unity VR app 最佳實踐」打架，**spec 為準**。

---

## 3. 環境前提

| 項目 | 版本/版本要求 |
|---|---|
| Unity | **6000.x LTS**（spec 假設新專案，不繼承舊 HoloLens 專案） |
| Build target | Android (Quest 3) |
| XR | Meta XR All-in-One SDK + OpenXR Plugin |
| 互動 | XR Interaction Toolkit (XRI) |
| .NET | Unity 內建（C# 9 + Unity 6 records 支援） |
| Python | 3.11+ |
| FastAPI | 最新 |
| Quest 3 | 已開啟開發者模式、ADB 可連 |

確認 Unity MCP 在這個 session 可用：
- `mcp__unity-mcp__Unity_RunCommand` — 執行任意 C# editor 程式碼
- `mcp__unity-mcp__Unity_GetConsoleLogs` — 讀錯誤
- `mcp__unity-mcp__Unity_SceneView_CaptureMultiAngleSceneView` — 截圖驗收

如果工具列裡看不到 `mcp__unity-mcp__*`，先請人類確認 Unity MCP server 已啟動且 Unity Editor 開著。

---

## 4. 建議的 repo 結構

新專案建議用 monorepo，把 docs 帶過來：

```
inspection-quest3/                      ← 新 git repo（與舊 HoloLens 專案分離）
├── docs/                               ← 從舊專案 docs/ 複製過來
│   ├── spec.md
│   ├── handoff.md                      ← 你正在看的檔
│   └── sample-data/
├── api/                                ← Python FastAPI
│   ├── main.py
│   ├── tests/
│   ├── storage/                        ← cp -r ../docs/sample-data/storage/* 到此
│   ├── pyproject.toml (或 requirements.txt)
│   └── README.md
└── unity/                              ← Unity 6 Quest 3 專案
    ├── Assets/
    ├── Packages/
    ├── ProjectSettings/
    └── ...
```

第一件事就是建這個結構，並 `git init`。

---

## 5. 工作順序與里程碑

### 階段 1：Python API（先做，因為它是 source of truth）

**Done 標準**：
- `cd api && uvicorn main:app --reload` 跑得起來
- `curl http://localhost:8000/healthz` 回 `{"status":"ok"}`
- `curl http://localhost:8000/companies/acme/courses` 內容與 [sample-data/expected/list-courses.json](sample-data/expected/list-courses.json) 一致
- `curl http://localhost:8000/companies/acme/courses/engine-room-inspection/csv` 拿到 CSV 內容
- `curl http://localhost:8000/companies/acme/courses/engine-room-inspection/files/image/engine-hood.jpg` 拿到 jpg（即使是 1×1 placeholder）
- `curl -H "Range: bytes=0-1023" .../files/video/startup.mp4` 回 206 Partial Content
- `pytest` 全綠

不要做：上傳端點、認證、manifest 端點、版本比對。

### 階段 2：Unity 純資料層（Editor mode 單元測試，不需要實機）

建立 asmdef `Inspection.Domain`，**不引用 UnityEngine**。

**Done 標準**：
- `Course` / `Step` / `Media` / `ExceptionAction` 等 record 寫好
- `CsvParser.Parse(csvText, "engine-room-inspection")` 能跑通，序列化結果與 [sample-data/expected/engine-room-inspection.parsed.json](sample-data/expected/engine-room-inspection.parsed.json) 結構一致
- EditMode test：`[Test] public void Parse_SampleCsv_MatchesExpectedJson()` 綠燈

### 階段 3：Unity App 場景（用 Unity MCP 建構）

照 [spec.md §7.10](spec.md) 的 7 個步驟，用 `Unity_RunCommand` 把 `App.unity` 拼出來：

1. 建空場景
2. Persistent root（XR Origin + EventSystem + Services）
3. RootCanvas + 三個 View（ManualListView / CourseView / LoadingOverlay）
4. CourseCard / ExceptionButton prefab
5. AppSettings.asset
6. SerializedObject 串引用
7. SaveScene

每做完一步就用 `Unity_SceneView_CaptureMultiAngleSceneView` 截圖回報，避免做完 7 步發現第 2 步搞錯。

**Done 標準**：
- `App.unity` 開啟無 missing reference
- 在 Editor 內按 Play，AppBootstrapper 不會 NRE
- LoadingOverlay 顯示 → 消失（如果 API 有跑）
- ManualListView 顯示出兩張卡片（acme 底下兩門課）

### 階段 4：UI 行為串接（依 spec.md §7.6 / §7.7 / §7.8）

- ManualListView 卡片點 [進入] → 切到 CourseView
- CourseView 顯示第 1 步 + 媒體
- 上一步/下一步 / 例外按鈕全部跑通

**Done 標準**：spec.md §10 驗收條件 10.1 與 10.2 全綠。

### 階段 5：Build to Quest 3 實機

- Switch platform → Android、ARM64、Vulkan
- XR Plug-in Management → Android → OpenXR + Meta Quest support
- Build APK
- `adb install -r build.apk`
- 戴上頭盔測試

**Done 標準**：spec.md §10 三大塊全部 ✓。

---

## 6. Unity MCP 使用心法

### 安全使用 `Unity_RunCommand`

`Unity_RunCommand` 執行的是 Editor context 的 C# 程式碼。把它當作 Editor 自動化腳本：

- 每次只做一件可驗證的事，不要一次 100 行
- 每段 command 結尾呼叫 `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()`
- 改完場景內容呼叫 `EditorSceneManager.MarkSceneDirty(scene)` 再 `SaveScene`
- 動 Asset 後常需要 `CompilationPipeline.RequestScriptCompilation()` 重編
- 失敗就先 `Unity_GetConsoleLogs` 看真實錯誤

### 建場景的順序很重要

先確保所有 MonoBehaviour script 已存在且編譯通過，才能 `gameObject.AddComponent<MyComponent>()`。否則 Unity 會丟 "Script reference is missing"。

建議流程：
1. 寫 C# script 到 `Assets/Scripts/`（用 `File.WriteAllText`）
2. 等編譯完成（`CompilationPipeline.RequestScriptCompilation()` + 等 ApplicationDomain reload）
3. 才開始 `AddComponent` / `SerializedObject` 串引用

### 用截圖驗收

每完成一個視覺里程碑（建好 Canvas、放好 ManualListView、放好 CourseView），都呼叫一次：

```
mcp__unity-mcp__Unity_SceneView_CaptureMultiAngleSceneView
```

把截圖回報給人類。比起一直問「我這樣對嗎？」，截圖是最有效的同步方式。

---

## 7. 出問題該怎麼辦

| 狀況 | 怎麼辦 |
|---|---|
| 規格寫得不夠清楚 | 用 sample-data 反推；還不夠就問人類，**不要憑感覺自己決定關鍵業務邏輯**（例外按鈕的 goto/message 判斷、CSV 欄位順序這類） |
| 規格與真實 SDK 衝突 | 例：spec 說用 `XRI Ray Interactor` 但你發現 Meta Building Blocks 更合適 — 先講出來、提建議、等人類點頭，再改 |
| Unity MCP 失靈 | 改用 Bash + Edit 把 .unity 當 YAML 檔手動寫；但這很容易壞 GUID，最好還是修好 MCP |
| Quest 連不上 | 確認 ADB devices 看得到、`adb tcpip 5555` 設好、Quest 開發者模式開著 |
| 影片在 Quest 上播不出來 | 大機率是 `videoPlayer.url` 沒帶 `http://` 前綴、或 mp4 不是 faststart |

---

## 8. 第一句該回什麼

讀完上面所有檔案後，你的第一句回答應該包含：

1. 確認你看完了 spec + sample-data
2. 摘要你理解的「要做什麼 / 不做什麼」（兩三句）
3. 提出**一個**最重要的開放問題（如果有）
4. 提議從階段 1（Python API）開始

不要：
- 大段重複 spec 內容
- 一上來就開始改 Unity（先做 API）
- 跳過讀範例資料
