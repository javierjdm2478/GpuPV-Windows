using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HyperVGpuShareManager.App.Converters;

public sealed class BooleanBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is null
            ? "WarningBrush"
            : value is bool boolean && boolean
                ? "SuccessBrush"
                : "DangerBrush";
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
