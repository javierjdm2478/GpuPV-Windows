using System.Windows;

namespace HyperVGpuShareManager.App.Infrastructure;

public sealed class ThemeManager : ObservableObject
{
    private bool _isDarkTheme = true;

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                ApplyTheme();
            }
        }
    }

    public void ApplyTheme()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var source = IsDarkTheme
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var dictionary = new ResourceDictionary { Source = source };
        var oldTheme = app.Resources.MergedDictionaries
            .FirstOrDefault(item => item.Source is not null && item.Source.OriginalString.Contains("Theme", StringComparison.OrdinalIgnoreCase));

        if (oldTheme is not null)
        {
            app.Resources.MergedDictionaries.Remove(oldTheme);
        }

        app.Resources.MergedDictionaries.Add(dictionary);
    }
}
