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
                await DbSeeder.SeedAsync(dbContext, passwordHasher);
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
                await AppHost.StopAsync();
                AppHost.Dispose();
            }
            base.OnExit(e);
        }
    }
}
