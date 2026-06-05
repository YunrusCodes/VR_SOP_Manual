"""Seed 5 text-only courses into the running API so the VR client has real
content instead of TestUtil placeholder data.

Usage:
    py -3 seed_real_courses.py                       # POST to http://127.0.0.1:8000
    py -3 seed_real_courses.py --api http://host:8000
    py -3 seed_real_courses.py --company edu --overwrite

`--overwrite` deletes the course (soft, into _trash/) before recreating, so the
script is safe to re-run after tweaking the text below.
"""
from __future__ import annotations

import argparse
import json
import urllib.error
import urllib.request

COMPANY = "edu"

COURSES: list[dict] = [
    {
        "name": "lab-safety",
        "displayName": "實驗室安全須知",
        "introduction": (
            "進入實驗室前的必修課程，涵蓋服裝穿戴、個人防護、緊急設備認知、廢棄物分類，與離場前檢查。"
            "完成後請在簽到本記錄今日操作項目。預計時間：5 分鐘。"
        ),
        "steps": [
            {
                "mainTitle": "進場準備",
                "subTitle": "服裝",
                "name": "換上實驗服與包鞋",
                "description": (
                    "進入實驗室前換上實驗服、長褲與包鞋；長髮綁起、飾品取下，手機調整為震動模式並收進口袋。"
                    "禁止穿涼鞋、拖鞋或裙裝進場。"
                ),
                "nextStepIndication": "走到 PPE 取用區",
            },
            {
                "mainTitle": "進場準備",
                "subTitle": "PPE",
                "name": "配戴防護裝備",
                "description": (
                    "戴上護目鏡與適合材質的手套：化學品使用丁基橡膠手套、生物樣本使用丁腈手套。"
                    "處理揮發性溶劑時，啟動抽氣櫃並戴口罩。"
                ),
                "nextStepIndication": "認識實驗室緊急設備",
                "exceptions": [
                    {
                        "label": "手套破了怎麼辦",
                        "action": {
                            "type": "showMessage",
                            "text": "立即離開操作區、脫除手套並洗手；若皮膚直接接觸化學品，使用洗眼器/淋浴器沖洗 15 分鐘並通報。",
                        },
                    }
                ],
            },
            {
                "mainTitle": "現場確認",
                "subTitle": "緊急設備",
                "name": "確認緊急設備位置",
                "description": (
                    "巡視一次：洗眼器、淋浴器、急救箱、滅火器、緊急電話。"
                    "每月例行測試洗眼器是否暢通並紀錄在保養單。"
                ),
                "nextStepIndication": "進入廢棄物分類流程",
            },
            {
                "mainTitle": "操作中",
                "subTitle": "廢棄物",
                "name": "廢液與固體廢棄物分類",
                "description": (
                    "有機溶劑、鹵化溶劑、酸鹼液、生物廢棄物分別投入對應容器。"
                    "容器滿八分時即送暫存區，標籤須寫明內容物、日期、操作人員。"
                ),
                "nextStepIndication": "完成今日實驗，準備離場",
                "exceptions": [
                    {
                        "label": "廢容器破損",
                        "action": {
                            "type": "showMessage",
                            "text": "立即通知值班；使用吸液棉吸附後依破損物處理流程處置，並重新貼上標籤送暫存區。",
                        },
                    },
                    {
                        "label": "不確定分類",
                        "action": {
                            "type": "showMessage",
                            "text": "暫不投入任何容器；放在「待確認」區並通報值班，依 SDS 查閱後再分類。",
                        },
                    },
                ],
            },
            {
                "mainTitle": "結束",
                "subTitle": "離場",
                "name": "離開實驗室前檢查",
                "description": (
                    "關閉電源、瓦斯總閥與抽氣櫃；洗手後脫除實驗服。"
                    "在出入登記本簽退並記錄使用設備、藥品與異常狀況。"
                ),
                "nextStepIndication": "完成課程",
            },
        ],
    },
    {
        "name": "microscope-basics",
        "displayName": "顯微鏡操作入門",
        "introduction": "從開機到收尾，帶你做一次完整的光學顯微鏡觀察。預計時間：6 分鐘。",
        "steps": [
            {
                "mainTitle": "啟動",
                "subTitle": "電源與光源",
                "name": "開機與調整光源",
                "description": (
                    "確認電源線接好，按下顯微鏡底部電源開關，待光源穩定後將亮度旋鈕轉到中段。"
                    "若使用 LED 光源，先選白光模式。"
                ),
                "nextStepIndication": "拿出待觀察玻片",
            },
            {
                "mainTitle": "玻片",
                "subTitle": "安裝",
                "name": "放置玻片於載物台",
                "description": (
                    "用機械式載物台夾住玻片，將觀察樣本對準光圈中央。"
                    "移動 X/Y 旋鈕讓目標區域位於正中央。"
                ),
                "nextStepIndication": "切到低倍鏡開始對焦",
            },
            {
                "mainTitle": "對焦",
                "subTitle": "低倍",
                "name": "使用 4x 或 10x 對焦",
                "description": (
                    "旋轉到 10x 接物鏡。先用粗調對焦旋鈕讓影像出現，再用細調旋鈕確認清晰。"
                    "若看到的是黑色，檢查光源是否被擋。"
                ),
                "nextStepIndication": "切到高倍鏡細看",
                "exceptions": [
                    {
                        "label": "看不到任何影像",
                        "action": {
                            "type": "showMessage",
                            "text": "可能玻片貼反、光圈關閉或樣本不在視野中央。先回到 4x 重新對焦並確認光圈打開。",
                        },
                    }
                ],
            },
            {
                "mainTitle": "對焦",
                "subTitle": "高倍",
                "name": "切換到 40x 並微調",
                "description": (
                    "切換到 40x 接物鏡，只用細調旋鈕對焦——不可在高倍下使用粗調，會撞壞玻片。"
                    "如需 100x 油鏡，加一滴香柏油。"
                ),
                "nextStepIndication": "拍照或紀錄觀察結果",
            },
            {
                "mainTitle": "紀錄",
                "subTitle": "觀察",
                "name": "拍照與描述",
                "description": (
                    "拍下視野照片並在筆記本記錄：放大倍率、樣本名稱、特徵描述。"
                    "若有特殊發現，標註位置以便後續比對。"
                ),
                "nextStepIndication": "進入收尾清潔",
            },
            {
                "mainTitle": "結束",
                "subTitle": "清潔",
                "name": "關機與清潔",
                "description": (
                    "切回 4x 接物鏡，下降載物台後取下玻片。"
                    "用拭鏡紙清潔接物鏡與目鏡，覆上防塵罩。"
                ),
                "nextStepIndication": "完成課程",
            },
        ],
    },
    {
        "name": "chemical-handling",
        "displayName": "化學藥品取用",
        "introduction": "藥品櫃取用 SOP，涵蓋 SDS 查閱、稱量、貯存與廢液回收。預計時間：5 分鐘。",
        "steps": [
            {
                "mainTitle": "前置",
                "subTitle": "資料查閱",
                "name": "閱讀 SDS",
                "description": (
                    "從藥品櫃或線上資料庫查閱 SDS（安全資料表）。"
                    "確認危害分類、儲存條件、急救處置、相容性。"
                ),
                "nextStepIndication": "穿戴對應 PPE",
            },
            {
                "mainTitle": "前置",
                "subTitle": "PPE",
                "name": "依 SDS 配戴防護",
                "description": (
                    "依 SDS 推薦選擇手套材質與口罩等級。"
                    "強酸/強鹼或揮發性溶劑請在抽氣櫃內操作。"
                ),
                "nextStepIndication": "前往藥品櫃取用",
            },
            {
                "mainTitle": "取用",
                "subTitle": "稱量",
                "name": "從藥品櫃取用並稱量",
                "description": (
                    "登記取用量與時間到藥品登記本。"
                    "使用乾淨藥匙稱量，不可將剩餘藥品倒回原瓶以避免污染。"
                ),
                "nextStepIndication": "回實驗台進行操作",
                "exceptions": [
                    {
                        "label": "取用過量",
                        "action": {
                            "type": "showMessage",
                            "text": "將多餘部分依廢棄物分類處理，不可倒回原瓶；登記本註記實際使用量。",
                        },
                    }
                ],
            },
            {
                "mainTitle": "結束",
                "subTitle": "歸位",
                "name": "藥品歸位",
                "description": (
                    "操作完成後立即將原瓶歸回藥品櫃，確認瓶蓋鎖緊、瓶身乾淨。"
                    "強酸與強鹼分開存放、不可同櫃。"
                ),
                "nextStepIndication": "處理廢液",
            },
            {
                "mainTitle": "結束",
                "subTitle": "廢液",
                "name": "廢液分類回收",
                "description": (
                    "依「實驗室安全須知」的廢棄物分類流程處理。"
                    "標籤須清楚寫明內容、日期與操作人員。"
                ),
                "nextStepIndication": "完成課程",
            },
        ],
    },
    {
        "name": "first-aid-basics",
        "displayName": "急救基礎流程",
        "introduction": "遇到實驗室人員受傷或不適時的標準應變流程。預計時間：4 分鐘。",
        "steps": [
            {
                "mainTitle": "評估",
                "subTitle": "現場",
                "name": "確認現場安全",
                "description": (
                    "靠近傷者前先確認現場無危險源（電擊、火源、化學洩漏）。"
                    "若有，先排除危險或將傷者拖離。"
                ),
                "nextStepIndication": "評估傷者意識",
            },
            {
                "mainTitle": "評估",
                "subTitle": "傷者",
                "name": "確認意識與呼吸",
                "description": (
                    "輕拍肩膀並大聲呼喚。觀察胸部起伏 10 秒判斷是否有呼吸。"
                    "若無呼吸或僅有喘息，準備執行 CPR。"
                ),
                "nextStepIndication": "求救並進入處置",
                "exceptions": [
                    {
                        "label": "無意識無呼吸",
                        "action": {
                            "type": "showMessage",
                            "text": "立刻請旁人撥 119、取 AED，並開始 CPR：胸外按壓 30 次配合人工呼吸 2 次，循環不停直到救護人員到場。",
                        },
                    }
                ],
            },
            {
                "mainTitle": "處置",
                "subTitle": "求救",
                "name": "呼叫求援",
                "description": (
                    "請旁人撥 119、通知實驗室值班與安全衛生人員。"
                    "提供：地點、傷者狀況、受傷原因。若涉及化學品，提供 SDS。"
                ),
                "nextStepIndication": "依傷情處置",
            },
            {
                "mainTitle": "處置",
                "subTitle": "止血/沖洗",
                "name": "止血或化學品沖洗",
                "description": (
                    "出血傷口：使用乾淨敷料直接壓迫 10 分鐘。"
                    "化學品潑灑：用洗眼器或淋浴器沖洗至少 15 分鐘，沖洗時不要停。"
                ),
                "nextStepIndication": "等待救護人員",
            },
            {
                "mainTitle": "結束",
                "subTitle": "交接",
                "name": "向救護人員交接",
                "description": (
                    "救護人員到場時，告知：事發時間、傷情、已執行的處置、傷者個資。"
                    "完成後填寫工安事故報告，24 小時內呈安衛室。"
                ),
                "nextStepIndication": "完成課程",
            },
        ],
    },
    {
        "name": "lab-notebook",
        "displayName": "實驗紀錄撰寫",
        "introduction": "如何寫一份能被他人重現、能在審查時站得住腳的實驗紀錄。預計時間：4 分鐘。",
        "steps": [
            {
                "mainTitle": "開頭",
                "subTitle": "識別",
                "name": "標題、日期、編號",
                "description": (
                    "每一份紀錄必須有：實驗標題、執行日期、操作員、實驗編號、相關計畫案編號。"
                    "標題簡短但能讓六個月後的自己讀懂。"
                ),
                "nextStepIndication": "寫下目的與假設",
            },
            {
                "mainTitle": "前置",
                "subTitle": "目的",
                "name": "目的與假設",
                "description": (
                    "用一兩句話寫清楚：本次想驗證/觀察什麼？預期結果是什麼？"
                    "假設可以用如果...則...的句型，方便結束時對照。"
                ),
                "nextStepIndication": "列出材料與設備",
            },
            {
                "mainTitle": "前置",
                "subTitle": "資源",
                "name": "材料與設備清單",
                "description": (
                    "列出所有藥品（含廠牌、批號、純度）、儀器（含型號、序號、最近校期）。"
                    "缺一個就重做。"
                ),
                "nextStepIndication": "記錄操作步驟",
            },
            {
                "mainTitle": "執行",
                "subTitle": "操作",
                "name": "步驟與觀察",
                "description": (
                    "邊做邊寫，不要事後憑印象補。"
                    "每個關鍵步驟記下時間、實際使用量（不是預定量）、觀察到的異常。"
                ),
                "nextStepIndication": "整理結果與結論",
                "exceptions": [
                    {
                        "label": "操作中途出錯",
                        "action": {
                            "type": "showMessage",
                            "text": "錯誤不可塗黑或撕頁；劃單線刪除、寫上原因與修正，並簽上日期。實驗紀錄是法律文件。",
                        },
                    }
                ],
            },
            {
                "mainTitle": "結束",
                "subTitle": "結果",
                "name": "結果分析與結論",
                "description": (
                    "貼上原始數據（拍照或印出）、寫下圖表編號。"
                    "結論回應一開始的假設，並列出待解問題。"
                ),
                "nextStepIndication": "完成課程",
            },
        ],
    },
]


def _http(method: str, url: str, body: dict | None = None) -> dict | None:
    headers = {"Content-Type": "application/json"} if body is not None else {}
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            text = resp.read().decode("utf-8")
            return json.loads(text) if text else None
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")
        raise SystemExit(f"{method} {url} -> {e.code}: {text}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api", default="http://127.0.0.1:8000")
    parser.add_argument("--company", default=COMPANY)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    existing = _http("GET", f"{args.api}/companies/{args.company}/courses") or {}
    existing_names = {c["name"] for c in existing.get("courses", [])}

    for c in COURSES:
        name = c["name"]
        if name in existing_names:
            if args.overwrite:
                _http("DELETE", f"{args.api}/companies/{args.company}/courses/{name}")
            else:
                print(f"  skip {name} (exists; pass --overwrite to replace)")
                continue
        _http(
            "POST",
            f"{args.api}/companies/{args.company}/courses",
            {"name": name, "displayName": c["displayName"]},
        )
        # Reload, fill in steps, save.
        course = _http("GET", f"{args.api}/companies/{args.company}/courses/{name}/structured")
        assert course is not None
        course["introduction"] = c["introduction"]
        course["steps"] = []
        for i, raw in enumerate(c["steps"], start=1):
            step = {
                "order": i,
                "mainTitle": raw.get("mainTitle", ""),
                "subTitle": raw.get("subTitle", ""),
                "name": raw.get("name", ""),
                "description": raw.get("description", ""),
                "media": {"kind": "none", "filename": None},
                "nextStepIndication": raw.get("nextStepIndication", ""),
                "exceptions": raw.get("exceptions", []),
            }
            course["steps"].append(step)
        _http(
            "PUT",
            f"{args.api}/companies/{args.company}/courses/{name}/structured",
            course,
        )
        print(f"  + {name} ({len(course['steps'])} steps)")

    after = _http("GET", f"{args.api}/companies/{args.company}/courses") or {}
    print(f"\nCompany '{args.company}' now has {len(after.get('courses', []))} courses.")


if __name__ == "__main__":
    main()
