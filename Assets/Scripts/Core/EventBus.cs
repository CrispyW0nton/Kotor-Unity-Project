using System;
using System.Collections.Generic;
using UnityEngine;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Core
{
    /// <summary>
    /// Central event bus for decoupled inter-system communication.
    /// Systems publish events here; other systems subscribe without
    /// needing direct references to each other.
    /// 
    /// Usage:
    ///   EventBus.Subscribe(EventBus.EventType.ModeSwitch, OnModeSwitch);
    ///   EventBus.Publish(EventBus.EventType.ModeSwitch, new ModeSwitchEventArgs(GameMode.RTS));
    ///   EventBus.Unsubscribe(EventBus.EventType.ModeSwitch, OnModeSwitch);
    /// </summary>
    public static class EventBus
    {
        // ── EVENT TYPES ────────────────────────────────────────────────────────
        public enum EventType
        {
            // Mode
            ModeSwitch,
            ModeTransitionStarted,
            ModeTransitionCompleted,

            // Combat
            EntityDamaged,
            EntityKilled,
            AbilityUsed,
            AbilityQueued,
            AbilityDequeued,
            CombatStarted,
            CombatEnded,
            EncounterStarted,
            EncounterCompleted,

            // Player
            PlayerDamaged,
            PlayerDied,
            PlayerLevelUp,
            PlayerRespawned,

            // Companion
            CompanionOrderIssued,
            CompanionOrderCompleted,
            CompanionDied,
            FormationChanged,

            // Enemy
            EnemyDetectedPlayer,
            EnemyAdaptationStackAdded,
            EnemyKilled,

            // Progression
            ExperienceGained,
            TalentUnlocked,
            WeaponEquipped,
            ModeAffinityBonusTriggered,

            // Module / World
            ModuleLoaded,
            ModuleUnloaded,
            RoomEntered,
            DoorInteracted,
            PlaceableInteracted,

            // UI
            UIHUDRefresh,
            UICommandIssued,

            // Save
            GameSaved,
            GameLoaded,

            // Pause
            GamePaused,
            GameResumed,

            // Dialogue
            DialogueStarted,
            DialogueEnded,
            DialogueLineAdvanced,

            // Inventory
            ItemEquipped,
            ItemUnequipped,
            ItemPickedUp,
            ItemDropped,
            ItemUsed,

            // Area / World
            AreaTransitionRequested,
            AreaTransitionCompleted,

            // Combat (RTWP)
            CombatRoundStarted,
            CombatRoundEnded,
            ActionQueueChanged,

            // Progression / XP (alias used by DevConsole)
            XPAwarded,

            // Mod system
            ModsLoaded,

            // Dev tools
            GodModeChanged,
            HUDVisibilityChanged,

            // Achievements + Codex
            AchievementUnlocked,
            CodexEntryDiscovered,

            // Force / Alignment
            AlignmentChanged,
            ForcePowerActivated,
            ForcePointsChanged,

            // Items / Loot
            LootDropped,
            LootCollected,

            // Merchant
            ItemPurchased,
            ItemSold,

            // Interaction
            WorkbenchOpened,
            ContainerOpened,
            DoorOpened,
            DoorLocked,
            NPCInteracted,

            // Area
            NavMeshBaked,
            SpawnPointReached,

            // Dialogue (additional – DialogueStarted/Ended declared in UI section above)
            DialogueReplyChosen
        }

        // ── BASE EVENT ARGS ────────────────────────────────────────────────────
        public class GameEventArgs : EventArgs
        {
            // Generic payload fields — not all events use all fields
            public int    IntValue    = 0;
            public float  FloatValue  = 0f;
            public bool   BoolValue   = false;
            public string StringValue = "";
        }

        // ── SPECIFIC EVENT ARGS ────────────────────────────────────────────────

        public class ModeSwitchEventArgs : GameEventArgs
        {
            public GameMode PreviousMode { get; }
            public GameMode NewMode { get; }
            public ModeSwitchEventArgs(GameMode prev, GameMode next)
            {
                PreviousMode = prev;
                NewMode = next;
            }
        }

        public class DamageEventArgs : GameEventArgs
        {
            public GameObject Source { get; }
            public GameObject Target { get; }
            public float Amount { get; }
            public DamageType Type { get; }
            public HitType HitType { get; }
            public DamageEventArgs(GameObject src, GameObject tgt, float amt, DamageType dtype, HitType htype)
            {
                Source = src; Target = tgt; Amount = amt; Type = dtype; HitType = htype;
            }
        }

        public class ExperienceEventArgs : GameEventArgs
        {
            public float Amount { get; }
            public string Source { get; }
            public bool AffinityBonusApplied { get; }
            public ExperienceEventArgs(float amount, string source, bool affinityBonus)
            {
                Amount = amount; Source = source; AffinityBonusApplied = affinityBonus;
            }
        }

        public class EncounterEventArgs : GameEventArgs
        {
            public EncounterType EncounterType { get; }
            public int EnemyCount { get; }
            public EncounterEventArgs(EncounterType type, int count)
            {
                EncounterType = type; EnemyCount = count;
            }
        }

        public class ModuleEventArgs : GameEventArgs
        {
            public string ModuleName  { get; }
            public string WaypointTag { get; }
            public ModuleEventArgs(string name) { ModuleName = name; WaypointTag = ""; }
            public ModuleEventArgs(string name, string waypoint) { ModuleName = name; WaypointTag = waypoint; }
        }

        public class AbilityEventArgs : GameEventArgs
        {
            public GameObject Caster { get; }
            public GameObject Target { get; }
            public string AbilityName { get; }
            public GameMode ExecutionMode { get; }
            public AbilityEventArgs(GameObject caster, GameObject target, string abilityName, GameMode mode)
            {
                Caster = caster; Target = target; AbilityName = abilityName; ExecutionMode = mode;
            }
        }

        /// <summary>Published when an achievement is unlocked.</summary>
        public class AchievementEventArgs : GameEventArgs
        {
            public string AchievementId    { get; }
            public string AchievementTitle { get; }
            public int    PointValue       { get; }
            public AchievementEventArgs(string id, string title, int pts)
            { AchievementId = id; AchievementTitle = title; PointValue = pts; }
        }

        /// <summary>Published when a Codex entry is first discovered.</summary>
        public class CodexEventArgs : GameEventArgs
        {
            public string EntryId    { get; }
            public string EntryTitle { get; }
            public string Category   { get; }
            public CodexEventArgs(string id, string title, string cat)
            { EntryId = id; EntryTitle = title; Category = cat; }
        }

        /// <summary>Published when Force alignment changes.</summary>
        public class ForcePowerEventArgs : GameEventArgs
        {
            public int   Alignment      { get; }  // -100 dark .. +100 light
            public float ForceCurrent   { get; }
            public float ForceMax       { get; }
            public string PowerName     { get; }
            public ForcePowerEventArgs(int align, float fp, float maxFp, string power = "")
            { Alignment = align; ForceCurrent = fp; ForceMax = maxFp; PowerName = power; }
        }

        /// <summary>Published when the player's alignment shifts (light/dark).</summary>
        public class AlignmentEventArgs : GameEventArgs
        {
            public int NewAlignment  { get; }  // -100 (dark) … +100 (light)
            public int Delta         { get; }  // positive = more light, negative = more dark
            public AlignmentEventArgs(int newAlign, int delta) { NewAlignment = newAlign; Delta = delta; }
        }

        /// <summary>Published when an item is picked up (looted or purchased).</summary>
        public class ItemEventArgs : GameEventArgs
        {
            public string ResRef   { get; }
            public string ItemName { get; }
            public int    Credits  { get; }    // price paid / received (0 if looted)
            public ItemEventArgs(string resRef, string name, int credits = 0)
            { ResRef = resRef; ItemName = name; Credits = credits; }
        }

        /// <summary>Published when a loot item is dropped or collected.</summary>
        public class LootEventArgs : GameEventArgs
        {
            public string ItemResRef { get; }
            public int    Quantity   { get; }
            public Vector3 WorldPos  { get; }
            public LootEventArgs(string resRef, int qty, Vector3 pos)
            { ItemResRef = resRef; Quantity = qty; WorldPos = pos; }
        }

        /// <summary>Published when a merchant transaction completes.</summary>
        public class MerchantEventArgs : GameEventArgs
        {
            public string ItemResRef  { get; }
            public int    Credits     { get; }
            public bool   IsPurchase  { get; }  // true=buy, false=sell
            public MerchantEventArgs(string resRef, int credits, bool isPurchase)
            { ItemResRef = resRef; Credits = credits; IsPurchase = isPurchase; }
        }

        // ── INTERNAL REGISTRY ─────────────────────────────────────────────────
        private static readonly Dictionary<EventType, List<Action<GameEventArgs>>> _listeners
            = new Dictionary<EventType, List<Action<GameEventArgs>>>();

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>Subscribe a callback to a specific event type.</summary>
        public static void Subscribe(EventType eventType, Action<GameEventArgs> callback)
        {
            if (!_listeners.ContainsKey(eventType))
                _listeners[eventType] = new List<Action<GameEventArgs>>();

            if (!_listeners[eventType].Contains(callback))
                _listeners[eventType].Add(callback);
        }

        /// <summary>Unsubscribe a callback from a specific event type.</summary>
        public static void Unsubscribe(EventType eventType, Action<GameEventArgs> callback)
        {
            if (_listeners.ContainsKey(eventType))
                _listeners[eventType].Remove(callback);
        }

        /// <summary>
        /// Publish an event to all subscribers.
        /// Pass null for args if the event carries no data.
        /// </summary>
        public static void Publish(EventType eventType, GameEventArgs args = null)
        {
            if (!_listeners.ContainsKey(eventType)) return;

            // Iterate a copy in case listeners modify the list during dispatch
            var callbacks = new List<Action<GameEventArgs>>(_listeners[eventType]);
            foreach (var callback in callbacks)
            {
                try
                {
                    callback?.Invoke(args ?? new GameEventArgs());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] Exception in listener for {eventType}: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        /// <summary>Clear all listeners (useful in tests or on scene unload).</summary>
        public static void ClearAll()
        {
            _listeners.Clear();
        }

        /// <summary>Clear all listeners for a specific event type.</summary>
        public static void Clear(EventType eventType)
        {
            if (_listeners.ContainsKey(eventType))
                _listeners[eventType].Clear();
        }
    }
}
