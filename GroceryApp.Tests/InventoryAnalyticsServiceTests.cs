namespace GroceryApp.Tests;

/// <summary>
/// Tests for the inventory analysis: how stock is classified, what the catalogue is worth, how the
/// ABC bands fall, how fast things move, what to reorder and how healthy the whole thing is.
/// </summary>
public class InventoryAnalyticsServiceTests
{
    /// <summary>
    /// Fixed "now", so every window in these tests is measured from the same moment and the results do
    /// not drift with the clock.
    /// </summary>
    private static readonly DateTime Now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly InventoryAnalyticsService _analytics = new();

    private static GroceryItemDto Item(
        int id,
        string name,
        Category category = Category.Fruit,
        decimal price = 4m,
        decimal cost = 2m,
        int stock = 10,
        int reorderPoint = 5,
        decimal targetMarginPercent = 40m) => new()
        {
            Id = id,
            Name = name,
            Sku = $"SKU-{id:D4}",
            Category = category,
            Supplier = "Test Supplier",
            Price = price,
            CostToProduce = cost,
            Stock = stock,
            ReorderPoint = reorderPoint,
            TargetMarginPercent = targetMarginPercent
        };

    /// <summary>
    /// A stock removal: units leaving the shelf, which is what the demand figures are built from.
    /// </summary>
    private static ActivityEntry Movement(int itemId, int units, int daysAgo, string name = "Item") => new()
    {
        ItemId = itemId,
        ItemName = name,
        Kind = ActivityKind.StockAdjusted,
        StockDelta = -Math.Abs(units),
        TimestampUtc = Now.AddDays(-daysAgo)
    };

    /// <summary>A restock: units arriving, which must never be counted as demand.</summary>
    private static ActivityEntry Restock(int itemId, int units, int daysAgo) => new()
    {
        ItemId = itemId,
        ItemName = "Item",
        Kind = ActivityKind.BulkRestock,
        StockDelta = Math.Abs(units),
        TimestampUtc = Now.AddDays(-daysAgo)
    };

    #region Classify

    [Fact]
    public void Classify_WhenStockIsZero_IsOutOfStock()
    {
        Assert.Equal(StockStatus.OutOfStock, _analytics.Classify(Item(1, "Empty", stock: 0)));
    }

    [Fact]
    public void Classify_WhenStockIsAtOrBelowAQuarterOfTheReorderPoint_IsCritical()
    {
        // A quarter of 20 is 5, so five units is the last critical level.
        Assert.Equal(StockStatus.Critical, _analytics.Classify(Item(1, "Bare", stock: 5, reorderPoint: 20)));
        Assert.Equal(StockStatus.Critical, _analytics.Classify(Item(1, "Bare", stock: 1, reorderPoint: 20)));
    }

    [Fact]
    public void Classify_WhenStockIsBetweenAQuarterAndTheReorderPoint_IsLow()
    {
        Assert.Equal(StockStatus.Low, _analytics.Classify(Item(1, "Low", stock: 6, reorderPoint: 20)));
        Assert.Equal(StockStatus.Low, _analytics.Classify(Item(1, "Low", stock: 20, reorderPoint: 20)));
    }

    [Fact]
    public void Classify_JustAboveTheReorderPoint_IsHealthy()
    {
        Assert.Equal(StockStatus.Healthy, _analytics.Classify(Item(1, "Fine", stock: 21, reorderPoint: 20)));
    }

    [Fact]
    public void Classify_WhenStockIsMoreThanFourTimesTheReorderPoint_IsOverstock()
    {
        Assert.Equal(StockStatus.Healthy, _analytics.Classify(Item(1, "Plenty", stock: 20, reorderPoint: 5)));
        Assert.Equal(StockStatus.Overstock, _analytics.Classify(Item(1, "Too much", stock: 21, reorderPoint: 5)));
    }

    [Fact]
    public void Classify_WhenTheReorderPointIsZero_TreatsTheThresholdAsOneUnit()
    {
        // A threshold of zero would make every stocked item look healthy, which would hide the fact
        // that nothing has been configured for the item.
        Assert.Equal(StockStatus.OutOfStock, _analytics.Classify(Item(1, "Unset", stock: 0, reorderPoint: 0)));
        Assert.Equal(StockStatus.Low, _analytics.Classify(Item(1, "Unset", stock: 1, reorderPoint: 0)));
        Assert.Equal(StockStatus.Healthy, _analytics.Classify(Item(1, "Unset", stock: 2, reorderPoint: 0)));
    }

    [Fact]
    public void Classify_WhenTheItemIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _analytics.Classify(null!));
    }

    #endregion

    #region Summarise

    [Fact]
    public void Summarise_WhenTheCatalogueIsEmpty_ReturnsZeroes()
    {
        var summary = _analytics.Summarise(Array.Empty<GroceryItemDto>());

        Assert.Equal(0, summary.ItemCount);
        Assert.Equal(0, summary.TotalUnits);
        Assert.Equal(0m, summary.CostValue);
        Assert.Equal(0m, summary.RetailValue);
        Assert.Equal(0m, summary.PotentialProfit);
        Assert.Equal(0m, summary.AverageMarginPercent);
        Assert.Equal(0m, summary.LargestItemShareOfValue);
    }

    [Fact]
    public void Summarise_AddsUpUnitsCostAndProfitAcrossTheCatalogue()
    {
        var summary = _analytics.Summarise(new[]
        {
            Item(1, "Apple", price: 2m, cost: 1m, stock: 10),
            Item(2, "Banana", price: 4m, cost: 1m, stock: 5)
        });

        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(15, summary.TotalUnits);
        Assert.Equal(15m, summary.CostValue);
        Assert.Equal(40m, summary.RetailValue);
        Assert.Equal(25m, summary.PotentialProfit);
        Assert.Equal(3m, summary.AverageUnitPrice);
    }

    [Fact]
    public void Summarise_AveragesTheMarginPercentagesOfTheItems()
    {
        // 50% and 75% average to 62.5%.
        var summary = _analytics.Summarise(new[]
        {
            Item(1, "Apple", price: 2m, cost: 1m),
            Item(2, "Banana", price: 4m, cost: 1m)
        });

        Assert.Equal(62.5m, summary.AverageMarginPercent);
    }

    [Fact]
    public void Summarise_ReportsTheShareOfValueHeldByTheLargestItem()
    {
        var summary = _analytics.Summarise(new[]
        {
            Item(1, "Small", price: 1m, stock: 10),
            Item(2, "Big", price: 9m, stock: 10)
        });

        Assert.Equal(90m, summary.LargestItemShareOfValue);
    }

    [Fact]
    public void Summarise_CountsOutOfStockItemsAndItemsUnderTheirMarginTarget()
    {
        var summary = _analytics.Summarise(new[]
        {
            Item(1, "Empty", stock: 0),
            Item(2, "Thin", price: 10m, cost: 9m, targetMarginPercent: 40m),
            Item(3, "Fine", price: 10m, cost: 5m, targetMarginPercent: 40m)
        });

        Assert.Equal(1, summary.OutOfStockCount);
        Assert.Equal(1, summary.BelowTargetMarginCount);
    }

    [Fact]
    public void Summarise_WhenNothingIsInStock_ReportsNoConcentrationRatherThanDividingByZero()
    {
        var summary = _analytics.Summarise(new[] { Item(1, "Empty", stock: 0) });

        Assert.Equal(0m, summary.RetailValue);
        Assert.Equal(0m, summary.LargestItemShareOfValue);
    }

    [Fact]
    public void Summarise_WhenTheCatalogueIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _analytics.Summarise(null!));
    }

    #endregion

    #region ByCategory

    [Fact]
    public void ByCategory_WhenTheCatalogueIsEmpty_ReturnsNothing()
    {
        Assert.Empty(_analytics.ByCategory(Array.Empty<GroceryItemDto>()));
    }

    [Fact]
    public void ByCategory_GroupsItemsAndOrdersTheMostValuableCategoryFirst()
    {
        var categories = _analytics.ByCategory(new[]
        {
            Item(1, "Apple", Category.Fruit, price: 1m, stock: 10),
            Item(2, "Pear", Category.Fruit, price: 1m, stock: 10),
            Item(3, "Brie", Category.Cheese, price: 20m, stock: 10)
        });

        Assert.Equal(2, categories.Count);
        Assert.Equal(Category.Cheese, categories[0].Category);
        Assert.Equal(1, categories[0].ItemCount);
        Assert.Equal(200m, categories[0].StockRetailValue);
        Assert.Equal(Category.Fruit, categories[1].Category);
        Assert.Equal(2, categories[1].ItemCount);
        Assert.Equal(20, categories[1].TotalStock);
    }

    [Fact]
    public void ByCategory_ReportsEachCategorysShareOfTheTotalValue()
    {
        var categories = _analytics.ByCategory(new[]
        {
            Item(1, "Apple", Category.Fruit, price: 3m, stock: 10),
            Item(2, "Brie", Category.Cheese, price: 7m, stock: 10)
        });

        Assert.Equal(70m, categories.Single(c => c.Category == Category.Cheese).ShareOfRetailValue);
        Assert.Equal(30m, categories.Single(c => c.Category == Category.Fruit).ShareOfRetailValue);
    }

    [Fact]
    public void ByCategory_WorksOutCostValueAndPotentialProfit()
    {
        var category = Assert.Single(_analytics.ByCategory(new[]
        {
            Item(1, "Apple", price: 4m, cost: 1m, stock: 10)
        }));

        Assert.Equal(10m, category.StockCostValue);
        Assert.Equal(40m, category.StockRetailValue);
        Assert.Equal(30m, category.PotentialProfit);
        Assert.Equal(3m, category.AverageMargin);
        Assert.Equal(75m, category.AverageMarginPercent);
    }

    [Fact]
    public void ByCategory_WhenNothingIsInStock_ReportsNoShareRatherThanDividingByZero()
    {
        var category = Assert.Single(_analytics.ByCategory(new[] { Item(1, "Empty", stock: 0) }));

        Assert.Equal(0m, category.ShareOfRetailValue);
        Assert.Equal(0m, category.StockRetailValue);
    }

    #endregion

    #region AnalyseAbc

    [Fact]
    public void AnalyseAbc_WhenTheCatalogueIsEmpty_ReturnsNoBands()
    {
        Assert.Empty(_analytics.AnalyseAbc(Array.Empty<GroceryItemDto>()).Classes);
    }

    [Fact]
    public void AnalyseAbc_SplitsTheCatalogueAtEightyAndNinetyFivePercentOfValue()
    {
        // Retail values of 80, 15 and 5 make the running total cross 80% and 95% exactly once each.
        var analysis = _analytics.AnalyseAbc(new[]
        {
            Item(1, "Alpha", price: 1m, cost: 0.5m, stock: 80),
            Item(2, "Bravo", price: 1m, cost: 0.5m, stock: 15),
            Item(3, "Charlie", price: 1m, cost: 0.5m, stock: 5)
        });

        Assert.Equal(3, analysis.Classes.Count);

        Assert.Equal(AbcClass.A, analysis.Classes[0].Class);
        Assert.Equal(1, analysis.Classes[0].ItemCount);
        Assert.Equal(80m, analysis.Classes[0].ShareOfValue);
        Assert.Equal(80m, analysis.Classes[0].CumulativeShare);

        Assert.Equal(AbcClass.B, analysis.Classes[1].Class);
        Assert.Equal(1, analysis.Classes[1].ItemCount);
        Assert.Equal(15m, analysis.Classes[1].ShareOfValue);
        Assert.Equal(95m, analysis.Classes[1].CumulativeShare);

        Assert.Equal(AbcClass.C, analysis.Classes[2].Class);
        Assert.Equal(1, analysis.Classes[2].ItemCount);
        Assert.Equal(5m, analysis.Classes[2].ShareOfValue);
        Assert.Equal(100m, analysis.Classes[2].CumulativeShare);
    }

    [Fact]
    public void AnalyseAbc_ReportsTheConcentrationAndHowManyLinesCarryIt()
    {
        var analysis = _analytics.AnalyseAbc(new[]
        {
            Item(1, "Alpha", price: 1m, cost: 0.5m, stock: 80),
            Item(2, "Bravo", price: 1m, cost: 0.5m, stock: 15),
            Item(3, "Charlie", price: 1m, cost: 0.5m, stock: 5)
        });

        Assert.Equal(80m, analysis.Concentration);
        Assert.Equal(1, analysis.ItemCountInClassA);
    }

    [Fact]
    public void AnalyseAbc_OrdersEachBandByValueAndTiesByNameSoTheBandsDoNotMoveBetweenLoads()
    {
        var analysis = _analytics.AnalyseAbc(new[]
        {
            Item(1, "Zucchini", price: 1m, cost: 0.5m, stock: 50),
            Item(2, "Apple", price: 1m, cost: 0.5m, stock: 50)
        });

        // Equal values, so the tie is broken by name and the order is the same on every load.
        Assert.Equal(new[] { "Apple", "Zucchini" }, analysis.Classes[0].Items.Select(item => item.Name));
    }

    [Fact]
    public void AnalyseAbc_WhenNothingIsInStock_PutsEverythingInTheLastBand()
    {
        var analysis = _analytics.AnalyseAbc(new[]
        {
            Item(1, "One", stock: 0),
            Item(2, "Two", stock: 0)
        });

        Assert.Equal(0, analysis.Classes[0].ItemCount);
        Assert.Equal(0, analysis.Classes[1].ItemCount);
        Assert.Equal(2, analysis.Classes[2].ItemCount);
        Assert.Equal(0m, analysis.Concentration);
    }

    #endregion

    #region CalculateVelocity

    [Fact]
    public void CalculateVelocity_MeasuresUnitsRemovedPerDayAcrossTheWindow()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(1, 10, 5), Movement(1, 20, 20) },
            Now,
            windowDays: 30));

        Assert.Equal(30, stat.UnitsMoved);
        Assert.Equal(30, stat.WindowDays);
        Assert.Equal(1d, stat.UnitsPerDay);
    }

    [Fact]
    public void CalculateVelocity_IgnoresRestocks()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(1, 10, 5), Restock(1, 40, 3) },
            Now));

        Assert.Equal(10, stat.UnitsMoved);
    }

    [Fact]
    public void CalculateVelocity_IgnoresMovementsOutsideTheWindow()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(1, 10, 5), Movement(1, 90, 45) },
            Now,
            windowDays: 30));

        Assert.Equal(10, stat.UnitsMoved);
    }

    [Fact]
    public void CalculateVelocity_IncludesMovementsOnTheBoundaryOfTheWindow()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(1, 7, 30) },
            Now,
            windowDays: 30));

        Assert.Equal(7, stat.UnitsMoved);
    }

    [Fact]
    public void CalculateVelocity_ComputesDaysOfCoverFromTheRateAndTheStockOnHand()
    {
        // Thirty units over thirty days is one a day, so ten units on hand last ten days.
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple", stock: 10) },
            new[] { Movement(1, 30, 2) },
            Now));

        Assert.Equal(10d, stat.DaysOfCover);
    }

    [Fact]
    public void CalculateVelocity_WhenNothingMoved_ReportsNoCoverInsteadOfAnInfiniteFigure()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            Array.Empty<ActivityEntry>(),
            Now));

        Assert.Equal(0, stat.UnitsMoved);
        Assert.Equal(0d, stat.UnitsPerDay);
        Assert.Null(stat.DaysOfCover);
    }

    [Fact]
    public void CalculateVelocity_OrdersTheFastestMovingFirst()
    {
        var stats = _analytics.CalculateVelocity(
            new[] { Item(1, "Slow"), Item(2, "Fast") },
            new[] { Movement(1, 5, 2), Movement(2, 60, 2) },
            Now);

        Assert.Equal(new[] { "Fast", "Slow" }, stats.Select(stat => stat.Name));
    }

    [Fact]
    public void CalculateVelocity_IgnoresMovementsLoggedAgainstItemsNoLongerInTheCatalogue()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(999, 30, 2) },
            Now));

        Assert.Equal(0, stat.UnitsMoved);
    }

    [Fact]
    public void CalculateVelocity_WhenTheWindowIsNotPositive_TreatsItAsASingleDay()
    {
        var stat = Assert.Single(_analytics.CalculateVelocity(
            new[] { Item(1, "Apple") },
            new[] { Movement(1, 4, 0) },
            Now,
            windowDays: 0));

        Assert.Equal(1, stat.WindowDays);
        Assert.Equal(4d, stat.UnitsPerDay);
    }

    #endregion

    #region SuggestReorders

    [Fact]
    public void SuggestReorders_WhenEverythingIsCovered_SuggestsNothing()
    {
        var suggestions = _analytics.SuggestReorders(
            new[] { Item(1, "Apple", stock: 40, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void SuggestReorders_WhenThereIsNoDemandHistory_UsesTheItemsOwnReorderPoint()
    {
        var suggestion = Assert.Single(_analytics.SuggestReorders(
            new[] { Item(1, "Apple", cost: 0.5m, stock: 5, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now));

        Assert.False(suggestion.UsesMeasuredDemand);
        Assert.Equal(StockStatus.Low, suggestion.Status);
        Assert.Equal(5, suggestion.ReorderPoint);
        Assert.Equal(10, suggestion.TargetStock);
        Assert.Equal(5, suggestion.SuggestedQuantity);
        Assert.Equal(2.5m, suggestion.EstimatedCost);
        Assert.Equal(InventoryAnalyticsService.DefaultLeadTimeDays, suggestion.LeadTimeDays);
    }

    [Fact]
    public void SuggestReorders_WhenThereIsDemandHistory_DerivesTheReorderPointFromThatDemand()
    {
        // One a day, a three day lead time and three days of safety stock: reorder at six and top up
        // to the nine that covers two lead times.
        var suggestion = Assert.Single(_analytics.SuggestReorders(
            new[] { Item(1, "Apple", cost: 1m, stock: 0, reorderPoint: 1) },
            new[] { Movement(1, 30, 3) },
            Now));

        Assert.True(suggestion.UsesMeasuredDemand);
        Assert.Equal(StockStatus.OutOfStock, suggestion.Status);
        Assert.Equal(1d, suggestion.UnitsPerDay);
        Assert.Equal(6, suggestion.ReorderPoint);
        Assert.Equal(9, suggestion.TargetStock);
        Assert.Equal(9, suggestion.SuggestedQuantity);
        Assert.Equal(9m, suggestion.EstimatedCost);
    }

    [Fact]
    public void SuggestReorders_UsesTheSupplierLeadTimeWhenOneIsAvailable()
    {
        // A ten day lead time and three days of safety stock: reorder at thirteen, top up to twenty three.
        var suggestion = Assert.Single(_analytics.SuggestReorders(
            new[] { Item(1, "Apple", cost: 1m, stock: 0) },
            new[] { Movement(1, 30, 3) },
            Now,
            leadTimeDays: _ => 10));

        Assert.Equal(10, suggestion.LeadTimeDays);
        Assert.Equal(13, suggestion.ReorderPoint);
        Assert.Equal(23, suggestion.TargetStock);
    }

    [Fact]
    public void SuggestReorders_ExcludesItemsThatAreOverstocked()
    {
        var suggestions = _analytics.SuggestReorders(
            new[] { Item(1, "Apple", stock: 500, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void SuggestReorders_ExcludesItemsAboveBothThresholds()
    {
        var suggestions = _analytics.SuggestReorders(
            new[] { Item(1, "Apple", stock: 8, reorderPoint: 5) },
            new[] { Movement(1, 30, 3) },
            Now);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void SuggestReorders_IncludesItemsBelowTheThresholdTheirOwnDemandImplies()
    {
        // Ten a day means a reorder point of thirty plus thirty of safety stock, so forty units is
        // already too few, even though it is well above the item's own reorder point of five.
        var suggestion = Assert.Single(_analytics.SuggestReorders(
            new[] { Item(1, "Apple", stock: 40, reorderPoint: 5) },
            new[] { Movement(1, 300, 3) },
            Now));

        Assert.True(suggestion.UsesMeasuredDemand);
        Assert.Equal(60, suggestion.ReorderPoint);
    }

    [Fact]
    public void SuggestReorders_OrdersTheMostUrgentFirst()
    {
        var suggestions = _analytics.SuggestReorders(
            new[]
            {
                Item(1, "Low", stock: 4, reorderPoint: 5),
                Item(2, "Empty", stock: 0, reorderPoint: 5),
                Item(3, "Critical", stock: 1, reorderPoint: 20)
            },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(new[] { "Empty", "Critical", "Low" }, suggestions.Select(suggestion => suggestion.Item.Name));
    }

    [Fact]
    public void SuggestReorders_CarriesTheSupplierAndTheReasonWhyTheItemIsOnTheList()
    {
        var suggestion = Assert.Single(_analytics.SuggestReorders(
            new[] { Item(1, "Apple", stock: 0, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now));

        Assert.Equal("Test Supplier", suggestion.Supplier);
        Assert.Contains("Out of stock", suggestion.Reason);
        Assert.Contains("reorder point", suggestion.Reason);
    }

    [Fact]
    public void SuggestReorders_WhenTheCatalogueIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _analytics.SuggestReorders(
            null!,
            Array.Empty<ActivityEntry>(),
            Now));
    }

    #endregion

    #region AssessHealth

    [Fact]
    public void AssessHealth_WhenEverythingIsCovered_ScoresFullMarksAndGradesAnA()
    {
        // Three balanced items, so no single item holds a worrying share of the value.
        var health = _analytics.AssessHealth(
            new[]
            {
                Item(1, "Apple", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(2, "Banana", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(3, "Cherry", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5)
            },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(100, health.Score);
        Assert.Equal("A", health.Grade);
        Assert.Equal(3, health.HealthyCount);
    }

    [Fact]
    public void AssessHealth_CountsEveryStockStatus()
    {
        var health = _analytics.AssessHealth(
            new[]
            {
                Item(1, "Empty", stock: 0, reorderPoint: 5),
                Item(2, "Critical", stock: 1, reorderPoint: 20),
                Item(3, "Low", stock: 4, reorderPoint: 5),
                Item(4, "Fine", stock: 10, reorderPoint: 5),
                Item(5, "Piled up", stock: 100, reorderPoint: 5)
            },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(1, health.OutOfStockCount);
        Assert.Equal(1, health.CriticalCount);
        Assert.Equal(1, health.LowCount);
        Assert.Equal(1, health.HealthyCount);
        Assert.Equal(1, health.OverstockCount);
    }

    [Fact]
    public void AssessHealth_DeductsInProportionToHowMuchOfTheCatalogueIsOutOfStock()
    {
        // One line of four is a quarter of the catalogue, worth a quarter of the fifty point weight.
        var health = _analytics.AssessHealth(
            new[]
            {
                Item(1, "Empty", price: 1m, cost: 0.5m, stock: 0, reorderPoint: 5),
                Item(2, "Apple", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(3, "Banana", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(4, "Cherry", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5)
            },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(87, health.Score);
    }

    [Fact]
    public void AssessHealth_DoesNotDeclareALargeCatalogueUnhealthyOverAFewBadLines()
    {
        // Seven failing lines out of fifty is a problem worth reporting, not a failed business.
        var items = Enumerable.Range(1, 50)
            .Select(id => Item(
                id,
                $"Item {id}",
                price: 1m,
                cost: 0.5m,
                stock: id <= 7 ? 0 : 10,
                reorderPoint: 5))
            .ToList();

        var health = _analytics.AssessHealth(items, Array.Empty<ActivityEntry>(), Now);

        Assert.True(health.Score >= 75, $"scored {health.Score}");
        Assert.Equal(7, health.OutOfStockCount);
    }

    [Fact]
    public void AssessHealth_FlagsStockThatNeverMovedAsDeadStock()
    {
        var health = _analytics.AssessHealth(
            new[] { Item(1, "Forgotten", stock: 20, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(1, health.DeadStockCount);
        Assert.Contains(health.Findings, finding => finding.Title.Contains("did not move"));
    }

    [Fact]
    public void AssessHealth_DoesNotFlagStockThatMovedAsDeadStock()
    {
        var health = _analytics.AssessHealth(
            new[] { Item(1, "Moving", stock: 20, reorderPoint: 5) },
            new[] { Movement(1, 4, 2) },
            Now);

        Assert.Equal(0, health.DeadStockCount);
        Assert.DoesNotContain(health.Findings, finding => finding.Title.Contains("did not move"));
    }

    [Fact]
    public void AssessHealth_WarnsWhenTheValueIsConcentratedInOneItem()
    {
        var health = _analytics.AssessHealth(
            new[]
            {
                Item(1, "Big", price: 9m, cost: 5m, stock: 40, reorderPoint: 5),
                Item(2, "Small", price: 1m, cost: 0.5m, stock: 40, reorderPoint: 5)
            },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Contains(health.Findings, finding => finding.Title == "The value is concentrated in one item");
    }

    [Fact]
    public void AssessHealth_WordsAProblemInTheSingularWhenOnlyOneItemIsAffected()
    {
        var health = _analytics.AssessHealth(
            new[] { Item(1, "Empty", stock: 0, reorderPoint: 5), Item(2, "Fine", stock: 40, reorderPoint: 5) },
            Array.Empty<ActivityEntry>(),
            Now);

        var warning = Assert.Single(health.Findings, finding => finding.Severity == FindingSeverity.Warning);

        Assert.Equal("1 item is out of stock", warning.Title);
    }

    [Fact]
    public void AssessHealth_AlwaysSummarisesWhatIsHeld()
    {
        var health = _analytics.AssessHealth(
            new[] { Item(1, "Apple", price: 2m, cost: 1m, stock: 10, reorderPoint: 1) },
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Contains(health.Findings, finding => finding.Title.Contains("units held"));
    }

    [Fact]
    public void AssessHealth_WhenEverythingIsCovered_SaysSoRatherThanReturningNoFindings()
    {
        var health = _analytics.AssessHealth(
            new[]
            {
                Item(1, "Apple", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(2, "Banana", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5),
                Item(3, "Cherry", price: 1m, cost: 0.5m, stock: 10, reorderPoint: 5)
            },
            new[] { Movement(1, 4, 1) },
            Now);

        Assert.Contains(health.Findings, finding => finding.Title == "Every item is within its thresholds");
    }

    [Fact]
    public void AssessHealth_WhenTheCatalogueIsEmpty_ReportsNothingToScore()
    {
        var health = _analytics.AssessHealth(
            Array.Empty<GroceryItemDto>(),
            Array.Empty<ActivityEntry>(),
            Now);

        Assert.Equal(0, health.Score);
        Assert.Equal("E", health.Grade);
        Assert.Contains(health.Findings, finding => finding.Title == "Nothing to score yet");
    }

    [Fact]
    public void AssessHealth_WhenTheCatalogueIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _analytics.AssessHealth(
            null!,
            Array.Empty<ActivityEntry>(),
            Now));
    }

    #endregion

    #region CalculateEconomicOrderQuantity

    [Fact]
    public void CalculateEconomicOrderQuantity_MatchesTheClassicFormula()
    {
        // sqrt(2 * 1000 * 25 / 2) = sqrt(25000) = 158.113...
        var quantity = _analytics.CalculateEconomicOrderQuantity(
            annualDemandUnits: 1000m,
            costPerOrder: 25m,
            annualHoldingCostPerUnit: 2m);

        Assert.Equal(158.11m, decimal.Round(quantity, 2));
    }

    [Fact]
    public void CalculateEconomicOrderQuantity_HalvingTheOrderingCost_LowersTheOrderQuantity()
    {
        var fullCost = _analytics.CalculateEconomicOrderQuantity(1000m, 25m, 2m);
        var halfCost = _analytics.CalculateEconomicOrderQuantity(1000m, 12.5m, 2m);

        Assert.True(halfCost < fullCost);
        Assert.Equal(111.80m, decimal.Round(halfCost, 2));
    }

    [Theory]
    [InlineData(0d, 25d, 2d)]
    [InlineData(1000d, 0d, 2d)]
    [InlineData(1000d, 25d, 0d)]
    [InlineData(-1d, 25d, 2d)]
    public void CalculateEconomicOrderQuantity_WhenAnInputIsNotPositive_ReturnsZero(
        double annualDemand,
        double costPerOrder,
        double holdingCostPerUnit)
    {
        // The formula divides by the holding cost and multiplies by the other two, so a caller
        // passing an unfilled form field gets a quantity of zero rather than an exception or a NaN.
        var quantity = _analytics.CalculateEconomicOrderQuantity(
            (decimal)annualDemand,
            (decimal)costPerOrder,
            (decimal)holdingCostPerUnit);

        Assert.Equal(0m, quantity);
    }

    #endregion
}
