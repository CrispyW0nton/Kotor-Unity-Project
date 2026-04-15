using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.KotOR.Parsers
{
    /// <summary>
    /// Parses KotOR binary MDL + MDX model files into Unity Meshes,
    /// GameObjects, and AnimationClips.
    ///
    /// BINARY LAYOUT (little-endian)
    /// ─────────────────────────────
    /// MDL Header  (12 bytes)
    ///   uint32  flags           (bit 0 = geometry present)
    ///   uint32  mdlDataSize
    ///   uint32  mdxDataSize
    ///
    /// Geometry Header  (80 bytes @ offset 12)
    ///   fn_ptr  p_RenderMesh    (ignored — PC values differ)
    ///   fn_ptr  p_Unknown
    ///   char[32] modelName
    ///   uint32  rootNodeOffset  (from start of MDL data section, i.e. offset 12)
    ///   uint32  nodeCount
    ///   ... (spare fields)
    ///   uint8   geometryType    (1=model, 2=walkmesh)
    ///
    /// Model Header  (extends Geometry Header, total 228 bytes @ offset 12)
    ///   uint8   classification (0=invalid,1=effect,2=tile,4=character,8=door)
    ///   uint8   fogged
    ///   uint16  pad
    ///   uint32  animCount / animOffset  (array of uint32 offsets into MDL)
    ///   uint32  superModelRef           (name, 32 chars)
    ///   float   boundingRadius
    ///   float[3] bboxMin, bboxMax
    ///   float   radius
    ///   float   scale                  (usually 1.0)
    ///   char[64] superModelName
    ///   uint32  nameOffsetArray / nameCount
    ///   ... (more spare)
    ///
    /// NODE (variable size, layout depends on nodeType bitmask)
    ///   uint16  nodeType        bitmask:
    ///                             0x0001 = Header (always set)
    ///                             0x0002 = Light
    ///                             0x0004 = Emitter
    ///                             0x0008 = Camera
    ///                             0x0010 = Reference
    ///                             0x0020 = Mesh
    ///                             0x0040 = Skin
    ///                             0x0080 = Anim
    ///                             0x0100 = Dangly
    ///                             0x0200 = AABBMesh
    ///                             0x0400 = Saber (lightsaber blade)
    ///   uint16  nodeNumber
    ///   uint32  nameOffset      (from MDL data start)
    ///   uint32  pad
    ///   uint32  rootOffset      (from MDL data start)
    ///   uint32  parentOffset    (from MDL data start, 0 if root)
    ///   float[3] position
    ///   float[4] orientation    (quaternion xyzw)
    ///   uint32  childOffsetArray / childCount
    ///   uint32  controllerArray / controllerCount   (animation keyframes)
    ///   uint32  controllerDataArray / controllerDataCount
    ///
    /// MESH NODE (appended after node header)
    ///   ... (face arrays, vertex arrays, UV arrays, normals)
    ///   All geometry data lives in the MDX file at a given offset.
    ///
    /// References:
    ///   nwmax mdl format docs, KotorBlender source, xoreos source
    /// </summary>
    public static class MdlReader
    {
        // ── NODE TYPE FLAGS ───────────────────────────────────────────────────
        private const uint NODE_HEADER   = 0x0001;
        private const uint NODE_LIGHT    = 0x0002;
        private const uint NODE_EMITTER  = 0x0004;
        private const uint NODE_MESH     = 0x0020;
        private const uint NODE_SKIN     = 0x0040;
        private const uint NODE_ANIM     = 0x0080;
        private const uint NODE_DANGLY   = 0x0100;
        private const uint NODE_AABB     = 0x0200;
        private const uint NODE_SABER    = 0x0400;

        // ── MDL HEADER OFFSETS ────────────────────────────────────────────────
        private const int HDR_FLAGS        = 0;
        private const int HDR_MDL_SIZE     = 4;
        private const int HDR_MDX_SIZE     = 8;
        private const int MDL_DATA_START   = 12;  // geometry header begins here

        // geometry header (relative to MDL_DATA_START)
        private const int GEO_MODEL_NAME   = 8;   // char[32]
        private const int GEO_ROOT_OFFSET  = 40;  // uint32
        private const int GEO_NODE_COUNT   = 44;  // uint32
        private const int GEO_TYPE         = 56;  // uint8

        // model header (relative to MDL_DATA_START), after geometry header (80 bytes)
        private const int MOD_CLASSIFICATION = 80; // uint8
        private const int MOD_ANIM_ARRAY_DEF = 88; // two uint32: offset, count
        private const int MOD_SCALE          = 128; // float

        // ── PUBLIC RESULT ─────────────────────────────────────────────────────
        public class MdlModel
        {
            public string   Name;
            public float    Scale = 1f;
            public MdlNode  RootNode;
            public List<MdlAnimation> Animations = new List<MdlAnimation>();
            public byte     Classification; // 1=effect,2=tile,4=char,8=door
        }

        public class MdlNode
        {
            public string       Name;
            public uint         NodeType;
            public Vector3      Position;
            public Quaternion   Rotation;
            public MdlMesh      Mesh;       // null if not a mesh node
            public List<MdlNode> Children = new List<MdlNode>();

            public bool IsMesh  => (NodeType & NODE_MESH) != 0 || (NodeType & NODE_AABB) != 0;
            public bool IsSkin  => (NodeType & NODE_SKIN) != 0;
            public bool IsSaber => (NodeType & NODE_SABER) != 0;
        }

        public class MdlMesh
        {
            public Vector3[]  Vertices;
            public Vector3[]  Normals;
            public Vector2[]  UVs;
            public int[]      Triangles;
            public string     TextureName;  // resref (no extension)
            public string     Texture2Name; // lightmap resref
            public bool       IsTransparent;
            public float      Alpha;

            // ── Skin data (only populated for NODE_SKIN nodes) ─────────────────
            /// <summary>Per-vertex bone indices (up to 4 bones, index into BoneNames).</summary>
            public BoneWeight1[][] BoneWeights;  // BoneWeights[vertIdx] = array of up to 4 BoneWeight1
            /// <summary>Ordered list of bone node names referenced by this skin mesh.</summary>
            public string[]       BoneNames;
            /// <summary>Bind-pose inverse matrices, one per bone (BoneNames order).</summary>
            public Matrix4x4[]    BindPoses;
            public bool           IsSkinned => BoneNames != null && BoneNames.Length > 0;
        }

        public class MdlAnimation
        {
            public string Name;
            public float  Length;       // seconds
            public float  TransitionTime;
            public List<MdlAnimNode> Nodes = new List<MdlAnimNode>();
        }

        public class MdlAnimNode
        {
            public string            NodeName;
            /// <summary>Name of this node's parent (empty string for root nodes).</summary>
            public string            ParentName;
            public List<Vector3Key>  PositionKeys = new List<Vector3Key>();
            public List<QuatKey>     RotationKeys = new List<QuatKey>();
        }

        public struct Vector3Key { public float Time; public Vector3 Value; }
        public struct QuatKey    { public float Time; public Quaternion Value; }

        // ── PARSE ENTRY POINT ─────────────────────────────────────────────────
        /// <summary>
        /// Parse an MDL + MDX byte pair into a MdlModel.
        /// mdxData may be null for models with no geometry.
        /// </summary>
        public static MdlModel Parse(byte[] mdlData, byte[] mdxData)
        {
            if (mdlData == null || mdlData.Length < MDL_DATA_START + 80)
            {
                Debug.LogWarning("[MdlReader] MDL data too short.");
                return null;
            }

            try
            {
                using var ms  = new MemoryStream(mdlData);
                using var br  = new BinaryReader(ms);

                // ── File header ───────────────────────────────────────────────
                br.BaseStream.Seek(HDR_FLAGS, SeekOrigin.Begin);
                uint flags      = br.ReadUInt32();
                uint mdlSize    = br.ReadUInt32();
                uint mdxSize    = br.ReadUInt32();

                // ── Geometry header ───────────────────────────────────────────
                br.BaseStream.Seek(MDL_DATA_START + GEO_MODEL_NAME, SeekOrigin.Begin);
                string modelName = ReadFixedString(br, 32);

                br.BaseStream.Seek(MDL_DATA_START + GEO_ROOT_OFFSET, SeekOrigin.Begin);
                uint rootOffset = br.ReadUInt32();
                uint nodeCount  = br.ReadUInt32();

                br.BaseStream.Seek(MDL_DATA_START + GEO_TYPE, SeekOrigin.Begin);
                byte geoType = br.ReadByte();

                // ── Model header ──────────────────────────────────────────────
                br.BaseStream.Seek(MDL_DATA_START + MOD_CLASSIFICATION, SeekOrigin.Begin);
                byte classification = br.ReadByte();

                br.BaseStream.Seek(MDL_DATA_START + MOD_SCALE, SeekOrigin.Begin);
                float scale = br.ReadSingle();
                if (scale == 0f || float.IsNaN(scale)) scale = 1f;

                // Animation array pointer (offset + count stored as ArrayDef)
                br.BaseStream.Seek(MDL_DATA_START + MOD_ANIM_ARRAY_DEF, SeekOrigin.Begin);
                uint animOffset = br.ReadUInt32();
                uint animCount  = br.ReadUInt32();

                var model = new MdlModel
                {
                    Name           = modelName,
                    Scale          = scale,
                    Classification = classification
                };

                // ── Parse node tree ───────────────────────────────────────────
                if (rootOffset > 0 && rootOffset < mdlData.Length - MDL_DATA_START)
                {
                    model.RootNode = ParseNode(br, mdlData, mdxData,
                        MDL_DATA_START + (int)rootOffset, new HashSet<int>());
                }

                // ── Parse animations ──────────────────────────────────────────
                if (animCount > 0 && animOffset > 0)
                {
                    uint[] animOffsets = ReadArrayUInt32(br, mdlData,
                        MDL_DATA_START + (int)animOffset, animCount);
                    foreach (uint ao in animOffsets)
                    {
                        if (ao == 0 || ao + MDL_DATA_START >= mdlData.Length) continue;
                        var anim = ParseAnimation(br, mdlData, MDL_DATA_START + (int)ao);
                        if (anim != null) model.Animations.Add(anim);
                    }
                }

                return model;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MdlReader] Parse error: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        // ── NODE PARSER ───────────────────────────────────────────────────────
        private static MdlNode ParseNode(BinaryReader br, byte[] mdl, byte[] mdx,
                                          int absOffset, HashSet<int> visited)
        {
            if (absOffset <= 0 || absOffset >= mdl.Length - 80) return null;
            if (visited.Contains(absOffset)) return null;
            visited.Add(absOffset);

            br.BaseStream.Seek(absOffset, SeekOrigin.Begin);

            uint nodeType   = br.ReadUInt16();
            uint nodeNumber = br.ReadUInt16();
            uint nameOffset = br.ReadUInt32();
            br.ReadUInt32(); // pad
            br.ReadUInt32(); // rootNodeOffset (unused here)
            br.ReadUInt32(); // parentOffset

            float px = br.ReadSingle();
            float py = br.ReadSingle();
            float pz = br.ReadSingle();
            float rx = br.ReadSingle();
            float ry = br.ReadSingle();
            float rz = br.ReadSingle();
            float rw = br.ReadSingle();

            // children array: offset + count
            uint childArrayOffset = br.ReadUInt32();
            uint childCount       = br.ReadUInt32();
            br.ReadUInt32(); // allocation size (ignore)

            // controller arrays (skip for base node)
            br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();
            br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();

            // Resolve name
            string nodeName = "";
            if (nameOffset > 0 && MDL_DATA_START + nameOffset < mdl.Length)
            {
                br.BaseStream.Seek(MDL_DATA_START + nameOffset, SeekOrigin.Begin);
                nodeName = ReadNullTermString(br, 64);
            }

            // KotOR → Unity coordinate system: swap Y and Z, negate new Z
            var node = new MdlNode
            {
                Name     = nodeName,
                NodeType = nodeType,
                Position = new Vector3(px, pz, py),
                Rotation = new Quaternion(rx, rz, ry, rw)
            };

            // ── Mesh data ─────────────────────────────────────────────────────
            if ((nodeType & NODE_MESH) != 0 && (nodeType & NODE_ANIM) == 0)
            {
                // Mesh node header follows immediately after the base node (80 bytes in)
                int meshHeaderOffset = absOffset + 80;
                node.Mesh = ParseMeshNode(br, mdl, mdx, meshHeaderOffset);

                // If this is a skin node, parse the bone/weight data appended
                // after the standard mesh header (mesh header = 332 bytes).
                if ((nodeType & NODE_SKIN) != 0 && node.Mesh != null)
                {
                    ParseSkinData(br, mdl, node.Mesh, meshHeaderOffset);
                }
            }

            // ── Children ──────────────────────────────────────────────────────
            if (childCount > 0 && childArrayOffset > 0)
            {
                uint[] childOffsets = ReadArrayUInt32(br, mdl,
                    MDL_DATA_START + (int)childArrayOffset, childCount);
                foreach (uint co in childOffsets)
                {
                    if (co == 0) continue;
                    var child = ParseNode(br, mdl, mdx,
                        MDL_DATA_START + (int)co, visited);
                    if (child != null)
                        node.Children.Add(child);
                }
            }

            return node;
        }

        // ── MESH NODE PARSER ──────────────────────────────────────────────────
        private static MdlMesh ParseMeshNode(BinaryReader br, byte[] mdl,
                                              byte[] mdx, int offset)
        {
            if (offset <= 0 || offset + 332 > mdl.Length) return null;

            br.BaseStream.Seek(offset, SeekOrigin.Begin);

            // Mesh header fields (see xoreos / KotorBlender source for offsets)
            br.ReadBytes(8);  // function pointers (skip)

            // Face array definition
            uint faceOffset = br.ReadUInt32();
            uint faceCount  = br.ReadUInt32();
            br.ReadUInt32(); // allocation size

            // Bounding box
            float bboxMinX = br.ReadSingle(); float bboxMinY = br.ReadSingle(); float bboxMinZ = br.ReadSingle();
            float bboxMaxX = br.ReadSingle(); float bboxMaxY = br.ReadSingle(); float bboxMaxZ = br.ReadSingle();
            br.ReadSingle(); // radius

            // Average position
            br.ReadSingle(); br.ReadSingle(); br.ReadSingle();

            // Textures (two slots, 32 chars each)
            string tex0 = ReadFixedString(br, 32);
            string tex1 = ReadFixedString(br, 32);

            // Vertex counts and MDX offsets
            uint vertCount     = br.ReadUInt32();
            uint texCount      = br.ReadUInt32();
            br.ReadBytes(4); // has lightmap
            br.ReadBytes(4); // render flag
            uint faceCount2    = br.ReadUInt32(); // same as faceCount, confirmatory

            // MDX data offsets (relative to start of MDX buffer)
            uint mdxDataFlags  = br.ReadUInt32();
            uint mdxVertOffset = br.ReadUInt32(); // offset into MDX for vertex positions
            uint mdxNormOffset = br.ReadUInt32(); // offset into MDX for normals (0xFFFFFFFF if absent)
            br.ReadUInt32(); // vertex colour offset
            uint mdxUV0Offset  = br.ReadUInt32(); // UV channel 0
            uint mdxUV1Offset  = br.ReadUInt32(); // UV channel 1 (lightmap)
            br.ReadBytes(8); // UV channels 2–3 (unused)
            uint mdxVertSize   = br.ReadUInt32(); // bytes per vertex in MDX (stride)

            br.ReadBytes(4); // unknown
            uint mdxOffset     = br.ReadUInt32(); // base offset in MDX for this mesh's data
            br.ReadBytes(4); // vertex offset array (KotOR1 only)

            uint vertCountB    = br.ReadUInt32(); // same as vertCount

            if (vertCount == 0 || faceCount == 0) return null;
            if (mdx == null) return null;

            // ── Read faces from MDL ───────────────────────────────────────────
            if (faceOffset == 0 || faceOffset + faceCount * 32 > mdl.Length - MDL_DATA_START)
                return null;

            br.BaseStream.Seek(MDL_DATA_START + faceOffset, SeekOrigin.Begin);
            var triangles = new List<int>();
            for (uint f = 0; f < faceCount; f++)
            {
                // Face: float[3] normal, float[1] dist, uint16[3] vertex_indices, uint16 material, uint16[3] neighbour_indices
                br.ReadBytes(16); // normal (12) + dist (4)
                ushort v0 = br.ReadUInt16();
                ushort v1 = br.ReadUInt16();
                ushort v2 = br.ReadUInt16();
                br.ReadUInt16(); // material
                br.ReadBytes(6); // neighbours
                triangles.Add(v0);
                triangles.Add(v2); // flip winding order KotOR→Unity
                triangles.Add(v1);
            }

            // ── Read vertices from MDX ────────────────────────────────────────
            if (mdxVertSize == 0) mdxVertSize = 32; // safe default
            uint mdxBase = mdxOffset;

            var vertices = new Vector3[vertCount];
            var normals  = new Vector3[vertCount];
            var uvs      = new Vector2[vertCount];
            bool hasNormals = mdxNormOffset != 0xFFFFFFFF;
            bool hasUV0     = mdxUV0Offset  != 0xFFFFFFFF;

            for (uint v = 0; v < vertCount; v++)
            {
                uint vertBase = mdxBase + v * mdxVertSize;

                // Position
                if (vertBase + mdxVertOffset + 12 <= mdx.Length)
                {
                    float vx = BitConverter.ToSingle(mdx, (int)(vertBase + mdxVertOffset));
                    float vy = BitConverter.ToSingle(mdx, (int)(vertBase + mdxVertOffset + 4));
                    float vz = BitConverter.ToSingle(mdx, (int)(vertBase + mdxVertOffset + 8));
                    vertices[v] = new Vector3(vx, vz, vy); // KotOR→Unity
                }

                // Normal
                if (hasNormals && vertBase + mdxNormOffset + 12 <= mdx.Length)
                {
                    float nx = BitConverter.ToSingle(mdx, (int)(vertBase + mdxNormOffset));
                    float ny = BitConverter.ToSingle(mdx, (int)(vertBase + mdxNormOffset + 4));
                    float nz = BitConverter.ToSingle(mdx, (int)(vertBase + mdxNormOffset + 8));
                    normals[v] = new Vector3(nx, nz, ny);
                }

                // UV
                if (hasUV0 && vertBase + mdxUV0Offset + 8 <= mdx.Length)
                {
                    float u = BitConverter.ToSingle(mdx, (int)(vertBase + mdxUV0Offset));
                    float w = BitConverter.ToSingle(mdx, (int)(vertBase + mdxUV0Offset + 4));
                    uvs[v] = new Vector2(u, 1f - w); // flip V axis
                }
            }

            return new MdlMesh
            {
                Vertices     = vertices,
                Normals      = !hasNormals ? null : normals,
                UVs          = !hasUV0    ? null : uvs,
                Triangles    = triangles.ToArray(),
                TextureName  = tex0.ToLowerInvariant(),
                Texture2Name = tex1.ToLowerInvariant(),
                Alpha        = 1f
            };
        }

        // ── SKIN NODE PARSER ─────────────────────────────────────────────────
        // KotOR skin node layout (appended after standard 332-byte mesh header):
        //   offset 332  — uint32  weights_offset   (into MDX per-vertex section)
        //   offset 336  — uint32  boneRefOffset     (array of uint32 bone node offsets)
        //   offset 340  — uint32  boneCount
        //   offset 344  — char[16][16] boneNames    (or separate name list)
        //   In MDX each vertex has:  4 × float bone_indices, 4 × float bone_weights
        //   (immediately following the standard position/normal/uv block)
        private static void ParseSkinData(BinaryReader br, byte[] mdl,
                                          MdlMesh mesh, int meshHeaderOffset)
        {
            try
            {
                // Read bone count and name-list offset from skin extension header
                int skinOffset = meshHeaderOffset + 332;
                if (skinOffset + 20 > mdl.Length) return;

                br.BaseStream.Seek(skinOffset, SeekOrigin.Begin);
                uint weightsOffs = br.ReadUInt32(); // MDX offset for weight data
                uint boneRefOffs = br.ReadUInt32(); // MDL offset for bone ref array
                uint boneCount   = br.ReadUInt32();

                if (boneCount == 0 || boneCount > 128) return;

                // ── Read bone names ───────────────────────────────────────────
                var boneNames = new string[boneCount];
                if (boneRefOffs > 0 && MDL_DATA_START + boneRefOffs + boneCount * 4 <= mdl.Length)
                {
                    // Each entry is a uint32 offset from MDL_DATA_START to a node header
                    // from which we read the node name (offset +8 in the node header).
                    br.BaseStream.Seek(MDL_DATA_START + boneRefOffs, SeekOrigin.Begin);
                    uint[] boneNodeOffsets = new uint[boneCount];
                    for (int i = 0; i < boneCount; i++)
                        boneNodeOffsets[i] = br.ReadUInt32();

                    for (int i = 0; i < boneCount; i++)
                    {
                        uint bno = boneNodeOffsets[i];
                        if (bno == 0 || MDL_DATA_START + bno + 8 >= mdl.Length) continue;
                        // Node header: uint16 type, uint16 number, uint32 nameOffset
                        br.BaseStream.Seek(MDL_DATA_START + bno + 4, SeekOrigin.Begin);
                        uint nameOff = br.ReadUInt32();
                        if (nameOff > 0 && MDL_DATA_START + nameOff < mdl.Length)
                        {
                            br.BaseStream.Seek(MDL_DATA_START + nameOff, SeekOrigin.Begin);
                            boneNames[i] = ReadNullTermString(br, 64);
                        }
                        if (string.IsNullOrEmpty(boneNames[i]))
                            boneNames[i] = $"bone_{i}";
                    }
                }
                else
                {
                    for (int i = 0; i < boneCount; i++) boneNames[i] = $"bone_{i}";
                }

                mesh.BoneNames = boneNames;

                // ── Read per-vertex weights from MDX ──────────────────────────
                // Each vertex in a skin mesh has an extra 32 bytes in MDX:
                //   4 × float boneIndices  (as floats; cast to int for index)
                //   4 × float boneWeights
                // This block starts at mdxOffset + vertIndex * mdxVertSize,
                // but the weight/index data is at a fixed offset within the stride.
                // We read it from mdx using the weightsOffs as the offset within
                // each vertex block (relative to vertex start = mdxOffset + v * mdxVertSize).
                if (mesh.Vertices == null) return;
                int vertCount = mesh.Vertices.Length;
                var boneWeightArray = new BoneWeight1[vertCount][];

                // Locate the per-vertex skin data in MDX.
                // For KotOR, skin vertices in MDX are laid out as:
                //   [position(12)] [normal(12)] [uv(8)] [boneWeights(16)] [boneIndices(16)]
                // so boneWeight data starts at vertex_base + 32, index at vertex_base + 48.
                // The mesh parser already computed mdxBase + mdxVertSize in ParseMeshNode;
                // we re-derive it here using the standard skin stride = 64 bytes.
                // (Actual stride is in mdxVertSize which we'd need to re-read; use 64 as default.)
                int skinStride = 64; // standard for character skins
                // weightsOffs encodes the per-vertex MDX starting offset (= mdxBase of this mesh)
                uint mdxBase2 = weightsOffs;

                for (int v = 0; v < vertCount; v++)
                {
                    uint vBase = mdxBase2 + (uint)(v * skinStride);
                    boneWeightArray[v] = new BoneWeight1[4];
                    for (int b = 0; b < 4; b++)
                    {
                        uint wOff  = vBase + 32 + (uint)(b * 4); // weights at +32
                        uint iOff  = vBase + 48 + (uint)(b * 4); // indices at +48
                        float w = 0f;
                        int   idx = 0;
                        if (wOff + 4 <= mdl.Length)
                            w = BitConverter.ToSingle(mdl, (int)wOff);
                        if (iOff + 4 <= mdl.Length)
                            idx = Mathf.Clamp((int)BitConverter.ToSingle(mdl, (int)iOff),
                                              0, (int)boneCount - 1);
                        boneWeightArray[v][b] = new BoneWeight1
                        {
                            boneIndex = idx,
                            weight    = w
                        };
                    }
                }

                mesh.BoneWeights = boneWeightArray;

                // Build identity bind poses (refined when building Unity SkinnedMeshRenderer)
                mesh.BindPoses = new Matrix4x4[boneCount];
                for (int i = 0; i < boneCount; i++)
                    mesh.BindPoses[i] = Matrix4x4.identity;

                Debug.Log($"[MdlReader] Skin parsed: {boneCount} bones, {vertCount} weighted verts.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MdlReader] Skin parse failed: {ex.Message}");
            }
        }

        // ── ANIMATION PARSER ──────────────────────────────────────────────────
        private static MdlAnimation ParseAnimation(BinaryReader br,
                                                    byte[] mdl, int absOffset)
        {
            if (absOffset <= 0 || absOffset + 100 > mdl.Length) return null;

            // Animation header starts with geometry header (80 bytes)
            br.BaseStream.Seek(absOffset + GEO_MODEL_NAME, SeekOrigin.Begin);
            string animName = ReadFixedString(br, 32);

            br.BaseStream.Seek(absOffset + GEO_ROOT_OFFSET, SeekOrigin.Begin);
            uint rootOff   = br.ReadUInt32();
            uint nodeCount = br.ReadUInt32();

            // Animation-specific header fields (after 80-byte geometry header)
            br.BaseStream.Seek(absOffset + 80, SeekOrigin.Begin);
            float length         = br.ReadSingle();
            float transitionTime = br.ReadSingle();

            var anim = new MdlAnimation
            {
                Name           = animName,
                Length         = length,
                TransitionTime = transitionTime
            };

            // Parse animation node tree to extract controller keyframes
            if (rootOff > 0)
            {
                CollectAnimNodes(br, mdl, MDL_DATA_START + (int)rootOff,
                    anim.Nodes, new HashSet<int>());
            }

            return anim;
        }

        private static void CollectAnimNodes(BinaryReader br, byte[] mdl,
            int absOffset, List<MdlAnimNode> nodes, HashSet<int> visited,
            string parentNodeName = "")
        {
            if (absOffset <= 0 || absOffset >= mdl.Length - 80) return;
            if (visited.Contains(absOffset)) return;
            visited.Add(absOffset);

            br.BaseStream.Seek(absOffset, SeekOrigin.Begin);
            uint nodeType   = br.ReadUInt16();
            uint nodeNumber = br.ReadUInt16();
            uint nameOffset = br.ReadUInt32();
            br.ReadUInt32(); // pad
            br.ReadUInt32(); // rootNodeOffset
            br.ReadUInt32(); // parentOffset

            // Skip position/rotation (12 + 16 bytes)
            br.ReadBytes(28);

            // Children
            uint childArrayOffset = br.ReadUInt32();
            uint childCount       = br.ReadUInt32();
            br.ReadUInt32();

            // Controller arrays
            uint ctrlOffset = br.ReadUInt32();
            uint ctrlCount  = br.ReadUInt32();
            br.ReadUInt32();
            uint ctrlDataOffset = br.ReadUInt32();
            uint ctrlDataCount  = br.ReadUInt32();
            br.ReadUInt32();

            string nodeName = "";
            if (nameOffset > 0 && MDL_DATA_START + nameOffset < mdl.Length)
            {
                long saved = br.BaseStream.Position;
                br.BaseStream.Seek(MDL_DATA_START + nameOffset, SeekOrigin.Begin);
                nodeName = ReadNullTermString(br, 64);
                br.BaseStream.Seek(saved, SeekOrigin.Begin);
            }

            var animNode = new MdlAnimNode { NodeName = nodeName, ParentName = parentNodeName };

            // Parse controllers (keyframe data)
            if (ctrlCount > 0 && ctrlOffset > 0 && ctrlDataOffset > 0)
            {
                ParseControllers(br, mdl,
                    MDL_DATA_START + (int)ctrlOffset, ctrlCount,
                    MDL_DATA_START + (int)ctrlDataOffset, ctrlDataCount,
                    animNode);
            }

            if (animNode.PositionKeys.Count > 0 || animNode.RotationKeys.Count > 0)
                nodes.Add(animNode);

            // Recurse children
            if (childCount > 0 && childArrayOffset > 0)
            {
                uint[] childOffsets = ReadArrayUInt32(br, mdl,
                    MDL_DATA_START + (int)childArrayOffset, childCount);
                foreach (uint co in childOffsets)
                {
                    if (co == 0) continue;
                    CollectAnimNodes(br, mdl, MDL_DATA_START + (int)co,
                        nodes, visited, parentNodeName: nodeName);
                }
            }
        }

        // Controller types (KotOR NWN values)
        private const uint CTRL_POSITION   = 8;
        private const uint CTRL_ORIENTATION = 20;
        private const uint CTRL_SCALE      = 36;

        private static void ParseControllers(BinaryReader br, byte[] mdl,
            int ctrlAbsOffset, uint ctrlCount,
            int dataAbsOffset, uint dataCount,
            MdlAnimNode node)
        {
            for (uint i = 0; i < ctrlCount; i++)
            {
                br.BaseStream.Seek(ctrlAbsOffset + i * 16, SeekOrigin.Begin);
                uint   ctrlType   = br.ReadUInt32();
                ushort rowCount   = br.ReadUInt16();
                ushort timeStart  = br.ReadUInt16(); // first index in data array
                ushort dataStart  = br.ReadUInt16(); // first data index
                byte   colCount   = br.ReadByte();
                br.ReadByte();                        // pad

                int timeBase = dataAbsOffset + timeStart * 4;
                int dataBase = dataAbsOffset + dataStart * 4;

                for (int r = 0; r < rowCount; r++)
                {
                    int timeIdx = timeBase + r * 4;
                    if (timeIdx + 4 > mdl.Length) break;
                    float t = BitConverter.ToSingle(mdl, timeIdx);

                    if (ctrlType == CTRL_POSITION && colCount >= 3)
                    {
                        int di = dataBase + r * colCount * 4;
                        if (di + 12 > mdl.Length) break;
                        float x = BitConverter.ToSingle(mdl, di);
                        float y = BitConverter.ToSingle(mdl, di + 4);
                        float z = BitConverter.ToSingle(mdl, di + 8);
                        node.PositionKeys.Add(new Vector3Key
                            { Time = t, Value = new Vector3(x, z, y) });
                    }
                    else if (ctrlType == CTRL_ORIENTATION && colCount >= 4)
                    {
                        int di = dataBase + r * colCount * 4;
                        if (di + 16 > mdl.Length) break;
                        float x = BitConverter.ToSingle(mdl, di);
                        float y = BitConverter.ToSingle(mdl, di + 4);
                        float z = BitConverter.ToSingle(mdl, di + 8);
                        float w = BitConverter.ToSingle(mdl, di + 12);
                        node.RotationKeys.Add(new QuatKey
                            { Time = t, Value = new Quaternion(x, z, y, w) });
                    }
                }
            }
        }

        // ── BINARY HELPERS ────────────────────────────────────────────────────
        private static string ReadFixedString(BinaryReader br, int length)
        {
            byte[] bytes = br.ReadBytes(length);
            int end = Array.IndexOf(bytes, (byte)0);
            return Encoding.ASCII.GetString(bytes, 0, end < 0 ? length : end);
        }

        private static string ReadNullTermString(BinaryReader br, int maxLen)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < maxLen; i++)
            {
                if (br.BaseStream.Position >= br.BaseStream.Length) break;
                byte b = br.ReadByte();
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static uint[] ReadArrayUInt32(BinaryReader br, byte[] mdl,
                                               int absOffset, uint count)
        {
            var result = new uint[count];
            if (absOffset <= 0 || absOffset + count * 4 > mdl.Length) return result;
            br.BaseStream.Seek(absOffset, SeekOrigin.Begin);
            for (uint i = 0; i < count; i++)
                result[i] = br.ReadUInt32();
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MDL BUILDER  —  converts MdlModel into a Unity GameObject hierarchy
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Converts a parsed MdlModel into a Unity GameObject with:
    ///   - MeshFilter + MeshRenderer on every mesh node
    ///   - Correct parent/child transform hierarchy
    ///   - AnimationClips stored on an Animator
    /// Call Build() then optionally ApplyTextures() once textures are loaded.
    /// </summary>
    public static class MdlBuilder
    {
        /// <summary>Build a GameObject hierarchy from a parsed model.</summary>
        public static GameObject Build(MdlReader.MdlModel model,
                                       Transform parent = null,
                                       Material defaultMaterial = null)
        {
            if (model == null) return null;

            var root = new GameObject(model.Name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * model.Scale;

            if (model.RootNode != null)
                BuildNode(model.RootNode, root.transform, defaultMaterial);

            return root;
        }

        private static void BuildNode(MdlReader.MdlNode node,
                                       Transform parent,
                                       Material defaultMaterial)
        {
            var go = new GameObject(string.IsNullOrEmpty(node.Name) ? "node" : node.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = node.Position;
            go.transform.localRotation = node.Rotation;

            if (node.Mesh != null && node.Mesh.Vertices != null &&
                node.Mesh.Vertices.Length > 0 && node.Mesh.Triangles != null &&
                node.Mesh.Triangles.Length > 0)
            {
                var mat = defaultMaterial != null
                    ? defaultMaterial
                    : CreateDefaultMaterial(node.Mesh.TextureName);

                if (node.Mesh.IsSkinned)
                {
                    // Skinned mesh — we need bone Transforms.
                    // Collect bones by traversing the GO hierarchy for matching names.
                    BuildSkinnedMesh(node.Mesh, node.Name, go, parent, mat);
                }
                else
                {
                    // Static mesh
                    var mesh = BuildMesh(node.Mesh, node.Name);
                    var mf   = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    var mr   = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                }

                // Store texture name for later assignment
                go.AddComponent<PendingTextureTag>().TextureName = node.Mesh.TextureName;
            }

            foreach (var child in node.Children)
                BuildNode(child, go.transform, defaultMaterial);
        }

        /// <summary>
        /// Build a SkinnedMeshRenderer for a skin mesh node.
        /// Bone Transforms are looked up by name in the GO hierarchy.
        /// If not yet created (bones declared later in tree), stubs are created
        /// and the caller should call FinaliseSkinBones() after full tree is built.
        /// </summary>
        private static void BuildSkinnedMesh(MdlReader.MdlMesh meshData,
                                              string nodeName, GameObject go,
                                              Transform hierarchyRoot, Material mat)
        {
            var mesh   = BuildMesh(meshData, nodeName);

            // Apply bone weights
            var unity4 = new BoneWeight[meshData.Vertices.Length];
            if (meshData.BoneWeights != null)
            {
                for (int v = 0; v < unity4.Length; v++)
                {
                    var bw = meshData.BoneWeights[v];
                    if (bw == null || bw.Length == 0) continue;
                    if (bw.Length > 0) { unity4[v].boneIndex0 = bw[0].boneIndex; unity4[v].weight0 = bw[0].weight; }
                    if (bw.Length > 1) { unity4[v].boneIndex1 = bw[1].boneIndex; unity4[v].weight1 = bw[1].weight; }
                    if (bw.Length > 2) { unity4[v].boneIndex2 = bw[2].boneIndex; unity4[v].weight2 = bw[2].weight; }
                    if (bw.Length > 3) { unity4[v].boneIndex3 = bw[3].boneIndex; unity4[v].weight3 = bw[3].weight; }
                }
            }
            mesh.boneWeights = unity4;

            // Locate or create bone Transforms
            var boneTransforms = new Transform[meshData.BoneNames.Length];
            for (int i = 0; i < meshData.BoneNames.Length; i++)
            {
                string bn = meshData.BoneNames[i];
                var found = FindTransformByName(hierarchyRoot, bn);
                if (found == null)
                {
                    // Create placeholder bone GO (will be populated when tree is finished)
                    var boneGo = new GameObject(bn);
                    boneGo.transform.SetParent(hierarchyRoot, false);
                    found = boneGo.transform;
                }
                boneTransforms[i] = found;
            }

            // Bind poses
            var bindPoses = meshData.BindPoses ?? new Matrix4x4[meshData.BoneNames.Length];
            for (int i = 0; i < bindPoses.Length; i++)
            {
                if (bindPoses[i] == Matrix4x4.zero)
                    bindPoses[i] = boneTransforms[i].worldToLocalMatrix * hierarchyRoot.localToWorldMatrix;
            }
            mesh.bindposes = bindPoses;

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh     = mesh;
            smr.bones          = boneTransforms;
            smr.sharedMaterial = mat;
            smr.rootBone       = boneTransforms.Length > 0 ? boneTransforms[0] : go.transform;
        }

        /// <summary>Depth-first search for a child Transform with the given name.</summary>
        private static Transform FindTransformByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindTransformByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Mesh BuildMesh(MdlReader.MdlMesh data, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = data.Vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(data.Vertices);
            if (data.Normals != null) mesh.SetNormals(data.Normals);
            if (data.UVs    != null) mesh.SetUVs(0, data.UVs);
            mesh.SetTriangles(data.Triangles, 0);

            if (data.Normals == null) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        private static Material CreateDefaultMaterial(string texName)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = texName };
            return mat;
        }

        /// <summary>
        /// Build Unity AnimationClips from parsed animation data.
        /// Returns a dictionary of clipName → AnimationClip.
        /// </summary>
        public static Dictionary<string, AnimationClip> BuildAnimations(
            MdlReader.MdlModel model)
        {
            var clips = new Dictionary<string, AnimationClip>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var anim in model.Animations)
            {
                var clip = new AnimationClip
                {
                    name       = anim.Name,
                    frameRate  = 30f,
                    wrapMode   = anim.Name.ToLower().Contains("idle")
                                 ? WrapMode.Loop : WrapMode.Once
                };

                // Build a lookup so we can walk parent-chains and compute
                // relative Transform paths as expected by AnimationClip.
                var nodeByName = new Dictionary<string, MdlReader.MdlAnimNode>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var n in anim.Nodes)
                    if (!string.IsNullOrEmpty(n.NodeName))
                        nodeByName[n.NodeName] = n;

                foreach (var animNode in anim.Nodes)
                {
                    // Walk the parent chain to build the full relative path.
                    // Unity AnimationClip paths are forward-slash separated
                    // relative paths from the Animator root (i.e. the model root GO).
                    // Root-level nodes have an empty ParentName → path is just NodeName.
                    var parts = new System.Collections.Generic.Stack<string>();
                    parts.Push(animNode.NodeName);
                    string cur = animNode.ParentName;
                    int guard = 0;
                    while (!string.IsNullOrEmpty(cur) && guard++ < 64)
                    {
                        parts.Push(cur);
                        cur = nodeByName.TryGetValue(cur, out var pNode) ? pNode.ParentName : "";
                    }
                    string path = string.Join("/", parts);

                    // Position curves
                    if (animNode.PositionKeys.Count > 0)
                    {
                        var cx = new AnimationCurve();
                        var cy = new AnimationCurve();
                        var cz = new AnimationCurve();
                        foreach (var k in animNode.PositionKeys)
                        {
                            cx.AddKey(k.Time, k.Value.x);
                            cy.AddKey(k.Time, k.Value.y);
                            cz.AddKey(k.Time, k.Value.z);
                        }
                        clip.SetCurve(path, typeof(Transform),
                            "localPosition.x", cx);
                        clip.SetCurve(path, typeof(Transform),
                            "localPosition.y", cy);
                        clip.SetCurve(path, typeof(Transform),
                            "localPosition.z", cz);
                    }

                    // Rotation curves
                    if (animNode.RotationKeys.Count > 0)
                    {
                        var cx = new AnimationCurve();
                        var cy = new AnimationCurve();
                        var cz = new AnimationCurve();
                        var cw = new AnimationCurve();
                        foreach (var k in animNode.RotationKeys)
                        {
                            cx.AddKey(k.Time, k.Value.x);
                            cy.AddKey(k.Time, k.Value.y);
                            cz.AddKey(k.Time, k.Value.z);
                            cw.AddKey(k.Time, k.Value.w);
                        }
                        clip.SetCurve(path, typeof(Transform),
                            "localRotation.x", cx);
                        clip.SetCurve(path, typeof(Transform),
                            "localRotation.y", cy);
                        clip.SetCurve(path, typeof(Transform),
                            "localRotation.z", cz);
                        clip.SetCurve(path, typeof(Transform),
                            "localRotation.w", cw);
                    }
                }

                clips[anim.Name] = clip;
            }

            return clips;
        }
    }

    /// <summary>Marker component so texture loader can find nodes needing textures.</summary>
    public class PendingTextureTag : MonoBehaviour
    {
        public string TextureName;
        public bool   Applied = false;
    }
}
