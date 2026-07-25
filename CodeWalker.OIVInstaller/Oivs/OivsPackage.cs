using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace CodeWalker.OIVInstaller
{
    // ============================================================================
    // Super OIV (.oivs) format - Model B reader.
    //
    // A .oivs is a ZIP with a single super.xml manifest that carries ALL the
    // install instructions inline (the same <content> operation grammar an .oiv
    // uses), grouped under each <module>/<option>. Source content lives in a
    // shared content/ tree namespaced per module (content/<id>/...).
    //
    // This class parses the manifest and, for a given user selection, builds the
    // combined OivOperation list that the existing OivInstaller engine runs.
    // See OIVS_FORMAT.md for the full spec.
    // ============================================================================

    public class OivsMedia
    {
        public string Title = "";
        public string Before = "";   // <compare before=>
        public string After = "";    // <compare after=>
        public string Image = "";    // <image src=> (single showcase image)
        public string Caption = "";
        public bool IsCompare => !string.IsNullOrEmpty(Before) || !string.IsNullOrEmpty(After);
    }

    /// <summary>An item that can be installed: a module or a group option.</summary>
    public abstract class OivsInstallable
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public List<OivsMedia> Media = new List<OivsMedia>();
        // Raw install spec, resolved lazily into operations:
        public List<XmlNode> ContentNodes = new List<XmlNode>();  // <content> ops blocks
        public List<string> FolderSources = new List<string>();   // <folder source=".."/>
    }

    public class OivsModule : OivsInstallable
    {
        public bool Required = false;   // always installed, not shown as a toggle
        public bool Default = false;    // optional modules: checked by default?
    }

    public class OivsOption : OivsInstallable { }

    public class OivsGroup
    {
        public string Id = "";
        public string Type = "single";     // "single" = radio (multi reserved)
        public string Title = "";
        public bool AllowNone = true;
        public string Default = "none";    // "none" or an option id
        public string Description = "";
        public List<OivsOption> Options = new List<OivsOption>();

        public OivsOption FindOption(string id)
        {
            return Options.Find(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>A user's choices: which optional modules are on, and each group's pick.</summary>
    public class OivsSelection
    {
        public HashSet<string> EnabledModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> GroupChoices =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // groupId -> optionId or "none"
    }

    public class OivsPackage : IDisposable
    {
        public OivMetadata Metadata { get; private set; } = new OivMetadata();
        public List<OivsModule> Modules { get; private set; } = new List<OivsModule>();
        public List<OivsGroup> Groups { get; private set; } = new List<OivsGroup>();
        public string ContentPath { get; private set; } = "";
        // Extraction root (contains super.xml, content/, icon.png and any bundled
        // media). Relative media paths in the manifest resolve against this.
        public string RootPath { get; private set; } = "";
        public byte[] IconData { get; private set; }

        private XmlDocument _doc;          // kept alive so cached XmlNodes stay valid
        private string _tempDirectory = "";
        private bool _disposed;

        public static OivsPackage Load(string oivsPath)
        {
            var pkg = new OivsPackage();
            pkg.LoadInternal(oivsPath);
            return pkg;
        }

        private void LoadInternal(string oivsPath)
        {
            // .oivs packages are routinely multi-gigabyte; PackageTempSpace falls back
            // off the system drive when %TEMP% hasn't the room to unpack them.
            _tempDirectory = PackageTempSpace.Create(oivsPath, "OivsInstaller_");

            ZipFile.ExtractToDirectory(oivsPath, _tempDirectory);

            RootPath = _tempDirectory;
            ContentPath = Path.Combine(_tempDirectory, "content");

            var manifestPath = Path.Combine(_tempDirectory, "super.xml");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("super.xml not found in .oivs package");

            ParseManifest(manifestPath);

            var iconPath = Path.Combine(_tempDirectory, "icon.png");
            if (File.Exists(iconPath))
                IconData = File.ReadAllBytes(iconPath);
        }

        private void ParseManifest(string xmlPath)
        {
            _doc = new XmlDocument();
            _doc.Load(xmlPath);

            var root = _doc.DocumentElement;
            if (root == null || root.Name != "superpackage")
                throw new InvalidDataException("Invalid super.xml: root element must be 'superpackage'");

            var metaNode = root.SelectSingleNode("metadata");
            if (metaNode != null) ParseMetadata(metaNode);

            var colorsNode = root.SelectSingleNode("colors");
            if (colorsNode != null) ParseColors(colorsNode);

            var gvNode = root.SelectSingleNode("//gameversion");
            if (gvNode != null)
            {
                string v = gvNode.InnerText.Trim().ToLowerInvariant();
                if (v == "enhanced" || v == "gen9") Metadata.GameVersion = GameVersion.Enhanced;
                else if (v == "legacy" || v == "gen8") Metadata.GameVersion = GameVersion.Legacy;
            }

            var modulesNode = root.SelectSingleNode("modules");
            if (modulesNode != null)
            {
                foreach (XmlNode m in modulesNode.ChildNodes)
                {
                    if (m.NodeType != XmlNodeType.Element || m.Name != "module") continue;
                    var mod = new OivsModule
                    {
                        Id = Attr(m, "id"),
                        Name = Attr(m, "name"),
                        Required = ParseBool(Attr(m, "required"), false),
                        Default = ParseBool(Attr(m, "default"), false),
                    };
                    FillInstallable(mod, m);
                    Modules.Add(mod);
                }
            }

            var groupsNode = root.SelectSingleNode("groups");
            if (groupsNode != null)
            {
                foreach (XmlNode g in groupsNode.ChildNodes)
                {
                    if (g.NodeType != XmlNodeType.Element || g.Name != "group") continue;
                    var grp = new OivsGroup
                    {
                        Id = Attr(g, "id"),
                        Type = string.IsNullOrEmpty(Attr(g, "type")) ? "single" : Attr(g, "type"),
                        Title = Attr(g, "title"),
                        AllowNone = ParseBool(Attr(g, "allowNone"), true),
                        Default = string.IsNullOrEmpty(Attr(g, "default")) ? "none" : Attr(g, "default"),
                        Description = ChildText(g, "description"),
                    };
                    foreach (XmlNode o in g.ChildNodes)
                    {
                        if (o.NodeType != XmlNodeType.Element || o.Name != "option") continue;
                        var opt = new OivsOption { Id = Attr(o, "id"), Name = Attr(o, "name") };
                        FillInstallable(opt, o);
                        grp.Options.Add(opt);
                    }
                    Groups.Add(grp);
                }
            }
        }

        /// <summary>Reads description, media and the &lt;install&gt; spec into an item.</summary>
        private void FillInstallable(OivsInstallable item, XmlNode node)
        {
            item.Description = ChildText(node, "description");

            var mediaNode = node.SelectSingleNode("media");
            if (mediaNode != null)
            {
                foreach (XmlNode m in mediaNode.ChildNodes)
                {
                    if (m.NodeType != XmlNodeType.Element) continue;
                    if (m.Name == "compare")
                    {
                        item.Media.Add(new OivsMedia
                        {
                            Title = Attr(m, "title"),
                            Before = Attr(m, "before"),
                            After = Attr(m, "after"),
                        });
                    }
                    else if (m.Name == "image")
                    {
                        item.Media.Add(new OivsMedia { Image = Attr(m, "src"), Caption = Attr(m, "caption") });
                    }
                }
            }

            var installNode = node.SelectSingleNode("install");
            if (installNode != null)
            {
                foreach (XmlNode i in installNode.ChildNodes)
                {
                    if (i.NodeType != XmlNodeType.Element) continue;
                    if (i.Name == "content") item.ContentNodes.Add(i);
                    else if (i.Name == "folder") item.FolderSources.Add(Attr(i, "source"));
                }
            }
        }

        private void ParseMetadata(XmlNode node)
        {
            Metadata.Name = ChildText(node, "name");
            var versionNode = node.SelectSingleNode("version");
            if (versionNode != null)
            {
                int.TryParse(ChildText(versionNode, "major"), out int major);
                int.TryParse(ChildText(versionNode, "minor"), out int minor);
                Metadata.VersionMajor = major;
                Metadata.VersionMinor = minor;
                Metadata.Tag = ChildText(versionNode, "tag");
            }
            var descNode = node.SelectSingleNode("description");
            if (descNode != null) Metadata.Description = descNode.InnerText;
            var licNode = node.SelectSingleNode("licence");
            if (licNode != null) Metadata.License = licNode.InnerText;
            var authorNode = node.SelectSingleNode("author");
            if (authorNode != null)
            {
                Metadata.AuthorDisplayName = ChildText(authorNode, "displayName");
                Metadata.AuthorActionLink = ChildText(authorNode, "actionlink");
                if (string.IsNullOrEmpty(Metadata.AuthorActionLink))
                    Metadata.AuthorActionLink = ChildText(authorNode, "actionLink");
                Metadata.AuthorWeb = ChildText(authorNode, "web");
                Metadata.AuthorYoutube = ChildText(authorNode, "youtube");
            }
        }

        private void ParseColors(XmlNode node)
        {
            var headerBgNode = node.SelectSingleNode("headerBackground");
            if (headerBgNode != null)
            {
                Metadata.HeaderBackground = headerBgNode.InnerText;
                var useBlackAttr = headerBgNode.Attributes?["useBlackTextColor"];
                if (useBlackAttr != null && bool.TryParse(useBlackAttr.Value, out bool useBlack))
                    Metadata.UseBlackTextColor = useBlack;
            }
            Metadata.IconBackground = ChildText(node, "iconBackground");
        }

        // ---- selection -------------------------------------------------------

        /// <summary>Required modules + default-on optionals + each group's default.</summary>
        public OivsSelection DefaultSelection()
        {
            var sel = new OivsSelection();
            foreach (var m in Modules)
                if (!m.Required && m.Default) sel.EnabledModules.Add(m.Id);
            foreach (var g in Groups)
                sel.GroupChoices[g.Id] = g.Default ?? "none";
            return sel;
        }

        public OivsModule FindModule(string id) =>
            Modules.Find(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        public OivsGroup FindGroup(string id) =>
            Groups.Find(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

        // ---- operation building ---------------------------------------------

        /// <summary>
        /// Builds the combined OivOperation list for the selected modules/options.
        /// Required modules are always included. The returned ops resolve their
        /// source= against ContentPath, so feed them to a synthetic OivPackage.
        /// </summary>
        public List<OivOperation> BuildOperations(OivsSelection selection)
        {
            var ops = new List<OivOperation>();

            foreach (var m in Modules)
            {
                if (m.Required || selection.EnabledModules.Contains(m.Id))
                    AppendInstall(m, ops);
            }

            foreach (var g in Groups)
            {
                if (!selection.GroupChoices.TryGetValue(g.Id, out string choice))
                    choice = g.Default ?? "none";
                if (string.IsNullOrEmpty(choice) ||
                    choice.Equals("none", StringComparison.OrdinalIgnoreCase))
                    continue;
                var opt = g.FindOption(choice);
                if (opt != null) AppendInstall(opt, ops);
            }

            return ops;
        }

        private void AppendInstall(OivsInstallable item, List<OivOperation> ops)
        {
            // 1) inline <content> operation blocks
            foreach (var contentNode in item.ContentNodes)
                OivPackage.ParseContent(contentNode, ops);

            // 2) <folder> copies -> expand into top-level <add> ops (copied to game
            //    root by OivInstaller's ProcessTopLevelAdd, exactly like the .bat xcopy)
            foreach (var folderSrc in item.FolderSources)
            {
                if (string.IsNullOrWhiteSpace(folderSrc)) continue;
                string rel = folderSrc.Replace("/", "\\").TrimStart('\\');
                string baseDir = Path.Combine(ContentPath, rel);
                if (!Directory.Exists(baseDir)) continue;

                foreach (var file in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
                {
                    string relInside = file.Substring(baseDir.Length).TrimStart('\\', '/');
                    ops.Add(new OivAddOperation
                    {
                        // Source resolves via ContentPath + Source in ReadContentFile
                        Source = Path.Combine(rel, relInside),
                        // Destination is relative to game root (no update/x64/.rpf prefix)
                        Destination = relInside,
                    });
                }
            }
        }

        // ---- helpers ---------------------------------------------------------

        private static string Attr(XmlNode node, string name) =>
            node?.Attributes?[name]?.Value ?? "";

        private static string ChildText(XmlNode parent, string childName)
        {
            var child = parent?.SelectSingleNode(childName);
            return child?.InnerText?.Trim() ?? "";
        }

        private static bool ParseBool(string s, bool dflt)
        {
            if (string.IsNullOrEmpty(s)) return dflt;
            return bool.TryParse(s, out bool b) ? b : dflt;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
            {
                try { Directory.Delete(_tempDirectory, true); } catch { }
            }
        }
    }
}
