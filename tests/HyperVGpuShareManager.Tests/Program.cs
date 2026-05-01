using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Validation;

var tests = new List<(string Name, Action Test)>
{
    ("Recommended GPU-P settings are ordered", RecommendedSettingsAreOrdered),
    ("VM names reject shell-sensitive characters", VmNameRejectsUnsafeCharacters),
    ("Valid absolute directory path is accepted syntactically", AbsoluteDirectoryPathAccepted)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static void RecommendedSettingsAreOrdered()
{
    var validator = new InputValidationService();
    var settings = GpuPartitionSettings.CreateRecommended();
    var results = validator.ValidateGpuPartitionSettings(settings);
    Assert(!results.Any(result => result.Severity == CheckSeverity.Error), "Recommended settings should not produce errors.");
}

static void VmNameRejectsUnsafeCharacters()
{
    var validator = new InputValidationService();
    var result = validator.ValidateVmName("bad\"name");
    Assert(result.Severity == CheckSeverity.Error, "Unsafe VM name should be rejected.");
}

static void AbsoluteDirectoryPathAccepted()
{
    var validator = new InputValidationService();
    var result = validator.ValidateDirectoryPath(@"C:\HyperV\VMs", "Ruta");
    Assert(result.Severity == CheckSeverity.Ok, "Absolute Windows path should be accepted syntactically.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
