# Grocery Shopping App - Blazor WebAssembly (front-end only)

A **front-end only** Blazor WebAssembly application for managing grocery items, and the
WebAssembly port of [Grocery-App-Blazor](https://github.com/pattygcoding/Grocery-App-Blazor)
(which is a full-stack Blazor Server app with EF Core and SQLite).

There is **no server, no database and no API**: C# runs in the browser via WebAssembly, the
catalogue is persisted to the browser's `localStorage`, and the Excel report is generated
client side. The whole thing is published as static files to GitHub Pages.

🔗 **Live demo:** <https://blazor.pattygcoding.com>

## What changed compared to the Blazor Server original

| Concern | Blazor Server version | This Blazor WebAssembly version |
| --- | --- | --- |
| Hosting model | ASP.NET Core server, interactive Server render mode | Standalone WebAssembly app (static files) |
| Persistence | EF Core 9 + SQLite database | `localStorage` through JS interop, seeded from an embedded `SeedData.json` |
| Data access | `DbContext` + repository | Repository over a browser storage abstraction (`IGroceryStore`) |
| Excel export | ClosedXML + REST endpoint `/api/reports/export-excel` | `XlsxExportService`, a dependency-free Open XML writer, downloads the file from the browser |
| API endpoints | `GroceriesController`, `ReportsController`, Swagger | Not present (nothing to talk to) |
| Client side routing | Server side routing | Static host + `404.html` fallback so deep links and refreshes work on GitHub Pages |
| Tests | Service layer (xUnit + NSubstitute) | Same service tests, plus repository/storage and workbook generation tests |

Everything else - the domain model, DTOs, service layer, pages, layout and styling - is kept
aligned with the original application.

## Features

- **Add Grocery Items**: Create new grocery items with name, category, price, cost to produce, and stock information
- **List Grocery Items**: View all grocery items organized by category
- **View Item Details**: Display detailed information about specific grocery items
- **Edit Grocery Items**: Update existing grocery items (including cost to produce)
- **Delete Grocery Items**: Remove grocery items with confirmation
- **Stock Controls**: Increment or decrement stock directly from the grocery list
- **Reports Dashboard**: Profit analysis, stock levels, price ranges and category breakdowns with sortable tables
- **Excel Export**: 5 sheet workbook generated in the browser (Key Metrics, Profit by Category, Top Profitable Items, Stock Analysis, Price Range Analysis)
- **Browser Persistence**: The catalogue is stored in `localStorage` and survives page reloads
- **Reset Demo Data**: Restore the original seeded catalogue from the home page
- **Seeded Data**: Application starts with 50 pre-loaded grocery items across 5 categories

## Domain Model

- **Id**: Unique identifier (auto-generated)
- **Name**: Item name (required, 1-100 characters)
- **Category**: One of Fruit, Vegetable, Meat, Cheese, or Bread
- **Price**: Item price (must be > 0)
- **Cost to Produce**: Production cost (must be > 0)
- **Stock**: Available quantity (default: 10, cannot be negative)
- **CreatedUtc**: Creation timestamp (auto-generated)

## Architecture

```
GroceryApp/               Blazor WebAssembly app (root component, router, pages, layout, static assets)
GroceryApp.Models/        Domain models, enum and DTOs
GroceryApp.Data/          Browser storage abstraction (IGroceryStore) + repository pattern
GroceryApp.Services/      Business logic (IGroceryService) and the client side XLSX export
GroceryApp.Tests/         xUnit + NSubstitute test suite
```

Data flow: `Pages` → `IGroceryService` → `IGroceryRepository` → `IGroceryStore`.

- `GroceryRepository` keeps the catalogue in memory for the session and writes the whole payload
  through the store on every change.
- `BrowserLocalStorageStore` (in the WebAssembly app) implements the store with JS interop and
  falls back to memory when a browser blocks `localStorage`.
- On first use the repository seeds the catalogue from `SeedData.json`, which is embedded in
  `GroceryApp.Data.dll`.

## Tech Stack

- .NET 8 / C# 12
- Blazor WebAssembly (standalone)
- `System.Text.Json` source generated serialization (trim safe)
- JS interop for `localStorage` and file downloads
- Hand written Open XML (.xlsx) generation - no ClosedXML, no server
- Bootstrap 5 for styling
- xUnit + NSubstitute for testing
- GitHub Actions + GitHub Pages for hosting

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Any IDE that supports .NET development (Visual Studio, VS Code, JetBrains Rider)

## Run locally

```bash
git clone https://github.com/pattygcoding/Grocery-App-Blazor-WASM.git
cd Grocery-App-Blazor-WASM

dotnet restore
dotnet build

cd GroceryApp
dotnet run
```

Then open the URL printed by the dev server (for example `https://localhost:7033` or
`http://localhost:5073`).

To run the published (trimmed) output exactly as GitHub Pages serves it:

```bash
dotnet publish GroceryApp/GroceryApp.csproj -c Release -o publish
# serve publish/wwwroot with any static file server
```

## Running tests

```bash
dotnet test
```

The suite covers:

- `GroceryServiceTests` - the service layer, including the tests carried over from the original application
- `GroceryRepositoryTests` - seeding from the embedded resource, ordering, CRUD, persistence and demo reset
- `XlsxExportServiceTests` - the generated workbook package, worksheets, ordering, escaping and number formats
