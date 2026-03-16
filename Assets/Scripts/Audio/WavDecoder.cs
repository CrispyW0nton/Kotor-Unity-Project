using System;
using UnityEngine;

namespace KotORUnity
{
    /// <summary>
    /// Decodes a PCM WAV byte array into a Unity AudioClip at runtime.
    /// Supports 8-bit and 16-bit mono/stereo uncompressed PCM WAV files.
    /// KotOR ships WAV files in this format.
    /// </summary>
    public static class WavDecoder
    {
        public static AudioClip Decode(byte[] data, string clipName = "clip")
        {
            if (data == null || data.Length < 44) return null;

            try
            {
                // Verify RIFF header
                string riff = System.Text.Encoding.ASCII.GetString(data, 0, 4);
                string wave = System.Text.Encoding.ASCII.GetString(data, 8, 4);
                if (riff != "RIFF" || wave != "WAVE") return null;

                int pos = 12;

                // Find fmt chunk
                int channels    = 1;
                int sampleRate  = 22050;
                int bitsPerSample = 16;

                while (pos + 8 < data.Length)
                {
                    string chunkId   = System.Text.Encoding.ASCII.GetString(data, pos, 4);
                    int    chunkSize = BitConverter.ToInt32(data, pos + 4);
                    pos += 8;

                    if (chunkId == "fmt ")
                    {
                        // int16  AudioFormat   (PCM=1)
                        // int16  NumChannels
                        // int32  SampleRate
                        // int32  ByteRate
                        // int16  BlockAlign
                        // int16  BitsPerSample
                        channels     = BitConverter.ToInt16(data, pos + 2);
                        sampleRate   = BitConverter.ToInt32(data, pos + 4);
                        bitsPerSample = BitConverter.ToInt16(data, pos + 14);
                        pos += chunkSize;
                    }
                    else if (chunkId == "data")
                    {
                        int sampleCount = chunkSize / (bitsPerSample / 8);
                        float[] samples = new float[sampleCount];

                        if (bitsPerSample == 16)
                        {
                            for (int i = 0; i < sampleCount; i++)
                            {
                                short s16 = BitConverter.ToInt16(data, pos + i * 2);
                                samples[i] = s16 / 32768f;
                            }
                        }
                        else if (bitsPerSample == 8)
                        {
                            for (int i = 0; i < sampleCount; i++)
                                samples[i] = (data[pos + i] - 128) / 128f;
                        }

                        var clip = AudioClip.Create(clipName, sampleCount / channels,
                                                    channels, sampleRate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }
                    else
                    {
                        pos += chunkSize;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WavDecoder] Failed to decode '{clipName}': {e.Message}");
            }

            return null;
        }
    }
}
