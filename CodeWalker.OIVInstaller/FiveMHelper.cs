using System;
using System.IO;

namespace CodeWalker.OIVInstaller
{
    public static class FiveMHelper
    {
        public static string GetFiveMModsFolder()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fiveMAppPath = Path.Combine(localAppData, "FiveM", "FiveM.app");

            if (Directory.Exists(fiveMAppPath))
            {
                return Path.Combine(fiveMAppPath, "mods");
            }

            return null;
        }

        public static bool IsFiveMInstalled()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fiveMPath = Path.Combine(localAppData, "FiveM", "FiveM.app");
            return Directory.Exists(fiveMPath);
        }
    }
}
