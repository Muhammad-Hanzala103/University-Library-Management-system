using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Reports.Providers;

public sealed class VisitDetailReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.VisitDetail,
        Title = "Visit Detail Report",
        Description = "Inspection and institutional visit observations and follow-up state.",
        Category = "Compliance Reports",
        Columns =
        [
            Column("VisitId", "Visit Id"),
            Column("Organization", "Organization"),
            Column("VisitType", "Visit Type"),
            Column("VisitDate", "Visit Date", "dd-MMM-yyyy"),
            Column("Department", "Department"),
            Column("Purpose", "Purpose"),
            Column("Observations", "Observations"),
            Column("Findings", "Findings"),
            Column("Suggestions", "Suggestions"),
            Column("ActionTaken", "Action Taken"),
            Column("NextFollowUpDate", "Next Follow Up Date", "dd-MMM-yyyy"),
            Column("Status", "Status")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.Organization, "Organization", ReportFilterType.Text),
            Filter(ReportFilterKeys.Department, "Department", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Visit Date", ReportFilterType.DateRange),
            Filter(ReportFilterKeys.PendingFollowUpOnly, "Pending Follow Up Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var visits = await Context.VisitRecords.AsNoTracking()
            .OrderByDescending(item => item.VisitDate)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var organization = FilterReader.Text(filters, ReportFilterKeys.Organization);
        var department = FilterReader.Text(filters, ReportFilterKeys.Department);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var pendingOnly = FilterReader.Boolean(filters, ReportFilterKeys.PendingFollowUpOnly);

        var rows = visits.Select(item =>
        {
            var pending = item.NextFollowUpDate.HasValue &&
                string.IsNullOrWhiteSpace(item.ActionTaken);
            return new { Visit = item, Status = pending ? "Pending Follow Up" : "Completed" };
        })
        .Where(item =>
            TextMatches(search, item.Visit.VisitNumber, item.Visit.OrganizationName, item.Visit.Purpose,
                item.Visit.Observations, item.Visit.Findings, item.Visit.Suggestions) &&
            TextMatches(organization, item.Visit.OrganizationName) &&
            ExactMatches(department, item.Visit.Department) &&
            (!fromDate.HasValue || AsLocalDate(item.Visit.VisitDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || AsLocalDate(item.Visit.VisitDate) <= toDate.Value.Date) &&
            (!pendingOnly || item.Status == "Pending Follow Up"))
        .Select(item => Row(
            ("VisitId", item.Visit.Id),
            ("Organization", item.Visit.OrganizationName),
            ("VisitType", item.Visit.VisitType),
            ("VisitDate", AsLocalDate(item.Visit.VisitDate)),
            ("Department", item.Visit.Department),
            ("Purpose", item.Visit.Purpose),
            ("Observations", item.Visit.Observations),
            ("Findings", item.Visit.Findings),
            ("Suggestions", item.Visit.Suggestions),
            ("ActionTaken", item.Visit.ActionTaken),
            ("NextFollowUpDate", item.Visit.NextFollowUpDate?.ToLocalTime()),
            ("Status", item.Status)))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Visits"] = rows.Count.ToString(),
                ["Pending Follow Up"] = rows.Count(row => Equals(row["Status"], "Pending Follow Up")).ToString()
            }, cancellationToken);
    }
}

public sealed class AuditReportDataProvider(KicsitLibraryDbContext context)
    : ReportDataProviderBase(context)
{
    private static readonly ReportDefinition ReportDefinition = new()
    {
        Key = ReportKeys.Audit,
        Title = "Audit Report",
        Description = "Audit observations, responsibilities, and action status.",
        Category = "Compliance Reports",
        Columns =
        [
            Column("AuditId", "Audit Id"),
            Column("AuditNumber", "Audit Number"),
            Column("AuditType", "Audit Type"),
            Column("AuditDate", "Audit Date", "dd-MMM-yyyy"),
            Column("FinancialYear", "Financial Year"),
            Column("Observations", "Observations"),
            Column("Findings", "Findings"),
            Column("Suggestions", "Suggestions"),
            Column("ActionRequired", "Action Required"),
            Column("ActionTaken", "Action Taken"),
            Column("ResponsiblePerson", "Responsible Person"),
            Column("Status", "Status")
        ],
        Filters =
        [
            Filter(ReportFilterKeys.SearchText, "Search Text", ReportFilterType.Text),
            Filter(ReportFilterKeys.AuditType, "Audit Type", ReportFilterType.Text),
            Filter(ReportFilterKeys.Status, "Status", ReportFilterType.Enum, Enum.GetNames<AuditStatus>()),
            Filter(ReportFilterKeys.FinancialYear, "Financial Year", ReportFilterType.Text),
            Filter(ReportFilterKeys.DateRange, "Audit Date", ReportFilterType.DateRange),
            Filter(ReportFilterKeys.PendingActionOnly, "Pending Action Only", ReportFilterType.Boolean)
        ]
    };

    public override ReportDefinition Definition => ReportDefinition;

    public override async Task<ReportResult> GenerateAsync(
        IReadOnlyCollection<ReportFilter> filters,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var audits = await Context.AuditRecords.AsNoTracking()
            .OrderByDescending(item => item.AuditDate)
            .ToListAsync(cancellationToken);
        var search = FilterReader.Text(filters, ReportFilterKeys.SearchText);
        var auditType = FilterReader.Text(filters, ReportFilterKeys.AuditType);
        var status = FilterReader.Text(filters, ReportFilterKeys.Status);
        var financialYear = FilterReader.Text(filters, ReportFilterKeys.FinancialYear);
        var fromDate = FilterReader.StartDate(filters, ReportFilterKeys.DateRange);
        var toDate = FilterReader.EndDate(filters, ReportFilterKeys.DateRange);
        var pendingOnly = FilterReader.Boolean(filters, ReportFilterKeys.PendingActionOnly);

        var rows = audits.Where(item =>
            TextMatches(search, item.AuditNumber, item.Observations, item.Findings, item.Suggestions,
                item.ActionRequired, item.ResponsiblePerson) &&
            TextMatches(auditType, item.AuditType) &&
            ExactMatches(status, item.Status.ToString()) &&
            ExactMatches(financialYear, item.FinancialYear) &&
            (!fromDate.HasValue || AsLocalDate(item.AuditDate) >= fromDate.Value.Date) &&
            (!toDate.HasValue || AsLocalDate(item.AuditDate) <= toDate.Value.Date) &&
            (!pendingOnly || (!string.IsNullOrWhiteSpace(item.ActionRequired) &&
                string.IsNullOrWhiteSpace(item.ActionTaken))))
        .Select(item => Row(
            ("AuditId", item.Id),
            ("AuditNumber", item.AuditNumber),
            ("AuditType", item.AuditType),
            ("AuditDate", AsLocalDate(item.AuditDate)),
            ("FinancialYear", item.FinancialYear),
            ("Observations", item.Observations),
            ("Findings", item.Findings),
            ("Suggestions", item.Suggestions),
            ("ActionRequired", item.ActionRequired),
            ("ActionTaken", item.ActionTaken),
            ("ResponsiblePerson", item.ResponsiblePerson),
            ("Status", item.Status.ToString())))
        .ToList();

        return await CreateResultAsync(filters, generatedBy, rows,
            new Dictionary<string, string>
            {
                ["Audits"] = rows.Count.ToString(),
                ["Pending Actions"] = rows.Count(row =>
                    !string.IsNullOrWhiteSpace(row["ActionRequired"]?.ToString()) &&
                    string.IsNullOrWhiteSpace(row["ActionTaken"]?.ToString())).ToString()
            }, cancellationToken);
    }
}
