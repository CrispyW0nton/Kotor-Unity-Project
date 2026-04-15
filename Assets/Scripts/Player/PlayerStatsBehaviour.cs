using UnityEngine;
using KotORUnity.Player;
using KotORUnity.Core;
using KotORUnity.UI;

namespace KotORUnity.Player
{
    /// <summary>
    /// MonoBehaviour wrapper for the PlayerStats data class.
    /// Attach to any character that needs stats (player, companion, enemy).
    ///
    /// On Start (player tag only):
    ///   1. Calls GameStarter.ApplyPendingConfig() to consume the NewGameConfig
    ///      stashed by CharacterCreation, applying the chosen name/attributes.
    ///   2. Publishes UIHUDRefresh so HUDManager shows correct initial values.
    /// </summary>
    public class PlayerStatsBehaviour : MonoBehaviour
    {
        [SerializeField] private string characterName = "Character";
        [SerializeField] private int startingLevel = 1;

        public PlayerStats Stats { get; private set; }

        private void Awake()
        {
            Stats = new PlayerStats(characterName, startingLevel);
            Stats.OnDied += OnCharacterDied;
        }

        private void Start()
        {
            // Only the Player-tagged object should consume the pending new-game config.
            // Companions/enemies that also use PlayerStatsBehaviour skip this.
            if (CompareTag("Player"))
            {
                // Consumes GameStarter.PendingConfig (if any) and applies it.
                // If no config exists (loaded save game), this is a no-op.
                GameStarter.ApplyPendingConfig(this);

                // Tell HUDManager to refresh bars with the (possibly updated) stats.
                EventBus.Publish(EventBus.EventType.UIHUDRefresh);
            }
        }

        private void OnCharacterDied()
        {
            Debug.Log($"[{characterName}] has died.");

            // Only publish PlayerDied for the actual player-tagged object.
            // Companions and enemies should subscribe to EntityKilled via DamageSystem instead.
            if (CompareTag("Player"))
                Core.EventBus.Publish(Core.EventBus.EventType.PlayerDied);

            // Additional death handling (ragdoll, animator triggers, etc.) goes here
        }

        public void ApplyDamage(float amount, Core.GameEnums.DamageType type)
        {
            Stats?.TakeDamage(amount, type);
        }

        /// <summary>
        /// Apply a NewGameConfig (from CharacterCreation) to this player's stats.
        /// Called once at the start of a new game via GameStarter.ApplyPendingConfig().
        /// </summary>
        public void ApplyNewGameConfig(NewGameConfig config)
        {
            if (config == null) return;

            characterName = config.PlayerName;

            // Re-initialize Stats with the new name and default level 1
            Stats = new PlayerStats(characterName, 1);
            Stats.OnDied += OnCharacterDied;

            // Apply attribute modifiers from point-buy
            // PlayerStats exposes base stat fields directly
            Stats.Strength     = config.Attributes.Strength;
            Stats.Dexterity    = config.Attributes.Dexterity;
            Stats.Constitution = config.Attributes.Constitution;
            Stats.Intelligence = config.Attributes.Intelligence;
            Stats.Wisdom       = config.Attributes.Wisdom;
            Stats.Charisma     = config.Attributes.Charisma;

            // Recalculate derived stats (HP, FP) now that Constitution is set
            Stats.RecalculateDerivedStats();

            Debug.Log($"[PlayerStatsBehaviour] Applied new-game config: " +
                      $"'{characterName}' STR={Stats.Strength} DEX={Stats.Dexterity} " +
                      $"CON={Stats.Constitution} HP={Stats.MaxHP}");
        }
    }
}
