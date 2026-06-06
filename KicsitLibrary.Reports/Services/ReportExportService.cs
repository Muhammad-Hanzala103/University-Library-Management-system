using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Services
{
    public sealed class ReportExportService : IReportExportService
    {
        private readonly IReadOnlyList<IReportExporter> _exporters;
        private readonly IActivityLogService _activityLogService;

        public ReportExportService(
            IEnumerable<IReportExporter> exporters,
            IActivityLogService activityLogService)
        {
            _exporters = exporters?.ToList() ??
                throw new ArgumentNullException(nameof(exporters));
            _activityLogService = activityLogService ??
                throw new ArgumentNullException(nameof(activityLogService));
        }

        public async Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            var exporter = _exporters.FirstOrDefault(item =>
                item.Format == request.Format);
            if (exporter == null)
            {
                return new ReportExportResult
                {
                    Succeeded = false,
                    Format = request.Format,
                    Message = "Report export failed.",
                    ErrorMessage =
                        $"No exporter is registered for {request.Format}.",
                    ExportedAt = DateTime.UtcNow
                };
            }

            var result = await exporter.ExportAsync(
                report,
                request,
                cancellationToken);
            var detail = result.Succeeded
                ? $"{report.ReportTitle} exported as {request.Format} to {result.FilePath}."
                : $"{report.ReportTitle} {request.Format} export failed: {result.ErrorMessage}";
            await _activityLogService.LogActivityAsync(
                result.Succeeded ? "Report Exported" : "Report Export Failed",
                detail,
                userId);
            return result;
        }
    }
}
