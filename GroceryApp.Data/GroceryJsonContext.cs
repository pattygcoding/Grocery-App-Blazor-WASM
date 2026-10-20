using System.Text.Json.Serialization;
using GroceryApp.Models;

namespace GroceryApp.Data;

/// <summary>
/// Source generated serializer context used by the data layer.
/// Generating the serializers up front keeps serialization working in the trimmed,
/// browser (WebAssembly) build where reflection based JSON is not available.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false,
    Converters = new[]
    {
        typeof(JsonStringEnumConverter<Category>),
        typeof(JsonStringEnumConverter<UnitOfMeasure>)
    })]
[JsonSerializable(typeof(List<GroceryItem>))]
[JsonSerializable(typeof(List<SeedGroceryItem>))]
internal sealed partial class GroceryJsonContext : JsonSerializerContext
{
}
