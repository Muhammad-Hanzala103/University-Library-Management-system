using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Reports.Export;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Services.Clearance;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class ClearanceWorkflowTests
{
    [Fact]
    public async Task StudentWithoutIssuesOrFines_CanBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateService(database, data.User);

        var result = await service.CheckStudentClearanceAsync(data.Student.Id);

        Assert.True(result.CanClear);
        Assert.Empty(result.BlockingItems);
    }

    [Fact]
    public async Task StudentWithActiveIssue_CannotBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(10));

        var result = await CreateService(database, data.User)
            .CheckStudentClearanceAsync(data.Student.Id);

        Assert.False(result.CanClear);
        Assert.Equal(1, result.PendingBooksCount);
        Assert.Contains(result.BlockingItems, item => item.BlockType == "Active Issue");
    }

    [Fact]
    public async Task StudentWithUnpaidFine_CannotBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddFineAsync(database, data, FineStatus.Unpaid, 50);

        var result = await CreateService(database, data.User)
            .CheckStudentClearanceAsync(data.Student.Id);

        Assert.False(result.CanClear);
        Assert.Equal(50, result.PendingFineAmount);
    }

    [Fact]
    public async Task StudentWithPartialFine_CannotBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await AddFineAsync(database, data, FineStatus.Partial, 20);

        var result = await CreateService(database, data.User)
            .CheckStudentClearanceAsync(data.Student.Id);

        Assert.False(result.CanClear);
        Assert.Contains(result.BlockingItems, item => item.BlockType == "Pending Fine");
    }

    [Fact]
    public async Task StudentWithDamagedPendingCase_CannotBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(10));
        data.Copy.AvailabilityStatus = BookStatus.Damaged;
        await database.Context.SaveChangesAsync();

        var result = await CreateService(database, data.User)
            .CheckStudentClearanceAsync(data.Student.Id);

        Assert.False(result.CanClear);
        Assert.Equal(1, result.LostOrDamagedCaseCount);
        Assert.Contains(result.BlockingItems, item => item.BlockType == "Lost or Damaged Case");
    }

    [Fact]
    public async Task FacultyWithoutIssuesOrFines_CanBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var faculty = await AddFacultyAsync(database);

        var result = await CreateService(database, data.User)
            .CheckFacultyStaffClearanceAsync(faculty.Id);

        Assert.True(result.CanClear);
    }

    [Fact]
    public async Task FacultyWithActiveIssue_CannotBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var faculty = await AddFacultyAsync(database);
        database.Context.IssueRecords.Add(new IssueRecord
        {
            AccessionNumber = data.Copy.AccessionNumber,
            BookCopyId = data.Copy.Id,
            MemberType = MemberType.FacultyStaff,
            FacultyStaffId = faculty.Id,
            IssueDate = DateTime.UtcNow,
            ExpectedReturnDate = DateTime.UtcNow.AddDays(14),
            FinePerDay = 10,
            IssuedByUserId = data.User.Id
        });
        await database.Context.SaveChangesAsync();

        var result = await CreateService(database, data.User)
            .CheckFacultyStaffClearanceAsync(faculty.Id);

        Assert.False(result.CanClear);
        Assert.Equal(1, result.PendingBooksCount);
    }

    [Fact]
    public async Task ClearanceApproval_UpdatesStudentStatusAndDate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();

        var result = await CreateService(database, data.User)
            .ApproveStudentClearanceAsync(data.Student.Id, "Graduation clearance");

        Assert.True(result.Succeeded, result.ErrorMessage);
        var student = await database.Context.Students.FindAsync(data.Student.Id);
        Assert.Equal(ClearanceStatus.Cleared, student!.ClearanceStatus);
        Assert.NotNull(student.ClearanceDate);
        Assert.Equal(data.User.Id, student.ClearedByUserId);
    }

    [Fact]
    public async Task ClearanceRevoke_ResetsStatusAndStoresReason()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateService(database, data.User);
        await service.ApproveStudentClearanceAsync(data.Student.Id, "Approved");

        var result = await service.RevokeStudentClearanceAsync(
            data.Student.Id,
            "New dues discovered");

        Assert.True(result.Succeeded, result.ErrorMessage);
        var student = await database.Context.Students.FindAsync(data.Student.Id);
        Assert.Equal(ClearanceStatus.NotCleared, student!.ClearanceStatus);
        Assert.Null(student.ClearanceDate);
        Assert.Contains("New dues discovered", student.ClearanceRemarks);
    }

    [Fact]
    public async Task ClearanceApproval_WritesActivityLog()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();

        await CreateService(database, data.User)
            .ApproveStudentClearanceAsync(data.Student.Id, "Approved for graduation");

        Assert.Contains(
            await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Clearance Approved" &&
                log.UserId == data.User.Id &&
                log.Detail.Contains(data.Student.RegistrationNumber));
    }

    [Fact]
    public async Task ClearanceCertificate_CreatesPdfForClearedMember()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateService(database, data.User);
        await service.ApproveStudentClearanceAsync(data.Student.Id, "Graduation completed");
        using var directory = new TemporaryDirectory();

        var result = await service.GenerateClearanceCertificateAsync(
            MemberType.Student,
            data.Student.Id,
            directory.Path);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(File.Exists(result.FilePath));
        Assert.True(new FileInfo(result.FilePath!).Length > 100);
        Assert.Contains(
            await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Clearance Certificate Generated");
    }

    [Fact]
    public async Task CertificateGeneration_FailsWhenClearedMemberBecomesBlocked()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateService(database, data.User);
        await service.ApproveStudentClearanceAsync(data.Student.Id, "Initially clear");
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(10));
        using var directory = new TemporaryDirectory();

        var result = await service.GenerateClearanceCertificateAsync(
            MemberType.Student,
            data.Student.Id,
            directory.Path);

        Assert.False(result.Succeeded);
        Assert.Contains("dues", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task StudentClearanceReport_ReflectsServicePendingCounts()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(10));
        data.Copy.AvailabilityStatus = BookStatus.Lost;
        await database.Context.SaveChangesAsync();

        var serviceResult = await CreateService(database, data.User)
            .CheckStudentClearanceAsync(data.Student.Id);
        var reportResult = await new StudentClearanceReportDataProvider(database.Context)
            .GenerateAsync([], "Tester");

        var row = Assert.Single(reportResult.Rows);
        Assert.Equal(serviceResult.PendingBooksCount, row["PendingBooksCount"]);
        Assert.Equal(serviceResult.LostOrDamagedCaseCount, row["LostOrDamagedCaseCount"]);
    }

    [Fact]
    public async Task FacultyApprovalAndHistory_ArePersisted()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var faculty = await AddFacultyAsync(database);
        var service = CreateService(database, data.User);

        var approval = await service.ApproveFacultyStaffClearanceAsync(
            faculty.Id,
            "Employment exit clearance");
        var history = await service.GetClearanceHistoryAsync();

        Assert.True(approval.Succeeded);
        var updated = await database.Context.FacultyStaff.FindAsync(faculty.Id);
        Assert.Equal(ClearanceStatus.Cleared, updated!.ClearanceStatus);
        Assert.Contains(history, item =>
            item.MemberType == MemberType.FacultyStaff &&
            item.MemberId == faculty.Id &&
            item.Action == "Clearance Approved");
    }

    private static ClearanceService CreateService(
        SqliteTestDatabase database,
        User? currentUser)
    {
        return new ClearanceService(
            database.Context,
            new FakeAuthenticationService(currentUser),
            [new PdfReportExporter()]);
    }

    private static async Task<Fine> AddFineAsync(
        SqliteTestDatabase database,
        TestLibraryData data,
        FineStatus status,
        decimal remainingAmount)
    {
        var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-2));
        database.Context.ReceiveRecords.Add(new ReceiveRecord
        {
            IssueRecordId = issue.Id,
            ReceiveDate = DateTime.UtcNow,
            ReceivedByUserId = data.User.Id
        });
        var fine = new Fine
        {
            FineRecordNumber = $"FINE-{Guid.NewGuid():N}",
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueRecordId = issue.Id,
            AccessionNumber = data.Copy.AccessionNumber,
            FineAmount = 50,
            PaidAmount = 50 - remainingAmount,
            RemainingAmount = remainingAmount,
            PaymentStatus = status
        };
        database.Context.Fines.Add(fine);
        data.Copy.AvailabilityStatus = BookStatus.Available;
        await database.Context.SaveChangesAsync();
        return fine;
    }

    private static async Task<FacultyStaff> AddFacultyAsync(SqliteTestDatabase database)
    {
        var faculty = new FacultyStaff
        {
            PersonnelNumber = $"FAC-{Guid.NewGuid():N}",
            Name = "Test Faculty",
            FacultyType = FacultyType.PermanentFaculty,
            Department = "CS",
            Designation = "Lecturer",
            Email = "faculty@test.invalid",
            Phone = "000",
            Address = "Test"
        };
        database.Context.FacultyStaff.Add(faculty);
        await database.Context.SaveChangesAsync();
        return faculty;
    }

    private sealed class FakeAuthenticationService(User? currentUser)
        : IAuthenticationService
    {
        public User? CurrentUser { get; } = currentUser;
        public Task<User?> LoginAsync(string username, string password) =>
            Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) =>
            Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) =>
            Task.FromResult(true);
        public Task<(bool Success, string Message)> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult((true, ""));
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "KicsitLibrary.Tests",
                $"Clearance-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}


