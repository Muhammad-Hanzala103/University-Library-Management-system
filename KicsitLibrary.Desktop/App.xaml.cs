using System;
using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Data;
using KicsitLibrary.Data.Repositories;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Services;
using KicsitLibrary.Services.Authentication;
using KicsitLibrary.Services.Dashboard;
using KicsitLibrary.Services.Logging;
using KicsitLibrary.Services.Catalog;
using KicsitLibrary.Services.Consumer;
using KicsitLibrary.Services.Circulation;
using KicsitLibrary.Services.Clearance;
using KicsitLibrary.Services.Reservations;
using KicsitLibrary.Services.Auditing;
using KicsitLibrary.Services.Inventory;
using KicsitLibrary.Services.Backup;
using KicsitLibrary.Services.Restore;
using KicsitLibrary.Services.Notifications;
using KicsitLibrary.Services.Ownership;
using KicsitLibrary.Services.Preferences;
using KicsitLibrary.Services.Documents;
using KicsitLibrary.Services;
using KicsitLibrary.Services.Runtime;
using KicsitLibrary.Desktop.Views;
using KicsitLibrary.Reports.Contracts;
using KicsitLibrary.Reports.Export;
using KicsitLibrary.Reports.Providers;
using KicsitLibrary.Reports.Services;

namespace KicsitLibrary.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;
                    var connectionString = configuration.GetConnectionString("DefaultConnection");
                    var dbProvider = configuration.GetValue<string>("SystemSettings:DatabaseProvider") ?? "SqlServer";

                    // Register DB Context Factory dynamically (SQL Server or SQLite)
                    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                    {
                        connectionString = ResolveSqliteConnectionString(connectionString);
                        services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
                            options.UseSqlite(connectionString, b => b.MigrationsAssembly("KicsitLibrary.Data")));
                    }
                    else
                    {
                        services.AddDbContextFactory<KicsitLibraryDbContext>(options =>
                            options.UseSqlServer(connectionString, b => b.MigrationsAssembly("KicsitLibrary.Data")));
                    }

                    // Register scoped DbContext resolved from factory for backwards compatibility
                    services.AddScoped(p => p.GetRequiredService<IDbContextFactory<KicsitLibraryDbContext>>().CreateDbContext());

                    // Register Repositories
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

                    // Register Services
                    services.AddSingleton<IPasswordHasher, PasswordHasher>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IHintService>(_ => HintService.Current);
                    services.AddScoped<IActivityLogService, ActivityLogService>();
                    services.AddSingleton<IAuthenticationService, AuthenticationService>();
                    services.AddScoped<IDashboardService, DashboardService>();
                    services.AddScoped<ICatalogService, CatalogService>();
                    services.AddScoped<IConsumerService, ConsumerService>();
                    services.AddScoped<ICirculationService, CirculationService>();
                    services.AddScoped<IEmailSettingsService, EmailSettingsService>();
                    services.AddSingleton<IEmailTransport, MailKitEmailTransport>();
                    services.AddScoped<INotificationService, NotificationService>();
                    services.AddScoped<IOverdueService, OverdueService>();
                    services.AddSingleton<IOverdueSchedulerService, OverdueSchedulerService>();
                    services.AddSingleton<OverdueSchedulerStartupSignal>();
                    services.AddHostedService<OverdueSchedulerBackgroundService>();
                    services.AddScoped<IRecordDetailsService, RecordDetailsService>();
                    services.AddScoped<IReportDataProvider, CatalogReportDataProvider>();
                    services.AddScoped<IReportDataProvider, IssuedBooksReportDataProvider>();
                    services.AddScoped<IReportDataProvider, OverdueBooksReportDataProvider>();
                    services.AddScoped<IReportDataProvider, FineReportDataProvider>();
                    services.AddScoped<IReportDataProvider, NotificationReportDataProvider>();
                    services.AddScoped<IReportDataProvider, StudentClearanceReportDataProvider>();
                    services.AddScoped<IReportDataProvider, StudentBorrowingHistoryReportDataProvider>();
                    services.AddScoped<IReportDataProvider, FacultyBorrowingHistoryReportDataProvider>();
                    services.AddScoped<IReportDataProvider, ReservationReportDataProvider>();
                    services.AddScoped<IReportDataProvider, LostDamagedBooksReportDataProvider>();
                    services.AddScoped<IReportDataProvider, DeletedBooksArchiveReportDataProvider>();
                    services.AddScoped<IReportDataProvider, VisitDetailReportDataProvider>();
                    services.AddScoped<IReportDataProvider, AuditReportDataProvider>();
                    services.AddScoped<IReportDataProvider, InventoryReportDataProvider>();
                    services.AddScoped<IReportDataProvider, NewArrivalsReportDataProvider>();
                    services.AddScoped<IReportDataProvider, StockVerificationReportDataProvider>();
                    services.AddScoped<IReportDataProvider, SopDocumentsReportDataProvider>();
                    services.AddScoped<IReportDataProvider, NationalLibraryRatesDocumentsReportDataProvider>();
                    services.AddScoped<IReportExporter, CsvReportExporter>();
                    services.AddScoped<IReportExporter, ExcelReportExporter>();
                    services.AddScoped<IReportExporter, PdfReportExporter>();
                    services.AddScoped<IReportService, ReportService>();
                    services.AddScoped<IReportExportService, ReportExportService>();
                    services.AddScoped<IClearanceService, ClearanceService>();
                    services.AddSingleton<IClearanceDetailsDialogService, ClearanceDetailsDialogService>();
                    services.AddScoped<IReservationService, ReservationService>();
                    services.AddSingleton<IReservationDialogService, ReservationDialogService>();
                    services.AddScoped<IActivityLogBrowserService, ActivityLogBrowserService>();
                    services.AddScoped<IAuditRecordService, AuditRecordService>();
                    services.AddSingleton<IAuditDialogService, AuditDialogService>();
                    services.AddScoped<IInventoryService, InventoryService>();
                    services.AddScoped<IStockVerificationService, StockVerificationService>();
                    services.AddSingleton<IInventoryDialogService, InventoryDialogService>();
                    services.AddScoped<IBackupService, BackupService>();
                    services.AddScoped<IBackupRetentionService, BackupRetentionService>();
                    services.AddSingleton<IAutomaticBackupSchedulerService, AutomaticBackupSchedulerService>();
                    services.AddSingleton<AutomaticBackupStartupSignal>();
                    services.AddHostedService<AutomaticBackupBackgroundService>();
                    services.AddSingleton<IBackupDialogService, BackupDialogService>();
                    services.AddScoped<IRestoreService, RestoreService>();
                    services.AddSingleton<IRestoreDialogService, RestoreDialogService>();
                    services.AddSingleton<IDatabaseOwnershipService, DatabaseOwnershipService>();
                    services.AddScoped<IRuntimePathService, RuntimePathService>();
                    services.AddScoped<IDatabaseRelocationService, DatabaseRelocationService>();
                    services.AddScoped<IDocumentStorageService, DocumentStorageService>();
                    services.AddScoped<IDocumentService, DocumentService>();
                    services.AddSingleton<IDocumentDialogService, DocumentDialogService>();
                    services.AddScoped<ISettingsManagementService, SettingsManagementService>();
                    services.AddSingleton<ISettingsDialogService, SettingsDialogService>();

                    // Register Shell Window and ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>(s => new MainWindow
                    {
                        DataContext = s.GetRequiredService<MainViewModel>()
                    });
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<BookCatalogViewModel>();
                    services.AddTransient<BookFormViewModel>();
                    services.AddTransient<AuthorViewModel>();
                    services.AddTransient<PublisherViewModel>();
                    services.AddTransient<CopiesViewModel>();
                    services.AddTransient<StudentsManagementViewModel>();
                    services.AddTransient<StudentFormViewModel>();
                    services.AddTransient<FacultyStaffManagementViewModel>();
                    services.AddTransient<FacultyStaffFormViewModel>();
                    services.AddTransient<ConsumerProfileViewModel>();
                    services.AddTransient<VisitRecordsViewModel>();
                    services.AddTransient<VisitRecordFormViewModel>();
                    services.AddTransient<IssueMaterialViewModel>();
                    services.AddTransient<ReceiveMaterialViewModel>();
                    services.AddTransient<FinesManagementViewModel>();
                    services.AddTransient<OverdueRemindersViewModel>();
                    services.AddTransient<NotificationCenterViewModel>();
                    services.AddTransient<ReportsDashboardViewModel>();
                    services.AddTransient<ClearanceDashboardViewModel>();
                    services.AddTransient<StudentClearanceViewModel>();
                    services.AddTransient<FacultyStaffClearanceViewModel>();
                    services.AddTransient<ClearanceDetailsViewModel>();
                    services.AddTransient<ReservationManagementViewModel>();
                    services.AddTransient<ReservationFormViewModel>();
                    services.AddTransient<ReservationQueueViewModel>();
                    services.AddTransient<ActivityLogsViewModel>();
                    services.AddTransient<ActivityLogDetailsViewModel>();
                    services.AddTransient<AuditRecordsViewModel>();
                    services.AddTransient<AuditRecordFormViewModel>();
                    services.AddTransient<AuditRecordDetailsViewModel>();
                    services.AddTransient<InventoryManagementViewModel>();
                    services.AddTransient<InventoryItemFormViewModel>();
                    services.AddTransient<InventoryDetailsViewModel>();
                    services.AddTransient<InventoryAdjustmentViewModel>();
                    services.AddTransient<StockVerificationViewModel>();
                    services.AddTransient<StockVerificationDetailsViewModel>();
                    services.AddTransient<BackupManagementViewModel>();
                    services.AddTransient<BackupDetailsViewModel>();
                    services.AddTransient<RestoreManagementViewModel>();
                    services.AddTransient<RestorePreviewViewModel>();
                    services.AddTransient<DocumentManagementViewModel>();
                    services.AddTransient<DocumentUploadViewModel>();
                    services.AddTransient<DocumentDetailsViewModel>();
                    services.AddTransient<SettingsManagementViewModel>();
                    services.AddTransient<SettingsEditViewModel>();
                    services.AddTransient<SettingsDetailsViewModel>();
                    services.AddTransient<OverdueRemindersView>();
                    services.AddTransient<NotificationCenterView>();
                    services.AddTransient<ReportsDashboardView>();
                    services.AddTransient<ReportPreviewView>();
                    services.AddTransient<ClearanceDashboardView>();
                    services.AddTransient<StudentClearanceView>();
                    services.AddTransient<FacultyStaffClearanceView>();
                    services.AddTransient<ClearanceDetailsWindow>();
                    services.AddTransient<ReservationManagementView>();
                    services.AddTransient<ReservationFormWindow>();
                    services.AddTransient<ReservationQueueWindow>();
                    services.AddTransient<ActivityLogsView>();
                    services.AddTransient<ActivityLogDetailsWindow>();
                    services.AddTransient<AuditRecordsView>();
                    services.AddTransient<AuditRecordFormWindow>();
                    services.AddTransient<AuditRecordDetailsWindow>();
                    services.AddTransient<InventoryManagementView>();
                    services.AddTransient<InventoryItemFormWindow>();
                    services.AddTransient<InventoryDetailsWindow>();
                    services.AddTransient<InventoryAdjustmentWindow>();
                    services.AddTransient<StockVerificationView>();
                    services.AddTransient<StockVerificationDetailsWindow>();
                    services.AddTransient<BackupManagementView>();
                    services.AddTransient<BackupDetailsWindow>();
                    services.AddTransient<RestoreManagementView>();
                    services.AddTransient<RestorePreviewWindow>();
                    services.AddTransient<DocumentManagementView>();
                    services.AddTransient<DocumentUploadWindow>();
                    services.AddTransient<DocumentDetailsWindow>();
                    services.AddTransient<SettingsManagementView>();
                    services.AddTransient<SettingsEditWindow>();
                    services.AddTransient<SettingsDetailsWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await AppHost!.StartAsync();
                
                var configuration = AppHost!.Services.GetRequiredService<IConfiguration>();
                var provider =
                    configuration.GetValue<string>("SystemSettings:DatabaseProvider") ?? "SqlServer";
                string? sqliteDatabasePath = null;
                
                var ownershipService = AppHost.Services.GetRequiredService<IDatabaseOwnershipService>();
                
                if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    var resolvedConnectionString = ResolveSqliteConnectionString(
                        configuration.GetConnectionString("DefaultConnection"));
                    sqliteDatabasePath =
                        Path.GetFullPath(new SqliteConnectionStringBuilder(resolvedConnectionString).DataSource);
                        
                    var instanceLock = await ownershipService.AcquireApplicationInstanceLockAsync(sqliteDatabasePath);
                    
                    bool singleInstanceMode = true;
                    bool allowReadOnly = false;
                    bool cleanupStaleLocks = true;

                    // Read settings directly to decide
                    if (File.Exists(sqliteDatabasePath))
                    {
                        try
                        {
                            var builder = new SqliteConnectionStringBuilder(resolvedConnectionString) { Mode = SqliteOpenMode.ReadOnly };
                            using var conn = new SqliteConnection(builder.ToString());
                            await conn.OpenAsync();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "SELECT Key, Value FROM SystemSettings WHERE Key IN ('SingleInstanceMode', 'AllowReadOnlySecondInstance', 'CleanupStaleLockFilesOnStartup')";
                            using var reader = await cmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                var key = reader.GetString(0);
                                var val = reader.GetString(1);
                                if (key == "SingleInstanceMode" && bool.TryParse(val, out var b1)) singleInstanceMode = b1;
                                if (key == "AllowReadOnlySecondInstance" && bool.TryParse(val, out var b2)) allowReadOnly = b2;
                                if (key == "CleanupStaleLockFilesOnStartup" && bool.TryParse(val, out var b3)) cleanupStaleLocks = b3;
                            }
                        }
                        catch { /* Ignore if DB not fully created */ }
                    }

                    if (!instanceLock.Succeeded && singleInstanceMode && !allowReadOnly)
                    {
                        MessageBox.Show(
                            "Another instance of Ilm-o-Kutub System is already running.\n\nThe application will now exit.",
                            "Application Already Running",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        Shutdown();
                        return;
                    }

                    if (cleanupStaleLocks && instanceLock.Succeeded)
                    {
                        try { await ownershipService.CleanupStaleLockFilesAsync(true); } catch { }
                    }

                    if (instanceLock.Succeeded)
                    {
                        await ownershipService.RunWithCriticalOperationLockAsync("Apply Pending Restore", sqliteDatabasePath, async (ct) =>
                        {
                            var pendingRestore = await PendingRestoreProcessor.ApplyPendingRestoreAsync(sqliteDatabasePath);
                            if (pendingRestore?.Status == "CriticalFailure")
                            {
                                throw new InvalidOperationException(
                                    $"Pending database restore and rollback failed: {pendingRestore.ErrorMessage}");
                            }
                        });
                    }
                }

                using var scope = AppHost.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                // Current development databases were created with EnsureCreated.
                // Do not mix this path with migrations until a safe baseline strategy is implemented.
                if (!await dbContext.Database.EnsureCreatedAsync())
                {
                    if (!await dbContext.Database.CanConnectAsync())
                    {
                        throw new InvalidOperationException("The configured database exists but cannot be opened.");
                    }
                }
                if (!string.IsNullOrWhiteSpace(sqliteDatabasePath))
                {
                    await ownershipService.RunWithCriticalOperationLockAsync(
                        "Database Compatibility Initialization",
                        sqliteDatabasePath,
                        _ => DatabaseCompatibilityInitializer.ApplyAsync(dbContext));
                }
                else
                {
                    await DatabaseCompatibilityInitializer.ApplyAsync(dbContext);
                }
                await DbSeeder.SeedAsync(dbContext, passwordHasher);
                if (!string.IsNullOrWhiteSpace(sqliteDatabasePath))
                {
                    await PendingRestoreProcessor.ImportResultAsync(
                        dbContext,
                        sqliteDatabasePath);
                }
                AppHost.Services
                    .GetRequiredService<OverdueSchedulerStartupSignal>()
                    .MarkReady();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Database initialization failed. The application cannot continue.\n\n{ex.Message}",
                    "Fatal Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            var loginWindow = AppHost.Services.GetRequiredService<LoginWindow>();
            var loginResult = loginWindow.ShowDialog();

            if (loginResult == true)
            {
                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                AppHost.Services
                    .GetRequiredService<AutomaticBackupStartupSignal>()
                    .MarkReady();
            }
            else
            {
                Shutdown();
            }

            base.OnStartup(e);
        }

        private static string ResolveSqliteConnectionString(string? connectionString)
            => StartupDatabasePathResolver.ResolveSqliteConnectionString(
                connectionString,
                AppContext.BaseDirectory);

        protected override async void OnExit(ExitEventArgs e)
        {
            if (AppHost != null)
            {
                await AppHost.StopAsync(TimeSpan.FromSeconds(10));
                AppHost.Dispose();
            }
            base.OnExit(e);
        }
    }
}
