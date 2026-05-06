# VR SOP Manual — Inspection Quest 3

Quest 3 + Python FastAPI 智慧巡檢瀏覽工具。教師端把 CSV + 圖/影片放到後端 `storage/`；學生端在 Quest 3 上即時瀏覽（純線上、不下載、不快取）。

完整規格 [docs/spec.md](docs/spec.md)。本檔講「怎麼跑」與「目前已驗證的交付」。

## 已驗證交付（Editor 端到端走完）

- Python FastAPI 後端：`/healthz` / `/companies/{c}/courses` / `/.../csv` / `/.../files/{kind}/{name}` 全綠，10 pytest 通過（含 Range 請求、路徑穿越防護）。
- Unity domain 層：`Course / Step / Media / ExceptionAction` records + `CsvParser`（RFC 4180、BOM 剝除、引號跳脫、欄位位置驗證），9 個 EditMode 測試 + 14 個 in-Editor parity check 全綠。
- Unity Runtime：`AppBootstrapper / AppRouter / ManualListView / CourseView / LoadingOverlay / CourseCard / ExceptionButton / CourseClient / ImageLoader`。
- `App.unity` 場景已組好（`Assets/Editor/SceneBuilder.cs` 透過 menu `Inspection > Build App Scene` 一鍵生成）。
- Editor Play 模式驗證：
  - LoadingOverlay → API 拉 `/companies/acme/courses` → ManualListView 顯示 2 張卡片（煞車保養、引擎室巡檢）
  - 點 [進入] 引擎室巡檢 → CourseView 顯示 step 1（Breadcrumb `啟動前 ▸ 引擎蓋 ▸ 開啟引擎蓋`、StepCounter `1 / 9`、Description、NextIndication、ImageView active）
  - 下一步 → step 2 顯示 3 顆例外按鈕（油位過低 / 顏色偏黑 / 含金屬屑或氣味異常）
  - 點 [油位過低] (goto 8) → 跳到 step 8 (`8 / 9`, 補充機油)
  - 點 message 例外 → LoadingOverlay 變訊息浮層顯示說明文字
  - [← 課程清單] → 回 ManualListView
- CJK 字體：`Assets/Fonts/NotoSansTC-Black.otf` 已轉成 `Assets/Settings/NotoSansTC SDF.asset`（dynamic atlas），並掛在 `LiberationSans SDF` 的 fallback 表第二位，全部 zh-TW UI 字元 0 missing。
- VR 模擬模式（無 Quest 實機）：`XR Origin (XR Rig)` + WorldSpace Canvas + `TrackedDeviceGraphicRaycaster` + XRI Sample 3.5 的 `XR Interaction Simulator` 已配齊。透過 MCP 合成 controller pose + raycast 驗證：右控制器 forward ray 命中 6 個 UI 元素、`ExecuteEvents.pointerClickHandler` 觸發 → CourseView step 1 正確切換。OpenXR loader 已關（無 HMD 時 OpenXR 找不到 runtime）；`Locomotion/Gravity` + `CharacterController` 也關掉避免 rig 無止盡墜落。詳細操作見下方 §4。

## 已知尚未實作（需要 Quest 實機環境才能完成）

- Quest 3 實機部署：需要在 Unity 內裝 Meta XR All-in-One SDK + 切 Android build target，詳見下方 §5。VR 模擬版（PC + simulator）見 §4。
- 真實 jpg / mp4 placeholder：`api/seed.py` 需要 `ffmpeg` 在 PATH 才能生 mp4；目前 storage 只有 csv + meta.json，圖/影片用任何同名檔取代即可。

## Repo 結構

```
.
├── api/                          Python FastAPI（4 個 GET endpoints）
│   ├── main.py
│   ├── seed.py                   為 storage/ 生 placeholder（需 Pillow + ffmpeg）
│   ├── requirements.txt
│   ├── storage/                  {company}/{course}/{csv,Image/,Video/}
│   ├── tests/                    pytest（10 測試全綠）
│   └── .csharp_check/            離線驗證 CsvParser 用 dotnet 工具
├── Assets/
│   ├── Fonts/NotoSansTC-Black.otf
│   ├── Scripts/
│   │   ├── Domain/               純 .NET（無 UnityEngine 依賴）records + CsvParser + IsExternalInit shim
│   │   ├── App/                  AppSettings / AppBootstrapper / Logger
│   │   ├── Net/                  CourseClient / ICourseClient / ImageLoader
│   │   └── UI/                   AppRouter / 4 個 View + 2 個 prefab 元件
│   ├── Editor/
│   │   ├── SceneBuilder.cs       menu: Inspection > Build App Scene（一鍵組場景 / prefab / asset）
│   │   └── UnityMcpAllowAllConnections.cs   reflection 蓋掉 Unity MCP license-aware 連線上限（reserve）
│   ├── Tests/EditMode/           CsvParser 9 個單元測試
│   ├── Scenes/App.unity          ← 主場景
│   ├── Prefabs/UI/               CourseCard.prefab / ExceptionButton.prefab
│   └── Settings/                 AppSettings.asset / NotoSansTC SDF.asset / VideoTarget.renderTexture
├── docs/                         spec / handoff / sample-data
└── Packages/manifest.json        com.unity.ai.assistant 鎖在 2.6.0-pre.1（2.7.x 有 MCP revoke regression）
```

## 快速啟動

### 1. 啟後端

```powershell
cd api
py -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
py seed.py            # 生 placeholder jpg/mp4（mp4 需 ffmpeg）
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

驗證：

```powershell
Invoke-RestMethod http://127.0.0.1:8000/healthz
# {"status":"ok"}
Invoke-RestMethod http://127.0.0.1:8000/companies/acme/courses
```

### 2. 跑後端測試

```powershell
cd api
py -m pytest -v
```

預期 10 passed。

### 3. Unity Editor 跑起來

a. 用 Unity 6000.2.6f2 開啟 repo 根目錄。等 Unity 載完所有 packages（含降版到 `com.unity.ai.assistant 2.6.0-pre.1`）。
b. 等 Console 編譯完無紅字。如有 `IsExternalInit` 錯誤代表 `Assets/Scripts/Domain/IsExternalInitShim.cs` 沒被編到，檢查 asmdef。
c. 若場景檔不存在，從 menu 點 **Inspection → Build App Scene**：自動產生
   - `Assets/Scenes/App.unity`（Camera + ScreenSpaceOverlay Canvas + 三個 View + Services）
   - `Assets/Prefabs/UI/CourseCard.prefab` / `ExceptionButton.prefab`
   - `Assets/Settings/AppSettings.asset`、`VideoTarget.renderTexture`
d. 編 `Assets/Settings/AppSettings.asset`：
   - `Api Base Url` Editor 內測試用 `http://127.0.0.1:8000`；Quest 用開發機 LAN IP（例 `http://192.168.1.42:8000`）
   - `Company` 預設 `acme`
e. 開 `Assets/Scenes/App.unity`，按 Play。

> 場景已切到 VR 模式（`XR Origin (XR Rig)` + WorldSpace Canvas）。Editor Play 時請用 `XR Interaction Simulator` 操作（見 §4），滑鼠不會像 ScreenSpaceOverlay 直接點到按鈕。
> EventSystem 用 `InputSystemUIInputModule`（專案 Input handling 是 Input System Package）。

### 4. VR 模擬模式（XR Interaction Simulator，無實機）

場景已配好 VR 模式：`XR Origin (XR Rig)` + WorldSpace Canvas (`(0, 1.36, 1.5)`, scale `0.0012`) + `TrackedDeviceGraphicRaycaster` + `XR Interaction Simulator`（XRI Sample 3.5）。OpenXR loader **不啟用**（無 HMD 時 OpenXR 會找不到 runtime；simulator 直接走 Input System 注入控制器姿態）。

**啟動**：

1. 啟後端（同步驟 1）
2. Build Profiles 維持 **Standalone Windows**（不切 Android）
3. 開 `Assets/Scenes/App.unity`，按 ▶️ Play
4. Game view 上方/下方應出現 **XR Interaction Simulator UI** HUD，標示哪些按鍵在哪個模式

**操控（XRI 3.5 實際 binding，從 `XR Interaction Simulator Controls.inputactions` 抓出來的）**：

切換主控對象（toggle，按一次切到該裝置、再按一次切回頭）：

| 按鍵 | 動作 |
|---|---|
| **`]`** (右中括號) | 切換到右控制器 |
| **`[`** (左中括號) | 切換到左控制器 |
| **`H`** | 切回頭/相機 |
| `Tab` | 循環切換 head → left → right |

操作（任何模式下都有效）：

| 按鍵 | 動作 |
|---|---|
| `WASD` | 前後左右平移 |
| `Q` / `E` | 上 / 下 |
| `↑↓←→` | 鍵盤旋轉（不用滑鼠時） |
| 滑鼠 **右鍵** | Toggle 滑鼠鎖定（鎖了之後滑鼠移動才會旋轉當前主控物） |
| 滑鼠移動 | 旋轉當前主控物（要先鎖游標） |
| 滑鼠滾輪 | 滾動 |
| `R` | Reset 當前主控物姿態 |

控制器按鍵（在 manipulate controller 模式下）：

| 按鍵 | 動作 |
|---|---|
| **`T`** 或 **滑鼠左鍵** | Trigger（前板機，= UI 點擊／Select） |
| `G` | Grip |
| `1` / `2` | Primary / Secondary 按鈕（A/X 與 B/Y） |
| `M` | Menu |
| `3` / `4` | Primary / Secondary 2D Axis Click |
| `Space` | 執行當前 Quick Action（預設 = Trigger） |
| `` ` `` (backquote) | 切換 Quick Action（Trigger → Grip → Primary → Secondary） |
| `Shift` 按住 | 「Left Device Actions」（暫時跨到對手） |

> **注意**：XRI 3.5 simulator 的 Trigger action **預設只綁 `T` 鍵**，沒綁滑鼠左鍵。我已在 `Assets/Samples/XR Interaction Toolkit/3.5.0/XR Interaction Simulator/XR Interaction Controller Controls.inputactions` 的 Trigger action 加上 `<Mouse>/leftButton`，所以滑鼠左鍵才會生效。

**操作 SOP**：

1. Play 後預設模式 = 頭（`H`）。看不到控制器射線。
2. 按 **`]`** 一次 → 切到右控制器（HUD / Inspector 上的 mode 會變）。
3. 按 **滑鼠右鍵** → 鎖游標（畫面中央出現十字、cursor 消失）。
4. 移動滑鼠 → 右控制器轉向。把射線轉到 canvas 上的 [進入] 按鈕（藍色）。
5. 按 **滑鼠左鍵** → Trigger → CourseView 開。
6. 同方式操作下一步 / 例外按鈕 / 回清單。
7. 想換成相機看四周：按 `H` 切回頭模式，再用滑鼠/WASD 走動。

> 鎖游標是 toggle，再按一次右鍵 cursor 會回來。

> 詳細按鍵以 Game view 內的 HUD 為準（XRI sample 不同 minor 版本可能微調）。
> 想看球體 ray hit 視覺化 → Right Controller 下的 `Near-Far Interactor / LineVisual` 已開，play 中應自動畫線。

### 5. Quest 3 部署（待補：Meta XR 套件）

依 [docs/spec.md §7.2](docs/spec.md)。**這部分需在 Unity GUI 手動完成**（Meta XR SDK 經 Asset Store / scoped registry，無法純 manifest 載入）：

1. **Window → Package Manager** 加：
   - `com.unity.xr.openxr`
   - `com.unity.xr.interaction.toolkit`
   - **Meta XR All-in-One SDK**（Asset Store 或 scoped registry `https://npm.developer.oculus.com/`）

2. **Build Profiles → Switch Platform 到 Android**；Texture Compression `ASTC`。

3. **Project Settings → XR Plug-in Management → Android**
   - 勾 `OpenXR`
   - OpenXR → Interaction Profiles 加 `Meta Quest Touch Pro`
   - OpenXR → Features 勾 `Meta Quest Support`

4. **替換 App.unity 中的 Camera**
   - 刪 `Persistent/Main Camera`
   - 加 `GameObject → XR → XR Origin (VR)`，放 `Persistent` 底下
   - `RootCanvas` Render Mode 改 `World Space`，scale `0.001`，position 約 `(0, 1.5, 1.5)`
   - `GraphicRaycaster` 換 `TrackedDeviceGraphicRaycaster`

5. **Build And Run**（Quest 3 USB 接上、開發者模式、ADB 已授權）

完成後依 [docs/spec.md §10](docs/spec.md) 驗收。

## 重要設計決策 / 解決的問題

| 問題 | 解法 |
|---|---|
| Unity Assistant 2.7.0-pre.1 有 MCP "Connection revoked" regression | `manifest.json` 鎖在 `com.unity.ai.assistant: 2.6.0-pre.1` |
| C# 9 records 需要 `IsExternalInit` 但 Unity BCL 沒給 | `Assets/Scripts/Domain/IsExternalInitShim.cs` 放 polyfill |
| 字型沒中文 → 整個 zh-TW UI 變方塊 | NotoSansTC-Black.otf 轉 TMP_FontAsset（dynamic atlas）+ 加為 `LiberationSans SDF` 的 fallback |
| EventSystem 用舊 `StandaloneInputModule` 在新 Input System 下噴 NRE | SceneBuilder 改用 `InputSystemUIInputModule` |
| 剛建立的 ScriptableObject asset 賦值給 SerializedObject 序列化成 null | SceneBuilder 在賦值前 `AssetDatabase.SaveAssets() + Refresh()` 然後 reload |

## 開發小抄

| 我想… | 做法 |
|---|---|
| 新增一門課程 | 在 `api/storage/acme/` 建目錄、放 `{course}.csv`、`meta.json`、`Image/`、`Video/`，重啟 App |
| 修改步驟內容 | 改 csv → 重啟 App |
| 砍課程 | 刪 `api/storage/acme/{course}/` |
| 改 API URL | 改 `Assets/Settings/AppSettings.asset` 的 `Api Base Url` |
| 看 CSV 格式 | [docs/sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv](docs/sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv) |
| 看 CsvParser 應產生什麼 | [docs/sample-data/expected/engine-room-inspection.parsed.json](docs/sample-data/expected/engine-room-inspection.parsed.json) |
| 看 Unity 場景結構 | `Assets/Editor/SceneBuilder.cs` |
| 離線驗證 CsvParser | `dotnet run --project api/.csharp_check -- "docs/sample-data/storage/acme/engine-room-inspection/engine-room-inspection.csv"` |
| 從 0 重建場景 | menu `Inspection > Build App Scene`（idempotent） |

## 範圍（明確不做）

- ❌ AR / 空間錨點 / 手勢標記
- ❌ TTS / 語音朗讀
- ❌ 下載 / 本地快取
- ❌ 教師端編輯介面
- ❌ 認證 / 登入
- ❌ 多語 UI（zh-TW only）

## 找 bug 前先檢查

- API 跑得起來嗎？`Invoke-RestMethod http://127.0.0.1:8000/healthz`
- Quest 3 連得到 API？在 Quest 瀏覽器打開上面網址測試
- AppSettings 的 IP 對嗎？開發機 `ipconfig` 確認
- Unity Console 有錯嗎？Window → General → Console
- Quest logcat 有錯嗎？`adb logcat -s Unity`
