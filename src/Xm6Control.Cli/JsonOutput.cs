using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xm6Control.Protocol;

namespace Xm6Control.Cli;

internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    public static string SerializeRedacted<T>(T value)
    {
        var node = JsonNode.Parse(Serialize(value))!;
        RedactNode(node);
        return node.ToJsonString(Options);
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToList())
                {
                    var value = obj[key];
                    if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
                    {
                        obj[key] = Redaction.RedactMac(text);
                    }
                    else
                    {
                        RedactNode(value);
                    }
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var value = array[index];
                    if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
                    {
                        array[index] = Redaction.RedactMac(text);
                    }
                    else
                    {
                        RedactNode(value);
                    }
                }

                break;
        }
    }
}
