// ============================================================================
//  KotOR-Unity  —  Editor Window wrappers for all 11 Mod Tools
//
//  Access via:  Unity menu  →  Window  →  KotOR Mod Tools  →  <tool name>
//
//  Each window is a thin EditorWindow shell that delegates to the runtime
//  service classes in Assets/Scripts/ModTools/.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using KotORUnity.ModTools;
using KotORUnity.Bootstrap;

namespace KotORUnity.Editor
{
    // ── 1. ASSET BROWSER ─────────────────────────────────────────────────────
    public class AssetBrowserWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Asset Browser", priority = 100)]
        public static void Open() => GetWindow<AssetBrowserWindow>("KotOR Asset Browser");

        private AssetBrowser _browser;
        private string _searchFilter = "";
        private Vector2 _scroll;

        private void OnEnable()
        {
            _browser = new AssetBrowser();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("KotOR Asset Browser", EditorStyles.boldLabel);

            if (!SceneBootstrapper.IsReady)
            {
                EditorGUILayout.HelpBox(
                    "SceneBootstrapper not ready.\n" +
                    "Enter Play Mode with a valid KotOR install path set.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                _browser.BuildIndex();
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            // Search() signature: (string query, ushort typeFilter = 0, int maxResults = 200)
            var results = _browser.Search(_searchFilter, 0, 200);
            foreach (var entry in results)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.DisplayName, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(entry.TypeLabel,   GUILayout.Width(50));
                EditorGUILayout.LabelField($"{entry.Size / 1024}KB", GUILayout.Width(60));
                if (GUILayout.Button("Extract", GUILayout.Width(60)))
                {
                    string outPath = EditorUtility.SaveFilePanel(
                        "Extract Asset", Application.dataPath,
                        entry.DisplayName, entry.TypeLabel.ToLower());
                    if (!string.IsNullOrEmpty(outPath))
                    {
                        var bytes = SceneBootstrapper.Resources?.GetResource(
                            entry.ResRef,
                            (KotORUnity.KotOR.FileReaders.ResourceType)entry.ResType);
                        if (bytes != null)
                            System.IO.File.WriteAllBytes(outPath, bytes);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 2. 2DA EDITOR ────────────────────────────────────────────────────────
    public class TwoDAEditorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/2DA Editor", priority = 101)]
        public static void Open() => GetWindow<TwoDAEditorWindow>("2DA Editor");

        private TwoDAEditor _tool;
        private string _tableName = "spells";
        private Vector2 _scroll;

        private void OnEnable() => _tool = new TwoDAEditor();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("2DA Table Editor", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _tableName = EditorGUILayout.TextField("Table Name", _tableName);
            if (GUILayout.Button("Load", GUILayout.Width(60)) && SceneBootstrapper.IsReady)
            {
                var repo = Object.FindObjectOfType<Data.GameDataRepository>();
                var table = repo?.Get(_tableName);
                if (table != null)
                    _tool.LoadFromTableExt(table, _tableName);
                else
                    Debug.LogWarning($"[2DA Editor] Table '{_tableName}' not found.");
            }
            EditorGUILayout.EndHorizontal();

            if (_tool.CurrentTable == null)
            {
                EditorGUILayout.HelpBox("Load a table to begin editing. (Enter Play Mode first)", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            // TwoDAEditorTool exposes its own IMGUI rendering
            EditorGUILayout.LabelField($"Table: {_tool.CurrentTableName}  |  Rows: {_tool.CurrentTable.RowCount}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 3. DIALOGUE EDITOR ───────────────────────────────────────────────────
    public class DialogueEditorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Dialogue Editor", priority = 102)]
        public static void Open() => GetWindow<DialogueEditorWindow>("Dialogue Editor");

        private DialogueEditorTool _tool;
        private string _dlgResRef = "";
        private Vector2 _scroll;

        private void OnEnable() => _tool = new DialogueEditorTool();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogue Editor", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _dlgResRef = EditorGUILayout.TextField("DLG ResRef", _dlgResRef);
            if (GUILayout.Button("Load", GUILayout.Width(60)) && SceneBootstrapper.IsReady)
            {
                var bytes = SceneBootstrapper.Resources?.GetResource(
                    _dlgResRef, KotORUnity.KotOR.FileReaders.ResourceType.DLG);
                if (bytes != null)
                    _tool.LoadFromBytes(bytes, _dlgResRef);
                else
                    Debug.LogWarning($"[Dialogue Editor] DLG not found: {_dlgResRef}");
            }
            if (GUILayout.Button("New",  GUILayout.Width(50)))  _tool.NewDlg();
            if (GUILayout.Button("Export", GUILayout.Width(60)))
            {
                string path = EditorUtility.SaveFilePanel("Export DLG", "", _dlgResRef, "json");
                if (!string.IsNullOrEmpty(path)) _tool.ExportJson(path);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 4. MODULE / AREA EDITOR ───────────────────────────────────────────────
    public class ModuleAreaEditorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Module Area Editor", priority = 103)]
        public static void Open() => GetWindow<ModuleAreaEditorWindow>("Module Area Editor");

        private ModuleAreaEditor _tool;
        private string _moduleName = "my_module_01";
        private Vector2 _scroll;

        private void OnEnable() => _tool = new ModuleAreaEditor();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Module / Area Editor", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _moduleName = EditorGUILayout.TextField("Module Name", _moduleName);
            if (GUILayout.Button("Load", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("Open Module JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path)) _tool.Load(path);
            }
            if (GUILayout.Button("New", GUILayout.Width(50)))
                _tool.NewModule(_moduleName);
            if (GUILayout.Button("Save", GUILayout.Width(50)))
            {
                string path = EditorUtility.SaveFolderPanel("Save Module To", Application.dataPath, _moduleName);
                if (!string.IsNullOrEmpty(path)) _tool.Save(path);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Validate"))
            {
                var errors = _tool.Validate();
                foreach (var e in errors) Debug.LogWarning($"[Area Editor] {e}");
                if (errors.Count == 0) Debug.Log("[Area Editor] Validation passed.");
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 5. ITEM CREATOR ───────────────────────────────────────────────────────
    public class ItemCreatorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Item Creator", priority = 104)]
        public static void Open() => GetWindow<ItemCreatorWindow>("Item Creator");

        private ItemCreatorTool _tool;
        private Vector2 _scroll;

        private void OnEnable() => _tool = new ItemCreatorTool();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Item Creator (UTI)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Item"))  _tool.NewItem();
            if (GUILayout.Button("Load UTI"))
            {
                string path = EditorUtility.OpenFilePanel("Load UTI JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path)) _tool.Load(path);
            }
            if (GUILayout.Button("Save"))
            {
                string folder = EditorUtility.SaveFolderPanel("Save UTI To", Application.dataPath, "");
                if (!string.IsNullOrEmpty(folder)) _tool.Save(folder);
            }
            if (GUILayout.Button("Validate"))
            {
                var errors = _tool.Validate();
                foreach (var e in errors) Debug.LogWarning($"[Item Creator] {e}");
                if (errors.Count == 0) Debug.Log("[Item Creator] Validation passed.");
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 6. CREATURE CREATOR ──────────────────────────────────────────────────
    public class CreatureCreatorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Creature Creator", priority = 105)]
        public static void Open() => GetWindow<CreatureCreatorWindow>("Creature Creator");

        private CreatureCreatorTool _tool;
        private Vector2 _scroll;

        private void OnEnable() => _tool = new CreatureCreatorTool();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Creature Creator (UTC)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Creature")) _tool.NewCreature();
            if (GUILayout.Button("Load UTC"))
            {
                string path = EditorUtility.OpenFilePanel("Load UTC JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path)) _tool.Load(path);
            }
            if (GUILayout.Button("Save"))
            {
                string folder = EditorUtility.SaveFolderPanel("Save UTC To", Application.dataPath, "");
                if (!string.IsNullOrEmpty(folder)) _tool.Save(folder);
            }
            if (GUILayout.Button("Validate"))
            {
                var errors = _tool.Validate();
                foreach (var e in errors) Debug.LogWarning($"[Creature Creator] {e}");
                if (errors.Count == 0) Debug.Log("[Creature Creator] Validation passed.");
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 7. FORCE POWER EDITOR ────────────────────────────────────────────────
    public class ForcePowerEditorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Force Power Editor", priority = 106)]
        public static void Open() => GetWindow<ForcePowerEditorWindow>("Force Power Editor");

        private ForcePowerEditorTool _tool;
        private Vector2 _scroll;

        private void OnEnable() => _tool = new ForcePowerEditorTool();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Force Power Editor", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Power"))  _tool.NewPower();
            if (GUILayout.Button("Load"))
            {
                string path = EditorUtility.OpenFilePanel("Load Force Power JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path)) _tool.Load(path);
            }
            if (GUILayout.Button("Save"))
            {
                string folder = EditorUtility.SaveFolderPanel("Save Force Power To", Application.dataPath, "");
                if (!string.IsNullOrEmpty(folder)) _tool.Save(folder);
            }
            if (GUILayout.Button("Validate"))
            {
                var errors = _tool.Validate();
                foreach (var e in errors) Debug.LogWarning($"[Force Power Editor] {e}");
                if (errors.Count == 0) Debug.Log("[Force Power Editor] Validation passed.");
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 8. CAMPAIGN PACKAGER ─────────────────────────────────────────────────
    public class CampaignPackagerWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Campaign Packager", priority = 107)]
        public static void Open() => GetWindow<CampaignPackagerWindow>("Campaign Packager");

        private CampaignPackager _tool;
        private Vector2 _scroll;

        private void OnEnable() => _tool = new CampaignPackager();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Campaign Packager (.kotormod)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Source Folder"))
            {
                string path = EditorUtility.OpenFolderPanel("Mod Source Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path)) _tool.SetSourceFolder(path);
            }
            if (GUILayout.Button("Validate")) _tool.Validate();
            if (GUILayout.Button("Package"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save .kotormod", Application.dataPath,
                    _tool.ModName ?? "mymod", "kotormod");
                if (!string.IsNullOrEmpty(path)) _tool.Package(path);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 9. MOD LOADER (runtime management panel) ─────────────────────────────
    public class ModLoaderWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Mod Loader", priority = 108)]
        public static void Open() => GetWindow<ModLoaderWindow>("Mod Loader");

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mod Loader — Active Mods", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to manage loaded mods.", MessageType.Info);
                return;
            }

            var loader = Object.FindObjectOfType<ModLoader>();
            if (loader == null)
            {
                EditorGUILayout.HelpBox("ModLoader not found in scene.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var mod in loader.LoadedMods)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{mod.DisplayName} v{mod.Version}", GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"Priority: {mod.Manifest?.LoadOrder ?? 100}", GUILayout.Width(80));
                if (GUILayout.Button("Reload", GUILayout.Width(60)))
                    loader.HotReload(mod.DisplayName);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 10. NWSCRIPT MIGRATION TOOL ──────────────────────────────────────────
    public class NWScriptMigrationWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/NWScript Migration Tool", priority = 109)]
        public static void Open() => GetWindow<NWScriptMigrationWindow>("NWScript Migration");

        private NWScriptMigrationTool _tool;
        private Vector2 _scroll;

        private void OnEnable() => _tool = new NWScriptMigrationTool();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("NWScript → C# Migration Tool", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load .nss File"))
            {
                string path = EditorUtility.OpenFilePanel("Open NWScript", Application.dataPath, "nss");
                if (!string.IsNullOrEmpty(path)) _tool.LoadFile(path);
            }
            if (GUILayout.Button("Load Folder"))
            {
                string path = EditorUtility.OpenFolderPanel("Open Script Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path)) _tool.LoadFolder(path);
            }
            if (GUILayout.Button("Convert All")) _tool.ConvertAll();
            if (GUILayout.Button("Export"))
            {
                string path = EditorUtility.SaveFolderPanel("Export C# Scripts", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path)) _tool.ExportAll(path);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tool.DrawEditorGUI();
            EditorGUILayout.EndScrollView();
        }
    }

    // ── 11. IN-GAME DEV CONSOLE (Editor panel) ───────────────────────────────
    public class DevConsoleEditorWindow : EditorWindow
    {
        [MenuItem("Window/KotOR Mod Tools/Dev Console", priority = 110)]
        public static void Open() => GetWindow<DevConsoleEditorWindow>("Dev Console");

        private string _commandInput = "";
        private string _output = "";
        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dev Console (Editor Panel)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the dev console.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("ConsoleInput");
            _commandInput = EditorGUILayout.TextField(_commandInput);
            bool enter = Event.current.type == EventType.KeyDown
                         && Event.current.keyCode == KeyCode.Return
                         && GUI.GetNameOfFocusedControl() == "ConsoleInput";

            if ((GUILayout.Button("Run", GUILayout.Width(50)) || enter)
                && !string.IsNullOrEmpty(_commandInput))
            {
                var console = Object.FindObjectOfType<InGameDevConsole>();
                if (console != null)
                {
                    string result = console.ExecuteCommand(_commandInput);
                    _output += $"\n> {_commandInput}\n{result}";
                    _commandInput = "";
                    Repaint();
                }
                else
                {
                    _output += "\n[Editor] InGameDevConsole MonoBehaviour not found in scene.\n" +
                               "Add it to a GameObject in the Boot scene.";
                }
                if (enter) Event.current.Use();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
