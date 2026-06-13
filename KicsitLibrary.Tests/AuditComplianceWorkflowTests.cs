using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Models;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Services.Auditing;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class AuditComplianceWorkflowTests
{
    [Fact]
    public async Task ActivityLogBrowser_ReturnsLatestLogsFirst()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddLogAsync(database, data.User, "Older", DateTime.UtcNow.AddMinutes(-5));
        await AddLogAsync(database, data.User, "Newest", DateTime.UtcNow);

        var logs = await CreateBrowser(database, Admin(data.User))
            .GetActivityLogsAsync(new ActivityLogFilter());

        Assert.Equal("Newest", logs[0].Action);
        Assert.Equal("Older", logs[1].Action);
    }

    [Fact]
    public async Task ActivityLogBrowser_FiltersByAction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddLogAsync(database, data.User, "Book Checkout", DateTime.UtcNow);
        await AddLogAsync(database, data.User, "Login", DateTime.UtcNow);

        var logs = await CreateBrowser(database, Admin(data.User))
            .GetActivityLogsAsync(new ActivityLogFilter { Action = "Login" });

        Assert.Single(logs);
        Assert.Equal("Login", logs[0].Action);
    }

    [Fact]
    public async Task ActivityLogBrowser_FiltersByEntityMetadata()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddLogAsync(
            database, data.User, "Audit Record Updated", DateTime.UtcNow,
            "EntityName=AuditRecord;EntityId=42;AuditNumber=AUD-42");
        await AddLogAsync(database, data.User, "Login", DateTime.UtcNow);

        var logs = await CreateBrowser(database, Admin(data.User))
            .GetActivityLogsAsync(new ActivityLogFilter
            {
                EntityName = "AuditRecord",
                EntityId = 42
            });

        var log = Assert.Single(logs);
        Assert.Equal("AuditRecord", log.EntityName);
        Assert.Equal(42, log.EntityId);
    }

    [Fact]
    public async Task ActivityLogBrowser_FiltersByUserAndUsernameSearch()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var second = new User
        {
            Username = "second-auditor",
            PasswordHash = "test",
            FullName = "Second Auditor",
            Email = "second@test.invalid"
        };
        database.Context.Users.Add(second);
        await database.Context.SaveChangesAsync();
        await AddLogAsync(database, data.User, "First Action", DateTime.UtcNow);
        await AddLogAsync(database, second, "Second Action", DateTime.UtcNow);

        var service = CreateBrowser(database, Admin(data.User));
        var byUser = await service.GetActivityLogsAsync(
            new ActivityLogFilter { UserId = second.Id });
        var byUsername = await service.GetActivityLogsAsync(
            new ActivityLogFilter { SearchText = "second-auditor" });

        Assert.Single(byUser);
        Assert.Single(byUsername);
        Assert.Equal(second.Id, byUsername[0].UserId);
    }

    [Fact]
    public async Task ActivityLogDetails_ReturnsFullRowAndMetadata()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var log = await AddLogAsync(
            database, data.User, "Audit Status Changed", DateTime.UtcNow,
            "EntityName=AuditRecord;EntityId=7;NewStatus=Closed");

        var details = await CreateBrowser(database, Admin(data.User))
            .GetActivityLogDetailsAsync(log.Id);

        Assert.Contains("NewStatus=Closed", details.FullDetail);
        Assert.Equal("Closed", details.Metadata["NewStatus"]);
        Assert.Equal(data.User.Username, details.Username);
    }

    [Fact]
    public async Task ActivityLogBrowser_DefaultLimitIsLatestFiveHundred()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.ActivityLogs.AddRange(Enumerable.Range(1, 510).Select(index =>
            new ActivityLog
            {
                Action = $"Action {index}",
                Detail = $"EntityName=Test;EntityId={index}",
                UserId = data.User.Id,
                CreatedAt = DateTime.UtcNow.AddSeconds(index)
            }));
        await database.Context.SaveChangesAsync();

        var logs = await CreateBrowser(database, Admin(data.User))
            .GetActivityLogsAsync(new ActivityLogFilter());

        Assert.Equal(500, logs.Count);
        Assert.Equal("Action 510", logs[0].Action);
    }

    [Fact]
    public async Task ActivityLogSnapshot_UsesExistingExportPipeline()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddLogAsync(database, data.User, "Export Test", DateTime.UtcNow);
        var exporter = new CapturingReportExportService();

        var result = await new ActivityLogBrowserService(
            database.Context, Admin(data.User), exporter)
            .ExportActivityLogSnapshotAsync(new ActivityLogFilter(), "CSV");

        Assert.True(result.Succeeded);
        Assert.NotNull(exporter.Report);
        Assert.Equal("Activity Log Snapshot", exporter.Report!.ReportTitle);
        Assert.Single(exporter.Report.Rows);
    }

    [Fact]
    public async Task AuditRecord_CanBeCreatedWithRequiredFields()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();

        var result = await CreateAuditService(database, Admin(data.User))
            .CreateAuditRecordAsync(Request("AUD-001"));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("Full finding text", result.AuditRecord!.Findings);
        Assert.Equal(1, await database.Context.AuditRecords.CountAsync());
    }

    [Fact]
    public async Task DuplicateAuditNumber_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        
        var res1 = await service.CreateAuditRecordAsync(Request(""));
        var res2 = await service.CreateAuditRecordAsync(Request(""));

        var req2 = res2.AuditRecord!;
        req2.AuditNumber = "1";

        var duplicate = await service.UpdateAuditRecordAsync(req2.AuditRecordId, req2);

        Assert.False(duplicate.Succeeded);
        Assert.Contains("already exists", duplicate.ErrorMessage);
    }

    [Fact]
    public async Task AuditRecord_CanBeUpdated()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        var created = await service.CreateAuditRecordAsync(Request("AUD-UPD"));
        var request = created.AuditRecord!;
        request.Findings = "Updated full finding";
        request.ActionTaken = "Corrective action completed";

        var updated = await service.UpdateAuditRecordAsync(
            request.AuditRecordId, request);

        Assert.True(updated.Succeeded, updated.ErrorMessage);
        Assert.Equal("Updated full finding", updated.AuditRecord!.Findings);
        Assert.Equal("Corrective action completed", updated.AuditRecord.ActionTaken);
    }

    [Fact]
    public async Task AuditStatusChange_RequiresAndStoresRemarks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        var created = await service.CreateAuditRecordAsync(Request("AUD-STATUS"));

        var blocked = await service.ChangeAuditStatusAsync(
            created.AuditRecord!.AuditRecordId, AuditStatus.Submitted, "");
        var changed = await service.ChangeAuditStatusAsync(
            created.AuditRecord.AuditRecordId, AuditStatus.UnderReview, "Committee review started");

        Assert.False(blocked.Succeeded);
        Assert.True(changed.Succeeded, changed.ErrorMessage);
        Assert.Equal(AuditStatus.UnderReview, changed.AuditRecord!.Status);
        Assert.Contains("Committee review started", changed.AuditRecord.Remarks);
    }

    [Fact]
    public async Task AuditDelete_IsSoftDeleteAndRequiresReason()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        var created = await service.CreateAuditRecordAsync(Request("AUD-DEL"));

        var blocked = await service.DeleteAuditRecordAsync(
            created.AuditRecord!.AuditRecordId, "");
        var deleted = await service.DeleteAuditRecordAsync(
            created.AuditRecord.AuditRecordId, "Duplicate inspection entry");

        Assert.False(blocked.Succeeded);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);
        Assert.Empty(await database.Context.AuditRecords.ToListAsync());
        var stored = await database.Context.AuditRecords.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == created.AuditRecord.AuditRecordId);
        Assert.True(stored.IsDeleted);
        Assert.Equal("Duplicate inspection entry", stored.DeletedReason);
        Assert.Contains(await database.Context.DeletedRecordArchives.ToListAsync(),
            item => item.TableName == "AuditRecords" && item.RecordId == stored.Id);
    }

    [Fact]
    public async Task AuditActions_WriteActivityLogs()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        var created = await service.CreateAuditRecordAsync(Request("AUD-LOG"));
        var request = created.AuditRecord!;
        request.Observations = "Updated observation";
        await service.UpdateAuditRecordAsync(request.AuditRecordId, request);
        await service.ChangeAuditStatusAsync(
            request.AuditRecordId, AuditStatus.Submitted, "Submitted to committee");

        var actions = await database.Context.ActivityLogs
            .Select(item => item.Action).ToListAsync();

        Assert.Contains("Audit Record Created", actions);
        Assert.Contains("Audit Record Updated", actions);
        Assert.Contains("Audit Status Changed", actions);
    }

    [Fact]
    public async Task AuditRecords_FilterByStatusAndDateRange()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));
        var first = Request("AUD-OLD");
        first.AuditDate = new DateTime(2025, 1, 10);
        first.Status = AuditStatus.Draft;
        var second = Request("AUD-NEW");
        second.AuditDate = new DateTime(2026, 6, 1);
        second.Status = AuditStatus.Completed;
        await service.CreateAuditRecordAsync(first);
        await service.CreateAuditRecordAsync(second);

        var results = await service.GetAuditRecordsAsync(new AuditRecordFilter
        {
            Status = AuditStatus.Completed,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        var row = Assert.Single(results);
        Assert.Equal("2", row.AuditNumber);
    }

    [Fact]
    public async Task AuditReport_StillReturnsAuditRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await CreateAuditService(database, Admin(data.User))
            .CreateAuditRecordAsync(Request("AUD-REPORT"));

        var report = await new AuditReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        Assert.Single(report.Rows);
        Assert.Equal("Internal Audit", report.Rows[0]["AuditType"]);
    }

    [Fact]
    public async Task AuditorCanViewButCannotManageAuditRecords()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        database.Context.AuditRecords.Add(new AuditRecord
        {
            AuditNumber = "AUD-VIEW",
            AuditDate = DateTime.Today,
            AuditType = "Internal Audit",
            Status = AuditStatus.Draft,
            CreatedByUserId = data.User.Id
        });
        await database.Context.SaveChangesAsync();
        var auditor = WithRole(data.User, "Auditor", canView: true, canManage: false);
        var service = CreateAuditService(database, auditor);

        var rows = await service.GetAuditRecordsAsync(new AuditRecordFilter());
        var create = await service.CreateAuditRecordAsync(Request("AUD-BLOCKED"));

        Assert.Single(rows);
        Assert.False(create.Succeeded);
        Assert.Contains("cannot create", create.ErrorMessage);
    }

    private static ActivityLogBrowserService CreateBrowser(
        SqliteTestDatabase database,
        IAuthenticationService authenticationService) =>
        new(database.Context, authenticationService, new CapturingReportExportService());

    private static AuditRecordService CreateAuditService(
        SqliteTestDatabase database,
        IAuthenticationService authenticationService) =>
        new(database.Context, authenticationService);

    private static FakeAuthenticationService Admin(User user) =>
        WithRole(user, "Admin", canView: true, canManage: true);

    private static FakeAuthenticationService WithRole(
        User user,
        string roleName,
        bool canView,
        bool canManage)
    {
        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            Role = new Role { Name = roleName }
        });
        return new FakeAuthenticationService(user, canView, canManage);
    }

    private static async Task<ActivityLog> AddLogAsync(
        SqliteTestDatabase database,
        User user,
        string action,
        DateTime createdAt,
        string detail = "Test detail")
    {
        var log = new ActivityLog
        {
            Action = action,
            Detail = detail,
            UserId = user.Id,
            IpAddress = "127.0.0.1",
            CreatedAt = createdAt
        };
        database.Context.ActivityLogs.Add(log);
        await database.Context.SaveChangesAsync();
        log.CreatedAt = createdAt;
        await database.Context.SaveChangesAsync();
        return log;
    }

    private static AuditRecordDetails Request(string auditNumber) =>
        new()
        {
            AuditNumber = auditNumber,
            AuditDate = DateTime.Today,
            AuditType = "Internal Audit",
            FinancialYear = "2025-26",
            InspectionDetail = "Inspection detail",
            FinancialDetail = "Financial detail",
            Observations = "Full observation text",
            Findings = "Full finding text",
            Suggestions = "Full suggestion text",
            ActionRequired = "Corrective action required",
            ResponsiblePerson = "Head Librarian",
            Status = AuditStatus.Draft,
            Remarks = "Initial audit"
        };

    private sealed class FakeAuthenticationService(
        User currentUser,
        bool canView,
        bool canManage) : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;
        public Task<User?> LoginAsync(string username, string password) =>
            Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) =>
            Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
            Task.FromResult(permissionCode switch
            {
                "VIEW_AUDITS" => canView,
                "MANAGE_AUDITS" => canManage,
                _ => false
            });
        public Task<bool> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult(true);
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class CapturingReportExportService : IReportExportService
    {
        public ReportResult? Report { get; private set; }

        public Task<ReportExportResult> ExportAsync(
            ReportResult report,
            ReportExportRequest request,
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            Report = report;
            return Task.FromResult(new ReportExportResult
            {
                Succeeded = true,
                Format = request.Format,
                FilePath = "test-export.csv",
                Message = "Export completed.",
                ExportedAt = DateTime.UtcNow
            });
        }
    }

    [Fact]
    public async Task AuditNumber_AutomaticGenerationStartsAt1AndIncrements()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));

        // Create first audit (should get number 1)
        var req1 = Request(""); // Empty audit number
        var res1 = await service.CreateAuditRecordAsync(req1);
        Assert.True(res1.Succeeded, res1.ErrorMessage);
        Assert.Equal("1", res1.AuditRecord!.AuditNumber);

        // Create second audit (should get number 2)
        var req2 = Request("");
        var res2 = await service.CreateAuditRecordAsync(req2);
        Assert.True(res2.Succeeded, res2.ErrorMessage);
        Assert.Equal("2", res2.AuditRecord!.AuditNumber);

        // Soft delete first audit
        await service.DeleteAuditRecordAsync(res1.AuditRecord.AuditRecordId, "soft delete test");

        // Create third audit (should still increment to 3, even if 1 was deleted and max is 2)
        var req3 = Request("");
        var res3 = await service.CreateAuditRecordAsync(req3);
        Assert.True(res3.Succeeded, res3.ErrorMessage);
        Assert.Equal("3", res3.AuditRecord!.AuditNumber);
    }

    [Fact]
    public async Task AuditNumber_MustAlwaysBeNumeric()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateAuditService(database, Admin(data.User));

        // Attempting to save with a non-numeric audit number (should fail validation)
        var req = Request("ABC");
        var res = await service.CreateAuditRecordAsync(req);

        // Wait! CreateAuditRecordAsync auto-generates a numeric number by overriding whatever we pass!
        // But let's check: if we pass a non-numeric audit number, it gets overwritten by the auto-generator!
        // Let's verify that the generated audit number is numeric:
        Assert.True(res.Succeeded);
        Assert.True(int.TryParse(res.AuditRecord!.AuditNumber, out _));
    }

    [Fact]
    public void FinancialYearState_DefaultsToCurrentCalculatedYearAndToggles()
    {
        // Default
        Assert.True(KicsitLibrary.Core.Helpers.FinancialYearState.IsCurrentYear);
        var calculated = KicsitLibrary.Core.Helpers.FinancialYearState.GetCurrentFinancialYear();
        Assert.Equal(calculated, KicsitLibrary.Core.Helpers.FinancialYearState.SelectedYear);

        // Toggle to custom
        KicsitLibrary.Core.Helpers.FinancialYearState.IsCurrentYear = false;
        KicsitLibrary.Core.Helpers.FinancialYearState.SelectedYear = "2024-2025";
        Assert.Equal("2024-2025", KicsitLibrary.Core.Helpers.FinancialYearState.SelectedYear);

        // Restore default for other tests
        KicsitLibrary.Core.Helpers.FinancialYearState.IsCurrentYear = true;
    }
}


