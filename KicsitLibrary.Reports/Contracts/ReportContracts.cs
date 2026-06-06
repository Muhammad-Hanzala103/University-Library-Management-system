using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Contracts
{
    public interface IReportDataProvider
    {
        ReportDefinition Definition { get; }

        Task<ReportResult> GenerateAsync(
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default);
    }

    public interface IReportExporter
    {
        ReportFormat Format { get; }

        Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IReportExportService
    {
        Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            int? userId = null,
            CancellationToken cancellationToken = default);
    }

    public interface IReportService
    {
        IReadOnlyList<ReportDefinition> GetDefinitions();

        Task<ReportResult> GenerateAsync(
            string reportKey,
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default);
    }
}
