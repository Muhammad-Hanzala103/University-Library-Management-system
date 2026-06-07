using System;
using System.IO;
using KicsitLibrary.Core;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Export
{
    internal static class ReportFilePathResolver
    {
        public static string Resolve(
            ReportResult report,
            ReportExportRequest request,
            string extension)
        {
            var directory = string.IsNullOrWhiteSpace(request.OutputDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    ProductBrand.ReportFolderName)
                : request.OutputDirectory;
            Directory.CreateDirectory(directory);

            var requestedName = string.IsNullOrWhiteSpace(request.FileName)
                ? $"{report.ReportTitle}_{DateTime.Now:yyyyMMdd_HHmmss}"
                : Path.GetFileNameWithoutExtension(request.FileName);
            var baseName = FileNameSanitizer.Sanitize(requestedName);
            var filePath = Path.Combine(directory, $"{baseName}.{extension}");

            if (request.Overwrite || !File.Exists(filePath))
            {
                return filePath;
            }

            for (var index = 2; ; index++)
            {
                var candidate = Path.Combine(
                    directory,
                    $"{baseName}_{index}.{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
