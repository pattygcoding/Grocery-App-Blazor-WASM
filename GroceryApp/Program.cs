using System.Globalization;
using GroceryApp;
using GroceryApp.Data;
using GroceryApp.Data.Repositories;
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

await builder.Build().RunAsync();
