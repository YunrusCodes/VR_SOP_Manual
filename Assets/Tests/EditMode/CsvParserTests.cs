using System.IO;
using Inspection.Domain;
using NUnit.Framework;

namespace Inspection.Tests
{
    [TestFixture]
    public class CsvParserTests
    {
        const string CourseName = "engine-room-inspection";
        const string DisplayName = "引擎室巡檢";

        // Located via Application.dataPath at runtime; in EditMode this resolves to <project>/Assets.
        // We walk up to <project>/docs/sample-data/storage/acme/engine-room-inspection/...
        static string ProjectRoot
        {
            get
            {
                var dataPath = UnityEngine.Application.dataPath;
                return Path.GetDirectoryName(dataPath);
            }
        }

        static string SampleCsvPath =>
            Path.Combine(ProjectRoot, "docs", "sample-data", "storage", "acme", CourseName, $"{CourseName}.csv");

        [Test]
        public void Parse_SampleCsv_HasNineSteps()
        {
            var csv = File.ReadAllText(SampleCsvPath);
            var course = new CsvParser().Parse(csv, CourseName, DisplayName);

            Assert.AreEqual(CourseName, course.Name);
            Assert.AreEqual(DisplayName, course.DisplayName);
            StringAssert.Contains("引擎室例行巡檢", course.Introduction);
            Assert.AreEqual(9, course.Steps.Count);
        }

        [Test]
        public void Parse_Step1_HasImageAndNextIndication()
        {
            var course = ParseSample();
            var s1 = course.Steps[0];
            Assert.AreEqual(1, s1.Order);
            Assert.AreEqual("啟動前", s1.MainTitle);
            Assert.AreEqual("引擎蓋", s1.SubTitle);
            Assert.AreEqual("開啟引擎蓋", s1.Name);
            Assert.IsInstanceOf<Media.Image>(s1.Media);
            Assert.AreEqual("engine-hood.jpg", ((Media.Image)s1.Media).FileName);
            Assert.AreEqual("請走到車頭前方", s1.NextStepIndication);
            Assert.AreEqual(0, s1.Exceptions.Count);
        }

        [Test]
        public void Parse_Step2_HasThreeExceptions_MixOfGotoAndMessage()
        {
            var course = ParseSample();
            var s2 = course.Steps[1];
            Assert.AreEqual(3, s2.Exceptions.Count);

            Assert.AreEqual("油位過低", s2.Exceptions[0].Label);
            Assert.IsInstanceOf<ExceptionAction.GoToStep>(s2.Exceptions[0].Action);
            Assert.AreEqual(8, ((ExceptionAction.GoToStep)s2.Exceptions[0].Action).Step);

            Assert.AreEqual("顏色偏黑", s2.Exceptions[1].Label);
            Assert.IsInstanceOf<ExceptionAction.GoToStep>(s2.Exceptions[1].Action);
            Assert.AreEqual(9, ((ExceptionAction.GoToStep)s2.Exceptions[1].Action).Step);

            Assert.AreEqual("含金屬屑或氣味異常", s2.Exceptions[2].Label);
            Assert.IsInstanceOf<ExceptionAction.ShowMessage>(s2.Exceptions[2].Action);
            StringAssert.Contains("立即停止操作", ((ExceptionAction.ShowMessage)s2.Exceptions[2].Action).Text);
        }

        [Test]
        public void Parse_Step4_NoMedia_NoExceptions()
        {
            var course = ParseSample();
            var s4 = course.Steps[3];
            Assert.IsInstanceOf<Media.None>(s4.Media);
            Assert.AreEqual(0, s4.Exceptions.Count);
            Assert.IsNull(s4.NextStepIndication);
        }

        [Test]
        public void Parse_Step5_HasVideo_NoSubTitle()
        {
            var course = ParseSample();
            var s5 = course.Steps[4];
            Assert.IsNull(s5.SubTitle);
            Assert.IsInstanceOf<Media.Video>(s5.Media);
            Assert.AreEqual("startup.mp4", ((Media.Video)s5.Media).FileName);
        }

        [Test]
        public void Parse_Step6_TwoExceptions_MixedOrder()
        {
            var course = ParseSample();
            var s6 = course.Steps[5];
            Assert.AreEqual(2, s6.Exceptions.Count);
            Assert.IsInstanceOf<ExceptionAction.ShowMessage>(s6.Exceptions[0].Action);
            Assert.IsInstanceOf<ExceptionAction.GoToStep>(s6.Exceptions[1].Action);
            Assert.AreEqual(8, ((ExceptionAction.GoToStep)s6.Exceptions[1].Action).Step);
        }

        [Test]
        public void Parse_BomIsStripped()
        {
            var withBom = "﻿\"intro\"\nh1,h2,h3,h4,h5,h6,h7,h8,h9,h10,h11,h12,h13\n1,m,,n,d,,,,,,,,";
            var course = new CsvParser().Parse(withBom, "x");
            Assert.AreEqual("intro", course.Introduction);
            Assert.AreEqual(1, course.Steps.Count);
        }

        [Test]
        public void Parse_QuotedFieldWithCommaAndNewline()
        {
            var csv = "\"intro\"\n" +
                      "h1,h2,h3,h4,h5,h6,h7,h8,h9,h10,h11,h12,h13\n" +
                      "1,main,sub,name,\"line1, with comma\nline2\",,,,,,,,";
            var course = new CsvParser().Parse(csv, "x");
            Assert.AreEqual(1, course.Steps.Count);
            StringAssert.Contains("with comma", course.Steps[0].Description);
            StringAssert.Contains("line2", course.Steps[0].Description);
        }

        [Test]
        public void Parse_EscapedDoubleQuote()
        {
            var csv = "\"intro\"\n" +
                      "h1,h2,h3,h4,h5,h6,h7,h8,h9,h10,h11,h12,h13\n" +
                      "1,main,,name,\"He said \"\"hi\"\"\",,,,,,,,";
            var course = new CsvParser().Parse(csv, "x");
            Assert.AreEqual("He said \"hi\"", course.Steps[0].Description);
        }

        static Course ParseSample()
        {
            var csv = File.ReadAllText(SampleCsvPath);
            return new CsvParser().Parse(csv, CourseName, DisplayName);
        }
    }
}
