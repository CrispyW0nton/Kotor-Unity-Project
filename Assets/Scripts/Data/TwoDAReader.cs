using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KotORUnity.Data
{
    /// <summary>
    /// Parses BioWare 2DA (Two-Dimensional Array) files.
    ///
    /// 2DA files are tab/space separated tables used for nearly all static
    /// game data in KotOR: classes.2da, baseitems.2da, spells.2da,
    /// feat.2da, portraits.2da, appearance.2da, placeables.2da, etc.
    ///
    /// Format:
    ///   Line 0:  "2DA V2.0"
    ///   Line 1:  blank
    ///   Line 2:  column headers (tab/space separated)
    ///   Line 3+: rows, first token is row index (integer), remaining are values
    ///            "**** " means empty/null cell
    ///
    /// Usage:
    ///   var table = TwoDAReader.Load(bytes);
    ///   string label = table.GetString(5, "label");
    ///   int hitdie  = table.GetInt(2, "hitdie");
    /// </summary>
    public class TwoDATable
    {
        // ── DATA ──────────────────────────────────────────────────────────────
        private readonly List<string> _columns = new List<string>();
        private readonly Dictionary<int, Dictionary<string, string>> _rows
            = new Dictionary<int, Dictionary<string, string>>();

        // Column index lookup for O(1) row parsing
        private readonly Dictionary<string, int> _colIndex = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        public string Name { get; set; } = "";
        public int RowCount => _rows.Count;
        public IReadOnlyList<string> Columns => _columns;

        // ── ACCESSORS ─────────────────────────────────────────────────────────
        public bool HasRow(int row) => _rows.ContainsKey(row);
        public bool HasColumn(string col) => _colIndex.ContainsKey(col);

        public string GetString(int row, string col, string def = "")
        {
            if (!_rows.TryGetValue(row, out var r)) return def;
            if (!r.TryGetValue(col.ToLowerInvariant(), out var v)) return def;
            return v == "****" ? def : v;
        }

        public int GetInt(int row, string col, int def = 0)
        {
            string v = GetString(row, col, null);
            if (v == null) return def;
            return int.TryParse(v, out int result) ? result : def;
        }

        public float GetFloat(int row, string col, float def = 0f)
        {
            string v = GetString(row, col, null);
            if (v == null) return def;
            return float.TryParse(v,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : def;
        }

        public bool GetBool(int row, string col, bool def = false)
        {
            int v = GetInt(row, col, -1);
            if (v == -1) return def;
            return v != 0;
        }

        /// <summary>Find the first row where column equals value (case-insensitive).</summary>
        public int FindRow(string col, string value)
        {
            string lv = value?.ToLowerInvariant() ?? "";
            foreach (var kvp in _rows)
                if (kvp.Value.TryGetValue(col.ToLowerInvariant(), out var v)
                    && string.Equals(v, lv, StringComparison.OrdinalIgnoreCase))
                    return kvp.Key;
            return -1;
        }

        // ── INTERNAL BUILD ────────────────────────────────────────────────────
        internal void SetColumns(List<string> cols)
        {
            _columns.Clear();
            _colIndex.Clear();
            for (int i = 0; i < cols.Count; i++)
            {
                string c = cols[i].ToLowerInvariant();
                _columns.Add(c);
                _colIndex[c] = i;
            }
        }

        internal void AddRow(int index, List<string> values)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _columns.Count && i < values.Count; i++)
                row[_columns[i]] = values[i];
            _rows[index] = row;
        }

        // ── MOD PATCH API ─────────────────────────────────────────────────────

        /// <summary>Append a new row at the end of the table using a column-value dictionary.</summary>
        public void AppendRow(Dictionary<string, string> rowData)
        {
            int nextIdx = _rows.Count > 0 ? _rows.Keys.Max() + 1 : 0;
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Fill defaults
            foreach (var col in _columns) row[col] = "****";
            // Apply provided values
            if (rowData != null)
                foreach (var kv in rowData)
                    row[kv.Key.ToLowerInvariant()] = kv.Value;
            _rows[nextIdx] = row;
        }

        /// <summary>Set a specific cell value in an existing row.</summary>
        public void SetCell(int row, string column, string value)
        {
            if (!_rows.TryGetValue(row, out var r)) return;
            r[column.ToLowerInvariant()] = value ?? "****";
        }

        /// <summary>Delete a row by index.</summary>
        public void DeleteRow(int row) => _rows.Remove(row);
    }

    /// <summary>
    /// Static parser + global cache for 2DA tables.
    /// </summary>
    public static class TwoDAReader
    {
        // ── CACHE ─────────────────────────────────────────────────────────────
        private static readonly Dictionary<string, TwoDATable> _cache
            = new Dictionary<string, TwoDATable>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache() => _cache.Clear();

        // ── LOAD FROM BYTES ───────────────────────────────────────────────────
        public static TwoDATable Load(byte[] data, string name = "")
        {
            if (data == null || data.Length == 0) return null;

            // Check cache
            if (!string.IsNullOrEmpty(name) && _cache.TryGetValue(name, out var cached))
                return cached;

            try
            {
                // KotOR .2da files are Windows-1252 encoded, not UTF-8.
                // Using UTF-8 corrupts high-byte characters and can break
                // line-ending detection. Fall back to Latin-1 (code page 1252
                // superset) which is a safe 1:1 byte mapping for ASCII content.
                System.Text.Encoding enc;
                try   { enc = System.Text.Encoding.GetEncoding(1252); }
                catch { enc = System.Text.Encoding.GetEncoding("iso-8859-1"); } // Mono/Unity fallback

                string text = enc.GetString(data);
                var table = ParseText(text);
                table.Name = name;

                if (!string.IsNullOrEmpty(name))
                    _cache[name] = table;

                return table;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TwoDAReader] Failed to parse '{name}': {e.Message}");
                return null;
            }
        }

        /// <summary>Load a 2DA from a file path (editor/standalone).</summary>
        public static TwoDATable LoadFile(string path)
        {
            if (!File.Exists(path)) return null;
            string name = Path.GetFileNameWithoutExtension(path);
            return Load(File.ReadAllBytes(path), name);
        }

        // ── PARSER ────────────────────────────────────────────────────────────
        private static TwoDATable ParseText(string text)
        {
            var table = new TwoDATable();

            // Normalise line endings: \r\n and \r both become \n
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            // Split into non-null lines (keep empty ones for positional parsing)
            var allLines = text.Split('\n');
            int cursor = 0;

            // ── Line 0: version header ─────────────────────────────────────────
            while (cursor < allLines.Length && string.IsNullOrWhiteSpace(allLines[cursor]))
                cursor++;   // skip any leading blank lines

            if (cursor >= allLines.Length)
                throw new Exception("Empty 2DA file");

            string header = allLines[cursor++].Trim();
            if (!header.StartsWith("2DA", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Not a valid 2DA file (header='{header}')");

            // ── Line 1: blank separator (optional in some modded files) ────────
            // Skip exactly one blank line if present
            if (cursor < allLines.Length && string.IsNullOrWhiteSpace(allLines[cursor]))
                cursor++;

            // ── Line 2: column names ───────────────────────────────────────────
            if (cursor >= allLines.Length)
                throw new Exception("Missing column header line");

            var cols = Tokenize(allLines[cursor++]);
            table.SetColumns(cols);

            // ── Lines 3+: data rows ────────────────────────────────────────────
            for (; cursor < allLines.Length; cursor++)
            {
                string line = allLines[cursor];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var tokens = Tokenize(line);
                if (tokens.Count == 0) continue;

                // First token is the row index (integer)
                if (!int.TryParse(tokens[0], out int rowIdx)) continue;
                var values = tokens.GetRange(1, tokens.Count - 1);
                table.AddRow(rowIdx, values);
            }

            return table;
        }

        /// <summary>
        /// Tokenizes a 2DA line.
        /// Quoted strings are kept as single tokens.
        /// Unquoted tokens are split on whitespace.
        /// </summary>
        private static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                // Skip whitespace
                while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
                if (i >= line.Length) break;

                if (line[i] == '"')
                {
                    // Quoted token
                    i++;
                    int start = i;
                    while (i < line.Length && line[i] != '"') i++;
                    tokens.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++; // skip closing quote
                }
                else
                {
                    // Unquoted token
                    int start = i;
                    while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                    tokens.Add(line.Substring(start, i - start));
                }
            }
            return tokens;
        }
    }
}
