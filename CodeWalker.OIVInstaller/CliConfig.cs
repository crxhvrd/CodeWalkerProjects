using System;
using System.IO;
using System.Text.Json;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Handles CLI configuration storage (default game folder, etc.)
    /// </summary>
    public static class CliConfig
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeWalker.OIVInstaller");
        
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "cli.json");

        private class Config
        {
            public string GameFolder { get; set; }
        }

        /// <summary>
        /// Gets the saved default game folder, or null if not set.
        /// </summary>
        public static string GetGameFolder()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    return config?.GameFolder;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Saves the default game folder to config.
        /// </summary>
        public static void SetGameFolder(string path)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                var config = new Config { GameFolder = path };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save config: {ex.Message}");
            }
        }
    }
}
