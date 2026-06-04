using System.Collections.Generic;
using Inspection.Domain;

namespace Inspection.UI
{
    /// <summary>
    /// Fabricates sample courses and steps so the pagination / outline / step
    /// UI can be exercised without a live backend or fully-populated CSVs.
    /// Triggered when ListCourses returns fewer than a few entries or when a
    /// "test-course-NN" summary is "entered" — the SOP flow then runs against
    /// these synthetic records exactly like real ones.
    /// </summary>
    public static class TestUtil
    {
        static readonly string[] Titles =
        {
            "機具外觀檢查", "燃油與機油位", "冷卻液檢查", "電瓶確認", "安全裝置確認",
            "暖機程序", "氣壓設定", "工作壓力檢查", "感測器校正", "緊急停止測試",
            "工件夾持", "刀具更換", "對刀作業", "首件確認", "切削參數調整",
            "切屑排除", "潤滑油補充", "冷卻系統清潔", "防護罩檢查", "對位調校",
            "成品檢測", "尺寸量測", "外觀檢查", "包裝與標示", "工件下料",
            "區域清潔", "刀具歸位", "電源關閉", "日誌填寫", "交班程序"
        };

        static readonly string LoremZh =
            "本步驟需確認所有相關元件的狀態與位置，避免因人為疏漏造成設備損壞。" +
            "首先檢視工件與夾治具的對位，確保偏差在公差範圍內，並使用百分錶或專用對位治具進行交叉驗證；" +
            "若發現對位偏差超過 0.05 mm，應立即停止作業並重新調整，切勿勉強進入下一道工序。\n\n" +
            "接著確認周邊安全裝置——光柵、急停按鈕、安全護蓋、雙手啟動——皆處於可用狀態。" +
            "光柵應遮擋時機具立即停止動作；急停按下後復歸前不可繼續運作；安全護蓋若開啟，連鎖開關須觸發。" +
            "雙手啟動按鈕之延遲設定應符合 ISO 13851 規範，並由安全工程師每月進行例行測試。\n\n" +
            "操作時保持平穩、避免突然動作；視線必須隨時關注工作區內所有移動部位；" +
            "如發現異常聲響、震動、煙霧、油液滲漏或任何感官上的異樣，立即按下急停並通報主管。" +
            "切勿在機具未完全停止前嘗試清除切屑或調整工件，亦不得徒手伸入加工區域。\n\n" +
            "完成此步驟後請填寫紀錄表，記入作業時間、操作員代號、設備編號以及任何異常事件；" +
            "如需暫停作業超過 15 分鐘，應將機具切換至閒置模式並通知下一班接手人員。" +
            "所有紀錄需經主管簽核後存檔於品管系統，保存期限不得少於三年，以利後續追蹤與審查。\n\n" +
            "個人防護裝備（PPE）穿戴方面，作業員應確認安全帽、護目鏡、防割手套、防滑安全鞋與聽力防護具皆已正確配戴。" +
            "若作業涉及切削液或溶劑，須另外加戴耐化學手套與圍裙；高粉塵環境應額外配戴 N95 等級以上之防塵口罩。" +
            "進入作業區前請檢視 PPE 是否有破損、髒污或老化現象，發現異常應立即更換，不可勉強使用。\n\n" +
            "環境準備上，先確認照明充足（建議照度 500 lux 以上）、地面乾燥、走道暢通、消防器材及急救箱位置明確標示；" +
            "切勿在機具周邊堆放雜物、油桶或長條鐵料，避免絆倒或妨礙緊急疏散。" +
            "若發現地面有油漬或冷卻液外漏，應立即以吸油棉或防滑墊處理，並於日誌中記錄維護事項。\n\n" +
            "工具與量具使用時務必校準確認；卡尺、千分尺、塞規、塊規等量具應在使用前歸零並比對標準件。" +
            "扭力扳手應依規格表設定扭力值，超過量程須使用次一級扳手；電動工具使用前檢查線路、開關與接地是否良好。" +
            "若量具校期已逾期，禁止繼續使用，須送回計量室重新校驗並更新貼紙。\n\n" +
            "緊急情況處置：發生火災時依「斷電—警報—滅火—疏散」順序執行；化學品潑灑應依 SDS 規定處置，並通報安全衛生人員；" +
            "人員受傷時保持冷靜，先評估意識與傷情，必要時撥打 119 求援，並向廠區醫護室通報。" +
            "所有事故無論大小皆須在 24 小時內填寫工安事故報告，由主管核章後上呈安衛室存檔，並列入下次月會檢討項目。";

        public static string FakeCourseTitle(int n)
        {
            string[] cats = { "引擎室巡檢", "煞車保養", "電氣安全檢查", "CNC 操作", "塗裝前置", "包裝出貨" };
            return cats[n % cats.Length];
        }

        public static Course FakeCourse(CourseSummary summary)
        {
            var steps = new List<Step>();
            for (int i = 1; i <= 30; i++)
            {
                string main = i <= 10 ? "啟動前檢查"
                            : i <= 20 ? "操作流程"
                            : "收尾與保養";
                string sub = i <= 5 ? "外觀檢查"
                           : i <= 10 ? "儀表確認"
                           : i <= 15 ? "啟動序列"
                           : i <= 20 ? "加工操作"
                           : i <= 25 ? "品質檢驗"
                           : null;

                string title = Titles[(i - 1) % Titles.Length];
                string desc = $"{LoremZh}\n\n本步驟重點：{title}。\n建議停留時間：3 分鐘。\n（測試資料 #{i:00}）";

                steps.Add(new Step(
                    Order: i,
                    MainTitle: main,
                    SubTitle: sub,
                    Name: $"{i:00}. {title}",
                    Description: desc,
                    Media: new Media.None(),
                    NextStepIndication: i % 5 == 0 ? "請移動到下一個工作區域。" : null,
                    Exceptions: new List<ExceptionOption>()
                ));
            }
            return new Course(
                Name: summary.Name,
                DisplayName: summary.DisplayName,
                Introduction: $"這是用來驗證分頁 UI 的測試課程「{summary.DisplayName}」，共 {steps.Count} 個步驟。",
                Steps: steps);
        }
    }
}
