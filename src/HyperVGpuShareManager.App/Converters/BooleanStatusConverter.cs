using System.Globalization;
using System.Windows.Data;

namespace HyperVGpuShareManager.App.Converters;

public sealed class BooleanStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var labels = (parameter as string ?? "OK|No").Split('|');
        var ok = labels.ElementAtOrDefault(0) ?? "OK";
        var fail = labels.ElementAtOrDefault(1) ?? "No";
        return value is bool boolean && boolean ? ok : fail;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
