using KicsitLibrary.Core.Entities;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Models;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Services.Inventory;
using KicsitLibrary.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KicsitLibrary.Tests;

public class InventoryStockVerificationWorkflowTests
{
    [Fact] public async Task InventoryItem_CanBeCreatedWithValidData()
    {
        await using var db = await SqliteTestDatabase.CreateAsync(); var data = await db.AddCirculationDataAsync();
        var result = await Inventory(db, Admin(data.User)).CreateInventoryItemAsync(Item());
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Equal(1, await db.Context.InventoryItems.CountAsync());
    }

    [Fact] public async Task InventoryItem_RejectsNegativeQuantity()
    {
        await using var db = await SqliteTestDatabase.CreateAsync(); var data = await db.AddCirculationDataAsync();
        var item = Item(); item.Quantity = -1;
        var result = await Inventory(db, Admin(data.User)).CreateInventoryItemAsync(item);
        Assert.False(result.Succeeded); Assert.Contains("negative", result.ErrorMessage);
    }

    [Fact] public async Task InventoryAdjustment_RequiresReason()
    {
        var setup = await CreateInventoryAsync();
        await using var db = setup.Db;
        var result = await setup.Service.AdjustInventoryQuantityAsync(setup.Id, new() { Quantity = 10, AvailableQuantity = 8, DamagedQuantity = 2 });
        Assert.False(result.Succeeded); Assert.Contains("reason", result.ErrorMessage);
    }

    [Fact] public async Task InventoryAdjustment_UpdatesTotals()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        var result = await setup.Service.AdjustInventoryQuantityAsync(setup.Id, new() { Quantity = 12, AvailableQuantity = 9, DamagedQuantity = 3, Reason = "Annual count" });
        Assert.True(result.Succeeded); Assert.Equal((12, 9, 3), (result.InventoryItem!.Quantity, result.InventoryItem.AvailableQuantity, result.InventoryItem.DamagedQuantity));
    }

    [Fact] public async Task MarkDamaged_RequiresReason()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        var result = await setup.Service.MarkInventoryDamagedAsync(setup.Id, 1, "");
        Assert.False(result.Succeeded); Assert.Contains("reason", result.ErrorMessage);
    }

    [Fact] public async Task MarkDamaged_UpdatesCounts()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        var result = await setup.Service.MarkInventoryDamagedAsync(setup.Id, 2, "Broken legs");
        Assert.True(result.Succeeded); Assert.Equal((8, 2), (result.InventoryItem!.AvailableQuantity, result.InventoryItem.DamagedQuantity));
    }

    [Fact] public async Task MarkRepaired_UpdatesCounts()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        await setup.Service.MarkInventoryDamagedAsync(setup.Id, 2, "Broken legs");
        var result = await setup.Service.MarkInventoryRepairedAsync(setup.Id, 1, "Workshop repair");
        Assert.True(result.Succeeded); Assert.Equal((9, 1), (result.InventoryItem!.AvailableQuantity, result.InventoryItem.DamagedQuantity));
    }

    [Fact] public async Task InventorySoftDelete_RequiresReason()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        var result = await setup.Service.SoftDeleteInventoryItemAsync(setup.Id, "");
        Assert.False(result.Succeeded); Assert.Contains("reason", result.ErrorMessage);
    }

    [Fact] public async Task InventoryRestore_WorksAfterSoftDelete()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        Assert.True((await setup.Service.SoftDeleteInventoryItemAsync(setup.Id, "Duplicate item")).Succeeded);
        var restored = await setup.Service.RestoreInventoryItemAsync(setup.Id, "Deletion reversed");
        Assert.True(restored.Succeeded); Assert.False((await db.Context.InventoryItems.IgnoreQueryFilters().SingleAsync(x => x.Id == setup.Id)).IsDeleted);
    }

    [Fact] public async Task InventoryActions_WriteActivityLogs()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        await setup.Service.MarkInventoryDamagedAsync(setup.Id, 1, "Damage test");
        var actions = await db.Context.ActivityLogs.Select(x => x.Action).ToListAsync();
        Assert.Contains("Inventory Item Created", actions); Assert.Contains("Inventory Marked Damaged", actions);
    }

    [Fact] public async Task InventoryReport_ReflectsUpdatedData()
    {
        var setup = await CreateInventoryAsync(); await using var db = setup.Db;
        await setup.Service.AdjustInventoryQuantityAsync(setup.Id, new() { Quantity = 15, AvailableQuantity = 14, DamagedQuantity = 1, Reason = "Count" });
        var report = await new InventoryReportDataProvider(db.Context).GenerateAsync([], "Tester");
        Assert.Equal(15, report.Rows.Single()["Quantity"]);
    }

    [Fact] public async Task StockVerification_ReturnsBookCopies()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        var rows = await setup.Service.GetStockVerificationItemsAsync(new() { SessionId = setup.SessionId });
        Assert.Single(rows); Assert.Equal(setup.Data.Copy.Id, rows[0].BookCopyId);
    }

    [Fact] public async Task StockVerification_SavesActualStatus()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        var result = await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Available, "", false, "");
        Assert.True(result.Succeeded); Assert.Equal(BookStatus.Available, result.Item!.ActualStatus);
    }

    [Fact] public async Task StockVerification_MismatchRequiresRemarks()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        var result = await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Missing, "", false, "");
        Assert.False(result.Succeeded); Assert.Contains("remarks", result.ErrorMessage);
    }

    [Fact] public async Task CompletingVerification_CreatesSummary()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Available, "", false, "");
        var result = await setup.Service.CompleteVerificationSessionAsync(setup.SessionId, "Completed count");
        Assert.True(result.Succeeded); Assert.Equal(1, result.Summary!.MatchedCount); Assert.Equal("Completed", result.Session!.Status);
    }

    [Fact] public async Task Verification_DoesNotChangeBookCopyWithoutReconciliation()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Missing, "Not found", false, "");
        await db.Context.Entry(setup.Data.Copy).ReloadAsync();
        Assert.Equal(BookStatus.Available, setup.Data.Copy.AvailabilityStatus);
    }

    [Fact] public async Task ExplicitReconciliation_UpdatesBookCopyStatus()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        var result = await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Missing, "Not found", true, "Approved physical reconciliation");
        await db.Context.Entry(setup.Data.Copy).ReloadAsync();
        Assert.True(result.Succeeded); Assert.Equal(BookStatus.Missing, setup.Data.Copy.AvailabilityStatus); Assert.True(result.Item!.IsReconciled);
    }

    [Fact] public async Task StockVerificationReport_ReflectsVerificationData()
    {
        var setup = await CreateStockAsync(); await using var db = setup.Db;
        await setup.Service.VerifyBookCopyAsync(setup.SessionId, setup.Data.Copy.Id, BookStatus.Missing, "Shelf search failed", false, "");
        var report = await new StockVerificationReportDataProvider(db.Context).GenerateAsync([], "Tester");
        Assert.Equal("Missing", report.Rows.Single()["ActualStatus"]); Assert.Equal("1", report.SummaryItems["Mismatched"]);
    }

    [Fact] public async Task LowPrivilegeUser_CannotMutateInventory()
    {
        await using var db = await SqliteTestDatabase.CreateAsync(); var data = await db.AddCirculationDataAsync();
        var result = await Inventory(db, Viewer(data.User)).CreateInventoryItemAsync(Item());
        Assert.False(result.Succeeded); Assert.Contains("cannot create", result.ErrorMessage);
    }

    private static async Task<(SqliteTestDatabase Db, InventoryService Service, int Id)> CreateInventoryAsync()
    {
        var db = await SqliteTestDatabase.CreateAsync(); var data = await db.AddCirculationDataAsync();
        var service = Inventory(db, Admin(data.User)); var created = await service.CreateInventoryItemAsync(Item());
        return (db, service, created.InventoryItem!.InventoryItemId);
    }
    private static async Task<(SqliteTestDatabase Db, StockVerificationService Service, TestLibraryData Data, int SessionId)> CreateStockAsync()
    {
        var db = await SqliteTestDatabase.CreateAsync(); var data = await db.AddCirculationDataAsync();
        var service = new StockVerificationService(db.Context, Admin(data.User));
        var session = await service.StartVerificationSessionAsync("Annual verification");
        return (db, service, data, session.Session!.StockVerificationSessionId);
    }
    private static InventoryService Inventory(SqliteTestDatabase db, IAuthenticationService auth) => new(db.Context, auth);
    private static InventoryItemDetails Item() => new()
    {
        ItemName = "Reading Chair", ItemType = InventoryItemType.Chair, Quantity = 10,
        AvailableQuantity = 10, DamagedQuantity = 0, Location = "Reading Hall",
        Condition = "Good", PurchaseDate = DateTime.Today, PurchasePrice = 5000, Supplier = "Test Supplier"
    };
    private static FakeAuthenticationService Admin(User user) => Auth(user, "Admin", true, true);
    private static FakeAuthenticationService Viewer(User user) => Auth(user, "Read Only Viewer", true, false);
    private static FakeAuthenticationService Auth(User user, string role, bool view, bool manage)
    {
        user.UserRoles.Clear(); user.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { Name = role } });
        return new(user, view, manage);
    }
    private sealed class FakeAuthenticationService(User user, bool view, bool manage) : IAuthenticationService
    {
        public User? CurrentUser { get; } = user;
        public Task<User?> LoginAsync(string username, string password) => Task.FromResult(CurrentUser);
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword) => Task.FromResult(false);
        public Task<bool> VerifyUserPermissionAsync(int userId, string permissionCode) => Task.FromResult(permissionCode switch { "VIEW_INVENTORY" => view, "MANAGE_INVENTORY" => manage, _ => false });
        public Task<(bool Success, string Message)> RequestPasswordResetAsync(string usernameOrEmail) => Task.FromResult((true, ""));
            public Task<bool> ResetPasswordAsync(string usernameOrEmail, string token, string newPassword) => Task.FromResult(true);
            public Task<bool> GenerateAndSendOtpAsync(int userId) => Task.FromResult(true);
            public Task<bool> VerifyOtpAsync(int userId, string otp) => Task.FromResult(true);
            public Task LogoutAsync() => Task.CompletedTask;
    }
}


