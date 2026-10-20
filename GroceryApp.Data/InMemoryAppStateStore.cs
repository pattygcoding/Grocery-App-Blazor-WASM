namespace GroceryApp.Data;

/// <summary>
/// In-memory <see cref="IAppStateStore"/> implementation.
/// </summary>
/// <remarks>
/// Used by unit tests and whenever browser storage is unavailable (for example private browsing
/// modes that block localStorage). The application keeps working and everything the visitor does
/// holds together for the session; only persistence across a reload is lost.
/// </remarks>
public sealed class InMemoryAppStateStore : IAppStateStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the stored key/value pairs, so a test can assert on what was written.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <inheritdoc />
    public Task<string?> ReadAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    }

    /// <inheritdoc />
    public Task WriteAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        _values[key] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _values.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> KeysAsync()
    {
        IReadOnlyList<string> keys = _values.Keys
            .Where(key => key.StartsWith(AppStateKeys.Prefix, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(keys);
    }
}
