using System.Text.Json;
using Yapair.Core.Verification;

namespace Yapair.Core.Serialization;

/// <summary>Serialization for the verification result contract (schema v1.0).</summary>
public static class VerificationJson
{
    public static string Serialize(VerificationResult result, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, indented ? SnapshotJson.IndentedOptions : SnapshotJson.Options);
    }

    public static VerificationResult Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<VerificationResult>(json, SnapshotJson.Options)
            ?? throw new InvalidOperationException("Verification JSON did not contain a result.");
    }
}
