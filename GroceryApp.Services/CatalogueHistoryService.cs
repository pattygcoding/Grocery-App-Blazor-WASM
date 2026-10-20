using GroceryApp.Data.Repositories;
using GroceryApp.Models;

namespace GroceryApp.Services;

/// <summary>
/// Keeps a bounded stack of catalogue snapshots so a change can be put back.
/// </summary>
/// <remarks>
/// The snapshots hold detached copies, which the repository already hands out, so nothing in the stack can
/// be changed underneath it by a later edit.
/// </remarks>
public class CatalogueHistoryService : ICatalogueHistoryService
{
    /// <summary>
    /// How many steps are kept.
    /// </summary>
    /// <remarks>
    /// Fifty steps of a fifty line catalogue is a few hundred kilobytes at most, and it is the number beyond
    /// which a visitor is unlikely to be walking back by hand rather than fixing the thing directly.
    /// </remarks>
    public const int MaxDepth = 50;

    private readonly IGroceryRepository _repository;
    private readonly IActivityService _activity;

    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();

    /// <summary>
    /// How many changes are running right now. Anything above zero means the current change is part of a
    /// change that is already being recorded.
    /// </summary>
    private int _running;

    /// <summary>
    /// Initializes a new instance of the CatalogueHistoryService class.
    /// </summary>
    /// <param name="repository">The catalogue that is snapshotted and restored.</param>
    /// <param name="activity">The log an undo or redo is recorded in.</param>
    public CatalogueHistoryService(IGroceryRepository repository, IActivityService activity)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public string? NextUndoLabel => _undo.Count == 0 ? null : _undo[^1].Label;

    /// <inheritdoc />
    public string? NextRedoLabel => _redo.Count == 0 ? null : _redo[^1].Label;

    /// <inheritdoc />
    public int UndoDepth => _undo.Count;

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public async Task RunAsync(string label, Func<Task> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        await RunAsync<object?>(label, async () =>
        {
            await change();
            return null;
        });
    }

    /// <inheritdoc />
    public async Task<TResult> RunAsync<TResult>(string label, Func<Task<TResult>> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        // A change made while another change is running is part of it, and the outermost call names the step.
        // Without this, a restock written through a service that records its own edits would take one undo per
        // line rather than one for the order.
        if (_running > 0)
        {
            _running += 1;

            try
            {
                return await change();
            }
            finally
            {
                _running -= 1;
            }
        }

        // The catalogue as it stands, which is what an undo has to put back.
        var before = (await _repository.GetAllAsync()).ToList();

        _running = 1;

        TResult result;

        try
        {
            result = await change();
        }
        finally
        {
            // A change that throws is not recorded, so the step is not created and the exception travels on.
            _running = 0;
        }

        _undo.Add(new Snapshot(label, before));

        if (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
        }

        // A new change abandons the redo branch, which is what every editor does and what stops a redo from
        // reapplying a change against a catalogue that has moved on since.
        _redo.Clear();
        Changed?.Invoke();

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> UndoAsync()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        // What is there now becomes the step that a redo would put back.
        var current = (await _repository.GetAllAsync()).ToList();
        await _repository.ReplaceAllAsync(snapshot.Items);
        _redo.Add(new Snapshot(snapshot.Label, current));

        await _activity.RecordCatalogueAsync(ActivityKind.Undone, $"Undid {snapshot.Label}");
        Changed?.Invoke();

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RedoAsync()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var snapshot = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        var current = (await _repository.GetAllAsync()).ToList();
        await _repository.ReplaceAllAsync(snapshot.Items);
        _undo.Add(new Snapshot(snapshot.Label, current));

        await _activity.RecordCatalogueAsync(ActivityKind.Redone, $"Redid {snapshot.Label}");
        Changed?.Invoke();

        return true;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();

        return Task.CompletedTask;
    }

    /// <summary>
    /// One step: what it was called and what the catalogue looked like before it.
    /// </summary>
    private sealed record Snapshot(string Label, List<GroceryItem> Items);
}
