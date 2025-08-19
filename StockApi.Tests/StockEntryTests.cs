using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockApi.Data;
using StockApi.Models;
using Xunit;

public class StockEntryTests
{
    private static AppDbContext NewMemDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task AddsQuantity_ToProductStock()
    {
        using var db = NewMemDb(nameof(AddsQuantity_ToProductStock));
        var p = new Product { Id = Guid.NewGuid(), Name = "Bola", Price = 100m, Stock = 0 };
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var entry = new StockEntry
        {
            Id = Guid.NewGuid(),
            ProductId = p.Id,
            Quantity = 5,
            InvoiceNumber = "NF-1"
        };
        db.StockEntries.Add(entry);
        p.Stock += entry.Quantity;
        await db.SaveChangesAsync();

        var updated = await db.Products.FindAsync(p.Id);
        Assert.Equal(5, updated!.Stock);
    }
}