using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace KicsitLibrary.Desktop.Helpers
{
    public static class ClipboardHelper
    {
        public static readonly DependencyProperty ClickToCopyProperty =
            DependencyProperty.RegisterAttached(
                "ClickToCopy",
                typeof(bool),
                typeof(ClipboardHelper),
                new PropertyMetadata(false, OnClickToCopyChanged));

        public static bool GetClickToCopy(DependencyObject obj) => (bool)obj.GetValue(ClickToCopyProperty);
        public static void SetClickToCopy(DependencyObject obj, bool value) => obj.SetValue(ClickToCopyProperty, value);

        private static void OnClickToCopyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                element.PreviewMouseDown -= Element_PreviewMouseDown;
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseDown += Element_PreviewMouseDown;
                    if (d is FrameworkElement fe)
                    {
                        fe.Cursor = Cursors.Hand;
                        if (string.IsNullOrEmpty(fe.ToolTip as string))
                        {
                            fe.ToolTip = "Click to copy to clipboard";
                        }
                    }
                }
            }
        }

        private static void Element_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string? textToCopy = null;
            if (sender is TextBlock tb)
            {
                textToCopy = tb.Text;
            }
            else if (sender is TextBox txt)
            {
                textToCopy = txt.Text;
            }
            else if (sender is Button btn && btn.Content is string btnStr)
            {
                textToCopy = btnStr;
            }
            else if (sender is ContentControl cc && cc.Content is string ccStr)
            {
                textToCopy = ccStr;
            }

            if (!string.IsNullOrWhiteSpace(textToCopy))
            {
                try
                {
                    Clipboard.SetText(textToCopy);
                    MessageBox.Show("Copied to clipboard", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                    e.Handled = true;
                }
                catch
                {
                    // Do not crash if clipboard is unavailable
                }
            }
        }
    }
}
