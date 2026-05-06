using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inspection.Domain;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection.Net
{
    public sealed class CourseClient : ICourseClient
    {
        readonly string _baseUrl;
        readonly string _company;
        readonly CsvParser _parser;

        public CourseClient(string baseUrl, string company, CsvParser parser)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("baseUrl required", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(company)) throw new ArgumentException("company required", nameof(company));
            _baseUrl = baseUrl.TrimEnd('/');
            _company = company;
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public async Task<IReadOnlyList<CourseSummary>> ListCoursesAsync(CancellationToken ct)
        {
            var url = $"{_baseUrl}/companies/{Esc(_company)}/courses";
            var json = await GetStringAsync(url, ct);
            return ParseCourseList(json);
        }

        public async Task<Course> GetCourseAsync(string courseName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(courseName)) throw new ArgumentException("courseName required", nameof(courseName));
            var listing = await ListCoursesAsync(ct);
            string displayName = courseName;
            foreach (var c in listing)
            {
                if (c.Name == courseName) { displayName = c.DisplayName; break; }
            }

            var url = $"{_baseUrl}/companies/{Esc(_company)}/courses/{Esc(courseName)}/csv";
            var csv = await GetStringAsync(url, ct);
            return _parser.Parse(csv, courseName, displayName);
        }

        public string GetImageUrl(string courseName, string fileName) =>
            $"{_baseUrl}/companies/{Esc(_company)}/courses/{Esc(courseName)}/files/image/{Esc(fileName)}";

        public string GetVideoUrl(string courseName, string fileName) =>
            $"{_baseUrl}/companies/{Esc(_company)}/courses/{Esc(courseName)}/files/video/{Esc(fileName)}";

        static string Esc(string s) => UnityWebRequest.EscapeURL(s ?? string.Empty);

        static async Task<string> GetStringAsync(string url, CancellationToken ct)
        {
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    ct.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            if (req.result != UnityWebRequest.Result.Success)
                throw new ApiException(url, (int)req.responseCode, req.error ?? "request failed");
            return req.downloadHandler.text;
        }

        // Minimal hand-rolled parse of {"company":"...","courses":[{"name":"...","displayName":"..."}, ...]}
        // to avoid dragging Newtonsoft into the runtime asmdef. We only consume this single shape.
        static IReadOnlyList<CourseSummary> ParseCourseList(string json)
        {
            var result = new List<CourseSummary>();
            if (string.IsNullOrEmpty(json)) return result;

            int i = json.IndexOf("\"courses\"", StringComparison.Ordinal);
            if (i < 0) return result;
            i = json.IndexOf('[', i);
            if (i < 0) return result;
            int end = json.IndexOf(']', i);
            if (end < 0) return result;

            var arr = json.Substring(i + 1, end - i - 1);
            int p = 0;
            while (p < arr.Length)
            {
                int objStart = arr.IndexOf('{', p);
                if (objStart < 0) break;
                int objEnd = arr.IndexOf('}', objStart);
                if (objEnd < 0) break;
                var obj = arr.Substring(objStart + 1, objEnd - objStart - 1);
                var name = ExtractField(obj, "name");
                var disp = ExtractField(obj, "displayName");
                if (!string.IsNullOrEmpty(name))
                    result.Add(new CourseSummary(name, string.IsNullOrEmpty(disp) ? name : disp));
                p = objEnd + 1;
            }
            return result;
        }

        static string ExtractField(string obj, string key)
        {
            var marker = "\"" + key + "\"";
            int k = obj.IndexOf(marker, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = obj.IndexOf(':', k);
            if (colon < 0) return null;
            int q1 = obj.IndexOf('"', colon + 1);
            if (q1 < 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int j = q1 + 1; j < obj.Length; j++)
            {
                var c = obj[j];
                if (c == '\\' && j + 1 < obj.Length)
                {
                    var n = obj[j + 1];
                    sb.Append(n switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => n });
                    j++;
                }
                else if (c == '"') return sb.ToString();
                else sb.Append(c);
            }
            return null;
        }
    }

    public sealed class ApiException : Exception
    {
        public string Url { get; }
        public int StatusCode { get; }
        public ApiException(string url, int statusCode, string message)
            : base($"{message} (HTTP {statusCode}, {url})")
        {
            Url = url;
            StatusCode = statusCode;
        }
    }
}
