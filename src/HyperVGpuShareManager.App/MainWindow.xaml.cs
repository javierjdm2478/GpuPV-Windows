using System.Windows;
using System.Windows.Controls;
using HyperVGpuShareManager.App.ViewModels;

namespace HyperVGpuShareManager.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void GuestPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.GuestPassword = passwordBox.Password;
        }
    }
}
