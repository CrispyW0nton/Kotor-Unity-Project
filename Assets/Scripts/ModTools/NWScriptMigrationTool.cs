using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
#pragma warning disable 0414, 0219

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  NWSCRIPT MIGRATION TOOL  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Result of a single file migration.
    /// </summary>
    public class MigrationResult
    {
        public bool   Success;
        public string SourcePath;
        public string OutputPath;
        public string CSharpCode;
        public List<MigrationNote> Notes = new List<MigrationNote>();
        public int    LinesConverted;
        public int    UnsupportedFunctions;
    }

    /// <summary>
    /// A note produced during migration (info, warning, or manual-action required).
    /// </summary>
    public class MigrationNote
    {
        public enum NoteType { Info, Warning, ManualRequired }

        public NoteType Type;
        public int      SourceLine;
        public string   Message;
        public string   Suggestion;

        public override string ToString() =>
            $"[{Type}] Line {SourceLine}: {Message}" +
            (string.IsNullOrEmpty(Suggestion) ? "" : $"\n  → {Suggestion}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  NWSCRIPT MIGRATION TOOL SERVICE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts KotOR NWScript (.nss) source files to C# stubs compatible with
    /// the KotOR Unity port's NWScriptVM and EventBus patterns.
    ///
    /// CONVERSION COVERAGE:
    ///   ✅ Variable declarations (int, float, string, object → C# equivalents)
    ///   ✅ Arithmetic, comparison, logical operators
    ///   ✅ if/else, while, for, switch/case statements
    ///   ✅ Function declarations → C# methods with return types
    ///   ✅ void main() → the standard KotOR entry point → RunScript
    ///   ✅ int StartingConditional() → RunCondition
    ///   ✅ Common engine calls mapped to NWScriptVM statics
    ///   ⚠️  #include expansion (partially: flags for manual review)
    ///   ⚠️  struct types (partially: comment placeholder + note)
    ///   ❌  object handles — C# uses GameObject references (manual fix required)
    ///   ❌  effect/talent/itemproperty types — replaced with stubs + notes
    ///
    /// OUTPUT FORMAT:
    ///   A C# class in namespace KotORUnity.ModScripts implementing IModScript.
    ///   The class is auto-registered with the NWScriptVM registry via
    ///   [ConsoleCommand] attribute on static Run / RunCondition entry points.
    /// </summary>
    public class NWScriptMigrationTool
    {
        // ── CONSTANTS ─────────────────────────────────────────────────────────
        public const string OutputExtension = ".cs";
        private const string DefaultFolder  = "ModOutput/Scripts";
        private const string Namespace      = "KotORUnity.ModScripts";
        private const string Indent         = "    ";

        // ── TYPE MAP  (NWScript → C#) ─────────────────────────────────────────
        private static readonly Dictionary<string, string> TypeMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "int",           "int"         },
            { "float",         "float"       },
            { "string",        "string"      },
            { "void",          "void"        },
            { "object",        "GameObject"  },
            { "location",      "Vector3"     },
            { "vector",        "Vector3"     },
            { "effect",        "object /* effect */"         },
            { "talent",        "object /* talent */"         },
            { "action",        "System.Action /* action */"  },
            { "itemproperty",  "object /* itemproperty */"   },
            { "event",         "object /* event */"          },
            { "sqlquery",      "object /* sqlquery */"       },
            { "cassowary",     "object /* cassowary */"      },
        };

        // ── ENGINE CALL MAP  (NWScript function → C# call) ───────────────────
        /// <summary>
        /// Direct substitution rules. Each entry maps a raw NWScript call
        /// (lower-case, no parens) to its C# equivalent expression fragment.
        ///
        /// Calls not listed here are wrapped in NWScriptVM.CallEngine(…) stubs
        /// with a warning note so modders can fill them in manually.
        /// </summary>
        private static readonly Dictionary<string, string> CallMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Object / identity
            { "OBJECT_SELF",              "NWScriptVM.GetObjectSelf()"                           },
            { "OBJECT_INVALID",           "null"                                                  },
            { "GetIsObjectValid",         "NWScriptVM.GetIsObjectValid"                          },
            { "GetObjectByTag",           "NWScriptVM.GetObjectByTag"                            },
            { "GetFirstObjectInArea",     "NWScriptVM.GetFirstObjectInArea"                      },
            { "GetNextObjectInArea",      "NWScriptVM.GetNextObjectInArea"                       },

            // PC / party
            { "GetFirstPC",               "NWScriptVM.GetFirstPC()"                              },
            { "GetNextPC",                "NWScriptVM.GetNextPC()"                               },
            { "GetPartyMemberCount",      "NWScriptVM.GetPartyMemberCount()"                     },
            { "IsInParty",                "NWScriptVM.IsInParty"                                 },
            { "AddPartyMember",           "NWScriptVM.AddPartyMember"                            },
            { "RemovePartyMember",        "NWScriptVM.RemovePartyMember"                         },

            // Variables
            { "SetGlobalBoolean",         "NWScriptVM.SetGlobalBool"                             },
            { "GetGlobalBoolean",         "NWScriptVM.GetGlobalBool"                             },
            { "SetGlobalNumber",          "NWScriptVM.SetGlobalInt"                              },
            { "GetGlobalNumber",          "NWScriptVM.GetGlobalInt"                              },
            { "SetGlobalString",          "NWScriptVM.SetGlobalString"                           },
            { "GetGlobalString",          "NWScriptVM.GetGlobalString"                           },
            { "SetLocalBoolean",          "NWScriptVM.SetLocalBool"                              },
            { "GetLocalBoolean",          "NWScriptVM.GetLocalBool"                              },
            { "SetLocalNumber",           "NWScriptVM.SetLocalInt"                               },
            { "GetLocalNumber",           "NWScriptVM.GetLocalInt"                               },

            // Inventory
            { "GiveItemToObject",         "NWScriptVM.GiveItem"                                  },
            { "CreateItemOnObject",       "NWScriptVM.CreateItemOnObject"                        },
            { "DestroyObject",            "NWScriptVM.DestroyObject"                             },
            { "GetItemInSlot",            "NWScriptVM.GetItemInSlot"                             },
            { "GetFirstItemInInventory",  "NWScriptVM.GetFirstItemInInventory"                   },
            { "GetNextItemInInventory",   "NWScriptVM.GetNextItemInInventory"                    },

            // Dialogue / conversation
            { "StartConversation",        "NWScriptVM.StartConversation"                         },
            { "ActionStartConversation",  "NWScriptVM.StartConversation"                         },
            { "SetCustomToken",           "NWScriptVM.SetCustomToken"                            },
            { "GetCurrentDialog",         "NWScriptVM.GetCurrentDialog()"                        },

            // Journal
            { "AddJournalQuestEntry",     "NWScriptVM.AddJournalQuestEntry"                      },
            { "GetJournalQuestState",     "NWScriptVM.GetJournalQuestState"                      },

            // Stats / XP
            { "GiveXPToCreature",         "NWScriptVM.AwardXP"                                   },
            { "GetXP",                    "NWScriptVM.GetXP"                                     },
            { "SetXP",                    "NWScriptVM.SetXP"                                     },
            { "GetLevel",                 "NWScriptVM.GetLevel"                                   },
            { "GetCurrentHitPoints",      "NWScriptVM.GetCurrentHP"                              },
            { "GetMaxHitPoints",          "NWScriptVM.GetMaxHP"                                  },
            { "SetMaxHitPoints",          "NWScriptVM.SetMaxHP"                                  },
            { "ApplyEffectToObject",      "NWScriptVM.ApplyEffect"                               },

            // Area / location
            { "JumpToLocation",           "NWScriptVM.JumpToLocation"                            },
            { "GetLocation",              "NWScriptVM.GetLocation"                               },
            { "GetPosition",              "NWScriptVM.GetPosition"                               },
            { "GetArea",                  "NWScriptVM.GetArea"                                   },
            { "GetModule",                "NWScriptVM.GetModule()"                               },
            { "TriggerModule",            "NWScriptVM.TriggerModule"                             },

            // Door / placeable
            { "OpenDoor",                 "NWScriptVM.OpenDoor"                                  },
            { "CloseDoor",                "NWScriptVM.CloseDoor"                                 },
            { "SetLocked",                "NWScriptVM.SetLocked"                                 },
            { "GetIsOpen",                "NWScriptVM.GetIsOpen"                                 },

            // Sound
            { "PlaySound",                "NWScriptVM.PlaySound"                                 },
            { "PlayVoiceChat",            "NWScriptVM.PlayVoiceChat"                             },
            { "MusicBackgroundPlay",      "NWScriptVM.MusicBackgroundPlay"                       },
            { "MusicBackgroundStop",      "NWScriptVM.MusicBackgroundStop"                       },

            // Utils
            { "SendMessageToPC",          "NWScriptVM.SendMessageToPC"                           },
            { "FloatingTextStringOnCreature", "NWScriptVM.FloatingText"                          },
            { "DelayCommand",             "NWScriptVM.DelayCommand"                              },
            { "AssignCommand",            "NWScriptVM.AssignCommand"                             },
            { "ActionDoCommand",          "NWScriptVM.ActionDoCommand"                           },
            { "Random",                   "UnityEngine.Random.Range"                             },
            { "IntToFloat",               "(float)"                                              },
            { "FloatToInt",               "(int)"                                                },
            { "IntToString",              ".ToString()"                                          },
            { "StringToInt",              "int.Parse"                                            },
            { "GetStringLength",          ".Length"                                              },
            { "GetSubString",             "NWScriptVM.GetSubString"                             },
            { "PrintString",              "Debug.Log"                                            },
        };

        // ── STATE ─────────────────────────────────────────────────────────────
        public string LastError { get; private set; } = "";

        // ── MAIN ENTRY ────────────────────────────────────────────────────────

        /// <summary>
        /// Migrate a .nss file to C# and return the result.
        /// </summary>
        public MigrationResult MigrateFile(string nssPath, string outputFolder = "")
        {
            var result = new MigrationResult { SourcePath = nssPath };

            if (!File.Exists(nssPath))
            {
                result.Success = false;
                LastError = $"File not found: {nssPath}";
                return result;
            }

            try
            {
                string source = File.ReadAllText(nssPath, Encoding.UTF8);
                string className = SanitiseClassName(Path.GetFileNameWithoutExtension(nssPath));

                result.CSharpCode = ConvertNSSToCSharp(source, className, result.Notes, out int lines, out int unsupported);
                result.LinesConverted       = lines;
                result.UnsupportedFunctions = unsupported;

                if (!string.IsNullOrEmpty(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                    string outPath = Path.Combine(outputFolder, className + OutputExtension);
                    File.WriteAllText(outPath, result.CSharpCode, Encoding.UTF8);
                    result.OutputPath = outPath;
                    Debug.Log($"[NWScriptMigrationTool] Migrated '{nssPath}' → '{outPath}'");
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                LastError = ex.Message;
                Debug.LogError($"[NWScriptMigrationTool] {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Migrate an entire folder of .nss files.
        /// </summary>
        public List<MigrationResult> MigrateFolder(string nssFolder, string outputFolder = "")
        {
            var results = new List<MigrationResult>();

            if (!Directory.Exists(nssFolder))
            {
                LastError = $"Folder not found: {nssFolder}";
                return results;
            }

            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = Path.Combine(Application.persistentDataPath, DefaultFolder);

            foreach (var file in Directory.GetFiles(nssFolder, "*.nss", SearchOption.AllDirectories))
                results.Add(MigrateFile(file, outputFolder));

            int ok   = results.Count(r => r.Success);
            int warn = results.Sum(r => r.Notes.Count(n => n.Type == MigrationNote.NoteType.Warning));
            int manual = results.Sum(r => r.Notes.Count(n => n.Type == MigrationNote.NoteType.ManualRequired));
            Debug.Log($"[NWScriptMigrationTool] {ok}/{results.Count} files migrated | {warn} warnings | {manual} manual-action required.");
            return results;
        }

        // ── CORE CONVERTER ────────────────────────────────────────────────────

        private string ConvertNSSToCSharp(string source, string className,
                                          List<MigrationNote> notes,
                                          out int linesConverted, out int unsupportedCount)
        {
            var sb     = new StringBuilder();
            var lines  = source.Split('\n');
            linesConverted  = lines.Length;
            unsupportedCount = 0;

            // ── FILE HEADER ──────────────────────────────────────────────────
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"// AUTO-MIGRATED from {className}.nss by NWScriptMigrationTool");
            sb.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("// REVIEW ALL 'MANUAL:' comments before use in-game.");
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using KotORUnity.Scripting;");
            sb.AppendLine("using KotORUnity.Core;");
            sb.AppendLine("using KotORUnity.Inventory;");
            sb.AppendLine();
            sb.AppendLine($"namespace {Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"{Indent}/// <summary>Migrated from {className}.nss</summary>");
            sb.AppendLine($"{Indent}public static class {className}");
            sb.AppendLine($"{Indent}{{");

            // ── TRACK STATE ──────────────────────────────────────────────────
            bool inBlockComment = false;
#pragma warning disable 0219
            bool insideClass    = false;
            int  braceDepth     = 0;
#pragma warning restore 0219

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmed = rawLine.Trim();

                // Block comments
                if (inBlockComment)
                {
                    sb.AppendLine($"{Indent}{Indent}{ConvertComment(rawLine)}");
                    if (trimmed.Contains("*/")) inBlockComment = false;
                    continue;
                }
                if (trimmed.StartsWith("/*")) { inBlockComment = !trimmed.Contains("*/"); }

                // Skip blank lines
                if (string.IsNullOrWhiteSpace(trimmed)) { sb.AppendLine(); continue; }

                // #include handling
                if (trimmed.StartsWith("#include"))
                {
                    string incFile = trimmed.Replace("#include", "").Trim().Trim('"');
                    notes.Add(new MigrationNote
                    {
                        Type       = MigrationNote.NoteType.ManualRequired,
                        SourceLine = i + 1,
                        Message    = $"#include \"{incFile}\" — include files must be migrated separately.",
                        Suggestion = $"// using KotORUnity.ModScripts; // (migrated {incFile}.cs)"
                    });
                    sb.AppendLine($"{Indent}{Indent}// MANUAL: #include \"{incFile}\" — add using statement manually.");
                    unsupportedCount++;
                    continue;
                }

                // const declarations
                if (trimmed.StartsWith("const "))
                {
                    string converted = ConvertDeclaration(trimmed, isConst: true);
                    sb.AppendLine($"{Indent}{Indent}{converted}");
                    continue;
                }

                // Function-like lines — detect entry points
                string converted2 = ConvertLine(trimmed, i + 1, notes, ref unsupportedCount);
                sb.AppendLine($"{Indent}{Indent}{converted2}");
            }

            // Close class + namespace
            sb.AppendLine($"{Indent}}}");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── LINE CONVERTERS ───────────────────────────────────────────────────

        private string ConvertLine(string line, int lineNum, List<MigrationNote> notes, ref int unsupportedCount)
        {
            // Single-line comment
            if (line.StartsWith("//")) return ConvertComment(line);

            // Type declarations (variable or function signature)
            if (IsDeclaration(line))
                return ConvertDeclaration(line, isConst: false);

            // Entry points
            if (line.StartsWith("void main("))
            {
                notes.Add(new MigrationNote
                {
                    Type       = MigrationNote.NoteType.Info,
                    SourceLine = lineNum,
                    Message    = "void main() converted to RunScript entry point.",
                    Suggestion = "Register in NWScriptVM: NWScriptVM.RegisterScript(\"script_resref\", ClassName.RunScript);"
                });
                return "[ConsoleCommand(\"run_" + SanitiseId(line) + "\")]\npublic static void RunScript(string[] args)\n{";
            }
            if (line.StartsWith("int StartingConditional("))
            {
                notes.Add(new MigrationNote
                {
                    Type       = MigrationNote.NoteType.Info,
                    SourceLine = lineNum,
                    Message    = "StartingConditional() converted to RunCondition entry point."
                });
                return "public static bool RunCondition(string[] args)\n{";
            }

            // return TRUE / FALSE (NWScript bool values)
            line = Regex.Replace(line, @"\bTRUE\b",  "true",  RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\bFALSE\b", "false", RegexOptions.IgnoreCase);

            // Engine calls substitution
            line = SubstituteEngineCalls(line, lineNum, notes, ref unsupportedCount);

            // OBJECT_SELF constant
            line = line.Replace("OBJECT_SELF", "NWScriptVM.GetObjectSelf()");
            line = line.Replace("OBJECT_INVALID", "null");

            // Semicolons — NWScript uses same as C# so pass through
            return line;
        }

        private string SubstituteEngineCalls(string line, int lineNum,
                                              List<MigrationNote> notes, ref int unsupportedCount)
        {
            foreach (var kv in CallMap)
            {
                // Match whole-word function name
                string pattern = $@"\b{Regex.Escape(kv.Key)}\b";
                if (Regex.IsMatch(line, pattern))
                    line = Regex.Replace(line, pattern, kv.Value);
            }

            // Detect remaining unknown engine calls (CapitalCase identifier followed by '(')
            var unknown = Regex.Matches(line, @"\b([A-Z][a-zA-Z0-9]+)\s*\(");
            foreach (Match m in unknown)
            {
                string fn = m.Groups[1].Value;
                if (CallMap.ContainsKey(fn)) continue;
                if (fn is "NWScriptVM" or "Debug" or "Mathf" or "UnityEngine"
                       or "Vector3" or "GameObject") continue;

                notes.Add(new MigrationNote
                {
                    Type       = MigrationNote.NoteType.ManualRequired,
                    SourceLine = lineNum,
                    Message    = $"Unknown engine call: {fn}() — no direct C# equivalent found.",
                    Suggestion = $"NWScriptVM.CallEngine(\"{fn}\", /*args*/)  // MANUAL: implement"
                });
                line = line.Replace($"{fn}(", $"/* MANUAL:{fn} */ NWScriptVM.CallEngine(\"{fn}\",");
                unsupportedCount++;
            }

            return line;
        }

        private static bool IsDeclaration(string line)
        {
            // e.g. "int nCount = 0;" or "float fDist = 5.0f;" or "string sTag = \"\";"
            foreach (var type in TypeMap.Keys)
            {
                if (line.StartsWith(type + " ") || line.StartsWith(type + "\t"))
                    return true;
            }
            return false;
        }

        private string ConvertDeclaration(string line, bool isConst)
        {
            // Map NWScript type to C#
            foreach (var kv in TypeMap)
            {
                string pattern = $@"^{Regex.Escape(kv.Key)}\s+";
                if (Regex.IsMatch(line, pattern))
                {
                    string mapped = Regex.Replace(line, pattern, (isConst ? "const " : "") + kv.Value + " ");
                    // NWScript doesn't use 'f' suffix on floats; C# does
                    mapped = Regex.Replace(mapped, @"\b(\d+\.\d+)\b", "$1f");
                    return mapped;
                }
            }
            return line;
        }

        private static string ConvertComment(string line) =>
            line.TrimStart().StartsWith("//") ? line : "// " + line;

        // ── REPORT GENERATION ─────────────────────────────────────────────────

        /// <summary>
        /// Generate a human-readable migration report listing all notes.
        /// </summary>
        public static string GenerateReport(IEnumerable<MigrationResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("  NWScript → C# Migration Report");
            sb.AppendLine($"  Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");

            int totalFiles  = 0;
            int totalOk     = 0;
            int totalLines  = 0;
            int totalManual = 0;

            foreach (var result in results)
            {
                totalFiles++;
                if (result.Success) totalOk++;
                totalLines  += result.LinesConverted;
                totalManual += result.Notes.Count(n => n.Type == MigrationNote.NoteType.ManualRequired);

                sb.AppendLine();
                sb.AppendLine($"FILE: {result.SourcePath}");
                sb.AppendLine($"  Status: {(result.Success ? "OK" : "FAILED")}");
                sb.AppendLine($"  Lines: {result.LinesConverted}  |  " +
                              $"Unsupported calls: {result.UnsupportedFunctions}");

                if (result.Notes.Count > 0)
                {
                    sb.AppendLine("  Notes:");
                    foreach (var note in result.Notes)
                        sb.AppendLine($"    {note}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine($"SUMMARY: {totalOk}/{totalFiles} files migrated successfully.");
            sb.AppendLine($"  Total lines processed: {totalLines}");
            sb.AppendLine($"  Manual-action items:   {totalManual}");
            sb.AppendLine();
            sb.AppendLine("NEXT STEPS:");
            sb.AppendLine("  1. Add 'using KotORUnity.ModScripts;' to any files that include your scripts.");
            sb.AppendLine("  2. Search for '// MANUAL:' comments and implement those calls.");
            sb.AppendLine("  3. Register scripts: NWScriptVM.RegisterScript(\"resref\", ClassName.RunScript);");
            sb.AppendLine("     Place registration calls in a ModBootstrapper.Initialize() method.");
            sb.AppendLine("  4. For objects — replace 'GameObject' stubs with actual scene lookups.");
            sb.AppendLine("  5. Review effect/talent/itemproperty stubs and implement via the");
            sb.AppendLine("     ForcePowers, InventorySystem, and CombatResolver APIs.");

            return sb.ToString();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static string SanitiseClassName(string name) =>
            string.Concat(name.Select((c, i) =>
                char.IsLetterOrDigit(c) ? (i == 0 && char.IsDigit(c) ? '_' : c) : '_'));

        private static string SanitiseId(string line) =>
            Regex.Match(line, @"\w+").Value.ToLowerInvariant();

        // ── EDITOR GUI STUB ───────────────────────────────────────────────────
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            UnityEditor.EditorGUILayout.LabelField(
                $"Files loaded: {_pendingFiles.Count}  Converted: {_convertedResults.Count}",
                UnityEditor.EditorStyles.helpBox);
            foreach (var r in _convertedResults)
            {
                bool hasErr  = r.UnsupportedFunctions > 0;
                bool hasWarn = r.Notes.Exists(n => n.Type == MigrationNote.NoteType.Warning);
                var  msgType = hasErr  ? UnityEditor.MessageType.Error :
                               hasWarn ? UnityEditor.MessageType.Warning :
                               UnityEditor.MessageType.Info;
                string name = System.IO.Path.GetFileNameWithoutExtension(r.SourcePath ?? "");
                UnityEditor.EditorGUILayout.HelpBox(
                    $"{name}: {r.LinesConverted} lines  |  {r.UnsupportedFunctions} unsupported  |  {r.Notes.Count} notes",
                    msgType);
            }
#endif
        }

        // ── EDITOR WINDOW API ──────────────────────────────────────────────────
        private readonly List<string> _pendingFiles = new List<string>();
        private readonly List<MigrationResult> _convertedResults = new List<MigrationResult>();

        public void LoadFile(string path)
        {
            if (!System.IO.File.Exists(path)) return;
            if (!_pendingFiles.Contains(path)) _pendingFiles.Add(path);
        }

        public void LoadFolder(string folder)
        {
            if (!System.IO.Directory.Exists(folder)) return;
            foreach (var f in System.IO.Directory.GetFiles(folder, "*.nss", System.IO.SearchOption.AllDirectories))
                LoadFile(f);
        }

        public void ConvertAll()
        {
            _convertedResults.Clear();
            string tempOut = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "MigrationOutput");
            System.IO.Directory.CreateDirectory(tempOut);
            foreach (var f in _pendingFiles)
            {
                var results = MigrateFolder(System.IO.Path.GetDirectoryName(f), tempOut);
                _convertedResults.AddRange(results);
            }
        }

        public void ExportAll(string outputFolder)
        {
            System.IO.Directory.CreateDirectory(outputFolder);
            string tempOut = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath, "MigrationOutput");
            if (System.IO.Directory.Exists(tempOut))
            {
                foreach (var f in System.IO.Directory.GetFiles(tempOut, "*.cs"))
                {
                    string dest = System.IO.Path.Combine(outputFolder, System.IO.Path.GetFileName(f));
                    System.IO.File.Copy(f, dest, true);
                }
            }
            UnityEngine.Debug.Log($"[NWScriptMigration] Exported to {outputFolder}");
        }
    }
}
