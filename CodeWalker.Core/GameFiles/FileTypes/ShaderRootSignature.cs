using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CodeWalker.GameFiles
{
    /// <summary>
    /// Minimal reader for the DXBC container format. Both SM5 bytecode and SM6 DXIL
    /// ship inside a container with the 'DXBC' magic; the payload differs, the envelope
    /// does not. Only the chunk directory is parsed here, which is all that is needed
    /// to find a shader's embedded root signature.
    /// </summary>
    public static class DxbcContainer
    {
        public const uint Magic = 0x43425844; //'DXBC'

        //4 magic + 16 digest + 4 version + 4 total size + 4 chunk count = 32
        private const int ChunkCountOffset = 28;
        private const int ChunkTableOffset = 32;
        private const int MaxChunks = 64; //sanity bound; real containers have well under this

        public static bool IsContainer(byte[] blob)
        {
            return (blob != null) && (blob.Length >= ChunkTableOffset + 4)
                && (BitConverter.ToUInt32(blob, 0) == Magic);
        }

        /// <summary>The FourCCs of every chunk, in directory order. Empty if unparseable.</summary>
        public static List<string> ChunkNames(byte[] blob)
        {
            var names = new List<string>();
            foreach (var c in Chunks(blob)) names.Add(c.Key);
            return names;
        }

        public static bool HasChunk(byte[] blob, string fourcc)
        {
            return GetChunk(blob, fourcc) != null;
        }

        /// <summary>A chunk's payload (excluding its 8-byte header), or null if absent.</summary>
        public static byte[] GetChunk(byte[] blob, string fourcc)
        {
            foreach (var c in Chunks(blob))
            {
                if (c.Key != fourcc) continue;
                var payload = new byte[c.Value.Value];
                Buffer.BlockCopy(blob, c.Value.Key, payload, 0, c.Value.Value);
                return payload;
            }
            return null;
        }

        //fourcc -> (payload offset, payload length)
        private static IEnumerable<KeyValuePair<string, KeyValuePair<int, int>>> Chunks(byte[] blob)
        {
            if (!IsContainer(blob)) yield break;

            uint count = BitConverter.ToUInt32(blob, ChunkCountOffset);
            if ((count == 0) || (count > MaxChunks)) yield break;
            if (ChunkTableOffset + count * 4 > blob.Length) yield break;

            for (int i = 0; i < count; i++)
            {
                uint uoff = BitConverter.ToUInt32(blob, ChunkTableOffset + i * 4);
                if (uoff > int.MaxValue) continue;
                int off = (int)uoff;
                if ((off < ChunkTableOffset) || (off + 8 > blob.Length)) continue;

                string name = Encoding.ASCII.GetString(blob, off, 4);
                uint ulen = BitConverter.ToUInt32(blob, off + 4);
                if (ulen > int.MaxValue) continue;
                int len = (int)ulen;
                if (off + 8 + len > blob.Length) continue;

                yield return new KeyValuePair<string, KeyValuePair<int, int>>(
                    name, new KeyValuePair<int, int>(off + 8, len));
            }
        }
    }


    public enum RootSigVerdict
    {
        /// <summary>The original carries no root signature, so there is nothing to preserve.</summary>
        NothingToPreserve,
        /// <summary>The replacement already carries the original's root signature, byte for byte.</summary>
        AlreadyMatches,
        /// <summary>Both carry one, but they differ. The caller should confirm with the user.</summary>
        Differs,
        /// <summary>The original has one and the replacement does not. Unsafe without a transplant.</summary>
        Missing,
        /// <summary>The replacement is not a DXBC container at all.</summary>
        NotAContainer,
    }

    public struct RootSigCheck
    {
        public RootSigVerdict Verdict;
        public string Message;
        /// <summary>True when the replacement can be written as-is without risking PSO creation.</summary>
        public bool SafeAsIs => (Verdict == RootSigVerdict.NothingToPreserve)
                             || (Verdict == RootSigVerdict.AlreadyMatches);
    }


    /// <summary>
    /// Root-signature preservation for shader replacement.
    ///
    /// Every shader in GTA V Enhanced's shader libraries embeds an RTS0 chunk, and the
    /// game builds its pipeline state objects from it. HLSL recompiled with dxc emits no
    /// RTS0 unless asked, so dropping a freshly compiled blob into a slot leaves the PSO
    /// without the signature the game expects: PSO creation fails, and the effect either
    /// silently never runs or takes the game down with it. Nothing about the archive looks
    /// wrong afterwards, which makes it a miserable failure to diagnose.
    ///
    /// So: check first, and only write when the replacement carries a usable signature.
    /// A shader built by a pipeline that already transplants RTS0 passes straight through
    /// with no external tooling. Anything else needs dxc.exe, because an SM6 container is
    /// hash-signed and re-signing it requires Microsoft's dxil.dll - it cannot be done in
    /// managed code.
    /// </summary>
    public static class ShaderRootSignature
    {
        public const string ChunkName = "RTS0";

        /// <summary>Compares the replacement's root signature against the shader it replaces.</summary>
        public static RootSigCheck Check(byte[] original, byte[] replacement)
        {
            if (!DxbcContainer.IsContainer(replacement))
            {
                return new RootSigCheck
                {
                    Verdict = RootSigVerdict.NotAContainer,
                    Message = "The replacement is not a DXBC/DXIL container, so it carries no root signature."
                };
            }

            var oldrs = DxbcContainer.GetChunk(original, ChunkName);
            if (oldrs == null)
            {
                return new RootSigCheck
                {
                    Verdict = RootSigVerdict.NothingToPreserve,
                    Message = "The shader being replaced has no embedded root signature."
                };
            }

            var newrs = DxbcContainer.GetChunk(replacement, ChunkName);
            if (newrs == null)
            {
                return new RootSigCheck
                {
                    Verdict = RootSigVerdict.Missing,
                    Message = "The shader being replaced embeds a root signature (" + oldrs.Length
                            + " bytes) and the replacement does not."
                };
            }

            if (BytesEqual(oldrs, newrs))
            {
                return new RootSigCheck
                {
                    Verdict = RootSigVerdict.AlreadyMatches,
                    Message = "The replacement already carries the original root signature."
                };
            }

            return new RootSigCheck
            {
                Verdict = RootSigVerdict.Differs,
                Message = "The replacement embeds a different root signature (" + newrs.Length
                        + " bytes vs " + oldrs.Length + "). The game builds its PSO from the "
                        + "original, so this may fail pipeline creation."
            };
        }

        /// <summary>
        /// Copies the original shader's root signature onto the replacement and re-signs the
        /// container, via dxc. Returns false and leaves <paramref name="result"/> null when the
        /// replacement cannot accept it - which means it binds resources the original signature
        /// does not describe, and injecting it anyway would be the crash this class exists to
        /// prevent. Callers must treat false as "do not write".
        /// </summary>
        public static bool TryTransplant(byte[] original, byte[] replacement, string dxcPath,
            out byte[] result, out string message)
        {
            result = null;

            if (!DxbcContainer.HasChunk(original, ChunkName))
            {
                result = replacement;
                message = "Original has no root signature; nothing to transplant.";
                return true;
            }
            if (string.IsNullOrEmpty(dxcPath) || !File.Exists(dxcPath))
            {
                message = "dxc.exe was not found, so the root signature cannot be transplanted.";
                return false;
            }

            string dir = Path.Combine(Path.GetTempPath(), "cw_rootsig_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                string oldCso = Path.Combine(dir, "old.cso");
                string newCso = Path.Combine(dir, "new.cso");
                string rsBin = Path.Combine(dir, "rs.bin");
                string outCso = Path.Combine(dir, "final.cso");

                File.WriteAllBytes(oldCso, original);
                File.WriteAllBytes(newCso, replacement);

                string err;
                if (!RunDxc(dxcPath, "-dumpbin \"" + oldCso + "\" -extractrootsignature -Fo \"" + rsBin + "\"", out err)
                    || !File.Exists(rsBin) || new FileInfo(rsBin).Length == 0)
                {
                    message = "Could not extract the original root signature." + Tail(err);
                    return false;
                }

                if (!RunDxc(dxcPath, "-dumpbin \"" + newCso + "\" -setrootsignature \"" + rsBin + "\" -Fo \"" + outCso + "\"", out err)
                    || !File.Exists(outCso))
                {
                    message = "The replacement will not accept the original root signature - it most "
                            + "likely binds a resource the original does not declare." + Tail(err);
                    return false;
                }

                result = File.ReadAllBytes(outCso);
                message = "Root signature transplanted and container re-signed.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Root signature transplant failed: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>
        /// Locates dxc.exe: the supplied hint, then a dxcompilers folder beside the
        /// application, then the working directory. Returns null when unavailable, which
        /// callers must treat as "cannot transplant" rather than "carry on".
        /// </summary>
        public static string FindDxc(string hint = null)
        {
            if (!string.IsNullOrEmpty(hint))
            {
                if (File.Exists(hint)) return hint;
                var h = Path.Combine(hint, "dxc.exe");
                if (File.Exists(h)) return h;
            }

            // Shipped alongside the application's other native dependencies; the
            // dxcompilers subfolder is checked too so an existing toolchain layout
            // still works.
            string base_ = AppDomain.CurrentDomain.BaseDirectory ?? "";
            foreach (var rel in new[] { "dxc.exe", "dxcompilers\\dxc.exe" })
            {
                var p = Path.Combine(base_, rel);
                if (File.Exists(p)) return p;
                if (File.Exists(rel)) return Path.GetFullPath(rel);
            }
            return null;
        }

        private static bool RunDxc(string exe, string args, out string stderr)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            };
            using (var p = Process.Start(psi))
            {
                string e = p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit(60000);
                stderr = e;
                return p.HasExited && (p.ExitCode == 0);
            }
        }

        private static string Tail(string err)
        {
            if (string.IsNullOrWhiteSpace(err)) return "";
            var lines = err.Trim().Split('\n');
            return "\n\ndxc: " + lines[lines.Length - 1].Trim();
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if ((a == null) || (b == null) || (a.Length != b.Length)) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
