# 範例資料

這份範例資料配合 [spec.md](../spec.md)，讓人類與 AI 實作者都能用一個具體可驗證的案例理解業務邏輯。

## 目錄

```
sample-data/
├── README.md                                    ← 你正在看的檔
├── storage/                                     ← 對應 Python API 的 ./storage/
│   └── acme/
│       ├── engine-room-inspection/              ← 主範例，內容齊全
│       │   ├── engine-room-inspection.csv       ← 9 步驟，含各種 edge case
│       │   ├── meta.json                        ← {"displayName": "引擎室巡檢"}
│       │   ├── Image/                           ← 預期出現的 jpg 清單
│       │   └── Video/                           ← 預期出現的 mp4 清單
│       └── brake-maintenance/                   ← 第二門課程，極簡，只為了讓 list 端點不只回 1 筆
│           ├── brake-maintenance.csv
│           └── meta.json
└── expected/                                    ← 預期 API 回應 / 解析結果，供新 session 對照
    ├── list-courses.json                        ← GET /companies/acme/courses 的 response
    └── engine-room-inspection.parsed.json       ← CsvParser 解析後的 Course record
```

## 怎麼用

### 你要驗證商業邏輯時

直接看 [storage/acme/engine-room-inspection/engine-room-inspection.csv](storage/acme/engine-room-inspection/engine-room-inspection.csv)。內含 9 個步驟，刻意涵蓋：

- **三層分類**：MainTitle 有 `啟動前 / 啟動測試 / 例外處理`；SubTitle 有 `引擎蓋 / 油液檢查` 也有空白
- **媒體三種**：jpg、mp4、無媒體
- **例外按鈕兩種行為**：goto（action 是純數字 → 跳到指定 step）、message（action 是文字 → 顯示說明）
- **例外按鈕個數**：0、1、3 個
- **NextStepIndication**：有的步驟有提示文字，多數沒有

對照 [expected/engine-room-inspection.parsed.json](expected/engine-room-inspection.parsed.json) 你能看到 CSV 在 App 內被解析成什麼樣的 `Course` 物件。

### 新 session 實作時

1. **API 階段**：把 `storage/` 整包複製到 API 專案的 `./storage/` 之下。`seed.py` 也應產生這份資料（或用 git 直接 commit 進去）。
2. **API 階段對照**：實作完 `GET /companies/acme/courses` 後，回應內容應與 [expected/list-courses.json](expected/list-courses.json) 完全一致（順序可不同，但每個物件的內容要對得上）。
3. **API 階段對照**：實作完 `GET /companies/acme/courses/engine-room-inspection/csv` 後，回應的 body 應與 `storage/acme/engine-room-inspection/engine-room-inspection.csv` 內容相同（含 BOM 與否依實作）。
4. **CsvParser 階段**：寫完 `CsvParser.Parse(csvText, "engine-room-inspection")` 之後，把回傳的 `Course` 序列化成 JSON，應該與 [expected/engine-room-inspection.parsed.json](expected/engine-room-inspection.parsed.json) 結構一致。
5. **App 階段**：跑起來後手動驗證每個步驟的 UI 顯示與例外按鈕行為。

## 真正的圖片/影片從哪來？

本範例**沒有附**真實的 jpg/mp4 二進位檔（避免大檔污染 git）。你有兩個選擇：

- **快速驗證**：用任何同名的小檔（甚至 1×1 的 jpg、3 秒 mp4）放到 `Image/` 與 `Video/`。`seed.py` 可以動態生成。
- **真實場景**：拍幾張車輛/機具照片與短片，命名為 csv 中提到的檔名。

預期檔名清單：

**Image/**
- `engine-hood.jpg`
- `oil-dipstick.jpg`
- `coolant.jpg`
- `dashboard.jpg`

**Video/**
- `startup.mp4`
- `idle.mp4`
- `add-oil.mp4`

## 第二門課程 brake-maintenance

只有 3 個步驟、極簡格式，主要為了確認 `GET /courses` 能回兩筆而不是 hardcode 一筆。實作 `displayName` 排序時可以用這份驗證（acme 底下會有 `brake-maintenance` 與 `engine-room-inspection` 兩個目錄）。
