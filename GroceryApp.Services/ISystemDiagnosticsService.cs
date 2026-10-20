using GroceryApp.Models.DTOs;

namespace GroceryApp.Services;

/// <summary>
/// Reports what the application has stored in this browser, and can put it all back.
/// </summary>
/// <remarks>
/// A client only application holds everything on the visitor's own machine, so "where is my data?" deserves
/// an answer on a page rather than in a support thread. The report is assembled from the same services the
/// rest of the application uses, so it cannot describe a state the application is not in.
/// </remarks>
public interface ISystemDiagnosticsService
{
    /// <summary>
    /// Collects the current state of the browser storage the application owns.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the report.</returns>
    Task<DiagnosticsReport> CollectAsync();

    /// <summary>
    /// Removes everything the application has stored: the catalogue, the log, the preferences and the rest.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// The catalogue is seeded again afterwards, so the application is left in the state a first visit finds
    /// it in rather than in an empty one. Anything holding a cached copy has to be reloaded afterwards, which
    /// is why the page that offers this reloads the application.
    /// </remarks>
    Task ClearEverythingAsync();
}