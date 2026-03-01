using UnityEngine;
using KotORUnity.Player;

namespace KotORUnity.Player
{
    /// <summary>
    /// MonoBehaviour wrapper for the PlayerStats data class.
    /// Attach to any character that needs stats (player, companion, enemy).
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

        private void OnCharacterDied()
        {
            Debug.Log($"[{characterName}] has died.");
            // Additional death handling (ragdoll, etc.) goes here
        }

        public void ApplyDamage(float amount, Core.GameEnums.DamageType type)
        {
            Stats?.TakeDamage(amount, type);
        }
    }
}
