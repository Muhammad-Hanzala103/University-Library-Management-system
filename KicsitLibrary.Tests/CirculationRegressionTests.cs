using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Catalog;
using KicsitLibrary.Services.Circulation;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class CirculationRegressionTests
{
    [Fact]
    public async Task BookCopy_CannotBeIssuedTwiceWhileActiveIssueExists()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateCirculationService(database);

        await service.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id));

        Assert.Contains("not available", exception.Message);
        Assert.Equal(1, await database.Context.IssueRecords.CountAsync());
    }

    [Fact]
    public async Task ReturnedBook_BecomesAvailableAgain()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = CreateCirculationService(database);

        await service.IssueBookAsync(data.Copy.AccessionNumber, data.Student.Id, MemberType.Student, data.User.Id);
        await service.ReceiveBookAsync(data.Copy.AccessionNumber, "Normal", 0, null, null, data.User.Id);
        await database.Context.Entry(data.Copy).ReloadAsync();

        Assert.Equal(BookStatus.Available, data.Copy.AvailabilityStatus);
        Assert.Equal(1, await database.Context.ReceiveRecords.CountAsync());
    }

    [Fact]
    public async Task DuplicateAccessionNumber_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        var service = new CatalogService(database.Context);

        var duplicate = new BookCopy
        {
            AccessionNumber = data.Copy.AccessionNumber,
            BookMasterId = data.Book.Id,
            CopyNumber = 2,
            AvailabilityStatus = BookStatus.Available
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddCopyAsync(duplicate));

        Assert.Contains("Duplicate Accession Number", exception.Message);
    }

    private static CirculationService CreateCirculationService(SqliteTestDatabase database)
    {
        var repository = new Repository<ActivityLog>(database.Context);
        return new CirculationService(database.Context, new ActivityLogService(repository));
    }
}
