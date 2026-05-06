using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Inspection.Domain
{
    public sealed class CsvParser
    {
        const int ExpectedColumns = 13;

        public delegate void WarningHandler(string message);
        readonly WarningHandler _warn;

        public CsvParser(WarningHandler warn = null) => _warn = warn ?? (_ => { });

        public Course Parse(string csvText, string courseName, string displayName = null)
        {
            if (csvText == null) throw new ArgumentNullException(nameof(csvText));
            if (string.IsNullOrEmpty(courseName)) throw new ArgumentException("courseName required", nameof(courseName));

            if (csvText.Length > 0 && csvText[0] == '﻿')
                csvText = csvText.Substring(1);

            var rows = ReadRows(csvText);
            if (rows.Count < 2)
                throw new FormatException("CSV must contain at least an introduction row and a header row.");

            var introduction = rows[0].Count > 0 ? (rows[0][0] ?? string.Empty) : string.Empty;
            // rows[1] is header — ignored per spec (parse by index, not header text).

            var steps = new List<Step>();
            for (int i = 2; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Count == 0 || (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])))
                    continue; // skip blank lines

                if (row.Count != ExpectedColumns)
                    _warn($"Row {i + 1}: expected {ExpectedColumns} columns, got {row.Count}.");

                steps.Add(ParseStep(row, rowNumber: i + 1));
            }

            return new Course(
                Name: courseName,
                DisplayName: displayName ?? courseName,
                Introduction: introduction,
                Steps: steps);
        }

        Step ParseStep(IReadOnlyList<string> row, int rowNumber)
        {
            string Get(int i) => i < row.Count ? row[i] : string.Empty;

            int order;
            if (!int.TryParse(Get(0), out order))
            {
                _warn($"Row {rowNumber}: invalid Step Order '{Get(0)}'.");
                order = 0;
            }

            var mainTitle = Get(1);
            var subTitle = NullIfEmpty(Get(2));
            var name = Get(3);
            var description = Get(4);
            var media = ParseMedia(Get(5));
            var nextStepIndication = NullIfEmpty(Get(6));

            var exceptions = new List<ExceptionOption>(3);
            for (int slot = 0; slot < 3; slot++)
            {
                var labelCol = 7 + slot * 2;
                var actionCol = labelCol + 1;
                var label = Get(labelCol);
                var actionStr = Get(actionCol);
                if (string.IsNullOrEmpty(label))
                    continue;
                exceptions.Add(new ExceptionOption(label, ParseAction(actionStr)));
            }

            return new Step(
                Order: order,
                MainTitle: mainTitle,
                SubTitle: subTitle,
                Name: name,
                Description: description,
                Media: media,
                NextStepIndication: nextStepIndication,
                Exceptions: exceptions);
        }

        static Media ParseMedia(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return new Media.None();
            var dot = raw.LastIndexOf('.');
            var ext = dot >= 0 ? raw.Substring(dot + 1).ToLowerInvariant() : string.Empty;
            return ext switch
            {
                "jpg" or "jpeg" or "png" => new Media.Image(raw),
                "mp4" => new Media.Video(raw),
                _ => new Media.None(),
            };
        }

        static ExceptionAction ParseAction(string raw)
        {
            if (int.TryParse(raw, out var step))
                return new ExceptionAction.GoToStep(step);
            return new ExceptionAction.ShowMessage(raw ?? string.Empty);
        }

        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        // RFC 4180-ish CSV reader. Handles quoted fields, escaped quotes ("" → "),
        // and embedded newlines inside quoted fields.
        internal static List<List<string>> ReadRows(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            using var sr = new StringReader(text);
            int ic;
            while ((ic = sr.Read()) != -1)
            {
                char c = (char)ic;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (sr.Peek() == '"')
                        {
                            field.Append('"');
                            sr.Read();
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        if (sr.Peek() == '\n') sr.Read();
                        row.Add(field.ToString());
                        field.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
}
