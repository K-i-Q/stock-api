using Microsoft.EntityFrameworkCore;
using StockApi.Models;

namespace StockApi.Tests.Unit;

public class StockTests
{
    [Fact]
    public async Task StockEntry_IncrementsProductStock_AndSavesInvoice()
    {
        using var db = TestHelpers.CreateDb();

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Baqueta",
            Description = "5A",
            Price = 39.90m,
            Stock = 0
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = 15,
            InvoiceNumber = "NF-000123"
        };
        db.StockEntries.Add(entry);

        product.Stock += entry.Quantity;
        await db.SaveChangesAsync();

        var p = await db.Products.AsNoTracking().FirstAsync(x => x.Id == product.Id);
        Assert.Equal(15, p.Stock);

        var e = await db.StockEntries.AsNoTracking().FirstAsync(x => x.Id == entry.Id);
        Assert.Equal("NF-000123", e.InvoiceNumber);
    }
}
