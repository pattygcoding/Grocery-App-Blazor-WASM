namespace GroceryApp.Services;

/// <summary>
/// Makes a change to the catalogue undoable.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is small enough to snapshot whole, so history is a stack of complete copies rather than a
/// set of inverse operations. That is a deliberate trade: it costs memory proportional to the catalogue and
/// the number of steps kept, and in exchange an undo cannot get the catalogue wrong, because it does not
/// have to reason about how to reverse a change - it just puts the previous copy back.
/// </para>
/// <para>
/// Every mutation that should be reversible goes through <see cref="RunAsync"/>, which is what makes a
/// multi-row restock one undo step rather than twenty.
/// </para>
/// </remarks>
public interface ICatalogueHistoryService
{
    /// <summary>True when there is a change that can be undone.</summary>
    bool CanUndo { get; }

    /// <summary>True when a change that was undone can be applied again.</summary>
    bool CanRedo { get; }

    /// <summary>What the next undo would put back, or null when there is nothing to undo.</summary>
    string? NextUndoLabel { get; }

    /// <summary>What the next redo would apply again, or null when there is nothing to redo.</summary>
    string? NextRedoLabel { get; }

    /// <summary>How many undo steps are available.</summary>
    int UndoDepth { get; }

    /// <summary>Raised whenever the history changes, so a control can show or hide itself.</summary>
    event Action? Changed;

    /// <summary>
    /// Runs a change to the catalogue as one undoable step.
    /// </summary>
    /// <param name="label">What the change was, in the words the undo control should use.</param>
    /// <param name="change">The change itself.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The label is taken before the change runs, so an undo reads as "undo restocking eight lines" rather
    /// than as "undo something". If the change throws, nothing is recorded and the exception propagates.
    /// </remarks>
    Task RunAsync(string label, Func<Task> change);

    /// <summary>
    /// Runs a change to the catalogue as one undoable step, and hands back what the change returned.
    /// </summary>
    /// <typeparam name="TResult">What the change returns.</typeparam>
    /// <param name="label">What the change was, in the words the undo control should use.</param>
    /// <param name="change">The change itself.</param>
    /// <returns>A task whose result is what the change returned.</returns>
    /// <remarks>
    /// A change made while another change is already running is part of that one rather than a step of its
    /// own. That is what lets a service record its own edits as steps without a page that calls several of
    /// them having to work its way around them.
    /// </remarks>
    Task<TResult> RunAsync<TResult>(string label, Func<Task<TResult>> change);

    /// <summary>
    /// Puts the catalogue back the way it was before the most recent change.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result says whether there was anything to undo.</returns>
    Task<bool> UndoAsync();

    /// <summary>
    /// Applies again the change that was most recently undone.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result says whether there was anything to redo.</returns>
    Task<bool> RedoAsync();

    /// <summary>
    /// Forgets the whole history, for example after the catalogue has been replaced wholesale.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearAsync();
}
