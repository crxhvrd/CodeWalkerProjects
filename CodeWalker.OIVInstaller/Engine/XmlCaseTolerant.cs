using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace CodeWalker.OIVInstaller
{
    /// <summary>
    /// XPath 1.0 string equality is case-sensitive, but GTA data names are
    /// case-insensitive (joaat-hashed) and ship in either case depending on game
    /// build — popgroups has "VEH_MID" in some builds and "veh_mid" in others, so a
    /// package xpath like Name[.="veh_utility"] silently misses. Select with the
    /// exact xpath first; when nothing matches, retry with every string-literal
    /// equality comparison rewritten through translate() to ignore case.
    /// </summary>
    internal static class XmlCaseTolerant
    {
        private const string Lower = "abcdefghijklmnopqrstuvwxyz";
        private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static XmlNode SelectSingleNode(XmlNode root, string xpath, out bool usedCaseFallback)
        {
            usedCaseFallback = false;
            var node = root.SelectSingleNode(xpath);
            if (node != null) return node;

            string relaxed = RelaxComparisons(xpath);
            if (relaxed == xpath) return null;
            try
            {
                node = root.SelectSingleNode(relaxed);
            }
            catch
            {
                // Rewrite produced an invalid xpath — keep the original not-found result.
                return null;
            }
            usedCaseFallback = node != null;
            return node;
        }

        // (see XmlTextUtil below for the save-side counterpart)

        // lhs="Value" / lhs='Value' → translate(lhs,'a…z','A…Z')="VALUE".
        // XPath 1.0 has no lower-case(); translate() is the standard workaround.
        internal static string RelaxComparisons(string xpath)
        {
            return Regex.Replace(xpath,
                @"(?<lhs>[^\s\[\]=!<>|,'""]+)\s*=\s*(?:""(?<val>[^""]*)""|'(?<val>[^']*)')",
                m =>
                {
                    string val = m.Groups["val"].Value;
                    if (!val.Any(char.IsLetter)) return m.Value;
                    string quote = val.Contains('"') ? "'" : "\"";
                    return $"translate({m.Groups["lhs"].Value},'{Lower}','{Upper}')={quote}{val.ToUpperInvariant()}{quote}";
                });
        }
    }

    internal static class XmlTextUtil
    {
        /// <summary>
        /// Puts the file's original XML declaration back after a save.
        ///
        /// XmlDocument.Save() rewrites the declaration from the writer's encoding, so
        /// the game's <c>&lt;?xml version="1.0" encoding="UTF-8"?&gt;</c> comes back as
        /// lowercase <c>utf-8</c>. Parsers don't care, but it means every edited file
        /// differs from vanilla on its very first line — so an uninstall could never
        /// restore it byte-for-byte, and diffs were noisy for no reason.
        /// </summary>
        public static string PreserveDeclaration(string original, string updated)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(updated)) return updated;

            var o = Regex.Match(original, @"^﻿?<\?xml[^>]*\?>");
            var u = Regex.Match(updated, @"^﻿?<\?xml[^>]*\?>");
            if (!o.Success || !u.Success || o.Value == u.Value) return updated;

            return o.Value + updated.Substring(u.Length);
        }
    }
}
