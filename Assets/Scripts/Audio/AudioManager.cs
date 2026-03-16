using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.FileReaders;

namespace KotORUnity.Audio
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  AUDIO MANAGER  —  central audio dispatcher for KotOR-Unity
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Responsibilities:
    //    • Play VO lines from TLK StrRef (looks up .wav resref in dialog.tlk)
    //    • Play ambient/SFX wav files by resref
    //    • Background music play/stop/crossfade (streamed via AudioSource)
    //    • Trigger LipSyncSystem when VO is played on a speaker
    //    • Respond to NWScriptVM actions (PlayVoiceChat, MusicBackgroundPlay/Stop)
    //    • Integrate with EventBus (CombatStarted → combat music, etc.)
    //
    //  Wire-up:
    //    Attach to a persistent GO (e.g. GameManager).
    //    Assign _musicSources (2 AudioSources for crossfading).
    //    Assign _sfxSource    (1 AudioSource for SFX / VO).
    //
    //  KotOR ambient track IDs come from ambientmusic.2da (id → wav resref).

    /// <summary>Mapping from MusicBackgroundPlay integer IDs to wav resrefs.</summary>
    public static class MusicTable
    {
        // Subset of KotOR 1 ambientmusic.2da rows (id → wavresref without extension)
        private static readonly Dictionary<int, string> _table =
            new Dictionary<int, string>
        {
            {  1, "mus_area_tar_acq" },
            {  2, "mus_area_tar_ld"  },
            {  3, "mus_area_dan"     },
            {  4, "mus_area_tat"     },
            {  5, "mus_area_kas"     },
            {  6, "mus_area_man"     },
            {  7, "mus_area_kor"     },
            {  8, "mus_area_unk"     },
            { 10, "mus_combat_tar"   },
            { 11, "mus_combat_boss"  },
            { 12, "mus_combat_gen"   },
            { 20, "mus_menu_main"    },
            { 21, "mus_credits"      },
            { 30, "mus_battle_gen"   },
        };

        public static string GetResRef(int id)
        {
            _table.TryGetValue(id, out string r);
            return r;
        }
    }

    /// <summary>Mapping from VoiceChat integer IDs to a wav resref prefix.</summary>
    public static class VoiceChatTable
    {
        // KotOR VoiceChat IDs (voicechat.2da rows)
        private static readonly Dictionary<int, string> _table =
            new Dictionary<int, string>
        {
            {  1, "vc_attack"        },
            {  2, "vc_canthear"      },
            {  3, "vc_cantmove"      },
            {  4, "vc_confused"      },
            {  5, "vc_criticalatk"   },
            {  6, "vc_death"         },
            {  7, "vc_dominated"     },
            {  8, "vc_flee"          },
            {  9, "vc_glayout"       },
            { 10, "vc_groupok"       },
            { 11, "vc_guardpost"     },
            { 12, "vc_halp"          },
            { 13, "vc_help"          },
            { 14, "vc_neardeath"     },
            { 15, "vc_playerdeath"   },
            { 16, "vc_poisoned"      },
            { 17, "vc_rest"          },
            { 18, "vc_retreat"       },
            { 19, "vc_solo"          },
            { 20, "vc_spellok"       },
            { 21, "vc_success"       },
            { 22, "vc_taskfocus"     },
            { 23, "vc_taunted"       },
            { 24, "vc_toofar"        },
            { 25, "vc_weakened"      },
        };

        public static string GetResRef(int id) { _table.TryGetValue(id, out string r); return r; }
    }

    public class AudioManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static AudioManager Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Audio Sources")]
        [Tooltip("Two AudioSources used for music crossfading.")]
        [SerializeField] private AudioSource _musicA;
        [SerializeField] private AudioSource _musicB;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _voSource;

        [Header("Settings")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume   = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume    = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume      = 1f;
        [SerializeField, Range(0f, 1f)] private float _voiceVolume    = 1f;
        [SerializeField] private float _crossfadeTime = 2f;

        // ── STATE ─────────────────────────────────────────────────────────────
        private AudioSource _activeMusicSource;
        private int         _currentMusicId = -1;
        private bool        _combatMusicActive = false;
        private Coroutine   _crossfadeRoutine;

        // Cache of loaded AudioClips (keyed by resref)
        private readonly Dictionary<string, AudioClip> _clipCache =
            new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-create AudioSources if not assigned
            if (_musicA  == null) _musicA  = CreateAudioSource("MusicA",  loop: true);
            if (_musicB  == null) _musicB  = CreateAudioSource("MusicB",  loop: true);
            if (_sfxSource == null) _sfxSource = CreateAudioSource("SFX",   loop: false);
            if (_voSource  == null) _voSource  = CreateAudioSource("VO",    loop: false);

            _activeMusicSource = _musicA;
        }

        private void Start()
        {
            // Load saved volumes from PlayerPrefs (set by InGameOptionsMenu)
            _masterVolume = PlayerPrefs.GetFloat("opt_vol_master", _masterVolume);
            _musicVolume  = PlayerPrefs.GetFloat("opt_vol_music",  _musicVolume);
            _sfxVolume    = PlayerPrefs.GetFloat("opt_vol_sfx",    _sfxVolume);
            _voiceVolume  = PlayerPrefs.GetFloat("opt_vol_vo",     _voiceVolume);
            ApplyVolumes();

            EventBus.Subscribe(EventBus.EventType.CombatStarted,   OnCombatStarted);
            EventBus.Subscribe(EventBus.EventType.CombatEnded,     OnCombatEnded);
            EventBus.Subscribe(EventBus.EventType.ModuleLoaded,    OnModuleLoaded);
            EventBus.Subscribe(EventBus.EventType.PlayerDied,      OnPlayerDied);

            // Register NWScript handlers
            Scripting.NWScriptVM.RegisterHandler("PlaySound", ctx =>
            {
                string resref = Scripting.GlobalVars.GetString("_sound_resref");
                if (!string.IsNullOrEmpty(resref)) PlaySFX(resref);
            });
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.CombatStarted,  OnCombatStarted);
            EventBus.Unsubscribe(EventBus.EventType.CombatEnded,    OnCombatEnded);
            EventBus.Unsubscribe(EventBus.EventType.ModuleLoaded,   OnModuleLoaded);
            EventBus.Unsubscribe(EventBus.EventType.PlayerDied,     OnPlayerDied);
        }

        // ── PUBLIC API — MUSIC ────────────────────────────────────────────────

        /// <summary>
        /// Play background music by KotOR ambientmusic.2da ID.
        /// Crossfades from the currently playing track.
        /// </summary>
        public void PlayMusic(int musicId, bool immediate = false)
        {
            if (musicId == _currentMusicId) return;

            string resref = MusicTable.GetResRef(musicId);
            if (string.IsNullOrEmpty(resref))
            {
                Debug.LogWarning($"[AudioManager] Unknown music ID {musicId}.");
                return;
            }

            _currentMusicId = musicId;
            var clip = LoadClip(resref);
            if (clip == null) { Debug.LogWarning($"[AudioManager] Music '{resref}' not found."); return; }

            if (immediate)
            {
                _activeMusicSource.clip   = clip;
                _activeMusicSource.volume = _musicVolume * _masterVolume;
                _activeMusicSource.Play();
            }
            else
            {
                if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = StartCoroutine(CrossfadeMusic(clip));
            }

            Debug.Log($"[AudioManager] Music: {resref} (id={musicId})");
        }

        /// <summary>Stop background music with optional fade-out.</summary>
        public void StopMusic(bool fadeOut = true)
        {
            _currentMusicId = -1;
            if (fadeOut)
            {
                if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
                _crossfadeRoutine = StartCoroutine(FadeOut(_activeMusicSource, _crossfadeTime));
            }
            else
            {
                _musicA.Stop();
                _musicB.Stop();
            }
        }

        // ── PUBLIC API — SFX ─────────────────────────────────────────────────

        /// <summary>Play a one-shot sound effect by wav resref.</summary>
        public void PlaySFX(string resref, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(resref)) return;
            var clip = LoadClip(resref);
            if (clip == null) { Debug.LogWarning($"[AudioManager] SFX '{resref}' not found."); return; }
            _sfxSource.PlayOneShot(clip, _sfxVolume * _masterVolume * volumeScale);
        }

        /// <summary>Play a positional SFX at a world-space position.</summary>
        public void PlaySFXAt(string resref, Vector3 position, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(resref)) return;
            var clip = LoadClip(resref);
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume * _masterVolume * volumeScale);
        }

        // ── PUBLIC API — VOICE OVER ───────────────────────────────────────────

        /// <summary>
        /// Play a VO line by TLK StrRef.
        /// Looks up the wav resref from the TLK entry, loads, and plays.
        /// Optionally drives lip-sync on the speaker GameObject.
        /// </summary>
        public void PlayVO(uint strRef, GameObject speaker = null)
        {
            string wavResRef = SceneBootstrapper.GetVoiceWavResRef(strRef);
            if (string.IsNullOrEmpty(wavResRef))
            {
                Debug.LogWarning($"[AudioManager] No VO wav for StrRef {strRef}.");
                return;
            }
            PlayVOByResRef(wavResRef, speaker);
        }

        /// <summary>Play a VO line by direct wav resref with optional lip-sync.</summary>
        public void PlayVOByResRef(string wavResRef, GameObject speaker = null)
        {
            if (string.IsNullOrEmpty(wavResRef)) return;

            var clip = LoadClip(wavResRef);
            if (clip == null) { Debug.LogWarning($"[AudioManager] VO '{wavResRef}' not found."); return; }

            _voSource.volume = _voiceVolume * _masterVolume;
            _voSource.clip   = clip;
            _voSource.Play();
            Debug.Log($"[AudioManager] VO: {wavResRef} ({clip.length:F1}s)");

            // Trigger lip-sync
            if (speaker != null && LipSyncSystem.Instance != null)
            {
                string lipResRef = wavResRef; // KotOR .lip files share the same resref as .wav
                LipSyncSystem.Instance.PlayLip(lipResRef, _voSource, speaker);
            }
        }

        /// <summary>Stop any currently playing VO.</summary>
        public void StopVO()
        {
            _voSource.Stop();
            LipSyncSystem.Instance?.Stop();
        }

        // ── PUBLIC API — VOICE CHAT ───────────────────────────────────────────

        /// <summary>
        /// Play a voice-chat line (from voicechat.2da) on a creature.
        /// Called by NWScriptVM.PlayVoiceChat().
        /// </summary>
        public void PlayVoiceChat(int vcId, GameObject creature = null)
        {
            string resref = VoiceChatTable.GetResRef(vcId);
            if (string.IsNullOrEmpty(resref))
            {
                Debug.LogWarning($"[AudioManager] Unknown VoiceChat ID {vcId}.");
                return;
            }

            // Append creature-specific prefix if available (e.g. "m_" for male)
            // For now use the generic resref
            if (creature != null)
                PlaySFXAt(resref, creature.transform.position);
            else
                PlaySFX(resref);

            Debug.Log($"[AudioManager] VoiceChat {vcId}: {resref} on {creature?.name ?? "unknown"}");
        }

        // ── VOLUME CONTROLS ───────────────────────────────────────────────────

        public void SetMasterVolume(float v)
        {
            _masterVolume = Mathf.Clamp01(v);
            ApplyVolumes();
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = Mathf.Clamp01(v);
            _activeMusicSource.volume = _musicVolume * _masterVolume;
        }

        public void SetSFXVolume(float v)   { _sfxVolume   = Mathf.Clamp01(v); }
        public void SetVoiceVolume(float v) { _voiceVolume = Mathf.Clamp01(v); }

        // ── EVENT HANDLERS ────────────────────────────────────────────────────

        private void OnCombatStarted(EventBus.GameEventArgs _)
        {
            if (_combatMusicActive) return;
            _combatMusicActive = true;
            PlayMusic(12, immediate: false); // mus_combat_gen
        }

        private void OnCombatEnded(EventBus.GameEventArgs _)
        {
            _combatMusicActive = false;
            // Return to ambient music — use module default (id 3 = Dantooine default)
            // In a full impl this would remember the pre-combat track id.
            PlayMusic(3, immediate: false);
        }

        private void OnModuleLoaded(EventBus.GameEventArgs args)
        {
            // Play area-appropriate ambient music based on module name
            if (args is EventBus.ModuleEventArgs ma)
            {
                int musicId = GuessModuleMusicId(ma.ModuleName);
                if (musicId > 0) PlayMusic(musicId);
            }
        }

        private void OnPlayerDied(EventBus.GameEventArgs _)
        {
            StopMusic(fadeOut: true);
            PlaySFX("mus_death");
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        /// <summary>Load an AudioClip from ResourceManager by wav resref (cached).</summary>
        private AudioClip LoadClip(string resref)
        {
            if (_clipCache.TryGetValue(resref, out var cached)) return cached;

            // Ask ResourceManager for the raw bytes
            byte[] data = ResourceManager.Instance?.GetResource(resref, ResourceType.WAV);
            if (data == null || data.Length == 0) return null;

            var clip = WavDecoder.Decode(data, resref);
            if (clip != null) _clipCache[resref] = clip;
            return clip;
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            // Find the inactive source
            var next = (_activeMusicSource == _musicA) ? _musicB : _musicA;
            next.clip   = newClip;
            next.volume = 0f;
            next.Play();

            float elapsed = 0f;
            float startVol = _activeMusicSource.volume;

            while (elapsed < _crossfadeTime)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / _crossfadeTime;
                _activeMusicSource.volume = Mathf.Lerp(startVol, 0f, t);
                next.volume               = Mathf.Lerp(0f, _musicVolume * _masterVolume, t);
                yield return null;
            }

            _activeMusicSource.Stop();
            _activeMusicSource = next;
        }

        private IEnumerator FadeOut(AudioSource source, float duration)
        {
            float startVol = source.volume;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            source.Stop();
            source.volume = startVol;
        }

        private void ApplyVolumes()
        {
            _activeMusicSource.volume = _musicVolume * _masterVolume;
        }

        private AudioSource CreateAudioSource(string goName, bool loop)
        {
            var go = new GameObject($"AudioSource_{goName}");
            go.transform.SetParent(transform, false);
            var src  = go.AddComponent<AudioSource>();
            src.loop = loop;
            src.playOnAwake = false;
            return src;
        }

        // Simple heuristic: map module name prefix to music ID
        private static int GuessModuleMusicId(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName)) return -1;
            string m = moduleName.ToLowerInvariant();
            if (m.StartsWith("tar")) return  1; // Taris
            if (m.StartsWith("dan")) return  3; // Dantooine
            if (m.StartsWith("tat")) return  4; // Tatooine
            if (m.StartsWith("kas")) return  5; // Kashyyyk
            if (m.StartsWith("man")) return  6; // Manaan
            if (m.StartsWith("kor")) return  7; // Korriban
            if (m.StartsWith("unk")) return  8; // Unknown World
            if (m.StartsWith("sta")) return 12; // Star Forge → combat
            return 3; // default
        }
    }
}
