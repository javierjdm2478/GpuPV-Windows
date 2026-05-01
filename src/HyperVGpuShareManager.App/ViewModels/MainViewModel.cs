using System.Collections.ObjectModel;
using System.Windows;
using HyperVGpuShareManager.App.Infrastructure;
using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Services;
using HyperVGpuShareManager.Core.Validation;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace HyperVGpuShareManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IHyperVService _hyperV;
    private readonly IGpuDetectionService _gpuDetection;
    private readonly IVmCreationService _vmCreation;
    private readonly IDriverPreparationService _driverPreparation;
    private readonly ILoggingService _logger;
    private readonly InputValidationService _validation;
    private readonly ThemeManager _themeManager;
    private readonly bool _isAdministrator;

    private SystemStatus? _systemStatus;
    private GpuInfo? _selectedGpu;
    private VmInfo? _selectedVm;
    private GpuPartitionSettings _partitionSettings = GpuPartitionSettings.CreateRecommended();
    private VmCreationRequest _newVmRequest = new();
    private bool _isBusy;
    private bool _hyperVCmdletsAvailable;
    private bool _createCheckpoint = true;
    private bool _forceTurnOff;
    private bool _tryStartDuringValidation = true;
    private int _selectedStepIndex;
    private string _statusMessage = "Listo.";
    private string _driverInstructions = string.Empty;
    private string _guestUserName = string.Empty;
    private string _guestPassword = string.Empty;

    public MainViewModel(
        IHyperVService hyperV,
        IGpuDetectionService gpuDetection,
        IVmCreationService vmCreation,
        IDriverPreparationService driverPreparation,
        ILoggingService logger,
        InputValidationService validation,
        ThemeManager themeManager,
        bool isAdministrator)
    {
        _hyperV = hyperV;
        _gpuDetection = gpuDetection;
        _vmCreation = vmCreation;
        _driverPreparation = driverPreparation;
        _logger = logger;
        _validation = validation;
        _themeManager = themeManager;
        _isAdministrator = isAdministrator;

        Steps = new ObservableCollection<WizardStepViewModel>
        {
            new() { Number = 1, Title = "Sistema", IsSelected = true },
            new() { Number = 2, Title = "GPU" },
            new() { Number = 3, Title = "VM" },
            new() { Number = 4, Title = "Recursos" },
            new() { Number = 5, Title = "Aplicar" },
            new() { Number = 6, Title = "Validar" }
        };

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyGpuPartitionCommand = new AsyncRelayCommand(ApplyGpuPartitionAsync, () => !IsBusy);
        RemoveGpuPartitionCommand = new AsyncRelayCommand(RemoveGpuPartitionAsync, () => SelectedVm is not null);
        CreateVmCommand = new AsyncRelayCommand(CreateVmAsync, () => !IsBusy);
        StopVmCommand = new AsyncRelayCommand(StopVmAsync, () => SelectedVm is not null);
        ValidateCommand = new AsyncRelayCommand(ValidateAsync, () => SelectedVm is not null);
        StartVmCommand = new AsyncRelayCommand(StartVmAsync, () => SelectedVm is not null);
        PrepareDriversCommand = new AsyncRelayCommand(PrepareDriversAsync, () => SelectedVm is not null && SelectedGpu is not null);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync);
        RestoreRecommendedCommand = new RelayCommand(RestoreRecommendedSettings);
        OpenHyperVManagerCommand = new RelayCommand(OpenHyperVManager);
        OpenVmConnectCommand = new RelayCommand(OpenVmConnect, () => SelectedVm is not null);
        BrowseIsoCommand = new RelayCommand(BrowseIso);
        BrowseStorageCommand = new RelayCommand(BrowseStorage);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        SelectStepCommand = new RelayCommand(parameter =>
        {
            if (parameter is WizardStepViewModel step)
            {
                SelectedStepIndex = Math.Max(0, step.Number - 1);
            }
        });

        _logger.LogWritten += OnLogWritten;
        _themeManager.ApplyTheme();
        _logger.Info("Hyper-V GPU Share Manager started.");
    }

    public ObservableCollection<WizardStepViewModel> Steps { get; }
    public ObservableCollection<GpuInfo> Gpus { get; } = new();
    public ObservableCollection<VmInfo> VirtualMachines { get; } = new();
    public ObservableCollection<VirtualSwitchInfo> VirtualSwitches { get; } = new();
    public ObservableCollection<ValidationResult> ValidationResults { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();

    public SystemStatus? SystemStatus
    {
        get => _systemStatus;
        private set => SetProperty(ref _systemStatus, value);
    }

    public GpuInfo? SelectedGpu
    {
        get => _selectedGpu;
        set
        {
            if (SetProperty(ref _selectedGpu, value))
            {
                DriverInstructions = value is null ? string.Empty : _driverPreparation.GetManualInstructions(value);
                RaiseCommandStates();
            }
        }
    }

    public VmInfo? SelectedVm
    {
        get => _selectedVm;
        set
        {
            if (SetProperty(ref _selectedVm, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public GpuPartitionSettings PartitionSettings
    {
        get => _partitionSettings;
        private set => SetProperty(ref _partitionSettings, value);
    }

    public VmCreationRequest NewVmRequest
    {
        get => _newVmRequest;
        set => SetProperty(ref _newVmRequest, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool HyperVCmdletsAvailable
    {
        get => _hyperVCmdletsAvailable;
        private set => SetProperty(ref _hyperVCmdletsAvailable, value);
    }

    public bool CreateCheckpoint
    {
        get => _createCheckpoint;
        set => SetProperty(ref _createCheckpoint, value);
    }

    public bool ForceTurnOff
    {
        get => _forceTurnOff;
        set => SetProperty(ref _forceTurnOff, value);
    }

    public bool TryStartDuringValidation
    {
        get => _tryStartDuringValidation;
        set => SetProperty(ref _tryStartDuringValidation, value);
    }

    public bool IsDarkTheme
    {
        get => _themeManager.IsDarkTheme;
        set
        {
            _themeManager.IsDarkTheme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeLabel));
        }
    }

    public string ThemeLabel => IsDarkTheme ? "Oscuro" : "Claro";

    public int SelectedStepIndex
    {
        get => _selectedStepIndex;
        set
        {
            if (SetProperty(ref _selectedStepIndex, value))
            {
                for (var i = 0; i < Steps.Count; i++)
                {
                    Steps[i].IsSelected = i == value;
                }
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DriverInstructions
    {
        get => _driverInstructions;
        private set => SetProperty(ref _driverInstructions, value);
    }

    public string GuestUserName
    {
        get => _guestUserName;
        set => SetProperty(ref _guestUserName, value);
    }

    public string GuestPassword
    {
        get => _guestPassword;
        set => SetProperty(ref _guestPassword, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyGpuPartitionCommand { get; }
    public AsyncRelayCommand RemoveGpuPartitionCommand { get; }
    public AsyncRelayCommand CreateVmCommand { get; }
    public AsyncRelayCommand StopVmCommand { get; }
    public AsyncRelayCommand ValidateCommand { get; }
    public AsyncRelayCommand StartVmCommand { get; }
    public AsyncRelayCommand PrepareDriversCommand { get; }
    public AsyncRelayCommand ExportDiagnosticsCommand { get; }
    public RelayCommand RestoreRecommendedCommand { get; }
    public RelayCommand OpenHyperVManagerCommand { get; }
    public RelayCommand OpenVmConnectCommand { get; }
    public RelayCommand BrowseIsoCommand { get; }
    public RelayCommand BrowseStorageCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SelectStepCommand { get; }

    public async Task InitializeAsync() => await RefreshAsync(CancellationToken.None);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await RunWithBusyStateAsync("Comprobando sistema, GPUs y VMs...", async () =>
        {
            SystemStatus = await _hyperV.GetSystemStatusAsync(_isAdministrator, cancellationToken);
            HyperVCmdletsAvailable = await _hyperV.AreHyperVCmdletsAvailableAsync(cancellationToken);

            ReplaceCollection(Gpus, await _gpuDetection.DetectGpusAsync(cancellationToken));
            ReplaceCollection(VirtualMachines, await _hyperV.GetVirtualMachinesAsync(cancellationToken));
            ReplaceCollection(VirtualSwitches, await _vmCreation.GetVirtualSwitchesAsync(cancellationToken));

            SelectedGpu ??= Gpus.FirstOrDefault();
            SelectedVm ??= VirtualMachines.FirstOrDefault();

            ValidationResults.Clear();
            foreach (var check in SystemStatus.Checks)
            {
                ValidationResults.Add(check);
            }

            if (HyperVCmdletsAvailable)
            {
                ValidationResults.Add(ValidationResult.Ok("Cmdlets Hyper-V", "Los cmdlets requeridos están disponibles."));
            }
            else
            {
                ValidationResults.Add(ValidationResult.Error("Cmdlets Hyper-V", "Faltan uno o más cmdlets de Hyper-V GPU-P."));
            }

            UpdateStepCompletion();
            StatusMessage = "Inventario actualizado.";
        });
    }

    private async Task CreateVmAsync(CancellationToken cancellationToken)
    {
        await RunWithBusyStateAsync("Creando VM...", async () =>
        {
            var created = await _vmCreation.CreateWindowsVmAsync(NewVmRequest, cancellationToken);
            ReplaceCollection(VirtualMachines, await _hyperV.GetVirtualMachinesAsync(cancellationToken));
            SelectedVm = VirtualMachines.FirstOrDefault(vm => vm.Name.Equals(created?.Name, StringComparison.OrdinalIgnoreCase)) ?? created;
            Steps[2].IsComplete = SelectedVm is not null;
            StatusMessage = $"VM creada: {SelectedVm?.Name}";
        });
    }

    private async Task StopVmAsync(CancellationToken cancellationToken)
    {
        if (SelectedVm is null)
        {
            return;
        }

        await RunWithBusyStateAsync("Apagando VM...", async () =>
        {
            await _hyperV.StopVmAsync(SelectedVm.Name, ForceTurnOff, cancellationToken);
            await RefreshAsync(cancellationToken);
            StatusMessage = "VM apagada.";
        });
    }

    private async Task ApplyGpuPartitionAsync(CancellationToken cancellationToken)
    {
        await RunWithBusyStateAsync("Aplicando GPU-P directamente...", async () =>
        {
            ValidationResults.Clear();
            var status = SystemStatus ?? await _hyperV.GetSystemStatusAsync(_isAdministrator, cancellationToken);
            var prerequisiteResults = _validation.ValidateApplyPrerequisites(status, SelectedGpu, SelectedVm, HyperVCmdletsAvailable);
            var settingsResults = _validation.ValidateGpuPartitionSettings(PartitionSettings);

            foreach (var result in prerequisiteResults.Concat(settingsResults))
            {
                ValidationResults.Add(result);
            }

            if (ValidationResults.Any(result => result.Severity == CheckSeverity.Error))
            {
                StatusMessage = "No se aplica nada porque hay validaciones críticas fallidas.";
                _logger.Warning(StatusMessage);
                return;
            }

            await _hyperV.ApplyGpuPartitionAsync(SelectedVm!.Name, SelectedGpu!, PartitionSettings, CreateCheckpoint, cancellationToken);
            ReplaceCollection(VirtualMachines, await _hyperV.GetVirtualMachinesAsync(cancellationToken));
            SelectedVm = VirtualMachines.FirstOrDefault(vm => vm.Name.Equals(SelectedVm!.Name, StringComparison.OrdinalIgnoreCase));
            Steps[4].IsComplete = true;
            StatusMessage = "GPU-P aplicado a la VM.";
        });
    }

    private async Task RemoveGpuPartitionAsync(CancellationToken cancellationToken)
    {
        if (SelectedVm is null)
        {
            return;
        }

        await RunWithBusyStateAsync("Quitando GPU-P de la VM...", async () =>
        {
            await _hyperV.RemoveGpuPartitionAsync(SelectedVm.Name, cancellationToken);
            ReplaceCollection(VirtualMachines, await _hyperV.GetVirtualMachinesAsync(cancellationToken));
            SelectedVm = VirtualMachines.FirstOrDefault(vm => vm.Name.Equals(SelectedVm.Name, StringComparison.OrdinalIgnoreCase));
            StatusMessage = "Adaptador GPU-P quitado.";
        });
    }

    private async Task PrepareDriversAsync(CancellationToken cancellationToken)
    {
        if (SelectedVm is null || SelectedGpu is null)
        {
            return;
        }

        await RunWithBusyStateAsync("Preparando drivers para la VM...", async () =>
        {
            var result = SelectedVm.IsOff
                ? await _driverPreparation.PrepareDriversOfflineAsync(SelectedVm, SelectedGpu, cancellationToken)
                : await _driverPreparation.PrepareDriversWithPowerShellDirectAsync(SelectedVm, SelectedGpu, GuestUserName, GuestPassword, cancellationToken);

            DriverInstructions = result.Success
                ? $"{result.Message}{Environment.NewLine}Destino: {result.Destination}"
                : $"{result.Message}{Environment.NewLine}{_driverPreparation.GetManualInstructions(SelectedGpu)}";
            StatusMessage = result.Success ? "Drivers copiados al VHDX." : "La copia automática de drivers no se completó.";
            ValidationResults.Add(result.Success
                ? ValidationResult.Ok("Drivers preparados", result.Message)
                : ValidationResult.Warning("Drivers requieren atención", result.Message));
        });
    }

    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (SelectedVm is null)
        {
            return;
        }

        await RunWithBusyStateAsync("Validando configuración de GPU-P...", async () =>
        {
            ValidationResults.Clear();
            foreach (var result in await _hyperV.ValidateGpuPartitionAsync(SelectedVm.Name, TryStartDuringValidation, cancellationToken))
            {
                ValidationResults.Add(result);
            }

            Steps[5].IsComplete = ValidationResults.All(result => result.Severity != CheckSeverity.Error);
            StatusMessage = "Validación completada.";
        });
    }

    private async Task StartVmAsync(CancellationToken cancellationToken)
    {
        if (SelectedVm is null)
        {
            return;
        }

        await RunWithBusyStateAsync("Iniciando VM...", async () =>
        {
            await _hyperV.StartVmAsync(SelectedVm.Name, cancellationToken);
            await RefreshAsync(cancellationToken);
            StatusMessage = "VM iniciada.";
        });
    }

    private async Task ExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de destino para el ZIP de diagnostico",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        await RunWithBusyStateAsync("Exportando diagnostico...", async () =>
        {
            var archivePath = await _logger.ExportDiagnosticsAsync(dialog.SelectedPath, cancellationToken);
            StatusMessage = $"Diagnostico exportado: {archivePath}";
        });
    }

    private void RestoreRecommendedSettings()
    {
        PartitionSettings = GpuPartitionSettings.CreateRecommended();
        StatusMessage = "Valores recomendados restaurados.";
    }

    private void OpenHyperVManager()
    {
        try
        {
            _hyperV.OpenHyperVManager();
        }
        catch (Exception ex)
        {
            HandleException("No se pudo abrir Hyper-V Manager.", ex);
        }
    }

    private void OpenVmConnect()
    {
        if (SelectedVm is null)
        {
            return;
        }

        try
        {
            _hyperV.OpenVmConnect(SelectedVm.Name);
        }
        catch (Exception ex)
        {
            HandleException("No se pudo abrir VMConnect.", ex);
        }
    }

    private void BrowseIso()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona una ISO de Windows 10 Pro",
            Filter = "ISO (*.iso)|*.iso|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            NewVmRequest.IsoPath = dialog.FileName;
            OnPropertyChanged(nameof(NewVmRequest));
        }
    }

    private void BrowseStorage()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde se guardara la VM",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            NewVmRequest.StoragePath = dialog.SelectedPath;
            OnPropertyChanged(nameof(NewVmRequest));
        }
    }

    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    private async Task RunWithBusyStateAsync(string message, Func<Task> operation)
    {
        IsBusy = true;
        StatusMessage = message;
        _logger.Info(message);

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            HandleException(message, ex);
        }
        finally
        {
            IsBusy = false;
            UpdateStepCompletion();
        }
    }

    private void HandleException(string message, Exception exception)
    {
        _logger.Error(message, exception);
        StatusMessage = $"{message} {exception.Message}";
        ValidationResults.Add(ValidationResult.Error("Error", exception.Message));
    }

    private void OnLogWritten(object? sender, LogEntry entry)
    {
        var line = entry.ToString();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AddLogLine(line);
        }
        else
        {
            dispatcher.Invoke(() => AddLogLine(line));
        }
    }

    private void AddLogLine(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > 500)
        {
            LogLines.RemoveAt(0);
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private void UpdateStepCompletion()
    {
        Steps[0].IsComplete = SystemStatus?.Checks.All(check => check.Severity != CheckSeverity.Error) == true;
        Steps[1].IsComplete = SelectedGpu?.IsPartitionable == true;
        Steps[2].IsComplete = SelectedVm is not null;
        Steps[3].IsComplete = _validation.ValidateGpuPartitionSettings(PartitionSettings).All(result => result.Severity != CheckSeverity.Error);
        Steps[4].IsComplete = SelectedVm?.HasGpuPartitionAdapter == true;
        Steps[5].IsComplete = ValidationResults.Count > 0 && ValidationResults.All(result => result.Severity != CheckSeverity.Error);
    }

    private void RaiseCommandStates()
    {
        ApplyGpuPartitionCommand.RaiseCanExecuteChanged();
        RemoveGpuPartitionCommand.RaiseCanExecuteChanged();
        CreateVmCommand.RaiseCanExecuteChanged();
        StopVmCommand.RaiseCanExecuteChanged();
        ValidateCommand.RaiseCanExecuteChanged();
        StartVmCommand.RaiseCanExecuteChanged();
        PrepareDriversCommand.RaiseCanExecuteChanged();
        OpenVmConnectCommand.RaiseCanExecuteChanged();
    }
}
