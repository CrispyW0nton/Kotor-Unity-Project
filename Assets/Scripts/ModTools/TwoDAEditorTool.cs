using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KotORUnity.Data;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  2DA TABLE EDITOR  —  Runtime data model + serialisation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Editable wrapper around a TwoDATable.
    ///
    /// Modders can:
    ///   1. Load any .2da from the archives (or from a file on disk).
    ///   2. Edit any cell value.
    ///   3. Add or delete rows.
    ///   4. Add new columns.
    ///   5. Save back to a .2da text file that can be placed in the Override folder.
    ///   6. Export as C# const snippet for code-side integration.
    ///
    /// The Unity Editor window is in Assets/Editor/ModTools/TwoDAEditorWindow.cs.
    /// </summary>
    public class TwoDAEditor
    {
        // ── PUBLIC STATE ──────────────────────────────────────────────────────
        public string          TableName  { get; private set; }
        public List<string>    Columns    { get; private set; } = new List<string>();
        public List<string[]>  Rows       { get; private set; } = new List<string[]>();
        public bool            IsDirty    { get; private set; }

        // ── LOAD ──────────────────────────────────────────────────────────────

        /// <summary>Load from a TwoDATable (already parsed from archives).</summary>
        public void LoadFromTable(TwoDATable table, string name)
        {
            TableName = name;
            Columns   = new List<string>(table.Columns);
            Rows      = new List<string[]>();
            for (int r = 0; r < table.RowCount; r++)
            {
                var row = new string[Columns.Count];
                for (int c = 0; c < Columns.Count; c++)
                    row[c] = table.GetString(r, Columns[c]);
                Rows.Add(row);
            }
            IsDirty = false;
        }

        /// <summary>Load directly from raw .2da bytes.</summary>
        public void LoadFromBytes(byte[] data, string name)
        {
            var table = TwoDAReader.Load(data, name);
            if (table != null) LoadFromTable(table, name);
        }

        /// <summary>Load from a file path.</summary>
        public void LoadFromFile(string path)
        {
            var table = TwoDAReader.LoadFile(path);
            if (table != null) LoadFromTable(table, Path.GetFileNameWithoutExtension(path));
        }

        // ── EDIT ──────────────────────────────────────────────────────────────

        /// <summary>Set a cell value. Row and column are 0-based.</summary>
        public void SetCell(int row, int col, string value)
        {
            if (row < 0 || row >= Rows.Count) return;
            if (col < 0 || col >= Columns.Count) return;
            Rows[row][col] = value ?? "****";
            IsDirty = true;
        }

        /// <summary>Set a cell value by column name.</summary>
        public void SetCell(int row, string colName, string value)
        {
            int col = Columns.IndexOf(colName);
            if (col < 0)
            {
                Debug.LogWarning($"[2DAEditor] Column not found: '{colName}'");
                return;
            }
            SetCell(row, col, value);
        }

        /// <summary>Add a new empty row. Returns the new row index.</summary>
        public int AddRow()
        {
            var row = new string[Columns.Count];
            for (int i = 0; i < row.Length; i++) row[i] = "****";
            Rows.Add(row);
            IsDirty = true;
            return Rows.Count - 1;
        }

        /// <summary>Duplicate an existing row. Returns the new row index.</summary>
        public int DuplicateRow(int sourceRow)
        {
            if (sourceRow < 0 || sourceRow >= Rows.Count) return -1;
            var copy = (string[])Rows[sourceRow].Clone();
            Rows.Add(copy);
            IsDirty = true;
            return Rows.Count - 1;
        }

        /// <summary>Delete a row by index.</summary>
        public void DeleteRow(int row)
        {
            if (row < 0 || row >= Rows.Count) return;
            Rows.RemoveAt(row);
            IsDirty = true;
        }

        /// <summary>Add a new column with a default value. Returns column index.</summary>
        public int AddColumn(string colName, string defaultValue = "****")
        {
            if (Columns.Contains(colName))
            {
                Debug.LogWarning($"[2DAEditor] Column already exists: '{colName}'");
                return Columns.IndexOf(colName);
            }
            Columns.Add(colName);
            foreach (var row in Rows)
            {
                // Extend the row array
                var extended = new string[row.Length + 1];
                Array.Copy(row, extended, row.Length);
                extended[row.Length] = defaultValue;
                Rows[Rows.IndexOf(row)] = extended;
            }
            IsDirty = true;
            return Columns.Count - 1;
        }

        // ── QUERY ─────────────────────────────────────────────────────────────

        public string GetCell(int row, int col)
        {
            if (row < 0 || row >= Rows.Count || col < 0 || col >= Columns.Count)
                return "****";
            return Rows[row][col];
        }

        public string GetCell(int row, string colName)
        {
            int col = Columns.IndexOf(colName);
            return col < 0 ? "****" : GetCell(row, col);
        }

        /// <summary>
        /// Find rows where a column matches a value (case-insensitive).
        /// Returns a list of matching row indices.
        /// </summary>
        public List<int> FindRows(string colName, string value)
        {
            var result = new List<int>();
            int col = Columns.IndexOf(colName);
            if (col < 0) return result;
            for (int r = 0; r < Rows.Count; r++)
                if (string.Equals(Rows[r][col], value, StringComparison.OrdinalIgnoreCase))
                    result.Add(r);
            return result;
        }

        // ── SAVE / EXPORT ─────────────────────────────────────────────────────

        /// <summary>
        /// Serialise to .2da text format.
        /// The output can be placed in the Override/ folder to override the base game table.
        /// </summary>
        public string ToTwoDAText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("2DA V2.0");
            sb.AppendLine();  // blank line required by format

            // Column header line — row number placeholder + column names
            sb.Append("    ");
            foreach (var col in Columns)
                sb.Append(col.PadRight(20));
            sb.AppendLine();

            // Data rows
            for (int r = 0; r < Rows.Count; r++)
            {
                sb.Append($"{r,-4}");
                for (int c = 0; c < Columns.Count; c++)
                {
                    string val = Rows[r][c];
                    if (string.IsNullOrEmpty(val)) val = "****";
                    // Quote values with spaces
                    if (val.Contains(' ')) val = $"\"{val}\"";
                    sb.Append(val.PadRight(20));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Save to disk as .2da text file.</summary>
        public bool SaveToFile(string outputPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, ToTwoDAText(), Encoding.UTF8);
                IsDirty = false;
                Debug.Log($"[2DAEditor] Saved: {outputPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[2DAEditor] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export C# constants snippet for the table's label column.
        /// Useful for generating typed IDs (e.g., FeatId.Toughness = 4).
        /// </summary>
        public string ExportCSharpConstants(string className = null, string labelColumn = "label")
        {
            className = className ?? $"{TableName}Ids";
            var sb = new StringBuilder();
            sb.AppendLine($"// Auto-generated from {TableName}.2da");
            sb.AppendLine($"public static class {className}");
            sb.AppendLine("{");
            int labelCol = Columns.IndexOf(labelColumn);
            for (int r = 0; r < Rows.Count; r++)
            {
                string label = labelCol >= 0 ? Rows[r][labelCol] : $"Row{r}";
                if (label == "****" || string.IsNullOrWhiteSpace(label)) continue;
                // Sanitize label to valid C# identifier
                string id = System.Text.RegularExpressions.Regex.Replace(label, @"[^A-Za-z0-9_]", "_");
                if (char.IsDigit(id[0])) id = "_" + id;
                sb.AppendLine($"    public const int {id} = {r};");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        public void MarkClean() => IsDirty = false;

        // ── EDITOR GUI STUB (used by EditorWindow) ────────────────────────────
        /// <summary>Table currently loaded for editing. Null if no table loaded.</summary>
        public KotORUnity.Data.TwoDATable CurrentTable { get; private set; }
        /// <summary>Name of the currently loaded table.</summary>
        public string CurrentTableName { get; private set; } = "";

        // Override LoadFromTable to cache references for the EditorWindow
        // (The existing LoadFromTable sets an internal copy; we just store handles here)
        public void LoadFromTableExt(KotORUnity.Data.TwoDATable table, string name)
        {
            CurrentTable     = table;
            CurrentTableName = name;
            LoadFromTable(table, name);
        }

        /// <summary>Placeholder IMGUI draw — extend as needed.</summary>
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (CurrentTable == null) return;
            UnityEditor.EditorGUILayout.LabelField(
                $"Rows: {CurrentTable.RowCount}  |  Dirty: {IsDirty}",
                UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
