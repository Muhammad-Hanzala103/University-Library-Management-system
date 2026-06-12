using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace KicsitLibrary.Desktop
{
    public class MenuSelectionConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null)
            {
                if (value.ToString() == parameter.ToString())
                {
                    return "Selected";
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class NullToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isInverse = parameter?.ToString() == "Inverse";
            bool isNull = value == null;
            bool check = isInverse ? !isNull : isNull;
            return check ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class BooleanToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                bool isInverse = parameter?.ToString() == "Inverse";
                bool check = isInverse ? !flag : flag;
                return check ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class StringVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasText = !string.IsNullOrWhiteSpace(value?.ToString());
            bool isInverse = parameter?.ToString() == "Inverse";
            bool check = isInverse ? !hasText : hasText;
            return check ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class AuthorsDisplayConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is System.Collections.Generic.IEnumerable<KicsitLibrary.Core.Entities.BookAuthor> bookAuthors)
            {
                return string.Join(", ", bookAuthors.Select(ba => ba.Author?.Name).Where(n => !string.IsNullOrEmpty(n)));
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class BooleanToColorConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool flag && parameter is string paramStr)
            {
                var parts = paramStr.Split('|');
                if (parts.Length == 2)
                {
                    string colorHex = flag ? parts[0] : parts[1];
                    try
                    {
                        var converter = new System.Windows.Media.BrushConverter();
                        return converter.ConvertFromString(colorHex) ?? System.Windows.Media.Brushes.Transparent;
                    }
                    catch
                    {
                        return System.Windows.Media.Brushes.Transparent;
                    }
                }
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class InvertedBooleanConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                return !flag;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                return !flag;
            }
            return false;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class ObjectEqualityConverter : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            if (values[0] == null || values[1] == null) return false;
            return values[0].Equals(values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }

    public class SummaryValueConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return 0.0;
            string strValue = value.ToString() ?? "";
            
            string clean = "";
            bool hasPercentage = strValue.Contains("%");
            foreach (char c in strValue)
            {
                if (char.IsDigit(c) || c == '.')
                {
                    clean += c;
                }
            }

            if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                if (hasPercentage)
                {
                    return Math.Min(result, 100.0);
                }
                
                if (result <= 10.0) return result * 10.0;
                if (result <= 100.0) return result;
                if (result <= 1000.0) return result / 10.0;
                return Math.Min(result / 100.0, 100.0);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
