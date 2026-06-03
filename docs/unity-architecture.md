# VR SOP Manual — Unity 端架構與接手指南

> 適用對象：第一次拿到本專案、要在 Unity 端維護或擴充功能的工程師。
> 範圍：`Assets/Scripts/` 下的所有 C#。後端 (`api/`) 與場景組裝 (`Assets/Editor/SceneBuilder.cs`) 僅在必要處提及。
> 對應實作：[Assets/Scripts/](../Assets/Scripts/)。

---

## 0. 一頁總覽

- **平台**：Unity 6000.2.6f2 + URP + XR Interaction Toolkit 3.5，目標機 Quest 3。Editor 內以 XR Interaction Simulator 模擬。
- **程式風格**：C# 9 records + sealed pattern hierarchies（`Media` / `ExceptionAction`）、`async/await`、建構式注入、無第三方 DI 容器。
- **分層**：`App → UI → Net → Domain` 單向依賴，`Domain` 完全不參考 `UnityEngine`（強制由 [Inspection.Domain.asmdef](../Assets/Scripts/Domain/Inspection.Domain.asmdef) 的 `noEngineReferences: true` 把關）。
- **資料來源**：FastAPI 後端的 CSV + 圖檔/影片，純線上、不快取。網路層僅四個端點。
- **入口**：場景 `Assets/Scenes/App.unity` → `AppBootstrapper.Awake()`。

---

## 1. 代碼架構

### 1.1 目錄與命名空間

```
Assets/Scripts/
├── Domain/            namespace Inspection.Domain   (純 .NET，零 Unity 依賴)
│   ├── Course.cs                 Course / CourseSummary / Step / Media / ExceptionOption / ExceptionAction
│   ├── CsvParser.cs              RFC 4180-ish CSV reader + 欄位解析
│   ├── IsExternalInitShim.cs     讓 record init-only setter 能在 Unity 裡編
│   └── Inspection.Domain.asmdef  noEngineReferences = true
│
├── App/               namespace Inspection.App
│   ├── AppSettings.cs            ScriptableObject (ApiBaseUrl / Company / VerboseLog)
│   ├── AppBootstrapper.cs        場景組合根 — 唯一 new CourseClient() 的地方
│   └── Logger.cs                 Log.V/I/W/E 包裝
│
├── Net/               namespace Inspection.Net
│   ├── ICourseClient.cs          抽象介面 (UI 只認它)
│   ├── CourseClient.cs           UnityWebRequest 實作 + 手寫迷你 JSON parser
│   └── ImageLoader.cs            UnityWebRequestTexture 非同步載圖
│
├── UI/                namespace Inspection.UI       (全部 MonoBehaviour)
│   ├── AppRouter.cs              兩個畫面切換 (清單 ↔ 課程)
│   ├── ManualListView.cs         課程清單頁
│   ├── CourseCard.cs             清單裡的單一卡片 prefab
│   ├── CourseView.cs             課程內步驟頁 (本專案最大的 view)
│   ├── ExceptionButton.cs        例外按鈕 prefab
│   ├── OutlinePanel.cs           大綱抽屜 (執行時動態 spawn 列)
│   ├── LoadingOverlay.cs         全域 loading / message 浮層
│   └── SettingsPanel.cs          執行時改 API base URL
│
├── Debug/             namespace Inspection.Debugging   (#if UNITY_EDITOR)
│   ├── QACourseWalker.cs         自動走完一個課程並擷圖
│   ├── VRMultiCourseWalker.cs    在 VR 模擬下走完多門課
│   ├── VROutlineWalker.cs        驗證 OutlinePanel 跳步功能
│   ├── QASnapshot.cs             把 main camera 拍成 PNG
│   ├── LiveMonitor.cs            執行時把 view 狀態印到 console
│   └── VRCanvasPinner.cs         確保 World Space canvas 不亂飄
│
└── Inspection.Runtime.asmdef     參考 Inspection.Domain + TMP/InputSystem/XRI/URP
```

### 1.2 分層依賴圖

```
┌────────────────────────────────────────────────────────────┐
│  App                                                       │
│    AppBootstrapper ── new ─→ CourseClient                  │
│         │                                                  │
│         │ Init(client, router, overlay)                    │
│         ▼                                                  │
└────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────┐
│  UI            (依賴 Net 透過 ICourseClient，依賴 Domain)  │
│    AppRouter ◄──► ManualListView ─┐                        │
│                   CourseView ─────┤  使用 ICourseClient    │
│                   SettingsPanel ──┘                        │
└────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────┐
│  Net           (依賴 Domain；用 UnityWebRequest)           │
│    ICourseClient ◄── CourseClient   ImageLoader            │
└────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────┐
│  Domain        (純 .NET，可被任何環境 unit test)          │
│    Course / Step / Media{None|Image|Video}                 │
│    ExceptionAction{GoToStep|ShowMessage} / CsvParser       │
└────────────────────────────────────────────────────────────┘
```

依賴箭頭只往下，**Domain 對其他層完全沒概念**。這是 EditMode 測試 ([Assets/Tests/EditMode/](../Assets/Tests/EditMode/)) 能脫離 Unity 跑的關鍵。

### 1.3 設計原則速記

| 原則 | 在哪裡實踐 |
|---|---|
| 介面隔離 (UI 不認得具體 client) | [ICourseClient.cs](../Assets/Scripts/Net/ICourseClient.cs) |
| 建構式注入 (View 不自己 Find) | [AppBootstrapper.Awake()](../Assets/Scripts/App/AppBootstrapper.cs#L28) |
| Closed type hierarchy + switch pattern | `Media` / `ExceptionAction` 在 [Course.cs:23-36](../Assets/Scripts/Domain/Course.cs#L23-L36) |
| Cancellation token 串穿整條 async 路徑 | [CourseView.UpdateMedia()](../Assets/Scripts/UI/CourseView.cs#L118)、[ManualListView.RefreshAsync()](../Assets/Scripts/UI/ManualListView.cs#L45) |
| 執行時可改 API URL（不用重 build APK） | [SettingsPanel](../Assets/Scripts/UI/SettingsPanel.cs) + [AppBootstrapper.UpdateApiBaseUrlAsync](../Assets/Scripts/App/AppBootstrapper.cs#L71) |

---

## 2. 使用者操作呼叫流程

### 2.1 應用啟動 (Awake)

```
[Unity 進場景 App.unity]
        │
        ▼
AppBootstrapper.Awake()
  ├─ 讀 PlayerPrefs("Inspection.ApiBaseUrl") 或 fallback AppSettings.ApiBaseUrl
  ├─ TryBuildClient(url) → new CourseClient(baseUrl, company, CsvParser)
  ├─ manualList.Init(client, router, overlay)
  ├─ courseView.Init(client, router, overlay)
  ├─ settingsPanel.Init(UpdateApiBaseUrlAsync); settingsPanel.Hide()
  ├─ settingsButton.onClick → settingsPanel.Show(currentUrl)
  ├─ router.ShowManualList()
  ├─ overlay.Show("載入課程清單…")
  └─ await manualList.RefreshAsync()  → finally overlay.Hide()
```

### 2.2 載入課程清單

```
ManualListView.RefreshAsync()
  ├─ ClearChildren(contentRoot)
  ├─ _cts = new CancellationTokenSource()
  ├─ courses = await _client.ListCoursesAsync(_cts.Token)
  │     └─ CourseClient → GET /companies/{c}/courses
  │           └─ ParseCourseList(json)  // 手寫 JSON
  ├─ foreach course → Instantiate(courseCardPrefab) → card.Bind(displayName, OnEnter)
  └─ catch ApiException → SetError("無法載入…")
```

### 2.3 點選課程 → 進入步驟頁

```
[user 用 VR ray 點 CourseCard]
        │
        ▼
CourseCard.cardBtn / enterButton.onClick
  → ManualListView.OnEnterAsync(summary)
      ├─ overlay.Show("載入課程：…")
      ├─ course = await _client.GetCourseAsync(summary.Name, ct)
      │     └─ CourseClient
      │           ├─ ListCoursesAsync (找 displayName)
      │           ├─ GET /companies/{c}/courses/{name}/csv
      │           └─ CsvParser.Parse(csvText, name, displayName) → Course
      ├─ _router.ShowCourse(course)
      │     ├─ manualList.gameObject.SetActive(false)
      │     └─ courseView.Bind(course); courseView.gameObject.SetActive(true)
      └─ overlay.Hide()

CourseView.Bind(course)
  └─ ShowStepAt(0)
        ├─ 更新 breadcrumb / stepCounter / stepName / description / nextIndication
        ├─ UpdateMedia(step.Media)
        │     ├─ _mediaCts.Cancel(); 重新 new
        │     ├─ leftColumn.anchorMax.x = (Media.None ? 1 : 0.6)
        │     ├─ Media.Image → LoadImageAsync(fileName, ct)
        │     │     └─ ImageLoader.LoadAsync → GET /files/image/{name}
        │     └─ Media.Video → videoPlayer.url = client.GetVideoUrl(...); Play()
        ├─ UpdateExceptions(step) → spawn ExceptionButton 們
        └─ prev/next button.interactable 依 index 設定
```

### 2.4 步驟內互動

| 動作 | 路徑 |
|---|---|
| 下一步 | `nextStepButton.onClick → OnNext → ShowStepAt(_currentIndex + 1)` |
| 上一步 | `prevStepButton.onClick → OnPrev → ShowStepAt(_currentIndex - 1)` |
| 例外 → 跳步 | `ExceptionButton → OnExceptionPressed(opt) → switch case GoToStep → GoToStepOrder(order) → ShowStepAt(matchedIndex)` |
| 例外 → 顯示訊息 | `→ switch case ShowMessage → overlay.ShowMessage(text)`（這時 overlay 進「訊息模式」帶關閉按鈕） |
| 開大綱 | `outlineToggleButton → OnToggleOutline → outlinePanel.Bind(course, currentOrder); Show()` |
| 大綱跳步 | `OutlinePanel StepRow.button → onJump(order) → CourseView.GoToStepOrder(order); Hide()` |
| 回課程清單 | `backToListButton → OnBackToList → CleanupMedia() → router.ShowManualList()` |
| 改 API URL | `settingsButton → settingsPanel.Show → user 編輯 → OnSaveClicked → AppBootstrapper.UpdateApiBaseUrlAsync → 重建 client → 重新 Init view → manualList.RefreshAsync` |

### 2.5 完整 Sequence（user 第一次進 App，跳一個例外，回清單）

```
User      Bootstrapper   ManualList   CourseView   Client    FastAPI
 │  Play       │             │             │          │         │
 │ ───────────►│             │             │          │         │
 │             │  Init      │             │          │         │
 │             │ ───────────►│  Init       │          │         │
 │             │ ────────────────────────►│           │         │
 │             │  Refresh    │             │          │         │
 │             │ ───────────►│             │          │         │
 │             │             │  ListCourses│          │         │
 │             │             │ ────────────┴────────►│ GET /courses
 │             │             │ ◄───────────┴────────│  JSON   │
 │             │             │  spawn cards          │         │
 │  click card │             │             │         │         │
 │ ────────────┴────────────►│             │         │         │
 │             │             │  GetCourse  │         │         │
 │             │             │ ────────────┴───────►│ GET /csv │
 │             │             │ ◄───────────┴───────│  CSV    │
 │             │             │  Router.ShowCourse  │         │
 │             │             │ ──────────►│         │         │
 │             │             │            │ Bind / ShowStepAt │
 │             │             │            │ LoadImageAsync ─►│ GET /image
 │  next       │             │            │         │         │
 │ ────────────┴─────────────┴───────────►│         │         │
 │  exception (GoToStep 8)                │         │         │
 │ ────────────┴─────────────┴───────────►│         │         │
 │             │             │            │ GoToStepOrder(8)  │
 │  back       │             │            │         │         │
 │ ────────────┴─────────────┴───────────►│ Router.ShowManualList
```

---

## 3. 資料流

### 3.1 後端到 Domain 物件

```
┌──────────────────────────────────────────┐
│ FastAPI storage/{company}/{course}/      │
│   ├─ {course}.csv  (RFC 4180, 13 欄)     │
│   ├─ Image/*.jpg                         │
│   └─ Video/*.mp4                         │
└──────────────────────────────────────────┘
              │
              ▼ HTTP GET
┌──────────────────────────────────────────┐
│ CourseClient.GetCourseAsync              │
│   ├─ ListCoursesAsync (取 displayName)   │
│   └─ csv = await GetStringAsync(...)     │
└──────────────────────────────────────────┘
              │
              ▼ string (csvText)
┌──────────────────────────────────────────┐
│ CsvParser.Parse                          │
│   ├─ ReadRows  (RFC 4180-ish)            │
│   ├─ row[0][0] → introduction            │
│   ├─ row[1] → header (ignored)           │
│   └─ for r in row[2..]                   │
│        ParseStep                         │
│         ├─ col0  Order        int        │
│         ├─ col1  MainTitle    string     │
│         ├─ col2  SubTitle     string?    │
│         ├─ col3  Name         string     │
│         ├─ col4  Description  string     │
│         ├─ col5  Media        ParseMedia │
│         ├─ col6  NextStepIndication      │
│         └─ col7..12  3×(Label,Action)    │
└──────────────────────────────────────────┘
              │
              ▼ Domain record
┌──────────────────────────────────────────┐
│ Course {                                 │
│   Name, DisplayName, Introduction,       │
│   Steps: [Step { Order, MainTitle,       │
│            SubTitle, Name, Description,  │
│            Media, NextStepIndication,    │
│            Exceptions: [ExceptionOption  │
│              { Label, Action: GoToStep   │
│                          | ShowMessage } │
│            ] }, ...]                     │
│ }                                        │
└──────────────────────────────────────────┘
```

### 3.2 CSV 欄位對照（給編 CSV 的人）

| 欄位 | 名稱 | 型別 | 範例 | 解析 |
|---|---|---|---|---|
| 0 | Step Order | int | `8` | `int.TryParse`，失敗則 0 並 warn |
| 1 | Main Title | string | `啟動前` | 直接存 |
| 2 | Sub Title | string? | `引擎蓋` | 空字串 → null |
| 3 | Name | string | `開啟引擎蓋` | 直接存 |
| 4 | Description | string | (任意，可含換行/逗號) | RFC 4180 引號處理 |
| 5 | Media | string | `step01.jpg` / `intro.mp4` / 空 | 副檔名分流 (`jpg/jpeg/png`→Image, `mp4`→Video, 其他→None) |
| 6 | Next Step Indication | string? | `按下解鎖鈕` | 空 → null（UI 不顯示該行） |
| 7,8 | Exception 1 Label / Action | string, string | `油位過低` / `8` 或 `請聯絡技師` | 純數字 → `GoToStep(N)`；其他 → `ShowMessage(text)` |
| 9,10 | Exception 2 | 同上 | | |
| 11,12 | Exception 3 | 同上 | | |

### 3.3 媒體載入資料流

```
Step.Media
  │
  ├─ Media.None  → mediaPanel.SetActive(false); leftColumn 占滿 1.0
  │
  ├─ Media.Image(fileName)
  │     └─ url = client.GetImageUrl(courseName, fileName)
  │         = {baseUrl}/companies/{c}/courses/{course}/files/image/{file}
  │         └─ ImageLoader.LoadAsync (UnityWebRequestTexture)
  │             └─ Texture2D → imageView.texture
  │                 (步驟切換時 _currentTexture 會被 Destroy 釋放)
  │
  └─ Media.Video(fileName)
        └─ url = client.GetVideoUrl(...)
            └─ videoPlayer.source = Url; videoPlayer.url = ...; Play()
              (BackEnd Range 請求支援，由 FastAPI 處理 stream)
```

**重要**：每次 `UpdateMedia` 會先 `_mediaCts.Cancel()` 中斷上一次 in-flight 的圖片下載，並 `Destroy` 上一張 `Texture2D`，避免 GPU 記憶體在 VR 中緩慢膨脹。

### 3.4 例外動作資料流

```
ExceptionOption.Action (Closed type)
  │
  ├─ ExceptionAction.GoToStep(Step)
  │     → CourseView.GoToStepOrder(order)
  │       → 找到 _course.Steps 裡 Order==order 的 index → ShowStepAt(idx)
  │
  └─ ExceptionAction.ShowMessage(Text)
        → overlay.ShowMessage(text)
          → label.text = text; closeButton 顯示; overlay 顯示
```

### 3.5 設定 (API URL) 資料流

```
PlayerPrefs ("Inspection.ApiBaseUrl")
   ▲                         │
   │ Save                    │ Read in EffectiveApiBaseUrl
   │                         ▼
SettingsPanel.OnSaveClicked  AppBootstrapper.Awake / .UpdateApiBaseUrlAsync
   │                         │
   └────► UpdateApiBaseUrlAsync(url)
              ├─ trim/normalize
              ├─ PlayerPrefs.SetString + Save
              ├─ TryBuildClient(url) → new CourseClient
              ├─ manualList.Init / courseView.Init   ← 重新注入
              └─ overlay.Show; await manualList.RefreshAsync; overlay.Hide
```

---

## 4. UML

### 4.1 Domain 類別圖（純資料）

```
                          ┌────────────────┐
                          │ <<record>>     │
                          │ Course         │
                          │ ───────────── │
                          │ Name          │
                          │ DisplayName   │
                          │ Introduction  │
                          │ Steps : IList │
                          └──────┬────────┘
                                 │ 1..*
                                 ▼
                          ┌────────────────┐
                          │ <<record>>     │
                          │ Step           │
                          │ ───────────── │
                          │ Order : int   │
                          │ MainTitle     │
                          │ SubTitle?     │
                          │ Name          │
                          │ Description   │
                          │ NextStepIndication?
                          │ Media         │
                          │ Exceptions    │
                          └──┬──────┬─────┘
              0..1            │      │ 0..3
              ▼               │      ▼
     ┌────────────────┐       │   ┌──────────────────┐
     │ <<abstract>>   │       │   │ <<record>>       │
     │ Media          │       │   │ ExceptionOption  │
     │ ────────────── │       │   │ ──────────────── │
     │ + None         │       │   │ Label            │
     │ + Image(file)  │       │   │ Action           │
     │ + Video(file)  │       │   └──────┬───────────┘
     └────────────────┘       │          │
                              │          ▼
                              │   ┌──────────────────────┐
                              │   │ <<abstract>>         │
                              │   │ ExceptionAction      │
                              │   │ ──────────────────── │
                              │   │ + GoToStep(int)      │
                              │   │ + ShowMessage(text)  │
                              │   └──────────────────────┘
                              │
                              ▼
                       ┌────────────────┐
                       │ CourseSummary  │  (列清單時用，純 Name + DisplayName)
                       └────────────────┘
```

### 4.2 Runtime 元件 / 依賴圖

```
┌──────────────────┐         ┌──────────────────┐
│ AppSettings      │         │ MEvent (Unity)    │
│ (ScriptableObj)  │         │ EventSystem +     │
└──────┬───────────┘         │ XR Ray Interactor │
       │ ApiBaseUrl/Company  └─────────┬─────────┘
       │                               │ pointer events
       ▼                               ▼
┌────────────────────────┐    ┌────────────────────────┐
│ AppBootstrapper        │───►│ AppRouter              │
│ ─────────────────────  │    │ ────────────────────── │
│ - Client : ICourseClient│   │ + ShowManualList()     │
│ + UpdateApiBaseUrlAsync│    │ + ShowCourse(course)   │
└──────┬─────────────────┘    └────────────────────────┘
       │ Init                       ▲
       │                            │
       ▼                            │
┌──────────────────┐  ┌──────────────────────────────┐
│ ManualListView   │  │ CourseView                   │
│ ──────────────── │  │ ──────────────────────────── │
│ + Init           │  │ + Init / Bind                │
│ + RefreshAsync   │  │ - ShowStepAt(i)              │
│ - OnEnterAsync   │  │ - UpdateMedia(media)         │
└─────┬───────┬────┘  │ - UpdateExceptions(step)     │
      │spawn  │       │ - GoToStepOrder(order)       │
      ▼       ▼       │ + TestNext / TestPrev / ...  │
┌──────────┐ ┌──────┐ └────┬─────────┬─────────┬─────┘
│CourseCard│ │Empty │      │ spawn   │         │
│  prefab  │ │State │      ▼         ▼         ▼
└──────────┘ └──────┘ ┌──────────┐ ┌─────────┐ ┌────────────┐
                      │Exception │ │Outline  │ │LoadingOver │
                      │ Button   │ │ Panel   │ │   lay      │
                      │ prefab   │ │         │ │            │
                      └──────────┘ └─────────┘ └────────────┘

                   ICourseClient
                        ▲
                        │ implements
                ┌───────────────┐
                │ CourseClient  │ ── uses ──► UnityWebRequest
                │               │ ── uses ──► CsvParser (Domain)
                └───────────────┘
                        │
                  ┌─────────────────┐
                  │ ImageLoader     │ ── uses ──► UnityWebRequestTexture
                  │ (static)        │
                  └─────────────────┘
                        │ throws
                        ▼
                ┌───────────────┐
                │ ApiException  │
                └───────────────┘
```

### 4.3 主要狀態機（CourseView）

```
                ┌──────────────┐
                │  Inactive    │ ◄────────── OnDisable / OnBackToList
                └──────┬───────┘                  ▲
                       │ Bind(course)             │
                       ▼                          │
                ┌──────────────┐                  │
        ┌──────►│  ShowingStep │ ─── back ────────┘
        │       └──┬───┬───┬──┘
        │          │   │   │
        │     next │   │ prev   exception(GoToStep)
        │          ▼   │       ▼
        │       ShowStepAt(i±1) / GoToStepOrder
        │
        │     exception(ShowMessage)
        │          ▼
        │       overlay.ShowMessage   (CourseView 仍在 ShowingStep，
        │                              overlay 是平行的 UI 層)
        │
        │     OutlineToggle
        │          ▼
        └─── OutlinePanel.Bind/Show → 點 row → GoToStepOrder + Hide
```

---

## 5. 詳解專案代碼

以下逐檔說明關鍵點。**只挑「為什麼這樣寫」非顯而易見之處**；單純 getter/setter 不重複。

### 5.1 Domain 層

#### [Course.cs](../Assets/Scripts/Domain/Course.cs)

```csharp
public sealed record Course(string Name, string DisplayName,
                            string Introduction, IReadOnlyList<Step> Steps);
```
- 全部 `record` 是因為它們是值物件，UI 更新時直接整個換掉、不會 mutate。
- `Media` / `ExceptionAction` 用 abstract record + nested sealed record 形成 closed hierarchy；配合 [CourseView.OnExceptionPressed()](../Assets/Scripts/UI/CourseView.cs#L205) 的 `switch` pattern matching，**新增分支時編譯器會逼你補處理**。
- `IReadOnlyList<>` 不用 `List<>` 是為了讓上層拿到後沒辦法不小心改動。

#### [CsvParser.cs](../Assets/Scripts/Domain/CsvParser.cs)

- `ReadRows` 是手寫 RFC 4180 reader，**支援引號內換行、`""` 跳脫雙引號、CRLF/LF**。Unity 沒內建 CSV，而我們不想拉 CsvHelper 進來只為這一個檔。
- `ExpectedColumns = 13` 是硬性契約：1 個 order + 5 個基本欄 + 1 個 next + 3×2 個例外 = 13。少於 13 仍嘗試解析（用 `Get(i)` 防越界），但會 `_warn`。
- BOM 剝除（檔頭 `﻿`）：FastAPI 後端用 utf-8-sig 寫檔時會帶 BOM；不剝會讓 col0 第一筆變成 `﻿1` 而 `int.TryParse` 失敗。
- `WarningHandler` delegate 是為了讓 `Log.W` 注入但 Domain 仍不參考 UnityEngine（[`AppBootstrapper.TryBuildClient`](../Assets/Scripts/App/AppBootstrapper.cs#L89) 傳 `Log.W`）。
- `ParseAction`：純數字 → `GoToStep`，其他 → `ShowMessage`。**沒有第三種**——這就是 closed type 的好處。

#### [IsExternalInitShim.cs](../Assets/Scripts/Domain/IsExternalInitShim.cs)

C# 9 record 的 init-only setter 需要 `System.Runtime.CompilerServices.IsExternalInit`。Unity 6 BCL 沒給，這個檔提供 internal 同名 stub，型別系統就滿足了。**任何專案要在 Unity 用 record 都需要這招**。

### 5.2 Net 層

#### [ICourseClient.cs](../Assets/Scripts/Net/ICourseClient.cs)

只 4 個方法：
- `ListCoursesAsync` — 取 CourseSummary 清單
- `GetCourseAsync` — 取單一課程（含 csv 解析）
- `GetImageUrl` / `GetVideoUrl` — 純拼 URL，不下載（圖在 ImageLoader、影片在 VideoPlayer 各自處理）

UI 全部只認這個介面。要做離線/假資料 demo 只要實作另一個 class 就行。

#### [CourseClient.cs](../Assets/Scripts/Net/CourseClient.cs)

- `GetStringAsync`：把 `UnityWebRequest` 包成 `Task<string>`，用 `await Task.Yield()` 在主 thread 上 spin（UnityWebRequest 必須在 main thread）；同時檢查 `CancellationToken`，被 cancel 時呼叫 `req.Abort()`。**這個 pattern 在 Unity 用 await 很常見**。
- `ParseCourseList`：手寫 JSON parse 是因為 `UnityEngine.JsonUtility` 不支援 `IList<T>` 根層、Newtonsoft 又是 ~2MB 額外依賴；只解析 `{"courses":[{"name","displayName"},...]}` 這一種固定 shape，不通用，但夠了。
- `GetCourseAsync` 為了拿 `displayName` **多打一次 ListCourses**。如果以後 `/csv` endpoint 直接回 displayName 可以拿掉這次往返。
- `Esc(s)` = `UnityWebRequest.EscapeURL`，避免課程名含中文/空白炸 URL。
- `ApiException` 帶 `Url` + `StatusCode`，UI 層可以判斷 4xx/5xx 給不同訊息（目前 UI 沒分流，全部當作「載入失敗」）。

#### [ImageLoader.cs](../Assets/Scripts/Net/ImageLoader.cs)

跟 `GetStringAsync` 同 pattern，只是用 `UnityWebRequestTexture.GetTexture` 並回 `Texture2D`。**呼叫端負責 `Destroy` 這張 texture**——CourseView 用 `_currentTexture` 持有並在切換時釋放。

### 5.3 App 層

#### [AppSettings.cs](../Assets/Scripts/App/AppSettings.cs)

`ScriptableObject`，存 ApiBaseUrl / Company / VerboseLog。Editor 內改 `Assets/Settings/AppSettings.asset` 就能換目標機，但 runtime 改 URL 走的是 PlayerPrefs（見 5.4）。

#### [AppBootstrapper.cs](../Assets/Scripts/App/AppBootstrapper.cs)

整個 app 唯一的「組合根」（composition root）。
- `EffectiveApiBaseUrl`：先看 `PlayerPrefs("Inspection.ApiBaseUrl")`，沒設才用 `AppSettings.ApiBaseUrl`。**這讓使用者在 Quest 上用 SettingsPanel 改了之後，重啟 app 仍然記得**。
- `Awake` 為什麼是 `async void`：Unity 的 lifecycle method 沒有 async overload，`async void` 是這裡可接受的例外（不會被 await）。
- `TryBuildClient`：try/catch 把 `CourseClient` 建構參數驗證的例外包成 false，避免整個 app 啟動失敗就停在黑屏。
- `UpdateApiBaseUrlAsync` 重建 client 之後**重新 Init view**——因為 view 內有 `_client` 欄位是 by reference 拿來儲存的；不重 init 會繼續用舊的。

#### [Logger.cs](../Assets/Scripts/App/Logger.cs)

四個靜態方法 `V/I/W/E` + 全域 `Verbose` flag。Verbose 只在 V 層級判斷，I/W/E 永遠輸出。前綴 `[Inspection]` 方便在 logcat / Editor Console grep。

### 5.4 UI 層

#### [AppRouter.cs](../Assets/Scripts/UI/AppRouter.cs)

最簡單的「兩頁切換」router：直接 `SetActive(true/false)` 兩個 GameObject。沒做 navigation stack（不需要）。`ShowCourse` 順手 call `courseView.Bind(course)`，所以呼叫端寫起來只是 `router.ShowCourse(course)` 一行。

#### [ManualListView.cs](../Assets/Scripts/UI/ManualListView.cs)

- `Init` 接收依賴，並把 refresh button 接上 `RefreshWithOverlayAsync`（顯示 overlay → 呼叫 `RefreshAsync` → 隱藏）。`Awake` 啟動的第一次 refresh 因為 `AppBootstrapper` 自己會包 overlay，所以走的是 `RefreshAsync`（不重複包）。
- `OnEnterAsync` 用了 `CancellationToken.None`：course 的下載不可中途取消（會半載入卡死），跟 list 重整可中斷不一樣。
- `OnDisable` cancel `_cts`：避免被 router 關掉之後 in-flight refresh 還回來灌 UI。

#### [CourseCard.cs](../Assets/Scripts/UI/CourseCard.cs)

VR 體驗的小花招：[Bind()](../Assets/Scripts/UI/CourseCard.cs#L14) 不只接小小的「進入」按鈕，**還在 card root 自動加一個 `Button`，整張卡片都可被 ray 點**——VR 用 raycast 瞄 160px 按鈕很痛苦，整張卡可點才符合預期。

#### [CourseView.cs](../Assets/Scripts/UI/CourseView.cs)

最大的 view，重點：

- **Cancellation token 有兩條獨立路徑**：`_mediaCts` 只管 image 載入，跟 ManualListView 的 `_cts` 無關。每次 `UpdateMedia` 會 cancel 上一次。
- **左欄寬度動態調整**（[CourseView.cs:138-143](../Assets/Scripts/UI/CourseView.cs#L138-L143)）：沒媒體時把 `leftColumn.anchorMax.x` 從 0.6 拉到 1.0，避免「右邊一塊死區」（VR 中尤其明顯，使用者會以為 UI 壞了）。
- **`_currentTexture` 釋放**：`UpdateMedia` 開頭一定把上一張 texture `Destroy`、把 `imageView.texture` 清掉。VR 上忘記做這件事是常見記憶體洩漏來源。
- **`OnDisable` → `CleanupMedia`**：router 切走時，影片要停、texture 要丟、cts 要 cancel。
- **`Test*` public 方法**：給 [QACourseWalker](../Assets/Scripts/Debug/QACourseWalker.cs) 等自動化測試走訪用，繞過 UI 點擊直接驅動狀態（`onClick.Invoke` 在某些情況不可靠，註解裡有寫）。
- **`GoToStepOrder` 用 Order 而不是 index**：CSV 編寫者可能跳號（例如 step 7 異常 → 跳 step 12），所以例外動作存的是 `Order` 不是 `index`。CourseView 自己掃陣列翻譯。

#### [OutlinePanel.cs](../Assets/Scripts/UI/OutlinePanel.cs)

- 動態 spawn 三層：MainTitle header → SubTitle header → Step row。前一個值用 `lastMain` / `lastSub` 追蹤，避免重複輸出。
- `LayoutRebuilder.ForceRebuildLayoutImmediate` 是必須的：`VerticalLayoutGroup` 在同一 frame 加完所有 child 後不會立刻 layout，畫面會閃爍空白一拍。
- 整個 panel 是程式 spawn UI（`new GameObject(...)`），不走 prefab，因為 row 數量隨課程變、層級結構簡單，這樣比較好維護。
- 當前步驟的 row 會用 `currentStepOrder == step.Order` 做高亮（金色 + Bold）。

#### [LoadingOverlay.cs](../Assets/Scripts/UI/LoadingOverlay.cs)

雙模式：
- `Show(text)` — 純 loading，無關閉鈕。給「載入中」用。
- `ShowMessage(text)` — 帶關閉鈕。給例外動作 `ShowMessage` 用，user 看完按關閉。

兩者都同一個 GameObject，靠 `closeButtonRoot.SetActive` 切。

#### [SettingsPanel.cs](../Assets/Scripts/UI/SettingsPanel.cs)

- `Init` 收一個 `Func<string, Task>`（= `AppBootstrapper.UpdateApiBaseUrlAsync`），這樣 panel 不認得 bootstrapper、也不認得 client，純 UI。
- `OnSaveClicked` 自動補 `http://` 前綴（user 常輸入只有 IP）。
- `async void` + `await _onSave(url)` 是 UI 點擊處理器的標準寫法（同 Awake）。

#### [ExceptionButton.cs](../Assets/Scripts/UI/ExceptionButton.cs)

最簡單的 binder：把 label 跟 onClick 接上、覆蓋舊 listener。`RemoveAllListeners` 是因為 prefab 復用時會疊加。

### 5.5 Debug / QA 層

只在 `#if UNITY_EDITOR` 編譯，**正式 build 不進**。給 MCP-driven 自動測試或人工 walkthrough 用：

- [QACourseWalker.cs](../Assets/Scripts/Debug/QACourseWalker.cs)：runtime 動態加 component → 等指定卡片出現 → 自動 click → 走完所有步驟並擷圖 → log `[QA] WALKER DONE`。`SerializedObject` 取私有欄位是為了不開 public 也能拿 button reference。
- [VRMultiCourseWalker.cs / VROutlineWalker.cs](../Assets/Scripts/Debug/)：在 VR 模擬環境下走多門課 / 驗證 outline 功能。
- [QASnapshot.cs](../Assets/Scripts/Debug/QASnapshot.cs)：把 main camera 內容寫成 PNG。
- [LiveMonitor.cs](../Assets/Scripts/Debug/LiveMonitor.cs)：把 view 狀態定期印到 console，給「黑箱」測試提供觀測點。
- [VRCanvasPinner.cs](../Assets/Scripts/Debug/VRCanvasPinner.cs)：World Space canvas 在 XR Origin reset 時容易飄；這個 component 確保它跟著相機合理停留。

### 5.6 Assembly definitions

- [Inspection.Domain.asmdef](../Assets/Scripts/Domain/Inspection.Domain.asmdef)：`noEngineReferences: true`、零 references。**這是讓 Domain 真的純 .NET 的硬性護欄**——加 `using UnityEngine;` 會編不過。
- [Inspection.Runtime.asmdef](../Assets/Scripts/Inspection.Runtime.asmdef)：references = `Inspection.Domain` + TMP + InputSystem + XRI + URP。Net/UI/App/Debug 都在這顆。

---

## 6. 接手後常見任務 cheatsheet

| 任務 | 改哪 |
|---|---|
| 新增一門課 | 後端 `api/storage/{company}/{course}/`；Unity 不用改任何代碼 |
| 改 CSV 欄位定義 | [CsvParser.cs](../Assets/Scripts/Domain/CsvParser.cs) 的 `ParseStep` + 對應 [Course.cs](../Assets/Scripts/Domain/Course.cs) record 欄位 + 補 EditMode 測試 |
| 加一種媒體類型（例如 `gif`） | [CsvParser.ParseMedia](../Assets/Scripts/Domain/CsvParser.cs#L93) + 在 `Media` 加 `sealed record Gif`，[CourseView.UpdateMedia](../Assets/Scripts/UI/CourseView.cs#L118) `switch` 編譯器會提醒你補分支 |
| 加一種例外動作（例如 `OpenUrl`） | 在 `ExceptionAction` 加 sealed record，`CsvParser.ParseAction` 加分流，[CourseView.OnExceptionPressed](../Assets/Scripts/UI/CourseView.cs#L205) 補 case |
| 換 API client（離線假資料） | 寫一個 `class FakeCourseClient : ICourseClient`，在 [AppBootstrapper.TryBuildClient](../Assets/Scripts/App/AppBootstrapper.cs#L89) 條件 new 它 |
| 新增一個 view（例如「設定」） | 仿 ManualListView 寫 `MyView : MonoBehaviour`，加到 AppRouter，AppBootstrapper Init 它 |
| 改 VR 控制器/UI 投射方式 | 場景內 `XR Origin` 下的 `Near-Far Interactor` + canvas 上的 `TrackedDeviceGraphicRaycaster`，不在這份代碼 |
| 加新測試 | EditMode 寫在 `Assets/Tests/EditMode/` 下；不能參考 UnityEngine 的測試（純 Domain）就只引 `Inspection.Domain` asmdef |

## 7. 已知坑 / 警告

1. **`AppBootstrapper.Awake` 是 `async void`**：第一次 refresh 會在 Awake 裡 fire-and-forget。如果 user 在第一次 refresh 完成前就按了 settings 按鈕，會有兩條 refresh 同時跑——`ManualListView._cts` 的 cancel 機制能應付，但記得不要在 Init 後又馬上做需要 client 已就緒的事。
2. **VideoPlayer 沒被 cancel 機制管**：影片切換 step 時只 `Stop()`、不 cancel；如果 VideoPlayer.Prepare 還在跑會在背景完成。VR 上目前沒看到問題，但如果以後接到 VOD 大檔要注意。
3. **`ParseCourseList` 的手寫 JSON 不通用**：`displayName` 含 `]` 字元會炸（內層找 `]` 終止）。後端目前 schema 不會出現，但若 schema 變動要重寫成 `JsonUtility` + 包裝 class。
4. **OutlinePanel 程式 spawn UI**：要改視覺風格只能在 [SpawnHeader](../Assets/Scripts/UI/OutlinePanel.cs#L61) / [SpawnStepRow](../Assets/Scripts/UI/OutlinePanel.cs#L83) 改 magic number。值得未來重構成 prefab。
5. **C# 9 record 在 Unity inspector 不會顯示**：record 不是 `[Serializable]`，這沒問題（我們不想讓 inspector 編 domain 物件）；但若有人把 record 當成 SerializeField 欄位 type，會默默變 null。
6. **PlayerPrefs key (`Inspection.ApiBaseUrl`) 重 install Quest app 會還原**：這是 PlayerPrefs 的特性，不是 bug。

---

## 8. 延伸閱讀

- 規格：[docs/spec.md](spec.md)
- 後端 API 與 storage layout：[api/main.py](../api/main.py)、[api/storage/](../api/storage/)
- 場景產生器（一鍵組 App.unity）：[Assets/Editor/SceneBuilder.cs](../Assets/Editor/SceneBuilder.cs)
- VR 模擬模式按鍵：[README.md §4](../README.md)
- Quest 3 部署步驟：[README.md §5](../README.md)
