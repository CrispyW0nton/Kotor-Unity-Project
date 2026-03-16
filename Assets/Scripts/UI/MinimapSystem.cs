using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Party;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  MINIMAP SYSTEM  —  KotOR-style overhead radar
    //
    //  Architecture:
    //    MinimapSystem        : MonoBehaviour on HUD canvas, drives everything
    //    MinimapRenderer      : Renders a RenderTexture from an overhead Camera
    //    MinimapDot           : Per-entity icon (player, companions, enemies, NPCs)
    //    MinimapFogOfWar      : Pixel-shader fog reveal as the player explores
    //    MinimapAreaBoundary  : Defines the bounding rectangle of the current area
    //
    //  Features:
    //    • Orthographic overhead camera follows the player
    //    • Colored dots: player=yellow, companions=cyan, enemies=red, NPCs=green
    //    • Optional PNG area map background (same resref as the module)
    //    • Fog-of-war: black overlay revealed around the player in real-time
    //    • Click-to-set-waypoint in RTS mode
    //    • Zoom-in/zoom-out mouse scroll
    //    • Expand to full-screen area map (M key)
    //    • EventBus: subscribes to ModeSwitch, ModuleLoaded, EntityKilled
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Root minimap controller. Attach to the minimap panel in the HUD canvas.
    /// </summary>
    public class MinimapSystem : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────

        [Header("UI References")]
        [SerializeField] private RawImage        _minimapDisplay;      // shows minimap RenderTexture
        [SerializeField] private RawImage        _fullmapDisplay;      // full-screen area map
        [SerializeField] private GameObject      _minimapPanel;        // compact HUD panel
        [SerializeField] private GameObject      _fullmapPanel;        // full-screen overlay
        [SerializeField] private Transform       _dotContainer;        // parent for dot UI elements
        [SerializeField] private TextMeshProUGUI _areaNameText;

        [Header("Overhead Camera")]
        [SerializeField] private UnityEngine.Camera _minimapCamera;
        [SerializeField] private float           _cameraHeight    = 30f;
        [SerializeField] private float           _orthoSizeMin    = 8f;
        [SerializeField] private float           _orthoSizeMax    = 40f;
        [SerializeField] private float           _defaultOrthoSize = 15f;
        [SerializeField] private LayerMask       _renderLayers;

        [Header("Dot Prefabs")]
        [SerializeField] private GameObject      _dotPrefab;           // generic dot prefab with Image

        [Header("Fog of War")]
        [SerializeField] private bool            _fogEnabled       = true;
        [SerializeField] private float           _revealRadius     = 12f;
        [SerializeField] private int             _fogTextureSize   = 256;
        [SerializeField] private RawImage        _fogOverlay;

        [Header("Colors")]
        [SerializeField] private Color _playerColor    = new Color(1f, 0.9f, 0f);      // yellow
        [SerializeField] private Color _companionColor = new Color(0.2f, 0.85f, 1f);   // cyan
        [SerializeField] private Color _enemyColor     = new Color(1f, 0.2f, 0.2f);    // red
        [SerializeField] private Color _npcColor       = new Color(0.2f, 1f, 0.4f);    // green
        [SerializeField] private Color _waypointColor  = new Color(1f, 0.6f, 0f);      // orange

        [Header("Config")]
        [SerializeField] private float  _dotSize          = 8f;
        [SerializeField] private float  _zoomStep         = 2f;
        [SerializeField] private KeyCode _toggleFullmapKey = KeyCode.M;

        // ── RUNTIME ───────────────────────────────────────────────────────────

        private RenderTexture _minimapRT;
        private Texture2D     _fogTexture;
        private Color32[]     _fogPixels;
        private bool          _fogDirty     = true;

        private Transform     _playerTransform;
        private float         _currentOrthoSize;
        private bool          _fullmapOpen  = false;
        private Rect          _areaBounds   = new Rect(-50f, -50f, 100f, 100f);

        // dot registry
        private readonly Dictionary<Transform, MinimapDot> _dots = new Dictionary<Transform, MinimapDot>();

        // singleton
        public static MinimapSystem Instance { get; private set; }

        // ── UNITY ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _currentOrthoSize = _defaultOrthoSize;
            SetupMinimapCamera();
            SetupFogOfWar();
        }

        private void Start()
        {
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_playerTransform != null)
                RegisterDot(_playerTransform, MinimapDotType.Player);

            RegisterPartyDots();

            // EventBus subscriptions
            EventBus.Subscribe(EventBus.EventType.ModuleLoaded,   OnModuleLoaded);
            EventBus.Subscribe(EventBus.EventType.EntityKilled,   OnEntityKilled);
            EventBus.Subscribe(EventBus.EventType.ModeSwitch,     OnModeSwitch);
            EventBus.Subscribe(EventBus.EventType.CompanionDied,  OnCompanionDied);

            _fullmapPanel?.SetActive(false);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModuleLoaded, OnModuleLoaded);
            EventBus.Unsubscribe(EventBus.EventType.EntityKilled, OnEntityKilled);
            EventBus.Unsubscribe(EventBus.EventType.ModeSwitch,   OnModeSwitch);
            EventBus.Unsubscribe(EventBus.EventType.CompanionDied, OnCompanionDied);

            if (_minimapRT != null) { _minimapRT.Release(); Destroy(_minimapRT); }
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            // Follow player
            if (_minimapCamera != null)
            {
                var pos = _playerTransform.position;
                _minimapCamera.transform.position = new Vector3(pos.x, _cameraHeight, pos.z);
            }

            // Zoom with scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f && IsMouseOverMinimap())
            {
                _currentOrthoSize = Mathf.Clamp(
                    _currentOrthoSize - scroll * _zoomStep,
                    _orthoSizeMin, _orthoSizeMax);
                if (_minimapCamera != null)
                    _minimapCamera.orthographicSize = _currentOrthoSize;
            }

            // Toggle full map
            if (Input.GetKeyDown(_toggleFullmapKey))
                ToggleFullMap();

            // Update dot positions
            UpdateDots();

            // Update fog of war
            if (_fogEnabled) UpdateFog();

            // Click-to-waypoint in RTS mode
            if (Input.GetMouseButtonDown(0) && IsMouseOverMinimap()
                && ModeSwitchSystem.Instance?.CurrentMode == GameMode.RTS)
            {
                HandleMinimapClick();
            }
        }

        // ── CAMERA SETUP ─────────────────────────────────────────────────────

        private void SetupMinimapCamera()
        {
            if (_minimapCamera == null)
            {
                var camGO = new GameObject("MinimapCamera");
                camGO.transform.SetParent(transform);
                _minimapCamera = camGO.AddComponent<UnityEngine.Camera>();
            }

            _minimapCamera.orthographic      = true;
            _minimapCamera.orthographicSize  = _currentOrthoSize;
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _minimapCamera.clearFlags        = (ClearFlags)CameraClearFlags.SolidColor;
            _minimapCamera.backgroundColor   = Color.black;
            _minimapCamera.cullingMask       = _renderLayers != 0 ? (int)_renderLayers : ~0;
            _minimapCamera.depth             = -2;

            // Create RenderTexture
            _minimapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            _minimapRT.filterMode = FilterMode.Bilinear;
            _minimapCamera.targetTexture = _minimapRT;

            if (_minimapDisplay != null) _minimapDisplay.texture = _minimapRT;
        }

        // ── FOG OF WAR ────────────────────────────────────────────────────────

        private void SetupFogOfWar()
        {
            if (!_fogEnabled || _fogOverlay == null) return;

            _fogTexture = new Texture2D(_fogTextureSize, _fogTextureSize, TextureFormat.RGBA32, false);
            _fogPixels  = new Color32[_fogTextureSize * _fogTextureSize];

            // Start fully black
            var black = new Color32(0, 0, 0, 220);
            for (int i = 0; i < _fogPixels.Length; i++) _fogPixels[i] = black;
            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply();
            _fogOverlay.texture = _fogTexture;
        }

        private void UpdateFog()
        {
            if (!_fogDirty || _fogTexture == null || _playerTransform == null) return;
            _fogDirty = false;

            // Map world position to fog texture coords
            float wx = _playerTransform.position.x;
            float wz = _playerTransform.position.z;

            float u = (wx - _areaBounds.x) / _areaBounds.width;
            float v = (wz - _areaBounds.y) / _areaBounds.height;

            int cx = Mathf.RoundToInt(u * _fogTextureSize);
            int cy = Mathf.RoundToInt(v * _fogTextureSize);

            // Reveal radius in texture pixels
            float texReveal = (_revealRadius / _areaBounds.width) * _fogTextureSize;
            int r = Mathf.CeilToInt(texReveal);

            bool changed = false;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= _fogTextureSize || py < 0 || py >= _fogTextureSize) continue;

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > texReveal) continue;

                    int idx = py * _fogTextureSize + px;
                    if (_fogPixels[idx].a > 0)
                    {
                        // Fade near edges
                        float fade = Mathf.Clamp01(1f - (dist / texReveal));
                        byte alpha = (byte)Mathf.Max(0, _fogPixels[idx].a - (byte)(fade * 220));
                        _fogPixels[idx].a = alpha;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                _fogTexture.SetPixels32(_fogPixels);
                _fogTexture.Apply(false);
            }

            // Schedule next fog update in 0.1s to avoid per-frame texture upload
            StartCoroutine(FogUpdateCooldown());
        }

        private IEnumerator FogUpdateCooldown()
        {
            yield return new WaitForSeconds(0.1f);
            _fogDirty = true;
        }

        // ── DOTS ──────────────────────────────────────────────────────────────

        public enum MinimapDotType { Player, Companion, Enemy, NPC, Waypoint, Objective }

        public MinimapDot RegisterDot(Transform target, MinimapDotType type)
        {
            if (target == null || _dots.ContainsKey(target)) return null;

            GameObject dotGO;
            if (_dotPrefab != null)
            {
                dotGO = Instantiate(_dotPrefab, _dotContainer ?? transform);
            }
            else
            {
                dotGO = new GameObject($"Dot_{target.name}");
                dotGO.transform.SetParent(_dotContainer ?? transform, false);
                var img = dotGO.AddComponent<Image>();
                img.rectTransform.sizeDelta = Vector2.one * _dotSize;
            }

            var dot = dotGO.GetComponent<MinimapDot>() ?? dotGO.AddComponent<MinimapDot>();
            dot.Init(target, type, GetDotColor(type), _dotSize);
            _dots[target] = dot;
            return dot;
        }

        public void UnregisterDot(Transform target)
        {
            if (_dots.TryGetValue(target, out var dot))
            {
                if (dot != null && dot.gameObject != null) Destroy(dot.gameObject);
                _dots.Remove(target);
            }
        }

        private void UpdateDots()
        {
            if (_minimapCamera == null || _minimapDisplay == null) return;

            var toRemove = new List<Transform>();
            var rt       = _minimapDisplay.rectTransform;

            foreach (var kvp in _dots)
            {
                if (kvp.Key == null) { toRemove.Add(kvp.Key); continue; }

                // Project world pos to minimap UV
                Vector3 worldPos = kvp.Key.position;
                Vector3 viewPos  = _minimapCamera.WorldToViewportPoint(worldPos);

                if (viewPos.z < 0f)
                {
                    kvp.Value.gameObject.SetActive(false);
                    continue;
                }

                kvp.Value.gameObject.SetActive(true);

                // Place dot in local rect space of the minimap display
                Vector2 uv = new Vector2(viewPos.x, viewPos.y);
                Vector2 local = new Vector2(
                    (uv.x - 0.5f) * rt.rect.width,
                    (uv.y - 0.5f) * rt.rect.height);
                kvp.Value.SetPosition(local);
            }

            foreach (var t in toRemove) _dots.Remove(t);
        }

        private Color GetDotColor(MinimapDotType type) => type switch
        {
            MinimapDotType.Player    => _playerColor,
            MinimapDotType.Companion => _companionColor,
            MinimapDotType.Enemy     => _enemyColor,
            MinimapDotType.NPC       => _npcColor,
            MinimapDotType.Waypoint  => _waypointColor,
            MinimapDotType.Objective => Color.white,
            _                        => Color.grey,
        };

        // ── PARTY DOTS ────────────────────────────────────────────────────────

        private void RegisterPartyDots()
        {
            var pm = PartyManager.Instance;
            if (pm == null) return;

            foreach (var member in pm.ActiveMembers)
            {
                if (member.IsPlayer) continue;
                // Find the spawned companion GameObject by tag + name
                var go = GameObject.Find(member.Tag ?? member.ResRef);
                if (go != null) RegisterDot(go.transform, MinimapDotType.Companion);
            }
        }

        // ── FULL MAP TOGGLE ───────────────────────────────────────────────────

        private void ToggleFullMap()
        {
            _fullmapOpen = !_fullmapOpen;
            _fullmapPanel?.SetActive(_fullmapOpen);
            _minimapPanel?.SetActive(!_fullmapOpen);

            if (_fullmapOpen)
            {
                EventBus.Publish(EventBus.EventType.GamePaused);
                // Load the area map image
                LoadAreaMapTexture();
            }
            else
            {
                EventBus.Publish(EventBus.EventType.GameResumed);
            }
        }

        private void LoadAreaMapTexture()
        {
            // Try to load a PNG named after the current module
            string moduleName = PlayerPrefs.GetString("CurrentModule", "unk_m01aa");
            var tex = Resources.Load<Texture2D>($"AreaMaps/{moduleName}");
            if (tex == null) tex = Resources.Load<Texture2D>("AreaMaps/default_area");
            if (_fullmapDisplay != null && tex != null)
                _fullmapDisplay.texture = tex;
        }

        // ── MINIMAP CLICK (RTS waypoint) ──────────────────────────────────────

        private void HandleMinimapClick()
        {
            var rt = _minimapDisplay?.rectTransform;
            if (rt == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, null, out Vector2 localPoint);

            // Convert to world position
            float u = (localPoint.x / rt.rect.width)  + 0.5f;
            float v = (localPoint.y / rt.rect.height) + 0.5f;

            Vector3 viewPos = new Vector3(u, v, _cameraHeight);
            Vector3 worldPos = _minimapCamera.ViewportToWorldPoint(viewPos);
            worldPos.y = 0f;

            // Issue move command
            EventBus.Publish(EventBus.EventType.UICommandIssued,
                new EventBus.GameEventArgs
                {
                    StringValue = "MinimapMove",
                    FloatValue  = worldPos.x,
                    IntValue    = (int)worldPos.z,
                });

            // Spawn a waypoint dot temporarily
            var dotGO = new GameObject("WaypointDot");
            dotGO.transform.position = worldPos;
            RegisterDot(dotGO.transform, MinimapDotType.Waypoint);
            StartCoroutine(RemoveDotAfter(dotGO.transform, 3f));
        }

        private IEnumerator RemoveDotAfter(Transform t, float delay)
        {
            yield return new WaitForSeconds(delay);
            UnregisterDot(t);
            if (t != null) Destroy(t.gameObject);
        }

        // ── AREA BOUNDS ───────────────────────────────────────────────────────

        /// <summary>Set the minimap area bounds (called by AreaLoader/ModuleLoader).</summary>
        public void SetAreaBounds(Rect bounds)
        {
            _areaBounds = bounds;
            // Reset fog when entering a new area
            if (_fogEnabled && _fogPixels != null)
            {
                var black = new Color32(0, 0, 0, 220);
                for (int i = 0; i < _fogPixels.Length; i++) _fogPixels[i] = black;
                _fogDirty = true;
            }
        }

        public void SetAreaName(string name)
        {
            if (_areaNameText != null) _areaNameText.text = name;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private bool IsMouseOverMinimap()
        {
            if (_minimapDisplay == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _minimapDisplay.rectTransform, Input.mousePosition);
        }

        // ── EVENT HANDLERS ────────────────────────────────────────────────────

        private void OnModuleLoaded(EventBus.GameEventArgs args)
        {
            // Clear enemy dots, re-register party
            var toRemove = new List<Transform>();
            foreach (var kvp in _dots)
            {
                if (kvp.Value.DotType == MinimapDotType.Enemy) toRemove.Add(kvp.Key);
            }
            foreach (var t in toRemove) UnregisterDot(t);

            RegisterPartyDots();

            if (args != null && !string.IsNullOrEmpty(args.StringValue))
                SetAreaName(args.StringValue);
        }

        private void OnEntityKilled(EventBus.GameEventArgs args)
        {
            // If the killed entity has a dot, mark it as dead (grey) then remove
            // We don't have the GameObject here, but enemies auto-remove when their
            // Transform becomes null in UpdateDots().
        }

        private void OnModeSwitch(EventBus.GameEventArgs args)
        {
            // Nothing to change on mode switch for now
        }

        private void OnCompanionDied(EventBus.GameEventArgs args) { }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MINIMAP DOT
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>A single colored dot on the minimap, tracking a world-space Transform.</summary>
    public class MinimapDot : MonoBehaviour
    {
        private RectTransform  _rt;
        private Image          _img;

        public  Transform              TrackedTransform { get; private set; }
        public  MinimapSystem.MinimapDotType DotType   { get; private set; }

        public void Init(Transform target, MinimapSystem.MinimapDotType type, Color color, float size)
        {
            TrackedTransform = target;
            DotType = type;

            _rt  = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>().GetComponent<RectTransform>();
            _img = GetComponent<Image>() ?? gameObject.AddComponent<Image>();

            _rt.sizeDelta  = Vector2.one * size;
            _rt.anchorMin  = new Vector2(0.5f, 0.5f);
            _rt.anchorMax  = new Vector2(0.5f, 0.5f);
            _img.color     = color;

            // Scale player dot slightly larger
            if (type == MinimapSystem.MinimapDotType.Player)
                _rt.sizeDelta = Vector2.one * (size * 1.4f);
        }

        public void SetPosition(Vector2 localPos)
        {
            if (_rt != null) _rt.anchoredPosition = localPos;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MINIMAP AREA BOUNDARY  —  Placed in scene to define fog/camera bounds
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add to the area root to define the minimap bounds.
    /// MinimapSystem reads this on Start/module load.
    /// </summary>
    public class MinimapAreaBoundary : MonoBehaviour
    {
        [Tooltip("Centre of the area in world X/Z.")]
        [SerializeField] public Vector2 Centre = Vector2.zero;
        [Tooltip("Full width (X) and height (Z) of the area in metres.")]
        [SerializeField] public Vector2 Size   = new Vector2(100f, 100f);

        public Rect Bounds => new Rect(
            Centre.x - Size.x * 0.5f,
            Centre.y - Size.y * 0.5f,
            Size.x, Size.y);

        private void Start()
        {
            MinimapSystem.Instance?.SetAreaBounds(Bounds);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            UnityEngine.Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            UnityEngine.Gizmos.DrawWireCube(
                new Vector3(Centre.x, transform.position.y, Centre.y),
                new Vector3(Size.x, 1f, Size.y));
        }
#endif
    }
}
