using System;
using System.IO;
using UnityEngine;

namespace KotORUnity.KotOR.Parsers
{
    /// <summary>
    /// Decodes TGA (Targa) and DDS (DirectDraw Surface) image files
    /// into Unity Texture2D objects at runtime.
    ///
    /// KotOR uses:
    ///   TGA – uncompressed RGBA/RGB for most character/environment textures
    ///   DDS – DXT1/DXT5 compressed textures for skyboxes and large surfaces
    ///   TPC – BioWare proprietary wrapper around TGA/DDS (handled separately)
    ///
    /// TGA FORMAT (little-endian)
    /// ─────────────────────────
    ///   Byte  0    idLength        (skip N bytes)
    ///   Byte  1    colorMapType    (0 = no palette)
    ///   Byte  2    imageType       (2=RGB, 3=grey, 10=RLE-RGB, 11=RLE-grey)
    ///   Bytes 3-7  colorMapSpec    (skip)
    ///   Bytes 8-9  xOrigin
    ///   Bytes 10-11 yOrigin
    ///   Bytes 12-13 width
    ///   Bytes 14-15 height
    ///   Byte  16   bitsPerPixel    (16/24/32)
    ///   Byte  17   imageDescriptor (bit4=flip-x, bit5=flip-y)
    ///
    /// DDS FORMAT (little-endian)
    /// ─────────────────────────
    ///   Bytes 0-3   magic    "DDS "
    ///   Bytes 4-7   size     124 (header size)
    ///   Bytes 8-11  flags
    ///   Bytes 12-15 height
    ///   Bytes 16-19 width
    ///   Bytes 20-23 pitchOrLinearSize
    ///   Bytes 24-27 depth
    ///   Bytes 28-31 mipMapCount
    ///   Bytes 32-75 reserved
    ///   Bytes 76-107 pixelFormat
    ///     +0  size   (32)
    ///     +4  flags  (bit2=fourCC, bit6=RGB)
    ///     +8  fourCC "DXT1","DXT3","DXT5"
    ///     +12 rgbBitCount
    ///     ... masks
    ///   Bytes 108-123 caps
    /// </summary>
    public static class TextureLoader
    {
        // ── PUBLIC ENTRY POINT ────────────────────────────────────────────────
        /// <summary>
        /// Decode a raw TGA or DDS byte array into a Texture2D.
        /// Returns null on failure.
        /// </summary>
        public static Texture2D Decode(byte[] data, string name = "texture")
        {
            if (data == null || data.Length < 8) return null;

            // Detect format by magic bytes
            if (data[0] == 'D' && data[1] == 'D' && data[2] == 'S' && data[3] == ' ')
                return DecodeDDS(data, name);

            // TGA has no magic — detect by checking image type byte at offset 2
            // Valid KotOR TGA types: 2 (RGB), 3 (grey), 10 (RLE-RGB), 11 (RLE-grey)
            byte tgaType = data[2];
            if (tgaType == 2 || tgaType == 3 || tgaType == 10 || tgaType == 11)
                return DecodeTGA(data, name);

            // TPC (BioWare wrapper)
            if (data.Length > 4)
                return DecodeTPC(data, name);

            Debug.LogWarning($"[TextureLoader] Unknown format for '{name}'.");
            return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TGA DECODER
        // ══════════════════════════════════════════════════════════════════════
        private static Texture2D DecodeTGA(byte[] data, string name)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);

                byte  idLength      = br.ReadByte();
                byte  colorMapType  = br.ReadByte();
                byte  imageType     = br.ReadByte();

                // Color map spec (5 bytes – skip)
                br.ReadBytes(5);

                short xOrigin  = br.ReadInt16();
                short yOrigin  = br.ReadInt16();
                short width    = br.ReadInt16();
                short height   = br.ReadInt16();
                byte  bpp      = br.ReadByte();
                byte  imgDesc  = br.ReadByte();

                // Skip image ID
                if (idLength > 0) br.ReadBytes(idLength);

                int  channels   = bpp / 8;
                bool flipY      = (imgDesc & 0x20) == 0; // KotOR TGAs are usually bottom-up
                bool rle        = imageType == 10 || imageType == 11;

                int pixelCount = width * height;
                byte[] pixels  = new byte[pixelCount * 4]; // RGBA output

                if (rle)
                    ReadTGARLE(br, pixels, pixelCount, channels);
                else
                    ReadTGARaw(br, pixels, pixelCount, channels);

                // Flip Y if needed (TGA is bottom-up by default)
                if (flipY)
                    FlipY(pixels, width, height);

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
                tex.name = name;
                tex.LoadRawTextureData(pixels);
                tex.Apply(true, true);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextureLoader] TGA decode failed '{name}': {e.Message}");
                return null;
            }
        }

        private static void ReadTGARaw(BinaryReader br, byte[] output,
                                        int pixelCount, int channels)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                int dst = i * 4;
                if (channels == 4)
                {
                    byte b = br.ReadByte(); byte g = br.ReadByte();
                    byte r = br.ReadByte(); byte a = br.ReadByte();
                    output[dst] = r; output[dst+1] = g;
                    output[dst+2] = b; output[dst+3] = a;
                }
                else if (channels == 3)
                {
                    byte b = br.ReadByte(); byte g = br.ReadByte(); byte r = br.ReadByte();
                    output[dst] = r; output[dst+1] = g;
                    output[dst+2] = b; output[dst+3] = 255;
                }
                else // greyscale / 16-bit fallback
                {
                    byte v = br.ReadByte();
                    if (channels > 1) br.ReadByte();
                    output[dst] = v; output[dst+1] = v;
                    output[dst+2] = v; output[dst+3] = 255;
                }
            }
        }

        private static void ReadTGARLE(BinaryReader br, byte[] output,
                                        int pixelCount, int channels)
        {
            int written = 0;
            while (written < pixelCount)
            {
                byte header = br.ReadByte();
                int  count  = (header & 0x7F) + 1;

                if ((header & 0x80) != 0)
                {
                    // RLE run: one pixel repeated
                    byte b=0,g=0,r=0,a=255;
                    if (channels==4){b=br.ReadByte();g=br.ReadByte();r=br.ReadByte();a=br.ReadByte();}
                    else if (channels==3){b=br.ReadByte();g=br.ReadByte();r=br.ReadByte();}
                    else {b=br.ReadByte(); r=g=b;}

                    for (int k = 0; k < count && written < pixelCount; k++, written++)
                    {
                        int dst = written * 4;
                        output[dst]=r; output[dst+1]=g;
                        output[dst+2]=b; output[dst+3]=a;
                    }
                }
                else
                {
                    // Raw packet
                    for (int k = 0; k < count && written < pixelCount; k++, written++)
                    {
                        byte b=0,g=0,r=0,a=255;
                        if (channels==4){b=br.ReadByte();g=br.ReadByte();r=br.ReadByte();a=br.ReadByte();}
                        else if (channels==3){b=br.ReadByte();g=br.ReadByte();r=br.ReadByte();}
                        else {b=br.ReadByte(); r=g=b;}
                        int dst = written * 4;
                        output[dst]=r; output[dst+1]=g;
                        output[dst+2]=b; output[dst+3]=a;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DDS DECODER
        // ══════════════════════════════════════════════════════════════════════
        private static Texture2D DecodeDDS(byte[] data, string name)
        {
            try
            {
                if (data.Length < 128) return null;

                int height = BitConverter.ToInt32(data, 12);
                int width  = BitConverter.ToInt32(data, 16);
                int mips   = BitConverter.ToInt32(data, 28);
                if (mips == 0) mips = 1;

                // Pixel format at offset 76
                int   pfFlags  = BitConverter.ToInt32(data, 80);
                byte  f0 = data[84], f1 = data[85], f2 = data[86], f3 = data[87];
                string fourCC  = $"{(char)f0}{(char)f1}{(char)f2}{(char)f3}";

                TextureFormat fmt;
                switch (fourCC)
                {
                    case "DXT1": fmt = TextureFormat.DXT1;  break;
                    case "DXT5": fmt = TextureFormat.DXT5;  break;
                    case "DXT3": fmt = TextureFormat.DXT5;  break; // closest Unity has
                    default:
                        // Uncompressed — fall back to RGBA32
                        return DecodeDDSUncompressed(data, name, width, height, mips, pfFlags);
                }

                // Unity can load DXT directly
                var tex  = new Texture2D(width, height, fmt, mips > 1);
                tex.name = name;

                // DDS pixel data starts at byte 128
                int dataSize = data.Length - 128;
                byte[] rawPixels = new byte[dataSize];
                Array.Copy(data, 128, rawPixels, 0, dataSize);
                tex.LoadRawTextureData(rawPixels);
                tex.Apply(false, true);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextureLoader] DDS decode failed '{name}': {e.Message}");
                return null;
            }
        }

        private static Texture2D DecodeDDSUncompressed(byte[] data, string name,
            int width, int height, int mips, int pfFlags)
        {
            // RGBA32 uncompressed
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mips > 1);
            tex.name = name;
            int dataSize = data.Length - 128;
            byte[] raw = new byte[dataSize];
            Array.Copy(data, 128, raw, 0, dataSize);
            // DDS is stored top-to-bottom; Unity expects bottom-to-top
            FlipY(raw, width, height);
            tex.LoadRawTextureData(raw);
            tex.Apply(false, true);
            return tex;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TPC DECODER  (BioWare proprietary format)
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// TPC wraps TGA or DXT data with a custom 128-byte header.
        ///
        /// Header:
        ///   uint32  dataSize
        ///   float   alphaTest  (0 = no alpha test, >0 = cutoff)
        ///   uint16  width
        ///   uint16  height
        ///   uint8   encoding   (2=grey, 4=RGB, 5=RGBA, 6=DXT1, 7=DXT3, 8=DXT5)
        ///   uint8   mipCount
        ///   byte[114] reserved
        /// </summary>
        private static Texture2D DecodeTPC(byte[] data, string name)
        {
            try
            {
                if (data.Length < 128) return null;

                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);

                uint  dataSize  = br.ReadUInt32();
                float alphaTest = br.ReadSingle();
                ushort width    = br.ReadUInt16();
                ushort height   = br.ReadUInt16();
                byte  encoding  = br.ReadByte();
                byte  mipCount  = br.ReadByte();

                // Skip 114 reserved bytes
                br.ReadBytes(114);

                // Pixel data follows the 128-byte header
                byte[] pixelData = br.ReadBytes((int)(data.Length - 128));

                switch (encoding)
                {
                    case 4: // RGB
                        return BuildTexture(ConvertRGBtoRGBA(pixelData),
                            width, height, TextureFormat.RGBA32, name);
                    case 5: // RGBA
                        return BuildTexture(pixelData,
                            width, height, TextureFormat.RGBA32, name);
                    case 6: // DXT1
                        return BuildTexture(pixelData,
                            width, height, TextureFormat.DXT1, name);
                    case 8: // DXT5
                        return BuildTexture(pixelData,
                            width, height, TextureFormat.DXT5, name);
                    default:
                        Debug.LogWarning($"[TextureLoader] TPC encoding {encoding} not handled for '{name}'.");
                        return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextureLoader] TPC decode failed '{name}': {e.Message}");
                return null;
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private static Texture2D BuildTexture(byte[] pixels, int w, int h,
                                               TextureFormat fmt, string name)
        {
            var tex = new Texture2D(w, h, fmt, false);
            tex.name = name;
            tex.LoadRawTextureData(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static byte[] ConvertRGBtoRGBA(byte[] rgb)
        {
            int pixCount = rgb.Length / 3;
            byte[] rgba  = new byte[pixCount * 4];
            for (int i = 0; i < pixCount; i++)
            {
                rgba[i*4]   = rgb[i*3];
                rgba[i*4+1] = rgb[i*3+1];
                rgba[i*4+2] = rgb[i*3+2];
                rgba[i*4+3] = 255;
            }
            return rgba;
        }

        private static void FlipY(byte[] pixels, int width, int height)
        {
            int rowSize = width * 4;
            byte[] tmp  = new byte[rowSize];
            for (int y = 0; y < height / 2; y++)
            {
                int topOffset = y * rowSize;
                int botOffset = (height - 1 - y) * rowSize;
                Array.Copy(pixels, topOffset, tmp, 0, rowSize);
                Array.Copy(pixels, botOffset, pixels, topOffset, rowSize);
                Array.Copy(tmp, 0, pixels, botOffset, rowSize);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TEXTURE CACHE  —  avoid decoding the same texture twice
    // ══════════════════════════════════════════════════════════════════════════
    public static class TextureCache
    {
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D>
            _cache = new System.Collections.Generic.Dictionary<string, Texture2D>(
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Load a texture by resref from the ResourceManager, decode it,
        /// cache and return it. Returns a 1×1 magenta fallback on failure.
        /// </summary>
        public static Texture2D Get(string resref)
        {
            if (string.IsNullOrEmpty(resref)) return Fallback();
            if (_cache.TryGetValue(resref, out var cached)) return cached;

            var rm = Bootstrap.SceneBootstrapper.Resources;
            if (rm == null) return Fallback();

            // Try TGA first, then DDS, then TPC
            byte[] data = rm.GetResource(resref, KotORUnity.KotOR.FileReaders.ResourceType.TGA)
                       ?? rm.GetResource(resref, KotORUnity.KotOR.FileReaders.ResourceType.DDS)
                       ?? rm.GetResource(resref, KotORUnity.KotOR.FileReaders.ResourceType.TPC);

            if (data == null)
            {
                Debug.LogWarning($"[TextureCache] Texture not found: '{resref}'");
                return Fallback();
            }

            var tex = TextureLoader.Decode(data, resref);
            if (tex == null) tex = Fallback();
            _cache[resref] = tex;
            return tex;
        }

        public static void Clear() => _cache.Clear();

        private static Texture2D Fallback()
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, Color.magenta);
            t.Apply();
            return t;
        }
    }
}
