using System.Globalization;
using System.Windows.Data;

namespace HyperVGpuShareManager.App.Converters;

public sealed class BytesToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ulong bytes && bytes > 0)
        {
            var gb = bytes / 1024d / 1024d / 1024d;
            return $"{gb:n1} GB";
        }

        return "No detectado";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
