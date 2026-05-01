# Hyper-V GPU Share Manager

Aplicación de escritorio para Windows que ayuda a configurar GPU Partitioning / GPU-P en Hyper-V, compartiendo una GPU física del host con una VM sin retirar la GPU del host. No usa RemoteFX.

## Estructura

```text
HyperVGpuShareManager/
  HyperVGpuShareManager.sln
  src/
    HyperVGpuShareManager.App/       WPF, MVVM, temas, assets locales
    HyperVGpuShareManager.Core/      modelos, servicios, validacion, logging
  tests/
    HyperVGpuShareManager.Tests/     pruebas basicas sin dependencias externas
```

Modelos principales:

- `GpuInfo`
- `VmInfo`
- `ValidationResult`
- `GpuPartitionSettings`

Servicios principales:

- `PowerShellService`
- `HyperVService`
- `GpuDetectionService`
- `VmCreationService`
- `DriverPreparationService`
- `LoggingService`

## Requisitos

- Windows 11 Pro, Enterprise o Education como host.
- Hyper-V instalado y habilitado.
- .NET 8 SDK o superior para compilar.
- Ejecutar como administrador.
- GPU y driver compatibles con GPU Partitioning. La app valida `Get-VMHostPartitionableGpu`, pero no promete soporte universal.
- VM objetivo recomendada: Windows 10 Pro, Generación 2.

Cmdlets oficiales usados cuando están disponibles:

- `Get-VMHostPartitionableGpu`
- `Add-VMGpuPartitionAdapter`
- `Get-VMGpuPartitionAdapter`
- `Set-VMGpuPartitionAdapter`
- `Remove-VMGpuPartitionAdapter`
- `Set-VM`
- `New-VM`, `New-VHD`, `Set-VMProcessor`, `Set-VMMemory`, `Add-VMDvdDrive`, `Set-VMFirmware`

## Compilar

```powershell
cd .\HyperVGpuShareManager
dotnet build .\HyperVGpuShareManager.sln -c Release
```

Para publicar un ejecutable:

```powershell
dotnet publish .\src\HyperVGpuShareManager.App\HyperVGpuShareManager.App.csproj -c Release -r win-x64 --self-contained false
```

## Ejecutar como administrador

El manifiesto de la app solicita elevación y, además, la app intenta relanzarse con `runas` si detecta que no está elevada.

```powershell
.\src\HyperVGpuShareManager.App\bin\Release\net8.0-windows\win-x64\publish\Hyper-V GPU Share Manager.exe
```

## Flujo de uso

1. Instalar Hyper-V desde Características de Windows o PowerShell y reiniciar si Windows lo pide.
2. Ejecutar la app como administrador.
3. Seleccionar una GPU. Debe aparecer como particionable mediante `Get-VMHostPartitionableGpu`.
4. Crear una VM nueva de Windows 10 Pro o seleccionar una VM existente apagada.
5. Revisar o restaurar los valores recomendados de GPU-P.
6. Pulsar **Aplicar GPU-P ahora**. La app configura el adaptador GPU-P y los espacios MMIO recomendados.
7. Preparar/verificar drivers dentro de la VM. La copia asistida monta el VHDX offline si la VM esta apagada, o usa PowerShell Direct si la VM esta encendida y proporcionas credenciales de administrador del guest.
8. Validar con el botón **Validar configuracion**.
9. Dentro de Windows 10 Pro VM, comprobar `dxdiag` o Administrador de dispositivos.

## Logs y diagnostico

Los logs se guardan en:

```text
%ProgramData%\HyperVGpuShareManager\logs
```

La UI muestra logs en tiempo real y permite exportar un ZIP de diagnostico.

## Notas de compatibilidad

GPU-P funciona solo si hardware, driver y Windows lo soportan. Si `Get-VMHostPartitionableGpu` no devuelve la GPU, la app no aplica cambios y muestra una explicación. En ese caso conviene revisar:

- Driver actualizado del fabricante.
- Windows 11 Pro actualizado.
- BIOS/UEFI con virtualización habilitada.
- Compatibilidad real de GPU-P para el modelo concreto.

## Seguridad

- No se descargan drivers automáticamente.
- Los nombres de VM se validan y los comandos PowerShell usan parámetros.
- Antes de cambios destructivos puede crearse un checkpoint.
- Si la VM está encendida, la app bloquea la aplicación de GPU-P hasta que se apague.

## Documentacion Microsoft

- [Partition and assign GPUs to a VM](https://learn.microsoft.com/windows-server/virtualization/hyper-v/partition-assign-vm-gpu)
- [Add-VMGpuPartitionAdapter](https://learn.microsoft.com/powershell/module/hyper-v/add-vmgpupartitionadapter)
- [Set-VMGpuPartitionAdapter](https://learn.microsoft.com/powershell/module/hyper-v/set-vmgpupartitionadapter)
- [Get-VMHostPartitionableGpu](https://learn.microsoft.com/powershell/module/hyper-v/get-vmhostpartitionablegpu)
