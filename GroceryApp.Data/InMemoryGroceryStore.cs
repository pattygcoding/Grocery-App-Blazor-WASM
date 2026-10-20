namespace GroceryApp.Data;

/// <summary>
/// In-memory <see cref="IGroceryStore"/> implementation.
/// Used by unit tests and whenever browser storage is unavailable (for example private
/// browsing modes that block localStorage). The application keeps working, the data simply
/// does not survive a page reload.
/// </summary>
public sealed class InMemoryGroceryStore : JsonGroceryStore
{
    private string? _payload;

    /// <summary>
    /// Gets the raw JSON payload currently held in memory (null when empty).
    /// </summary>
    public string? Payload => _payload;

    /// <inheritdoc />
    protected override Task<string?> ReadRawAsync() => Task.FromResult(_payload);

    /// <inheritdoc />
    protected override Task WriteRawAsync(string value)
    {
        _payload = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task RemoveRawAsync()
    {
        _payload = null;
        return Task.CompletedTask;
    }
}
