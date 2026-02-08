using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CodeWalker.GameFiles;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Handles all CLI operations for the OIV Installer
    /// </summary>
    public static class CliHandler
    {
        // Import kernel32 for console attachment
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
        
        private const int ATTACH_PARENT_PROCESS = -1;

        /// <summary>
        /// Attaches to parent console for CLI output
        /// </summary>
        public static void AttachToConsole()
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
        }

        /// <summary>
        /// Detaches from console
        /// </summary>
        public static void DetachConsole()
        {
            FreeConsole();
        }

        /// <summary>
        /// Opens a folder browser dialog to select the game folder
        /// </summary>
        public static string BrowseForGameFolder(string title = "Select GTA 5 Game Folder")
        {
            string selectedPath = null;
            
            // Run on STA thread for dialog
            var thread = new System.Threading.Thread(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = title;
                    dialog.ShowNewFolderButton = false;
                    dialog.UseDescriptionForTitle = true;
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        selectedPath = dialog.SelectedPath;
                    }
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            
            return selectedPath;
        }

        /// <summary>
        /// Prompts user to select game folder if not already set
        /// </summary>
        public static string PromptForGameFolder()
        {
            var existing = CliConfig.GetGameFolder();
            if (!string.IsNullOrEmpty(existing) && Directory.Exists(existing))
            {
                return existing;
            }
            
            Console.WriteLine();
            Console.WriteLine("No game folder configured. Opening folder picker...");
            
            var folder = BrowseForGameFolder("Select your GTA 5 game folder");
            
            if (string.IsNullOrEmpty(folder))
            {
                Console.WriteLine("No folder selected.");
                return null;
            }
            
            // Validate
            bool isValid = File.Exists(Path.Combine(folder, "GTA5.exe")) ||
                          File.Exists(Path.Combine(folder, "GTA5_Enhanced.exe")) ||
                          File.Exists(Path.Combine(folder, "eboot.bin"));
            
            if (!isValid)
            {
                Console.WriteLine("Warning: This doesn't look like a GTA5 game folder.");
                Console.WriteLine("Expected GTA5.exe, GTA5_Enhanced.exe, or eboot.bin");
            }
            
            // Save as default
            CliConfig.SetGameFolder(folder);
            Console.WriteLine($"Game folder set to: {folder}");
            
            return folder;
        }

        /// <summary>
        /// Displays help text
        /// </summary>
        public static int ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("CodeWalker OIV Package Installer - CLI Mode");
            Console.WriteLine("============================================");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  CodeWalker.OIVInstaller.exe [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --help                    Show this help message");
            Console.WriteLine("  --set-game <path>         Set default GTA5 game folder");
            Console.WriteLine("  --get-game                Show current default game folder");
            Console.WriteLine("  --install <oiv-path>      Install a mod package");
            Console.WriteLine("  --uninstall <name>        Uninstall a mod by name");
            Console.WriteLine("  --list                    List installed mods");
            Console.WriteLine("  --game <path>             Override game folder for this command");
            Console.WriteLine("  --vanilla                 Use vanilla reset mode for uninstall");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  --set-game \"C:\\Games\\GTA5\"");
            Console.WriteLine("  --install \"C:\\Mods\\mymod.oiv\"");
            Console.WriteLine("  --uninstall \"My Mod Name\"");
            Console.WriteLine("  --uninstall \"My Mod Name\" --vanilla");
            Console.WriteLine("  --list");
            Console.WriteLine();
            return 0;
        }

        /// <summary>
        /// Sets the default game folder
        /// </summary>
        public static int SetGameFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("Error: No path specified.");
                return 2;
            }

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Error: Directory does not exist: {path}");
                return 4;
            }

            // Validate it looks like a GTA5 folder
            bool isValid = File.Exists(Path.Combine(path, "GTA5.exe")) ||
                          File.Exists(Path.Combine(path, "GTA5_Enhanced.exe")) ||
                          File.Exists(Path.Combine(path, "eboot.bin"));

            if (!isValid)
            {
                Console.WriteLine("Warning: This doesn't look like a GTA5 game folder.");
                Console.WriteLine("Expected GTA5.exe, GTA5_Enhanced.exe, or eboot.bin");
            }

            try
            {
                CliConfig.SetGameFolder(path);
                Console.WriteLine($"Default game folder set to: {path}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Gets and displays the default game folder
        /// </summary>
        public static int GetGameFolder()
        {
            var path = CliConfig.GetGameFolder();
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("No default game folder set.");
                Console.WriteLine("Use --set-game <path> to set one.");
                return 0;
            }

            Console.WriteLine($"Default game folder: {path}");
            
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Warning: This folder no longer exists!");
            }
            
            return 0;
        }

        /// <summary>
        /// Lists installed mod packages
        /// </summary>
        public static int ListPackages(string gameFolder)
        {
            if (string.IsNullOrEmpty(gameFolder))
            {
                // Auto-prompt for game folder
                gameFolder = PromptForGameFolder();
                if (string.IsNullOrEmpty(gameFolder))
                {
                    return 4;
                }
            }

            if (!Directory.Exists(gameFolder))
            {
                Console.WriteLine($"Error: Game folder not found: {gameFolder}");
                return 4;
            }

            try
            {
                var manager = new BackupManager(gameFolder);
                var packages = manager.GetInstalledPackages();

                if (packages.Count == 0)
                {
                    Console.WriteLine("No installed packages found.");
                    return 0;
                }

                Console.WriteLine($"Installed packages ({packages.Count}):");
                Console.WriteLine(new string('-', 60));
                
                foreach (var pkg in packages)
                {
                    string platform = pkg.IsGen9 ? "[Gen9]" : "[Legacy]";
                    Console.WriteLine($"  {platform} {pkg.PackageName}");
                    Console.WriteLine($"           Version: {pkg.Version}");
                    Console.WriteLine($"           Installed: {pkg.InstallDate}");
                    Console.WriteLine();
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing packages: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Validates game folder against package version and prompts user if needed.
        /// Returns true if valid/updated, false if cancelled.
        /// </summary>
        private static bool EnsureCorrectGameFolder(OivMetadata metadata, ref string gameFolder)
        {
            // Check Game Version Compatibility
            bool isGen9 = File.Exists(Path.Combine(gameFolder, "eboot.bin")) ||
                         File.Exists(Path.Combine(gameFolder, "GTA5_Enhanced.exe"));
            
            bool versionMismatch = false;
            string requiredVersion = "";

            if (metadata.GameVersion == GameVersion.Enhanced && !isGen9)
            {
                versionMismatch = true;
                requiredVersion = "Enhanced (Gen9)";
            }
            else if (metadata.GameVersion == GameVersion.Legacy && isGen9)
            {
                versionMismatch = true;
                requiredVersion = "Legacy (Old Gen)";
            }

            if (versionMismatch)
            {
                Console.WriteLine();
                Console.WriteLine("============================================================");
                Console.WriteLine($"WARNING: This package targets {requiredVersion}!");
                Console.WriteLine("The selected game folder appears to be the wrong version.");
                Console.WriteLine("============================================================");
                Console.WriteLine();
                Console.WriteLine($"Please select your GTA V {requiredVersion} game folder.");
                
                string newFolder = BrowseForGameFolder($"Select GTA V {requiredVersion} Game Folder");
                
                if (string.IsNullOrEmpty(newFolder))
                {
                    Console.WriteLine("Operation cancelled by user.");
                    return false;
                }
                
                gameFolder = newFolder;
                Console.WriteLine($"Game folder updated to: {gameFolder}");
                Console.WriteLine();
                return true;
            }
            else if (metadata.GameVersion == GameVersion.Any)
            {
                Console.WriteLine();
                Console.WriteLine("============================================================");
                Console.WriteLine("NOTICE: This package does not specify a target Game Version.");
                Console.WriteLine("To ensure safety, you must manually select the Game Folder.");
                Console.WriteLine("============================================================");
                Console.WriteLine();
                Console.WriteLine("Mod Authors: To automate this, add <gameversion> to your assembly.xml:");
                Console.WriteLine("  <metadata>");
                Console.WriteLine("    <gameversion>Enhanced</gameversion>  <!-- or Legacy -->");
                Console.WriteLine("    ...");
                Console.WriteLine("  </metadata>");
                Console.WriteLine();

                string newFolder = BrowseForGameFolder($"Select GTA V Game Folder for {metadata.Name}");
                
                if (string.IsNullOrEmpty(newFolder))
                {
                    Console.WriteLine("Operation cancelled by user.");
                    return false;
                }
                gameFolder = newFolder;
                Console.WriteLine($"Game folder selected: {gameFolder}");
                Console.WriteLine();
                return true;
            }

            return true;
        }



        /// <summary>
        /// Checks for essential modding files based on game version and warns if missing.
        /// Legacy: dinput8.dll + OpenIV.asi
        /// Enhanced: xinput1_4.dll + OpenRPF.asi
        /// </summary>
        private static void CheckModdingEnvironment(string gameFolder)
        {
            // Detect game version first
            bool isGen9 = File.Exists(Path.Combine(gameFolder, "eboot.bin")) || 
                          File.Exists(Path.Combine(gameFolder, "GTA5_Enhanced.exe"));

            string asiLoaderName = isGen9 ? "xinput1_4.dll" : "dinput8.dll";
            string openAsiName = isGen9 ? "OpenRPF.asi" : "OpenIV.asi";

            bool hasAsiLoader = File.Exists(Path.Combine(gameFolder, asiLoaderName));
            bool hasOpenAsi = File.Exists(Path.Combine(gameFolder, openAsiName));
            
            Console.WriteLine("Modding Environment:");
            string versionStr = isGen9 ? "Enhanced (Gen9)" : "Legacy";
            Console.WriteLine($"  Detected Version: {versionStr}");
            Console.WriteLine($"  ASI Loader ({asiLoaderName}): {(hasAsiLoader ? "Present" : "MISSING")}");
            Console.WriteLine($"  Mods Loader ({openAsiName}):   {(hasOpenAsi ? "Present" : "MISSING")}");
            
            if (!hasAsiLoader || !hasOpenAsi)
            {
                Console.WriteLine();
                Console.WriteLine("============================================================");
                Console.WriteLine("WARNING: Essential modding files are missing!");
                Console.WriteLine($"Mods will NOT load in-game without {asiLoaderName} and {openAsiName}.");
                Console.WriteLine("============================================================");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Installs an OIV package
        /// </summary>
        public static int RunInstall(string oivPath, string gameFolder)
        {
            if (ProcessHelper.IsGameRunning(out string processName))
            {
                Console.WriteLine($"Error: The game process '{processName}' is currently running.");
                Console.WriteLine("Please close the game before installing mods.");
                return 6; // Exit code 6 for Game Running
            }

            if (string.IsNullOrEmpty(oivPath))
            {
                Console.WriteLine("Error: No OIV package path specified.");
                return 2;
            }

            if (!File.Exists(oivPath))
            {
                Console.WriteLine($"Error: OIV file not found: {oivPath}");
                return 3;
            }

            if (string.IsNullOrEmpty(gameFolder))
            {
                // Auto-prompt for game folder
                gameFolder = PromptForGameFolder();
                if (string.IsNullOrEmpty(gameFolder))
                {
                    return 4;
                }
            }

            if (!Directory.Exists(gameFolder))
            {
                Console.WriteLine($"Error: Game folder not found: {gameFolder}");
                return 4;
            }

            try
            {
                Console.WriteLine($"Loading package: {Path.GetFileName(oivPath)}");
                var package = OivPackage.Load(oivPath);
                
                Console.WriteLine($"Package: {package.Metadata.Name} v{package.Metadata.Version}");
                
                string supportedGame = package.Metadata.GameVersion switch
                {
                    GameVersion.Enhanced => "GTA V Enhanced (Gen9)",
                    GameVersion.Legacy => "GTA V Legacy (Old Gen)",
                    _ => "Any GTA V Version"
                };
                Console.WriteLine($"Supported Game: {supportedGame}");
                
                Console.WriteLine($"Author: {package.Metadata.AuthorDisplayName}");
                Console.WriteLine($"Game folder: {gameFolder}");
                Console.WriteLine();

                // Validate Game Version
                if (!EnsureCorrectGameFolder(package.Metadata, ref gameFolder))
                {
                    return 5;
                }

                // Check Modding Environment
                CheckModdingEnvironment(gameFolder);

                // Initialize GTA5 keys
                Console.WriteLine("Initializing encryption keys...");
                bool isGen9 = File.Exists(Path.Combine(gameFolder, "eboot.bin")) ||
                             File.Exists(Path.Combine(gameFolder, "GTA5_Enhanced.exe"));
                GTA5Keys.LoadFromPath(gameFolder, isGen9, null);

                // Create installer and run
                var installer = new OivInstaller(gameFolder, package, msg => Console.WriteLine($"  {msg}"));
                
                var progress = new Progress<InstallProgress>(p =>
                {
                    Console.Write($"\r[{p.Percent,3}%] {p.Status,-50}");
                });

                Console.WriteLine("Installing...");
                installer.Install(progress);
                
                Console.WriteLine();
                Console.WriteLine("Installation complete!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error during installation: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Uninstalls a mod package by name
        /// </summary>
        public static int RunUninstall(string packageName, string gameFolder, bool useVanilla)
        {
            if (ProcessHelper.IsGameRunning(out string processName))
            {
                Console.WriteLine($"Error: The game process '{processName}' is currently running.");
                Console.WriteLine("Please close the game before uninstalling mods.");
                return 6;
            }

            if (string.IsNullOrEmpty(packageName))
            {
                Console.WriteLine("Error: No package name specified.");
                return 2;
            }

            if (string.IsNullOrEmpty(gameFolder))
            {
                // Auto-prompt for game folder
                gameFolder = PromptForGameFolder();
                if (string.IsNullOrEmpty(gameFolder))
                {
                    return 4;
                }
            }

            if (!Directory.Exists(gameFolder))
            {
                Console.WriteLine($"Error: Game folder not found: {gameFolder}");
                return 4;
            }

            try
            {
                var manager = new BackupManager(gameFolder);
                var packages = manager.GetInstalledPackages();
                
                // Find matching package (case-insensitive, bidirectional partial match)
                BackupLog targetPackage = null;
                foreach (var pkg in packages)
                {
                    // Exact match
                    if (pkg.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPackage = pkg;
                        break;
                    }
                    // Package name contains search term (User typed "Core" and we found "CoreFX")
                    // BUT NOT the other way around (User typed "CoreFX Roads" and we found "CoreFX" -> BAD)
                    if (pkg.PackageName.Contains(packageName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetPackage = pkg;
                        break;
                    }
                }

                if (targetPackage == null)
                {
                    Console.WriteLine($"Error: Package not found: {packageName}");
                    Console.WriteLine("Use --list to see installed packages.");
                    return 3;
                }

                Console.WriteLine($"Uninstalling: {targetPackage.PackageName}");
                Console.WriteLine($"Mode: {(useVanilla ? "Vanilla Reset" : "Revert to Backup")}");
                Console.WriteLine();

                // Initialize GTA5 keys
                Console.WriteLine("Initializing encryption keys...");
                bool isGen9 = targetPackage.IsGen9;
                GTA5Keys.LoadFromPath(gameFolder, isGen9, null);

                var mode = useVanilla ? UninstallMode.Vanilla : UninstallMode.Backup;
                var progress = new Progress<string>(msg => Console.WriteLine($"  {msg}"));

                manager.Uninstall(targetPackage, progress, mode);

                Console.WriteLine();
                Console.WriteLine("Uninstall complete!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error during uninstall: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Uninstalls a mod by loading the OIV file and extracting the package name from metadata
        /// </summary>
        public static int RunUninstallFromOiv(string oivPath, string gameFolder, bool useVanilla)
        {
            if (string.IsNullOrEmpty(oivPath))
            {
                Console.WriteLine("Error: No OIV package path specified.");
                return 2;
            }

            if (!File.Exists(oivPath))
            {
                Console.WriteLine($"Error: OIV file not found: {oivPath}");
                return 3;
            }

            try
            {
                Console.WriteLine($"Loading package to get name: {Path.GetFileName(oivPath)}");
                using var package = OivPackage.Load(oivPath);
                string packageName = package.Metadata.Name;
                
                Console.WriteLine($"Package name from metadata: {packageName}");
                
                string supportedGame = package.Metadata.GameVersion switch
                {
                    GameVersion.Enhanced => "GTA V Enhanced (Gen9)",
                    GameVersion.Legacy => "GTA V Legacy (Old Gen)",
                    _ => "Any GTA V Version"
                };
                Console.WriteLine($"Supported Game: {supportedGame}");
                Console.WriteLine();

                // Validate Game Version
                if (!EnsureCorrectGameFolder(package.Metadata, ref gameFolder))
                {
                    return 5;
                }
                
                // Check Modding Environment
                CheckModdingEnvironment(gameFolder);

                // Delegate to the standard uninstall with the correct package name
                return RunUninstall(packageName, gameFolder, useVanilla);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading OIV package: {ex.Message}");
                return 1;
            }
        }
    }
}
