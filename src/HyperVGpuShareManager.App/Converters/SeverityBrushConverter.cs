using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.App.Converters;

public sealed class SeverityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is CheckSeverity severity
            ? severity switch
            {
                CheckSeverity.Ok => "SuccessBrush",
                CheckSeverity.Warning => "WarningBrush",
                CheckSeverity.Error => "DangerBrush",
                _ => "MutedTextBrush"
            }
            : "MutedTextBrush";

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
