using System.Text.Json;
using System.Text.Json.Serialization;

namespace ASFItemDropper;

public class ItemList
{
    [JsonPropertyName("accountid")]
    public string? Accountid { get; set; }

    [JsonPropertyName("itemid")]
    public string? Itemid { get; set; }

    [JsonPropertyName("quantity")]
    public long Quantity { get; set; }

    [JsonPropertyName("originalitemid")]
    public string? Originalitemid { get; set; }

    [JsonPropertyName("itemdefid")]
    [JsonConverter(typeof(ParseStringConverter))]
    public long Itemdefid { get; set; }

    [JsonPropertyName("appid")]
    public long Appid { get; set; }

    [JsonPropertyName("acquired")]
    public string? Acquired { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("origin")]
    public string? Origin { get; set; }

    [JsonPropertyName("state_changed_timestamp")]
    public string? StateChangedTimestamp { get; set; }

    public static ItemList[] FromJson(string json)
    {
        return JsonSerializer.Deserialize<ItemList[]>(json, Converter.Settings) ?? [];
    }
}

public static class Serialize
{
    public static string ToJson(this ItemList[] self) => JsonSerializer.Serialize(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerOptions Settings = new JsonSerializerOptions
    {
        Converters = { new ParseStringConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

internal class ParseStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return 0;
        var value = reader.GetString();
        if (long.TryParse(value, out var result))
            return result;
        throw new JsonException("Cannot unmarshal type long");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}