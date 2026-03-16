using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KotORUnity.KotOR.Parsers;
using KotORUnity.Bootstrap;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE NODE TYPES
    // ═══════════════════════════════════════════════════════════════════════════

    public enum DlgNodeType { Entry, Reply }

    /// <summary>
    /// A node in the dialogue graph. Corresponds to one "entry" (NPC line) or
    /// "reply" (player option) in a KotOR DLG file.
    /// </summary>
    [Serializable]
    public class DlgNode
    {
        // ── IDENTITY ─────────────────────────────────────────────────────────
        public string   NodeId;           // GUID assigned by editor
        public DlgNodeType Type;
        public int      OriginalIndex;    // original row in DLG entry/reply list (-1 = new)

        // ── CONTENT ──────────────────────────────────────────────────────────
        public string   Text;             // display text (from TLK or direct override)
        public uint     TextStrRef;       // TLK string reference (0 = use Text directly)
        public string   Speaker;          // tag of the NPC who says this (entries only)
        public string   Listener;         // tag of who hears it

        // ── AUDIO / LIP ──────────────────────────────────────────────────────
        public string   SoundResRef;      // VO wav/mp3 resref
        public string   AnimResRef;       // animation to play on speaker
        public string   LipResRef;        // .lip file resref
        public float    Delay;            // pause before this node plays (seconds)

        // ── SCRIPTS ──────────────────────────────────────────────────────────
        public string   ActionScript;     // fires when this node is spoken/selected
        public string   ConditionScript;  // returns bool — hide this option if false

        // ── CAMERA ───────────────────────────────────────────────────────────
        public int      CameraAngle;      // 0=default,1=top,2=right,3=left,4=custom
        public string   CameraResRef;     // custom camera position object tag

        // ── LINKS ────────────────────────────────────────────────────────────
        /// <summary>Ordered list of child NodeIds (entries link to replies, replies link to entries).</summary>
        public List<string> Links = new List<string>();
        public bool     IsEnd;            // no links = end of conversation

        // ── EDITOR LAYOUT ─────────────────────────────────────────────────────
        public Vector2  EditorPosition;   // position in the node-graph canvas
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE GRAPH
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A complete dialogue graph — one .dlg file's contents as an editable model.
    /// Serializes to/from the KotOR GFF DLG format and a custom JSON format for
    /// the Unity Editor.
    /// </summary>
    [Serializable]
    public class DialogueGraph
    {
        public string              FileName;      // resref (no extension)
        public string              StartNodeId;   // NodeId of the first entry to show
        public bool                PreventZoom;   // KotOR DLG flag
        public string              OnEndScript;
        public string              OnAbortScript;
        public string              NpcTag;        // tag of the main NPC speaker

        public string ResRef    => FileName;          // alias used by EditorWindow
        public int    EntryCount => Nodes?.Count ?? 0; // alias used by EditorWindow

        public List<DlgNode> Nodes = new List<DlgNode>();

        // ── NODE ACCESS ───────────────────────────────────────────────────────

        private Dictionary<string, DlgNode> _byId;

        public DlgNode GetNode(string id)
        {
            if (_byId == null) RebuildIndex();
            _byId.TryGetValue(id, out var n);
            return n;
        }

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, DlgNode>(StringComparer.Ordinal);
            foreach (var n in Nodes) _byId[n.NodeId] = n;
        }

        public IEnumerable<DlgNode> Entries => Nodes.FindAll(n => n.Type == DlgNodeType.Entry);
        public IEnumerable<DlgNode> Replies  => Nodes.FindAll(n => n.Type == DlgNodeType.Reply);

        // ── CREATE / DELETE NODES ─────────────────────────────────────────────

        public DlgNode CreateNode(DlgNodeType type, string text = "")
        {
            var node = new DlgNode
            {
                NodeId        = Guid.NewGuid().ToString("N"),
                Type          = type,
                Text          = text,
                OriginalIndex = -1
            };
            Nodes.Add(node);
            _byId = null; // invalidate
            return node;
        }

        public void DeleteNode(string nodeId)
        {
            Nodes.RemoveAll(n => n.NodeId == nodeId);
            // Remove all links to this node
            foreach (var n in Nodes) n.Links.Remove(nodeId);
            _byId = null;
        }

        public void AddLink(string fromId, string toId)
        {
            var from = GetNode(fromId);
            if (from == null || from.Links.Contains(toId)) return;
            from.Links.Add(toId);
        }

        public void RemoveLink(string fromId, string toId)
        {
            GetNode(fromId)?.Links.Remove(toId);
        }

        // ── PARSE FROM GFF DLG ───────────────────────────────────────────────

        /// <summary>
        /// Parse a KotOR DLG GFF byte array into an editable DialogueGraph.
        /// Populates Nodes with all entries and replies, and resolves links.
        /// </summary>
        public static DialogueGraph ParseFromGff(byte[] dlgData, string resref,
                                                   Bootstrap.TlkReader tlk = null)
        {
            if (dlgData == null || dlgData.Length < 4) return null;

            var root = GffReader.Parse(dlgData);
            if (root == null) return null;

            var graph = new DialogueGraph
            {
                FileName     = resref,
                OnEndScript  = GffReader.GetString(root, "EndConverAbort"),
                OnAbortScript= GffReader.GetString(root, "EndConversation"),
                NpcTag       = GffReader.GetString(root, "Tag")
            };

            // Parse entry list
            var entryList = GffReader.GetList(root, "EntryList");
            var entryNodes = new List<DlgNode>(entryList.Count);
            for (int i = 0; i < entryList.Count; i++)
            {
                var gffEntry = entryList[i];
                uint strRef  = (uint)GffReader.GetInt(gffEntry, "Text");
                string text  = tlk != null ? tlk.GetString(strRef) : $"[{strRef}]";

                var node = new DlgNode
                {
                    NodeId        = $"e_{i}",
                    Type          = DlgNodeType.Entry,
                    OriginalIndex = i,
                    Text          = text,
                    TextStrRef    = strRef,
                    Speaker       = GffReader.GetString(gffEntry, "Speaker"),
                    Listener      = GffReader.GetString(gffEntry, "Listener"),
                    SoundResRef   = GffReader.GetString(gffEntry, "VO_ResRef"),
                    AnimResRef    = GffReader.GetString(gffEntry, "Animation"),
                    LipResRef     = GffReader.GetString(gffEntry, "VO_ResRef"), // same as VO in KotOR
                    ActionScript  = GffReader.GetString(gffEntry, "Script"),
                    CameraAngle   = GffReader.GetInt   (gffEntry, "CameraAngle"),
                    EditorPosition= new Vector2(i * 240, 0)
                };
                graph.Nodes.Add(node);
                entryNodes.Add(node);
            }

            // Parse reply list
            var replyList  = GffReader.GetList(root, "ReplyList");
            var replyNodes = new List<DlgNode>(replyList.Count);
            for (int i = 0; i < replyList.Count; i++)
            {
                var gffReply = replyList[i];
                uint strRef  = (uint)GffReader.GetInt(gffReply, "Text");
                string text  = tlk != null ? tlk.GetString(strRef) : $"[{strRef}]";

                var node = new DlgNode
                {
                    NodeId           = $"r_{i}",
                    Type             = DlgNodeType.Reply,
                    OriginalIndex    = i,
                    Text             = text,
                    TextStrRef       = strRef,
                    ActionScript     = GffReader.GetString(gffReply, "Script"),
                    ConditionScript  = GffReader.GetString(gffReply, "Active"),
                    IsEnd            = GffReader.GetInt   (gffReply, "IsChild", 0) == 0,
                    EditorPosition   = new Vector2(i * 240, 200)
                };
                graph.Nodes.Add(node);
                replyNodes.Add(node);
            }

            // Wire entry → reply links
            for (int i = 0; i < entryList.Count; i++)
            {
                var links = GffReader.GetList(entryList[i], "RepliesList");
                foreach (var lnk in links)
                {
                    int replyIdx = GffReader.GetInt(lnk, "Index");
                    if (replyIdx >= 0 && replyIdx < replyNodes.Count)
                        entryNodes[i].Links.Add(replyNodes[replyIdx].NodeId);
                }
            }

            // Wire reply → entry links
            for (int i = 0; i < replyList.Count; i++)
            {
                var links = GffReader.GetList(replyList[i], "EntriesList");
                foreach (var lnk in links)
                {
                    int entryIdx = GffReader.GetInt(lnk, "Index");
                    if (entryIdx >= 0 && entryIdx < entryNodes.Count)
                        replyNodes[i].Links.Add(entryNodes[entryIdx].NodeId);
                }
            }

            // Start node: first entry in StartingList
            var startList = GffReader.GetList(root, "StartingList");
            if (startList.Count > 0)
            {
                int startIdx = GffReader.GetInt(startList[0], "Index");
                graph.StartNodeId = $"e_{startIdx}";
            }
            else if (entryNodes.Count > 0)
            {
                graph.StartNodeId = entryNodes[0].NodeId;
            }

            return graph;
        }

        // ── SERIALIZE TO JSON ─────────────────────────────────────────────────

        /// <summary>
        /// Serialize to a JSON string using Unity's JsonUtility.
        /// The JSON can be stored in a mod's AssetBundle or the Override folder
        /// as a .dlg.json file loaded at runtime.
        /// </summary>
        public string ToJson() => UnityEngine.JsonUtility.ToJson(this, prettyPrint: true);

        public static DialogueGraph FromJson(string json) =>
            UnityEngine.JsonUtility.FromJson<DialogueGraph>(json);

        /// <summary>Save to a .dlg.json file.</summary>
        public bool SaveToFile(string outputPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, ToJson(), Encoding.UTF8);
                Debug.Log($"[DialogueEditor] Saved: {outputPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueEditor] Save failed: {e.Message}");
                return false;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE EDITOR  —  Service class (runtime + editor)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Service that loads, manages, and saves DialogueGraphs.
    /// Used by the Editor window and by runtime mod loading.
    /// </summary>
    public class DialogueEditorTool
    {
        public DialogueGraph CurrentGraph { get; private set; }
        public bool IsDirty => _dirty;
        private bool _dirty;

        // ── LOAD ──────────────────────────────────────────────────────────────

        public bool LoadFromArchive(string dlgResRef, TlkReader tlk = null)
        {
            var rm = SceneBootstrapper.Resources;
            if (rm == null) { Debug.LogWarning("[DlgEditor] No ResourceManager."); return false; }

            byte[] data = rm.GetResource(dlgResRef, KotOR.FileReaders.ResourceType.DLG);
            if (data == null) { Debug.LogWarning($"[DlgEditor] DLG not found: '{dlgResRef}'"); return false; }

            CurrentGraph = DialogueGraph.ParseFromGff(data, dlgResRef, tlk);
            _dirty       = false;
            return CurrentGraph != null;
        }

        public bool LoadFromFile(string path, TlkReader tlk = null)
        {
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                CurrentGraph = DialogueGraph.FromJson(File.ReadAllText(path));
            }
            else
            {
                byte[] data = File.ReadAllBytes(path);
                CurrentGraph = DialogueGraph.ParseFromGff(data,
                    Path.GetFileNameWithoutExtension(path), tlk);
            }
            _dirty = false;
            return CurrentGraph != null;
        }

        public void NewGraph(string dlgResRef)
        {
            CurrentGraph = new DialogueGraph { FileName = dlgResRef };
            var firstEntry = CurrentGraph.CreateNode(DlgNodeType.Entry, "Hello, traveller.");
            firstEntry.Speaker = "npc";
            CurrentGraph.StartNodeId = firstEntry.NodeId;
            _dirty = true;
        }

        // ── EDIT ──────────────────────────────────────────────────────────────

        public DlgNode AddEntry(string text, string speaker = "")
        {
            var n = CurrentGraph?.CreateNode(DlgNodeType.Entry, text);
            if (n != null) { n.Speaker = speaker; _dirty = true; }
            return n;
        }

        public DlgNode AddReply(string text, string conditionScript = "")
        {
            var n = CurrentGraph?.CreateNode(DlgNodeType.Reply, text);
            if (n != null) { n.ConditionScript = conditionScript; _dirty = true; }
            return n;
        }

        public void Link(string fromId, string toId)
        {
            CurrentGraph?.AddLink(fromId, toId);
            _dirty = true;
        }

        public void Unlink(string fromId, string toId)
        {
            CurrentGraph?.RemoveLink(fromId, toId);
            _dirty = true;
        }

        public void SetNodeText(string nodeId, string text)
        {
            var n = CurrentGraph?.GetNode(nodeId);
            if (n != null) { n.Text = text; _dirty = true; }
        }

        public void SetNodeScript(string nodeId, string actionScript)
        {
            var n = CurrentGraph?.GetNode(nodeId);
            if (n != null) { n.ActionScript = actionScript; _dirty = true; }
        }

        // ── SAVE ──────────────────────────────────────────────────────────────

        public bool SaveToJson(string outputPath)
        {
            bool ok = CurrentGraph?.SaveToFile(outputPath) ?? false;
            if (ok) _dirty = false;
            return ok;
        }

        // ── EDITOR GUI / EXTRA API STUBS ───────────────────────────────
        /// <summary>Create a blank new dialogue tree.</summary>
        public void NewDlg() { CurrentGraph = new DialogueGraph(); _dirty = false; }

        /// <summary>Load DLG from raw GFF bytes.</summary>
        public void LoadFromBytes(byte[] data, string resref)
        {
            CurrentGraph = new DialogueGraph { FileName = resref };
            if (data != null && data.Length > 0)
            {
                try
                {
                    // Parse via DialogueSystem runtime parser
                    var tree = new KotORUnity.Dialogue.DialogueTree();
                    if (tree.Load(data, resref))
                    {
                        // Walk the current-entry start list by starting the tree and walking
                        // entries. We populate the editor graph with NPC entry nodes.
                        int imported = 0;
                        // DialogueTree exposes CurrentEntry after Start(); iterate via
                        // the internal list by calling GetRawEntry helper (or fall back
                        // to StartTree walking for the root nodes).
                        // Since _entries is private, we import via GFF parser directly.
                        var gff = GffReader.Parse(data);
                        if (gff != null)
                        {
                            var entryList = GffReader.GetList(gff, "EntryList");
                            foreach (var entryStruct in entryList)
                            {
                                uint strRef  = (uint)GffReader.GetInt(entryStruct, "Text", 0);
                                string text  = KotORUnity.Bootstrap.SceneBootstrapper.GetString(strRef);
                                string spk   = GffReader.GetString(entryStruct, "Speaker");
                                string script= GffReader.GetString(entryStruct, "Script");

                                var node = new DlgNode
                                {
                                    NodeId       = System.Guid.NewGuid().ToString(),
                                    Type         = DlgNodeType.Entry,
                                    OriginalIndex= imported,
                                    Text         = text,
                                    TextStrRef   = strRef,
                                    Speaker      = spk,
                                    ActionScript = script
                                };
                                CurrentGraph.Nodes.Add(node);
                                imported++;
                            }
                        }
                        UnityEngine.Debug.Log($"[DialogueEditor] Imported {imported} entry nodes from {resref}.");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[DialogueEditor] DLG parse failed for {resref}.");
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[DialogueEditor] Exception parsing {resref}: {ex.Message}");
                }
            }
            _dirty = false;
            UnityEngine.Debug.Log($"[DialogueEditor] Loaded DLG: {resref} ({data?.Length ?? 0} bytes)");
        }

        /// <summary>Export current graph to JSON.</summary>
        public void ExportJson(string path)
        {
            if (CurrentGraph == null) return;
            string json = UnityEngine.JsonUtility.ToJson(CurrentGraph, true);
            System.IO.File.WriteAllText(path, json);
            UnityEngine.Debug.Log($"[DialogueEditor] Exported to {path}");
        }

        /// <summary>Minimal IMGUI draw for the EditorWindow.</summary>
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (CurrentGraph == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("No dialogue loaded. Use Load or New.", UnityEditor.MessageType.Info);
                return;
            }
            UnityEditor.EditorGUILayout.LabelField($"ResRef: {CurrentGraph.ResRef}  |  Nodes: {CurrentGraph.EntryCount}",
                UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
