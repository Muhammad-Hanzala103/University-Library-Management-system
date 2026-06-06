using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data;
using KicsitLibrary.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests.Infrastructure;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _databasePath;

    private SqliteTestDatabase(string databasePath, KicsitLibraryDbContext context)
    {
        _databasePath = databasePath;
        Context = context;
    }

    public KicsitLibraryDbContext Context { get; }

    public static async Task<SqliteTestDatabase> CreateAsync(bool seed = false)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            "KicsitLibrary.Tests",
            $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var options = new DbContextOptionsBuilder<KicsitLibraryDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        var context = new KicsitLibraryDbContext(options);
        await context.Database.EnsureCreatedAsync();

        if (seed)
        {
            await DbSeeder.SeedAsync(context, new PasswordHasher());
        }

        return new SqliteTestDatabase(databasePath, context);
    }

    public async Task<TestLibraryData> AddCirculationDataAsync()
    {
        var user = new User
        {
            Username = $"tester-{Guid.NewGuid():N}",
            PasswordHash = "test-only",
            FullName = "Test Librarian",
            Email = "librarian@test.invalid"
        };
        var student = new Student
        {
            RegistrationNumber = $"REG-{Guid.NewGuid():N}",
            AdmissionNumber = "ADM-1",
            RollNumber = "ROLL-1",
            Name = "Test Student",
            FatherName = "Test Parent",
            Program = "BSCS",
            Department = "CS",
            Batch = "2026",
            Semester = "1",
            Session = "2026-2030",
            Email = "student@test.invalid",
            Phone = "0000000000",
            Address = "Test Address",
            PageNumber = 1,
            RegisterNumber = 1
        };
        var publisher = new Publisher { Name = "Test Publisher" };
        var category = new Category { Name = "Test Category" };
        var department = new DepartmentCategory { Name = "Test Department" };
        var literature = new LiteratureCategory { Name = "Test Literature" };

        Context.AddRange(user, student, publisher, category, department, literature);
        await Context.SaveChangesAsync();

        var book = new BookMaster
        {
            Title = "Test Book",
            UniqueTitleNumber = $"TITLE-{Guid.NewGuid():N}",
            PublisherId = publisher.Id,
            CategoryId = category.Id,
            DepartmentCategoryId = department.Id,
            LiteratureCategoryId = literature.Id,
            PublicationYear = 2026,
            PurchasePrice = 1000,
            Status = BookStatus.Available
        };
        Context.BookMasters.Add(book);
        await Context.SaveChangesAsync();

        var copy = new BookCopy
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            BookMasterId = book.Id,
            CopyNumber = 1,
            AvailabilityStatus = BookStatus.Available
        };
        Context.BookCopies.Add(copy);
        Context.SystemSettings.AddRange(
            new SystemSettings { Key = "StudentIssueLimit", Value = "3", Group = "Borrowing" },
            new SystemSettings { Key = "DefaultIssueDays", Value = "14", Group = "Borrowing" },
            new SystemSettings { Key = "FinePerDay", Value = "10", Group = "Billing" },
            new SystemSettings { Key = "NotificationCooldownHours", Value = "24", Group = "Notifications" },
            new SystemSettings { Key = "MaxNotificationRetryCount", Value = "3", Group = "Notifications" });
        await Context.SaveChangesAsync();

        return new TestLibraryData(user, student, book, copy);
    }

    public async Task<BookCopy> AddCopyAsync(TestLibraryData data)
    {
        var copy = new BookCopy
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            BookMasterId = data.Book.Id,
            CopyNumber = await Context.BookCopies.CountAsync() + 1,
            AvailabilityStatus = BookStatus.Issued
        };
        Context.BookCopies.Add(copy);
        await Context.SaveChangesAsync();
        return copy;
    }

    public async Task<IssueRecord> AddIssueAsync(
        TestLibraryData data,
        DateTime expectedReturnDateUtc,
        BookCopy? copy = null)
    {
        copy ??= data.Copy;
        copy.AvailabilityStatus = BookStatus.Issued;
        var issue = new IssueRecord
        {
            AccessionNumber = copy.AccessionNumber,
            BookCopyId = copy.Id,
            MemberType = MemberType.Student,
            StudentId = data.Student.Id,
            IssueDate = expectedReturnDateUtc.AddDays(-14),
            ExpectedReturnDate = expectedReturnDateUtc,
            FinePerDay = 10,
            IssuedByUserId = data.User.Id
        };
        Context.IssueRecords.Add(issue);
        await Context.SaveChangesAsync();
        return issue;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

internal sealed record TestLibraryData(
    User User,
    Student Student,
    BookMaster Book,
    BookCopy Copy);
