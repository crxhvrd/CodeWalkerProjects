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
        private readonly Dictionary<string, RpfFile> _openRpfs = new Dictionary<string, RpfFile>(StringComparer.OrdinalIgnoreCase);
        private bool _keysInitialized = false;
        private BackupManager _backupManager;
        private BackupSession _backupSession;

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

        public void Install(IProgress<InstallProgress> progress = null, List<BackupLog> packagesToUninstall = null, UninstallMode uninstallMode = UninstallMode.Backup)
        {
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
                _backupSession.Save();
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
                if (existingFile != null)
                {
                    // It's a replacement - backup original
                    byte[] oldData = existingFile.File.ExtractFile(existingFile as RpfFileEntry);
                    _backupSession.BackupRpfFile(rpf.Path, destPath, oldData); // destPath is internal RPF path
                }
                else
                {
                    // It's a new file - log as added
                    // BackupManager handles "added" logic if we pass a path that doesn't exist on disk?
                    // But for RPF, we need to log it specially as "RPF Added".
                    // Current BackupManager.BackupRpfFile assumes we have data.
                    // Let's overload or extend BackupSession for "RpfAdded".
                    // Use a trick: pass null data to signify "Added"? Or update BackupManager.
                    // For now, let's just log it if we can, or skip RPF additions tracking (harder to revert if we don't know it's new).
                    
                    // Actually, let's just assume we only backup what we replace. 
                    // To support uninstalling added files in RPF, we need to track them.
                    // Let's add 'BackupRpfAdded' method to session.
                    _backupSession.TrackRpfAdded(rpf.Path, destPath);
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
                    // Backup before delete!
                    byte[] originalData = file.File.ExtractFile(file as RpfFileEntry);
                    _backupSession.BackupRpfDeletedFile(rpf.Path, targetPath, originalData);
                    
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
                        _backupSession.BackupDeletedFile(relPath);

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
                
                // Determine if this should go to mods folder
                // RPF files, or files going to update/ or x64/ typically belong in mods
                bool useMods = destPath.StartsWith("update", StringComparison.OrdinalIgnoreCase) ||
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

                // Read source file from OIV package
                byte[] fileData = Package.ReadContentFile(op.Source);

                // --- BACKUP LOGIC ---
                // destPath is relative to GameFolder?? No, wait.
                // op.Destination is relative to game folder for top level adds
                // fullDestPath is absolute path.
                // We need relative path for backup manager
                
                string relativeDestPath = fullDestPath.Substring(GameFolder.Length).TrimStart(Path.DirectorySeparatorChar);
                _backupSession.BackupFile(relativeDestPath);
                // --------------------

                // Write to game folder
                File.WriteAllBytes(fullDestPath, fileData);
                Log($"  Copied to game: {destPath} ({fileData.Length} bytes)");
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
                    content = Encoding.UTF8.GetString(data);
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
                if (fileEntry != null)
                {
                     byte[] originalBytes = fileEntry.File.ExtractFile(fileEntry);
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
                string xmlContent = Encoding.UTF8.GetString(data);
                
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
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
                using (var sw = new StringWriterWithEncoding(Encoding.UTF8))
                {
                    xmlDoc.Save(sw);
                    byte[] newData = Encoding.UTF8.GetBytes(sw.ToString());
                    
                    // --- BACKUP LOGIC (XML) ---
                    if (fileEntry != null)
                    {
                        byte[] originalBytes = fileEntry.File.ExtractFile(fileEntry);
                        _backupSession.BackupRpfFile(rpf.Path, filePath, originalBytes, xmlOps: xmlOps);
                    }
                    // --------------------------

                    RpfFile.CreateFile(targetDir, fileName, newData, overwrite: true);
                    Log($"  Modified {fileName} ({newData.Length} bytes)");
                }
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
            // Check if content already exists (avoid duplicates)
            string contentTrimmed = addOp.Content.Trim();
            if (xmlDoc.InnerXml.Contains(contentTrimmed))
            {
                Log($"  Skipping XPath add (already exists): {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
                return;
            }

            // Find target node using XPath
            var targetNode = xmlDoc.SelectSingleNode(addOp.XPath);
            if (targetNode == null)
            {
                Log($"  WARNING: XPath not found: {addOp.XPath}");
                return;
            }

            // Create new node from content
            var fragment = xmlDoc.CreateDocumentFragment();
            fragment.InnerXml = "\r\n\t\t" + addOp.Content + "\r\n\t";
            
            string appendMode = addOp.Append ?? "Last";
            
            // Append based on position
            if (appendMode.Equals("First", StringComparison.OrdinalIgnoreCase))
            {
                // Add as first child of selected node
                targetNode.PrependChild(fragment);
                Log($"  Added at start of {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else if (appendMode.Equals("Before", StringComparison.OrdinalIgnoreCase))
            {
                // Add as sibling before selected node
                if (targetNode.ParentNode == null)
                {
                    Log($"  WARNING: Cannot insert before root node: {addOp.XPath}");
                    return;
                }
                targetNode.ParentNode.InsertBefore(fragment, targetNode);
                Log($"  Added before {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else if (appendMode.Equals("After", StringComparison.OrdinalIgnoreCase))
            {
                // Add as sibling after selected node
                if (targetNode.ParentNode == null)
                {
                    Log($"  WARNING: Cannot insert after root node: {addOp.XPath}");
                    return;
                }
                targetNode.ParentNode.InsertAfter(fragment, targetNode);
                Log($"  Added after {addOp.XPath}: {contentTrimmed.Substring(0, Math.Min(50, contentTrimmed.Length))}...");
            }
            else // Last or default
            {
                // Add as last child of selected node
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


        private void ApplyXmlReplaceOperation(XmlDocument xmlDoc, OivXmlReplaceOperation op, List<XmlEditOperation> trackOps)
        {
            var node = xmlDoc.SelectSingleNode(op.XPath);
            if (node == null)
            {
                Log($"  WARNING: XPath not found for replace: {op.XPath}");
                return;
            }

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
            var node = xmlDoc.SelectSingleNode(op.XPath);
            if (node == null)
            {
                Log($"  WARNING: XPath not found for remove: {op.XPath}");
                return;
            }

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
                // Track this as a new file in mods folder so uninstall can remove it
                _backupSession.BackupFile(relativePath); // Checks existence on disk. Since it doesn't exist, it tracks as Added.

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
                    _backupSession.TrackRpfAdded(parentRpf.Path, nestedPath);
                    
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
                _backupSession.BackupFile(relativeDestPath);
                
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

            // 2. Load File into CodeWalker Object and Convert to XML
            string xmlContent = "";
            string virtualXmlName = "";

            try 
            {
                if (ext == ".ymt")
                {
                    var f = new YmtFile(); f.Load(fileData, fileEntry);
                    xmlContent = MetaXml.GetXml(f, out virtualXmlName);
                }
                else if (ext == ".ymf")
                {
                    var f = new YmfFile(); f.Load(fileData, null);
                    xmlContent = MetaXml.GetXml(f, out virtualXmlName);
                }
                else if (ext == ".ymap")
                {
                    var f = new YmapFile(); f.Load(fileData, null);
                    xmlContent = MetaXml.GetXml(f, out virtualXmlName);
                }
                else if (ext == ".ytyp")
                {
                    var f = new YtypFile(); f.Load(fileData, null);
                    xmlContent = MetaXml.GetXml(f, out virtualXmlName);
                }
                else if (ext == ".pso")
                {
                    var f = new JPsoFile(); f.Load(fileData, null);
                    xmlContent = MetaXml.GetXml(f, out virtualXmlName);
                }
                else if (ext == ".xml")
                {
                    xmlContent = Encoding.UTF8.GetString(fileData);
                    virtualXmlName = fileName;
                }
                else
                {
                    Log($"  ERROR: Unsupported PSO file extension: {ext}");
                    return;
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
                if (ext == ".xml")
                {
                    newBytes = Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
                }
                else
                {
                    int trimLen = 0;
                    MetaFormat format = XmlMeta.GetXMLFormat(virtualXmlName, out trimLen);
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
                    // Backup original before replacing
                    _backupSession.BackupRpfFile(rpf.Path, filePath, fileData, xmlOps: xmlOps);
                    
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
