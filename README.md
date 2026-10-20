# Grocery Shopping App - Blazor WebAssembly

A personal project by [Patrick Goodwin](https://pattygcoding.com): a grocery catalogue written in
C#, which I moved from my earlier
[Grocery-App-Blazor](https://github.com/pattygcoding/Grocery-App-Blazor) app (a full-stack Blazor
Server application with EF Core and SQLite) to a Blazor WebAssembly build.

C# runs in the browser through WebAssembly, the catalogue is kept in the browser's `localStorage`,
and the Excel report is generated client side. The build is published as static files to
GitHub Pages.

**Live demo:** <https://blazor.pattygcoding.com>

## What changed compared to the Blazor Server original

| Concern | Blazor Server version | This Blazor WebAssembly version |
| --- | --- | --- |
| Hosting model | ASP.NET Core server, interactive Server render mode | Standalone WebAssembly app (static files) |
| Persistence | EF Core 9 + SQLite database | `localStorage` through JS interop, seeded from an embedded `SeedData.json` |
| Data access | `DbContext` + repository | Repository over a browser storage abstraction (`IGroceryStore`) |
| Excel export | ClosedXML + REST endpoint `/api/reports/export-excel` | `XlsxExportService`, a dependency-free Open XML writer, downloads the file from the browser |
| Data flow | `GroceriesController` + `ReportsController`, Swagger | Pages call `IGroceryService` directly through dependency injection |
| Client side routing | Server side routing | `404.html` fallback so deep links and refreshes work on GitHub Pages |
| Tests | Service layer (xUnit + NSubstitute) | Same service tests, plus repository/storage and workbook generation tests |

Everything else - the domain model, DTOs, service layer, pages, layout and styling - is kept
aligned with the original application.

## Features

- **Dashboard**: Valuation, profit potential, a thirty day sales comparison, category breakdowns as a
  ring chart, ABC concentration, the restock list, the fastest movers and a running activity feed, all
  worked out from the catalogue and the movements recorded in the browser
- **Inventory analysis**: Stock classification against each item's own reorder point, ABC (Pareto)
  banding by value, measured velocity and days of cover, reorder points and safety stock derived from
  actual demand, urgency scoring, a health score with written findings, and economic order quantity
- **Filtering and sorting**: Free text search across name, SKU, supplier, notes and tags, plus category,
  supplier, tag, stock position, ABC band and archived filters and nine sort columns. The filter lives in
  the address bar, so any view can be bookmarked or shared
- **Restock worksheet**: An urgency-ordered list of what to order, with the quantity suggested per line and
  editable before ordering, the supplier grouping and the cost of the whole order. Applying it adds every
  line to stock, records each one in the log and passes it to the history as one step
- **Undo and redo**: Every catalogue change is one undoable step, including a whole restock. History is a
  bounded stack of catalogue snapshots, so an undo cannot get the catalogue wrong: it puts the previous
  copy back rather than reasoning about how to reverse a change
- **Command palette**: `Ctrl+K` searches every page, every item and the actions worth reaching without
  hunting for them (undo, redo, add an item, restore the demo data), with arrow keys to move and Enter to
  run. An item is found by its code as well as its name
- **Keyboard shortcuts**: `Ctrl+K` for the palette, `/` to search the catalogue, `Ctrl+Z` and
  `Ctrl+Shift+Z` to put a change back or apply it again, `?` for the list, and `Esc` to close whatever is
  open. Undo and redo work from every page, and the page that is showing the catalogue reloads itself
- **Archiving and bulk actions**: Any row can be archived or restored, and a selection of rows can be
  archived, restored or deleted in one step. Archiving is not deleting: the item keeps its code, its history
  and its place in the log, and is simply left out of the catalogue and its figures until it comes back
- **Activity timeline**: Every change the application records, grouped by day, filterable by kind, by free
  text and by whether the entry moved stock. The generated demonstration history is labelled and can be
  filtered out, and the page says how many entries it is not showing
- **Suppliers**: The catalogue grouped by who it is bought from, with the value held, the margin, and how
  many lines need ordering from each. A supplier's items can be opened from the list, and the page leads with
  who is worth a phone call today
- **Import**: A catalogue can be loaded from a pasted CSV or JSON, or a chosen file. Every row is judged
  against the catalogue and shown as new, an update, unchanged or unreadable before anything is written, and
  the whole import is one undoable step
- **Preferences**: Row density, whether archived items are shown by default, whether deleting a row asks
  first, reduced motion and the landing page. They are stored apart from the catalogue, so restoring the
  demo data never resets them
- **About and diagnostics**: What the application is, how much each storage key is holding, what the figures
  are built on, and one button that clears everything this app has stored and starts again
- **Add Grocery Items**: Create new grocery items with name, category, price, cost to produce, stock,
  supplier, unit, reorder point, target margin and tags
- **List Grocery Items**: View the catalogue with per-category counts, colour-coded status, SKU, margin
  against target and units sold per day
- **View Item Details**: Display detailed information about specific grocery items
- **Edit Grocery Items**: Update existing grocery items (including cost to produce)
- **Delete Grocery Items**: Remove grocery items with confirmation
- **Stock Controls**: Increment or decrement stock directly from the grocery list
- **Reports Dashboard**: Profit analysis, stock levels, price ranges and category breakdowns with sortable tables
- **Excel Export**: 8 sheet workbook generated in the browser (Key Metrics, Profit by Category, Top Profitable Items, Stock Analysis, Price Range Analysis, ABC Analysis, Supplier Summary, Items to Deal With)
- **Browser Persistence**: The catalogue and the activity log are stored in `localStorage` and survive page reloads
- **Generated Demonstration History**: A catalogue with no history has nothing to plot, so the first visit
  simulates two months of trading. Every movement it invents is labelled as generated, and it is never
  applied over stock levels that have been changed by hand
- **Reset Demo Data**: Restore the original seeded catalogue from the home page
- **Seeded Data**: Application starts with 50 pre-loaded grocery items across 5 categories

## Domain Model

- **Id**: Unique identifier (auto-generated)
- **Name**: Item name (required, 1-100 characters)
- **Sku**: Stock keeping unit code, generated from the category and identifier when left blank
- **Category**: One of Fruit, Vegetable, Meat, Cheese, or Bread
- **Supplier**: Who it is bought from, held as a name rather than a foreign key because there is no
  database to enforce a relationship and an item has to survive its supplier being renamed
- **Unit**: What the price and the stock count refer to (each, kilogram, litre, loaf, and so on)
- **Price**: Item price (must be > 0)
- **Cost to Produce**: Production cost (must be > 0)
- **Stock**: Available quantity (default: 10, cannot be negative)
- **Reorder Point**: Level at or below which the item needs reordering (default: 5)
- **Target Margin**: The margin the item is meant to achieve, as a percentage of its price (default: 40)
- **Tags**: Free-form labels used by search and the tag filter
- **Notes**: Anything worth remembering about the item (up to 500 characters)
- **IsArchived**: Archived items stay in storage but are hidden from the catalogue and the reports
- **LastRestockedUtc**: When stock was last added, left alone by ordinary edits
- **CreatedUtc**: Creation timestamp (auto-generated)

## Architecture

```
GroceryApp/               Blazor WebAssembly app (root component, router, pages, layout, static assets)
GroceryApp.Models/        Domain models, enums and DTOs
GroceryApp.Data/          Browser storage (IGroceryStore and IAppStateStore) + repositories
GroceryApp.Services/      Business logic: catalogue, inventory analysis, querying, history, XLSX export
GroceryApp.Tests/         xUnit + NSubstitute test suite
```

Data flow: `Pages` → `IGroceryService` → `IGroceryRepository` → `IGroceryStore`.

- `GroceryRepository` keeps the catalogue in memory for the session and writes the whole payload
  through the store on every change.
- `BrowserLocalStorageStore` (in the WebAssembly app) implements the store with JS interop and
  falls back to memory when a browser blocks `localStorage`.
- On first use the repository seeds the catalogue from `SeedData.json`, which is embedded in
  `GroceryApp.Data.dll`.

### The two stores

The catalogue is one document that is always rewritten as a whole, so it has its own contract
(`IGroceryStore`). Everything else the app remembers is small and separately versioned, so those live
behind a keyed store (`IAppStateStore`) with one typed view per concern:

| Key | Holds |
| --- | --- |
| `groceryapp.catalogue.v1` | The catalogue itself |
| `groceryapp.activity.v1` | The append-only movement log, capped and pruned oldest first |
| `groceryapp.prefs.v1` | Interface preferences, kept apart so a demo reset never resets them |
| `groceryapp.theme` | The resolved theme, read by the pre-paint script |

Keys are namespaced so the app can enumerate and clear only its own records: the published demo shares
an origin with the rest of the house sites.

### Analysis and history

`InventoryAnalyticsService` is a pure calculation over the data it is handed, so the same figures serve
the dashboard, the catalogue list and the exported workbook. Every constant it has to assume - days of
safety stock, how many lead times one order covers, the point at which stock counts as overstocked - is
a named constant with the reasoning recorded next to it.

The activity log is the app's only sense of time. Velocity, days of cover, dead stock detection and the
thirty day comparison are all derived from stock movements recorded against items, which is why a
mutation that is not logged is a period the app believes nothing happened in.

Demonstration history is generated by simulating each item day by day rather than by inventing numbers
per chart, which gives the whole thing one property worth having: **an item's stock on hand is exactly
the sum of the movements recorded against it.** Generation is deterministic, seeded from the item's
identifier, so charts do not move between reloads.

### Undo without inverse operations

The catalogue is small enough to snapshot whole, so history is a stack of complete copies rather than a set
of inverse operations: an undo cannot get the catalogue wrong because it does not have to reason about how
to reverse a change, it just puts the previous copy back. Every mutation goes through
`ICatalogueHistoryService.RunAsync`, including the ones inside `GroceryService`, and a change made while
another change is already running is treated as part of it - which is what makes a twenty line restock one
step rather than twenty. Pages subscribe to `Changed` rather than being told, so an undo from the top bar or
from the keyboard refreshes whatever is on screen without the layout having to know what that is.

## UI and design system

The interface is built on a token driven design system in `GroceryApp/wwwroot/app.css`
(colour, type scale, spacing, radii, elevation), so no component hardcodes a colour:

- **Light and dark themes** come from the same tokens and are selectable with a three state control
  in the top bar (Light / Dark / System). The choice is stored in `localStorage` and applied by an
  inline script before first paint, so a dark preference never flashes light; **System** tracks
  `prefers-color-scheme` live, and an explicit choice always wins over the OS setting.
- **Semantic component classes** (`panel`, `stat-tile`, `item-row`, `chip`, `stepper`) rather than
  utility soup, with the Bootstrap components still used by the markup restyled onto the tokens.
- **Iconography** comes from an inlined SVG sprite (`wwwroot/icons.svg`, built from Bootstrap Icons,
  MIT) rendered by the `Icon` component: no icon webfont, no CDN request, and every glyph inherits
  `currentColor` so it follows both themes.
- **Two effects are adapted from [Magic UI](https://magicui.design)**: the hero `dot-pattern`
  (1px dots on a 16px grid in neutral-400/80, radially masked) and `blur-fade`
  (0.4s duration, 0.04s delay, 6px offset, 6px blur) as the single page entrance transition.
  Magic UI ships React/Tailwind components that a Blazor application cannot consume, so the
  design values were reimplemented in plain CSS instead of pulling in a JavaScript dependency.
- **Charts are hand written SVG and CSS** (`DonutChart`, `BarChart`, `Sparkline`): no charting library
  and no CDN request, in the same spirit as the icon sprite. The ring uses the classic trick of a radius
  of 15.9155, which makes the circumference exactly 100 so a share in percent is also its dash length.
  Colour arrives as a class from the design system rather than as a hardcoded value, so the same chart
  draws categories, stock bands or statuses without knowing which it is drawing, and both themes follow
  from that one decision.
- **Accessibility**: measured in the browser at build time - every text pair scores between
  4.56:1 and 17.75:1 (WCAG AA) in both themes, focus-visible rings are defined globally, icon-only
  controls carry `aria-label`s, and all animation is switched off under `prefers-reduced-motion`.
- **Keyboard first**: the palette (`CommandPalette`) and the shortcut list (`ShortcutHelp`) are ordinary
  Blazor dialogs over the same tokens as everything else, opened by a document level key handler in
  `wwwroot/keyboard.js`. That module only names the shortcut it saw - "palette", "undo", "search" - and
  `MainLayout` decides what each one means, so the application has one place that says what `Ctrl+Z` does.
  What the palette *contains*, and the order it puts results in, lives in `CommandIndexService` and is
  covered by tests, because a palette that ranks badly is a palette nobody uses twice.

## Tech Stack

- .NET 8 / C# 12
- Blazor WebAssembly (standalone)
- `System.Text.Json` source generated serialization (trim safe)
- JS interop for `localStorage` and file downloads
- Hand written Open XML (.xlsx) generation in the browser (no ClosedXML dependency)
- Bootstrap 5.1 for the grid and form basics, restyled by the design system above
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
- `InventoryAnalyticsServiceTests` - stock classification, valuation, ABC banding, velocity and days of
  cover, reorder points and safety stock from measured demand, urgency ordering, the health score and its
  findings, and economic order quantity
- `InventoryQueryServiceTests` - every filter, how the parts combine, ordering in both directions, the
  facets behind the results
- `InventoryFilterQueryTests` - reading and writing a filter as a query string, including the values a
  stale link can carry
- `DemoHistoryGeneratorTests` - that generation is reproducible, that stock always equals the sum of the
  recorded movements, and that an item can never sell stock it does not have
- `ActivityServiceTests` - what gets logged, ordering, pruning and reloading
- `AppStateStoreTests` - the keyed store, the typed views over it, corrupt payload recovery and key isolation
- `JsonGroceryStoreTests` - the catalogue payload, including recovery from a corrupted one
- `XlsxExportServiceTests` - the generated workbook package, worksheets, ordering, escaping and number formats
