using System;
using System.Diagnostics;
using System.Linq;

namespace CodeWalker.OIVInstaller
{
    public static class ProcessHelper
    {
        private static readonly string[] GameProcessNames = new[]
        {
            "GTA5",
            "GTA5_Enhanced",
            "FiveM",
            "FiveM_b*", // Wildcard handling might be needed, but Process.GetProcessesByName doesn't support it directly.
                        // We'll handle it manually.
            "RAGEPluginHook"
        };

        public static bool IsGameRunning(out string processName)
        {
            processName = null;
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    string pName = p.ProcessName;
                    
                    // Direct match
                    if (GameProcessNames.Contains(pName, StringComparer.OrdinalIgnoreCase))
                    {
                        processName = pName;
                        p.Dispose();
                        return true;
                    }

                    // Special case for FiveM build versions (FiveM_b2189, etc.)
                    if (pName.StartsWith("FiveM_b", StringComparison.OrdinalIgnoreCase))
                    {
                        processName = pName;
                        p.Dispose();
                        return true;
                    }
                }
                catch
                {
                    // Ignore processes we can't access
                }
                finally
                {
                    p.Dispose();
                }
            }

            return false;
        }
    }
}
