using System.Text.Json.Serialization;
using GroceryApp.Models;

namespace GroceryApp.Data;

/// <summary>
/// Source generated serializer context for the application state that is stored beside the
/// catalogue.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="GroceryJsonContext"/> so the catalogue payload and its serializer
/// are untouched by everything added around it. Generating the serializers up front keeps
/// serialization working in the trimmed, browser (WebAssembly) build where reflection based JSON is
/// not available.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false,
    Converters = new[]
    {
        typeof(JsonStringEnumConverter<UnitOfMeasure>),
        typeof(JsonStringEnumConverter<ActivityKind>),
        typeof(JsonStringEnumConverter<TableDensity>)
    })]
[JsonSerializable(typeof(UserPreferences))]
[JsonSerializable(typeof(List<ActivityEntry>))]
[JsonSerializable(typeof(List<SavedView>))]
[JsonSerializable(typeof(List<Supplier>))]
[JsonSerializable(typeof(List<int>))]
internal sealed partial class AppStateJsonContext : JsonSerializerContext
{
}
