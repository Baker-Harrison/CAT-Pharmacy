using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CatAdaptive.App.Converters;

public class ZeroToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
        {
            return intVal == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        // If it's not an int, default to Collapsed or Visible? 
        // Usually if binding fails, we might want to hide the "empty" state.
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
