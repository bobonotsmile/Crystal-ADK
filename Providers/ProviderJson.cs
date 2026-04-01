using System.Text.Json;

namespace Crystal.Adk.Providers;

internal static class ProviderJson
{
    public static Dictionary<string, object?> ParseArguments(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return ConvertObject(doc.RootElement);
            }
        }
        catch
        {
            // 有些 provider 返回的 arguments 可能是不完整片段，
            // 或者根本不是对象；这里保留原始文本，方便上层继续判断。
            return new Dictionary<string, object?> { ["__raw"] = text };
        }

        return new Dictionary<string, object?> { ["__raw"] = text };
    }

    public static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        // 把 JsonElement 归一化成普通 CLR 值，
        // 这样 runtime / tool 代码就不用直接处理 provider 的 JSON 节点对象。
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertValue(property.Value);
        }

        return dict;
    }

    public static object? ConvertValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Object => ConvertObject(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
        _ => null
    };
}
