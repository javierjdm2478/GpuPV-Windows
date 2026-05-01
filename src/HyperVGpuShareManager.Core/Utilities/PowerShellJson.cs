using System.Text.Json;

namespace HyperVGpuShareManager.Core.Utilities;

public static class PowerShellJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<T> DeserializeArray<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<T>();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>(),
            JsonValueKind.Object => new List<T> { JsonSerializer.Deserialize<T>(json, Options)! },
            _ => Array.Empty<T>()
        };
    }

    public static T? DeserializeObject<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
