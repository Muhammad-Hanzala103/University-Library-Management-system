using KicsitLibrary.Core;
using KicsitLibrary.Services.Preferences;

namespace KicsitLibrary.Tests;

public class BrandingAndHintsTests
{
    [Fact]
    public void VisibleProductName_IsIlmOKutubSystem()
    {
        Assert.Equal("Ilm-o-Kutub System", ProductBrand.Name);
    }

    [Fact]
    public void HintPreference_DefaultsToEnabled()
    {
        var hints = new HintService();

        Assert.True(hints.ShowHelpfulHints);
    }

    [Fact]
    public void HintPreference_CanToggleOffAndOn()
    {
        var hints = new HintService();

        hints.ShowHelpfulHints = false;
        Assert.False(hints.ShowHelpfulHints);

        hints.ShowHelpfulHints = true;
        Assert.True(hints.ShowHelpfulHints);
    }

    [Fact]
    public void KeyUiFiles_UseNewVisibleBranding()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "MainWindow.xaml"),
            Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "LoginWindow.xaml"),
            Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "appsettings.json")
        };

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.Contains(ProductBrand.Name, content, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "KICSIT Library Management System",
                content,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "University Library Management System",
                content,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SplashWindow_ExistsAndIsIntegrated()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xamlPath = Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "Views", "SplashWindow.xaml");
        var csPath = Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "Views", "SplashWindow.xaml.cs");
        var appCsPath = Path.Combine(repositoryRoot, "KicsitLibrary.Desktop", "App.xaml.cs");

        Assert.True(File.Exists(xamlPath), "SplashWindow.xaml should exist.");
        Assert.True(File.Exists(csPath), "SplashWindow.xaml.cs should exist.");

        var appCsContent = File.ReadAllText(appCsPath);
        Assert.Contains("new SplashWindow()", appCsContent);
        Assert.Contains("splash.Show()", appCsContent);
        Assert.Contains("splash.Close()", appCsContent);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KicsitLibrary.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
