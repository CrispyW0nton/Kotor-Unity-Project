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
            GameResumed
        }

        // ── BASE EVENT ARGS ────────────────────────────────────────────────────
        public class GameEventArgs : EventArgs { }

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
            public string ModuleName { get; }
            public ModuleEventArgs(string name) { ModuleName = name; }
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
