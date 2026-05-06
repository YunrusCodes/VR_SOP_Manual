# 預期影片清單

此目錄應包含以下檔案，被 [../engine-room-inspection.csv](../engine-room-inspection.csv) 第 6 欄引用：

| 檔名 | 出現於 step | 內容建議 | 建議長度 |
|---|---|---|---|
| `startup.mp4` | 5 | 啟動引擎示範 | 5 秒 |
| `idle.mp4` | 7 | 怠速狀態示範 | 30 秒 |
| `add-oil.mp4` | 8 | 補充機油操作示範 | 30–60 秒 |

開發階段可以用任何小尺寸 mp4 檔暫代，重點是檔名要對得上、且能 HTTP streaming（H.264 + AAC + faststart 確保能 seek）。
