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

                    // Register DB Context (SQL Server LocalDB)
                    services.AddDbContext<KicsitLibraryDbContext>(options =>
                        options.UseSqlServer(connectionString, b => b.MigrationsAssembly("KicsitLibrary.Data")));

                    // Register Repositories
                    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

                    // Register Shell Window and ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>(s => new MainWindow
                    {
                        DataContext = s.GetRequiredService<MainViewModel>()
                    });
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
            }

            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

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
