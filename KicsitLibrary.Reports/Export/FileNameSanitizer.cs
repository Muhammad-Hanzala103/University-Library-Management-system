using System;
using System.IO;
using System.Linq;

namespace KicsitLibrary.Reports.Export
{
    public static class FileNameSanitizer
    {
        public static string Sanitize(string? fileName)
        {
            var value = string.IsNullOrWhiteSpace(fileName)
                ? "Report"
                : fileName.Trim();
            var invalidCharacters = Path.GetInvalidFileNameChars()
                .Concat([':', '/', '\\', '*', '?', '"', '<', '>', '|'])
                .Distinct()
                .ToArray();

            foreach (var invalidCharacter in invalidCharacters)
            {
                value = value.Replace(invalidCharacter, '_');
            }

            value = string.Join(
                "_",
                value.Split(
                    [' ', '_'],
                    StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(value) ? "Report" : value;
        }
    }
}
