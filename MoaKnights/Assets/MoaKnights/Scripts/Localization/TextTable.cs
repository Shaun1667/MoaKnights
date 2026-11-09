using System;
using System.Collections.Generic;
using System.Text;

namespace MoaKnights.Localization
{
    /// <summary>키 → 언어별 문장 표. CSV 첫 줄은 key,언어코드,언어코드…</summary>
    public sealed class TextTable
    {
        private readonly Dictionary<string, string[]> _rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly List<string> _languages = new List<string>();

        public IReadOnlyList<string> Languages => _languages;
        public IEnumerable<string> Keys => _rows.Keys;

        public static TextTable Parse(string csv)
        {
            var table = new TextTable();
            var records = ReadRecords(csv);
            if (records.Count == 0) throw new FormatException("Text table has no header.");

            var header = records[0];
            for (var i = 1; i < header.Count; i++) table._languages.Add(header[i].Trim());

            for (var r = 1; r < records.Count; r++)
            {
                var record = records[r];
                var key = record[0].Trim();
                if (key.Length == 0) continue;
                if (table._rows.ContainsKey(key)) throw new FormatException("Duplicate text key: " + key);

                var values = new string[table._languages.Count];
                for (var i = 0; i < values.Length; i++)
                    values[i] = i + 1 < record.Count ? record[i + 1] : string.Empty;
                table._rows.Add(key, values);
            }
            return table;
        }

        public bool Contains(string key) => _rows.ContainsKey(key);

        /// <summary>해당 언어 문장을 찾는다. 비어 있으면 첫 언어(기본 언어)로 대체한다.</summary>
        public bool TryGet(string key, string language, out string value)
        {
            value = null;
            if (!_rows.TryGetValue(key, out var values) || values.Length == 0) return false;

            var index = _languages.IndexOf(language);
            value = index >= 0 ? values[index] : string.Empty;
            if (string.IsNullOrEmpty(value)) value = values[0];
            return !string.IsNullOrEmpty(value);
        }

        private static List<List<string>> ReadRecords(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var start = csv.Length > 0 && csv[0] == '﻿' ? 1 : 0;

            for (var i = start; i < csv.Length; i++)
            {
                var c = csv[i];
                if (quoted)
                {
                    if (c != '"') { field.Append(c); continue; }
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                    continue;
                }

                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': record.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        record.Add(field.ToString()); field.Clear();
                        records.Add(record); record = new List<string>();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }
}
