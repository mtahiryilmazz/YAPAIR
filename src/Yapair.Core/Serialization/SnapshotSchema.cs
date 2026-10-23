using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Yapair.Core.Contracts;
using Yapair.Core.Serialization;

namespace Yapair.Core.Serialization;

public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public string DescribeErrors() => Errors.Count == 0 ? "valid" : string.Join("; ", Errors);
}

/// <summary>
/// Loads the embedded v1 snapshot schema and validates output before it leaves
/// the process. The schema is a contract, not documentation: a mismatch is a
/// release blocker.
/// </summary>
public static class SnapshotSchema
{
    public const string ResourceName = "Yapair.Core.Schemas.system-snapshot.v1.schema.json";

    public static string SchemaText { get; } = LoadSchemaText();

    public static JsonSchema Schema { get; } = JsonSchema.FromText(SchemaText);

    public static SchemaValidationResult Validate(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Validate(SnapshotJson.ToNode(snapshot));
    }

    public static SchemaValidationResult Validate(JsonNode snapshotNode)
    {
        ArgumentNullException.ThrowIfNull(snapshotNode);
        return JsonSchemaValidator.Validate(Schema, snapshotNode);
    }

    private static string LoadSchemaText()
    {
        var assembly = typeof(SnapshotSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
