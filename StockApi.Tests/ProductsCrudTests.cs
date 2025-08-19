using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockApi.Data;
using StockApi.Models;
using Xunit;

public class ProductsCrudTests
{
    private static AppDbContext NewMemDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task CanCreateUpdateDelete_Product()
    {
        using var db = NewMemDb(nameof(CanCreateUpdateDelete_Product));

        var p = new Product { Id = Guid.NewGuid(), Name = "Bola", Description = "Futebol", Price = 50m, Stock = 0 };
        db.Products.Add(p);
        await db.SaveChangesAsync();

        var saved = await db.Products.FirstAsync();
        Assert.Equal("Bola", saved.Name);

        saved.Price = 60m;
        await db.SaveChangesAsync();
        var updated = await db.Products.FindAsync(p.Id);
        Assert.Equal(60m, updated!.Price);

        db.Products.Remove(updated);
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.Products.CountAsync());
    }
}