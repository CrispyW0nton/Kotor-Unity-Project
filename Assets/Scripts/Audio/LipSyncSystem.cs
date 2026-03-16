using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.FileReaders;

namespace KotORUnity.Audio
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LIP FILE FORMAT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// KotOR .lip file binary format (little-endian):
    ///
    ///   char[4]  fileType    "LIP "
    ///   char[4]  fileVersion "V1.0"
    ///   float    length      (total animation length in seconds)
    ///   uint32   entryCount
    ///   Entry[entryCount]:
    ///     float  time        (key-frame time in seconds)
    ///     uint8  shape       (mouth shape index 0–17, see KotOR phoneme table)
    ///
    /// Phoneme/shape mapping (KotOR1):
    ///   0=EE, 1=EH, 2=schwa, 3=AH, 4=OH, 5=OOH, 6=S/Z, 7=Th,
    ///   8=F/V, 9=N/NG, 10=LDT, 11=K, 12=CH, 13=M/P/B, 14=J,
    ///   15=R, 16=W, 17=silence
    /// </summary>
    public class LipData
    {
        public float        Length;     // total duration in seconds
        public LipKeyframe[] Keyframes;
    }

    public struct LipKeyframe
    {
        public float Time;   // seconds
        public byte  Shape;  // 0–17
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  LIP PARSER
    // ═══════════════════════════════════════════════════════════════════════════

    public static class LipReader
    {
        private static readonly byte[] Sig     = { (byte)'L', (byte)'I', (byte)'P', (byte)' ' };
        private static readonly byte[] Version = { (byte)'V', (byte)'1', (byte)'.', (byte)'0' };

        public static LipData Parse(byte[] data)
        {
            if (data == null || data.Length < 12)
            {
                Debug.LogWarning("[LipReader] Data too short.");
                return null;
            }

            try
            {
                using var ms = new System.IO.MemoryStream(data);
                using var br = new System.IO.BinaryReader(ms);

                // Header
                byte[] sig = br.ReadBytes(4);
                byte[] ver = br.ReadBytes(4);

                for (int i = 0; i < 4; i++)
                    if (sig[i] != Sig[i])
                    {
                        Debug.LogWarning("[LipReader] Invalid LIP signature.");
                        return null;
                    }

                float  length     = br.ReadSingle();
                uint   entryCount = br.ReadUInt32();

                var keyframes = new LipKeyframe[entryCount];
                for (uint i = 0; i < entryCount; i++)
                {
                    keyframes[i] = new LipKeyframe
                    {
                        Time  = br.ReadSingle(),
                        Shape = br.ReadByte()
                    };
                }

                return new LipData { Length = length, Keyframes = keyframes };
            }
            catch (Exception e)
            {
                Debug.LogError($"[LipReader] Parse error: {e.Message}");
                return null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  LIP SYNC SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// LipSyncSystem drives blend-shape or bone-based mouth animation on a
    /// character SkinnedMeshRenderer using a parsed LipData object.
    ///
    /// There are two modes:
    ///   1. BlendShape mode: maps shape indices to blend-shape indices on a
    ///      SkinnedMeshRenderer named "Head" (or any name set in Inspector).
    ///   2. RotationBone mode: rotates a jaw bone by a lookup table of angles.
    ///
    /// In KotOR, mouth shapes are baked into the creature model as blend shapes
    /// or jaw-bone rotation keys. Since we parse the MDL ourselves, we drive
    /// whichever is present on the instantiated model at runtime.
    ///
    /// Usage: call PlayLip(lipResRef, audioSource) during dialogue VO playback.
    /// The system automatically syncs with AudioSource.time each frame.
    /// </summary>
    public class LipSyncSystem : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static LipSyncSystem Instance { get; private set; }

        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Blend Shape Mode")]
        [SerializeField] private string _headMeshName       = "Head";
        [SerializeField] private string _blendShapePrefix   = "mouth_";  // e.g. mouth_EE, mouth_AH

        [Header("Bone Mode")]
        [SerializeField] private string _jawBoneName        = "Jaw";
        [SerializeField] private float  _maxJawOpenAngle    = 20f;

        [Header("Smoothing")]
        [SerializeField] private float  _blendSpeed         = 12f;

        // KotOR shape index → blend weight table (0–100)
        // index matches the phoneme list in the file format summary
        private static readonly float[] ShapeWeights =
        {
            // 0-EE   1-EH   2-sch  3-AH   4-OH   5-OOH  6-SZ   7-Th
              80f,    60f,   40f,   90f,   70f,   50f,   30f,   20f,
            // 8-FV   9-N   10-LDT  11-K   12-CH  13-MPB 14-J   15-R
              25f,   35f,   45f,   55f,   65f,   75f,   85f,   40f,
            // 16-W  17-silence
              50f,    0f
        };

        // ── STATE ──────────────────────────────────────────────────────────────
        private LipData          _currentLip;
        private AudioSource      _syncSource;
        private SkinnedMeshRenderer _headMesh;
        private Transform        _jawBone;
        private float            _currentWeight;
        private bool             _playing;

        // Shape index that corresponds to the current blend weight direction
        private int _currentShapeIndex = 17; // silence by default

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_playing || _currentLip == null || _syncSource == null) return;

            // Stop if audio finished
            if (!_syncSource.isPlaying)
            {
                Stop();
                return;
            }

            float t = _syncSource.time;
            int shapeIndex = GetShapeAtTime(_currentLip, t);
            float targetWeight = GetWeight(shapeIndex);

            _currentWeight = Mathf.Lerp(_currentWeight, targetWeight, Time.deltaTime * _blendSpeed);
            _currentShapeIndex = shapeIndex;

            ApplyWeight(shapeIndex, _currentWeight);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Begin lip-sync playback for a resref .lip file synced to an AudioSource.
        /// The character GameObject is used to find the head mesh / jaw bone.
        /// </summary>
        public void PlayLip(string lipResRef, AudioSource audioSource, GameObject character)
        {
            Stop();

            var rm = SceneBootstrapper.Resources;
            if (rm == null) return;

            byte[] lipData = rm.GetResource(lipResRef, ResourceType.LIP);
            if (lipData == null)
            {
                Debug.LogWarning($"[LipSync] LIP not found: '{lipResRef}'");
                return;
            }

            var lip = LipReader.Parse(lipData);
            if (lip == null) return;

            // Locate head mesh / jaw bone on the character
            FindMouthComponents(character);

            _currentLip  = lip;
            _syncSource  = audioSource;
            _playing     = true;
            _currentWeight = 0f;
        }

        /// <summary>
        /// Play lip sync from a pre-parsed LipData (used by DialogueSystem directly).
        /// </summary>
        public void PlayLip(LipData lip, AudioSource audioSource, GameObject character)
        {
            Stop();
            if (lip == null || audioSource == null || character == null) return;

            FindMouthComponents(character);
            _currentLip  = lip;
            _syncSource  = audioSource;
            _playing     = true;
            _currentWeight = 0f;
        }

        public void Stop()
        {
            _playing    = false;
            _currentLip = null;
            _syncSource = null;

            // Reset mouth to closed
            ApplyWeight(17, 0f); // shape 17 = silence
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private void FindMouthComponents(GameObject character)
        {
            _headMesh = FindSkinnedMesh(character.transform, _headMeshName);
            _jawBone  = FindBone(character.transform, _jawBoneName);
        }

        private static SkinnedMeshRenderer FindSkinnedMesh(Transform root, string name)
        {
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return smr;
            // Fallback: first SkinnedMeshRenderer
            return root.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        private static int GetShapeAtTime(LipData lip, float time)
        {
            if (lip.Keyframes == null || lip.Keyframes.Length == 0) return 17;

            int shape = 17; // silence
            for (int i = 0; i < lip.Keyframes.Length; i++)
            {
                if (lip.Keyframes[i].Time <= time)
                    shape = lip.Keyframes[i].Shape;
                else
                    break;
            }
            return Mathf.Clamp(shape, 0, 17);
        }

        private static float GetWeight(int shapeIndex)
        {
            if (shapeIndex < 0 || shapeIndex >= ShapeWeights.Length) return 0f;
            return ShapeWeights[shapeIndex];
        }

        private void ApplyWeight(int shapeIndex, float weight)
        {
            // Blend shape approach
            if (_headMesh != null && _headMesh.sharedMesh != null)
            {
                int count = _headMesh.sharedMesh.blendShapeCount;
                // Clear all mouth shapes
                for (int i = 0; i < count; i++)
                {
                    string bsName = _headMesh.sharedMesh.GetBlendShapeName(i);
                    if (bsName.StartsWith(_blendShapePrefix, StringComparison.OrdinalIgnoreCase))
                        _headMesh.SetBlendShapeWeight(i, 0f);
                }

                // Set target shape
                string targetName = $"{_blendShapePrefix}{PhonemeNames[shapeIndex]}";
                int idx = _headMesh.sharedMesh.GetBlendShapeIndex(targetName);
                if (idx >= 0)
                    _headMesh.SetBlendShapeWeight(idx, weight);
                return;
            }

            // Jaw bone fallback
            if (_jawBone != null)
            {
                float angle = weight / 100f * _maxJawOpenAngle;
                _jawBone.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }

        private static readonly string[] PhonemeNames =
        {
            "EE","EH","schwa","AH","OH","OOH","SZ","Th",
            "FV","N","LDT","K","CH","MPB","J","R","W","silence"
        };
    }
}
