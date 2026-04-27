using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CodeAlta.Acp;

namespace CodeAlta.Agent.Acp;

internal static class AcpJsonHelpers
{
    internal static string? GetDiscriminator(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    internal static string? GetStringValue(JsonElement wrapper)
    {
        return wrapper.ValueKind == JsonValueKind.String ? wrapper.GetString() : null;
    }

    internal static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    internal static T? DeserializeAcp<T>(JsonElement element)
    {
        return element.Deserialize(GetAcpJsonTypeInfo<T>());
    }

    internal static JsonElement SerializeAcpToElement<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value, GetAcpJsonTypeInfo<T>());
    }

    internal static JsonElement CreateElement(Action<Utf8JsonWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    internal static JsonElement CreateStringElement(string value)
    {
        return CreateElement(writer => writer.WriteStringValue(value));
    }

    internal static JsonElement CreateObjectElement(IEnumerable<KeyValuePair<string, JsonElement>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return CreateElement(writer =>
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        });
    }

    internal static JsonElement CreateBooleanElement(bool value)
    {
        return CreateElement(writer => writer.WriteBooleanValue(value));
    }

    internal static JsonElement CreateNumberElement(long value)
    {
        return CreateElement(writer => writer.WriteNumberValue(value));
    }

    internal static JsonElement CreateNumberElement(double value)
    {
        return CreateElement(writer => writer.WriteNumberValue(value));
    }

    private static JsonTypeInfo<T> GetAcpJsonTypeInfo<T>()
    {
        var options = AcpClient.CreateJsonSerializerOptions();
        var typeInfo = options.GetTypeInfo(typeof(T));
        if (typeInfo is JsonTypeInfo<T> typedTypeInfo)
        {
            return typedTypeInfo;
        }

        throw new InvalidOperationException($"ACP JSON metadata is not available for type '{typeof(T).FullName}'.");
    }
}
