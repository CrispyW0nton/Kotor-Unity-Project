using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.Core;
using KotORUnity.SaveSystem;
using KotORUnity.World;

namespace KotORUnity.Scripting
{
    /// <summary>
    /// Minimal NWScript virtual machine stub.
    ///
    /// KotOR uses compiled NWScript (.ncs) bytecode to drive all in-game logic:
    /// door actions, dialogue conditions, trigger events, item use, etc.
    ///
    /// This class provides:
    ///   1. A small library of the most-called NWScript "engine calls" implemented
    ///      in C# so that common scripts (Open/Close doors, GiveItem, etc.) work.
    ///   2. A "Run" entry point that attempts to find a handler registered for
    ///      a known script name, or logs a graceful warning for unsupported scripts.
    ///   3. A "RunCondition" entry point used for dialogue conditionals.
    ///
    /// Full bytecode execution is out of scope for the MVP; add specific handlers
    /// as needed via <see cref="RegisterHandler"/>.
    /// </summary>
    public static class NWScriptVM
    {
        // ── HANDLER REGISTRY ──────────────────────────────────────────────────
        /// <summary>A script handler is a C# delegate that mirrors an NWScript body.</summary>
        public delegate void  ScriptHandler(ScriptContext ctx);
        public delegate bool  ConditionHandler(ScriptContext ctx);

        private static readonly Dictionary<string, ScriptHandler>   _handlers
            = new Dictionary<string, ScriptHandler>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConditionHandler> _conditions
            = new Dictionary<string, ConditionHandler>(StringComparer.OrdinalIgnoreCase);

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>Run an action script (fires-and-forgets).</summary>
        public static void Run(string scriptName, GameObject self, GameObject caller = null)
        {
            if (string.IsNullOrEmpty(scriptName)) return;

            var ctx = new ScriptContext(self, caller);

            if (_handlers.TryGetValue(scriptName, out var handler))
            {
                try { handler(ctx); }
                catch (Exception e)
                {
                    Debug.LogError($"[NWScriptVM] Script '{scriptName}' threw: {e.Message}");
                }
                return;
            }

            // Try compiled bytecode if NcsRunner is available (future extension point)
            if (NcsRunner.TryRun(scriptName, ctx)) return;

            Debug.LogWarning($"[NWScriptVM] Unhandled script: '{scriptName}' " +
                             $"(self={self?.name ?? "null"})");
        }

        /// <summary>Run a condition script and return its boolean result.</summary>
        public static bool RunCondition(string scriptName, GameObject self)
        {
            if (string.IsNullOrEmpty(scriptName)) return true;

            var ctx = new ScriptContext(self, null);

            if (_conditions.TryGetValue(scriptName, out var cond))
            {
                try { return cond(ctx); }
                catch (Exception e)
                {
                    Debug.LogError($"[NWScriptVM] Condition '{scriptName}' threw: {e.Message}");
                }
            }

            // Unknown condition — default to true (show the entry / dialogue branch)
            return true;
        }

        /// <summary>Register a C# handler for a named script.</summary>
        public static void RegisterHandler(string scriptName, ScriptHandler handler)
            => _handlers[scriptName] = handler;

        /// <summary>Register a C# handler using the simpler Script-name → Action pattern.</summary>
        public static void RegisterScript(string scriptName, ScriptHandler handler)
            => _handlers[scriptName] = handler;

        /// <summary>Register a C# condition handler for a named script.</summary>
        public static void RegisterCondition(string scriptName, ConditionHandler handler)
            => _conditions[scriptName] = handler;

        /// <summary>Expose the script registry keys for dev-console listing.</summary>
        private static Dictionary<string, ScriptHandler> _scriptRegistry => _handlers;

        // ── ENGINE CALL DISPATCH (called by NcsInterpreter.DispatchEngineCall) ─
        /// <summary>
        /// Dispatch an NWScript engine call by its action-constant index.
        /// Mirrors CSWVirtualMachineCommands.ExecuteCommand(ScriptFunctions, paramCount).
        /// Only a subset of the 800+ NWScript functions are implemented here;
        /// unmapped indices log a warning and return null.
        /// </summary>
        public static object DispatchAction(int index, object[] parms, ScriptContext ctx)
        {
            // Helper to safely read a param
            T P<T>(int i, T def = default) =>
                (i < parms.Length && parms[i] is T v) ? v : def;

            switch (index)
            {
                // 0 - Random(int nMaxInteger) → int
                case 0:   return UnityEngine.Random.Range(0, P<int>(0, 1));
                // 1 - PrintString(string sString)
                case 1:   Debug.Log($"[NWScript] {P<string>(0)}"); return null;
                // 2 - PrintFloat(float fFloat, ...)
                case 2:   Debug.Log($"[NWScript] {P<float>(0)}"); return null;
                // 3 - FloatToInt(float fFloat) → int
                case 3:   return (int)P<float>(0);
                // 4 - IntToFloat(int nInteger) → float
                case 4:   return (float)P<int>(0);
                // 5 - IntToString(int nInteger) → string
                case 5:   return P<int>(0).ToString();
                // 6 - GetStringLength(string s) → int
                case 6:   return P<string>(0, "").Length;
                // 7 - GetStringUpperCase(string s) → string
                case 7:   return P<string>(0, "").ToUpper();
                // 8 - GetStringLowerCase(string s) → string
                case 8:   return P<string>(0, "").ToLower();
                // 9 - GetStringRight(string, int) → string
                case 9:
                {   string s = P<string>(0,""); int n = P<int>(1,0);
                    return n > 0 && n <= s.Length ? s.Substring(s.Length - n) : ""; }
                // 10 - GetStringLeft(string, int) → string
                case 10:
                {   string s = P<string>(0,""); int n = P<int>(1,0);
                    return n > 0 && n <= s.Length ? s.Substring(0, n) : ""; }
                // 14 - GetObjectType(object) → int
                case 14:  return 1; // OBJECT_TYPE_CREATURE
                // 26 - GetIsPC(object) → int
                case 26:
                {   var go = parms.Length > 0 ? parms[0] as GameObject : null;
                    return (go != null && go.CompareTag("Player")) ? 1 : 0; }
                // 27 - GetDistanceBetween(object, object) → float
                case 27:
                {   var a = parms.Length > 0 ? parms[0] as GameObject : null;
                    var b = parms.Length > 1 ? parms[1] as GameObject : null;
                    if (a == null || b == null) return 0f;
                    return UnityEngine.Vector3.Distance(a.transform.position, b.transform.position); }
                // 29 - GetObjectByTag(string tag, int nth) → object
                case 29:
                {   string tag = P<string>(0,"");
                    var found = UnityEngine.GameObject.FindGameObjectWithTag(tag);
                    return found; }
                // 47 - GetIsObjectValid(object) → int
                case 47:  return (parms.Length > 0 && parms[0] != null) ? 1 : 0;
                // 110 - SendMessageToPC(object, string)
                case 110: Debug.Log($"[NWScript→PC] {P<string>(1)}"); return null;
                // 190 - GetGlobalBoolean(string) → int
                case 190: return GlobalVars.GetBool(P<string>(0,"")) ? 1 : 0;
                // 191 - SetGlobalBoolean(string, int)
                case 191: GlobalVars.SetBool(P<string>(0,""), P<int>(1) != 0); return null;
                // 192 - GetGlobalNumber(string) → int
                case 192: return GlobalVars.GetInt(P<string>(0,""));
                // 193 - SetGlobalNumber(string, int)
                case 193: GlobalVars.SetInt(P<string>(0,""), P<int>(1)); return null;
                // 194 - GetGlobalString(string) → string
                case 194: return GlobalVars.GetString(P<string>(0,""));
                // 195 - SetGlobalString(string, string)
                case 195: GlobalVars.SetString(P<string>(0,""), P<string>(1,"")); return null;
                // 215 - GetLocalInt(object, string) → int
                case 215: return LocalVars.GetInt(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""));
                // 216 - SetLocalInt(object, string, int)
                case 216: LocalVars.SetInt(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""), P<int>(2)); return null;
                // 217 - GetLocalFloat(object, string) → float
                case 217: return LocalVars.GetFloat(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""));
                // 218 - SetLocalFloat(object, string, float)
                case 218: LocalVars.SetFloat(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""), P<float>(2)); return null;
                // 219 - GetLocalString(object, string) → string
                case 219: return LocalVars.GetString(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""));
                // 220 - SetLocalString(object, string, string)
                case 220: LocalVars.SetString(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""), P<string>(2,"")); return null;
                // 221 - GetLocalBoolean(object, string) → int
                case 221: return LocalVars.GetBool(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,"")) ? 1 : 0;
                // 222 - SetLocalBoolean(object, string, int)
                case 222: LocalVars.SetBool(parms.Length > 0 ? parms[0] as GameObject : null, P<string>(1,""), P<int>(2) != 0); return null;

                // ── KotOR: creature stats ────────────────────────────────────
                // 39 - GetCurrentHitPoints(object) → int
                case 39:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             return (int)(go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.CurrentHealth ?? 0f); }
                // 40 - GetMaxHitPoints(object) → int
                case 40:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             return (int)(go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.MaxHealth ?? 100f); }
                // 66 - GetHitDice / GetLevel(object) → int
                case 66:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             return go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.Level ?? 1; }
                // 43 - GetSkillRank(int skill, object) → int (not fully implemented)
                case 43:  return 0;
                // 139 - GetAbilityScore(object, int) → int
                case 139: { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             int ab = P<int>(1, 0);
                             var stats = go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats;
                             if (stats == null) return 10;
                             return ab switch { 0 => stats.Strength, 1 => stats.Dexterity, 2 => stats.Constitution,
                                                3 => stats.Intelligence, 4 => stats.Wisdom, 5 => stats.Charisma, _ => 10 }; }
                // 35 - GetIsDead(object) → int
                case 35:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             var hp = (int)(go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.CurrentHealth ?? 1f);
                             return hp <= 0 ? 1 : 0; }

                // ── KotOR: XP / Level ─────────────────────────────────────────
                // 72 - GiveXPToCreature(object, int)
                case 72:  { int xp = P<int>(1, 0);
                             Progression.LevelSystem.Instance?.AwardXP(xp, "Script"); return null; }
                // 73 - SetXP(object, int)
                case 73:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             int xp = P<int>(1, 0);
                             if (go != null && go.CompareTag("Player"))
                                 Progression.LevelSystem.Instance?.SetXP(xp);
                             return null; }
                // 74 - GetXP(object) → int
                case 74:  return Progression.LevelSystem.Instance?.CurrentXP ?? 0;

                // ── KotOR: Credits ─────────────────────────────────────────────
                // 69 - GetGold(object) → int
                case 69:  return Party.PartyManager.Instance?.Table?.Credits ?? 0;
                // 70 - GiveGoldToCreature(object, int)
                case 70:  { int credits = P<int>(1, 0);
                             Party.PartyManager.Instance?.Table?.AddCredits(credits); return null; }
                // 71 - TakeGoldFromCreature(int, object, int destroyIt)
                case 71:  { int amount = P<int>(0, 0);
                             Party.PartyManager.Instance?.Table?.SpendCredits(amount); return null; }

                // ── KotOR: party ──────────────────────────────────────────────
                // 395 - IsObjectPartyMember(object) → int
                case 395: { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             return Party.PartyManager.Instance?.Roster?.Any(m => m.ResRef == go?.name) == true ? 1 : 0; }
                // 3960 - AddPartyMember(int, object) [custom, avoids NWScript opcode conflict]
                case 3960: { var go = parms.Length > 1 ? parms[1] as GameObject : null;
                             if (go != null) Party.PartyManager.Instance?.Recruit(go.name);
                             return null; }
                // 3970 - RemovePartyMember(object) [custom, avoids NWScript opcode conflict]
                case 3970: { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             if (go != null) Party.PartyManager.Instance?.Deactivate(go.name);
                             return null; }

                // ── KotOR: faction ────────────────────────────────────────────
                // 169 - GetIsEnemy(object, object) → int
                case 169: { var a = parms.Length > 0 ? parms[0] as GameObject : null;
                             var b = parms.Length > 1 ? parms[1] as GameObject : null;
                             int facA = a?.GetComponent<World.KotorCreatureData>()?.FactionId ?? 0;
                             int facB = b?.GetComponent<World.KotorCreatureData>()?.FactionId ?? 0;
                             return Core.FactionManager.AreHostile(facA, facB) ? 1 : 0; }
                // 171 - GetIsFriend(object, object) → int
                case 171: { var a = parms.Length > 0 ? parms[0] as GameObject : null;
                             var b = parms.Length > 1 ? parms[1] as GameObject : null;
                             int facA2 = a?.GetComponent<World.KotorCreatureData>()?.FactionId ?? 0;
                             int facB2 = b?.GetComponent<World.KotorCreatureData>()?.FactionId ?? 0;
                             return Core.FactionManager.AreFriendly(facA2, facB2) ? 1 : 0; }
                // 168 - GetFactionEqual(object, object) → int
                case 168: return 0;

                // ── KotOR: items ──────────────────────────────────────────────
                // 33 - CreateItemOnObject(string resref, object target, int qty)
                case 33:  GiveItem(P<string>(0,""), parms.Length > 1 ? parms[1] as GameObject : ctx?.Self); return null;
                // 38 - DestroyObject(object, float delay)
                case 38:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             float delay = P<float>(1, 0f);
                             if (go != null) UnityEngine.Object.Destroy(go, delay); return null; }

                // ── KotOR: doors ──────────────────────────────────────────────
                // 11 - ActionOpenDoor(object)
                case 11:  { var go = parms.Length > 0 ? parms[0] as GameObject : null;
                             go?.GetComponent<World.DoorController>()?.TryOpen(); return null; }
                // 12 - ActionCloseDoor(object)
                case 12:  return null;

                // ── KotOR: dialogue ───────────────────────────────────────────
                // 171 already used above; 206 - SetCustomToken
                case 206: SetCustomToken(P<int>(0, 0), P<string>(1, "")); return null;

                // ── KotOR: sound / music ──────────────────────────────────────
                // 75 - PlaySound(string)
                case 75:  PlaySound(P<string>(0, "")); return null;
                // 50 - PlayVoiceChat(int, object)
                case 50:  PlayVoiceChat(P<int>(0, 0), parms.Length > 1 ? parms[1] as GameObject : null); return null;
                // 401 - MusicBackgroundPlay(int)
                case 401: MusicBackgroundPlay(P<int>(0, 0)); return null;
                // 402 - MusicBackgroundStop
                case 402: MusicBackgroundStop(); return null;
                // 403 - MusicBattlePlay(int)
                case 403: MusicBackgroundPlay(P<int>(0, 0)); return null;

                // ── KotOR: trigger / area ─────────────────────────────────────
                // 82 - GetEnteringObject() → object
                case 82:  return ctx?.Caller;
                // 83 - GetExitingObject() → object
                case 83:  return ctx?.Caller;
                // 236 - GetArea(object) → string
                case 236: return GetArea(parms.Length > 0 ? parms[0] as GameObject : null);
                // 237 - GetModule() → string
                case 237: return GetModule();
                // 243 - ActionJumpToLocation(location)
                case 243: { if (parms.Length > 0 && parms[0] is Vector3 loc)
                              JumpToLocation(ctx?.Self, loc);
                            return null; }

                // ── KotOR: misc ───────────────────────────────────────────────
                // 48 - GetObjectByTag(string, int nth) → object
                case 48:  return GetObjectByTag(P<string>(0, ""));
                // 26 (already handled) duplicate alias
                // 59 - GetDistanceBetween2D → float
                case 59:  { var a = parms.Length > 0 ? parms[0] as GameObject : null;
                             var b = parms.Length > 1 ? parms[1] as GameObject : null;
                             if (a == null || b == null) return 0f;
                             var d = a.transform.position - b.transform.position;
                             return Mathf.Sqrt(d.x*d.x + d.z*d.z); }
                // 89-92 - time functions
                case 89: return System.DateTime.Now.Hour;
                case 90: return System.DateTime.Now.Minute;
                case 91: return System.DateTime.Now.Second;
                case 92: return 0;
                // 96 - SpeakString(string)
                case 96:  Debug.Log($"[NWScript/Speak] {ctx?.Self?.name}: {P<string>(0)}"); return null;
                // 100 - ActionSpeakString(string)
                case 100: Debug.Log($"[NWScript/ActionSpeak] {ctx?.Self?.name}: {P<string>(0)}"); return null;
                // 110 (alias) - SendMessageToPC
                // case 110 already handled above

                // ── KotOR Journal functions ───────────────────────────────────
                // 396 - AddJournalQuestEntry(string tag, int stateId)
                case 396:
                {
                    string jTag     = P<string>(0, "");
                    int    jStateId = P<int>(1);
                    UI.JournalSystem.Instance?.AddEntry(jTag, jStateId);
                    return null;
                }
                // 397 - RemoveJournalQuestEntry(string tag)
                case 397:
                {
                    string jTag2 = P<string>(0, "");
                    UI.JournalSystem.Instance?.GetQuest(jTag2); // track removal as state 0
                    UI.JournalSystem.Instance?.AddEntry(jTag2, 0);
                    return null;
                }
                // 398 - GetJournalQuestExperience(string tag) → int (XP reward)
                case 398:
                {
                    // KotOR uses this to get the XP registered for a quest objective.
                    // Return a placeholder value; real value would come from journal.2da.
                    return 0;
                }

                // ── Galaxy Map travel ─────────────────────────────────────────
                // 470 - JumpToArea(string module, string waypoint) — also sets galaxy map planet
                case 470:
                {
                    string destModule   = P<string>(0, "");
                    string destWaypoint = P<string>(1, "");
                    EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                        new EventBus.ModuleEventArgs(destModule, destWaypoint));
                    return null;
                }

                // ── Pazaak ────────────────────────────────────────────────────
                // 480 - StartPazaakGame(object opponent, int wager)
                case 480:
                {
                    int wager = P<int>(1);
                    var manager = UnityEngine.Object.FindObjectOfType<UI.PazaakManager>();
                    manager?.StartMatch("Opponent", wager,
                        UI.PazaakManager.DefaultSideDeck());
                    return null;
                }

                default:
                    if (index > 0)
                        Debug.LogWarning($"[NWScriptVM] Unimplemented action index {index} (paramCount={parms.Length})");
                    return null;
            }
        }
        // These are static helpers that script handlers call, mirroring the
        // NWScript built-in function library.

        /// <summary>GiveItem — add an item to a creature's inventory.</summary>
        public static void GiveItem(string itemResRef, GameObject target)
        {
            var inv = Inventory.InventoryManager.Instance;
            if (inv != null && (target?.CompareTag("Player") ?? false))
                inv.PickUp(itemResRef);
            else
                Debug.Log($"[NWScript] GiveItem({itemResRef}) → {target?.name ?? "null"}");
        }

        /// <summary>OpenDoor — open the door tagged <paramref name="tag"/>.</summary>
        public static void OpenDoor(string tag)
        {
            var doors = GameObject.FindObjectsOfType<DoorController>();
            foreach (var d in doors)
                if (string.Equals(d.Tag, tag, StringComparison.OrdinalIgnoreCase))
                { d.TryOpen(); return; }
        }

        /// <summary>SetLocked — lock or unlock a door.</summary>
        public static void SetLocked(string tag, bool locked)
        {
            var doors = GameObject.FindObjectsOfType<DoorController>();
            foreach (var d in doors)
                if (string.Equals(d.Tag, tag, StringComparison.OrdinalIgnoreCase))
                { d.SetLocked(locked); return; }
        }

        /// <summary>StartConversation — begin a dialogue.</summary>
        public static void StartConversation(string dlgResRef, GameObject speaker = null)
            => Dialogue.DialogueManager.Instance?.StartDialogue(dlgResRef, speaker);

        /// <summary>JumpToArea — trigger an area transition.</summary>
        public static void JumpToArea(string moduleTag, string waypointTag = "")
            => EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                new AreaTransitionEventArgs(moduleTag, waypointTag));

        /// <summary>SendMessageToPC — pop a floating text message on the HUD.</summary>
        public static void SendMessageToPC(string message)
        {
            Debug.Log($"[NWScript] PC Message: {message}");
            // HUDManager.Instance?.ShowFloatingText(message);
        }

        /// <summary>AwardXP — give XP to the player.</summary>
        public static void AwardXP(int xp)
        {
            var ls = GameObject.FindObjectOfType<Progression.LevelSystem>();
            ls?.AwardXP(xp, "Script");
        }

        /// <summary>GetGlobalBoolean — retrieve a global flag (stub returns false).</summary>
        public static bool GetGlobalBoolean(string varName)
        {
            return GlobalVars.GetBool(varName);
        }

        /// <summary>SetGlobalBoolean — store a global flag.</summary>
        public static void SetGlobalBoolean(string varName, bool value)
            => GlobalVars.SetBool(varName, value);

        public static int  GetGlobalInt(string varName) => GlobalVars.GetInt(varName);
        public static void SetGlobalInt(string varName, int value) => GlobalVars.SetInt(varName, value);

        // ── ALIASES used by InGameDevConsole ──────────────────────────────────
        public static bool   GetGlobalBool(string k)         => GlobalVars.GetBool(k);
        public static void   SetGlobalBool(string k, bool v) => GlobalVars.SetBool(k, v);
        public static string GetGlobalString(string k)         => GlobalVars.GetString(k);
        public static void   SetGlobalString(string k, string v) => GlobalVars.SetString(k, v);

        // ── OBJECT HELPERS ────────────────────────────────────────────────────
        public static GameObject GetObjectSelf()  => GameObject.FindGameObjectWithTag("Player");
        public static bool       GetIsObjectValid(GameObject go) => go != null;
        public static GameObject GetObjectByTag(string tag)      => GameObject.FindGameObjectWithTag(tag);
        public static GameObject GetFirstPC()  => GetObjectSelf();
        public static GameObject GetNextPC()   => null;  // multiplayer stub
        public static int        GetPartyMemberCount() =>
            Party.PartyManager.Instance?.Roster?.Count ?? 1;
        public static bool       IsInParty(GameObject go) =>
            Party.PartyManager.Instance?.Roster?.Any(m => m.ResRef == go?.name) ?? false;
        public static void       AddPartyMember(string utcResRef) =>
            Party.PartyManager.Instance?.Recruit(utcResRef);
        public static void       RemovePartyMember(string utcResRef)
            => Party.PartyManager.Instance?.Deactivate(utcResRef);

        // ── STATS HELPERS ─────────────────────────────────────────────────────
        public static int   GetLevel(GameObject go)  =>
            go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.Level ?? 1;
        public static int   GetCurrentHP(GameObject go) =>
            (int)(go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.CurrentHealth ?? 0f);
        public static int   GetMaxHP(GameObject go) =>
            (int)(go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.MaxHealth ?? 0f);
        public static void  SetMaxHP(GameObject go, int maxHP) =>
            go?.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.SetMaxHealth(maxHP);
        public static int   GetXP(GameObject go) =>
            Progression.LevelSystem.Instance?.CurrentXP ?? 0;
        public static void  SetXP(GameObject go, int xp)
        {
            if (go != null && go.CompareTag("Player"))
                Progression.LevelSystem.Instance?.SetXP(xp);
        }

        // ── LOCAL VARS ────────────────────────────────────────────────────────
        // Local vars are stored as "tag::key" in GlobalVars for simplicity
        public static bool   GetLocalBool(GameObject go, string k)
            => GlobalVars.GetBool($"{go?.name}::{k}");
        public static void   SetLocalBool(GameObject go, string k, bool v)
            => GlobalVars.SetBool($"{go?.name}::{k}", v);
        public static int    GetLocalInt(GameObject go, string k)
            => GlobalVars.GetInt($"{go?.name}::{k}");
        public static void   SetLocalInt(GameObject go, string k, int v)
            => GlobalVars.SetInt($"{go?.name}::{k}", v);

        // ── LOCATION / MOVEMENT ───────────────────────────────────────────────
        public static Vector3 GetLocation(GameObject go)  => go?.transform.position ?? Vector3.zero;
        public static Vector3 GetPosition(GameObject go)  => go?.transform.position ?? Vector3.zero;
        public static void    JumpToLocation(GameObject go, Vector3 pos)
        {
            if (go != null) go.transform.position = pos;
        }
        public static string  GetArea(GameObject go) =>
            KotOR.Modules.ModuleLoader.Instance?.CurrentModuleName ?? "";
        public static string  GetModule() =>
            KotOR.Modules.ModuleLoader.Instance?.CurrentModuleName ?? "";

        // ── INVENTORY HELPERS ─────────────────────────────────────────────────
        public static void GiveItemToPlayer(string resRef) => GiveItem(resRef, GetObjectSelf());
        public static void CreateItemOnObject(string resRef, GameObject target, int qty = 1)
            => GiveItem(resRef, target);
        public static void DestroyObject(GameObject go, float delay = 0f)
        {
            if (go != null) UnityEngine.Object.Destroy(go, delay);
        }
        public static Inventory.ItemData GetItemInSlot(Inventory.EquipSlot slot, GameObject go) =>
            Inventory.InventoryManager.Instance?.PlayerInventory?.Equipped
                .TryGetValue(slot, out var item) == true ? item : null;
        public static Inventory.ItemData GetFirstItemInInventory(GameObject go) =>
            Inventory.InventoryManager.Instance?.PlayerInventory?.Items?.FirstOrDefault();
        public static Inventory.ItemData GetNextItemInInventory(GameObject go) => null;

        // ── DIALOGUE ──────────────────────────────────────────────────────────
        public static void SetCustomToken(int tokenId, string value) =>
            GlobalVars.SetString($"__token_{tokenId}", value);
        public static string GetCurrentDialog() => GlobalVars.GetString("__currentDlg");

        // ── ACHIEVEMENTS & CODEX ──────────────────────────────────────────────

        /// <summary>
        /// Unlock an achievement by id from NWScript (e.g. custom module scripts).
        /// Usage: NWScriptVM.UnlockAchievement("first_blood");
        /// </summary>
        public static void UnlockAchievement(string id)
        {
            Core.AchievementSystem.Instance?.Unlock(id);
            Debug.Log($"[NWScriptVM] UnlockAchievement({id})");
        }

        /// <summary>
        /// Increment an achievement progress counter from NWScript.
        /// Usage: NWScriptVM.IncrementAchievement("bounty_hunter", 1);
        /// </summary>
        public static void IncrementAchievement(string id, int amount = 1)
            => Core.AchievementSystem.Instance?.IncrementProgress(id, amount);

        /// <summary>
        /// Discover a Codex entry by id from NWScript.
        /// Triggers the discovered event and increments codex_scholar achievement.
        /// </summary>
        public static void DiscoverCodex(string id)
        {
            Core.CodexSystem.Instance?.Discover(id);
            Debug.Log($"[NWScriptVM] DiscoverCodex({id})");
        }

        // ── SOUND ─────────────────────────────────────────────────────────────
        public static void PlaySound(string resRef)
        {
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlaySFX(resRef);
            else
                Bootstrap.SceneBootstrapper.Resources?.GetResource(resRef, KotOR.FileReaders.ResourceType.WAV);
        }

        public static void PlayVoiceChat(int vcId, GameObject go)
        {
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayVoiceChat(vcId, go);
            else
                Debug.Log($"[NWScript] PlayVoiceChat id={vcId} speaker={go?.name}");
        }

        public static void MusicBackgroundPlay(int musicId)
        {
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayMusic(musicId);
            else
                Debug.Log($"[NWScript] MusicBackgroundPlay id={musicId}");
        }

        public static void MusicBackgroundStop()
        {
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.StopMusic(fadeOut: true);
            else
                Debug.Log("[NWScript] MusicBackgroundStop");
        }

        // ── MISC ──────────────────────────────────────────────────────────────
        public static void   FloatingText(string text, GameObject go, bool broadcastToParty = false)
            => Debug.Log($"[Float] {go?.name}: {text}");
        public static void   DelayCommand(float delay, System.Action action)
            => Bootstrap.SceneBootstrapper.Instance?.StartCoroutine(DelayCoroutine(delay, action));
        public static void   AssignCommand(GameObject go, System.Action action) => action?.Invoke();
        public static void   ActionDoCommand(System.Action action) => action?.Invoke();
        /// <summary>
        /// ApplyEffect — apply a scripted effect (stun, heal, damage, etc.) to a target.
        /// Effects are named string handles generated by Effect*() functions in NWScript.
        /// </summary>
        public static void ApplyEffect(object effect, GameObject target, float duration = 0f)
        {
            if (effect == null || target == null) return;
            string effectName = effect as string ?? effect.GetType().Name;
            // Parse common effect types and forward to the relevant system
            if (effectName.StartsWith("DAMAGE:", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(effectName.Substring(7), out float dmg))
                    target.GetComponent<Player.PlayerStatsBehaviour>()?.ApplyDamage(dmg, Core.GameEnums.DamageType.Energy);
            }
            else if (effectName.StartsWith("HEAL:", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(effectName.Substring(5), out float heal))
                    target.GetComponent<Player.PlayerStatsBehaviour>()?.Stats?.Heal(heal);
            }
            else
            {
                Debug.Log($"[NWScript] ApplyEffect '{effectName}' on {target.name} dur={duration:F1}s");
            }
        }
        /// <summary>EffectDamage — create a damage effect handle.</summary>
        public static object EffectDamage(float amount, Core.GameEnums.DamageType type = Core.GameEnums.DamageType.Energy)
            => $"DAMAGE:{amount}";
        /// <summary>EffectHeal — create a heal effect handle.</summary>
        public static object EffectHeal(float amount) => $"HEAL:{amount}";
        /// <summary>EffectDeath — create a death effect handle.</summary>
        public static object EffectDeath() => "DEATH";
        /// <summary>EffectKnockdown — create a knockdown effect handle.</summary>
        public static object EffectKnockdown() => "KNOCKDOWN";
        public static string GetSubString(string s, int start, int length)
            => start < s.Length ? s.Substring(start, Mathf.Min(length, s.Length - start)) : "";
        public static void   TriggerModule(string moduleName) => JumpToArea(moduleName, "");

        /// <summary>Spawn a creature by UTC resref at a world position.</summary>
        public static void SpawnCreatureAtLocation(string utcResRef, Vector3 pos)
            => World.CreatureSpawner.SpawnCreature(utcResRef, pos, Quaternion.identity);

        /// <summary>Returns all registered script names for the dev console 'listscripts' command.</summary>
        public static IEnumerable<string> ListScripts() => _scriptRegistry.Keys;

        private static System.Collections.IEnumerator DelayCoroutine(float delay, System.Action action)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            action?.Invoke();
        }

        /// <summary>Generic engine-call stub for unmapped NWScript functions (migration tool output).</summary>
        public static object CallEngine(string functionName, params object[] args)
        {
            Debug.LogWarning($"[NWScriptVM] Unmapped engine call: {functionName}({string.Join(", ", args)})");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SCRIPT CONTEXT  —  "self" and "caller" mirrors NWScript's OBJECT_SELF
    // ══════════════════════════════════════════════════════════════════════════
    public class ScriptContext
    {
        public GameObject Self   { get; }
        public GameObject Caller { get; }

        public ScriptContext(GameObject self, GameObject caller)
        {
            Self   = self;
            Caller = caller;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NCS BYTECODE INTERPRETER
    //  Mirrors CVirtualMachine + CVirtualMachineStack + CVirtualMachineScript
    //  from the KotOR 1 System Layout blueprint.
    //
    //  Opcode reference (BioWare NWScript Compiler):
    //    0x01 CPDOWNSP   copy-down (assignment to earlier stack slot)
    //    0x02 RSADD*     reserve stack slot for type * (I/F/S/O)
    //    0x03 CPTOPSP    copy-top (read from earlier stack slot)
    //    0x04 CONST*     push literal constant
    //    0x05 ACTION     call engine function by index + param count
    //    0x06 LOGANDII   logical AND (int,int)
    //    0x07 LOGORII    logical OR  (int,int)
    //    0x08 INCOSP     increment (SP-relative)
    //    0x09 DECOSP     decrement (SP-relative)
    //    0x0A DECIBP     decrement (BP-relative)
    //    0x0B INCIBP     increment (BP-relative)
    //    0x0C JMP        unconditional jump
    //    0x0D JSR        jump-to-subroutine (push return address)
    //    0x0E JZ         jump if top-of-stack == 0
    //    0x0F RETN       return from subroutine
    //    0x10 DESTRUCT   pop N bytes, keep inner K bytes
    //    0x11 NOTI       logical NOT (int)
    //    0x12 DECISP     decrement SP-relative
    //    0x13 INCISP     increment SP-relative
    //    0x14 JNZ        jump if top-of-stack != 0
    //    0x15 CPDOWNBP   copy-down (BP-relative)
    //    0x16 CPTOPBP    copy-top  (BP-relative)
    //    0x17 STORESTATE save state for delayed-action script
    //    0x18 NOP        no-op
    //    0x1A SAVEBP     save base pointer
    //    0x1B RESTOREBP  restore base pointer
    //    0x1C STORE_STATE_ALL  (TSL only, ignored here)
    //
    //  Sub-type bytes follow the opcode for typed operations:
    //    0x03 = int,  0x04 = float,  0x05 = string,  0x06 = object
    // ══════════════════════════════════════════════════════════════════════════
    public static class NcsInterpreter
    {
        // ── OPCODE CONSTANTS ──────────────────────────────────────────────────
        private const byte OP_CPDOWNSP   = 0x01;
        private const byte OP_RSADD      = 0x02;
        private const byte OP_CPTOPSP    = 0x03;
        private const byte OP_CONST      = 0x04;
        private const byte OP_ACTION     = 0x05;
        private const byte OP_LOGANDII   = 0x06;
        private const byte OP_LOGORII    = 0x07;
        private const byte OP_INCOSP     = 0x08;
        private const byte OP_DECOSP     = 0x09;
        private const byte OP_DECIBP     = 0x0A;
        private const byte OP_INCIBP     = 0x0B;
        private const byte OP_JMP        = 0x0C;
        private const byte OP_JSR        = 0x0D;
        private const byte OP_JZ         = 0x0E;
        private const byte OP_RETN       = 0x0F;
        private const byte OP_DESTRUCT   = 0x10;
        private const byte OP_NOTI       = 0x11;
        private const byte OP_DECISP     = 0x12;
        private const byte OP_INCISP     = 0x13;
        private const byte OP_JNZ        = 0x14;
        private const byte OP_CPDOWNBP   = 0x15;
        private const byte OP_CPTOPBP    = 0x16;
        private const byte OP_STORESTATE = 0x17;
        private const byte OP_NOP        = 0x18;
        private const byte OP_SAVEBP     = 0x1A;
        private const byte OP_RESTOREBP  = 0x1B;

        // Sub-type constants
        private const byte TYPE_INT    = 0x03;
        private const byte TYPE_FLOAT  = 0x04;
        private const byte TYPE_STRING = 0x05;
        private const byte TYPE_OBJECT = 0x06;

        // NCS file header
        private const string NCS_HEADER = "NCS V1.0";

        // Max instructions per execution to prevent infinite loops
        private const int MAX_INSTRUCTIONS = 100000;

        // ── EXECUTE ───────────────────────────────────────────────────────────
        /// <summary>
        /// Execute a compiled NCS script byte array.
        /// Returns true if execution completed normally, false on error.
        /// </summary>
        public static bool Execute(byte[] ncs, ScriptContext ctx, out object returnValue)
        {
            returnValue = null;
            if (ncs == null || ncs.Length < 13)
            {
                Debug.LogWarning("[NcsInterpreter] NCS data too short.");
                return false;
            }

            // Verify header "NCS V1.0"
            string header = System.Text.Encoding.ASCII.GetString(ncs, 0, 8);
            if (!header.StartsWith("NCS "))
            {
                Debug.LogWarning($"[NcsInterpreter] Invalid NCS header: {header}");
                return false;
            }

            // Byte 8 = file type byte ('B' = bytecode), bytes 9-12 = script size (big-endian)
            int scriptSize = (ncs[9] << 24) | (ncs[10] << 16) | (ncs[11] << 8) | ncs[12];

            var stack  = new NcsStack();
            var callStack = new Stack<int>();     // mirrors CVirtualMachineStack return addresses
            int pc     = 13;                      // program counter — starts after 13-byte header
            int bp     = 0;                       // base pointer (SAVEBP / RESTOREBP)
            int steps  = 0;

            while (pc < ncs.Length && steps < MAX_INSTRUCTIONS)
            {
                steps++;
                byte opcode  = ncs[pc++];
                if (pc >= ncs.Length && opcode != OP_RETN) break;
                byte subtype = (pc < ncs.Length) ? ncs[pc] : (byte)0;

                switch (opcode)
                {
                    // ── NOP ───────────────────────────────────────────────────
                    case OP_NOP:
                        pc++; // consume subtype byte
                        break;

                    // ── RSADD: reserve a zero-initialised slot ────────────────
                    case OP_RSADD:
                        pc++;
                        switch (subtype)
                        {
                            case TYPE_INT:    stack.PushInt(0);     break;
                            case TYPE_FLOAT:  stack.PushFloat(0f);  break;
                            case TYPE_STRING: stack.PushString(""); break;
                            case TYPE_OBJECT: stack.PushObject(null); break;
                        }
                        break;

                    // ── CONST: push literal ───────────────────────────────────
                    case OP_CONST:
                        pc++;
                        switch (subtype)
                        {
                            case TYPE_INT:
                                stack.PushInt(ReadInt32BE(ncs, ref pc));
                                break;
                            case TYPE_FLOAT:
                                stack.PushFloat(ReadFloat32BE(ncs, ref pc));
                                break;
                            case TYPE_STRING:
                            {
                                int len = ReadInt16BE(ncs, ref pc);
                                string s = System.Text.Encoding.UTF8.GetString(ncs, pc, len);
                                pc += len;
                                stack.PushString(s);
                                break;
                            }
                            case TYPE_OBJECT:
                                int objId = ReadInt32BE(ncs, ref pc);
                                // 0 = OBJECT_SELF, 1 = OBJECT_INVALID
                                stack.PushObject(objId == 0 ? ctx.Self : null);
                                break;
                        }
                        break;

                    // ── CPTOPSP: copy from SP-relative offset ─────────────────
                    case OP_CPTOPSP:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int size   = ReadInt16BE(ncs, ref pc);
                        stack.CopyTop(offset, size);
                        break;
                    }

                    // ── CPDOWNSP: copy-down assignment ────────────────────────
                    case OP_CPDOWNSP:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int size   = ReadInt16BE(ncs, ref pc);
                        stack.CopyDown(offset, size);
                        break;
                    }

                    // ── CPTOPBP / CPDOWNBP: BP-relative versions ──────────────
                    case OP_CPTOPBP:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int size   = ReadInt16BE(ncs, ref pc);
                        stack.CopyTopBP(bp, offset, size);
                        break;
                    }
                    case OP_CPDOWNBP:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int size   = ReadInt16BE(ncs, ref pc);
                        stack.CopyDownBP(bp, offset, size);
                        break;
                    }

                    // ── JMP / JSR / JZ / JNZ / RETN ─────────────────────────
                    case OP_JMP:
                        pc++;
                        pc += ReadInt32BE(ncs, ref pc) - 6; // offset is relative to opcode start
                        break;

                    case OP_JSR:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        callStack.Push(pc);               // push return address
                        pc = (pc - 6) + offset;           // jump
                        break;
                    }

                    case OP_JZ:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int top    = stack.PopInt();
                        if (top == 0) pc = (pc - 6) + offset;
                        break;
                    }

                    case OP_JNZ:
                    {
                        pc++;
                        int offset = ReadInt32BE(ncs, ref pc);
                        int top    = stack.PopInt();
                        if (top != 0) pc = (pc - 6) + offset;
                        break;
                    }

                    case OP_RETN:
                        if (callStack.Count == 0)
                        {
                            // Main function returning — capture return value if any
                            if (stack.Count > 0) returnValue = stack.Peek();
                            goto Done;
                        }
                        pc = callStack.Pop();
                        break;

                    // ── SAVEBP / RESTOREBP ────────────────────────────────────
                    case OP_SAVEBP:
                        pc++;
                        stack.PushInt(bp);
                        bp = stack.Count;
                        break;

                    case OP_RESTOREBP:
                        pc++;
                        bp = stack.PopInt();
                        break;

                    // ── LOGICAL AND / OR ──────────────────────────────────────
                    case OP_LOGANDII:
                        pc++;
                        { int b2 = stack.PopInt(); int a2 = stack.PopInt();
                          stack.PushInt((a2 != 0 && b2 != 0) ? 1 : 0); }
                        break;

                    case OP_LOGORII:
                        pc++;
                        { int b2 = stack.PopInt(); int a2 = stack.PopInt();
                          stack.PushInt((a2 != 0 || b2 != 0) ? 1 : 0); }
                        break;

                    case OP_NOTI:
                        pc++;
                        stack.PushInt(stack.PopInt() == 0 ? 1 : 0);
                        break;

                    // ── INC / DEC SP-relative ─────────────────────────────────
                    case OP_INCOSP:
                    case OP_INCISP:
                        pc++;
                        { int off = ReadInt32BE(ncs, ref pc);
                          stack.IncrementAt(off); }
                        break;

                    case OP_DECOSP:
                    case OP_DECISP:
                        pc++;
                        { int off = ReadInt32BE(ncs, ref pc);
                          stack.DecrementAt(off); }
                        break;

                    // ── INC / DEC BP-relative ─────────────────────────────────
                    case OP_INCIBP:
                        pc++;
                        { int off = ReadInt32BE(ncs, ref pc);
                          stack.IncrementAtBP(bp, off); }
                        break;

                    case OP_DECIBP:
                        pc++;
                        { int off = ReadInt32BE(ncs, ref pc);
                          stack.DecrementAtBP(bp, off); }
                        break;

                    // ── DESTRUCT ──────────────────────────────────────────────
                    case OP_DESTRUCT:
                    {
                        pc++;
                        int totalSize  = ReadInt16BE(ncs, ref pc);
                        int keepOffset = ReadInt16BE(ncs, ref pc);
                        int keepSize   = ReadInt16BE(ncs, ref pc);
                        stack.Destruct(totalSize, keepOffset, keepSize);
                        break;
                    }

                    // ── STORESTATE ────────────────────────────────────────────
                    case OP_STORESTATE:
                        // Delayed-action script state save — advance past operands
                        pc++;
                        pc += 8; // two int32 operands
                        break;

                    // ── ACTION: call engine function ──────────────────────────
                    case OP_ACTION:
                    {
                        pc++;
                        int funcIndex  = ReadInt16BE(ncs, ref pc);
                        int paramCount = ncs[pc++];
                        DispatchEngineCall(funcIndex, paramCount, stack, ctx);
                        break;
                    }

                    // ── Arithmetic / comparison sub-opcodes ───────────────────
                    // NWScript uses a single opcode byte (0x1E–0x3E range) with a
                    // sub-type byte that encodes the operand types. We handle the
                    // most common variants (II = int×int, FF = float×float, etc.).
                    //
                    // Reference: nwn-devbase opcode table
                    //   0x1E ADDII  0x1F ADDIF  0x20 ADDFI  0x21 ADDFF
                    //   0x22 ADDSS  0x23 ADDVV  0x25 SUBII  0x26 SUBFF
                    //   0x27 SUBVV  0x29 MULII  0x2A MULFF  0x2C DIVII
                    //   0x2D DIVFF  0x30 MODII  0x32 NEGF   0x33 NEGI
                    //   0x36 EQII   0x37 EQFF   0x38 EQSS   0x39 EQOO
                    //   0x3A NEQII  0x3B NEQFF  0x3E GEQII  0x3F GEQFF
                    //   0x40 GTII   0x41 GTFF   0x42 LTII   0x43 LTFF
                    //   0x44 LEQII  0x45 LEQFF

                    // ── ADD ──────────────────────────────────────────────────
                    case 0x1E: // ADDII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a + b); }
                        break;
                    case 0x1F: // ADDIF
                        pc++;
                        { float b = stack.PopFloat(); int a = stack.PopInt();
                          stack.PushFloat(a + b); }
                        break;
                    case 0x20: // ADDFI
                        pc++;
                        { int b = stack.PopInt(); float a = stack.PopFloat();
                          stack.PushFloat(a + b); }
                        break;
                    case 0x21: // ADDFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushFloat(a + b); }
                        break;
                    case 0x22: // ADDSS (string concat)
                        pc++;
                        { string b = stack.PopString(); string a = stack.PopString();
                          stack.PushString(a + b); }
                        break;

                    // ── SUB ──────────────────────────────────────────────────
                    case 0x25: // SUBII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a - b); }
                        break;
                    case 0x26: // SUBFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushFloat(a - b); }
                        break;

                    // ── MUL ──────────────────────────────────────────────────
                    case 0x29: // MULII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a * b); }
                        break;
                    case 0x2A: // MULFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushFloat(a * b); }
                        break;
                    case 0x2B: // MULIF
                        pc++;
                        { float b = stack.PopFloat(); int a = stack.PopInt();
                          stack.PushFloat(a * b); }
                        break;
                    case 0x2C: // MULFI
                        pc++;
                        { int b = stack.PopInt(); float a = stack.PopFloat();
                          stack.PushFloat(a * b); }
                        break;

                    // ── DIV ──────────────────────────────────────────────────
                    case 0x2D: // DIVII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(b != 0 ? a / b : 0); }
                        break;
                    case 0x2E: // DIVFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushFloat(b != 0f ? a / b : 0f); }
                        break;

                    // ── MOD / NEG ─────────────────────────────────────────────
                    case 0x30: // MODII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(b != 0 ? a % b : 0); }
                        break;
                    case 0x32: // NEGF
                        pc++;
                        stack.PushFloat(-stack.PopFloat());
                        break;
                    case 0x33: // NEGI
                        pc++;
                        stack.PushInt(-stack.PopInt());
                        break;

                    // ── BITWISE ───────────────────────────────────────────────
                    case 0x34: // COMP (bitwise NOT)
                        pc++;
                        stack.PushInt(~stack.PopInt());
                        break;
                    case 0x35: // SHLEFTII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a << b); }
                        break;
                    case 0x36: // SHRIGHTII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a >> b); }
                        break;
                    case 0x37: // USHRIGHTII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt((int)((uint)a >> b)); }
                        break;
                    case 0x38: // BOOLANDII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a & b); }
                        break;
                    case 0x39: // BOOLORRII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a | b); }
                        break;
                    case 0x3A: // BOOLXORII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a ^ b); }
                        break;

                    // ── EQUALITY ─────────────────────────────────────────────
                    case 0x3B: // EQII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a == b ? 1 : 0); }
                        break;
                    case 0x3C: // EQFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(Mathf.Approximately(a, b) ? 1 : 0); }
                        break;
                    case 0x3D: // EQSS
                        pc++;
                        { string b = stack.PopString(); string a = stack.PopString();
                          stack.PushInt(string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? 1 : 0); }
                        break;
                    case 0x3E: // EQOO
                        pc++;
                        { var b = stack.PopObject(); var a = stack.PopObject();
                          stack.PushInt(ReferenceEquals(a, b) ? 1 : 0); }
                        break;

                    // ── INEQUALITY ────────────────────────────────────────────
                    case 0x3F: // NEQII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a != b ? 1 : 0); }
                        break;
                    case 0x40: // NEQFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(!Mathf.Approximately(a, b) ? 1 : 0); }
                        break;
                    case 0x41: // NEQSS
                        pc++;
                        { string b = stack.PopString(); string a = stack.PopString();
                          stack.PushInt(!string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ? 1 : 0); }
                        break;

                    // ── RELATIONAL ────────────────────────────────────────────
                    case 0x42: // GEQII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a >= b ? 1 : 0); }
                        break;
                    case 0x43: // GEQFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(a >= b ? 1 : 0); }
                        break;
                    case 0x44: // GTII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a > b ? 1 : 0); }
                        break;
                    case 0x45: // GTFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(a > b ? 1 : 0); }
                        break;
                    case 0x46: // LTII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a < b ? 1 : 0); }
                        break;
                    case 0x47: // LTFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(a < b ? 1 : 0); }
                        break;
                    case 0x48: // LEQII
                        pc++;
                        { int b = stack.PopInt(); int a = stack.PopInt();
                          stack.PushInt(a <= b ? 1 : 0); }
                        break;
                    case 0x49: // LEQFF
                        pc++;
                        { float b = stack.PopFloat(); float a = stack.PopFloat();
                          stack.PushInt(a <= b ? 1 : 0); }
                        break;

                    default:
                        // Unknown / unimplemented opcode — advance one byte and continue
                        pc++;
                        break;
                }
            }
            Done:
            return true;
        }

        // ── ENGINE CALL DISPATCH ─────────────────────────────────────────────
        // Maps engine-function indices to NWScriptVM handlers.
        // Indices match the NWScript action constants in nwscript.nss.
        private static void DispatchEngineCall(int index, int paramCount,
                                               NcsStack stack, ScriptContext ctx)
        {
            // Pop parameters into an array (last param is top-of-stack)
            object[] parms = new object[paramCount];
            for (int i = paramCount - 1; i >= 0; i--)
                parms[i] = stack.Count > 0 ? stack.PopObject() : null;

            object result = NWScriptVM.DispatchAction(index, parms, ctx);
            if (result != null)
            {
                // Push return value
                switch (result)
                {
                    case int    iv: stack.PushInt(iv);    break;
                    case float  fv: stack.PushFloat(fv);  break;
                    case string sv: stack.PushString(sv); break;
                    default:        stack.PushObject(result as GameObject); break;
                }
            }
        }

        // ── BINARY HELPERS (big-endian, NCS format) ──────────────────────────
        private static int ReadInt32BE(byte[] data, ref int pos)
        {
            int v = (data[pos] << 24) | (data[pos+1] << 16) | (data[pos+2] << 8) | data[pos+3];
            pos += 4;
            return v;
        }
        private static float ReadFloat32BE(byte[] data, ref int pos)
        {
            // NCS stores floats big-endian; BitConverter on little-endian systems needs reversal
            byte[] bytes = { data[pos+3], data[pos+2], data[pos+1], data[pos] };
            pos += 4;
            return BitConverter.ToSingle(bytes, 0);
        }
        private static int ReadInt16BE(byte[] data, ref int pos)
        {
            int v = (data[pos] << 8) | data[pos+1];
            pos += 2;
            return v;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NCS STACK  —  mirrors CVirtualMachineStack
    //  Stores typed values; the original engine uses a flat byte buffer,
    //  we use a typed List<object> which is equivalent for correctness.
    // ══════════════════════════════════════════════════════════════════════════
    public class NcsStack
    {
        private readonly List<object> _data = new List<object>();

        public int Count => _data.Count;

        public void PushInt(int v)          => _data.Add(v);
        public void PushFloat(float v)      => _data.Add(v);
        public void PushString(string v)    => _data.Add(v);
        public void PushObject(GameObject v)=> _data.Add((object)v);

        public int    PopInt()    { var v = _data[_data.Count - 1]; _data.RemoveAt(_data.Count - 1); return v is int i ? i : 0; }
        public float  PopFloat()  { var v = _data[_data.Count - 1]; _data.RemoveAt(_data.Count - 1); return v is float f ? f : 0f; }
        public string PopString() { var v = _data[_data.Count - 1]; _data.RemoveAt(_data.Count - 1); return v as string ?? ""; }
        public object PopObject() { var v = _data[_data.Count - 1]; _data.RemoveAt(_data.Count - 1); return v; }
        public object Peek()      => _data.Count > 0 ? _data[_data.Count - 1] : null;

        // SP-relative index: offset is negative (e.g. -4 = one int below top)
        private int SpIndex(int offset) => _data.Count + (offset / 4) - 1;
        private int BpIndex(int bp, int offset) => bp + (offset / 4) - 1;

        public void CopyTop(int offset, int size)
        {
            int idx = SpIndex(offset);
            if (idx >= 0 && idx < _data.Count)
                _data.Add(_data[idx]);
        }
        public void CopyDown(int offset, int size)
        {
            int idx = SpIndex(offset);
            if (idx >= 0 && idx < _data.Count && _data.Count > 0)
                _data[idx] = _data[_data.Count - 1];
        }
        public void CopyTopBP(int bp, int offset, int size)
        {
            int idx = BpIndex(bp, offset);
            if (idx >= 0 && idx < _data.Count)
                _data.Add(_data[idx]);
        }
        public void CopyDownBP(int bp, int offset, int size)
        {
            int idx = BpIndex(bp, offset);
            if (idx >= 0 && idx < _data.Count && _data.Count > 0)
                _data[idx] = _data[_data.Count - 1];
        }

        public void IncrementAt(int offset)
        {
            int idx = SpIndex(offset);
            if (idx >= 0 && idx < _data.Count && _data[idx] is int iv)
                _data[idx] = iv + 1;
        }
        public void DecrementAt(int offset)
        {
            int idx = SpIndex(offset);
            if (idx >= 0 && idx < _data.Count && _data[idx] is int iv)
                _data[idx] = iv - 1;
        }
        public void IncrementAtBP(int bp, int offset)
        {
            int idx = BpIndex(bp, offset);
            if (idx >= 0 && idx < _data.Count && _data[idx] is int iv)
                _data[idx] = iv + 1;
        }
        public void DecrementAtBP(int bp, int offset)
        {
            int idx = BpIndex(bp, offset);
            if (idx >= 0 && idx < _data.Count && _data[idx] is int iv)
                _data[idx] = iv - 1;
        }

        public void Destruct(int totalSize, int keepOffset, int keepSize)
        {
            // Keep the 'keepSize' bytes at 'keepOffset' from the bottom of the block,
            // discard everything else in the block
            int total  = totalSize  / 4;
            int keep   = keepSize   / 4;
            int keepOff= keepOffset / 4;
            int startIdx = _data.Count - total;
            if (startIdx < 0) return;

            var kept = new List<object>();
            for (int i = keepOff; i < keepOff + keep && startIdx + i < _data.Count; i++)
                kept.Add(_data[startIdx + i]);

            _data.RemoveRange(startIdx, Math.Min(total, _data.Count - startIdx));
            _data.AddRange(kept);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  NCS RUNNER  —  updated to use real interpreter
    // ══════════════════════════════════════════════════════════════════════════
    public static class NcsRunner
    {
        public static bool TryRun(string scriptName, ScriptContext ctx)
        {
            byte[] ncsData = SceneBootstrapper.Resources?.GetResource(scriptName,
                KotORUnity.KotOR.FileReaders.ResourceType.NCS);

            if (ncsData == null) return false;

            bool ok = NcsInterpreter.Execute(ncsData, ctx, out object ret);
            if (ok)
                Debug.Log($"[NcsRunner] Executed '{scriptName}' ({ncsData.Length} bytes) " +
                          $"ret={ret ?? "void"}");
            else
                Debug.LogWarning($"[NcsRunner] Execution failed for '{scriptName}'");
            return ok;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GLOBAL VARIABLES  —  mirrors NWScript global variable store
    // ══════════════════════════════════════════════════════════════════════════
    public static class GlobalVars
    {
        private static readonly Dictionary<string, bool>   _bools
            = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int>    _ints
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float>  _floats
            = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _strings
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool   GetBool(string k)            => _bools.TryGetValue(k, out var v) ? v : false;
        public static void   SetBool(string k, bool v)    => _bools[k] = v;
        public static int    GetInt(string k)              => _ints.TryGetValue(k, out var v) ? v : 0;
        public static void   SetInt(string k, int v)       => _ints[k] = v;
        public static float  GetFloat(string k)            => _floats.TryGetValue(k, out var v) ? v : 0f;
        public static void   SetFloat(string k, float v)   => _floats[k] = v;
        public static string GetString(string k)           => _strings.TryGetValue(k, out var v) ? v : "";
        public static void   SetString(string k, string v) => _strings[k] = v;

        public static void Clear()
        {
            _bools.Clear(); _ints.Clear(); _floats.Clear(); _strings.Clear();
        }

        // Serialisation for SaveManager (array-based for JsonUtility compatibility)
        public static SaveSystem.GlobalVarsSaveData GetSaveData()
        {
            var data = new SaveSystem.GlobalVarsSaveData();
            var boolKeys = new System.Collections.Generic.List<string>();
            var boolVals = new System.Collections.Generic.List<bool>();
            foreach (var kv in _bools) { boolKeys.Add(kv.Key); boolVals.Add(kv.Value); }
            data.boolKeys   = boolKeys.ToArray();
            data.boolValues = boolVals.ToArray();

            var intKeys = new System.Collections.Generic.List<string>();
            var intVals = new System.Collections.Generic.List<int>();
            foreach (var kv in _ints) { intKeys.Add(kv.Key); intVals.Add(kv.Value); }
            data.intKeys   = intKeys.ToArray();
            data.intValues = intVals.ToArray();

            var fKeys = new System.Collections.Generic.List<string>();
            var fVals = new System.Collections.Generic.List<float>();
            foreach (var kv in _floats) { fKeys.Add(kv.Key); fVals.Add(kv.Value); }
            data.floatKeys   = fKeys.ToArray();
            data.floatValues = fVals.ToArray();

            var sKeys = new System.Collections.Generic.List<string>();
            var sVals = new System.Collections.Generic.List<string>();
            foreach (var kv in _strings) { sKeys.Add(kv.Key); sVals.Add(kv.Value); }
            data.stringKeys   = sKeys.ToArray();
            data.stringValues = sVals.ToArray();
            return data;
        }

        public static void RestoreFromSave(SaveSystem.GlobalVarsSaveData data)
        {
            Clear();
            if (data == null) return;
            if (data.boolKeys != null)
                for (int i = 0; i < data.boolKeys.Length && i < data.boolValues?.Length; i++)
                    _bools[data.boolKeys[i]] = data.boolValues[i];
            if (data.intKeys != null)
                for (int i = 0; i < data.intKeys.Length && i < data.intValues?.Length; i++)
                    _ints[data.intKeys[i]] = data.intValues[i];
            if (data.floatKeys != null)
                for (int i = 0; i < data.floatKeys.Length && i < data.floatValues?.Length; i++)
                    _floats[data.floatKeys[i]] = data.floatValues[i];
            if (data.stringKeys != null)
                for (int i = 0; i < data.stringKeys.Length && i < data.stringValues?.Length; i++)
                    _strings[data.stringKeys[i]] = data.stringValues[i];
        }

    }

    // GlobalVarsSaveData is defined in KotORUnity.SaveSystem (SaveManager.cs).
    // GetSaveData() / RestoreFromSave() use SaveSystem.GlobalVarsSaveData directly.
    // LoadSaveData() stub (old Dictionary-based shape) was removed — use RestoreFromSave().

    // ══════════════════════════════════════════════════════════════════════════
    //  LOCAL VARIABLES  —  per-GameObject NWScript local variable store
    //  Keys are stored as "objectName::variableName" inside GlobalVars for
    //  simplicity, matching BioWare's per-object local variable semantics.
    // ══════════════════════════════════════════════════════════════════════════
    public static class LocalVars
    {
        private static string K(UnityEngine.GameObject go, string k)
            => $"{go?.GetInstanceID().ToString() ?? "null"}::{k}";

        public static int    GetInt(UnityEngine.GameObject go, string k)        => GlobalVars.GetInt(K(go,k));
        public static void   SetInt(UnityEngine.GameObject go, string k, int v) => GlobalVars.SetInt(K(go,k), v);

        public static float  GetFloat(UnityEngine.GameObject go, string k)          => GlobalVars.GetFloat(K(go,k));
        public static void   SetFloat(UnityEngine.GameObject go, string k, float v) => GlobalVars.SetFloat(K(go,k), v);

        public static string GetString(UnityEngine.GameObject go, string k)           => GlobalVars.GetString(K(go,k));
        public static void   SetString(UnityEngine.GameObject go, string k, string v) => GlobalVars.SetString(K(go,k), v);

        public static bool   GetBool(UnityEngine.GameObject go, string k)         => GlobalVars.GetBool(K(go,k));
        public static void   SetBool(UnityEngine.GameObject go, string k, bool v) => GlobalVars.SetBool(K(go,k), v);

        public static void ClearObject(UnityEngine.GameObject go)
        {
            // Nothing to do — keys are namespaced; GlobalVars.Clear() wipes all
        }
    }
}
