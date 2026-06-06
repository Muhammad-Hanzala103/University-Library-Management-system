using KicsitLibrary.Core.Entities;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Services.Dashboard;
using KicsitLibrary.Tests.Infrastructure;

namespace KicsitLibrary.Tests;

public class DashboardOverdueTests
{
    [Fact]
    public async Task DashboardOverdueCount_UsesActiveIssueDueDateLogic()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var data = await database.AddCirculationDataAsync();
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(-2)));

        var futureCopy = await database.AddCopyAsync(data);
        await database.AddIssueAsync(data, LocalDateToUtc(DateTime.Now.Date.AddDays(2)), futureCopy);

        var returnedCopy = await database.AddCopyAsync(data);
        var returnedIssue = await database.AddIssueAsync(
            data,
            LocalDateToUtc(DateTime.Now.Date.AddDays(-5)),
            returnedCopy);
        database.Context.ReceiveRecords.Add(new ReceiveRecord
        {
            IssueRecordId = returnedIssue.Id,
            ReceiveDate = DateTime.UtcNow,
            ReceivedByUserId = data.User.Id,
            BookConditionAfterReturn = "Normal"
        });
        await database.Context.SaveChangesAsync();

        var service = new DashboardService(database.Context);
        var stats = await service.GetDashboardStatsAsync();

        Assert.Equal(1, stats.OverdueBooks);
    }

    private static DateTime LocalDateToUtc(DateTime localDate)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(12), DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
    }
}
