using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Circulation;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Services.Reservations;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class ReservationWorkflowTests
{
    [Fact]
    public async Task ActiveUnclearedMember_IsEligibleForReservation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();

        var result = await CreateServices(database, data.User).Reservation
            .CheckReservationEligibilityAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.True(result.IsEligible);
        Assert.Equal(1, result.AvailableCopyCount);
        Assert.True(result.DirectIssueAvailable);
    }

    [Fact]
    public async Task InactiveMember_CannotReserve()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        data.Student.ActiveStatus = false;
        await database.Context.SaveChangesAsync();

        var result = await CreateServices(database, data.User).Reservation
            .CreateReservationAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.False(result.Succeeded);
        Assert.Contains("inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearedMember_CannotReserve()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        data.Student.ClearanceStatus = ClearanceStatus.Cleared;
        await database.Context.SaveChangesAsync();

        var result = await CreateServices(database, data.User).Reservation
            .CheckReservationEligibilityAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.False(result.IsEligible);
        Assert.Contains("cleared", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateActiveReservation_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateServices(database, data.User).Reservation;

        Assert.True((await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student)).Succeeded);
        var duplicate = await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.False(duplicate.Succeeded);
        Assert.Contains("already", duplicate.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await database.Context.Reservations.CountAsync());
    }

    [Fact]
    public async Task MemberWithActiveIssueForTitle_CannotReserve()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(10));

        var result = await CreateServices(database, data.User).Reservation
            .CheckReservationEligibilityAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.False(result.IsEligible);
        Assert.True(result.HasActiveIssueForTitle);
    }

    [Fact]
    public async Task MemberWithUnpaidFine_CannotReserve()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-2));
        database.Context.ReceiveRecords.Add(new ReceiveRecord
        {
            IssueRecordId = issue.Id,
            ReceiveDate = DateTime.UtcNow,
            ReceivedByUserId = data.User.Id
        });
        database.Context.Fines.Add(new Fine
        {
            FineRecordNumber = $"FINE-{Guid.NewGuid():N}",
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueRecordId = issue.Id,
            AccessionNumber = data.Copy.AccessionNumber,
            FineAmount = 25,
            RemainingAmount = 25,
            PaymentStatus = FineStatus.Unpaid
        });
        data.Copy.AvailabilityStatus = BookStatus.Available;
        await database.Context.SaveChangesAsync();

        var result = await CreateServices(database, data.User).Reservation
            .CheckReservationEligibilityAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.False(result.IsEligible);
        Assert.Equal(25, result.PendingFineAmount);
    }

    [Fact]
    public async Task ReservationQueue_IsFirstComeFirstServed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var secondStudent = await AddStudentAsync(database, "Second Student");
        var service = CreateServices(database, data.User).Reservation;
        await service.CreateReservationAsync(data.Book.Id, data.Student.Id, MemberType.Student);
        await Task.Delay(10);
        await service.CreateReservationAsync(data.Book.Id, secondStudent.Id, MemberType.Student);

        var queue = await service.GetReservationQueueAsync(data.Book.Id);

        Assert.Equal(2, queue.Count);
        Assert.Equal(data.Student.Id, queue[0].MemberId);
        Assert.Equal(1, queue[0].QueuePosition);
        Assert.Equal(secondStudent.Id, queue[1].MemberId);
        Assert.Equal(2, queue[1].QueuePosition);
    }

    [Fact]
    public async Task ReservationExpiry_UsesConfiguredDays()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.SetSystemSettingAsync("ReservationExpiryDays", "5", "Reservation");
        var before = DateTime.UtcNow;

        var result = await CreateServices(database, data.User).Reservation
            .CreateReservationAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        Assert.True(result.Succeeded);
        Assert.InRange(result.Reservation!.ExpiryDate, before.AddDays(5), DateTime.UtcNow.AddDays(5));
    }

    [Fact]
    public async Task CancelReservation_StoresReasonAndActivityLog()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateServices(database, data.User).Reservation;
        var created = await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);

        var result = await service.CancelReservationAsync(
            created.Reservation!.Id, "Member no longer needs the title");

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStatus.Cancelled, result.Reservation!.Status);
        Assert.Contains("no longer", result.Reservation.Remarks);
        Assert.Contains(await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Reservation Cancelled");
    }

    [Fact]
    public async Task ExpireOldReservations_ExpiresOnlyPastActiveRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var secondStudent = await AddStudentAsync(database, "Second Student");
        var service = CreateServices(database, data.User).Reservation;
        var old = await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);
        var future = await service.CreateReservationAsync(
            data.Book.Id, secondStudent.Id, MemberType.Student);
        old.Reservation!.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        future.Reservation!.ExpiryDate = DateTime.UtcNow.AddDays(2);
        await database.Context.SaveChangesAsync();

        var count = await service.ExpireOldReservationsAsync();

        Assert.Equal(1, count);
        Assert.Equal(ReservationStatus.Expired,
            (await database.Context.Reservations.FindAsync(old.Reservation.Id))!.Status);
        Assert.Equal(ReservationStatus.Pending,
            (await database.Context.Reservations.FindAsync(future.Reservation.Id))!.Status);
    }

    [Fact]
    public async Task MarkAvailable_UpdatesOnlyFirstQueueReservation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var secondStudent = await AddStudentAsync(database, "Second Student");
        var service = CreateServices(database, data.User).Reservation;
        var first = await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);
        var second = await service.CreateReservationAsync(
            data.Book.Id, secondStudent.Id, MemberType.Student);

        var result = await service.MarkReservationAvailableAsync(data.Book.Id);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(ReservationStatus.Available,
            (await database.Context.Reservations.FindAsync(first.Reservation!.Id))!.Status);
        Assert.Equal(ReservationStatus.Pending,
            (await database.Context.Reservations.FindAsync(second.Reservation!.Id))!.Status);
    }

    [Fact]
    public async Task Availability_CreatesInAppAndEmailRecordsWithoutSending()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var services = CreateServices(database, data.User);
        var created = await services.Reservation.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);

        var result = await services.Reservation.MarkReservationAvailableAsync(data.Book.Id);
        var notifications = await database.Context.NotificationRecords.ToListAsync();

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(2, result.NotificationRecordsCreated);
        Assert.Contains(notifications, item => item.Channel == "InApp");
        Assert.Contains(notifications, item => item.Channel == "Email");
        Assert.All(notifications, item =>
            Assert.Equal(NotificationType.ReservationAvailableReminder, item.NotificationType));
        Assert.Equal(0, services.Transport.SendCount);
        Assert.Equal(created.Reservation!.Id, result.Reservation!.Id);
    }

    [Fact]
    public async Task Availability_MissingEmailCreatesClearFailedRecord()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        data.Student.Email = string.Empty;
        await database.Context.SaveChangesAsync();
        var service = CreateServices(database, data.User).Reservation;
        await service.CreateReservationAsync(data.Book.Id, data.Student.Id, MemberType.Student);

        await service.MarkReservationAvailableAsync(data.Book.Id);
        var email = await database.Context.NotificationRecords
            .SingleAsync(item => item.Channel == "Email");

        Assert.Equal(NotificationStatus.Failed, email.Status);
        Assert.Equal("Recipient email is missing.", email.FailureReason);
    }

    [Fact]
    public async Task ReservationAvailabilityNotifications_AreDeduplicated()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var services = CreateServices(database, data.User);
        var created = await services.Reservation.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);
        await database.Context.Entry(created.Reservation!).Reference(item => item.BookMaster).LoadAsync();
        await database.Context.Entry(created.Reservation!).Reference(item => item.Student).LoadAsync();

        var first = await services.Notification.CreateReservationAvailableNotificationsAsync(
            created.Reservation!, data.User.Id);
        var second = await services.Notification.CreateReservationAvailableNotificationsAsync(
            created.Reservation!, data.User.Id);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, await database.Context.NotificationRecords.CountAsync());
    }

    [Fact]
    public async Task FulfillReservation_IssuesAvailableCopyAndCompletesReservation()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateServices(database, data.User).Reservation;
        var created = await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student);

        var result = await service.FulfillReservationAsync(created.Reservation!.Id);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.IssueRecord);
        Assert.Equal(ReservationStatus.Issued, result.Reservation!.Status);
        Assert.Equal(data.Copy.AccessionNumber, result.Reservation.AccessionNumber);
        Assert.Equal(BookStatus.Issued, result.AssignedCopy!.AvailabilityStatus);
        Assert.Contains(await database.Context.ActivityLogs.ToListAsync(),
            log => log.Action == "Reservation Fulfilled");
    }

    [Fact]
    public async Task FulfillReservation_DoesNotSkipFirstQueueMember()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var secondStudent = await AddStudentAsync(database, "Second Student");
        var service = CreateServices(database, data.User).Reservation;
        await service.CreateReservationAsync(data.Book.Id, data.Student.Id, MemberType.Student);
        var second = await service.CreateReservationAsync(
            data.Book.Id, secondStudent.Id, MemberType.Student);

        var result = await service.FulfillReservationAsync(second.Reservation!.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("first", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await database.Context.IssueRecords.ToListAsync());
        Assert.Equal(BookStatus.Available, data.Copy.AvailabilityStatus);
    }

    [Fact]
    public async Task ReturnedCopy_MarksFirstReservationAvailableWithoutAutoIssue()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var reserver = await AddStudentAsync(database, "Waiting Student");
        var services = CreateServices(database, data.User);
        await services.Reservation.CreateReservationAsync(
            data.Book.Id, reserver.Id, MemberType.Student);
        await services.Circulation.IssueBookAsync(
            data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);

        await services.Circulation.ReceiveBookAsync(
            data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);
        var availability = await services.Reservation.MarkReservationAvailableAsync(data.Book.Id);

        Assert.True(availability.Succeeded, availability.ErrorMessage);
        Assert.Equal(ReservationStatus.Available, availability.Reservation!.Status);
        Assert.Equal(1, await database.Context.IssueRecords.CountAsync());
        Assert.Equal(1, await database.Context.ReceiveRecords.CountAsync());
    }

    [Fact]
    public async Task ReservationQueries_FilterAndReturnMemberHistory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateServices(database, data.User).Reservation;
        await service.CreateReservationAsync(
            data.Book.Id, data.Student.Id, MemberType.Student, "Course reading");

        var filtered = await service.GetReservationsAsync(
            data.Student.RegistrationNumber,
            ReservationStatus.Pending,
            MemberType.Student);
        var history = await service.GetMemberReservationsAsync(
            data.Student.Id,
            MemberType.Student);

        Assert.Single(filtered);
        Assert.Single(history);
        Assert.Equal("Course reading", history[0].Remarks);
    }

    [Fact]
    public async Task BookReturned_WithActiveReservation_SendsOrQueuesNotification()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(enabled: true);

        var nextStudent = await AddStudentAsync(database, "Next Queue Member");
        var services = CreateServices(database, data.User);

        // 1. Create a reservation for next student
        await services.Reservation.CreateReservationAsync(data.Book.Id, nextStudent.Id, MemberType.Student);

        // 2. Issue book to current student
        await services.Circulation.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);

        // 3. Return the book, which should trigger marking reservation available and sending notification
        await services.Circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

        var dbReservation = await database.Context.Reservations.SingleAsync(r => r.StudentId == nextStudent.Id);
        Assert.Equal(ReservationStatus.Available, dbReservation.Status);

        var notifications = await database.Context.NotificationRecords.Where(n => n.StudentId == nextStudent.Id).ToListAsync();
        Assert.Contains(notifications, n => n.Channel == "InApp");
        var emailNotification = notifications.Single(n => n.Channel == "Email");
        Assert.Equal(NotificationStatus.Sent, emailNotification.Status);
        Assert.Equal(1, services.Transport.SendCount);
    }

    [Fact]
    public async Task BookReturned_WithActiveReservation_MissingEmail_Handled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(enabled: true);

        var nextStudent = await AddStudentAsync(database, "Next Queue Member");
        nextStudent.Email = ""; // Missing email
        await database.Context.SaveChangesAsync();

        var services = CreateServices(database, data.User);

        await services.Reservation.CreateReservationAsync(data.Book.Id, nextStudent.Id, MemberType.Student);
        await services.Circulation.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);
        await services.Circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

        var emailNotification = await database.Context.NotificationRecords.SingleAsync(n => n.StudentId == nextStudent.Id && n.Channel == "Email");
        Assert.Equal(NotificationStatus.Failed, emailNotification.Status);
        Assert.Equal("Recipient email is missing.", emailNotification.FailureReason);
        Assert.Equal(0, services.Transport.SendCount);
    }

    [Fact]
    public async Task BookReturned_WithActiveReservation_SmtpDisabled_Handled()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.ConfigureValidEmailSettingsAsync(enabled: false); // SMTP disabled

        var nextStudent = await AddStudentAsync(database, "Next Queue Member");
        var services = CreateServices(database, data.User);

        await services.Reservation.CreateReservationAsync(data.Book.Id, nextStudent.Id, MemberType.Student);
        await services.Circulation.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);
        await services.Circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

        var emailNotification = await database.Context.NotificationRecords.SingleAsync(n => n.StudentId == nextStudent.Id && n.Channel == "Email");
        Assert.Equal(NotificationStatus.Pending, emailNotification.Status);
        Assert.Contains("disabled", emailNotification.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, services.Transport.SendCount);
    }

    [Fact]
    public async Task BookReturned_ReservationQueueSelectsCorrectNextPerson()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();

        var studentA = await AddStudentAsync(database, "Student A");
        var studentB = await AddStudentAsync(database, "Student B");

        var services = CreateServices(database, data.User);

        // First reserve: Student A, then Student B
        await services.Reservation.CreateReservationAsync(data.Book.Id, studentA.Id, MemberType.Student);
        await Task.Delay(10);
        await services.Reservation.CreateReservationAsync(data.Book.Id, studentB.Id, MemberType.Student);

        await services.Circulation.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);
        await services.Circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

        var resA = await database.Context.Reservations.SingleAsync(r => r.StudentId == studentA.Id);
        var resB = await database.Context.Reservations.SingleAsync(r => r.StudentId == studentB.Id);

        // Student A was first, so they should be Available. Student B should remain Pending.
        Assert.Equal(ReservationStatus.Available, resA.Status);
        Assert.Equal(ReservationStatus.Pending, resB.Status);
    }

    private static ReservationTestServices CreateServices(
        SqliteTestDatabase database,
        User currentUser)
    {
        var log = new ActivityLogService(new Repository<ActivityLog>(database.Context));
        var transport = new FakeEmailTransport();
        var notification = new NotificationService(
            database.Context,
            log,
            transport,
            new EmailSettingsService(database.Context));
        var circulation = new CirculationService(database.Context, log, notification);
        var reservation = new ReservationService(
            database.Context,
            new FakeAuthenticationService(currentUser),
            circulation,
            notification);
        return new ReservationTestServices(reservation, circulation, notification, transport);
    }

    private static async Task<Student> AddStudentAsync(
        SqliteTestDatabase database,
        string name)
    {
        var student = new Student
        {
            RegistrationNumber = new string(Guid.NewGuid().ToString("N").Where(char.IsDigit).Take(10).ToArray()),
            AdmissionNumber = $"ADM-{Guid.NewGuid():N}",
            RollNumber = $"ROLL-{Guid.NewGuid():N}",
            Name = name,
            FatherName = "Parent",
            Program = "BSCS",
            Department = "CS",
            Batch = "2026",
            Semester = "1",
            Session = "2026-2030",
            Email = $"{Guid.NewGuid():N}@test.invalid",
            Phone = "000",
            Address = "Test",
            PageNumber = 1,
            RegisterNumber = 1
        };
        database.Context.Students.Add(student);
        await database.Context.SaveChangesAsync();
        return student;
    }

    private sealed record ReservationTestServices(
        ReservationService Reservation,
        CirculationService Circulation,
        NotificationService Notification,
        FakeEmailTransport Transport);

    private sealed class FakeAuthenticationService(User currentUser)
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
}


