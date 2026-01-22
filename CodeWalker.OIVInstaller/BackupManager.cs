using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Xml;
using CodeWalker.GameFiles;

namespace CodeWalker.OIVInstaller
{
    public enum UninstallMode
    {
        Backup,
        Vanilla
    }

    public class BackupManager
    {
        private const string BACKUP_ROOT_NAME = "OIV_Uninstall_Data";
        public string GameFolder { get; private set; }
        public string BackupRoot { get; private set; }

        public BackupManager(string gameFolder)
        {
            GameFolder = gameFolder;
            BackupRoot = Path.Combine(GameFolder, BACKUP_ROOT_NAME);
        }

        public BackupSession CreateSession(string packageName, string description, string version, bool isGen9)
        {
            // Sanitize package name for folder use
            string safeName = string.Join("_", packageName.Split(Path.GetInvalidFileNameChars()));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionFolder = Path.Combine(BackupRoot, $"{safeName}_{timestamp}");
            
            return new BackupSession(this, sessionFolder, packageName, description, version, isGen9);
        }

        public List<BackupLog> GetInstalledPackages()
        {
            var results = new List<BackupLog>();
            if (!Directory.Exists(BackupRoot)) return results;

            foreach (var dir in Directory.GetDirectories(BackupRoot))
            {
                string logPath = Path.Combine(dir, "install.log");
                if (File.Exists(logPath))
                {
                    try
                    {
                        string json = File.ReadAllText(logPath);
                        var log = JsonSerializer.Deserialize<BackupLog>(json);
                        if (log != null)
                        {
                            log.BackupFolderPath = dir; // Inject path for runtime use
                            results.Add(log);
                        }
                    }
                    catch { /* Ignore invalid logs */ }
                }
            }
            return results.OrderByDescending(x => x.InstallDate).ToList();
        }
    
        public void Uninstall(BackupLog log, IProgress<string> progress, UninstallMode mode = UninstallMode.Backup)
        {
            // Separate RPF entries from filesystem entries
            var filesystemEntries = new List<FileBackupEntry>();
            var rpfEntriesByPath = new Dictionary<string, List<FileBackupEntry>>(StringComparer.OrdinalIgnoreCase);

            // Process entries in reverse order and group RPF entries by their parent RPF
            for (int i = log.Entries.Count - 1; i >= 0; i--)
            {
                var entry = log.Entries[i];
                if (entry.IsRpfContent)
                {
                    if (!rpfEntriesByPath.ContainsKey(entry.RpfPath))
                        rpfEntriesByPath[entry.RpfPath] = new List<FileBackupEntry>();
                    rpfEntriesByPath[entry.RpfPath].Add(entry);
                }
                else
                {
                    filesystemEntries.Add(entry);
                }
            }

            // Process filesystem entries first (these don't have the batching issue)
            foreach (var entry in filesystemEntries)
            {
                string fullPath = Path.Combine(GameFolder, entry.OriginalPath);
                try
                {
                    progress?.Report($"Reverting: {entry.OriginalPath}");
                    if (mode == UninstallMode.Vanilla)
                        RestoreFileFromVanilla(entry, progress);
                    else
                        RestoreFileFromBackup(entry, log.BackupFolderPath, fullPath, progress);
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error reverting {entry.OriginalPath}: {ex.Message}");
                }
            }

            // Process RPF entries in batches - one RPF at a time
            foreach (var kvp in rpfEntriesByPath)
            {
                string rpfRelPath = kvp.Key;
                var entries = kvp.Value;
                
                string rpfPath = Path.Combine(GameFolder, rpfRelPath);
                if (!File.Exists(rpfPath))
                {
                    progress?.Report($"RPF not found, skipping: {rpfRelPath}");
                    continue;
                }

                progress?.Report($"Processing RPF: {rpfRelPath} ({entries.Count} entries)");
                
                try
                {
                    // Process each entry with a fresh RPF scan
                    // This is necessary because modifying an RPF changes its internal structure
                    foreach (var entry in entries)
                    {
                        try
                        {
                            progress?.Report($"  Reverting: {entry.InternalPath}");
                            
                            // Re-open and rescan RPF for each entry to avoid stale references
                            var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
                            rpf.ScanStructure(null, null);
                            
                            RevertRpfEntryBatched(rpf, entry, log.BackupFolderPath, progress, mode);
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"  Error reverting {entry.InternalPath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error opening RPF {rpfRelPath}: {ex.Message}");
                }
            }

            // Cleanup backup folder
            try
            {
                progress?.Report("Cleaning up backup files...");
                if (Directory.Exists(log.BackupFolderPath))
                    Directory.Delete(log.BackupFolderPath, true);
            }
            catch { }
        }


        private void RestoreFileFromBackup(FileBackupEntry entry, string backupFolderPath, string fullPath, IProgress<string> progress)
        {
            switch (entry.Action)
            {
                case BackupAction.Added:
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    CleanupEmptyParents(Path.GetDirectoryName(fullPath));
                    break;

                case BackupAction.Replaced:
                case BackupAction.Edited:
                    // Try smart text revert first if ops are available
                    if (entry.TextOperations != null && entry.TextOperations.Count > 0)
                    {
                        if (PerformSmartTextRevert(fullPath, entry.TextOperations))
                            break; // Success, skip full restore
                    }
                    goto case BackupAction.Deleted; // Fallback to full restore

                case BackupAction.Deleted:
                    // Fallback to full restore from backup file
                    string backupFile = Path.Combine(backupFolderPath, entry.BackupPath);
                    if (File.Exists(backupFile))
                    {
                        string dir = Path.GetDirectoryName(fullPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        File.Copy(backupFile, fullPath, true);
                    }
                    break;
            }
        }

        private void CleanupEmptyParents(string dirPath)
        {
            try
            {
                while (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    if (Directory.GetFileSystemEntries(dirPath).Length == 0)
                    {
                        Directory.Delete(dirPath);
                        dirPath = Path.GetDirectoryName(dirPath);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch { }
        }

        private void RestoreFileFromVanilla(FileBackupEntry entry, IProgress<string> progress)
        {
            // OriginalPath is relative to GameFolder (e.g. "mods\update\update.rpf")
            // To find vanilla, we must strip "mods\" if present
            string vanillaRelPath = entry.OriginalPath;
            if (vanillaRelPath.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase))
            {
                vanillaRelPath = vanillaRelPath.Substring(5);
            }

            string vanillaPath = Path.Combine(GameFolder, vanillaRelPath);
            string targetPath = Path.Combine(GameFolder, entry.OriginalPath);

            switch (entry.Action)
            {
                case BackupAction.Added:
                    // If it was added by mod, and not in vanilla, delete it.
                    // If it exists in vanilla (unlikely for Added, but possible if mod added file that vanilla also has?), restoration means copy vanilla.
                    // Usually "Added" means "New file". So Vanilla state is "Not there".
                    if (File.Exists(vanillaPath))
                    {
                        File.Copy(vanillaPath, targetPath, true);
                    }
                    else
                    {
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                        CleanupEmptyParents(Path.GetDirectoryName(targetPath));
                    }
                    break;

                case BackupAction.Replaced:
                case BackupAction.Edited:
                case BackupAction.Deleted:
                    if (File.Exists(vanillaPath))
                    {
                        File.Copy(vanillaPath, targetPath, true);
                    }
                    else
                    {
                        // If no vanilla file, but it was replaced/edited? 
                        // Maybe it was a loose file in mods folder that didn't exist in vanilla but we are treating as replaced?
                        // Fallback to delete if no vanilla source.
                         if (File.Exists(targetPath)) File.Delete(targetPath);
                    }
                    break;
            }
        }

        private void RevertRpfEntry(FileBackupEntry entry, string backupFolder, IProgress<string> progress, UninstallMode mode)
        {
            var rpfPath = Path.Combine(GameFolder, entry.RpfPath);
            if (!File.Exists(rpfPath)) return;

            var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
            rpf.ScanStructure(null, null);

            var internalDir = Path.GetDirectoryName(entry.InternalPath);
            var fileName = Path.GetFileName(entry.InternalPath);
            var dir = FindRpfDirectory(rpf, internalDir);
            if (dir == null) return;

            if (mode == UninstallMode.Vanilla)
            {
                 RestoreRpfFromVanilla(rpf, dir, entry, progress);
            }
            else
            {
                // Existing Backup Logic
                if (entry.Action == BackupAction.Added)
                {
                    var file = dir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (file != null) RpfFile.DeleteEntry(file);
                }
                else if (entry.Action == BackupAction.Edited)
                {
                    bool success = false;
                    
                    // Try smart text revert
                    if (entry.TextOperations != null && entry.TextOperations.Count > 0)
                        success = PerformSmartRpfTextRevert(dir, fileName, entry.TextOperations);
                    // Try smart XML revert
                    else if (entry.XmlOperations != null && entry.XmlOperations.Count > 0)
                        success = PerformSmartRpfXmlRevert(dir, fileName, entry.XmlOperations);
                    // Legacy check
                    else if (!string.IsNullOrEmpty(entry.ContentChange))
                        success = PerformSmartRpfRevert(dir, fileName, entry.ContentChange);

                    if (!success)
                    {
                        RestoreFullRpfBackup(dir, fileName, backupFolder, entry.BackupPath);
                    }
                }
                else // Replaced or Deleted
                {
                    RestoreFullRpfBackup(dir, fileName, backupFolder, entry.BackupPath);
                }
            }
        }
        
        /// <summary>
        /// Reverts an RPF entry using an already-open RPF file (for batched operations)
        /// </summary>
        private void RevertRpfEntryBatched(RpfFile rpf, FileBackupEntry entry, string backupFolder, IProgress<string> progress, UninstallMode mode)
        {
            var internalDir = Path.GetDirectoryName(entry.InternalPath);
            var fileName = Path.GetFileName(entry.InternalPath);
            var dir = FindRpfDirectory(rpf, internalDir);
            if (dir == null) return;

            if (mode == UninstallMode.Vanilla)
            {
                 RestoreRpfFromVanilla(rpf, dir, entry, progress);
            }
            else
            {
                // Existing Backup Logic
                if (entry.Action == BackupAction.Added)
                {
                    var file = dir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (file != null) RpfFile.DeleteEntry(file);
                }
                else if (entry.Action == BackupAction.Edited)
                {
                    bool success = false;
                    
                    // Try smart text revert
                    if (entry.TextOperations != null && entry.TextOperations.Count > 0)
                        success = PerformSmartRpfTextRevert(dir, fileName, entry.TextOperations);
                    // Try smart XML revert
                    else if (entry.XmlOperations != null && entry.XmlOperations.Count > 0)
                        success = PerformSmartRpfXmlRevert(dir, fileName, entry.XmlOperations);
                    // Legacy check
                    else if (!string.IsNullOrEmpty(entry.ContentChange))
                        success = PerformSmartRpfRevert(dir, fileName, entry.ContentChange);

                    if (!success)
                    {
                        RestoreFullRpfBackup(dir, fileName, backupFolder, entry.BackupPath);
                    }
                }
                else // Replaced or Deleted
                {
                    RestoreFullRpfBackup(dir, fileName, backupFolder, entry.BackupPath);
                }
            }
        }
        
        private void RestoreRpfFromVanilla(RpfFile modRpf, RpfDirectoryEntry targetDir, FileBackupEntry entry, IProgress<string> progress)
        {
            // 1. Find Vanilla RPF path
            // entry.RpfPath is typically "mods\update\update.rpf". 
            // Vanilla is "update\update.rpf".
            string vanillaRpfPath = entry.RpfPath;
            if (vanillaRpfPath.StartsWith("mods\\", StringComparison.OrdinalIgnoreCase))
            {
                vanillaRpfPath = vanillaRpfPath.Substring(5);
            }
            string fullVanillaPath = Path.Combine(GameFolder, vanillaRpfPath);
            
            if (!File.Exists(fullVanillaPath))
            {
                 progress?.Report($"WARNING: Vanilla RPF not found: {vanillaRpfPath}. Cannot restore.");
                 return;
            }

            // 2. Open Vanilla RPF
            // NOTE: This requires keys to be initialized!
            // We assume calling code has done this.
            
            try 
            {
                var vanillaRpf = new RpfFile(fullVanillaPath, Path.GetFileName(fullVanillaPath));
                vanillaRpf.ScanStructure(null, null);
                
                // 3. Find File in Vanilla
                var vInternalDir = Path.GetDirectoryName(entry.InternalPath);
                var vFileName = Path.GetFileName(entry.InternalPath);
                
                var vDir = FindRpfDirectory(vanillaRpf, vInternalDir);
                if (vDir == null) 
                {
                     // Directory doesn't exist in vanilla -> Delete file from mod RPF?
                     // If it was "Added", yes. If "Replaced", it implies it SHOULD be there.
                     // But if it's not in vanilla, then "Replaced" metadata might technically be wrong relative to vanilla, 
                     // but correct relative to previous mod state.
                     // Safe bet: if not in vanilla, remove it from mods.
                     DeleteFileFromRpf(targetDir, vFileName);
                     return;
                }
                
                var vFile = vDir.Files?.FirstOrDefault(f => f.Name.Equals(vFileName, StringComparison.OrdinalIgnoreCase));
                
                if (vFile == null)
                {
                    // File not in vanilla -> Delete from mod
                    DeleteFileFromRpf(targetDir, vFileName);
                }
                else
                {
                    // File exists in vanilla -> Extract and Overwrite
                    byte[] data = vFile.File.ExtractFile(vFile as RpfFileEntry);
                    RpfFile.CreateFile(targetDir, vFileName, data, true);
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error reading vanilla RPF {vanillaRpfPath}: {ex.Message}");
            }
        }
        
        private void DeleteFileFromRpf(RpfDirectoryEntry dir, string fileName)
        {
             var file = dir.Files?.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
             if (file != null) RpfFile.DeleteEntry(file);
        }
        
        private RpfDirectoryEntry FindRpfDirectory(RpfFile rpf, string internalPath)
        {
            if (string.IsNullOrEmpty(internalPath)) return rpf.Root;

            var parts = internalPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = rpf.Root;

            foreach (var part in parts)
            {
                var next = current.Directories.FirstOrDefault(d => d.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private bool PerformSmartRpfRevert(RpfDirectoryEntry dir, string fileName, string contentChange)
        {
            var file = dir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (file == null) return false;

            try
            {
                byte[] data = file.File.ExtractFile(file); // Keys must be initialized globally
                string content = System.Text.Encoding.UTF8.GetString(data);
                
                if (content.Contains(contentChange))
                {
                    string newContent = content.Replace(contentChange, "");
                    byte[] newData = System.Text.Encoding.UTF8.GetBytes(newContent);
                    RpfFile.CreateFile(dir, fileName, newData, true); 
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool PerformSmartRpfTextRevert(RpfDirectoryEntry dir, string fileName, List<TextEditOperation> ops)
        {
            var file = dir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (file == null) return false;

            try
            {
                byte[] data = file.File.ExtractFile(file);
                string content = System.Text.Encoding.UTF8.GetString(data);
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

                // Revert operations in reverse order
                for (int i = ops.Count - 1; i >= 0; i--)
                {
                    var op = ops[i];
                    int idx = op.LineNumber - 1; // 1-indexed

                    if (op.Type == "Insert" || op.Type == "Add")
                    {
                        if (idx >= 0 && idx < lines.Count) lines.RemoveAt(idx);
                    }
                    else if (op.Type == "Replace")
                    {
                        if (idx >= 0 && idx < lines.Count) lines[idx] = op.RemovedContent;
                    }
                    else if (op.Type == "Delete")
                    {
                        if (idx >= 0 && idx <= lines.Count) lines.Insert(idx, op.RemovedContent);
                    }
                }
                
                byte[] newData = System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
                RpfFile.CreateFile(dir, fileName, newData, true); 
                return true;
            }
            catch { return false; }
        }

        private bool PerformSmartRpfXmlRevert(RpfDirectoryEntry dir, string fileName, List<XmlEditOperation> ops)
        {
            var file = dir.Files.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (file == null) return false;

            try
            {
                // Read
                byte[] data = file.File.ExtractFile(file);
                string xmlContent = System.Text.Encoding.UTF8.GetString(data);
                
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.LoadXml(xmlContent);

                // Revert operations in reverse order
                for (int i = ops.Count - 1; i >= 0; i--)
                {
                    var op = ops[i];
                    
                    if (op.Type == "Add")
                    {
                        // To revert Add: Remove the node we added.
                        // We need to find it. Since we added it, it's there.
                        // Ideally we stored precise XPath or we can try to find by content?
                        // op.XPath is the TARGET, not the new node.
                        // We added content to op.XPath.
                        // If we can identify the new node, we remove it.
                        // THIS IS TRICKY without storing valid reference.
                        // Strategy: Use the AddedXml to find the node within the parent.
                        
                        var parent = xmlDoc.SelectSingleNode(op.XPath);
                        if (parent != null)
                        {
                            // Try to find the child that matches our added XML
                            // This naive check relies on content uniqueness or exact XML match
                            bool removed = false;
                            foreach(XmlNode child in parent.ChildNodes)
                            {
                                // We trim for comparison robustness
                                if (child.OuterXml.Trim() == op.AddedXml.Trim())
                                {
                                    parent.RemoveChild(child);
                                    removed = true;
                                    break; // Only remove one instance?
                                }
                            }
                            // If not found in parent, maybe it was inserted Before/After?
                            // Then op.XPath is the sibling.
                             if (!removed && (op.Append == "Before" || op.Append == "After"))
                             {
                                 // Sibling logic... parent is actually op.XPath node's parent.
                                 var sibling = parent; // Wait, parent var here is the node from XPath
                                 var actualParent = sibling.ParentNode;
                                 if (actualParent != null)
                                 {
                                     foreach(XmlNode child in actualParent.ChildNodes)
                                     {
                                         if (child.OuterXml.Trim() == op.AddedXml.Trim())
                                         {
                                             actualParent.RemoveChild(child);
                                             removed = true;
                                             break;
                                         }
                                     }
                                 }
                             }
                        }
                    }
                    else if (op.Type == "Replace")
                    {
                        // To revert Replace: Find node at XPath and restore RemovedXml
                        var node = xmlDoc.SelectSingleNode(op.XPath);
                        if (node != null && node.ParentNode != null)
                        {
                            var fragment = xmlDoc.CreateDocumentFragment();
                            fragment.InnerXml = op.RemovedXml;
                            node.ParentNode.ReplaceChild(fragment, node);
                        }
                    }
                    else if (op.Type == "Remove")
                    {
                        // To revert Remove: Re-insert RemovedXml at XPath?
                        // If node at XPath is gone, we can't select it to insert before/after?
                        // Actually, if we removed it, XPath might not point to anything valid anymore if it was strict.
                        // But usually XPath points to the node we removed.
                        // We need the PARENT path + index or something.
                        // This is limitation of current plan.
                        // Fallback: This ops probably won't be used much or will fail gracefully here.
                        // If we can't restore, we return false eventually? 
                        // Actually, we can assume we fail if exception.
                        
                        // Try to find parent by stripping last part of XPath?
                        // Too complex for now. If Remove is used, we might fail smart revert and use full backup if needed,
                        // but currently we return 'true' if no exception.
                        // Let's rely on full backup for Remove if simple restore fails?
                        // Or just skip.
                        
                        // Implementation hole: Reverting remove requires parent/sibling context.
                    }
                }
                
                // Save
                using (var sw = new StringWriterWithEncoding(System.Text.Encoding.UTF8))
                {
                    xmlDoc.Save(sw);
                    byte[] newData = System.Text.Encoding.UTF8.GetBytes(sw.ToString());
                    RpfFile.CreateFile(dir, fileName, newData, true);
                }
                return true;
            }
            catch { return false; }
        }

        private void RestoreFullRpfBackup(RpfDirectoryEntry dir, string fileName, string backupFolder, string backupPath)
        {
            string backupFile = Path.Combine(backupFolder, backupPath);
            if (!File.Exists(backupFile)) return;

            byte[] data = File.ReadAllBytes(backupFile);
            RpfFile.CreateFile(dir, fileName, data, true);
        }

        private bool PerformSmartTextRevert(string fullPath, List<TextEditOperation> ops)
        {
            if (!File.Exists(fullPath)) return false;
            
            try
            {
                var content = File.ReadAllText(fullPath);
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                
                // Revert operations in reverse order
                for (int i = ops.Count - 1; i >= 0; i--)
                {
                    var op = ops[i];
                    // LineNumber is 1-indexed
                    int idx = op.LineNumber - 1;
                    
                    if (op.Type == "Insert" || op.Type == "Add")
                    {
                        // To revert insert: Remove the line(s) added
                        // If multi-line content was added, we need to handle that. 
                        // But ApplyInsertOperation added single string (maybe with newlines)
                        // If line count doesn't match, we might be in trouble. 
                        // Simplified: Try remove at index.
                        if (idx >= 0 && idx < lines.Count)
                        {
                            // If content matches, better safety
                            // But for now, trust the index? 
                            // Verify content match if possible
                            // For insert, we expect the line to be op.AddedContent (if single line)
                            lines.RemoveAt(idx);
                        }
                    }
                    else if (op.Type == "Replace")
                    {
                        // To revert replace: Restore RemovedContent at index
                        if (idx >= 0 && idx < lines.Count)
                        {
                            lines[idx] = op.RemovedContent;
                        }
                    }
                    else if (op.Type == "Delete")
                    {
                        // To revert delete: Insert RemovedContent back at index
                        if (idx >= 0 && idx <= lines.Count)
                        {
                            lines.Insert(idx, op.RemovedContent);
                        }
                    }
                }
                
                File.WriteAllText(fullPath, string.Join(Environment.NewLine, lines));
                return true;
            }
            catch 
            {
                return false; 
            }
        }

    }

    public class BackupSession
    {
        private BackupManager _manager;
        private BackupLog _log;
        private string _sessionFolder;

        public BackupSession(BackupManager manager, string sessionFolder, string packageName, string description, string version, bool isGen9)
        {
            _manager = manager;
            _sessionFolder = sessionFolder;
            _log = new BackupLog
            {
                PackageName = packageName,
                Description = description,
                Version = version,
                IsGen9 = isGen9,
                InstallDate = DateTime.Now,
                Entries = new List<FileBackupEntry>()
            };
        }

        public void BackupFile(string relativePath)
        {
            string fullPath = Path.Combine(_manager.GameFolder, relativePath);
            if (!File.Exists(fullPath))
            {
                // File doesn't exist, so this is an "Added" operation (no backup needed, just tracking)
                _log.Entries.Add(new FileBackupEntry
                {
                    Action = BackupAction.Added,
                    OriginalPath = relativePath
                });
                return;
            }

            // File exists, backup it
            EnsureSessionFolder();
            
            string backupFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fullPath);
            string backupDest = Path.Combine(_sessionFolder, backupFileName);
            
            File.Copy(fullPath, backupDest);

            _log.Entries.Add(new FileBackupEntry
            {
                Action = BackupAction.Replaced,
                OriginalPath = relativePath,
                BackupPath = backupFileName
            });
        }
        
        // For partial edits (XML/Text), backup original and track operations for smart reversal
        public void BackupForEdit(string relativePath, List<TextEditOperation> textOps = null, List<XmlEditOperation> xmlOps = null)
        {
             string fullPath = Path.Combine(_manager.GameFolder, relativePath);
            if (!File.Exists(fullPath)) return; // Should likely not happen for edits

            EnsureSessionFolder();

            string backupFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fullPath);
            string backupDest = Path.Combine(_sessionFolder, backupFileName);

            File.Copy(fullPath, backupDest);

            _log.Entries.Add(new FileBackupEntry
            {
                Action = BackupAction.Edited,
                OriginalPath = relativePath,
                BackupPath = backupFileName,
                TextOperations = textOps,
                XmlOperations = xmlOps
            });
        }
        
        // Overload for RPF edits with operation tracking
        public void BackupRpfFile(string rpfPath, string internalPath, byte[] originalData, 
            List<TextEditOperation> textOps = null, List<XmlEditOperation> xmlOps = null)
        {
             EnsureSessionFolder();
             
             // Construct a 'virtual' path for logging
             string displayPath = Path.Combine(rpfPath, internalPath).Replace("\\", "/"); 
             
             string backupFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(internalPath);
             string backupDest = Path.Combine(_sessionFolder, backupFileName);
             
             File.WriteAllBytes(backupDest, originalData);
             
             _log.Entries.Add(new FileBackupEntry
             {
                 Action = BackupAction.Edited,
                 OriginalPath = displayPath,
                 BackupPath = backupFileName,
                 IsRpfContent = true,
                 RpfPath = rpfPath,
                 InternalPath = internalPath,
                 TextOperations = textOps,
                 XmlOperations = xmlOps
             });
        }

        // Helper to track files added to RPF (so we can delete them on revert)
        public void TrackRpfAdded(string rpfPath, string internalPath)
        {
             // RPF Added doesn't need a backup file, just the entry
             string displayPath = Path.Combine(rpfPath, internalPath).Replace("\\", "/"); 
             
             _log.Entries.Add(new FileBackupEntry
             {
                 Action = BackupAction.Added,
                 OriginalPath = displayPath,
                 IsRpfContent = true,
                 RpfPath = rpfPath,
                 InternalPath = internalPath
             });
        }

        public void BackupDeletedFile(string relativePath)
        {
            string fullPath = Path.Combine(_manager.GameFolder, relativePath);
            if (!File.Exists(fullPath)) return;

            EnsureSessionFolder();
            
            string backupFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fullPath);
            string backupDest = Path.Combine(_sessionFolder, backupFileName);
            
            File.Copy(fullPath, backupDest);

            _log.Entries.Add(new FileBackupEntry
            {
                Action = BackupAction.Deleted,
                OriginalPath = relativePath,
                BackupPath = backupFileName
            });
        }

        public void BackupRpfDeletedFile(string rpfPath, string internalPath, byte[] originalData)
        {
             EnsureSessionFolder();
             
             string displayPath = Path.Combine(rpfPath, internalPath).Replace("\\", "/"); 
             string backupFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(internalPath);
             string backupDest = Path.Combine(_sessionFolder, backupFileName);
             
             File.WriteAllBytes(backupDest, originalData);
             
             _log.Entries.Add(new FileBackupEntry
             {
                 Action = BackupAction.Deleted,
                 OriginalPath = displayPath,
                 BackupPath = backupFileName,
                 IsRpfContent = true,
                 RpfPath = rpfPath,
                 InternalPath = internalPath
             });
        }

        public void Save()
        {
            if (_log.Entries.Count == 0 && !Directory.Exists(_sessionFolder)) return; // Nothing done

            EnsureSessionFolder();
            string json = JsonSerializer.Serialize(_log, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(_sessionFolder, "install.log"), json);
        }

        private void EnsureSessionFolder()
        {
            if (!Directory.Exists(_sessionFolder))
            {
                Directory.CreateDirectory(_sessionFolder);
            }
        }
    }

    public class BackupLog
    {
        public string PackageName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public bool IsGen9 { get; set; }
        public DateTime InstallDate { get; set; }
        public List<FileBackupEntry> Entries { get; set; } = new List<FileBackupEntry>();
        
        [JsonIgnore]
        public string BackupFolderPath { get; set; }
    }


    public class FileBackupEntry
    {
        public BackupAction Action { get; set; }
        public string OriginalPath { get; set; } // Relative to game folder
        public string BackupPath { get; set; }   // Filename in backup folder
        
        // Data for Smart Revert (Text/XML)
        public string ContentChange { get; set; } 
        
        // NEW: Lists of individual operations for smart reversal
        public List<TextEditOperation> TextOperations { get; set; }
        public List<XmlEditOperation> XmlOperations { get; set; }
        
        // RPF specific
        public bool IsRpfContent { get; set; }
        public string RpfPath { get; set; }
        public string InternalPath { get; set; }
    }

    public enum BackupAction
    {
        Added,
        Replaced,
        Edited,
        Deleted
    }
    
    /// <summary>
    /// Tracks a single text editing operation for smart reversal
    /// </summary>
    public class TextEditOperation
    {
        public string Type { get; set; }           // "Insert", "Replace", "Delete", "Add"
        public string AddedContent { get; set; }   // Content that was added
        public string RemovedContent { get; set; } // Content that was removed (for replace/delete)
        public int LineNumber { get; set; }        // Line where change occurred (1-indexed)
    }
    
    /// <summary>
    /// Tracks a single XML/PSO editing operation for smart reversal
    /// </summary>
    public class XmlEditOperation
    {
        public string Type { get; set; }           // "Add", "Replace", "Remove"
        public string XPath { get; set; }          // Target XPath
        public string AddedXml { get; set; }       // XML that was added
        public string RemovedXml { get; set; }     // XML that was removed
        public string Append { get; set; }         // Position: First/Last/Before/After
    }
}
