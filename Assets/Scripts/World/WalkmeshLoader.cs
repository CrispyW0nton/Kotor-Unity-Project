using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.KotOR.Parsers;
using KotORUnity.Core;

namespace KotORUnity.World
{
    /// <summary>
    /// Parses KotOR Walkmesh (.wok) files and builds Unity MeshColliders.
    /// Optionally bakes a NavMesh surface on the resulting geometry for AI pathfinding.
    ///
    /// Walkmesh geometry defines every walkable (and blocking) surface in a
    /// KotOR area.  The raw binary is structurally close to a KotOR MDL mesh:
    ///
    ///   Header (48 bytes):
    ///     char[8]  FileType   "BWM V1.0"
    ///     uint32   WalkType   0=area, 1=placeable, 2=door
    ///     float[3] RelUsePos1
    ///     float[3] RelUsePos2
    ///     float[3] AbsUsePos1
    ///     float[3] AbsUsePos2  (not always present; guarded by length check)
    ///
    ///   Vertex block:
    ///     uint32   VertCount
    ///     float[3] verts[VertCount]
    ///
    ///   Face block:
    ///     uint32   FaceCount
    ///     uint32[3] indices[FaceCount]
    ///     uint32   walkableFlags[FaceCount]  (bit 0 = walkable)
    ///     int32    adjacency[FaceCount * 3]  (-1 = no neighbour)
    ///     ... (normals, AABBs — we skip these for collision)
    ///
    /// Note: Axes — KotOR uses Y-up with X/Z horizontal; Unity also uses Y-up,
    ///       but KotOR's Z is Unity's Y and vice-versa for some export paths.
    ///       We keep KotOR native coordinates and let the root transform handle
    ///       any scene-level rotation.
    /// </summary>
    public static class WalkmeshLoader
    {
        // ── SURFACE TYPES ─────────────────────────────────────────────────────
        // KotOR walkmesh face material flags (bits in the walk flag uint)
        public enum WalkSurface
        {
            Walkable     = 0,
            Dirt         = 1,
            Grass        = 2,
            Stone        = 3,
            Wood         = 4,
            Water        = 5,
            NonWalkable  = 7,
            Transparent  = 8,
            Carpet       = 9,
            Metal        = 10,
            Puddles      = 11,
            Swamp        = 12,
            Mud          = 13,
            Leaves       = 14,
            Lava         = 15,
            BottomlessPit= 16,
            DeepWater    = 17,
            Door         = 18,
            Snow         = 19,
            Sand         = 20
        }

        // Surfaces the player can stand on
        private static readonly System.Collections.Generic.HashSet<int> _walkableTypes
            = new System.Collections.Generic.HashSet<int>
        {
            0,1,2,3,4,5,9,10,11,12,13,14,19,20
        };

        // ── PUBLIC ENTRY POINT ────────────────────────────────────────────────
        /// <summary>
        /// Parse a .wok byte array and create a child GameObject with a
        /// MeshCollider under <paramref name="parent"/>.
        /// Returns the created GameObject, or null on parse failure.
        /// </summary>
        public static GameObject BuildCollider(byte[] wokData, Transform parent,
                                               string name = "Walkmesh",
                                               bool walkableOnly = true)
        {
            if (wokData == null || wokData.Length < 48)
            {
                Debug.LogWarning("[WalkmeshLoader] WOK data too short.");
                return null;
            }

            var result = Parse(wokData, walkableOnly);
            if (result == null) return null;

            // Build Unity Mesh
            var mesh = new Mesh { name = name };
            mesh.SetVertices(result.Vertices);
            mesh.SetTriangles(result.Indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create GameObject
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("Walkmesh");   // create this layer in Unity

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
                                MeshColliderCookingOptions.WeldColocatedVertices;

            // Optional visual (transparent in editor only)
#if UNITY_EDITOR
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader == null)
                mat = new Material(Shader.Find("Standard"));
            var c = Color.green; c.a = 0.15f;
            mat.color = c;
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3);
                mat.renderQueue = 3000;
            }
            mr.sharedMaterial = mat;
            mr.enabled = false; // hidden by default
#endif

            return go;
        }

        /// <summary>
        /// Build a walkmesh collider AND immediately bake a Unity NavMesh on it
        /// so AI agents can path-find over it.
        ///
        /// <para>
        /// NavMesh baking is performed via Unity's internal NavMesh.AddNavMeshData
        /// approach (works at runtime without NavMeshSurface component, which
        /// requires the AI Navigation package).
        /// </para>
        ///
        /// Returns the created walkmesh GameObject (NavMeshData baked as a side-effect).
        /// </summary>
        public static GameObject BuildColliderAndBakeNavMesh(
            byte[] wokData, Transform parent,
            string name      = "Walkmesh",
            bool   walkableOnly = true,
            float  agentRadius  = 0.3f,
            float  agentHeight  = 1.8f,
            float  maxSlope     = 45f,
            float  stepHeight   = 0.4f)
        {
            var go = BuildCollider(wokData, parent, name, walkableOnly);
            if (go == null) return null;

            BakeNavMeshOnGameObject(go, agentRadius, agentHeight, maxSlope, stepHeight);
            return go;
        }

        /// <summary>
        /// Bakes (or re-bakes) a Unity NavMesh surface using the MeshFilter on
        /// <paramref name="walkmeshGO"/> at runtime.
        /// </summary>
        public static void BakeNavMeshOnGameObject(GameObject walkmeshGO,
                                                   float agentRadius = 0.3f,
                                                   float agentHeight = 1.8f,
                                                   float maxSlope    = 45f,
                                                   float stepHeight  = 0.4f)
        {
            if (walkmeshGO == null) return;

            var mf = walkmeshGO.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("[WalkmeshLoader] BakeNavMesh: no MeshFilter found.");
                return;
            }

            // Build a NavMeshBuildSource from the walkmesh mesh
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape     = NavMeshBuildSourceShape.Mesh,
                    sourceData = mf.sharedMesh,
                    transform = walkmeshGO.transform.localToWorldMatrix,
                    area      = 0  // Walkable
                }
            };

            // Derive bounds from the mesh
            var bounds    = mf.sharedMesh.bounds;
            var worldCenter = walkmeshGO.transform.TransformPoint(bounds.center);
            var buildBounds = new Bounds(worldCenter,
                walkmeshGO.transform.TransformVector(bounds.size) + Vector3.one * 2f);

            var settings = new NavMeshBuildSettings
            {
                agentRadius        = agentRadius,
                agentHeight        = agentHeight,
                agentSlope         = maxSlope,
                agentClimb         = stepHeight,
                minRegionArea      = 0.1f,
                overrideTileSize   = 0,
                overrideVoxelSize  = 0
            };

            var navMeshData = NavMeshBuilder.BuildNavMeshData(
                settings, sources, buildBounds,
                walkmeshGO.transform.position, walkmeshGO.transform.rotation);

            if (navMeshData == null)
            {
                Debug.LogWarning("[WalkmeshLoader] NavMesh build returned null.");
                return;
            }

            // Register and track on the walkmesh GO via a helper component
            var tracker = walkmeshGO.AddComponent<NavMeshDataTracker>();
            tracker.Initialise(navMeshData);

            Debug.Log($"[WalkmeshLoader] NavMesh baked for '{walkmeshGO.name}' " +
                      $"(vertices: {mf.sharedMesh.vertexCount}).");
        }

        // ── PARSE ─────────────────────────────────────────────────────────────
        public class WalkmeshData
        {
            public List<Vector3> Vertices = new List<Vector3>();
            public List<int>     Indices  = new List<int>();
            public List<int>     SurfaceFlags = new List<int>();
        }

        public static WalkmeshData Parse(byte[] data, bool walkableOnly = true)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);

                // ── Header ────────────────────────────────────────────────────
                string fileType = Encoding.ASCII.GetString(br.ReadBytes(4)).TrimEnd('\0');
                string version  = Encoding.ASCII.GetString(br.ReadBytes(4)).TrimEnd('\0');
                // "BWM " + "V1.0"
                if (!fileType.StartsWith("BWM"))
                {
                    Debug.LogWarning($"[WalkmeshLoader] Unexpected file type: '{fileType}'");
                }

                uint walkType = br.ReadUInt32();

                // Skip use positions (6 float[3] = 24 bytes or 4 float[3]=12 bytes)
                // Safe skip: read until vertices
                // Positions of next blocks are NOT stored in header; we use fixed layout:
                // After 4+4+4 = 12 bytes header → relative use pos1 (12), pos2 (12) = 24 bytes
                br.ReadBytes(24);  // RelUsePos1 + RelUsePos2

                // ── Vertices ──────────────────────────────────────────────────
                uint vertCount = br.ReadUInt32();
                var verts = new Vector3[vertCount];
                for (uint i = 0; i < vertCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    // KotOR→Unity: swap Y/Z
                    verts[i] = new Vector3(x, z, y);
                }

                // ── Faces ─────────────────────────────────────────────────────
                uint faceCount = br.ReadUInt32();
                var faceIndices = new int[faceCount * 3];
                for (uint i = 0; i < faceCount * 3; i++)
                    faceIndices[i] = (int)br.ReadUInt32();

                var walkFlags = new uint[faceCount];
                for (uint i = 0; i < faceCount; i++)
                    walkFlags[i] = br.ReadUInt32();

                // Build output
                var result = new WalkmeshData();
                // Add all vertices (we index selectively below)
                result.Vertices.AddRange(verts);

                for (uint f = 0; f < faceCount; f++)
                {
                    int surfType = (int)(walkFlags[f] & 0x1F);
                    if (walkableOnly && !_walkableTypes.Contains(surfType)) continue;

                    result.Indices.Add(faceIndices[f * 3 + 0]);
                    result.Indices.Add(faceIndices[f * 3 + 1]);
                    result.Indices.Add(faceIndices[f * 3 + 2]);
                    result.SurfaceFlags.Add(surfType);
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WalkmeshLoader] Parse error: {e.Message}");
                return null;
            }
        }

        // ── QUERY ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Simple ground-height query via a downward raycast from above <paramref name="worldPos"/>.
        /// Requires the walkmesh collider to exist and be on the "Walkmesh" layer.
        /// </summary>
        public static bool TryGetGroundHeight(Vector3 worldPos, out float height,
                                              float castFrom = 50f)
        {
            height = 0f;
            int layer = LayerMask.GetMask("Walkmesh");
            if (layer == 0) return false;

            var origin = new Vector3(worldPos.x, castFrom, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castFrom + 10f, layer))
            {
                height = hit.point.y;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether a world position is within the walkable NavMesh.
        /// Returns true if a nearest NavMesh point is within <paramref name="maxDistance"/>.
        /// </summary>
        public static bool IsOnNavMesh(Vector3 worldPos, float maxDistance = 1f)
        {
            return NavMesh.SamplePosition(worldPos, out _, maxDistance, NavMesh.AllAreas);
        }

        /// <summary>
        /// Returns the nearest walkable NavMesh position to <paramref name="worldPos"/>.
        /// </summary>
        public static bool SnapToNavMesh(Vector3 worldPos, out Vector3 snapped, float maxDistance = 2f)
        {
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                snapped = hit.position;
                return true;
            }
            snapped = worldPos;
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  NAV MESH DATA TRACKER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Added to a walkmesh GameObject at runtime to own and remove the baked
    /// NavMeshData when the area is unloaded.
    /// </summary>
    public class NavMeshDataTracker : MonoBehaviour
    {
        private NavMeshDataInstance _instance;

        public void Initialise(NavMeshData data)
        {
            _instance = NavMesh.AddNavMeshData(data);
            Debug.Log($"[NavMeshDataTracker] NavMeshData registered for '{gameObject.name}'.");
        }

        private void OnDestroy()
        {
            NavMesh.RemoveNavMeshData(_instance);
            Debug.Log($"[NavMeshDataTracker] NavMeshData removed for '{gameObject.name}'.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  WALKMESH COMPONENT  —  MonoBehaviour wrapper for designer-friendly setup
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drop this onto any area GameObject that has a MeshFilter/MeshRenderer.
    /// On Awake it automatically bakes the NavMesh and sets up the physics collider.
    ///
    /// For KotOR BWM files loaded at runtime via WalkmeshLoader, use
    ///   WalkmeshComponent.BuildFromRawBytes(data, parent).
    /// </summary>
    public class WalkmeshComponent : MonoBehaviour
    {
        [Header("NavMesh Bake Settings")]
        [Tooltip("Radius used for the NavMesh agent (default 0.3 m).")]
        [SerializeField] public float AgentRadius  = 0.3f;
        [Tooltip("Height used for the NavMesh agent (default 1.8 m).")]
        [SerializeField] public float AgentHeight  = 1.8f;
        [Tooltip("Maximum walkable slope in degrees.")]
        [SerializeField] public float MaxSlope     = 45f;
        [Tooltip("Maximum step height (stair climbing).")]
        [SerializeField] public float StepHeight   = 0.4f;
        [Tooltip("If true, bakes NavMesh automatically on Awake.")]
        [SerializeField] public bool  AutoBake     = true;
        [Tooltip("If true, makes this mesh invisible (used as an invisible collision hull).")]
        [SerializeField] public bool  InvisibleCollider = true;

        [Header("Status (Read-Only)")]
        [SerializeField] private bool _baked;
        public bool IsBaked => _baked;

        private void Awake()
        {
            if (InvisibleCollider)
            {
                var rend = GetComponent<Renderer>();
                if (rend != null) rend.enabled = false;
            }

            // Ensure a MeshCollider is present for physics
            var mc = GetComponent<MeshCollider>();
            if (mc == null)
            {
                mc = gameObject.AddComponent<MeshCollider>();
                var mf = GetComponent<MeshFilter>();
                if (mf != null) mc.sharedMesh = mf.sharedMesh;
            }

            if (AutoBake) Bake();
        }

        /// <summary>Bake (or re-bake) the NavMesh for this walkmesh object.</summary>
        public void Bake()
        {
            WalkmeshLoader.BakeNavMeshOnGameObject(gameObject, AgentRadius, AgentHeight, MaxSlope, StepHeight);
            _baked = true;

            EventBus.Publish(EventBus.EventType.NavMeshBaked,
                new KotORUnity.Core.EventBus.GameEventArgs { StringValue = gameObject.name });
        }

        /// <summary>
        /// Parse a KotOR BWM file and build a walkmesh + bake NavMesh.
        /// Returns the created GameObject with WalkmeshComponent.
        /// </summary>
        public static WalkmeshComponent BuildFromRawBytes(
            byte[]     bwmData,
            Transform  parent,
            string     name           = "Walkmesh",
            bool       walkableOnly   = true,
            float      agentRadius    = 0.3f,
            float      agentHeight    = 1.8f)
        {
            var go = WalkmeshLoader.BuildColliderAndBakeNavMesh(
                bwmData, parent, name, walkableOnly, agentRadius, agentHeight);

            if (go == null) return null;

            // Attach component for runtime management
            var comp = go.GetComponent<WalkmeshComponent>() ?? go.AddComponent<WalkmeshComponent>();
            comp.AgentRadius = agentRadius;
            comp.AgentHeight = agentHeight;
            comp.AutoBake    = false;  // already baked above
            comp._baked      = true;
            return comp;
        }
    }
}

