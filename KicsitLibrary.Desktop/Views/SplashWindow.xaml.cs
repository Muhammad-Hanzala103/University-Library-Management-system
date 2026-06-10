using System;
using System.Reflection;
using System.Windows;

namespace KicsitLibrary.Desktop.Views
{
    /// <summary>
    /// Interaction logic for SplashWindow.xaml
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            LoadVersion();
        }

        private void LoadVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                VersionText.Text = "v1.0.0";
            }
        }

        /// <summary>
        /// Safely updates the status text shown on the splash screen.
        /// </summary>
        public void UpdateStatus(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(message));
                return;
            }
            StatusText.Text = message;
        }
    }
}
