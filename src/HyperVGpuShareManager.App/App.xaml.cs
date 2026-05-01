using HyperVGpuShareManager.App.Infrastructure;
using HyperVGpuShareManager.App.ViewModels;
using HyperVGpuShareManager.Core.Services;
using HyperVGpuShareManager.Core.Validation;

namespace HyperVGpuShareManager.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!AdminRelauncher.IsAdministrator() && AdminRelauncher.TryRelaunchElevated())
        {
            System.Windows.Application.Current.Shutdown();
            return;
        }

        var logger = new LoggingService();
        var powerShell = new PowerShellService(logger);
        var validation = new InputValidationService();
        var themeManager = new ThemeManager();
        var hyperV = new HyperVService(powerShell, logger);
        var gpuDetection = new GpuDetectionService(powerShell, logger);
        var vmCreation = new VmCreationService(powerShell, validation);
        var driverPreparation = new DriverPreparationService(powerShell, logger);

        var viewModel = new MainViewModel(
            hyperV,
            gpuDetection,
            vmCreation,
            driverPreparation,
            logger,
            validation,
            themeManager,
            AdminRelauncher.IsAdministrator());

        var window = new MainWindow(viewModel);
        System.Windows.Application.Current.MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
