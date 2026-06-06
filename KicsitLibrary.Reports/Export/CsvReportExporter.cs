using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Export
{
    public sealed class CsvReportExporter : IReportExporter
    {
        public ReportFormat Format => ReportFormat.CSV;

        public async Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var filePath = ReportFilePathResolver.Resolve(report, request, "csv");
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    useAsync: true);
                await using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                await writer.WriteLineAsync(string.Join(
                    ",",
                    report.Columns.Select(column => Escape(column.Header))));

                foreach (var row in report.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var values = report.Columns.Select(column =>
                        Escape(FormatValue(row[column.Key], column.Format)));
                    await writer.WriteLineAsync(string.Join(",", values));
                }

                await writer.FlushAsync(cancellationToken);
                return Success(filePath);
            }
            catch (Exception ex)
            {
                return Failure(ex);
            }
        }

        private static string Escape(string? value)
        {
            var text = value ?? string.Empty;
            return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
                ? $"\"{text.Replace("\"", "\"\"")}\""
                : text;
        }

        internal static string FormatValue(object? value, string? format)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString(
                    string.IsNullOrWhiteSpace(format)
                        ? "yyyy-MM-dd HH:mm:ss"
                        : format,
                    CultureInfo.InvariantCulture);
            }

            return value is IFormattable formattable
                ? formattable.ToString(format, CultureInfo.InvariantCulture)
                : value.ToString() ?? string.Empty;
        }

        private static ReportExportResult Success(string filePath)
        {
            return new ReportExportResult
            {
                Succeeded = true,
                FilePath = filePath,
                Format = ReportFormat.CSV,
                Message = $"CSV report exported to {filePath}.",
                ExportedAt = DateTime.UtcNow
            };
        }

        private static ReportExportResult Failure(Exception exception)
        {
            return new ReportExportResult
            {
                Succeeded = false,
                Format = ReportFormat.CSV,
                Message = "CSV export failed.",
                ErrorMessage = exception.Message,
                ExportedAt = DateTime.UtcNow
            };
        }
    }
}
