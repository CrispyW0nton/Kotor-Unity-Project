using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Scripting;
#pragma warning disable 0414, 0219

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  IN-GAME DEV CONSOLE  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Single line of output in the console history.
    /// </summary>
    public class ConsoleEntry
    {
        public enum EntryType { Input, Output, Error, Warning, Info, Separator }

        public EntryType Type;
        public string    Text;
        public float     Timestamp;  // Time.unscaledTime

        public ConsoleEntry(EntryType type, string text)
        {
            Type      = type;
            Text      = text;
            Timestamp = Time.unscaledTime;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CONSOLE COMMAND REGISTRY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attribute that marks a static method as a console command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        public string Name    { get; }
        public string Usage   { get; }
        public string Summary { get; }

        public ConsoleCommandAttribute(string name, string usage = "", string summary = "")
        {
            Name    = name.ToLowerInvariant();
            Usage   = usage;
            Summary = summary;
        }
    }

    /// <summary>
    /// Registered command entry used by the runtime registry.
    /// </summary>
    public class RegisteredCommand
    {
        public string Name;
        public string Usage;
        public string Summary;
        public Func<string[], string> Handler; // args → output string (or exception)
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  IN-GAME DEV CONSOLE  —  MonoBehaviour
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drop-in dev console overlay for the KotOR Unity port.
    ///
    /// USAGE:
    ///   • Press ` (back-tick) to toggle the console panel.
    ///   • Type a command and press Enter.
    ///   • Use UP/DOWN arrows to browse history.
    ///   • Tab-complete command names.
    ///
    /// BUILT-IN COMMANDS (all lower-case):
    ///   help [cmd]          — list all commands, or details for one
    ///   clear               — clear the output buffer
    ///   echo <text>         — print text to console
    ///   setvar <k> <v>      — set a GlobalVar string
    ///   getvar <k>          — get a GlobalVar string
    ///   setvari <k> <v>     — set a GlobalVar int
    ///   getvari <k>         — get a GlobalVar int
    ///   setvarb <k> <v>     — set a GlobalVar bool
    ///   getvarb <k>         — get a GlobalVar bool
    ///   give <resref>       — add item to player inventory
    ///   tp <x> <y> <z>      — teleport player to world coords
    ///   godmode [0|1]       — toggle/set invincibility
    ///   noclip [0|1]        — toggle/set collision
    ///   heal [amount]       — heal player (default full)
    ///   xp <amount>         — award XP to player
    ///   level [n]           — set player level
    ///   fps                 — print current frame rate
    ///   loadmodule <name>   — load a module by name
    ///   reload              — reload all mounted archives
    ///   listmods            — list loaded mods
    ///   runscript <resref>  — run a registered NWScript by resref
    ///   listscripts         — list all registered scripts
    ///   scene <name>        — load a Unity scene by name
    ///   time <scale>        — set Time.timeScale
    ///   spawn <utc>         — spawn creature at player position
    ///   kill <tag>          — destroy GameObject with tag
    ///   listentities        — list all GameObjects in the scene
    ///   showhud [0|1]       — show / hide HUD
    ///
    /// MODDERS can register custom commands at runtime:
    ///   InGameDevConsole.Register("myCmd", args => "hello " + args[0], "usage", "summary");
    /// </summary>
    public class InGameDevConsole : MonoBehaviour
    {
        // ── CONFIGURATION ─────────────────────────────────────────────────────
        [Header("Console UI")]
        [SerializeField] private KeyCode toggleKey    = KeyCode.BackQuote;
        [SerializeField] private int     maxHistory   = 200;
        [SerializeField] private int     maxCmdHistory = 50;
        [SerializeField] private int     fontSize     = 13;
        [SerializeField] private float   consoleHeight = 0.4f;  // fraction of screen

        [Header("Auth")]
        [Tooltip("If true, require the player to type the unlock passphrase before using the console.")]
        [SerializeField] private bool requirePassphrase = false;
        [SerializeField] private string passphrase = "kotor_dev";

        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static InGameDevConsole Instance { get; private set; }

        // ── STATE ─────────────────────────────────────────────────────────────
        private bool _open        = false;
        private bool _unlocked    = false;

        private readonly List<ConsoleEntry>  _history    = new List<ConsoleEntry>();
        private readonly List<string>        _cmdHistory = new List<string>();
        private int                          _cmdHistoryPos = -1;

        private string _inputBuffer = "";
        private string _autocomplete = "";
        private Vector2 _scroll = Vector2.zero;
        private bool    _scrollToBottom = false;

        // ── COMMAND REGISTRY ──────────────────────────────────────────────────
        private static readonly Dictionary<string, RegisteredCommand> _commands =
            new Dictionary<string, RegisteredCommand>();

        // ── GUI STYLES (lazy) ─────────────────────────────────────────────────
        private GUIStyle _boxStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _outputStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _infoStyle;
        private bool     _stylesBuilt = false;

        // ── UNITY ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            _unlocked = !requirePassphrase;

            RegisterBuiltins();
            RegisterAttributeCommands();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _open = !_open;
        }

        private void OnGUI()
        {
            if (!_open) return;
            BuildStyles();

            float w = Screen.width;
            float h = Screen.height * consoleHeight;
            float inputH = 24f;

            // Background panel
            GUI.Box(new Rect(0, 0, w, h), GUIContent.none, _boxStyle);

            // Output area
            float outputH = h - inputH - 6f;
            _scroll = GUI.BeginScrollView(new Rect(2, 2, w - 4, outputH),
                                          _scroll, new Rect(0, 0, w - 20, EntryTotalHeight()));
            float y = 0;
            foreach (var entry in _history)
            {
                GUIStyle style = StyleForEntry(entry.Type);
                string prefix = entry.Type == ConsoleEntry.EntryType.Input ? "> " : "  ";
                float lh = style.CalcHeight(new GUIContent(prefix + entry.Text), w - 24);
                GUI.Label(new Rect(4, y, w - 24, lh), prefix + entry.Text, style);
                y += lh + 1f;
            }
            GUI.EndScrollView();

            if (_scrollToBottom)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
            }

            // Input field
            GUI.SetNextControlName("ConsoleInput");
            string newInput = GUI.TextField(new Rect(2, h - inputH - 2f, w - 4, inputH),
                                            _inputBuffer, _inputStyle);

            // Handle Tab autocomplete
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
            {
                TryAutocomplete();
                e.Use();
            }
            // Handle Enter
            else if (e.type == EventType.KeyDown &&
                     (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrWhiteSpace(_inputBuffer))
                    Submit(_inputBuffer.Trim());
                _inputBuffer = "";
                _cmdHistoryPos = -1;
                e.Use();
            }
            // History navigation
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow)
            {
                NavigateHistory(1);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow)
            {
                NavigateHistory(-1);
                e.Use();
            }
            else
            {
                _inputBuffer = newInput;
            }

            GUI.FocusControl("ConsoleInput");
        }

        // ── INPUT ─────────────────────────────────────────────────────────────

        private void Submit(string line)
        {
            AddEntry(ConsoleEntry.EntryType.Input, line);
            PushCmdHistory(line);

            if (!_unlocked)
            {
                if (line == passphrase)
                {
                    _unlocked = true;
                    AddEntry(ConsoleEntry.EntryType.Info, "Console unlocked.");
                }
                else
                {
                    AddEntry(ConsoleEntry.EntryType.Error, "Enter passphrase to unlock.");
                }
                return;
            }

            var parts = SplitArgs(line);
            if (parts.Length == 0) return;

            string cmdName = parts[0].ToLowerInvariant();
            if (_commands.TryGetValue(cmdName, out var cmd))
            {
                try
                {
                    string[] args = new string[parts.Length - 1];
                    Array.Copy(parts, 1, args, 0, args.Length);
                    string result = cmd.Handler(args);
                    if (!string.IsNullOrEmpty(result))
                        AddEntry(ConsoleEntry.EntryType.Output, result);
                }
                catch (Exception ex)
                {
                    AddEntry(ConsoleEntry.EntryType.Error, $"Error in '{cmdName}': {ex.Message}");
                }
            }
            else
            {
                AddEntry(ConsoleEntry.EntryType.Error, $"Unknown command '{cmdName}'. Type 'help'.");
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Write a line to the console output (for mod scripts).</summary>
        public static void Print(string text) =>
            Instance?.AddEntry(ConsoleEntry.EntryType.Info, text);

        /// <summary>Write a warning to the console output.</summary>
        public static void Warn(string text) =>
            Instance?.AddEntry(ConsoleEntry.EntryType.Warning, text);

        /// <summary>Write an error to the console output.</summary>
        public static void Error(string text) =>
            Instance?.AddEntry(ConsoleEntry.EntryType.Error, text);

        /// <summary>
        /// Execute a command string programmatically (e.g., from the Editor Window).
        /// Returns the output string produced by the command handler.
        /// </summary>
        public string ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return "";
            // Capture output by briefly hooking into the entry list
            int beforeCount = _history.Count;
            Submit(commandLine);
            var sb = new System.Text.StringBuilder();
            for (int i = beforeCount; i < _history.Count; i++)
                sb.AppendLine(_history[i].Text);
            return sb.ToString().TrimEnd();
        }
        /// Register a custom command at runtime.
        /// Handler receives args (trimmed, split by whitespace) and returns output string.
        /// </summary>
        public static void Register(string name, Func<string[], string> handler,
                                    string usage = "", string summary = "")
        {
            string key = name.ToLowerInvariant();
            _commands[key] = new RegisteredCommand
            {
                Name    = key,
                Usage   = usage,
                Summary = summary,
                Handler = handler
            };
        }

        /// <summary>Unregister a command by name.</summary>
        public static bool Unregister(string name) =>
            _commands.Remove(name.ToLowerInvariant());

        // ── BUILT-IN COMMANDS ─────────────────────────────────────────────────

        private void RegisterBuiltins()
        {
            // help
            Register("help", args =>
            {
                if (args.Length > 0 && _commands.TryGetValue(args[0].ToLower(), out var c))
                    return $"{c.Name} {c.Usage}\n  {c.Summary}";
                var sb = new StringBuilder();
                sb.AppendLine("Available commands:");
                foreach (var kv in _commands)
                    sb.AppendLine($"  {kv.Key,-20} {kv.Value.Summary}");
                return sb.ToString().TrimEnd();
            }, "[command]", "List commands or show help for a specific command");

            // clear
            Register("clear", _ => { _history.Clear(); return null; },
                     "", "Clear console output");

            // echo
            Register("echo", args => string.Join(" ", args),
                     "<text>", "Print text to the console");

            // fps
            Register("fps", _ => $"FPS: {(1f / Time.unscaledDeltaTime):F1}",
                     "", "Show current frame rate");

            // time scale
            Register("time", args =>
            {
                if (args.Length == 0) return $"timeScale = {Time.timeScale:F2}";
                if (float.TryParse(args[0], out float ts))
                {
                    Time.timeScale = Mathf.Clamp(ts, 0f, 10f);
                    return $"timeScale set to {Time.timeScale:F2}";
                }
                return "Usage: time <scale>   (e.g. 'time 0.5')";
            }, "<scale>", "Get or set Time.timeScale");

            // setvar / getvar
            Register("setvar", args =>
            {
                if (args.Length < 2) return "Usage: setvar <key> <value>";
                NWScriptVM.SetGlobalString(args[0], string.Join(" ", args, 1, args.Length - 1));
                return $"String '{args[0]}' set.";
            }, "<key> <value>", "Set a global string variable");

            Register("getvar", args =>
            {
                if (args.Length < 1) return "Usage: getvar <key>";
                return $"'{args[0]}' = \"{NWScriptVM.GetGlobalString(args[0])}\"";
            }, "<key>", "Get a global string variable");

            Register("setvari", args =>
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int v))
                    return "Usage: setvari <key> <int>";
                NWScriptVM.SetGlobalInt(args[0], v);
                return $"Int '{args[0]}' set to {v}.";
            }, "<key> <int>", "Set a global int variable");

            Register("getvari", args =>
            {
                if (args.Length < 1) return "Usage: getvari <key>";
                return $"'{args[0]}' = {NWScriptVM.GetGlobalInt(args[0])}";
            }, "<key>", "Get a global int variable");

            Register("setvarb", args =>
            {
                if (args.Length < 2) return "Usage: setvarb <key> <0|1>";
                bool v = args[1] != "0" && args[1].ToLower() != "false";
                NWScriptVM.SetGlobalBool(args[0], v);
                return $"Bool '{args[0]}' set to {v}.";
            }, "<key> <0|1>", "Set a global bool variable");

            Register("getvarb", args =>
            {
                if (args.Length < 1) return "Usage: getvarb <key>";
                return $"'{args[0]}' = {NWScriptVM.GetGlobalBool(args[0])}";
            }, "<key>", "Get a global bool variable");

            // xp
            Register("xp", args =>
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int amount))
                    return "Usage: xp <amount>";
                EventBus.Publish(EventBus.EventType.XPAwarded,
                    new EventBus.GameEventArgs { IntValue = amount });
                return $"Awarded {amount} XP.";
            }, "<amount>", "Award XP to the player");

            // heal
            Register("heal", args =>
            {
                int amount = 9999;
                if (args.Length > 0) int.TryParse(args[0], out amount);
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return "No Player found.";
                var stats = player.GetComponent<Player.PlayerStatsBehaviour>();
                if (stats == null) return "Player has no PlayerStatsBehaviour.";
                stats.Stats.Heal(amount);
                return $"Healed player for {amount} HP.";
            }, "[amount]", "Heal the player (default: full heal)");

            // godmode
            Register("godmode", args =>
            {
                bool on = args.Length == 0 || args[0] != "0";
                EventBus.Publish(EventBus.EventType.GodModeChanged,
                    new EventBus.GameEventArgs { BoolValue = on });
                return $"God mode: {(on ? "ON" : "OFF")}";
            }, "[0|1]", "Toggle god mode (player invincible)");

            // tp (teleport)
            Register("tp", args =>
            {
                if (args.Length < 3) return "Usage: tp <x> <y> <z>";
                if (!float.TryParse(args[0], out float x) ||
                    !float.TryParse(args[1], out float y) ||
                    !float.TryParse(args[2], out float z))
                    return "Error: x/y/z must be numbers.";
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return "No Player found.";
                player.transform.position = new Vector3(x, y, z);
                return $"Teleported to ({x:F2}, {y:F2}, {z:F2})";
            }, "<x> <y> <z>", "Teleport player to world coordinates");

            // give (item)
            Register("give", args =>
            {
                if (args.Length < 1) return "Usage: give <resref>";
                NWScriptVM.GiveItemToPlayer(args[0]);
                return $"Gave item '{args[0]}' to player.";
            }, "<resref>", "Add an item to player inventory");

            // spawn
            Register("spawn", args =>
            {
                if (args.Length < 1) return "Usage: spawn <utc_resref>";
                var player = GameObject.FindGameObjectWithTag("Player");
                Vector3 pos = player ? player.transform.position + player.transform.forward * 2f : Vector3.zero;
                NWScriptVM.SpawnCreatureAtLocation(args[0], pos);
                return $"Spawned '{args[0]}' at {pos:F1}.";
            }, "<utc_resref>", "Spawn a creature near the player");

            // kill
            Register("kill", args =>
            {
                if (args.Length < 1) return "Usage: kill <tag>";
                var go = GameObject.FindGameObjectWithTag(args[0]);
                if (go == null) return $"No GameObject with tag '{args[0]}' found.";
                Destroy(go);
                return $"Destroyed '{go.name}'.";
            }, "<tag>", "Destroy a GameObject by Unity tag");

            // loadmodule
            Register("loadmodule", args =>
            {
                if (args.Length < 1) return "Usage: loadmodule <modulename>";
                EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                    new World.AreaTransitionEventArgs(args[0], ""));
                return $"Requested load of module '{args[0]}'.";
            }, "<modulename>", "Load a KotOR module by name");

            // runscript
            Register("runscript", args =>
            {
                if (args.Length < 1) return "Usage: runscript <resref>";
                NWScriptVM.Run(args[0], null);
                return $"Ran script '{args[0]}'.";
            }, "<resref>", "Execute a registered NWScript by resref");

            // listscripts
            Register("listscripts", _ =>
            {
                var sb = new StringBuilder("Registered scripts:\n");
                foreach (var k in NWScriptVM.ListScripts())
                    sb.AppendLine($"  {k}");
                return sb.ToString().TrimEnd();
            }, "", "List all registered NWScript handlers");

            // listmods
            Register("listmods", _ =>
            {
                var loaded = ModLoader.Instance?.GetLoadedMods();
                if (loaded == null || loaded.Count == 0) return "No mods loaded.";
                var sb = new StringBuilder("Loaded mods:\n");
                foreach (var m in loaded)
                    sb.AppendLine($"  [{(m.IsEnabled ? "ON" : "--")}] {m.ModId} v{m.Version}  —  {m.DisplayName}");
                return sb.ToString().TrimEnd();
            }, "", "List all loaded mods");

            // scene
            Register("scene", args =>
            {
                if (args.Length < 1) return "Usage: scene <sceneName>";
                UnityEngine.SceneManagement.SceneManager.LoadScene(args[0]);
                return $"Loading scene '{args[0]}'…";
            }, "<sceneName>", "Load a Unity scene by name");

            // listentities
            Register("listentities", _ =>
            {
                var all = FindObjectsOfType<GameObject>();
                var sb = new StringBuilder($"Scene objects ({all.Length}):\n");
                foreach (var go in all)
                    sb.AppendLine($"  {go.name} (tag={go.tag})");
                return sb.ToString().TrimEnd();
            }, "", "List all GameObjects in the active scene");

            // showhud
            Register("showhud", args =>
            {
                bool show = args.Length == 0 || args[0] != "0";
                EventBus.Publish(EventBus.EventType.HUDVisibilityChanged,
                    new EventBus.GameEventArgs { BoolValue = show });
                return $"HUD: {(show ? "visible" : "hidden")}";
            }, "[0|1]", "Show or hide the HUD");

            // version
            Register("version", _ => "KotOR-Unity Port  |  Build 0.9.0-alpha  |  ModTools v1.0",
                     "", "Display engine build info");
        }

        // ── ATTRIBUTE-BASED COMMAND DISCOVERY ─────────────────────────────────

        private void RegisterAttributeCommands()
        {
            // Scan all loaded assemblies for [ConsoleCommand] on static methods
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    {
                        var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                        if (attr == null) continue;
                        var m = method; // capture
                        Register(attr.Name, args =>
                        {
                            object result = m.Invoke(null, new object[] { args });
                            return result?.ToString() ?? "";
                        }, attr.Usage, attr.Summary);
                    }
                }
            }
        }

        // ── HISTORY ───────────────────────────────────────────────────────────

        private void PushCmdHistory(string line)
        {
            _cmdHistory.Insert(0, line);
            if (_cmdHistory.Count > maxCmdHistory)
                _cmdHistory.RemoveAt(_cmdHistory.Count - 1);
        }

        private void NavigateHistory(int delta)
        {
            if (_cmdHistory.Count == 0) return;
            _cmdHistoryPos = Mathf.Clamp(_cmdHistoryPos + delta, -1, _cmdHistory.Count - 1);
            _inputBuffer = _cmdHistoryPos < 0 ? "" : _cmdHistory[_cmdHistoryPos];
        }

        // ── AUTOCOMPLETE ──────────────────────────────────────────────────────

        private void TryAutocomplete()
        {
            string prefix = _inputBuffer.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(prefix)) return;

            var matches = new List<string>();
            foreach (var key in _commands.Keys)
                if (key.StartsWith(prefix)) matches.Add(key);

            if (matches.Count == 1)
                _inputBuffer = matches[0] + " ";
            else if (matches.Count > 1)
                AddEntry(ConsoleEntry.EntryType.Info, string.Join("  ", matches));
        }

        // ── OUTPUT BUFFER ─────────────────────────────────────────────────────

        private void AddEntry(ConsoleEntry.EntryType type, string text)
        {
            _history.Add(new ConsoleEntry(type, text));
            while (_history.Count > maxHistory)
                _history.RemoveAt(0);
            _scrollToBottom = true;
        }

        // ── GUI HELPERS ───────────────────────────────────────────────────────

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(new Color(0f, 0f, 0f, 0.85f)) }
            };

            _outputStyle = MakeTextStyle(new Color(0.9f, 0.9f, 0.9f), fontSize);
            _errorStyle  = MakeTextStyle(new Color(1f, 0.3f, 0.3f), fontSize);
            _warnStyle   = MakeTextStyle(new Color(1f, 0.8f, 0f), fontSize);
            _infoStyle   = MakeTextStyle(new Color(0.4f, 0.9f, 1f), fontSize);
            _inputStyle  = MakeTextStyle(new Color(1f, 1f, 0.2f), fontSize);

            GUIStyle stdIn = new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                normal   = { textColor = new Color(1f, 1f, 0.2f) }
            };
            _inputStyle = stdIn;
        }

        private GUIStyle MakeTextStyle(Color col, int size) =>
            new GUIStyle(GUI.skin.label) { fontSize = size, normal = { textColor = col }, wordWrap = true };

        private GUIStyle StyleForEntry(ConsoleEntry.EntryType t) =>
            t switch
            {
                ConsoleEntry.EntryType.Error   => _errorStyle,
                ConsoleEntry.EntryType.Warning => _warnStyle,
                ConsoleEntry.EntryType.Info    => _infoStyle,
                ConsoleEntry.EntryType.Input   => _outputStyle,
                _ => _outputStyle
            };

        private float EntryTotalHeight()
        {
            float h = 0;
            foreach (var e in _history)
                h += _outputStyle.CalcHeight(new GUIContent(e.Text), Screen.width - 24) + 1f;
            return Mathf.Max(h, 1f);
        }

        private static Texture2D MakeTexture(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        // ── ARG SPLITTING ─────────────────────────────────────────────────────

        private static string[] SplitArgs(string line)
        {
            var args = new List<string>();
            var current = new StringBuilder();
            bool inQuote = false;
            foreach (char c in line)
            {
                if (c == '"') { inQuote = !inQuote; continue; }
                if (c == ' ' && !inQuote)
                {
                    if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); }
                }
                else current.Append(c);
            }
            if (current.Length > 0) args.Add(current.ToString());
            return args.ToArray();
        }
    }
}
