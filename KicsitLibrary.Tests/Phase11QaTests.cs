using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Services.Catalog;
using KicsitLibrary.Services.Circulation;
using KicsitLibrary.Services.Consumer;
using KicsitLibrary.Services.Dashboard;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Tests.Infrastructure;
using KicsitLibrary.Data.Repositories;

namespace KicsitLibrary.Tests
{
    public class Phase11QaTests
    {
        [Fact]
        public async Task Author_Search_CaseInsensitive()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddAuthorAsync(new Author { Name = "John Doe", Biography = "Bio" });
            await service.AddAuthorAsync(new Author { Name = "Jane Smith", Biography = "Bio" });

            var results = await service.SearchAuthorsAsync("john");
            Assert.Single(results);
            Assert.Equal("John Doe", results.First().Name);
        }

        [Fact]
        public async Task Author_Duplicate_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddAuthorAsync(new Author { Name = "John Doe" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddAuthorAsync(new Author { Name = "john doe" }));
        }

        [Fact]
        public async Task Author_LinkedDelete_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            var author = new Author { Name = "Linked Author" };
            await service.AddAuthorAsync(author);

            var publisher = new Publisher { Name = "Publisher" };
            database.Context.Publishers.Add(publisher);

            var category = new Category { Name = "Category" };
            database.Context.Categories.Add(category);

            var dept = new DepartmentCategory { Name = "Dept" };
            database.Context.DepartmentCategories.Add(dept);

            var lit = new LiteratureCategory { Name = "Lit" };
            database.Context.LiteratureCategories.Add(lit);
            await database.Context.SaveChangesAsync();

            var book = new BookMaster
            {
                Title = "Some Book",
                UniqueTitleNumber = "UNIQUE-1",
                PublisherId = publisher.Id,
                CategoryId = category.Id,
                DepartmentCategoryId = dept.Id,
                LiteratureCategoryId = lit.Id
            };
            database.Context.BookMasters.Add(book);
            await database.Context.SaveChangesAsync();

            var bookAuthor = new BookAuthor { BookMasterId = book.Id, AuthorId = author.Id };
            database.Context.BookAuthors.Add(bookAuthor);
            await database.Context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteAuthorAsync(author.Id, "Reason", 1));
        }

        [Fact]
        public async Task Publisher_Search_CaseInsensitive()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddPublisherAsync(new Publisher { Name = "O'Reilly Media" });
            await service.AddPublisherAsync(new Publisher { Name = "Packt Publishing" });

            var results = await service.SearchPublishersAsync("o'reilly");
            Assert.Single(results);
            Assert.Equal("O'Reilly Media", results.First().Name);
        }

        [Fact]
        public async Task Publisher_Duplicate_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddPublisherAsync(new Publisher { Name = "Packt" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddPublisherAsync(new Publisher { Name = "packt" }));
        }

        [Fact]
        public async Task Category_Sorting_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddCategoryAsync(new Category { Name = "Zebra Category" });
            await service.AddCategoryAsync(new Category { Name = "Alpha Category" });
            await service.AddCategoryAsync(new Category { Name = "Beta Category" });

            var results = await service.GetAllCategoriesAsync();
            var list = results.ToList();
            Assert.Equal("Alpha Category", list[0].Name);
            Assert.Equal("Beta Category", list[1].Name);
            Assert.Equal("Zebra Category", list[2].Name);
        }

        [Fact]
        public async Task Category_LinkedDelete_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            var category = new Category { Name = "Linked Category" };
            await service.AddCategoryAsync(category);

            var publisher = new Publisher { Name = "Pub" };
            database.Context.Publishers.Add(publisher);

            var dept = new DepartmentCategory { Name = "Dept" };
            database.Context.DepartmentCategories.Add(dept);

            var lit = new LiteratureCategory { Name = "Lit" };
            database.Context.LiteratureCategories.Add(lit);
            await database.Context.SaveChangesAsync();

            var book = new BookMaster
            {
                Title = "Some Book",
                UniqueTitleNumber = "UNIQUE-2",
                PublisherId = publisher.Id,
                CategoryId = category.Id,
                DepartmentCategoryId = dept.Id,
                LiteratureCategoryId = lit.Id
            };
            database.Context.BookMasters.Add(book);
            await database.Context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteCategoryAsync(category.Id, "Reason", 1));
        }

        [Fact]
        public async Task Department_Add_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            var dept = new DepartmentCategory { Name = "Civil Engineering", Description = "Civil Dept" };
            await service.AddDepartmentCategoryAsync(dept);

            var loaded = await database.Context.DepartmentCategories.FindAsync(dept.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Civil Engineering", loaded.Name);
        }

        [Fact]
        public async Task Department_Duplicate_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddDepartmentCategoryAsync(new DepartmentCategory { Name = "Electrical" });

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddDepartmentCategoryAsync(new DepartmentCategory { Name = "electrical" }));
        }

        [Fact]
        public async Task Department_Search_CaseInsensitive()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddDepartmentCategoryAsync(new DepartmentCategory { Name = "Mechanical Engineering" });
            await service.AddDepartmentCategoryAsync(new DepartmentCategory { Name = "Software Engineering" });

            var results = await service.SearchDepartmentCategoriesAsync("mechanical");
            Assert.Single(results);
            Assert.Equal("Mechanical Engineering", results.First().Name);
        }

        [Fact]
        public async Task Student_DuplicateEmail_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new ConsumerService(database.Context);

            var s1 = new Student
            {
                RegistrationNumber = "2026-CS-01",
                Name = "First",
                RollNumber = "01",
                Email = "duplicate@test.com"
            };
            await service.AddStudentAsync(s1);

            var s2 = new Student
            {
                RegistrationNumber = "2026-CS-02",
                Name = "Second",
                RollNumber = "02",
                Email = "duplicate@test.com"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddStudentAsync(s2));
        }

        [Fact]
        public void Student_PhoneValidation_Works()
        {
            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^\+?[0-9\s\-]{7,15}$");
            Assert.False(phoneRegex.IsMatch("abc-123"));
            Assert.True(phoneRegex.IsMatch("+92-300-1234567"));
            Assert.True(phoneRegex.IsMatch("03001234567"));
        }

        [Fact]
        public void Student_EmailValidation_Works()
        {
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            Assert.False(emailRegex.IsMatch("invalid-email"));
            Assert.True(emailRegex.IsMatch("valid.student@university.edu"));
        }

        [Fact]
        public async Task Student_SortingByRegistrationNumber_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new ConsumerService(database.Context);

            await service.AddStudentAsync(new Student { RegistrationNumber = "2026-CS-99", Name = "Zac", RollNumber = "99" });
            await service.AddStudentAsync(new Student { RegistrationNumber = "2026-CS-01", Name = "Abe", RollNumber = "01" });

            var students = await service.GetAllStudentsAsync();
            var list = students.ToList();
            Assert.Equal("2026-CS-01", list[0].RegistrationNumber);
            Assert.Equal("2026-CS-99", list[1].RegistrationNumber);
        }

        [Fact]
        public async Task BookLocation_SaveAndLoad_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            var rack = new Rack { Name = "Rack A" };
            await service.AddRackAsync(rack);

            var shelf = new Shelf { Name = "Shelf 1", RackId = rack.Id };
            await service.AddShelfAsync(shelf);

            var data = await database.AddCirculationDataAsync();
            var copy = new BookCopy
            {
                AccessionNumber = "LOC-1",
                BookMasterId = data.Book.Id,
                CopyNumber = 3,
                RackNumber = "Rack A",
                ShelfNumber = "Shelf 1",
                RowNumber = "3",
                AvailabilityStatus = BookStatus.Available
            };

            await service.AddCopyAsync(copy);

            var loaded = await database.Context.BookCopies.FirstOrDefaultAsync(bc => bc.AccessionNumber == "LOC-1");
            Assert.NotNull(loaded);
            Assert.Equal("Rack A", loaded.RackNumber);
            Assert.Equal("Shelf 1", loaded.ShelfNumber);
        }

        [Fact]
        public async Task DuplicateRackOrShelfLocation_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            var rack = new Rack { Name = "Rack B" };
            await service.AddRackAsync(rack);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddRackAsync(new Rack { Name = "rack b" }));
        }

        [Fact]
        public async Task BulkRackGeneration_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var service = new CatalogService(database.Context);

            await service.AddRackAsync(new Rack { Name = "Bulk-1" });
            Assert.True(await database.Context.Racks.AnyAsync(r => r.Name == "Bulk-1"));
        }

        [Fact]
        public async Task DuplicateAccession_Blocked()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var service = new CatalogService(database.Context);

            var copy = new BookCopy
            {
                AccessionNumber = data.Copy.AccessionNumber,
                BookMasterId = data.Book.Id,
                CopyNumber = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddCopyAsync(copy));
        }

        [Fact]
        public async Task Overdue_DepartmentFilter_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var log = new ActivityLogService(new Repository<ActivityLog>(database.Context));
            var overdueService = new OverdueService(
                database.Context,
                new NotificationService(database.Context, log, new KicsitLibrary.Tests.Infrastructure.FakeEmailTransport(), new EmailSettingsService(database.Context)),
                log
            );

            await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-5));

            var items = await overdueService.GetOverdueItemsAsync();
            Assert.Single(items);
            Assert.Equal("CS", items.First().Department);
        }

        [Fact]
        public async Task Overdue_DateFilter_Works()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var log = new ActivityLogService(new Repository<ActivityLog>(database.Context));
            var overdueService = new OverdueService(
                database.Context,
                new NotificationService(database.Context, log, new KicsitLibrary.Tests.Infrastructure.FakeEmailTransport(), new EmailSettingsService(database.Context)),
                log
            );

            await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-10));

            var items = await overdueService.GetOverdueItemsAsync();
            Assert.Single(items);
        }

        [Fact]
        public async Task Overdue_ExcludesReturnedIssue()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var log = new ActivityLogService(new Repository<ActivityLog>(database.Context));
            var overdueService = new OverdueService(
                database.Context,
                new NotificationService(database.Context, log, new KicsitLibrary.Tests.Infrastructure.FakeEmailTransport(), new EmailSettingsService(database.Context)),
                log
            );

            var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-5));

            var circulation = new CirculationService(database.Context, new ActivityLogService(new Repository<ActivityLog>(database.Context)));
            await circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

            var items = await overdueService.GetOverdueItemsAsync();
            
            Assert.Single(items);
            Assert.True(items.First().IsReturned);
        }

        [Fact]
        public async Task Return_PayNow_MarksFineResolved()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var circulation = new CirculationService(database.Context, new ActivityLogService(new Repository<ActivityLog>(database.Context)));

            var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-5));

            await circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 50, null, null, data.User.Id);

            var fine = await database.Context.Fines.FirstOrDefaultAsync(f => f.IssueRecordId == issue.Id);
            Assert.NotNull(fine);
            Assert.Equal(FineStatus.Paid, fine.PaymentStatus);
            Assert.Equal(0, fine.RemainingAmount);
        }

        [Fact]
        public async Task Return_PayLater_KeepsFinePending()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var circulation = new CirculationService(database.Context, new ActivityLogService(new Repository<ActivityLog>(database.Context)));

            var issue = await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-5));

            await circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

            var fine = await database.Context.Fines.FirstOrDefaultAsync(f => f.IssueRecordId == issue.Id);
            Assert.NotNull(fine);
            Assert.Equal(FineStatus.Partial, fine.PaymentStatus);
            Assert.Equal(50, fine.RemainingAmount);
        }

        [Fact]
        public async Task Return_UpdatesCopyStatusAvailable()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var circulation = new CirculationService(database.Context, new ActivityLogService(new Repository<ActivityLog>(database.Context)));

            await database.AddIssueAsync(data, DateTime.UtcNow.AddDays(-5));
            await circulation.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);

            await database.Context.Entry(data.Copy).ReloadAsync();
            Assert.Equal(BookStatus.Available, data.Copy.AvailabilityStatus);
        }

        [Fact]
        public async Task Dashboard_CatalogCounts_AreCorrect()
        {
            await using var database = await SqliteTestDatabase.CreateAsync();
            var data = await database.AddCirculationDataAsync();
            var service = new DashboardService(database.Context);

            var stats = await service.GetDashboardStatsAsync();
            Assert.Equal(1, stats.TotalUniqueTitles);
            Assert.Equal(1, stats.TotalAccessionCopies);
        }
    }
}
