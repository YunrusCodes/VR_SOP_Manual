using System;
using System.IO;
using Inspection.Domain;

class Program
{
    static int Main(string[] args)
    {
        var path = args.Length > 0
            ? args[0]
            : Path.Combine("..", "..", "docs", "sample-data", "storage", "acme",
                           "engine-room-inspection", "engine-room-inspection.csv");
        path = Path.GetFullPath(path);
        var csv = File.ReadAllText(path);
        var parser = new CsvParser(w => Console.WriteLine($"WARN: {w}"));
        var course = parser.Parse(csv, "engine-room-inspection", "引擎室巡檢");

        int failed = 0;
        void Check(bool ok, string desc)
        {
            Console.WriteLine($"  [{(ok ? "OK" : "FAIL")}] {desc}");
            if (!ok) failed++;
        }

        Check(course.Steps.Count == 9, $"step count = 9 (got {course.Steps.Count})");
        var s1 = course.Steps[0];
        Check(s1.Order == 1, "step1.order=1");
        Check(s1.MainTitle == "啟動前", $"step1.main='啟動前' (got '{s1.MainTitle}')");
        Check(s1.SubTitle == "引擎蓋", $"step1.sub='引擎蓋' (got '{s1.SubTitle}')");
        Check(s1.Media is Media.Image img && img.FileName == "engine-hood.jpg", "step1.media=Image('engine-hood.jpg')");
        Check(s1.NextStepIndication == "請走到車頭前方", "step1.nextIndication");
        Check(s1.Exceptions.Count == 0, "step1.exceptions=0");

        var s2 = course.Steps[1];
        Check(s2.Exceptions.Count == 3, $"step2.exceptions=3 (got {s2.Exceptions.Count})");
        Check(s2.Exceptions[0].Action is ExceptionAction.GoToStep g0 && g0.Step == 8, "step2.ex0=Goto(8)");
        Check(s2.Exceptions[1].Action is ExceptionAction.GoToStep g1 && g1.Step == 9, "step2.ex1=Goto(9)");
        Check(s2.Exceptions[2].Action is ExceptionAction.ShowMessage, "step2.ex2=Message");
        Check(((ExceptionAction.ShowMessage)s2.Exceptions[2].Action).Text.Contains("立即停止操作"), "step2.ex2.text contains '立即停止操作'");

        var s4 = course.Steps[3];
        Check(s4.Media is Media.None, "step4.media=None");
        Check(s4.Exceptions.Count == 0, "step4.exceptions=0");

        var s5 = course.Steps[4];
        Check(s5.SubTitle == null, $"step5.sub=null (got '{s5.SubTitle}')");
        Check(s5.Media is Media.Video v && v.FileName == "startup.mp4", "step5.media=Video('startup.mp4')");

        var s6 = course.Steps[5];
        Check(s6.Exceptions.Count == 2, $"step6.exceptions=2 (got {s6.Exceptions.Count})");
        Check(s6.Exceptions[0].Action is ExceptionAction.ShowMessage, "step6.ex0=Message");
        Check(s6.Exceptions[1].Action is ExceptionAction.GoToStep g8 && g8.Step == 8, "step6.ex1=Goto(8)");

        var s7 = course.Steps[6];
        Check(s7.NextStepIndication == "完成後請熄火並走出車外", "step7.nextIndication");

        // Quoted field with embedded comma
        var qcsv = "\"intro\"\nh1,h2,h3,h4,h5,h6,h7,h8,h9,h10,h11,h12,h13\n" +
                   "1,m,s,n,\"a, b\nc\",,,,,,,,";
        var qc = parser.Parse(qcsv, "x");
        Check(qc.Steps.Count == 1, "quoted: 1 step");
        Check(qc.Steps[0].Description == "a, b\nc", $"quoted desc='a, b\\nc' (got '{qc.Steps[0].Description}')");

        // Escaped quotes
        var escCsv = "\"intro\"\nh1,h2,h3,h4,h5,h6,h7,h8,h9,h10,h11,h12,h13\n" +
                     "1,m,,n,\"He said \"\"hi\"\"\",,,,,,,,";
        var ec = parser.Parse(escCsv, "x");
        Check(ec.Steps[0].Description == "He said \"hi\"", $"escaped quotes (got '{ec.Steps[0].Description}')");

        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "All checks passed." : $"{failed} check(s) FAILED.");
        return failed == 0 ? 0 : 1;
    }
}
