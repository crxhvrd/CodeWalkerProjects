using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

using System.Xml;
using CodeWalker.GameFiles;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Represents an OIV package and provides loading/parsing functionality
    /// </summary>
    public class OivPackage : IDisposable
    {
        public OivMetadata Metadata { get; private set; } = new OivMetadata();
        public List<OivOperation> Operations { get; private set; } = new List<OivOperation>();
        public string ContentPath { get; private set; } = "";
        public byte[] IconData { get; private set; }
        public bool IsFiveM { get; set; } = false;
        public GameFiles.RpfFile SourceRpf { get; private set; }
        
        private string _tempDirectory = "";
        private bool _disposed;

        /// <summary>
        /// Loads an OIV package from file
        /// </summary>
        public static OivPackage Load(string oivPath)
        {
            var package = new OivPackage();
            if (oivPath.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
            {
                package.LoadFromRpf(oivPath);
            }
            else
            {
                package.LoadInternal(oivPath);
            }
            return package;
        }

        /// <summary>
        /// Loads an OIV package from an already-extracted folder (for testing)
        /// </summary>
        public static OivPackage LoadFromFolder(string folderPath)
        {
            var package = new OivPackage();
            package.ContentPath = Path.Combine(folderPath, "content");
            package.ParseAssemblyXml(Path.Combine(folderPath, "assembly.xml"));
            
            // Load icon if present
            var iconPath = Path.Combine(folderPath, "icon.png");
            if (File.Exists(iconPath))
            {
                package.IconData = File.ReadAllBytes(iconPath);
            }
            
            return package;
        }

        /// <summary>
        /// Creates a synthetic package from a pre-extracted content folder and a
        /// pre-built operation list. Used by the Super OIV (.oivs) installer, which
        /// assembles the operations for the user's selected modules/options and
        /// runs them through the existing OivInstaller engine.
        /// </summary>
        public static OivPackage CreateSynthetic(OivMetadata metadata, string contentPath, List<OivOperation> operations, byte[] iconData = null)
        {
            var package = new OivPackage();
            package.Metadata = metadata ?? new OivMetadata();
            package.ContentPath = contentPath;
            package.Operations = operations ?? new List<OivOperation>();
            package.IconData = iconData;
            return package;
        }

        private void LoadInternal(string oivPath)
        {
            // OIV files are ZIP archives. Unpacking a multi-gigabyte package needs
            // room: PackageTempSpace falls back off a full system drive.
            _tempDirectory = PackageTempSpace.Create(oivPath, "OIVInstaller_");

            ZipFile.ExtractToDirectory(oivPath, _tempDirectory);
            
            ContentPath = Path.Combine(_tempDirectory, "content");
            
            var assemblyPath = Path.Combine(_tempDirectory, "assembly.xml");
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("assembly.xml not found in OIV package");
            }
            
            ParseAssemblyXml(assemblyPath);
            
            // Load icon if present
            var iconPath = Path.Combine(_tempDirectory, "icon.png");
            if (File.Exists(iconPath))
            {
                IconData = File.ReadAllBytes(iconPath);
            }
        }

        private void LoadFromRpf(string rpfPath)
        {
            // Set basic props
            IsFiveM = true; // Assume RPFs are for FiveM for now, or check internal structure
            var rpf = new RpfFile(rpfPath, Path.GetFileName(rpfPath));
            rpf.ScanStructure(null, null);
            SourceRpf = rpf;

            // Find assembly.xml in root
            var assemblyEntry = rpf.Root.Files.Find(e => e.Name.Equals("assembly.xml", StringComparison.OrdinalIgnoreCase));
            if (assemblyEntry == null)
            {
                throw new FileNotFoundException("assembly.xml not found in RPF package");
            }

            // Extract assembly.xml to temp so we can parse it using existing logic
            _tempDirectory = Path.Combine(Path.GetTempPath(), "OIVInstaller_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            
            var assemblyPath = Path.Combine(_tempDirectory, "assembly.xml");
            var assemblyBytes = rpf.ExtractFile(assemblyEntry);
            File.WriteAllBytes(assemblyPath, assemblyBytes);

            ParseAssemblyXml(assemblyPath);

            // Find and load icon.png if present
            var iconEntry = rpf.Root.Files.Find(e => e.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase));
            if (iconEntry != null)
            {
                IconData = rpf.ExtractFile(iconEntry);
            }
        }

        private void ParseAssemblyXml(string xmlPath)
        {
            var doc = new XmlDocument();
            doc.Load(xmlPath);
            
            var root = doc.DocumentElement;
            if (root == null || root.Name != "package")
            {
                throw new InvalidDataException("Invalid assembly.xml: root element must be 'package'");
            }
            
            // Parse metadata
            var metadataNode = root.SelectSingleNode("metadata");
            if (metadataNode != null)
            {
                ParseMetadata(metadataNode);
            }
            
            // Parse colors
            var colorsNode = root.SelectSingleNode("colors");
            if (colorsNode != null)
            {
                ParseColors(colorsNode);
            }
            
            // Parse game version (custom element for Enhanced/Legacy targeting)
            // Use XPath "//gameversion" to find it anywhere (root, inside metadata, etc.)
            var gameVersionNode = root.SelectSingleNode("//gameversion");
            if (gameVersionNode != null)
            {
                string versionText = gameVersionNode.InnerText.Trim().ToLowerInvariant();
                if (versionText == "enhanced" || versionText == "gen9")
                {
                    Metadata.GameVersion = GameVersion.Enhanced;
                }
                else if (versionText == "legacy" || versionText == "gen8")
                {
                    Metadata.GameVersion = GameVersion.Legacy;
                }
                // else defaults to Any
            }
            
            // Parse content operations
            var contentNode = root.SelectSingleNode("content");
            if (contentNode != null)
            {
                ParseContent(contentNode, Operations);
            }
        }

        private void ParseMetadata(XmlNode node)
        {
            Metadata.Name = GetNodeText(node, "name");
            
            // Version and Tag (2.2 spec: tag is inside version)
            var versionNode = node.SelectSingleNode("version");
            if (versionNode != null)
            {
                int.TryParse(GetNodeText(versionNode, "major"), out int major);
                int.TryParse(GetNodeText(versionNode, "minor"), out int minor);
                Metadata.VersionMajor = major;
                Metadata.VersionMinor = minor;
                Metadata.Tag = GetNodeText(versionNode, "tag");
            }

            // Fallback for Tag (older specs or loose parsing)
            if (string.IsNullOrEmpty(Metadata.Tag))
            {
                Metadata.Tag = GetNodeText(node, "tag");
            }

            // Description
            var descNode = node.SelectSingleNode("description");
            if (descNode != null)
            {
                Metadata.Description = descNode.InnerText;
                Metadata.DescriptionFooterLink = descNode.Attributes?["footerLink"]?.Value ?? "";
                Metadata.DescriptionFooterLinkTitle = descNode.Attributes?["footerLinkTitle"]?.Value ?? "";
            }

            // Large Description
            var largeDescNode = node.SelectSingleNode("largeDescription");
            if (largeDescNode != null)
            {
                Metadata.LargeDescription = largeDescNode.InnerText;
                Metadata.LargeDescriptionFooterLink = largeDescNode.Attributes?["footerLink"]?.Value ?? "";
                Metadata.LargeDescriptionFooterLinkTitle = largeDescNode.Attributes?["footerLinkTitle"]?.Value ?? "";
            }

            // License (Note: OIV uses "licence" spelling)
            var licenseNode = node.SelectSingleNode("licence");
            if (licenseNode != null)
            {
                Metadata.License = licenseNode.InnerText;
                Metadata.LicenseFooterLink = licenseNode.Attributes?["footerLink"]?.Value ?? "";
                Metadata.LicenseFooterLinkTitle = licenseNode.Attributes?["footerLinkTitle"]?.Value ?? "";
            }
            
            // Author
            var authorNode = node.SelectSingleNode("author");
            if (authorNode != null)
            {
                Metadata.AuthorDisplayName = GetNodeText(authorNode, "displayName");
                // Spec says actionLink (CamelCase), but check for lowercase too just in case
                Metadata.AuthorActionLink = GetNodeText(authorNode, "actionLink"); 
                if (string.IsNullOrEmpty(Metadata.AuthorActionLink)) Metadata.AuthorActionLink = GetNodeText(authorNode, "actionlink");

                Metadata.AuthorWeb = GetNodeText(authorNode, "web");
                Metadata.AuthorYoutube = GetNodeText(authorNode, "youtube");
                Metadata.AuthorFacebook = GetNodeText(authorNode, "facebook");
                Metadata.AuthorTwitter = GetNodeText(authorNode, "twitter");
            }
        }

        private void ParseColors(XmlNode node)
        {
            var headerBgNode = node.SelectSingleNode("headerBackground");
            if (headerBgNode != null)
            {
                Metadata.HeaderBackground = headerBgNode.InnerText;
                var useBlackAttr = headerBgNode.Attributes?["useBlackTextColor"];
                if (useBlackAttr != null)
                {
                    bool.TryParse(useBlackAttr.Value, out bool useBlack);
                    Metadata.UseBlackTextColor = useBlack;
                }
            }
            
            Metadata.IconBackground = GetNodeText(node, "iconBackground");
        }

        /// <summary>
        /// Parses an OIV-grammar &lt;content&gt; node into a list of operations.
        /// Public+static so the Super OIV (.oivs) parser can reuse the exact same
        /// operation grammar without duplicating it.
        /// </summary>
        public static void ParseContent(XmlNode node, List<OivOperation> operations)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;

                var op = ParseOperation(child);
                if (op != null)
                {
                    operations.Add(op);
                }
            }
        }

        private static OivOperation ParseOperation(XmlNode node)
        {
            switch (node.Name.ToLowerInvariant())
            {
                case "archive":
                    return ParseArchiveOperation(node);
                case "add":
                    return ParseAddOperation(node);
                case "delete":
                    return ParseDeleteOperation(node);
                case "defragmentation":
                    var defragOp = new OivDefragmentationOperation();
                    defragOp.Variable = node.Attributes?["archive"]?.Value ?? "";
                    return defragOp;
                case "text":
                    return ParseTextOperation(node);
                case "xml":
                    return ParseXmlOperation(node);
                case "pso":
                    // PSO parsing is identical to XML but creates OivPsoOperation
                    var psoOp = ParseXmlOperation(node, isPso: true); 
                    return psoOp;
                default:
                    return null; // Unknown operation type
            }
        }

        private static OivArchiveOperation ParseArchiveOperation(XmlNode node)
        {
            var op = new OivArchiveOperation();
            
            op.ArchivePath = node.Attributes?["path"]?.Value ?? "";
            op.Type = node.Attributes?["type"]?.Value ?? "RPF7";
            
            var createAttr = node.Attributes?["createIfNotExist"];
            if (createAttr != null)
            {
                bool.TryParse(createAttr.Value, out bool create);
                op.CreateIfNotExist = create;
            }
            
            // Parse child operations
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                
                var childOp = ParseOperation(child);
                if (childOp != null)
                {
                    op.Children.Add(childOp);
                }
            }
            
            return op;
        }

        private static OivAddOperation ParseAddOperation(XmlNode node)
        {
            var op = new OivAddOperation();
            op.Source = node.Attributes?["source"]?.Value ?? "";
            op.Destination = node.InnerText; // Destination is the element content
            return op;
        }

        private static OivDeleteOperation ParseDeleteOperation(XmlNode node)
        {
            var op = new OivDeleteOperation();
            op.Target = node.InnerText;
            return op;
        }

        private static OivTextOperation ParseTextOperation(XmlNode node)
        {
            var op = new OivTextOperation();
            op.FilePath = node.Attributes?["path"]?.Value ?? "";
            
            var createAttr = node.Attributes?["createIfNotExist"];
            if (createAttr != null)
            {
                bool.TryParse(createAttr.Value, out bool create);
                op.CreateIfNotExist = create;
            }
            
            // Parse child insert/replace operations
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                
                if (child.Name.Equals("insert", StringComparison.OrdinalIgnoreCase))
                {
                    var insertOp = ParseInsertOperation(child);
                    if (insertOp != null)
                    {
                        op.Inserts.Add(insertOp);
                    }
                }
                else if (child.Name.Equals("replace", StringComparison.OrdinalIgnoreCase))
                {
                    var replaceOp = ParseReplaceOperation(child);
                    if (replaceOp != null)
                    {
                        op.Replacements.Add(replaceOp);
                    }
                }
                else if (child.Name.Equals("add", StringComparison.OrdinalIgnoreCase))
                {
                    var addOp = new OivTextAddOperation { Content = child.InnerText };
                    op.Adds.Add(addOp);
                }
                else if (child.Name.Equals("delete", StringComparison.OrdinalIgnoreCase))
                {
                    var delOp = new OivTextDeleteOperation 
                    { 
                        Condition = child.Attributes?["condition"]?.Value ?? "Equal",
                        Content = child.InnerText
                    };
                    op.Deletions.Add(delOp);
                }
            }
            
            return op;
        }

        private static OivInsertOperation ParseInsertOperation(XmlNode node)
        {
            var op = new OivInsertOperation();
            op.Where = node.Attributes?["where"]?.Value ?? "Before";
            op.LinePattern = node.Attributes?["line"]?.Value ?? "";
            op.Condition = node.Attributes?["condition"]?.Value ?? "Mask";
            op.Content = node.InnerText; // This is HTML-decoded automatically by XmlDocument
            return op;
        }

        private static OivReplaceOperation ParseReplaceOperation(XmlNode node)
        {
            var op = new OivReplaceOperation();
            op.LinePattern = node.Attributes?["line"]?.Value ?? "";
            op.Condition = node.Attributes?["condition"]?.Value ?? "Equal";
            op.Content = node.InnerText; 
            return op;
        }

        private static OivXmlOperation ParseXmlOperation(XmlNode node, bool isPso = false)
        {
            OivXmlOperation op = isPso ? new OivPsoOperation() : new OivXmlOperation();
            op.FilePath = node.Attributes?["path"]?.Value ?? "";
            
            // Parse child add operations
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                
                string name = child.Name.ToLowerInvariant();
                if (name == "add")
                {
                    var addOp = new OivXmlAddOperation();
                    addOp.XPath = child.Attributes?["xpath"]?.Value ?? "";
                    addOp.Append = child.Attributes?["append"]?.Value ?? "Last";
                    addOp.Content = child.InnerXml; 
                    op.Adds.Add(addOp);
                }
                else if (name == "replace")
                {
                    var repOp = new OivXmlReplaceOperation();
                    repOp.XPath = child.Attributes?["xpath"]?.Value ?? "";
                    repOp.Content = child.InnerXml;
                    op.Replacements.Add(repOp);
                }
                else if (name == "remove")
                {
                    var remOp = new OivXmlRemoveOperation();
                    remOp.XPath = child.Attributes?["xpath"]?.Value ?? "";
                    op.Removals.Add(remOp);
                }
            }
            
            return op;
        }

        private string GetNodeText(XmlNode parent, string childName)
        {
            var child = parent.SelectSingleNode(childName);
            return child?.InnerText?.Trim() ?? "";
        }

        /// <summary>
        /// Gets the full path to a content file within the OIV package
        /// </summary>
        public string GetContentFilePath(string relativePath)
        {
            // Normalize path separators
            relativePath = relativePath.Replace("/", "\\");
            return Path.Combine(ContentPath, relativePath);
        }

        /// <summary>
        /// Reads the content of a file from the OIV package
        /// </summary>
        public byte[] ReadContentFile(string relativePath)
        {
            if (SourceRpf != null)
            {
                // RPF mode: find entry in RPF
                // Normalize path
                string searchPath = relativePath.Replace("/", "\\");
                // The content path in RPF should be relative to root or 'content' folder? 
                // In normal OIV, 'content' folder contains the files.
                // If RPF *is* the package, does it have a 'content' folder inside? 
                // The user said "read assembly.xml inside .rpf", implying the RPF root is the package root.
                // But OIV structure usually has 'content' folder. 
                // Let's assume the RPF structure mirrors the Zip structure.
                
                // Try finding the entry. 
                // RpfFile structure traversal is needed.
                var entry = FindRpfEntry(SourceRpf.Root, searchPath);
                if (entry == null)
                {
                     // Try prepending "content\" if not found, just in case
                     entry = FindRpfEntry(SourceRpf.Root, Path.Combine("content", searchPath));
                }

                if (entry == null)
                {
                    throw new FileNotFoundException($"Content file not found in RPF: {relativePath}");
                }
                
                return SourceRpf.ExtractFile(entry);
            }

            var fullPath = GetContentFilePath(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Content file not found: {relativePath}", fullPath);
            }
            return File.ReadAllBytes(fullPath);
        }

        /// <summary>
        /// Size of a content file in bytes, or -1 when it can't be determined.
        /// Lets callers pick a streaming path before trying to allocate a byte[].
        /// </summary>
        public long GetContentFileSize(string relativePath)
        {
            try
            {
                if (SourceRpf != null)
                {
                    var entry = FindContentEntry(relativePath);
                    return entry?.GetFileSize() ?? -1;
                }
                var fi = new FileInfo(GetContentFilePath(relativePath));
                return fi.Exists ? fi.Length : -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Copies a content file to <paramref name="destPath"/> WITHOUT loading it into
        /// memory. byte[] (and File.ReadAllBytes) top out just under 2 GB, so packages
        /// shipping a prebuilt multi-gigabyte dlc.rpf or texture archive can only be
        /// installed by streaming. File.Copy also lets Windows do the copy itself,
        /// which is far faster than a read-all/write-all round trip for big payloads.
        /// </summary>
        public void CopyContentFileTo(string relativePath, string destPath)
        {
            if (SourceRpf != null)
            {
                // RPF-sourced package: entries are bounded by the RPF format, so the
                // in-memory extract is safe here.
                var entry = FindContentEntry(relativePath)
                    ?? throw new FileNotFoundException($"Content file not found in RPF: {relativePath}");
                File.WriteAllBytes(destPath, SourceRpf.ExtractFile(entry));
                return;
            }

            var fullPath = GetContentFilePath(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Content file not found: {relativePath}", fullPath);
            }
            File.Copy(fullPath, destPath, true);
        }

        /// <summary>Locates a content entry inside an RPF-sourced package.</summary>
        private RpfFileEntry FindContentEntry(string relativePath)
        {
            string searchPath = relativePath.Replace("/", "\\");
            return FindRpfEntry(SourceRpf.Root, searchPath)
                ?? FindRpfEntry(SourceRpf.Root, Path.Combine("content", searchPath));
        }

        private RpfFileEntry FindRpfEntry(RpfDirectoryEntry dir, string path)
        {
            string[] parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            RpfDirectoryEntry currentDir = dir;
            
            for (int i = 0; i < parts.Length; i++)
            {
                bool isLast = i == parts.Length - 1;
                string part = parts[i];
                
                if (isLast)
                {
                    return currentDir.Files.Find(f => f.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    currentDir = currentDir.Directories.Find(d => d.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (currentDir == null) return null;
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Clean up temp directory if we created one
            if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
