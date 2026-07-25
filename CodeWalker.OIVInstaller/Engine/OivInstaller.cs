using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CodeWalker.GameFiles;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Handles the installation of OIV packages into the game
    /// </summary>
    public class OivInstaller
    {
        public string GameFolder { get; }
        public string ModsFolder => Path.Combine(GameFolder, "mods");
        public OivPackage Package { get; }
        
        private readonly Action<string> _logAction;
        private StreamWriter _logWriter;
        private readonly Dictionary<string, RpfFile> _openRpfs = new Dictionary<string, RpfFile>(StringComparer.OrdinalIgnoreCase);
        private bool _keysInitialized = false;
        private BackupManager _backupManager;
        private BackupSession _backupSession;
        private bool _skipBackup = false;

        /// <summary>Largest file that can be packed into an RPF entry: the CLR's
        /// byte[] ceiling. Files bound for the file system stream and have no limit.</summary>
        private const long MaxRpfEntryBytes = 2_147_483_591L;

        public OivInstaller(string gameFolder, OivPackage package, Action<string> logAction = null)
        {
            GameFolder = gameFolder;
            Package = package;
            _logAction = logAction ?? (_ => { });

            // Determine Gen9 status
            bool isGen9 = File.Exists(Path.Combine(GameFolder, "eboot.bin")) || 
                          File.Exists(Path.Combine(GameFolder, "GTA5_Enhanced.exe"));

            _backupManager = new BackupManager(GameFolder);
            _backupSession = _backupManager.CreateSession(
                Package.Metadata.Name, 
                Package.Metadata.Description, 
                Package.Metadata.Version,
                isGen9
            );
        }

        /// <summary>
        /// Installs the package specified in the constructor
        /// </summary>
        public void Install(IProgress<InstallProgress> progress = null, List<BackupLog> packagesToUninstall = null, UninstallMode uninstallMode = UninstallMode.Backup, bool skipBackup = false)
        {
            _skipBackup = skipBackup;
            InitializeLog();

            int totalOps = CountOperations(Package.Operations);
            int currentOp = 0;

            Log($"Starting installation of {Package.Metadata.Name} v{Package.Metadata.Version}");
            Log($"Game folder: {GameFolder}");
            Log($"Mods folder: {ModsFolder}");
            
            // Initialize GTA5Keys - required for reading encrypted RPF files and cleanup
            InitializeKeys(progress);
            
            // Handle requests to uninstall previous versions (passed from UI prompt)
            if (packagesToUninstall != null && packagesToUninstall.Count > 0)
            {
                Log("----------------------------------------");
                Log($"Removing {packagesToUninstall.Count} package(s) as requested...");
                Log($"Mode: {uninstallMode}");
                
                var uninstallProgress = new Progress<string>(msg => Log($"[Cleanup] {msg}"));
                
                foreach (var oldPkg in packagesToUninstall)
                {
                    Log($"Removing previous version installed on {oldPkg.InstallDate}...");
                    _backupManager.Uninstall(oldPkg, uninstallProgress, uninstallMode);
                }
                Log("Cleanup complete. Proceeding with new installation...");
                Log("----------------------------------------");
            }
            
            // Ensure mods folder exists
            if (!Directory.Exists(ModsFolder))
            {
                Log("Creating mods folder...");
                Directory.CreateDirectory(ModsFolder);
            }

            try
            {
                foreach (var op in Package.Operations)
                {
                    ProcessOperation(op, null, null, progress, ref currentOp, totalOps);
                }

                // Save backup log
                if (!_skipBackup) _backupSession.Save();
                Log("Backup created and installation completed successfully!");
                progress?.Report(new InstallProgress(100, "Installation complete"));
            }
            finally
            {
                // Close all opened RPF files
                foreach (var rpf in _openRpfs.Values)
                {
                    // RPF files don't need explicit closing in CodeWalker's implementation
                }
                _openRpfs.Clear();
                _logWriter?.Dispose();
                _logWriter = null;
            }
        }

        /// <summary>
        /// Initializes GTA5Keys required for RPF decryption
        /// </summary>
        private void InitializeKeys(IProgress<InstallProgress> progress)
        {
            if (_keysInitialized) return;
            if (GTA5Keys.PC_AES_KEY != null) 
            {
                _keysInitialized = true;
                return;
            }

            Log("Initializing encryption keys (scanning game exe)...");
            progress?.Report(new InstallProgress(0, "Scanning game exe for keys..."));

            try
            {
                // Try to detect if this is Gen9 (Enhanced/Next-Gen) version
                bool isGen9 = File.Exists(Path.Combine(GameFolder, "eboot.bin")) || 
                              File.Exists(Path.Combine(GameFolder, "GTA5_Enhanced.exe"));
                
                GTA5Keys.LoadFromPath(GameFolder, isGen9, null);
                
                if (GTA5Keys.PC_AES_KEY != null)
                {
                    Log("Encryption keys loaded successfully.");
                    _keysInitialized = true;
                }
                else
                {
                    Log("WARNING: Could not load encryption keys. RPF operations may fail.");
                }
            }
            catch (Exception ex)
            {
                Log($"WARNING: Error loading encryption keys: {ex.Message}");
            }
        }

        private void ProcessOperation(OivOperation op, RpfFile currentRpf, RpfDirectoryEntry currentDir,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            switch (op)
            {
                case OivArchiveOperation archiveOp:
                    ProcessArchiveOperation(archiveOp, currentRpf, progress, ref currentOp, totalOps);
                    break;
                case OivAddOperation addOp:
                    ProcessAddOperation(addOp, currentRpf, currentDir, progress, ref currentOp, totalOps);
                    break;
                case OivDeleteOperation deleteOp:
                    ProcessDeleteOperation(deleteOp, currentRpf, currentDir, progress, ref currentOp, totalOps);
                    break;
                case OivTextOperation textOp:
                    ProcessTextOperation(textOp, currentRpf, progress, ref currentOp, totalOps);
                    break;
                case OivPsoOperation psoOp:
                    ProcessPsoOperation(psoOp, currentRpf, progress, ref currentOp, totalOps);
                    break;
                case OivXmlOperation xmlOp:
                    ProcessXmlOperation(xmlOp, currentRpf, progress, ref currentOp, totalOps);
                    break;
                case OivDefragmentationOperation defragOp:
                    Log($"Defragmentation requested for {defragOp.Variable} (Not Implemented - placeholder)");
                    currentOp++; // Count it as done
                    break;
            }
        }

        private void ProcessArchiveOperation(OivArchiveOperation op, RpfFile parentRpf,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            string archivePath = op.ArchivePath.TrimStart('\\', '/');
            Log($"Processing archive: {archivePath}");

            RpfFile rpf;
            if (parentRpf == null)
            {
                // Top-level archive - needs to be in mods folder
                rpf = GetOrCopyRpfToMods(archivePath, op.CreateIfNotExist);
            }
            else
            {
                // Nested archive within another RPF
                rpf = GetNestedRpf(parentRpf, archivePath, op.CreateIfNotExist);
            }

            if (rpf == null)
            {
                Log($"ERROR: Could not open archive {archivePath}");
                return;
            }

            // Ensure OPEN encryption
            EnsureOpenEncryption(rpf);

            // Process child operations
            foreach (var childOp in op.Children)
            {
                ProcessOperation(childOp, rpf, rpf.Root, progress, ref currentOp, totalOps);
            }
        }

        private void ProcessAddOperation(OivAddOperation op, RpfFile rpf, RpfDirectoryEntry currentDir,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            currentOp++;
            int percent = (int)((currentOp * 100.0) / totalOps);
            progress?.Report(new InstallProgress(percent, $"Adding: {op.Destination}"));
            
            Log($"Adding: {op.Source} -> {op.Destination}");

            if (rpf == null)
            {
                // Top-level add operation - copy file directly to mods folder
                ProcessTopLevelAdd(op);
                return;
            }

            try
            {
                // A file stored INSIDE an RPF has to pass through a byte[], which tops
                // out just under 2 GB. Say that plainly rather than surfacing .NET's
                // "The file is too long" — a payload this big belongs on the file
                // system (a dlcpacks entry), not packed into an archive.
                long srcSize = Package.GetContentFileSize(op.Source);
                if (srcSize > MaxRpfEntryBytes)
                {
                    Log($"ERROR: {op.Source} is {FormatSize(srcSize)}, too large to store inside " +
                        $"{Path.GetFileName(rpf.Path)} (limit {FormatSize(MaxRpfEntryBytes)} per file in an archive). " +
                        "Install a file this large straight to the game folder instead — e.g. as a dlcpacks dlc.rpf.");
                    return;
                }

                // Read source file from OIV package
                byte[] fileData = Package.ReadContentFile(op.Source);

                // Ensure destination directory exists within RPF
                string destPath = op.Destination.Replace("/", "\\").TrimStart('\\');
                string destDir = Path.GetDirectoryName(destPath);
                string destFileName = Path.GetFileName(destPath);

                RpfDirectoryEntry targetDir = rpf.Root;
                if (!string.IsNullOrEmpty(destDir))
                {
                    targetDir = EnsureDirectory(rpf, destDir);
                }

                if (targetDir == null)
                {
                    Log($"ERROR: Could not create directory: {destDir}");
                    return;
                }

                // Add or replace the file
                
                // --- BACKUP LOGIC (RPF) ---
                var existingFile = targetDir.Files?.FirstOrDefault(f => f.Name.Equals(destFileName, StringComparison.OrdinalIgnoreCase));
                if (existingFile != null) Log($"  Backing up original file: {destFileName}");
                if (existingFile != null)
                {
                    // It's a replacement - backup original with RSC7 header preserved
                    byte[] oldData = RpfFileHelper.ExtractFileRaw(existingFile as RpfFileEntry);
                    if (!_skipBackup) _backupSession.BackupRpfFile(rpf.Path, destPath, oldData); // destPath is internal RPF path
                }
                else
                {
                    // It's a new file - log as added
                    // BackupManager handles "added" logic if we pass a path that doesn't exist on disk?
                    // But for RPF, we need to log it specially as "RPF Added".
                    // Current BackupManager.BackupRpfFile assumes we have data.
                    // Let's overload or extend BackupSession for "RpfAdded".
                    // Use a trick: pass null data to signify "Added"? Or update BackupManager.
                    // For now, let's just assume we only backup what we replace. 
                    // To support uninstalling added files in RPF, we need to track them.
                    // Let's add 'BackupRpfAdded' method to session.
                    if (!_skipBackup) _backupSession.TrackRpfAdded(rpf.Path, destPath);
                }
                // ---------------------------

                RpfFile.CreateFile(targetDir, destFileName, fileData, overwrite: true);
                Log($"  Added {destFileName} ({fileData.Length} bytes)");
            }
            catch (Exception ex)
            {
                Log($"ERROR adding file {op.Destination}: {ex.Message}");
            }
        }

        private void ProcessDeleteOperation(OivDeleteOperation op, RpfFile rpf, RpfDirectoryEntry currentDir,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            currentOp++;
            int percent = (int)((currentOp * 100.0) / totalOps);
            progress?.Report(new InstallProgress(percent, $"Deleting: {op.Target}"));
            
            Log($"Deleting: {op.Target}");

            if (rpf != null)
            {
                // RPF Deletion
                string targetPath = op.Target.Replace("/", "\\").TrimStart('\\');
                string fileName = Path.GetFileName(targetPath);
                string dirPath = Path.GetDirectoryName(targetPath);
                
                // Navigate to directory (relative to currentDir? Spec says full path usually?)
                // Spec says: "The node itself contain full path in the target game." but inside archive it behaves like file commands.
                // Assuming relative to current RPF root or currentDir? 
                // "ProcessAddOperation" uses "rpf.Root" if destDir is empty.
                // Let's FindDirectory starting from Root (absolute in RPF)
                RpfDirectoryEntry targetDir = rpf.Root;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    targetDir = FindDirectory(rpf, dirPath);
                }
                
                if (targetDir == null)
                {
                    Log($"  Directory not found: {dirPath}");
                    return;
                }
                
                var file = targetDir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (file != null)
                {
                    // Backup before delete! (preserve RSC7 header for resource files)
                    byte[] originalData = RpfFileHelper.ExtractFileRaw(file as RpfFileEntry);
                    if (!_skipBackup) _backupSession.BackupRpfDeletedFile(rpf.Path, targetPath, originalData);
                    
                    RpfFile.DeleteEntry(file);
                    Log($"  Deleted {fileName} from RPF");
                }
                else
                {
                    // Maybe it's a directory? OIV <delete> usually targets files.
                   Log($"  File not found in RPF: {targetPath}");
                }
            }
            else
            {
                // Filesystem Deletion
                string targetPath = Path.Combine(GameFolder, op.Target);
                
                if (File.Exists(targetPath))
                {
                    try
                    {
                        // Backup before delete!
                        string relPath = targetPath.Substring(GameFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                        if (!_skipBackup) _backupSession.BackupDeletedFile(relPath);

                        File.Delete(targetPath);
                        Log($"  Deleted {op.Target}");
                    }
                    catch (Exception ex)
                    {
                        Log($"  Could not delete {op.Target}: {ex.Message}");
                    }
                }
                else
                {
                    Log($"  File not found (skipped): {op.Target}");
                }
            }
        }

        /// <summary>
        /// Handles top-level add operations (files copied outside of RPF archives)
        /// RPF files go to mods folder, other files (.asi, .dll, scripts) go to game folder
        /// </summary>
        private void ProcessTopLevelAdd(OivAddOperation op)
        {
            try
            {
                // Normalize destination path
                string destPath = op.Destination.Replace("/", "\\").TrimStart('\\');

                string targetFolder = GameFolder;
                string modsFolder = Path.Combine(GameFolder, "mods");

                // A package may already spell the destination out as "mods\...". Strip
                // that prefix so it isn't re-applied below into mods\mods\... (same
                // normalization GetOrCopyRpfToMods does for archive paths).
                bool destWasModsPrefixed =
                    destPath.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase);
                if (destWasModsPrefixed) destPath = destPath.Substring(5);

                // Determine if this should go to mods folder
                // RPF files, or files going to update/ or x64/ typically belong in mods
                bool useMods = destWasModsPrefixed ||
                               destPath.StartsWith("update", StringComparison.OrdinalIgnoreCase) ||
                               destPath.StartsWith("x64", StringComparison.OrdinalIgnoreCase) ||
                               destPath.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase);

                if (useMods)
                {
                    // Ensure mods folder exists if we're using it
                    if (!Directory.Exists(modsFolder))
                    {
                        Directory.CreateDirectory(modsFolder);
                    }
                    targetFolder = modsFolder;
                    Log($"  Targeting mods folder for: {destPath}");
                }
                
                string fullDestPath = Path.Combine(targetFolder, destPath);

                // Ensure destination directory exists
                string destDir = Path.GetDirectoryName(fullDestPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    Log($"  Created directory: {destDir}");
                }

                // --- BACKUP LOGIC ---
                // destPath is relative to GameFolder?? No, wait.
                // op.Destination is relative to game folder for top level adds
                // fullDestPath is absolute path.
                // We need relative path for backup manager

                string relativeDestPath = fullDestPath.Substring(GameFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                Log($"  Backing up: {relativeDestPath}");
                if (!_skipBackup) _backupSession.BackupFile(relativeDestPath);
                // --------------------

                // Stream the file to the game folder. This is deliberately NOT
                // ReadAllBytes+WriteAllBytes: byte[] caps out just under 2 GB, which
                // made packages shipping a prebuilt multi-gigabyte dlc.rpf fail with
                // "The file is too long". Copying also keeps memory flat.
                long size = Package.GetContentFileSize(op.Source);
                EnsureDiskSpace(fullDestPath, size, destPath);
                Package.CopyContentFileTo(op.Source, fullDestPath);
                Log($"  Copied to game: {destPath} ({FormatSize(size >= 0 ? size : new FileInfo(fullDestPath).Length)})");
            }
            catch (Exception ex)
            {
                Log($"ERROR copying file {op.Destination}: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles text file modification operations (line insertion)
        /// </summary>
        private void ProcessTextOperation(OivTextOperation op, RpfFile rpf,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            currentOp++;
            int percent = (int)((currentOp * 100.0) / totalOps);
            progress?.Report(new InstallProgress(percent, $"Editing: {op.FilePath}"));
            
            Log($"Editing text file: {op.FilePath}");

            // --- BACKUP LOGIC ---
            // We need to backup the ORIGINAL data before modifying, 
            // AND track specific operations for smart reversal if possible.
            List<TextEditOperation> textOps = new List<TextEditOperation>();
            // --------------------
            
            if (rpf == null)
            {
                Log($"ERROR: No RPF context for text operation on {op.FilePath}");
                return;
            }

            try
            {
                string filePath = op.FilePath.Replace("/", "\\").TrimStart('\\');
                string dirPath = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                // Find the file entry in the RPF
                RpfDirectoryEntry targetDir = rpf.Root;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    targetDir = FindDirectory(rpf, dirPath);
                }

                if (targetDir == null)
                {
                    Log($"ERROR: Directory not found: {dirPath}");
                    return;
                }

                // Find the file
                var fileEntry = targetDir.Files?.FirstOrDefault(f => 
                    f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) as RpfFileEntry;

                if (fileEntry == null)
                {
                    if (op.CreateIfNotExist)
                    {
                        Log($"  File not found, creating: {fileName}");
                        // Create empty file - will be populated with inserts
                    }
                    else
                    {
                        Log($"ERROR: File not found: {op.FilePath}");
                        return;
                    }
                }

                // Read current file content
                string content = "";
                if (fileEntry != null)
                {
                    byte[] data = fileEntry.File.ExtractFile(fileEntry);
                    content = TrimBom(Encoding.UTF8.GetString(data));
                }

                // Apply all insert operations
                foreach (var insert in op.Inserts)
                {
                    content = ApplyInsertOperation(content, insert, textOps);
                }

                // Apply all replace operations
                foreach (var replace in op.Replacements)
                {
                    content = ApplyReplaceOperation(content, replace, textOps);
                }

                // Apply all add (append) operations
                foreach (var add in op.Adds)
                {
                    content += Environment.NewLine + add.Content;
                    textOps.Add(new TextEditOperation { Type = "Add", AddedContent = add.Content });
                }

                // Apply all delete operations
                foreach (var del in op.Deletions)
                {
                    content = ApplyTextDeleteOperation(content, del, textOps);
                }

                // Write modified content back to RPF
                
                // --- BACKUP LOGIC (Text) ---
                if (fileEntry != null && !_skipBackup)
                {
                     byte[] originalBytes = RpfFileHelper.ExtractFileRaw(fileEntry);
                     _backupSession.BackupRpfFile(rpf.Path, filePath, originalBytes, textOps: textOps);
                }
                // ---------------------------

                byte[] newData = Encoding.UTF8.GetBytes(content);
                RpfFile.CreateFile(targetDir, fileName, newData, overwrite: true);
                Log($"  Modified {fileName} ({newData.Length} bytes)");
            }
            catch (Exception ex)
            {
                Log($"ERROR editing file {op.FilePath}: {ex.Message}");
            }
        }

        private string ApplyReplaceOperation(string content, OivReplaceOperation op, List<TextEditOperation> trackOps)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            bool modified = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                bool match = false;

                if (op.Condition.Equals("Mask", StringComparison.OrdinalIgnoreCase))
                {
                    try 
                    {
                         match = Regex.IsMatch(line, WildcardToRegex(op.LinePattern), RegexOptions.IgnoreCase);
                    }
                    catch { match = line.Equals(op.LinePattern, StringComparison.OrdinalIgnoreCase); }
                }
                else if (op.Condition.Equals("Equal", StringComparison.OrdinalIgnoreCase))
                {
                    match = line.Equals(op.LinePattern, StringComparison.OrdinalIgnoreCase);
                }
                else if (op.Condition.Equals("StartWith", StringComparison.OrdinalIgnoreCase))
                {
                    match = line.Trim().StartsWith(op.LinePattern, StringComparison.OrdinalIgnoreCase); 
                }
                else if (op.Condition.Equals("EndWith", StringComparison.OrdinalIgnoreCase))
                {
                    match = line.Trim().EndsWith(op.LinePattern, StringComparison.OrdinalIgnoreCase);
                }
                else if (op.Condition.Equals("Contains", StringComparison.OrdinalIgnoreCase))
                {
                    match = line.IndexOf(op.LinePattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (match)
                {
                    string oldContent = lines[i];
                    lines[i] = op.Content; // Replace entire line
                    trackOps?.Add(new TextEditOperation 
                    { 
                        Type = "Replace", 
                        AddedContent = op.Content,
                        RemovedContent = oldContent,
                        LineNumber = i + 1 
                    });
                    modified = true;
                }
            }

            if (!modified)
            {
                Log($"    WARNING: No match found for replace: {op.LinePattern} ({op.Condition})");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string ApplyTextDeleteOperation(string content, OivTextDeleteOperation op, List<TextEditOperation> trackOps)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            bool modified = false;

            for (int i = lines.Count - 1; i >= 0; i--) // Iterate backwards to remove safely
            {
                string line = lines[i];
                bool match = false;

                if (op.Condition.Equals("Mask", StringComparison.OrdinalIgnoreCase))
                {
                    try 
                    {
                         match = Regex.IsMatch(line, WildcardToRegex(op.Content), RegexOptions.IgnoreCase);
                    }
                    catch { match = line.Equals(op.Content, StringComparison.OrdinalIgnoreCase); }
                }
                else if (op.Condition.Equals("Equal", StringComparison.OrdinalIgnoreCase))
                {
                    match = line.Equals(op.Content, StringComparison.OrdinalIgnoreCase);
                }
                else if (op.Condition.Equals("StartWith", StringComparison.OrdinalIgnoreCase))
                {
                     match = line.Trim().StartsWith(op.Content, StringComparison.OrdinalIgnoreCase); 
                }
                else if (op.Condition.Equals("Contains", StringComparison.OrdinalIgnoreCase))
                {
                     match = line.IndexOf(op.Content, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (match)
                {
                     string removedLine = lines[i];
                    lines.RemoveAt(i);
                    trackOps?.Add(new TextEditOperation 
                    { 
                        Type = "Delete", 
                        RemovedContent = removedLine,
                        LineNumber = i + 1 
                    });
                    modified = true;
                }
            }

            if (!modified)
            {
                Log($"    WARNING: No match found for delete: {op.Content} ({op.Condition})");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }

        /// <summary>
        /// Handles XML file modification using XPath operations
        /// </summary>
        private void ProcessXmlOperation(OivXmlOperation op, RpfFile rpf,
            IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            currentOp++;
            int percent = (int)((currentOp * 100.0) / totalOps);
            progress?.Report(new InstallProgress(percent, $"Editing XML: {op.FilePath}"));
            
            Log($"Editing XML file: {op.FilePath}");

            if (rpf == null)
            {
                Log($"ERROR: No RPF context for XML operation on {op.FilePath}");
                return;
            }

            try
            {
                string filePath = op.FilePath.Replace("/", "\\").TrimStart('\\');
                string dirPath = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                // Find the file entry in the RPF
                RpfDirectoryEntry targetDir = rpf.Root;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    targetDir = FindDirectory(rpf, dirPath);
                }

                if (targetDir == null)
                {
                    Log($"ERROR: Directory not found: {dirPath}");
                    return;
                }

                // Find the file
                var fileEntry = targetDir.Files?.FirstOrDefault(f => 
                    f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) as RpfFileEntry;

                if (fileEntry == null)
                {
                    Log($"ERROR: File not found: {op.FilePath}");
                    return;
                }

                // Read and parse XML
                byte[] data = fileEntry.File.ExtractFile(fileEntry);
                string xmlContent = "";
                string virtualXmlName = fileName;
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                bool isBinary = false;

                try
                {
                    string genXml = MetaXml.GetXml(fileEntry, data, out string outVirtualName, "");
                    if (!string.IsNullOrEmpty(genXml))
                    {
                        xmlContent = genXml;
                        virtualXmlName = outVirtualName;
                        isBinary = true;
                        Log($"  Decompiled binary {ext} to XML for editing.");
                    }
                    else
                    {
                        xmlContent = TrimBom(Encoding.UTF8.GetString(data));
                    }
                }
                catch (Exception ex)
                {
                    Log($"  WARNING: Failed to decompile binary {ext}, falling back to raw text. ({ex.Message})");
                    xmlContent = TrimBom(Encoding.UTF8.GetString(data));
                }
                
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = !isBinary;
                xmlDoc.LoadXml(xmlContent);
                
                // Track operations for smart reversal
                List<XmlEditOperation> xmlOps = new List<XmlEditOperation>();

                // Apply XPath operations
                foreach (var addOp in op.Adds)
                {
                    ApplyXmlAddOperation(xmlDoc, addOp, xmlOps);
                }

                foreach (var repOp in op.Replacements)
                {
                    ApplyXmlReplaceOperation(xmlDoc, repOp, xmlOps);
                }

                foreach (var remOp in op.Removals)
                {
                    ApplyXmlRemoveOperation(xmlDoc, remOp, xmlOps);
                }

                // Write modified content back to RPF
                byte[] newData = null;
                try
                {
                    if (isBinary)
                    {
                        int trimLen = 0;
                        MetaFormat format = XmlMeta.GetXMLFormat(virtualXmlName, out trimLen);
                        newData = XmlMeta.GetData(xmlDoc, format, virtualXmlName);
                        Log($"  Recompiled XML back to binary {ext}.");
                    }
                    else
                    {
                        using (var sw = new StringWriterWithEncoding(Encoding.UTF8))
                        {
                            xmlDoc.Save(sw);
                            newData = Encoding.UTF8.GetBytes(sw.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: Failed to recompile XML back to binary: {ex.Message}");
                    return;
                }

                if (newData == null || newData.Length == 0)
                {
                    Log($"  ERROR: Recompiled data is empty.");
                    return;
                }
                
                // --- BACKUP LOGIC (XML) ---
                if (fileEntry != null && !_skipBackup)
                {
                    byte[] originalBytes = RpfFileHelper.ExtractFileRaw(fileEntry);
                    _backupSession.BackupRpfFile(rpf.Path, filePath, originalBytes, xmlOps: xmlOps);
                }
                // --------------------------

                RpfFile.CreateFile(targetDir, fileName, newData, overwrite: true);
                Log($"  Modified {fileName} ({newData.Length} bytes)");
            }
            catch (Exception ex)
            {
                Log($"ERROR editing XML {op.FilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies an XPath add operation to an XML document
        /// </summary>
        /// <summary>
        /// Applies an XPath add operation to an XML document
        /// </summary>
        private void ApplyXmlAddOperation(XmlDocument xmlDoc, OivXmlAddOperation addOp, List<XmlEditOperation> trackOps)
        {
            string contentTrimmed = addOp.Content.Trim();

            // Find target node using XPath
            var targetNode = XmlCaseTolerant.SelectSingleNode(xmlDoc, addOp.XPath, out bool ciMatch);
            if (targetNode == null)
            {
                Log($"  WARNING: XPath not found: {addOp.XPath}");
                return;
            }
            if (ciMatch) Log($"  Note: XPath matched case-insensitively: {addOp.XPath}");

            string appendMode = addOp.Append ?? "Last";

            // Determine the container that will actually receive the new node.
            // For Before/After the new node becomes a sibling of the target, so the
            // container is the target's parent. For First/Last it's the target itself.
            XmlNode container;
            if (appendMode.Equals("Before", StringComparison.OrdinalIgnoreCase) ||
                appendMode.Equals("After", StringComparison.OrdinalIgnoreCase))
            {
                if (targetNode.ParentNode == null)
                {
                    Log($"  WARNING: Cannot insert {appendMode.ToLowerInvariant()} root node: {addOp.XPath}");
                    return;
                }
                container = targetNode.ParentNode;
            }
            else
            {
                container = targetNode;
            }

            // Scope the duplicate check to the immediate insertion container only.
            // Why: a global xmlDoc.InnerXml.Contains(...) check incorrectly skipped
            // legitimate adds where the same content was being inserted under multiple
            // different parents (e.g. same vehicle name added to several popgroups).
            if (IsDuplicateInContainer(xmlDoc, container, addOp.Content))
            {
                Log($"  Skipping XPath add (already exists in target container): {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
                return;
            }

            // Create new node from content
            var fragment = xmlDoc.CreateDocumentFragment();
            fragment.InnerXml = "\r\n\t\t" + addOp.Content + "\r\n\t";

            // Append based on position
            if (appendMode.Equals("First", StringComparison.OrdinalIgnoreCase))
            {
                targetNode.PrependChild(fragment);
                Log($"  Added at start of {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else if (appendMode.Equals("Before", StringComparison.OrdinalIgnoreCase))
            {
                targetNode.ParentNode.InsertBefore(fragment, targetNode);
                Log($"  Added before {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else if (appendMode.Equals("After", StringComparison.OrdinalIgnoreCase))
            {
                targetNode.ParentNode.InsertAfter(fragment, targetNode);
                Log($"  Added after {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else // Last or default
            {
                targetNode.AppendChild(fragment);
                Log($"  Added to {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }

            // Track for smart reversal
            trackOps?.Add(new XmlEditOperation
            {
                Type = "Add",
                XPath = addOp.XPath, // Note: Tracking precise XPath of NEW node is hard if user gave parent XPath. 
                                     // For now, store the Parent XPath and content, reversal logic will need to handle "Remove this content from this parent"
                AddedXml = addOp.Content,
                Append = appendMode
            });
        }


        /// <summary>
        /// Returns true if the immediate children of <paramref name="container"/> already include
        /// every top-level element from <paramref name="content"/> (whitespace-insensitive).
        /// Used to skip true sibling-level duplicates without blocking the same content from
        /// being added under a different parent elsewhere in the document.
        /// </summary>
        private static bool IsDuplicateInContainer(XmlDocument xmlDoc, XmlNode container, string content)
        {
            if (container == null || string.IsNullOrWhiteSpace(content)) return false;

            XmlDocumentFragment probe;
            try
            {
                probe = xmlDoc.CreateDocumentFragment();
                probe.InnerXml = content;
            }
            catch (XmlException)
            {
                // Content isn't well-formed XML on its own (e.g. raw text fragment).
                // Fall back to a direct text comparison against container's inner XML.
                // Case-insensitive: GTA data names are joaat-hashed, so "BURRITO4"
                // and "burrito4" are the same entry to the game.
                return NormalizeXmlForCompare(container.InnerXml).IndexOf(
                    NormalizeXmlForCompare(content), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var newKeys = new List<string>();
            foreach (XmlNode n in probe.ChildNodes)
            {
                if (n.NodeType == XmlNodeType.Element)
                {
                    newKeys.Add(NormalizeXmlForCompare(n.OuterXml));
                }
            }
            if (newKeys.Count == 0) return false;

            // OrdinalIgnoreCase: GTA data names are joaat-hashed (case-insensitive),
            // so an existing <Name>BURRITO4</Name> is a duplicate of <Name>burrito4</Name>.
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode child in container.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    existingKeys.Add(NormalizeXmlForCompare(child.OuterXml));
                }
            }

            foreach (var key in newKeys)
            {
                if (!existingKeys.Contains(key)) return false;
            }
            return true;
        }

        private static string NormalizeXmlForCompare(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return string.Empty;
            return Regex.Replace(xml, @">\s+<", "><").Trim();
        }

        private void ApplyXmlReplaceOperation(XmlDocument xmlDoc, OivXmlReplaceOperation op, List<XmlEditOperation> trackOps)
        {
            var node = XmlCaseTolerant.SelectSingleNode(xmlDoc, op.XPath, out bool ciMatch);
            if (node == null)
            {
                Log($"  WARNING: XPath not found for replace: {op.XPath}");
                return;
            }
            if (ciMatch) Log($"  Note: XPath matched case-insensitively: {op.XPath}");

            string oldXml = node.OuterXml;

            var fragment = xmlDoc.CreateDocumentFragment();
            fragment.InnerXml = op.Content;
            
            if (node.ParentNode != null)
            {
                node.ParentNode.ReplaceChild(fragment, node);
                
                trackOps?.Add(new XmlEditOperation
                {
                    Type = "Replace",
                    XPath = op.XPath,
                    AddedXml = op.Content,
                    RemovedXml = oldXml
                });
            }
        }

        private void ApplyXmlRemoveOperation(XmlDocument xmlDoc, OivXmlRemoveOperation op, List<XmlEditOperation> trackOps)
        {
            var node = XmlCaseTolerant.SelectSingleNode(xmlDoc, op.XPath, out bool ciMatch);
            if (node == null)
            {
                Log($"  WARNING: XPath not found for remove: {op.XPath}");
                return;
            }
            if (ciMatch) Log($"  Note: XPath matched case-insensitively: {op.XPath}");

            string oldXml = node.OuterXml;

            if (node.ParentNode != null)
            {
                node.ParentNode.RemoveChild(node);
                
                trackOps?.Add(new XmlEditOperation
                {
                    Type = "Remove",
                    XPath = op.XPath,
                    RemovedXml = oldXml
                });
            }
        }

        /// <summary>
        /// Applies an insert operation to text content
        /// </summary>
        private string ApplyInsertOperation(string content, OivInsertOperation insert, List<TextEditOperation> trackOps)
        {
            // Check if the content to insert already exists (avoid duplicates)
            string insertContent = insert.Content.Trim();
            if (content.Contains(insertContent))
            {
                Log($"  Skipping insert (already exists): {insertContent.Substring(0, Math.Min(50, insertContent.Length))}...");
                return content;
            }

            // Build regex pattern from line pattern
            string pattern = insert.LinePattern;
            if (insert.Condition.Equals("Mask", StringComparison.OrdinalIgnoreCase))
            {
                // Convert wildcard pattern to regex
                // * matches any characters
                pattern = Regex.Escape(pattern).Replace("\\*", ".*");
            }
            else
            {
                // Exact match
                pattern = Regex.Escape(pattern);
            }

            // Find matching line and insert
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            bool matched = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], pattern, RegexOptions.IgnoreCase))
                {
                    matched = true;
                    if (insert.Where.Equals("Before", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Insert(i, insert.Content);
                        trackOps?.Add(new TextEditOperation { 
                            Type = "Insert", 
                            AddedContent = insert.Content, 
                            LineNumber = i + 1 
                        });
                        Log($"  Inserted before line {i + 1}: {insertContent.Substring(0, Math.Min(50, insertContent.Length))}...");
                    }
                    else // After
                    {
                        lines.Insert(i + 1, insert.Content);
                        trackOps?.Add(new TextEditOperation { 
                            Type = "Insert", 
                            AddedContent = insert.Content, 
                            LineNumber = i + 2 
                        });
                        Log($"  Inserted after line {i + 1}: {insertContent.Substring(0, Math.Min(50, insertContent.Length))}...");
                    }
                    break; // Only insert once
                }
            }

            if (!matched)
            {
                Log($"  WARNING: No match found for pattern: {insert.LinePattern}");
            }

            return string.Join("\r\n", lines);
        }

        /// <summary>
        /// Finds a directory entry within an RPF by path
        /// </summary>
        private RpfDirectoryEntry FindDirectory(RpfFile rpf, string path)
        {
            path = path.Replace("/", "\\").Trim('\\');
            string[] parts = path.Split('\\');
            
            RpfDirectoryEntry current = rpf.Root;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                
                var next = current.Directories?.FirstOrDefault(d => 
                    d.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                
                if (next == null)
                {
                    return null;
                }
                current = next;
            }
            
            return current;
        }

        /// <summary>
        /// Gets an RPF from mods folder, copying from vanilla if not present
        /// </summary>
        private RpfFile GetOrCopyRpfToMods(string relativePath, bool createIfNotExist = false)
        {
            // Strip leading "mods\" or "mods/" prefix if present to avoid path duplication
            // (since we already target ModsFolder)
            if (relativePath.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("mods/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(5); // Remove "mods\" or "mods/"
            }
            
            string modsPath = Path.Combine(ModsFolder, relativePath);
            string vanillaPath = Path.Combine(GameFolder, relativePath);

            // Check cache first
            if (_openRpfs.TryGetValue(modsPath, out var cachedRpf))
            {
                return cachedRpf;
            }

            // Ensure parent directories exist in mods folder
            string modsDir = Path.GetDirectoryName(modsPath);
            if (!string.IsNullOrEmpty(modsDir) && !Directory.Exists(modsDir))
            {
                Directory.CreateDirectory(modsDir);
                Log($"Created directory: {modsDir}");
            }

            // Copy from vanilla if not in mods
            if (!File.Exists(modsPath))
            {
                if (!_skipBackup) _backupSession.BackupFile(relativePath); // Checks existence on disk. Since it doesn't exist, it tracks as Added.

                if (!File.Exists(vanillaPath))
                {
                    if (createIfNotExist)
                    {
                        Log($"Creating new archive: {modsPath}");
                        try
                        {
                            var rpf = RpfFile.CreateNew(ModsFolder, relativePath, RpfEncryption.OPEN);
                            
                            // Initialize basic structure if needed or just trust CreateNew
                            // CreateNew calls WriteNewArchive, which sets headers.
                            // We should probably ScanStructure to fully initialize the object wrapper state?
                            // But CreateNew returns a new RpfFile object. Let's assume it's ready.
                            
                            _openRpfs[modsPath] = rpf;
                            return rpf;
                        }
                        catch (Exception ex)
                        {
                            Log($"ERROR creating RPF {modsPath}: {ex.Message}");
                            return null;
                        }
                    }

                    Log($"WARNING: Archive not found: {vanillaPath}");
                    return null;
                }

                Log($"Copying {relativePath} to mods folder...");
                File.Copy(vanillaPath, modsPath);
            }

            // Open the RPF
            try
            {
                var rpf = new RpfFile(modsPath, "mods\\" + relativePath);
                rpf.ScanStructure(null, (err) => Log($"RPF Error: {err}"));
                _openRpfs[modsPath] = rpf;
                return rpf;
            }
            catch (Exception ex)
            {
                Log($"ERROR opening RPF {modsPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a nested RPF within a parent RPF, optionally creating it if it doesn't exist
        /// </summary>
        private RpfFile GetNestedRpf(RpfFile parentRpf, string nestedPath, bool createIfNotExist = false)
        {
            nestedPath = nestedPath.TrimStart('\\', '/').Replace('/', '\\');
            string nestedPathLower = nestedPath.ToLowerInvariant();
            
            // Search in existing children
            if (parentRpf.Children != null)
            {
                foreach (var child in parentRpf.Children)
                {
                    string childRelPath = child.Path;
                    if (childRelPath.StartsWith(parentRpf.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        childRelPath = childRelPath.Substring(parentRpf.Path.Length).TrimStart('\\');
                    }
                    
                    if (childRelPath.Equals(nestedPathLower, StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }
                }
            }

            // RPF not found - create if requested
            if (createIfNotExist)
            {
                Log($"Creating nested RPF: {nestedPath} in {parentRpf.Path}");
                try
                {
                    // Parse the path to determine directory structure and RPF name
                    // e.g., "inner\x64a.rpf" -> dir="inner", name="x64a.rpf"
                    string dirPath = Path.GetDirectoryName(nestedPath);
                    string rpfName = Path.GetFileName(nestedPath);
                    
                    // Ensure parent directories exist
                    RpfDirectoryEntry targetDir = parentRpf.Root;
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        targetDir = EnsureDirectory(parentRpf, dirPath);
                        if (targetDir == null)
                        {
                            Log($"ERROR: Could not create directory path: {dirPath}");
                            return null;
                        }
                    }
                    
                    // Create the nested RPF using CodeWalker's built-in method
                    var newRpf = RpfFile.CreateNew(targetDir, rpfName, RpfEncryption.OPEN);
                    
                    // Track as added to parent RPF
                    if (!_skipBackup) _backupSession.TrackRpfAdded(parentRpf.Path, nestedPath);
                    
                    Log($"Successfully created nested RPF: {nestedPath}");
                    return newRpf;
                }
                catch (Exception ex)
                {
                    Log($"ERROR creating nested RPF {nestedPath}: {ex.Message}");
                    return null;
                }
            }

            Log($"WARNING: Nested RPF not found: {nestedPath} in {parentRpf.Path}");
            return null;
        }

        /// <summary>
        /// Ensures the RPF and all parents have OPEN encryption
        /// </summary>
        private void EnsureOpenEncryption(RpfFile rpf)
        {
            if (rpf.Encryption != RpfEncryption.OPEN)
            {
                Log($"Converting {rpf.Name} to OPEN encryption...");
                RpfFile.EnsureValidEncryption(rpf, null, true);
            }
        }

        /// <summary>
        /// Ensures a directory path exists within an RPF, creating if necessary
        /// </summary>
        private RpfDirectoryEntry EnsureDirectory(RpfFile rpf, string path)
        {
            path = path.Replace("/", "\\").Trim('\\');
            string[] parts = path.Split('\\');
            
            RpfDirectoryEntry current = rpf.Root;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                
                // Find existing subdirectory
                var existing = current.Directories?.FirstOrDefault(d => 
                    d.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                
                if (existing != null)
                {
                    current = existing;
                }
                else
                {
                    // Create the directory
                    current = RpfFile.CreateDirectory(current, part);
                    if (current == null)
                    {
                        Log($"ERROR: Failed to create directory: {part}");
                        return null;
                    }
                    Log($"  Created directory: {part}");
                }
            }
            
            return current;
        }

        private void ProcessPsoOperation(OivPsoOperation op, RpfFile rpf, IProgress<InstallProgress> progress, ref int currentOp, int totalOps)
        {
            currentOp++;
            int percent = (int)((currentOp * 100.0) / totalOps);
            progress?.Report(new InstallProgress(percent, $"Processing PSO: {op.FilePath}"));
            Log($"Processing PSO file: {op.FilePath}");

            byte[] fileData = null;
            string filePath = op.FilePath.Replace("/", "\\").TrimStart('\\');
            string fileName = Path.GetFileName(filePath);
            string dirPath = Path.GetDirectoryName(filePath);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            
            RpfDirectoryEntry targetDir = null;
            RpfFileEntry fileEntry = null;

            // Determine if we're working inside an RPF or on disk
            if (rpf != null)
            {
                // Inside an RPF archive - extract the file
                targetDir = rpf.Root;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    targetDir = FindDirectory(rpf, dirPath);
                }

                if (targetDir == null)
                {
                    Log($"  ERROR: Directory not found in RPF: {dirPath}");
                    return;
                }

                fileEntry = targetDir.Files?.FirstOrDefault(f =>
                    f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)) as RpfFileEntry;

                if (fileEntry == null)
                {
                    Log($"  ERROR: PSO file not found in RPF: {op.FilePath}");
                    return;
                }

                try
                {
                    fileData = fileEntry.File.ExtractFile(fileEntry);
                    Log($"  Extracted {fileName} from RPF ({fileData.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: Could not extract file from RPF: {ex.Message}");
                    return;
                }
            }
            else
            {
                // Physical file on disk
                string fullPath = Path.Combine(GameFolder, filePath);
                
                if (!File.Exists(fullPath))
                {
                    Log($"  ERROR: Target PSO file not found: {fullPath}");
                    return;
                }

                string relativeDestPath = fullPath.Substring(GameFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                if (!_skipBackup) _backupSession.BackupFile(relativeDestPath);
                
                try
                {
                    fileData = File.ReadAllBytes(fullPath);
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: Could not read file {fullPath}: {ex.Message}");
                    return;
                }
            }

            if (fileData == null || fileData.Length == 0)
            {
                Log("  ERROR: File data is empty.");
                return;
            }

            // 2. Convert to XML via CodeWalker's own extension routing. The previous
            // per-extension loaders passed a null RpfFileEntry into Load(), which
            // throws immediately (every Load() starts with Name = entry.Name) — so
            // every binary <pso> edit on .ytyp/.ymap/.ymf/.pso failed with
            // "Failed to parse/convert". MetaXml.GetXml(entry, data, ...) is the
            // same proven path the <xml> operation uses for binary files.
            string xmlContent = "";
            string virtualXmlName = "";
            bool isBinary = false;

            try
            {
                if (fileEntry == null)
                {
                    // Disk file: synthesize an entry (strips + honors any RSC7
                    // resource header) the same way CodeWalker's explorer does.
                    fileEntry = CreateDiskFileEntry(fileName, filePath, ref fileData);
                }

                if (ext != ".xml")
                {
                    xmlContent = MetaXml.GetXml(fileEntry, fileData, out virtualXmlName, "");
                    isBinary = !string.IsNullOrEmpty(xmlContent);
                }

                if (!isBinary)
                {
                    // Plain-text XML (.xml/.meta, or an extension GetXml doesn't know).
                    xmlContent = TrimBom(Encoding.UTF8.GetString(fileData));
                    virtualXmlName = fileName;
                }
            }
            catch (Exception ex)
            {
                Log($"  ERROR: Failed to parse/convert PSO file: {ex.Message}");
                return;
            }

            if (string.IsNullOrEmpty(xmlContent))
            {
                Log("  ERROR: XML content is empty after conversion.");
                return;
            }

            // 3. Apply XML Operations
            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(xmlContent);
            }
            catch (XmlException ex)
            {
                Log($"  ERROR: Converted XML is invalid: {ex.Message}");
                return;
            }

            // Track ops
            List<XmlEditOperation> xmlOps = new List<XmlEditOperation>();

            // Apply Adds
            foreach (var add in op.Adds)
            {
                ApplyXmlAddOperation(xmlDoc, add, xmlOps);
            }
            // Apply Replacements
            foreach (var rep in op.Replacements)
            {
                ApplyXmlReplaceOperation(xmlDoc, rep, xmlOps);
            }
            // Apply Removals
            foreach (var rem in op.Removals)
            {
                ApplyXmlRemoveOperation(xmlDoc, rem, xmlOps);
            }

            // 4. Convert XML back to Binary
            byte[] newBytes = null;
            try
            {
                if (!isBinary)
                {
                    newBytes = Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
                }
                else
                {
                    int trimLen = 0;
                    MetaFormat format = XmlMeta.GetXMLFormat(virtualXmlName.ToLowerInvariant(), out trimLen);
                    newBytes = XmlMeta.GetData(xmlDoc, format, virtualXmlName);
                }
            }
            catch (Exception ex)
            {
                Log($"  ERROR: Failed to convert XML back to binary: {ex.Message}");
                return;
            }

            if (newBytes == null)
            {
                Log("  ERROR: Re-conversion returned null data.");
                return;
            }

            // 5. Write File back
            if (rpf != null)
            {
                // Write back to RPF
                try
                {
                    // Backup original before replacing (use raw extraction to preserve RSC7 header)
                    byte[] originalRaw = RpfFileHelper.ExtractFileRaw(fileEntry);
                    _backupSession.BackupRpfFile(rpf.Path, filePath, originalRaw, xmlOps: xmlOps);
                    
                    RpfFile.CreateFile(targetDir, fileName, newBytes, overwrite: true);
                    Log($"  Modified {fileName} in RPF ({newBytes.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: Failed to write PSO back to RPF: {ex.Message}");
                }
            }
            else
            {
                // Write to disk
                string fullPath = Path.Combine(GameFolder, filePath);
                try
                {
                    File.WriteAllBytes(fullPath, newBytes);
                    Log($"  Successfully updated: {fullPath}");
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: Failed to write file {fullPath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Fails fast with a readable message when the destination drive can't hold an
        /// incoming file. Multi-gigabyte packages otherwise die deep inside the copy
        /// with a bare "There is not enough space on the disk".
        /// </summary>
        private void EnsureDiskSpace(string destFullPath, long requiredBytes, string label)
        {
            if (requiredBytes <= 0) return;

            long free;
            string driveName;
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destFullPath)));
                if (!drive.IsReady) return;
                free = drive.AvailableFreeSpace;
                driveName = drive.Name;
            }
            catch { return; }   // probing is best-effort; never block an install over it

            if (free < requiredBytes)
            {
                throw new IOException(
                    $"Not enough free space on {driveName} to install {label}: " +
                    $"needs {FormatSize(requiredBytes)}, only {FormatSize(free)} free.");
            }
        }

        /// <summary>Human-readable byte count for install log lines.</summary>
        private static string FormatSize(long bytes)
        {
            if (bytes < 0) return "unknown size";
            if (bytes >= 1L << 30) return $"{bytes / 1024.0 / 1024.0 / 1024.0:N2} GB";
            if (bytes >= 1L << 20) return $"{bytes / 1024.0 / 1024.0:N1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:N1} KB";
            return $"{bytes} bytes";
        }

        /// <summary>
        /// Builds an RpfFileEntry for a file loaded from disk (outside any RPF), so
        /// the MetaXml/GetFile pipeline can parse it exactly like an in-RPF file.
        /// RSC7 resource headers are stripped and the payload decompressed; anything
        /// else becomes a plain binary entry. Mirrors CodeWalker's explorer.
        /// </summary>
        private static RpfFileEntry CreateDiskFileEntry(string name, string path, ref byte[] data)
        {
            RpfFileEntry e;
            uint rsc7 = (data?.Length > 4) ? BitConverter.ToUInt32(data, 0) : 0;
            if (rsc7 == 0x37435352) //RSC7 header present — resource file
            {
                e = RpfFile.CreateResourceFileEntry(ref data, 0);
                data = ResourceBuilder.Decompress(data);
            }
            else
            {
                var be = new RpfBinaryFileEntry();
                be.FileSize = (uint)(data?.Length ?? 0);
                be.FileUncompressedSize = be.FileSize;
                e = be;
            }
            e.Name = name;
            e.NameLower = name?.ToLowerInvariant();
            e.NameHash = JenkHash.GenHash(e.NameLower);
            e.ShortNameHash = JenkHash.GenHash(Path.GetFileNameWithoutExtension(e.NameLower));
            e.Path = path;
            return e;
        }

        private int CountOperations(List<OivOperation> operations)
        {
            int count = 0;
            foreach (var op in operations)
            {
                if (op is OivArchiveOperation archiveOp)
                {
                    count += CountOperations(archiveOp.Children);
                }
                else
                {
                    count++;
                }
            }
            return count;
        }

        private void Log(string message)
        {
            _logAction?.Invoke(message);
            try { _logWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}"); } catch { }
        }

        /// <summary>
        /// Strips a leading UTF-8 BOM character from a string if present.
        /// Some .meta/.xml files ship with a BOM that XmlDocument.LoadXml() cannot handle.
        /// </summary>
        private static string TrimBom(string s)
        {
            if (s != null && s.Length > 0 && s[0] == '\uFEFF')
                return s.Substring(1);
            return s;
        }

        private void InitializeLog()
        {
            try
            {
                string logDir = Path.Combine(GameFolder, "OIV_CW_Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                
                string safeName = string.Join("_", Package.Metadata.Name.Split(Path.GetInvalidFileNameChars()));
                string logFile = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{safeName}.log");
                
                _logWriter = new StreamWriter(logFile, false, Encoding.UTF8) { AutoFlush = true };
                _logWriter.WriteLine($"Log started for {Package.Metadata.Name} v{Package.Metadata.Version}");
            }
            catch { /* Ignore logging init failures */ }
        }
    }

    /// <summary>
    /// Progress information for installation
    /// </summary>
    public class InstallProgress
    {
        public int Percent { get; }
        public string Status { get; }

        public InstallProgress(int percent, string status)
        {
            Percent = percent;
            Status = status;
        }
    }

    public class StringWriterWithEncoding : StringWriter
    {
        private Encoding encoding;
        public StringWriterWithEncoding(Encoding encoding) { this.encoding = encoding; }
        public override Encoding Encoding => encoding;
    }
    

}
