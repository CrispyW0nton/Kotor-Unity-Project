using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.SaveSystem;

namespace KotORUnity.UI
{
    /// <summary>
    /// Bridges the CharacterCreation scene to the Game scene.
    ///
    /// Add this component to a GameObject in the CharacterCreation scene.
    /// It subscribes to CharacterCreationController.OnCreationComplete, stores
    /// the chosen config in a static slot (survives scene load), then loads the
    /// Game scene.  When the Game scene starts, PlayerStatsBehaviour.Start()
    /// calls GameStarter.ApplyPendingConfig() to push stats onto the player.
    ///
    /// Scene flow:
    ///   Boot (0) → MainMenu (1) → CharacterCreation (2) → Game (3)
    ///
    /// Inspector:
    ///   gameSceneName  — must match the scene name in Build Settings (default "Game")
    ///   fadeTime       — seconds for fade-to-black before scene load (default 0.5)
    /// </summary>
    public class GameStarter : MonoBehaviour
    {
        // ── STATIC PENDING CONFIG ─────────────────────────────────────────────
        // Stored statically so it survives the scene change from
        // CharacterCreation → Game without needing DontDestroyOnLoad.
        public static NewGameConfig PendingConfig { get; private set; }

        /// <summary>True if a new-game config is waiting to be applied.</summary>
        public static bool HasPendingConfig => PendingConfig != null;

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [SerializeField] private string    gameSceneName = "Game";
        [SerializeField] private float     fadeTime      = 0.5f;
        [SerializeField] private CanvasGroup fadeOverlay; // optional — wire a full-screen black CanvasGroup

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Start()
        {
            var charCreate = CharacterCreationController.Instance;
            if (charCreate == null)
            {
                Debug.LogError("[GameStarter] CharacterCreationController.Instance is null. " +
                    "Make sure this GameObject is in the same scene as CharacterCreationController.");
                return;
            }

            charCreate.OnCreationComplete += OnCreationComplete;
            Debug.Log("[GameStarter] Subscribed to OnCreationComplete.");
        }

        private void OnDestroy()
        {
            var charCreate = CharacterCreationController.Instance;
            if (charCreate != null)
                charCreate.OnCreationComplete -= OnCreationComplete;
        }

        // ── HANDLER ───────────────────────────────────────────────────────────
        private void OnCreationComplete(NewGameConfig config)
        {
            Debug.Log($"[GameStarter] Character confirmed: '{config.PlayerName}' " +
                      $"class={config.ClassId}  gender={config.Gender}");

            PendingConfig = config;     // stash for Game scene to consume
            StartCoroutine(FadeAndLoad());
        }

        // ── SCENE TRANSITION ─────────────────────────────────────────────────
        private IEnumerator FadeAndLoad()
        {
            // Fade to black if an overlay is wired
            if (fadeOverlay != null)
            {
                float elapsed = 0f;
                fadeOverlay.alpha = 0f;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fadeOverlay.alpha = Mathf.Clamp01(elapsed / fadeTime);
                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }
            else
            {
                yield return null;
            }

            if (SceneManager.GetSceneByName(gameSceneName).buildIndex < 0 &&
                !Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"[GameStarter] Scene '{gameSceneName}' is not in Build Settings. " +
                    "Open File → Build Settings and add the Game scene.");
                yield break;
            }

            SceneManager.LoadScene(gameSceneName);
        }

        // ── STATIC API ────────────────────────────────────────────────────────
        /// <summary>
        /// Called by the Game scene (e.g. from PlayerStatsBehaviour.Start) to
        /// consume the pending config and push it onto the player object.
        /// Clears PendingConfig after applying so it is only consumed once.
        /// </summary>
        public static void ApplyPendingConfig(PlayerStatsBehaviour playerStats)
        {
            if (PendingConfig == null)
            {
                Debug.Log("[GameStarter] No pending config — resuming existing save.");
                return;
            }

            if (playerStats == null)
            {
                Debug.LogError("[GameStarter] playerStats is null — cannot apply config.");
                return;
            }

            playerStats.ApplyNewGameConfig(PendingConfig);

            Debug.Log($"[GameStarter] Applied new-game config to player: " +
                      $"'{PendingConfig.PlayerName}' class={PendingConfig.ClassId}");

            PendingConfig = null;   // consume — don't apply twice
        }
    }
}
