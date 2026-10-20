using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GroceryApp.Data;

/// <summary>
/// Typed view over a single key in an <see cref="IAppStateStore"/>.
/// </summary>
/// <typeparam name="T">The payload type. Always a reference type, so a missing payload can be reported as null.</typeparam>
/// <remarks>
/// The serializer metadata is injected rather than discovered, which keeps serialization working in
/// the trimmed WebAssembly build where reflection based JSON is not available. A payload written by
/// an older build, or one that has been corrupted, is treated as "nothing stored" so a stale record
/// cannot break every page that reads it.
/// </remarks>
public class JsonAppStateStore<T> where T : class
{
    private readonly IAppStateStore _store;
    private readonly JsonTypeInfo<T> _typeInfo;

    /// <summary>
    /// Initializes a new instance of the JsonAppStateStore class.
    /// </summary>
    /// <param name="store">The raw keyed store to read and write through.</param>
    /// <param name="key">The storage key this instance owns.</param>
    /// <param name="typeInfo">Serializer metadata for <typeparamref name="T"/>.</param>
    public JsonAppStateStore(IAppStateStore store, string key, JsonTypeInfo<T> typeInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        _typeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
    }

    /// <summary>Gets the storage key this instance owns.</summary>
    public string Key { get; }

    /// <summary>
    /// Reads the payload.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the payload, or null when nothing usable is stored.</returns>
    public async Task<T?> LoadAsync()
    {
        var json = await _store.ReadAsync(Key);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, _typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the payload, replacing any previous value.
    /// </summary>
    /// <param name="value">The payload to store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SaveAsync(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _store.WriteAsync(Key, JsonSerializer.Serialize(value, _typeInfo));
    }

    /// <summary>
    /// Removes the payload so the next read reports nothing stored.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ClearAsync() => _store.RemoveAsync(Key);

    /// <summary>
    /// Measures the stored payload so the diagnostics page can show how much of the browser's
    /// storage each concern is using.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the payload length in characters, or 0 when nothing is stored.</returns>
    public async Task<int> SizeAsync() => (await _store.ReadAsync(Key))?.Length ?? 0;
}
