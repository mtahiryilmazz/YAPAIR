using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Yapair.Core.Serialization;

internal static class JsonSchemaValidator
{
    public static SchemaValidationResult Validate(JsonSchema schema, JsonNode node)
    {
        var evaluation = schema.Evaluate(
            JsonSerializer.SerializeToElement(node),
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

        if (evaluation.IsValid)
        {
            return new SchemaValidationResult(true, []);
        }

        var errors = new List<string>();
        CollectErrors(evaluation, errors);
        if (errors.Count == 0)
        {
            errors.Add("Schema evaluation failed without a specific error.");
        }

        return new SchemaValidationResult(false, errors);
    }

    private static void CollectErrors(EvaluationResults result, List<string> errors)
    {
        if (result.Errors is { Count: > 0 })
        {
            foreach (var (keyword, message) in result.Errors)
            {
                errors.Add($"{result.InstanceLocation} [{keyword}] {message}");
            }
        }

        foreach (var detail in result.Details ?? [])
        {
            CollectErrors(detail, errors);
        }
    }
}
