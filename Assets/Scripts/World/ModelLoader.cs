using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.KotOR.Parsers;

namespace KotORUnity.World
{
    /// <summary>
    /// ModelLoader — MonoBehaviour that converts a KotOR MDL/MDX pair into a
    /// live Unity GameObject hierarchy, complete with MeshRenderers, Materials,
    /// and AnimationClips baked into an Animator.
    ///
    /// Usage:
    ///   1. Attach to a root GameObject in the scene.
    ///   2. Call LoadModel(resref) — it schedules async loading and returns immediately.
    ///   3. Subscribe to OnModelLoaded / OnModelFailed for completion callbacks.
    ///
    /// Coordinate system:
    ///   KotOR uses a right-hand Y-up system rotated 90 ° about X relative to Unity.
    ///   MdlReader already converts positions/rotations: swap Y↔Z, negate new-Z.
    ///   ModelLoader applies a root scale of (1, 1, 1) and the model's own scale
    ///   factor from the MDL header.
    /// </summary>
    public class ModelLoader : MonoBehaviour
    {
        // ── EVENTS ─────────────────────────────────────────────────────────────
        public event Action<GameObject, string>  OnModelLoaded;
        public event Action<string, string>      OnModelFailed;  // (resref, reason)

        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Rendering")]
        [Tooltip("Override the default URP/Lit shader. Leave blank to auto-detect.")]
        [SerializeField] private Shader _overrideShader;

        [Tooltip("If true, models are also given a MeshCollider (for picking / raycast).")]
        [SerializeField] private bool _addMeshColliders = false;

        // ── PRIVATE CACHE ──────────────────────────────────────────────────────
        // Cached shader so we only look it up once.
        private Shader _shader;

        // Running loads — prevents duplicate requests.
        private readonly HashSet<string> _pendingLoads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── SINGLETON ──────────────────────────────────────────────────────────
        public static ModelLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _shader = _overrideShader
                   ?? Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Standard")
                   ?? Shader.Find("Diffuse");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a KotOR model by resref (no extension) and instantiate it as a
        /// child of <paramref name="parent"/>.
        /// Returns immediately; use <see cref="OnModelLoaded"/> for completion.
        /// </summary>
        public void LoadModel(string resref, Transform parent = null)
        {
            if (string.IsNullOrEmpty(resref)) return;
            if (_pendingLoads.Contains(resref)) return;

            _pendingLoads.Add(resref);
            StartCoroutine(LoadModelCoroutine(resref, parent));
        }

        /// <summary>
        /// Synchronous variant — blocks the calling frame.
        /// Prefer LoadModel() for large models.
        /// </summary>
        public GameObject LoadModelSync(string resref, Transform parent = null)
        {
            var rm = SceneBootstrapper.Resources;
            if (rm == null) { Debug.LogWarning("[ModelLoader] ResourceManager not ready."); return null; }

            byte[] mdlData = rm.GetResource(resref, ResourceType.MDL);
            byte[] mdxData = rm.GetResource(resref, ResourceType.MDX);

            if (mdlData == null)
            {
                Debug.LogWarning($"[ModelLoader] MDL not found: '{resref}'");
                return null;
            }

            var model = MdlReader.Parse(mdlData, mdxData);
            if (model == null)
            {
                Debug.LogWarning($"[ModelLoader] MdlReader returned null for '{resref}'");
                return null;
            }

            return BuildGameObject(model, parent);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  COROUTINE
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator LoadModelCoroutine(string resref, Transform parent)
        {
            // Yield one frame so the call returns to the caller first.
            yield return null;

            var rm = SceneBootstrapper.Resources;
            if (rm == null)
            {
                Debug.LogWarning("[ModelLoader] ResourceManager not ready.");
                _pendingLoads.Remove(resref);
                OnModelFailed?.Invoke(resref, "ResourceManager not initialised");
                yield break;
            }

            byte[] mdlData = rm.GetResource(resref, ResourceType.MDL);
            byte[] mdxData = rm.GetResource(resref, ResourceType.MDX);

            if (mdlData == null)
            {
                Debug.LogWarning($"[ModelLoader] MDL not found: '{resref}'");
                _pendingLoads.Remove(resref);
                OnModelFailed?.Invoke(resref, "MDL resource not found");
                yield break;
            }

            // Parsing is CPU-bound — do it synchronously in this coroutine slice.
            MdlReader.MdlModel model = null;
            try { model = MdlReader.Parse(mdlData, mdxData); }
            catch (Exception e)
            {
                Debug.LogError($"[ModelLoader] Parse exception for '{resref}': {e.Message}");
                _pendingLoads.Remove(resref);
                OnModelFailed?.Invoke(resref, e.Message);
                yield break;
            }

            if (model == null)
            {
                _pendingLoads.Remove(resref);
                OnModelFailed?.Invoke(resref, "MdlReader.Parse returned null");
                yield break;
            }

            // Build the Unity hierarchy on the main thread.
            GameObject root = null;
            try { root = BuildGameObject(model, parent); }
            catch (Exception e)
            {
                Debug.LogError($"[ModelLoader] Build exception for '{resref}': {e.Message}");
                _pendingLoads.Remove(resref);
                OnModelFailed?.Invoke(resref, e.Message);
                yield break;
            }

            _pendingLoads.Remove(resref);
            OnModelLoaded?.Invoke(root, resref);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HIERARCHY BUILDER
        // ══════════════════════════════════════════════════════════════════════

        private GameObject BuildGameObject(MdlReader.MdlModel model, Transform parent)
        {
            // Root object
            var root = new GameObject(model.Name);
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

            // Apply model-level scale from MDL header
            float s = model.Scale > 0f ? model.Scale : 1f;
            root.transform.localScale = new Vector3(s, s, s);

            // Build node hierarchy
            if (model.RootNode != null)
                BuildNode(model.RootNode, root.transform);

            // Bake animations into an Animator / AnimationClips
            if (model.Animations != null && model.Animations.Count > 0)
                BakeAnimations(root, model);

            return root;
        }

        private void BuildNode(MdlReader.MdlNode node, Transform parentTf)
        {
            var go = new GameObject(string.IsNullOrEmpty(node.Name) ? "node" : node.Name);
            go.transform.SetParent(parentTf, worldPositionStays: false);
            go.transform.localPosition = node.Position;
            go.transform.localRotation = node.Rotation;

            // Attach mesh if present
            if (node.Mesh != null)
                AttachMesh(go, node.Mesh);

            // Recurse
            foreach (var child in node.Children)
                BuildNode(child, go.transform);
        }

        private void AttachMesh(GameObject go, MdlReader.MdlMesh mesh)
        {
            if (mesh.Vertices == null || mesh.Vertices.Length == 0) return;
            if (mesh.Triangles == null || mesh.Triangles.Length == 0) return;

            var unityMesh = new Mesh { name = go.name };
            unityMesh.vertices  = mesh.Vertices;
            if (mesh.Normals  != null && mesh.Normals.Length  == mesh.Vertices.Length)
                unityMesh.normals = mesh.Normals;
            if (mesh.UVs != null && mesh.UVs.Length == mesh.Vertices.Length)
                unityMesh.uv = mesh.UVs;
            unityMesh.triangles = mesh.Triangles;

            if (mesh.Normals == null || mesh.Normals.Length != mesh.Vertices.Length)
                unityMesh.RecalculateNormals();
            unityMesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = unityMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BuildMaterial(mesh);

            if (_addMeshColliders)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = unityMesh;
            }
        }

        private Material BuildMaterial(MdlReader.MdlMesh mesh)
        {
            var mat = new Material(_shader ?? Shader.Find("Standard"))
            {
                name = mesh.TextureName ?? "kotor_mat"
            };

            // Main texture
            if (!string.IsNullOrEmpty(mesh.TextureName))
            {
                var tex = TextureCache.Get(mesh.TextureName);
                mat.mainTexture = tex;
            }

            // Alpha handling
            if (mesh.IsTransparent || mesh.Alpha < 1f)
            {
                SetMaterialTransparent(mat);
                var col = mat.color;
                col.a = mesh.Alpha;
                mat.color = col;
            }

            return mat;
        }

        private static void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ANIMATION BAKER
        // ══════════════════════════════════════════════════════════════════════

        private void BakeAnimations(GameObject root, MdlReader.MdlModel model)
        {
            // We create an AnimationClip for each KotOR animation and wire them
            // into a simple Animator Controller via the legacy Animation component
            // (simpler than full RuntimeAnimatorController at this stage).

            var anim = root.AddComponent<Animation>();

            foreach (var kAnim in model.Animations)
            {
                if (kAnim == null || string.IsNullOrEmpty(kAnim.Name)) continue;

                var clip = new AnimationClip
                {
                    name       = kAnim.Name,
                    legacy     = true,
                    wrapMode   = kAnim.Name.ToLowerInvariant().Contains("loop")
                                   ? WrapMode.Loop : WrapMode.Once
                };

                foreach (var animNode in kAnim.Nodes)
                {
                    if (animNode == null) continue;
                    string path = FindNodePath(root.transform, animNode.NodeName);
                    if (path == null) continue;

                    // Position curve (x, y, z)
                    if (animNode.PositionKeys.Count > 0)
                    {
                        var cx = new AnimationCurve();
                        var cy = new AnimationCurve();
                        var cz = new AnimationCurve();
                        foreach (var key in animNode.PositionKeys)
                        {
                            cx.AddKey(key.Time, key.Value.x);
                            cy.AddKey(key.Time, key.Value.y);
                            cz.AddKey(key.Time, key.Value.z);
                        }
                        clip.SetCurve(path, typeof(Transform), "localPosition.x", cx);
                        clip.SetCurve(path, typeof(Transform), "localPosition.y", cy);
                        clip.SetCurve(path, typeof(Transform), "localPosition.z", cz);
                    }

                    // Rotation curve (quaternion x, y, z, w)
                    if (animNode.RotationKeys.Count > 0)
                    {
                        var qx = new AnimationCurve();
                        var qy = new AnimationCurve();
                        var qz = new AnimationCurve();
                        var qw = new AnimationCurve();
                        foreach (var key in animNode.RotationKeys)
                        {
                            qx.AddKey(key.Time, key.Value.x);
                            qy.AddKey(key.Time, key.Value.y);
                            qz.AddKey(key.Time, key.Value.z);
                            qw.AddKey(key.Time, key.Value.w);
                        }
                        clip.SetCurve(path, typeof(Transform), "localRotation.x", qx);
                        clip.SetCurve(path, typeof(Transform), "localRotation.y", qy);
                        clip.SetCurve(path, typeof(Transform), "localRotation.z", qz);
                        clip.SetCurve(path, typeof(Transform), "localRotation.w", qw);
                    }
                }

                anim.AddClip(clip, kAnim.Name);

                // Designate "idle" as the default clip
                if (kAnim.Name.Equals("pause1", StringComparison.OrdinalIgnoreCase) ||
                    kAnim.Name.Equals("idle",   StringComparison.OrdinalIgnoreCase))
                {
                    anim.clip = clip;
                }
            }

            // Play default clip
            if (anim.clip != null)
                anim.Play(anim.clip.name);

            // ── Attach AnimatorBridge so callers can use the high-level API ──
            // Collect clips into a dictionary for AnimatorBridge
            var clipDict = new Dictionary<string, AnimationClip>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var s in anim)
            {
                // AnimationState enumerator returns AnimationState objects
                var state = s as AnimationState;
                if (state != null && state.clip != null)
                    clipDict[state.name] = state.clip;
            }

            var bridge = root.AddComponent<AnimatorBridge>();
            bridge.LoadClips(clipDict);
        }

        /// <summary>
        /// Returns the relative Transform path from root to the named child,
        /// e.g. "Body/Head". Returns null if not found.
        /// </summary>
        private static string FindNodePath(Transform root, string name)
        {
            var tf = FindRecursive(root, name);
            if (tf == null || tf == root) return null;

            var parts = new System.Text.StringBuilder();
            var current = tf;
            while (current != root && current != null)
            {
                if (parts.Length > 0) parts.Insert(0, "/");
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return parts.ToString();
        }

        private static Transform FindRecursive(Transform t, string name)
        {
            if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return t;
            foreach (Transform c in t)
            {
                var found = FindRecursive(c, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
