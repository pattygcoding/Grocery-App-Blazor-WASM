using System.Globalization;
using GroceryApp;
using GroceryApp.Data;
using GroceryApp.Data.Repositories;
using GroceryApp.Data.State;
using GroceryApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The demo ships USD prices in its seed data, so the culture is pinned instead of formatting
// currency and dates according to each visitor's browser locale.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The catalogue is kept in memory for the session and persisted to localStorage, so it is
// restored on the next visit.
builder.Services.AddSingleton<IGroceryStore, BrowserLocalStorageStore>();
builder.Services.AddSingleton<IGroceryRepository, GroceryRepository>();
builder.Services.AddSingleton<IGroceryService, GroceryService>();
builder.Services.AddSingleton<IExcelExportService, XlsxExportService>();
builder.Services.AddSingleton<IInventoryAnalyticsService, InventoryAnalyticsService>();
builder.Services.AddSingleton<IInventoryQueryService, InventoryQueryService>();
builder.Services.AddSingleton<IActivityService, ActivityService>();
builder.Services.AddSingleton<IDemoScenarioService, DemoScenarioService>();
builder.Services.AddSingleton<ICatalogueHistoryService, CatalogueHistoryService>();
builder.Services.AddSingleton<ICommandIndexService, CommandIndexService>();
builder.Services.AddSingleton<IPreferenceService, PreferenceService>();
builder.Services.AddSingleton<IActivityQueryService, ActivityQueryService>();
builder.Services.AddSingleton<ISystemDiagnosticsService, SystemDiagnosticsService>();
builder.Services.AddSingleton<ISupplierAnalyticsService, SupplierAnalyticsService>();
builder.Services.AddSingleton<IInventoryImportService, InventoryImportService>();

// Everything the app remembers beyond the catalogue - history, preferences, saved views, pinned
// items - shares one keyed store, and each concern gets its own typed view over a single key.
builder.Services.AddSingleton<IAppStateStore, BrowserAppStateStore>();
builder.Services.AddSingleton<UserPreferencesStore>();
builder.Services.AddSingleton<ActivityLogStore>();
builder.Services.AddSingleton<SavedViewStore>();
builder.Services.AddSingleton<FavouriteItemStore>();
builder.Services.AddSingleton<RecentlyViewedStore>();

await builder.Build().RunAsync();
