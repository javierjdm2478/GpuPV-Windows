using HyperVGpuShareManager.App.Infrastructure;

namespace HyperVGpuShareManager.App.ViewModels;

public sealed class WizardStepViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isComplete;

    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set => SetProperty(ref _isComplete, value);
    }
}
