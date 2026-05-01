using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Validation;

public sealed class InputValidationService
{
    private static readonly char[] InvalidVmNameChars = ['"', '\'', '`', '|', '<', '>', '\r', '\n'];

    public ValidationResult ValidateVmName(string? vmName)
    {
        if (string.IsNullOrWhiteSpace(vmName))
        {
            return ValidationResult.Error("Nombre de VM requerido", "Introduce el nombre de una máquina virtual.");
        }

        if (vmName.IndexOfAny(InvalidVmNameChars) >= 0)
        {
            return ValidationResult.Error("Nombre de VM no válido", "Evita comillas, saltos de línea y caracteres de shell en el nombre de la VM.");
        }

        return vmName.Length > 100
            ? ValidationResult.Warning("Nombre largo", "Hyper-V acepta nombres largos, pero conviene mantenerlos fáciles de leer.")
            : ValidationResult.Ok("Nombre de VM válido", "El nombre puede usarse en comandos parametrizados.");
    }

    public ValidationResult ValidateExistingFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error($"{label} requerido", $"Selecciona un archivo para {label.ToLowerInvariant()}.");
        }

        return File.Exists(path)
            ? ValidationResult.Ok($"{label} encontrado", path)
            : ValidationResult.Error($"{label} no encontrado", $"No existe el archivo: {path}");
    }

    public ValidationResult ValidateDirectoryPath(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error($"{label} requerido", $"Selecciona una carpeta para {label.ToLowerInvariant()}.");
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return ValidationResult.Error($"{label} no válido", "Usa una ruta absoluta.");
        }

        return ValidationResult.Ok($"{label} válido", path);
    }

    public IReadOnlyList<ValidationResult> ValidateGpuPartitionSettings(GpuPartitionSettings settings)
    {
        var results = new List<ValidationResult>();
        ValidateRange("VRAM", settings.MinPartitionVRAM, settings.OptimalPartitionVRAM, settings.MaxPartitionVRAM, results);
        ValidateRange("Encode", settings.MinPartitionEncode, settings.OptimalPartitionEncode, settings.MaxPartitionEncode, results);
        ValidateRange("Decode", settings.MinPartitionDecode, settings.OptimalPartitionDecode, settings.MaxPartitionDecode, results);
        ValidateRange("Compute", settings.MinPartitionCompute, settings.OptimalPartitionCompute, settings.MaxPartitionCompute, results);
        return results.Count == 0
            ? new[] { ValidationResult.Ok("Recursos GPU válidos", "Los valores mínimos, óptimos y máximos están ordenados.") }
            : results;
    }

    public IReadOnlyList<ValidationResult> ValidateApplyPrerequisites(SystemStatus status, GpuInfo? gpu, VmInfo? vm, bool hyperVCmdletsAvailable)
    {
        var results = new List<ValidationResult>();

        if (!status.IsAdministrator)
        {
            results.Add(ValidationResult.Error("Administrador requerido", "La app debe ejecutarse elevada para administrar Hyper-V."));
        }

        if (!status.IsHyperVInstalled || !status.IsHyperVEnabled)
        {
            results.Add(ValidationResult.Error("Hyper-V no está listo", "Instala y habilita Hyper-V antes de configurar GPU-P."));
        }

        if (!hyperVCmdletsAvailable)
        {
            results.Add(ValidationResult.Error("Cmdlets Hyper-V no disponibles", "No se han encontrado los cmdlets de Hyper-V requeridos."));
        }

        if (gpu is null)
        {
            results.Add(ValidationResult.Error("GPU no seleccionada", "Selecciona una GPU física del host."));
        }
        else if (!gpu.IsPartitionable)
        {
            results.Add(ValidationResult.Error("GPU no particionable", "Get-VMHostPartitionableGpu no devuelve esta GPU. El driver o el hardware pueden no soportar GPU Partitioning."));
        }

        if (vm is null)
        {
            results.Add(ValidationResult.Error("VM no seleccionada", "Selecciona o crea una VM de Hyper-V."));
        }
        else if (!vm.IsOff)
        {
            results.Add(ValidationResult.Error("VM encendida", "Apaga la VM antes de aplicar GPU-P. La app puede intentar un apagado ordenado desde el botón de VM."));
        }

        return results.Count == 0
            ? new[] { ValidationResult.Ok("Listo para aplicar", "Las validaciones críticas han pasado.") }
            : results;
    }

    private static void ValidateRange(string label, ulong min, ulong optimal, ulong max, ICollection<ValidationResult> results)
    {
        if (min > optimal || optimal > max)
        {
            results.Add(ValidationResult.Error($"{label} no válido", $"Debe cumplirse Min <= Optimal <= Max para {label}."));
        }
    }
}
