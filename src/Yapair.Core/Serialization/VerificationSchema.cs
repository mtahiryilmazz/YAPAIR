using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Yapair.Core.Verification;

namespace Yapair.Core.Serialization;

/// <summary>Validates the verification result contract before it is written or shared.</summary>
public static class VerificationSchema
{
    public const string ResourceName = "Yapair.Core.Schemas.verification-result.v1.schema.json";

    public static string SchemaText { get; } = LoadSchemaText();

    public static JsonSchema Schema { get; } = JsonSchema.FromText(SchemaText);

    public static SchemaValidationResult Validate(VerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var node = JsonSerializer.SerializeToNode(result, SnapshotJson.Options)
            ?? throw new InvalidOperationException("Verification result serialization produced no JSON node.");
        return JsonSchemaValidator.Validate(Schema, node);
    }

    private static string LoadSchemaText()
    {
        var assembly = typeof(VerificationSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
