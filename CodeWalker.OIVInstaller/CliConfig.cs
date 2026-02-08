using System;
using System.IO;
using System.Text.Json;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Handles CLI configuration storage (default game folder, etc.)
    /// </summary>
    /// <summary>
    /// Handles shared configuration storage (default game folder, etc.)
    /// </summary>
    public static class OivAppConfig
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeWalker");
        
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "OivInstaller.json");

        public class ConfigData
        {
            public string LastGameFolder { get; set; }
            public string GameFolderLegacy { get; set; }
            public string GameFolderEnhanced { get; set; }
        }

        public static ConfigData Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    return JsonSerializer.Deserialize<ConfigData>(json);
                }
            }
            catch { }
            return new ConfigData();
        }

        public static void Save(ConfigData config)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                // In a GUI app we might want to log this vs throw
                // throw new Exception($"Failed to save config: {ex.Message}");
            }
        }
    }
}
