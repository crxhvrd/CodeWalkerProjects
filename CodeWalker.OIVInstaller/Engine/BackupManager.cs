using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Xml;
using System.IO.Compression;
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
        private const string BACKUP_ROOT_NAME = "OIV_CW_Uninstall_Data";


        public string GameFolder { get; private set; }
        public string BackupRoot { get; private set; }
        private static string _debugLogPath;

        public BackupManager(string gameFolder)
        {
            GameFolder = gameFolder;
            BackupRoot = Path.Combine(GameFolder, BACKUP_ROOT_NAME);
            
            try 
            {
                string logDir = Path.Combine(GameFolder, "OIV_CW_Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                _debugLogPath = Path.Combine(logDir, "uninstall.log");
            }
            catch 
            { 
                 _debugLogPath = Path.Combine(GameFolder, "uninstall_debug.log"); // Fallback
            }
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
                    catch (Exception ex)
                    {
                        FileLog($"Error loading log {logPath}: {ex}");
                    }
                }
            }
            FileLog($"Found {results.Count} installed packages in {BackupRoot}");
            return results.OrderByDescending(x => x.InstallDate).ToList();
        }
        public static void FileLog(string message)
        {
            try
            {
                if (_debugLogPath != null)
                    File.AppendAllText(_debugLogPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }


    
        public void Uninstall(BackupLog log, IProgress<string> progress, UninstallMode mode = UninstallMode.Backup)
        {
            bool hasErrors = false;

            // Process in reverse order to undo changes correctly
            for (int i = log.Entries.Count - 1; i >= 0; i--)
            {
                var entry = log.Entries[i];
                string fullPath = Path.Combine(GameFolder, entry.OriginalPath);

                try
                {
                    progress?.Report($"Reverting: {entry.OriginalPath}");
                    
                    if (entry.IsRpfContent)
                    {
                        RevertRpfEntry(entry, log.BackupFolderPath, progress, mode);
                    }
                    else
                    {
                        if (mode == UninstallMode.Vanilla)
                        {
                            RestoreFileFromVanilla(entry, progress);
                        }
                        else
                        {
                            RestoreFileFromBackup(entry, log.BackupFolderPath, fullPath, progress);
                        }
                    }
                }
                catch (Exception ex)
                {
                    hasErrors = true;
                    progress?.Report($"ERROR: Failed to revert {entry.OriginalPath}: {ex.Message}");
                    FileLog($"Uninstall error for {entry.OriginalPath}: {ex}");
                }
            }

            // Cleanup backup folder ONLY if no errors occurred
            if (!hasErrors)
            {
                try
                {
                    progress?.Report("Cleaning up backup files...");
                    if (Directory.Exists(log.BackupFolderPath))
                        Directory.Delete(log.BackupFolderPath, true);
                }
                catch (Exception ex)
                {
                    // Non-critical: cleanup failed but uninstall succeeded
                    progress?.Report($"Warning: Could not delete backup folder: {ex.Message}");
                }
            }
            else
            {
                progress?.Report("WARNING: Uninstall completed with errors.");
                progress?.Report("Backup files were NOT deleted to allow manual recovery.");
                progress?.Report($"Backup location: {log.BackupFolderPath}");
            }
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
            var rpf = OpenRpfForRevert(entry.RpfPath);
            if (rpf == null)
            {
                FileLog($"[Revert] Could not open {entry.RpfPath} - {entry.InternalPath} left as installed.");
                return;
            }

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

                    // Smart revert re-applies the inverse edit to the CURRENT file, so
                    // other mods' changes to the same file survive. That only works on
                    // text: a binary meta would have to be decompiled, edited and
                    // recompiled, and a recompile is never byte-identical to what
                    // Rockstar shipped. We hold the original bytes, so for binary
                    // formats restore those verbatim and get the vanilla file back
                    // exactly.
                    if (IsBinaryGameFile(backupFolder, entry.BackupPath))
                    {
                        FileLog($"[Revert] {fileName} is a binary game file - restoring original bytes instead of rebuilding.");
                    }
                    // Try smart text revert
                    else if (entry.TextOperations != null && entry.TextOperations.Count > 0)
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
                else // Replaced
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
            // 2. Open Vanilla RPF - may be nested, so resolve it the same way as the
            // mods-side archive rather than assuming it is a file on disk.
            // NOTE: This requires keys to be initialized!
            // We assume calling code has done this.

            try
            {
                var vanillaRpf = OpenRpfForRevert(vanillaRpfPath);
                if (vanillaRpf == null)
                {
                    progress?.Report($"WARNING: Vanilla RPF not found: {vanillaRpfPath}. Cannot restore.");
                    return;
                }

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
                    // File exists in vanilla -> Extract and Overwrite (with RSC7 header preserved)
                    byte[] data = RpfFileHelper.ExtractFileRaw(vFile as RpfFileEntry);
                    if (data != null && data.Length >= 4)
                    {
                        uint magic = BitConverter.ToUInt32(data, 0);
                        FileLog($"[VanillaRestore] Extracted {vFileName}: {data.Length} bytes. RSC7 Magic: {(magic==0x37435352?"YES":"NO")}");
                    }
                    else
                    {
                        FileLog($"[VanillaRestore] Extracted {vFileName}: {data?.Length ?? 0} bytes. (Empty/Too small)");
                    }
                    RpfFile.CreateFile(targetDir, vFileName, data, true);
                }
            }
            catch (Exception ex)
            {
                FileLog($"[VanillaRestore] Error: {ex}");
                progress?.Report($"Error reading vanilla RPF {vanillaRpfPath}: {ex.Message}");
            }
        }
        
        private void DeleteFileFromRpf(RpfDirectoryEntry dir, string fileName)
        {
             var file = dir.Files?.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
             if (file != null) RpfFile.DeleteEntry(file);
        }
        
        /// <summary>
        /// Opens the archive a backup entry refers to, which may be NESTED.
        ///
        /// RpfPath is whatever RpfFile.Path was at install time, so for an operation
        /// inside a nested archive it reads like
        ///     mods\update\update.rpf\dlc_patch\...\koreatown_metadata.rpf
        /// which is not a path on disk. Treating it as one made File.Exists fail and
        /// the revert return silently, leaving every nested edit installed forever.
        /// Split at the first .rpf segment, open that physical archive, then walk the
        /// child archives for whatever .rpf segments remain.
        /// </summary>
        private RpfFile OpenRpfForRevert(string rpfRelPath)
        {
            if (string.IsNullOrEmpty(rpfRelPath)) return null;

            var parts = rpfRelPath.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int i = Array.FindIndex(parts, p => p.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase));
            if (i < 0) return null;

            string rootRel = string.Join("\\", parts.Take(i + 1));
            string physical = Path.Combine(GameFolder, rootRel);
            if (!File.Exists(physical)) return null;

            RpfFile root;
            try
            {
                root = new RpfFile(physical, rootRel);
                root.ScanStructure(null, null);
            }
            catch (Exception ex)
            {
                FileLog($"[Revert] Failed to scan {rootRel}: {ex.Message}");
                return null;
            }

            if (i == parts.Length - 1) return root;

            string nestedTail = string.Join("\\", parts.Skip(i + 1));
            var nested = FindChildArchive(root, nestedTail);
            if (nested == null)
                FileLog($"[Revert] Nested archive not found: {nestedTail} in {rootRel}");
            return nested;
        }

        private static RpfFile FindChildArchive(RpfFile root, string tail)
        {
            tail = "\\" + tail.TrimStart('\\');
            var stack = new Stack<RpfFile>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var f = stack.Pop();
                foreach (var c in f.Children ?? new List<RpfFile>())
                {
                    if (c.Path != null && c.Path.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) return c;
                    stack.Push(c);
                }
            }
            return null;
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
                byte[] data = RpfFileHelper.ExtractFileRaw(file as RpfFileEntry); // Preserve RSC7 header
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
                byte[] data = RpfFileHelper.ExtractFileRaw(file as RpfFileEntry);
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
                byte[] data = RpfFileHelper.ExtractFileRaw(file as RpfFileEntry);
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
                        
                        var parent = XmlCaseTolerant.SelectSingleNode(xmlDoc, op.XPath, out _);
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
                                    RemoveNodeAndItsPadding(parent, child);
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
                                             RemoveNodeAndItsPadding(actualParent, child);
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
                        var node = XmlCaseTolerant.SelectSingleNode(xmlDoc, op.XPath, out _);
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
                
                // Save. Keep the file's own XML declaration so a reverted file matches
                // vanilla byte-for-byte instead of differing on encoding="utf-8".
                using (var sw = new StringWriterWithEncoding(System.Text.Encoding.UTF8))
                {
                    xmlDoc.Save(sw);
                    byte[] newData = System.Text.Encoding.UTF8.GetBytes(
                        XmlTextUtil.PreserveDeclaration(xmlContent, sw.ToString()));
                    RpfFile.CreateFile(dir, fileName, newData, true);
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Does the stored backup start with one of RAGE's binary container magics?
        /// RSC7 = resource (.ytyp/.ymap/…), PSIN = PSO meta, RBF0 = RBF meta.
        /// Anything else (plain XML, .meta, .txt) is treated as text.
        /// </summary>
        private bool IsBinaryGameFile(string backupFolder, string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath)) return false;
            try
            {
                string backupFile = Path.Combine(backupFolder, backupPath);
                if (!File.Exists(backupFile)) return false;

                var head = new byte[4];
                using (var fs = File.OpenRead(backupFile))
                {
                    if (fs.Read(head, 0, 4) < 4) return false;
                }
                uint magic = BitConverter.ToUInt32(head, 0);
                return magic == 0x37435352   // "RSC7"
                    || magic == 0x4E495350   // "PSIN"
                    || magic == 0x30464252;  // "RBF0"
            }
            catch { return false; }
        }

        private void RestoreFullRpfBackup(RpfDirectoryEntry dir, string fileName, string backupFolder, string backupPath)
        {
            string backupFile = Path.Combine(backupFolder, backupPath);
            if (!File.Exists(backupFile))
            {
                FileLog($"[FullRestore] Warning: Backup file not found: {backupFile}");
                return;
            }

            byte[] data = File.ReadAllBytes(backupFile);
            
            if (data.Length >= 4)
            {
                uint magic = BitConverter.ToUInt32(data, 0);
                FileLog($"[FullRestore] Restoring {fileName} from backup. Size: {data.Length}. RSC7 Magic: {(magic==0x37435352?"YES":"NO")}");
            }
            else
            {
                FileLog($"[FullRestore] Restoring {fileName} from backup. Size: {data.Length} (Too small for header)");
            }

            RpfFile.CreateFile(dir, fileName, data, true);
        }

        /// <summary>
        /// Removes a node that an install added, along with the indentation the
        /// installer inserted with it.
        ///
        /// ApplyXmlAddOperation wraps new content in "\r\n\t\t" … "\r\n\t" so the file
        /// stays readable. Removing only the element leaves those text nodes behind,
        /// so an install/uninstall cycle grew the file by 7 bytes every time and never
        /// returned it to its vanilla bytes.
        /// </summary>
        private static void RemoveNodeAndItsPadding(XmlNode parent, XmlNode child)
        {
            var before = child.PreviousSibling;
            var after = child.NextSibling;

            parent.RemoveChild(child);

            // Only ever drop whitespace-only text — never real content.
            if (IsWhitespaceText(before) && IsWhitespaceText(after))
            {
                // The add contributed one leading and one trailing run; dropping the
                // leading one restores the original spacing between siblings.
                parent.RemoveChild(before);
            }
            else if (IsWhitespaceText(before) && after == null)
            {
                parent.RemoveChild(before);
            }
        }

        private static bool IsWhitespaceText(XmlNode n) =>
            n != null
            && (n.NodeType == XmlNodeType.Whitespace
                || n.NodeType == XmlNodeType.SignificantWhitespace
                || (n.NodeType == XmlNodeType.Text && string.IsNullOrWhiteSpace(n.Value)));

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

        public void TrackFileAdded(string relativePath)
        {
             _log.Entries.Add(new FileBackupEntry
             {
                 Action = BackupAction.Added,
                 OriginalPath = relativePath
             });
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
    
    /// <summary>
    /// Helper class for extracting files from RPF with proper header preservation
    /// </summary>
    public static class RpfFileHelper
    {
        /// <summary>
        /// Extracts raw file bytes from an RPF entry, preserving the RSC7 header for resource files.
        /// This is essential for backup/restore to maintain the file type.
        /// </summary>
        public static byte[] ExtractFileRaw(RpfFileEntry entry)
        {
            if (entry == null) return null;
            
            try
            {
                string physicalPath = entry.File.GetPhysicalFilePath();
                using (var br = new BinaryReader(File.OpenRead(physicalPath)))
                {
                    var result = ExtractFileRaw(entry, br, entry.File);
                    
                    // Debug: verify RSC7 header is present in result
                    if (result != null && result.Length >= 4)
                    {
                        uint magic = BitConverter.ToUInt32(result, 0);
                        System.Diagnostics.Debug.WriteLine($"[RpfFileHelper] Extracted {entry.Name}: {result.Length} bytes, RSC7={magic == 0x37435352}");
                    }
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RpfFileHelper] FALLBACK for {entry?.Name}: {ex.Message}");
                // Fallback to regular ExtractFile if raw extraction fails
                return entry.File.ExtractFile(entry);
            }
        }
        
        private static byte[] ExtractFileRaw(RpfFileEntry entry, BinaryReader br, RpfFile rpf)
        {
            br.BaseStream.Position = rpf.StartPos + ((long)entry.FileOffset * 512);

            bool isResourceExt = IsResourceExtension(entry.Name);
            
            // Try extracting as Resource if it IS a resource entry, OR looks like one
            if (entry is RpfResourceFileEntry || isResourceExt)
            {
                // Resource files: RSC7 header (16 bytes) + encrypted/compressed payload
                // We need to preserve the header but decrypt the payload
                long fileSize = entry is RpfBinaryFileEntry ? ((RpfBinaryFileEntry)entry).GetFileSize() : entry.FileSize; // Handle binary entry polymorphism
                
                if (fileSize <= 16) 
                {
                     // Too small to be a resource, fallback if it was speculatively a resource
                     if (isResourceExt && !(entry is RpfResourceFileEntry)) goto TryBinary;
                     return null;
                }
                
                // Read the 16-byte resource header. RSC7 is what a file exported to disk
                // starts with, but an entry sitting inside an archive is NOT required to
                // carry that magic - Rockstar's own entries frequently don't, and the
                // engine never looks at it: RpfFile.ExtractFileResource skips these 16
                // bytes unconditionally and inflates whatever follows. So the magic is a
                // hint, not a validity test. What CreateFile needs on the way back in is
                // an RSC7 header carrying the entry's real flags, which we can always
                // rebuild from the entry itself.
                byte[] header = br.ReadBytes(16);

                uint magic = BitConverter.ToUInt32(header, 0);
                if (magic != 0x37435352 && entry is RpfResourceFileEntry hres) // 'RSC7'
                {
                    header = new byte[16];
                    BitConverter.GetBytes((uint)0x37435352).CopyTo(header, 0);
                    BitConverter.GetBytes(hres.Version).CopyTo(header, 4);
                    BitConverter.GetBytes(hres.SystemFlags).CopyTo(header, 8);
                    BitConverter.GetBytes(hres.GraphicsFlags).CopyTo(header, 12);
                    magic = 0x37435352;
                }

                if (magic != 0x37435352)
                {
                    BackupManager.FileLog($"[RpfFileHelper] Warning: {entry.Name} (Type: {entry.GetType().Name}) has non-standard magic 0x{magic:X}.");
                    // Fallback to Binary Extraction
                    br.BaseStream.Position -= 16; // Rewind
                    byte[] binaryResult = ExtractBinary(entry, br, rpf);
                    
                    if (binaryResult != null && binaryResult.Length >= 4)
                    {
                        uint binMagic = BitConverter.ToUInt32(binaryResult, 0);
                        if (binMagic == 0x37435352)
                        {
                            BackupManager.FileLog($"[RpfFileHelper] Recovered RSC7 header from binary payload for {entry.Name}.");
                            return binaryResult;
                        }
                    }
                    
                    // A real resource entry never reaches here - its header is rebuilt from
                    // the entry's own flags above. Only a binary entry that merely LOOKS
                    // like a resource by extension gets this far, and for that the binary
                    // bytes are the correct backup. Stamping a synthetic RSC7 header over
                    // the first 16 bytes of a decompressed payload, as this used to do,
                    // destroyed those bytes and made the restore differ from vanilla.

                    return binaryResult; // Return best effort (headerless)
                }
                else
                {
                    BackupManager.FileLog($"[RpfFileHelper] Verified RSC7 header for {entry.Name} (Type: {entry.GetType().Name})");
                }
                
                // Read the payload (after header)
                int payloadLen = (int)fileSize - 16;
                if (payloadLen < 0) return header;

                byte[] payload = br.ReadBytes(payloadLen);
                
                // Decrypt the payload if needed
                if (entry.IsEncrypted)
                {
                    if (rpf.IsAESEncrypted)
                        payload = GTACrypto.DecryptAES(payload);
                    else
                        payload = GTACrypto.DecryptNG(payload, entry.Name, (uint)fileSize);
                }
                
                // Combine header + decrypted payload (still compressed, which is what CreateFile expects)
                byte[] result = new byte[16 + payload.Length];
                Buffer.BlockCopy(header, 0, result, 0, 16);
                Buffer.BlockCopy(payload, 0, result, 16, payload.Length);
                
                return result;
            }

            TryBinary:
            return ExtractBinary(entry, br, rpf);
        }
        
        private static byte[] ExtractBinary(RpfFileEntry entry, BinaryReader br, RpfFile rpf)
        {
            if (entry is RpfBinaryFileEntry binEntry)
            {
                // Binary files: may be encrypted/compressed
                long fileSize = binEntry.GetFileSize();
                if (fileSize <= 0) return null;
                
                byte[] rawBytes = br.ReadBytes((int)fileSize);
                byte[] decr = rawBytes;
                
                if (binEntry.IsEncrypted)
                {
                    if (rpf.IsAESEncrypted)
                        decr = GTACrypto.DecryptAES(rawBytes);
                    else
                        decr = GTACrypto.DecryptNG(rawBytes, binEntry.Name, binEntry.FileUncompressedSize);
                }
                
                // If compressed (FileSize > 0), decompress
                if (binEntry.FileSize > 0)
                {
                    decr = DecompressBytes(decr);
                }
                
                return decr;
            }
            // If we are here with RpfResourceFileEntry, we need to treat it as Binary manually
            else if (entry is RpfResourceFileEntry resEntry)
            {
                 // Resource entry fallback logic (if we rewound)
                 // Treat as binary: Read all, decrypt, decompress?
                 // But Resource Entries might handle encryption differently (Payload loop).
                 // However, assuming standard Binary extraction works if we ignore header logic:
                 
                 // fileSize includes header? Yes.
                 long fileSize = resEntry.FileSize;
                 byte[] rawBytes = br.ReadBytes((int)fileSize);
                 byte[] decr = rawBytes;
                 
                  if (resEntry.IsEncrypted)
                {
                    if (rpf.IsAESEncrypted)
                        decr = GTACrypto.DecryptAES(rawBytes);
                    else
                        decr = GTACrypto.DecryptNG(rawBytes, resEntry.Name, (uint)fileSize);
                }
                // Resources are usually compressed? 
                // Wait, ExtractFileRaw logic for Resource didn't decompress. It returned Decrypted Payload.
                // Binary logic Decompresses.
                
                // If we want "Raw for Backup", we probably want Decompressed?
                // CreateFile re-compresses.
                
                // We'll try DecompressBytes just in case.
                decr = DecompressBytes(decr);
                return decr;
            }
            
            return entry.File.ExtractFile(entry);
        }
        
        private static bool IsResourceExtension(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext == ".ytd" || ext == ".ydr" || ext == ".yft" || ext == ".ydd" || 
                   ext == ".ybn" || ext == ".ycd" || ext == ".ypt" || ext == ".ytyp" || 
                   ext == ".ymap" || ext == ".yldb"; 
        }

        private static byte[] DecompressBytes(byte[] bytes)
        {
            try
            {
                using (var ds = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress))
                {
                    using (var outstr = new MemoryStream())
                    {
                        ds.CopyTo(outstr);
                        return outstr.ToArray();
                    }
                }
            }
            catch
            {
                return bytes; // Return original if decompression fails
            }
        }
    }
}
