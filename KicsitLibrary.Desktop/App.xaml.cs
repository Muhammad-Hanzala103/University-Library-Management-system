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
using KicsitLibrary.Services.Notifications;
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
                    services.AddSingleton<IBackupDialogService, BackupDialogService>();

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
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await AppHost!.StartAsync();

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
                await DatabaseCompatibilityInitializer.ApplyAsync(dbContext);
                await DbSeeder.SeedAsync(dbContext, passwordHasher);
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
            }
            else
            {
                Shutdown();
            }

            base.OnStartup(e);
        }

        private static string ResolveSqliteConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("The SQLite connection string is not configured.");
            }

            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                throw new InvalidOperationException("The SQLite data source is not configured.");
            }

            if (!builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) &&
                !Path.IsPathRooted(builder.DataSource))
            {
                builder.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, builder.DataSource));
            }

            var databaseDirectory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrWhiteSpace(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            return builder.ToString();
        }

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
