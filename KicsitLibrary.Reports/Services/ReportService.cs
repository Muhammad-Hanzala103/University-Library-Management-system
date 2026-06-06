using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;

namespace KicsitLibrary.Reports.Services
{
    public sealed class ReportService : IReportService
    {
        private readonly IReadOnlyList<IReportDataProvider> _providers;

        public ReportService(IEnumerable<IReportDataProvider> providers)
        {
            _providers = providers?.ToList() ??
                throw new ArgumentNullException(nameof(providers));
        }

        public IReadOnlyList<ReportDefinition> GetDefinitions()
        {
            return _providers
                .Select(provider => provider.Definition)
                .OrderBy(definition => definition.Title)
                .ToList();
        }

        public Task<ReportResult> GenerateAsync(
            string reportKey,
            IReadOnlyCollection<ReportFilter> filters,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            var provider = _providers.FirstOrDefault(item =>
                item.Definition.Key.Equals(
                    reportKey,
                    StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                throw new InvalidOperationException(
                    $"Report '{reportKey}' is not registered.");
            }

            return provider.GenerateAsync(filters, generatedBy, cancellationToken);
        }
    }
}
