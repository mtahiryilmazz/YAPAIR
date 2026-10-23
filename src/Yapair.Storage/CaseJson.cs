using System.Text.Json;
using Yapair.Core.Serialization;

namespace Yapair.Storage;

public static class CaseJson
{
    public static string Serialize<T>(T value, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, indented ? SnapshotJson.IndentedOptions : SnapshotJson.Options);
    }
}
