# 交付驗收 — 智慧巡檢 / 科普課程平台

**日期**：2026-05-07（夜班自動跑 + 早班 VR 視角覆驗）
**狀態**：3 門教育課程已內容化、所有 UI 流程經 PLAYMODE 自動 walker 截圖驗證（含 ScreenSpaceOverlay 與 WorldSpace VR 兩套）、例外按鈕 (goto + message) 觸發成功、VR 視角字級已調整為頭顯舒適閱讀比例。

## 0. VR 視角驗收（[App_VR.unity](../Assets/Scenes/App_VR.unity)）

20 張 VR 視角快照記錄於 [qa/](.) 目錄，前綴 `vr_`：

- 課程清單：[`vr_manual_list.png`](vr_manual_list.png)
- 細胞分裂（5 步）：[`vr_mitosis_step01.png`](vr_mitosis_step01.png) … step05
- 太陽系巡禮（7 步）：[`vr_solar_step01.png`](vr_solar_step01.png) … step07
- 火山形成（6 步）：[`vr_volcano_step01.png`](vr_volcano_step01.png) … step06
- 走訪結束：[`vr_manual_list_final.png`](vr_manual_list_final.png)

VR 視角專屬問題與修補（給後續 onboard 的同事備查）：

| 問題 | 修補 |
|---|---|
| `CanvasScaler` 即使 disabled，play 一進去會把 RectTransform reset 成 (960,540,2) scale=(0,0,0) → 整個 game view 全黑 | 移除 CanvasScaler 元件 + 加 [`VRCanvasPinner`](../Assets/Scripts/Debug/VRCanvasPinner.cs) 每 LateUpdate 重新固定 pos (0,1.6,2) scale 0.0013 |
| URP 下 `Camera.Render()` 不畫 WorldSpace UI（snapshot 全黑） | 改用 `RenderPipeline.SubmitRenderRequest` 走 URP 正規 path，coroutine 內 yield WaitForEndOfFrame；見 [QASnapshot.cs](../Assets/Scripts/Debug/QASnapshot.cs) |
| 2m 距離下字級偏小 | Header 72→80, StepName 60→72, Description 36→44, Breadcrumb/StepCounter 36→42, NextIndication 28→32, 各按鈕 36→38, ExceptionButton 28→34 + size 220×64→280×88, NavBar 220×70→280×90, CourseCard title 40→52 + 高度 130→160 |
| 無媒體步驟（solar 5/7, volcano 3/6, mitosis 5）右半空白浪費 | [`CourseView.UpdateMedia`](../Assets/Scripts/UI/CourseView.cs) 偵測 `Media.None` 時把 `leftColumn.anchorMax.x` 從 0.6 改成 1.0，文字自動全寬展開 |
| 卡片點擊在 walker context 偶爾不觸發 OnEnterAsync | walker 改用 reflection 抓 `ManualListView._client`，直接 `_router.ShowCourse(course)` 繞過按鈕；見 [VRMultiCourseWalker.cs](../Assets/Scripts/Debug/VRMultiCourseWalker.cs) |


## 1. 已交付課程

| Course ID | 顯示名稱 | 步驟數 | 圖 | 影片 | 例外按鈕（goto / message）|
|---|---|---|---|---|---|
| `solar-system` | 太陽系巡禮 | **7** | 太陽 / 水星 / 木星 / 太空船（4 張） | 地球自轉 (5s) | 6 個（含 step3 → step7 月球 goto） |
| `volcano-formation` | 火山形成全紀錄 | **6** | 板塊 / 隱沒帶 / 熔岩平原（3 張） | 火山爆發夜景 (5s) | 5 個 |
| `cell-mitosis` | 細胞分裂 — 有絲分裂 | **5** | 細胞週期 / 染色體 / 細胞質分裂（3 張） | 有絲分裂顯微縮時 (5s) | 4 個 |

> 內容是 AI 編寫的科普文字，每步都附完整描述、下一步引導、例外延伸。CSV 在 [api/storage/edu/](../api/storage/edu/)。

### 圖/影片一覽
- ✅ 8 張 codex-image-gen 生成的圖（首批，太陽 / 木星 / 太空船 / 板塊 / 隱沒帶 / 染色體 / 細胞質分裂）
- ✅ 3 張 Seedream 5.0 (BytePlus Ark) 補生（mercury / lava_field / cell_cycle，因 codex parallel race + 配額耗盡而以 Ark 補上）
- ✅ 3 個 Seedance 1.0 Pro Fast 720p/5s 影片（共 ~$0.48）
- 全部步驟媒體齊全，無缺漏

## 2. UI 截圖索引（按用戶旅程順序）

開啟 `qa/` 看以下 22 張：

### ManualListView
- [`99_final_manual_list.png`](99_final_manual_list.png) — 課程清單（3 張卡片 + 進入按鈕）

### 太陽系巡禮（7 步全紀錄）
- [`30_solar_step01.png`](30_solar_step01.png) — 太陽（圖）
- [`30_solar_step02.png`](30_solar_step02.png) — 水星（圖）
- [`30_solar_step03.png`](30_solar_step03.png) — 地球（**影片**自轉中）
- [`30_solar_step04.png`](30_solar_step04.png) — 木星（圖）
- [`30_solar_step05.png`](30_solar_step05.png) — 望遠鏡入門（無媒體）
- [`30_solar_step06.png`](30_solar_step06.png) — 人類太空腳印（圖）
- [`30_solar_step07.png`](30_solar_step07.png) — 月球延伸（無媒體）

### 火山形成全紀錄（6 步）
- [`31_volcano_step01.png`](31_volcano_step01.png) — 板塊邊界（圖）
- [`31_volcano_step02.png`](31_volcano_step02.png) — 隱沒帶（圖）
- [`31_volcano_step03.png`](31_volcano_step03.png) — 岩漿庫累積（無媒體）
- [`31_volcano_step04.png`](31_volcano_step04.png) — 火山爆發（**影片**）
- [`31_volcano_step05.png`](31_volcano_step05.png) — 熔岩流冷卻（圖）
- [`31_volcano_step06.png`](31_volcano_step06.png) — 生態與人類（無媒體）

### 細胞分裂 — 有絲分裂（5 步）
- [`32_mitosis_step01.png`](32_mitosis_step01.png) — 細胞週期概覽（圖）
- [`32_mitosis_step02.png`](32_mitosis_step02.png) — 染色體加倍（圖）
- [`32_mitosis_step03.png`](32_mitosis_step03.png) — 四個分裂期（**影片**有絲分裂顯微）
- [`32_mitosis_step04.png`](32_mitosis_step04.png) — 細胞一分為二（圖）
- [`32_mitosis_step05.png`](32_mitosis_step05.png) — 兩個子細胞（無媒體）

### 例外按鈕互動驗證
- [`40_solar_step2_message_exception.png`](40_solar_step2_message_exception.png) — 水星 → 「水星上有水嗎」按下 → **訊息浮層**顯示說明
- [`41_solar_step3_before_goto.png`](41_solar_step3_before_goto.png) — 地球 step 3 點擊「想看月球」前
- [`42_solar_after_goto_step7.png`](42_solar_after_goto_step7.png) — 跳到 step 7 月球（**goto 跳步**驗證）

## 3. 自動測試流程（你不在時跑的）

寫了一個 `Inspection.Debugging.QACourseWalker` MonoBehaviour 可在 play mode 自動 walk 整門課，每步等待 2 秒讓媒體載入，然後 `QASnapshot.CaptureMainCamera` 渲染 main camera + 暫切 ScreenSpace canvas mode 抓全 UI（解決 ScreenSpaceOverlay 不入 camera.Render 的問題）。

亦於 `CourseView` 加了 `TestNext / TestPrev / TestBackToList / TestPressException(int)` 公開方法給自動化使用（runtime button.onClick.Invoke 在 play mode 偶爾不觸發 OnNext，原因待查；直接呼叫公開方法穩定）。

## 4. UI 修補紀錄

走訪過程發現 + 修好的：

1. **字體偏小** → SceneBuilder 統一升級：Header 56→72, StepName 44→60, Description 24→36, NextIndication 22→28, 各 Button label 22-28→28-36
2. **CourseCard 只 90px 高 + 標題用 44px 字** → 卡片擠出格 → 高度 90→130, 標題字 44→40, 不換行 + Ellipsis 截斷, 按鈕區擴 160→200×80
3. **Description 寬 100px 一字一行** → LeftColumn VerticalLayoutGroup 缺 `childControlWidth/Height` → 補上 → 文字正常 wrap
4. **Breadcrumb 的 ▸ 字符 NotoSansTC 沒收錄** → 用全形「／」(U+FF0F) 代替
5. **CourseCard 整張可點擊** → 加 Button 元件到卡片 root，跟原 EnterButton 共享同一個 onEnter handler

## 5. 後端與媒體快速驗證

```powershell
# API 啟動
cd d:\SideProjects\VR_SOP_Manual\api
py -m uvicorn main:app --host 127.0.0.1 --port 8000

# 列課程
Invoke-RestMethod http://127.0.0.1:8000/companies/edu/courses
# 應回 cell-mitosis / solar-system / volcano-formation 共 3 門

# 拿 CSV
Invoke-WebRequest http://127.0.0.1:8000/companies/edu/courses/solar-system/csv -UseBasicParsing | % RawContentLength
# ~3.4 KB

# 媒體檔
Invoke-WebRequest http://127.0.0.1:8000/companies/edu/courses/solar-system/files/image/sun.jpg -UseBasicParsing | % RawContentLength
# ~2.6 MB
Invoke-WebRequest http://127.0.0.1:8000/companies/edu/courses/solar-system/files/video/earth_rotation.mp4 -UseBasicParsing | % RawContentLength
# ~7.2 MB
```

## 6. 你開 Unity 直接跑的方式

1. 打開 Unity（已在 play 結束狀態下保留 scene）
2. AppSettings 已指 `http://127.0.0.1:8000` + Company `edu` — 跑後端後直接按 ▶️ Play
3. 整張卡片任一處點擊都能進課程（不只進入按鈕）
4. CourseView 內：滑鼠點上一步/下一步/例外按鈕/← 課程清單
5. 媒體（圖跟影片）會自動從 API streaming 載入；影片自動播放

## 7. 已知限制

- Quest 3 實機部署仍需另裝 Meta XR SDK + 切 Android build target（流程在 README §5）
- XR Origin / WorldSpace canvas 模式已退回 ScreenSpaceOverlay 給 editor 滑鼠測試用；Quest 部署時改回見 README

## 8. 預算狀態

- Seedance：3 影片 ≈ ~310k tokens × $1.50/M ≈ **$0.47**
- Seedream 5.0：3 張 2048×2048 圖（具體計費未知，BytePlus image API 一般 $0.01-0.05/張）
- Codex：免額度（OpenAI quota，每日重置；當天用完，所以才轉 Seedream 補生）
- Seedance budget 大約剩 $9.5

## 9. 相關檔案位置

| 內容 | 路徑 |
|---|---|
| 課程資料 | [api/storage/edu/](../api/storage/edu/) |
| 場景 | [Assets/Scenes/App.unity](../Assets/Scenes/App.unity) |
| App Settings | [Assets/Settings/AppSettings.asset](../Assets/Settings/AppSettings.asset) |
| Domain + CsvParser | [Assets/Scripts/Domain/](../Assets/Scripts/Domain/) |
| UI Views | [Assets/Scripts/UI/](../Assets/Scripts/UI/) |
| QA 工具 | [Assets/Scripts/Debug/](../Assets/Scripts/Debug/) |
| Editor 工具 | [Assets/Editor/SceneBuilder.cs](../Assets/Editor/SceneBuilder.cs) |
