using System;
using System.IO;
using System.Windows.Forms;

namespace CodeWalker.OIVInstaller
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            // Game data files are culture-neutral: floats are always written with a
            // '.' separator. On a locale that uses ',' (most of Europe and Latin
            // America) stray culture-dependent formatting turns 4.2 into "4,2", which
            // reads back as 42 — a silent 10x change to real values inside .ymap/.ymt
            // files. Pin the invariant culture so parsing and formatting can't drift
            // with the user's regional settings.
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = invariant;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = invariant;
            System.Threading.Thread.CurrentThread.CurrentCulture = invariant;

            // GUI launch intent for the bundled Install.bat / Uninstall.bat:
            //   exe <package>            → open the GUI with the package loaded
            //   exe <package> --manage   → open the GUI and jump to Manage Mods
            // The --manage flag must be detected before the CLI check so it doesn't
            // fall into console mode (which would happen if it were args[0]).
            bool openManage = HasFlag(args, "--manage") || HasFlag(args, "--uninstall-ui");

            // --preview <pkg.oivs> : open the selection wizard read-only (used by the
            // OIVS Packer's Preview button). Handled before the CLI check.
            string previewPath = GetFlagValue(args, "--preview");
            if (previewPath != null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                try
                {
                    if (!File.Exists(previewPath))
                        throw new FileNotFoundException("Package not found: " + previewPath);
                    using var pkg = OivsPackage.Load(previewPath);
                    using var wiz = new OivsSelectionForm(pkg, previewMode: true);
                    Application.Run(wiz);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't preview the package:\n\n" + ex.Message, "Preview",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 0;
            }

            // Check for CLI mode (console). Skipped when --manage requests the GUI.
            if (!openManage && args.Length > 0 && IsCliArg(args[0]))
            {
                return RunCli(args);
            }

            // GUI mode
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            var form = new MainForm();
            if (openManage) form.OpenManageOnShown = true;
            Application.Run(form);
            return 0;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args)
                if (a.Equals(flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Returns the argument following <flag>, or null if absent.
        private static string GetFlagValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        /// <summary>
        /// Checks if an argument is a CLI flag (starts with --)
        /// </summary>
        private static bool IsCliArg(string arg)
        {
            return arg.StartsWith("--") || arg.StartsWith("-h") || arg.StartsWith("/?");
        }

        /// <summary>
        /// Runs the CLI handler
        /// </summary>
        private static int RunCli(string[] args)
        {
            // Attach to parent console for output
            CliHandler.AttachToConsole();

            try
            {
                // Parse arguments
                string command = null;
                string oivPath = null;
                string packageName = null;
                string gameFolder = OivAppConfig.Load().LastGameFolder; // Use default if set
                bool useVanilla = false;
                bool force = false;
                bool skipBackup = false;
                string selectSpec = null;

                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLowerInvariant();

                    switch (arg)
                    {
                        case "--help":
                        case "-h":
                        case "/?":
                            return CliHandler.ShowHelp();

                        case "--set-game":
                            if (i + 1 < args.Length)
                            {
                                return CliHandler.SetGameFolder(args[++i]);
                            }
                            Console.WriteLine("Error: --set-game requires a path argument.");
                            return 2;

                        case "--get-game":
                            return CliHandler.GetGameFolder();

                        case "--install":
                            command = "install";
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            {
                                oivPath = args[++i];
                            }
                            break;

                        case "--uninstall":
                            command = "uninstall";
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            {
                                packageName = args[++i];
                            }
                            break;

                        case "--uninstall-oiv":
                            command = "uninstall-oiv";
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            {
                                oivPath = args[++i];
                            }
                            break;

                        case "--list":
                            command = "list";
                            break;

                        case "--list-options":
                            command = "list-options";
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            {
                                oivPath = args[++i];
                            }
                            break;

                        case "--select":
                            if (i + 1 < args.Length)
                            {
                                selectSpec = args[++i];
                            }
                            else
                            {
                                Console.WriteLine("Error: --select requires a value (e.g. \"roads,streetlights=coffee\").");
                                return 2;
                            }
                            break;

                        case "--game":
                            if (i + 1 < args.Length)
                            {
                                gameFolder = args[++i];
                            }
                            else
                            {
                                Console.WriteLine("Error: --game requires a path argument.");
                                return 2;
                            }
                            break;

                        case "--vanilla":
                            useVanilla = true;
                            break;

                        case "--force":
                        case "--ignore_gameversion":
                            force = true;
                            break;

                        case "--skip_backup":
                        case "--skip-backup":
                            skipBackup = true;
                            break;

                        default:
                            // Unknown argument - might be a path for install?
                            if (!arg.StartsWith("-") && File.Exists(args[i]) &&
                                (args[i].EndsWith(".oiv", StringComparison.OrdinalIgnoreCase) ||
                                 args[i].EndsWith(".oivs", StringComparison.OrdinalIgnoreCase)))
                            {
                                oivPath = args[i];
                                command = "install";
                            }
                            else if (arg.StartsWith("-"))
                            {
                                Console.WriteLine($"Unknown option: {args[i]}");
                                Console.WriteLine("Use --help for usage information.");
                                return 2;
                            }
                            break;
                    }
                }

                // Execute command
                switch (command)
                {
                    case "install":
                        if (!string.IsNullOrEmpty(oivPath) &&
                            oivPath.EndsWith(".oivs", StringComparison.OrdinalIgnoreCase))
                        {
                            return CliHandler.RunInstallOivs(oivPath, gameFolder, selectSpec, force, skipBackup);
                        }
                        return CliHandler.RunInstall(oivPath, gameFolder, force, skipBackup);

                    case "list-options":
                        return CliHandler.ListOivsOptions(oivPath);

                    case "uninstall":
                        return CliHandler.RunUninstall(packageName, gameFolder, useVanilla);

                    case "uninstall-oiv":
                        return CliHandler.RunUninstallFromOiv(oivPath, gameFolder, useVanilla);

                    case "list":
                        return CliHandler.ListPackages(gameFolder);

                    default:
                        Console.WriteLine("No command specified.");
                        return CliHandler.ShowHelp();
                }
            }
            finally
            {
                CliHandler.DetachConsole();
            }
        }
    }
}
