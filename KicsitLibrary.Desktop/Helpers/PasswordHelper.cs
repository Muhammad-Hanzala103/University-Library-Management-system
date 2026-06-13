using System.Windows;
using System.Windows.Controls;

namespace KicsitLibrary.Desktop.Helpers
{
    public static class PasswordHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordHelper),
                new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordHelper),
                new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject d)
        {
            return (string)d.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject d, string value)
        {
            d.SetValue(BoundPasswordProperty, value);
        }

        public static bool GetBindPassword(DependencyObject d)
        {
            return (bool)d.GetValue(BindPasswordProperty);
        }

        public static void SetBindPassword(DependencyObject d, bool value)
        {
            d.SetValue(BindPasswordProperty, value);
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                if ((bool)passwordBox.GetValue(BindPasswordProperty))
                {
                    if (!(bool)passwordBox.GetValue(UpdatingPasswordProperty))
                    {
                        var newPassword = (string)e.NewValue ?? string.Empty;
                        if (passwordBox.Password != newPassword)
                        {
                            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                            passwordBox.Password = newPassword;
                            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                        }
                    }
                }
            }
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                bool needToBind = (bool)e.NewValue;

                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

                if (needToBind)
                {
                    passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                }
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.SetValue(UpdatingPasswordProperty, true);
                SetBoundPassword(passwordBox, passwordBox.Password);
                passwordBox.SetValue(UpdatingPasswordProperty, false);
            }
        }
    }
}
