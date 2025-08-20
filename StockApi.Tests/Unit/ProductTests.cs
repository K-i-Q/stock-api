using Microsoft.EntityFrameworkCore;
using StockApi.Models;

namespace StockApi.Tests.Unit;

public class ProductTests
{
    [Fact]
    public async Task CreateProduct_DefaultStockZero_AndPersistsPrice()
    {
        using var db = TestHelpers.CreateDb();

        var p = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Violão",
            Description = "Clássico",
            Price = 799.90m,
            Stock = 0
        };

        db.Products.Add(p);
        await db.SaveChangesAsync();

        var saved = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.Id);
        Assert.NotNull(saved);
        Assert.Equal(0, saved!.Stock);
        Assert.Equal(799.90m, saved.Price);
    }

    [Fact]
    public async Task UpdateProduct_ChangesNameDescriptionAndPrice()
    {
        using var db = TestHelpers.CreateDb();

        var p = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Teclado",
            Description = "61 teclas",
            Price = 1200m,
            Stock = 0
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();

        p.Name = "Teclado Arranjador";
        p.Description = "61 teclas com ritmos";
        p.Price = 1350m;

        await db.SaveChangesAsync();

        var updated = await db.Products.AsNoTracking().FirstAsync(x => x.Id == p.Id);
        Assert.Equal("Teclado Arranjador", updated.Name);
        Assert.Equal("61 teclas com ritmos", updated.Description);
        Assert.Equal(1350m, updated.Price);
    }

    [Fact]
    public async Task DeleteProduct_RemovesEntity()
    {
        using var db = TestHelpers.CreateDb();

        var p = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Guitarra",
            Description = "Strato",
            Price = 2500m
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();

        db.Products.Remove(p);
        await db.SaveChangesAsync();

        var exists = await db.Products.AnyAsync(x => x.Id == p.Id);
        Assert.False(exists);
    }
}
