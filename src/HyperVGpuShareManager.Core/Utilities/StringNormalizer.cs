namespace HyperVGpuShareManager.Core.Utilities;

public static class StringNormalizer
{
    public static string NormalizeDevicePath(string value) =>
        (value ?? string.Empty)
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("#", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
}
