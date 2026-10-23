using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Yapair.Core.Contracts;

namespace Yapair.Core.Serialization;

/// <summary>
/// Single serialization configuration for the snapshot contract. snake_case is
/// used everywhere so the JSON Schema is language-neutral and stable.
/// </summary>
public static class SnapshotJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions(indented: false);

    public static JsonSerializerOptions IndentedOptions { get; } = CreateOptions(indented: true);

    public static string Serialize(SystemSnapshot snapshot, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, indented ? IndentedOptions : Options);
    }

    public static JsonNode ToNode(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.SerializeToNode(snapshot, Options)
            ?? throw new InvalidOperationException("Snapshot serialization produced no JSON node.");
    }

    public static SystemSnapshot Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<SystemSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Snapshot JSON did not contain a snapshot.");
    }

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
