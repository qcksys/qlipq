using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qlipq.Core;

/// <summary>
/// Shared System.Text.Json conventions for everything qlipq persists, matching the
/// camelCase IPC/JSON contract the original Tauri app used. Reusing these options keeps
/// <c>config.json</c> and <c>edits.json</c> byte-compatible with files written by the old app.
/// </summary>
public static class QlipqJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static readonly JsonSerializerOptions IndentedOptions = new(Options)
    {
        WriteIndented = true,
    };
}
