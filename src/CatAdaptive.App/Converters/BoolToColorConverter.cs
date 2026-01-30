using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CatAdaptive.App.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRead)
        {
            return isRead ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) // Green for read
                          : new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Gray for unread
        }
        return new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
