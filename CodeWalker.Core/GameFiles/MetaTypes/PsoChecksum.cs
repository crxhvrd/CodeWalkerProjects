using System;

namespace CodeWalker.GameFiles
{
    /// <summary>
    /// Recomputes the CHKS chunk that some PSO files carry.
    ///
    /// Some metadata files carry a CHKS chunk holding a checksum of their own
    /// contents, and the game validates it for a handful of them - cameras being the
    /// notable one. Any edit leaves that checksum stale, and the game then crashes on
    /// startup instead of reporting a problem. A single changed byte is enough, which
    /// is why those files appeared unmoddable.
    ///
    /// Verified by reproducing the shipped checksum of a stock cameras.ymt.
    /// </summary>
    public static class PsoChecksum
    {
        private const uint ChecksumSaltV2 = 0x3FAC7125;

        /// <summary>
        /// Fixes the CHKS chunk of a serialised PSO in place. Safe to call on files
        /// that have no CHKS chunk - it simply does nothing. Returns true if a
        /// checksum was written.
        /// </summary>
        public static bool Update(byte[] file)
        {
            if (file == null || file.Length < 20) return false;

            int chks = FindChunk(file);
            if (chks < 0) return false;

            uint chunkSize = ReadBE(file, chks + 4);
            if (chunkSize != 16 && chunkSize != 20) return false;

            //the checksum covers the file up to the recorded size, with the size and
            //checksum fields themselves zeroed
            uint fileSize = (uint)file.Length;
            WriteBE(file, chks + 8, fileSize);
            WriteBE(file, chks + 12, 0);

            uint saved = ReadBE(file, chks + 8);
            WriteBE(file, chks + 8, 0);

            int amount = (int)Math.Min(fileSize, (uint)file.Length);
            uint sum = Hash(file, amount, chunkSize == 20 ? ChecksumSaltV2 : 0);

            WriteBE(file, chks + 8, saved);
            WriteBE(file, chks + 12, sum);
            return true;
        }

        /// <summary>Offset of the CHKS chunk, or -1. It's the last chunk, so scan back.</summary>
        public static int FindChunk(byte[] f)
        {
            for (int i = f.Length - 8; i >= 0; i--)
            {
                if (f[i] == 0x43 && f[i + 1] == 0x48 && f[i + 2] == 0x4B && f[i + 3] == 0x53) return i; //"CHKS"
            }
            return -1;
        }

        /// <summary>
        /// Jenkins one-at-a-time seeded with a salt, over SIGNED bytes.
        ///
        /// The signedness matters: bytes >= 0x80 are treated as negative and subtract
        /// rather than add. Hashing them as unsigned produces a different value for
        /// any file containing high bytes.
        /// </summary>
        public static uint Hash(byte[] data, int length, uint salt)
        {
            uint key = salt;
            int n = Math.Min(length, data.Length);
            for (int i = 0; i < n; i++)
            {
                key = (uint)(key + (sbyte)data[i]);
                key += key << 10;
                key ^= key >> 6;
            }
            key += key << 3;
            key ^= key >> 11;
            key += key << 15;
            return key;
        }

        private static uint ReadBE(byte[] b, int o) =>
            (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

        private static void WriteBE(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }
    }
}
