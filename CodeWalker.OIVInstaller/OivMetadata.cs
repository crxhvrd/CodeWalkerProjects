using System;
using System.Collections.Generic;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// Target game version for the OIV package
    /// </summary>
    public enum GameVersion
    {
        Any,      // Works with both (default)
        Legacy,   // GTA5.exe only
        Enhanced  // GTA5_Enhanced.exe only (Gen9)
    }

    /// <summary>
    /// Represents OIV package metadata from assembly.xml
    /// </summary>
    public class OivMetadata
    {
        public string Name { get; set; } = "";
        public string Tag { get; set; } = "";
        public int VersionMajor { get; set; }
        public int VersionMinor { get; set; }
        public string Version => $"{VersionMajor}.{VersionMinor}";
        
        // Target game version
        public GameVersion GameVersion { get; set; } = GameVersion.Any;
        
        // Author info
        public string AuthorDisplayName { get; set; } = "";
        public string AuthorActionLink { get; set; } = "";
        public string AuthorWeb { get; set; } = "";
        public string AuthorYoutube { get; set; } = "";
        public string AuthorFacebook { get; set; } = "";
        public string AuthorTwitter { get; set; } = "";
        
        public string Description { get; set; } = "";
        public string DescriptionFooterLink { get; set; } = "";
        public string DescriptionFooterLinkTitle { get; set; } = "";

        public string LargeDescription { get; set; } = "";
        public string LargeDescriptionFooterLink { get; set; } = "";
        public string LargeDescriptionFooterLinkTitle { get; set; } = "";

        public string License { get; set; } = "";
        public string LicenseFooterLink { get; set; } = "";
        public string LicenseFooterLinkTitle { get; set; } = "";
        
        // Colors (ARGB format from OIV)
        public string HeaderBackground { get; set; } = "";
        public string IconBackground { get; set; } = "";
        public bool UseBlackTextColor { get; set; }
    }
}
