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
            // Check for CLI mode
            if (args.Length > 0 && IsCliArg(args[0]))
            {
                return RunCli(args);
            }

            // GUI mode
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new MainForm());
            return 0;
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

                        default:
                            // Unknown argument - might be a path for install?
                            if (!arg.StartsWith("-") && File.Exists(args[i]) && 
                                args[i].EndsWith(".oiv", StringComparison.OrdinalIgnoreCase))
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
                        return CliHandler.RunInstall(oivPath, gameFolder);

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
