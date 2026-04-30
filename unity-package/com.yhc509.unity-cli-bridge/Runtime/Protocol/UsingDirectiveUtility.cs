#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityCli.Protocol
{
    public static class UsingDirectiveUtility
    {
        // Note: generic type aliases (e.g. using Dict = Dictionary<string, int>) are not matched.
        private static readonly Regex UsingDirectiveLineRegex = new Regex(
            @"^[ \t]*using[ \t]+(?:(?:[A-Za-z_][\w]*[ \t]*=[ \t]*(?:global::)?[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)|(?:(?:static[ \t]+)?(?:global::)?[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*))[ \t]*;[ \t]*(?:\r?\n|$)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public static string StripUsingDirectives(string source, out string[] usings)
        {
            var extractedUsings = new List<string>();
            string stripped = UsingDirectiveLineRegex.Replace(source, delegate(Match match)
            {
                extractedUsings.Add(match.Value.Trim());
                return string.Empty;
            });

            usings = extractedUsings.ToArray();
            return stripped.TrimStart('\r', '\n');
        }
    }
}
