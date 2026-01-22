using System;
using System.Collections.Generic;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Base class for OIV installation operations
    /// </summary>
    public abstract class OivOperation
    {
        public abstract string OperationType { get; }
    }

    /// <summary>
    /// Represents an archive operation containing nested operations
    /// </summary>
    public class OivArchiveOperation : OivOperation
    {
        public override string OperationType => "Archive";
        
        /// <summary>
        /// Path to the archive relative to game folder (e.g., "update\update.rpf")
        /// </summary>
        public string ArchivePath { get; set; } = "";
        
        /// <summary>
        /// Whether to create the archive if it doesn't exist
        /// </summary>
        public bool CreateIfNotExist { get; set; }
        
        /// <summary>
        /// Archive type (e.g., "RPF7")
        /// </summary>
        public string Type { get; set; } = "RPF7";
        
        /// <summary>
        /// Child operations (add files, nested archives, etc.)
        /// </summary>
        public List<OivOperation> Children { get; set; } = new List<OivOperation>();
        
        public override string ToString() => $"Archive: {ArchivePath} ({Children.Count} operations)";
    }

    /// <summary>
    /// Represents a file add operation within an archive
    /// </summary>
    public class OivAddOperation : OivOperation
    {
        public override string OperationType => "Add";
        
        /// <summary>
        /// Source path within OIV content folder
        /// </summary>
        public string Source { get; set; } = "";
        
        /// <summary>
        /// Destination path within the target archive
        /// </summary>
        public string Destination { get; set; } = "";
        
        public override string ToString() => $"Add: {Destination}";
    }

    /// <summary>
    /// Represents a file delete operation in the game folder
    /// </summary>
    public class OivDeleteOperation : OivOperation
    {
        public override string OperationType => "Delete";
        
        /// <summary>
        /// Target file to delete (relative to game folder)
        /// </summary>
        public string Target { get; set; } = "";
        
        public override string ToString() => $"Delete: {Target}";
    }

    /// <summary>
    /// Represents a text file modification operation (container for insert/replace operations)
    /// </summary>
    public class OivTextOperation : OivOperation
    {
        public override string OperationType => "Text";
        
        /// <summary>
        /// Path to the text file within the RPF archive
        /// </summary>
        public string FilePath { get; set; } = "";
        
        /// <summary>
        /// Whether to create the file if it doesn't exist
        /// </summary>
        public bool CreateIfNotExist { get; set; }
        
        /// <summary>
        /// Child insert/replace operations to perform on this file
        /// </summary>
        public List<OivInsertOperation> Inserts { get; set; } = new List<OivInsertOperation>();
        public List<OivReplaceOperation> Replacements { get; set; } = new List<OivReplaceOperation>();
        public List<OivTextAddOperation> Adds { get; set; } = new List<OivTextAddOperation>();
        public List<OivTextDeleteOperation> Deletions { get; set; } = new List<OivTextDeleteOperation>();
        
        public override string ToString() => $"Text: {FilePath} ({Inserts.Count + Replacements.Count + Adds.Count + Deletions.Count} edits)";
    }

    /// <summary>
    /// Represents a line insertion operation within a text file
    /// </summary>
    public class OivInsertOperation : OivOperation
    {
        public override string OperationType => "Insert";
        
        /// <summary>
        /// Where to insert: "Before" or "After" the matched line
        /// </summary>
        public string Where { get; set; } = "Before";
        
        /// <summary>
        /// Line pattern to match (supports wildcards when Condition is "Mask")
        /// </summary>
        public string LinePattern { get; set; } = "";
        
        /// <summary>
        /// Match condition: "Mask" for wildcard matching, "Equal" for exact match
        /// </summary>
        public string Condition { get; set; } = "Mask";
        
        /// <summary>
        /// Text content to insert
        /// </summary>
        public string Content { get; set; } = "";
        
        public override string ToString() => $"Insert {Where} '{LinePattern}': {Content.Substring(0, Math.Min(30, Content.Length))}...";
    }

    /// <summary>
    /// Represents a line replacement operation within a text file
    /// </summary>
    public class OivReplaceOperation : OivOperation
    {
        public override string OperationType => "Replace";
        
        /// <summary>
        /// Line pattern to match (the line to be replaced)
        /// </summary>
        public string LinePattern { get; set; } = "";
        
        /// <summary>
        /// Match condition: "Mask", "Equal", "StartWith", "EndWith", "Contains"
        /// </summary>
        public string Condition { get; set; } = "Equal";
        
        /// <summary>
        /// New content to replace the line with
        /// </summary>
        public string Content { get; set; } = "";
        
        public override string ToString() => $"Replace '{LinePattern}' ({Condition}): {Content.Substring(0, Math.Min(30, Content.Length))}...";
    }

    /// <summary>
    /// Represents a line delete operation within a text file
    /// </summary>
    public class OivTextDeleteOperation : OivOperation
    {
        public override string OperationType => "Delete";
        
        public string Condition { get; set; } = "Equal";
        public string Content { get; set; } = ""; // The content/line to match for deletion
        
        public override string ToString() => $"Delete ({Condition}): {Content.Substring(0, Math.Min(30, Content.Length))}...";
    }

    /// <summary>
    /// Represents a text append operation (add to end of file)
    /// </summary>
    public class OivTextAddOperation : OivOperation
    {
        public override string OperationType => "Add";
        
        public string Content { get; set; } = "";
        
        public override string ToString() => $"Add Line: {Content.Substring(0, Math.Min(30, Content.Length))}...";
    }

    /// <summary>
    /// Represents an XML file modification operation using XPath
    /// </summary>
    public class OivXmlOperation : OivOperation
    {
        public override string OperationType => "Xml";
        
        /// <summary>
        /// Path to the XML file within the RPF archive
        /// </summary>
        public string FilePath { get; set; } = "";
        
        /// <summary>
        /// Child add operations (using XPath)
        /// </summary>
        public List<OivXmlAddOperation> Adds { get; set; } = new List<OivXmlAddOperation>();
        public List<OivXmlReplaceOperation> Replacements { get; set; } = new List<OivXmlReplaceOperation>();
        public List<OivXmlRemoveOperation> Removals { get; set; } = new List<OivXmlRemoveOperation>();
        
        public override string ToString() => $"Xml: {FilePath} ({Adds.Count + Replacements.Count + Removals.Count} edits)";
    }

    /// <summary>
    /// Represents an XML add operation using XPath
    /// </summary>
    public class OivXmlAddOperation : OivOperation
    {
        public override string OperationType => "XmlAdd";
        
        /// <summary>
        /// XPath to the parent element
        /// </summary>
        public string XPath { get; set; } = "";
        
        /// <summary>
        /// Where to append: "First", "Last", etc.
        /// </summary>
        public string Append { get; set; } = "Last";
        
        /// <summary>
        /// XML content to add
        /// </summary>
        public string Content { get; set; } = "";
        
        public override string ToString() => $"XmlAdd at {XPath}: {Content.Substring(0, Math.Min(30, Content.Length))}...";
    }

    public class OivXmlReplaceOperation : OivOperation
    {
        public override string OperationType => "XmlReplace";
        public string XPath { get; set; } = "";
        public string Content { get; set; } = ""; // New node content
        public override string ToString() => $"XmlReplace at {XPath}";
    }

    public class OivXmlRemoveOperation : OivOperation
    {
        public override string OperationType => "XmlRemove";
        public string XPath { get; set; } = "";
        public override string ToString() => $"XmlRemove at {XPath}";
    }

    /// <summary>
    /// Represents a PSO/META file modification (similar to XML)
    /// </summary>
    public class OivPsoOperation : OivXmlOperation
    {
        public override string OperationType => "Pso";
        public override string ToString() => $"Pso: {FilePath} ({Adds.Count + Replacements.Count + Removals.Count} edits)";
    }

    public class OivDefragmentationOperation : OivOperation
    {
        public override string OperationType => "Defragmentation";
        public string Variable { get; set; } = ""; // archive path
        public override string ToString() => $"Defragment: {Variable}";
    }
}
