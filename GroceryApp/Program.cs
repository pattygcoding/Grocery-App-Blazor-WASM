using GroceryApp;
using GroceryApp.Data;
using GroceryApp.Data.Repositories;
using GroceryApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Everything runs in the browser: there is no server, no database and no API.
// The catalogue is kept in memory for the session and persisted to localStorage.
builder.Services.AddSingleton<IGroceryStore, BrowserLocalStorageStore>();
builder.Services.AddSingleton<IGroceryRepository, GroceryRepository>();
builder.Services.AddSingleton<IGroceryService, GroceryService>();
builder.Services.AddSingleton<IExcelExportService, XlsxExportService>();

await builder.Build().RunAsync();
