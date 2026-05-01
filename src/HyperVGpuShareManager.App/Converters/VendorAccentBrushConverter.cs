using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.App.Converters;

public sealed class VendorAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is GpuVendor vendor
            ? vendor switch
            {
                GpuVendor.Nvidia => new SolidColorBrush(Color.FromRgb(118, 185, 0)),
                GpuVendor.Amd => new SolidColorBrush(Color.FromRgb(237, 28, 36)),
                GpuVendor.Intel => new SolidColorBrush(Color.FromRgb(0, 113, 197)),
                _ => new SolidColorBrush(Color.FromRgb(96, 165, 250))
            }
            : new SolidColorBrush(Color.FromRgb(96, 165, 250));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
