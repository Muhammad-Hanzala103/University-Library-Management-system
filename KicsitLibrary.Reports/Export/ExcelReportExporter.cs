using System;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Export
{
    public sealed class ExcelReportExporter : IReportExporter
    {
        public ReportFormat Format => ReportFormat.Excel;

        public Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = ReportFilePathResolver.Resolve(report, request, "xlsx");
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Report");
                var lastColumn = Math.Max(1, report.Columns.Count);
                var rowNumber = 1;

                worksheet.Cell(rowNumber, 1).Value = report.ReportTitle;
                worksheet.Range(rowNumber, 1, rowNumber, lastColumn).Merge();
                worksheet.Cell(rowNumber, 1).Style.Font.Bold = true;
                worksheet.Cell(rowNumber, 1).Style.Font.FontSize = 16;
                rowNumber++;

                worksheet.Cell(rowNumber++, 1).Value = report.InstitutionName;
                worksheet.Cell(rowNumber++, 1).Value =
                    $"Generated: {report.GeneratedAt:dd-MMM-yyyy HH:mm:ss} by {report.GeneratedBy}";

                if (report.AppliedFilters.Count > 0)
                {
                    worksheet.Cell(rowNumber++, 1).Value =
                        $"Filters: {string.Join("; ", report.AppliedFilters)}";
                }

                rowNumber++;
                var headerRow = rowNumber++;
                for (var columnIndex = 0; columnIndex < report.Columns.Count; columnIndex++)
                {
                    worksheet.Cell(headerRow, columnIndex + 1).Value =
                        report.Columns[columnIndex].Header;
                }
                worksheet.Range(headerRow, 1, headerRow, lastColumn).Style.Font.Bold = true;
                worksheet.Range(headerRow, 1, headerRow, lastColumn).Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#1B2A47");
                worksheet.Range(headerRow, 1, headerRow, lastColumn).Style.Font.FontColor =
                    XLColor.White;
                worksheet.Range(headerRow, 1, headerRow, lastColumn).Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

                foreach (var row in report.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var columnIndex = 0; columnIndex < report.Columns.Count; columnIndex++)
                    {
                        var column = report.Columns[columnIndex];
                        SetCellValue(
                            worksheet.Cell(rowNumber, columnIndex + 1),
                            row[column.Key],
                            column.Format);
                    }
                    rowNumber++;
                }

                if (report.Rows.Count == 0)
                {
                    worksheet.Cell(rowNumber++, 1).Value =
                        "No records matched the selected filters.";
                }

                if (report.SummaryItems.Count > 0)
                {
                    rowNumber++;
                    worksheet.Cell(rowNumber++, 1).Value = "Summary";
                    foreach (var item in report.SummaryItems)
                    {
                        worksheet.Cell(rowNumber, 1).Value = item.Key;
                        worksheet.Cell(rowNumber, 2).Value = item.Value;
                        rowNumber++;
                    }
                }

                if (report.Rows.Count > 0 && report.Columns.Count > 0)
                {
                    worksheet.Range(
                        headerRow,
                        1,
                        headerRow + report.Rows.Count,
                        report.Columns.Count).SetAutoFilter();
                }

                worksheet.SheetView.FreezeRows(headerRow);
                worksheet.Columns().AdjustToContents();
                foreach (var column in worksheet.ColumnsUsed())
                {
                    if (column.Width > 45)
                    {
                        column.Width = 45;
                        column.Style.Alignment.WrapText = true;
                    }
                }
                var usedRange = worksheet.RangeUsed();
                if (usedRange != null)
                {
                    usedRange.Style.Alignment.Vertical =
                        XLAlignmentVerticalValues.Top;
                }
                workbook.SaveAs(filePath);

                return Task.FromResult(new ReportExportResult
                {
                    Succeeded = true,
                    FilePath = filePath,
                    Format = ReportFormat.Excel,
                    Message = $"Excel report exported to {filePath}.",
                    ExportedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ReportExportResult
                {
                    Succeeded = false,
                    Format = ReportFormat.Excel,
                    Message = "Excel export failed.",
                    ErrorMessage = ex.Message,
                    ExportedAt = DateTime.UtcNow
                });
            }
        }

        private static void SetCellValue(
            IXLCell cell,
            object? value,
            string? format)
        {
            switch (value)
            {
                case null:
                    cell.Value = string.Empty;
                    break;
                case DateTime dateTime:
                    cell.Value = dateTime;
                    cell.Style.DateFormat.Format =
                        string.IsNullOrWhiteSpace(format)
                            ? "dd-MMM-yyyy HH:mm"
                            : format;
                    break;
                case int integer:
                    cell.Value = integer;
                    break;
                case long longInteger:
                    cell.Value = longInteger;
                    break;
                case decimal decimalValue:
                    cell.Value = decimalValue;
                    cell.Style.NumberFormat.Format =
                        string.IsNullOrWhiteSpace(format)
                            ? "#,##0.00"
                            : NormalizeNumberFormat(format);
                    break;
                case double doubleValue:
                    cell.Value = doubleValue;
                    break;
                case bool boolean:
                    cell.Value = boolean;
                    break;
                default:
                    cell.Value = value.ToString() ?? string.Empty;
                    break;
            }
        }

        private static string NormalizeNumberFormat(string format)
        {
            return format.Equals("N2", StringComparison.OrdinalIgnoreCase)
                ? "#,##0.00"
                : format;
        }
    }
}
