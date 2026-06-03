using System;
using System.IO;
using System.Windows;
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
                    config.SetBasePath(Directory.GetCurrentDirectory());
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

                    // Register Shell Window and ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>(s => new MainWindow
                    {
                        DataContext = s.GetRequiredService<MainViewModel>()
                    });
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<DashboardViewModel>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            // Run database migrations on startup
            using (var scope = AppHost.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<KicsitLibraryDbContext>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                try
                {
                    await dbContext.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    // Soft fall back to EnsureCreated if localdb isn't fully installed or migrations fail for testing
                    try
                    {
                        await dbContext.Database.EnsureCreatedAsync();
                    }
                    catch (Exception innerEx)
                    {
                        MessageBox.Show($"Database initialization failed:\n{ex.Message}\n\nFallback Error:\n{innerEx.Message}", 
                            "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                try
                {
                    await DbSeeder.SeedAsync(dbContext, passwordHasher);
                }
                catch (Exception seedEx)
                {
                    MessageBox.Show($"Database seeding failed:\n{seedEx.Message}", 
                        "Seeding Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
