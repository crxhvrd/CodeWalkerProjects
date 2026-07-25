using System;
using System.IO;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Picks the working folder a package is unpacked into.
    ///
    /// Unpacking needs roughly the package's own size in free space, and %TEMP%
    /// usually lives on a small system SSD — a multi-gigabyte mod would fail there
    /// with a bare "There is not enough space on the disk" even when the drive
    /// holding the package (or the game) has plenty. So: use the system temp when it
    /// fits, otherwise fall back to the package's own drive.
    /// </summary>
    internal static class PackageTempSpace
    {
        /// <summary>Headroom over the package size, for zip overhead and the OS.</summary>
        private const double SpaceFactor = 1.15;
        private const long MinHeadroom = 256L * 1024 * 1024;

        /// <summary>
        /// Creates and returns an empty working directory able to hold
        /// <paramref name="packagePath"/> unpacked.
        /// </summary>
        public static string Create(string packagePath, string prefix)
        {
            string name = prefix + Guid.NewGuid().ToString("N");

            long needed = 0;
            try
            {
                var fi = new FileInfo(packagePath);
                if (fi.Exists) needed = (long)(fi.Length * SpaceFactor) + MinHeadroom;
            }
            catch { }

            foreach (var base_ in CandidateRoots(packagePath))
            {
                if (string.IsNullOrEmpty(base_)) continue;
                if (needed > 0 && FreeSpace(base_) < needed) continue;
                try
                {
                    string dir = Path.Combine(base_, name);
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch { }   // unwritable candidate — try the next one
            }

            // Nothing had measurable room; fall back to the system temp and let the
            // real error surface from the extraction itself.
            string last = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(last);
            return last;
        }

        private static string[] CandidateRoots(string packagePath)
        {
            string sysTemp = Path.GetTempPath();
            string pkgTemp = null;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(packagePath));
                // Only worth trying when it's a different volume than the system temp.
                if (!string.IsNullOrEmpty(dir) &&
                    !string.Equals(Path.GetPathRoot(dir), Path.GetPathRoot(sysTemp),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    pkgTemp = Path.Combine(dir, "_oivtemp");
                }
            }
            catch { }

            return new[] { sysTemp, pkgTemp };
        }

        private static long FreeSpace(string path)
        {
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)));
                return d.IsReady ? d.AvailableFreeSpace : 0;
            }
            catch { return 0; }
        }
    }
}
