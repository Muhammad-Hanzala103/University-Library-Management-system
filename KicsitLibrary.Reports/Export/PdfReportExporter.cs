using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Export
{
    public sealed class PdfReportExporter : IReportExporter
    {
        private const int LinesPerPage = 46;

        public ReportFormat Format => ReportFormat.PDF;

        public async Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = ReportFilePathResolver.Resolve(report, request, "pdf");
                var lines = BuildLines(report);
                var pages = lines
                    .Select((line, index) => new { line, index })
                    .GroupBy(item => item.index / LinesPerPage)
                    .Select(group => group.Select(item => item.line).ToList())
                    .ToList();
                if (pages.Count == 0)
                {
                    pages.Add(["No records matched the selected filters."]);
                }

                var bytes = BuildPdf(pages, cancellationToken);
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                return new ReportExportResult
                {
                    Succeeded = true,
                    FilePath = filePath,
                    Format = ReportFormat.PDF,
                    Message = $"PDF report exported to {filePath}.",
                    ExportedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ReportExportResult
                {
                    Succeeded = false,
                    Format = ReportFormat.PDF,
                    Message = "PDF export failed.",
                    ErrorMessage = ex.Message,
                    ExportedAt = DateTime.UtcNow
                };
            }
        }

        private static List<string> BuildLines(ReportResult report)
        {
            var lines = new List<string>
            {
                report.ReportTitle,
                report.InstitutionName,
                $"Generated: {report.GeneratedAt:dd-MMM-yyyy HH:mm:ss} by {report.GeneratedBy}"
            };
            if (report.AppliedFilters.Count > 0)
            {
                lines.Add($"Filters: {string.Join("; ", report.AppliedFilters)}");
            }

            lines.Add(string.Empty);
            lines.Add(string.Join(" | ", report.Columns.Select(column => column.Header)));
            lines.Add(new string('-', 110));
            if (report.Rows.Count == 0)
            {
                lines.Add("No records matched the selected filters.");
            }
            else
            {
                lines.AddRange(report.Rows.Select(row => string.Join(
                    " | ",
                    report.Columns.Select(column =>
                        CsvReportExporter.FormatValue(
                            row[column.Key],
                            column.Format)))));
            }

            if (report.SummaryItems.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Summary");
                lines.AddRange(report.SummaryItems.Select(item =>
                    $"{item.Key}: {item.Value}"));
            }

            return lines.SelectMany(WrapLine).ToList();
        }

        private static IEnumerable<string> WrapLine(string line)
        {
            const int maximumLength = 130;
            if (line.Length <= maximumLength)
            {
                yield return line;
                yield break;
            }

            for (var index = 0; index < line.Length; index += maximumLength)
            {
                yield return line.Substring(
                    index,
                    Math.Min(maximumLength, line.Length - index));
            }
        }

        private static byte[] BuildPdf(
            IReadOnlyList<List<string>> pages,
            CancellationToken cancellationToken)
        {
            var objectBodies = new List<string>();
            var pageObjectNumbers = new List<int>();
            var contentObjectNumbers = new List<int>();
            var nextObjectNumber = 4;
            foreach (var _ in pages)
            {
                pageObjectNumbers.Add(nextObjectNumber++);
                contentObjectNumbers.Add(nextObjectNumber++);
            }

            objectBodies.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objectBodies.Add(
                $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] /Count {pages.Count} >>");
            objectBodies.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                objectBodies.Add(
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectNumbers[pageIndex]} 0 R >>");

                var content = new StringBuilder();
                content.AppendLine("BT");
                content.AppendLine("/F1 7 Tf");
                content.AppendLine("36 560 Td");
                foreach (var line in pages[pageIndex])
                {
                    content.Append('(')
                        .Append(EscapePdfText(line))
                        .AppendLine(") Tj");
                    content.AppendLine("0 -11 Td");
                }
                content.AppendLine("ET");
                var contentText = content.ToString();
                objectBodies.Add(
                    $"<< /Length {Encoding.ASCII.GetByteCount(contentText)} >>\nstream\n{contentText}endstream");
            }

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(
                stream,
                Encoding.ASCII,
                1024,
                leaveOpen: true)
            {
                NewLine = "\n"
            };
            writer.WriteLine("%PDF-1.4");
            writer.Flush();

            var offsets = new List<long> { 0 };
            for (var index = 0; index < objectBodies.Count; index++)
            {
                offsets.Add(stream.Position);
                writer.WriteLine($"{index + 1} 0 obj");
                writer.WriteLine(objectBodies[index]);
                writer.WriteLine("endobj");
                writer.Flush();
            }

            var xrefOffset = stream.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {objectBodies.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");
            foreach (var offset in offsets.Skip(1))
            {
                writer.WriteLine($"{offset:D10} 00000 n ");
            }
            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {objectBodies.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(xrefOffset);
            writer.WriteLine("%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static string EscapePdfText(string value)
        {
            var ascii = new string(value.Select(character =>
                character <= 127 ? character : '?').ToArray());
            return ascii
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
