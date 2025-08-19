using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockApi.Data;
using StockApi.Models;
using Xunit;

public class OrderTests
{
    private static AppDbContext NewMemDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(opts);
    }

    /// <summary>
    /// Simula a mesma regra do endpoint /orders (Program.cs):
    /// - valida existência e estoque
    /// - baixa estoque
    /// - cria Order/OrderItems
    /// </summary>
    private static async Task<Order> PlaceOrderAsync(
        AppDbContext db,
        string customerDocument,
        string sellerName,
        (Guid productId, int qty)[] items)
    {
        if (items == null || items.Length == 0)
            throw new InvalidOperationException("No items.");

        if (items.Any(i => i.qty <= 0))
            throw new InvalidOperationException("All quantities must be > 0.");

        var productIds = items.Select(i => i.productId).Distinct().ToList();
        var productsDb = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

        if (productsDb.Count != productIds.Count)
            throw new InvalidOperationException("Some product(s) not found.");

        foreach (var it in items)
        {
            var p = productsDb.First(x => x.Id == it.productId);
            if (p.Stock < it.qty)
                throw new InvalidOperationException($"Insufficient stock for product '{p.Name}'.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerDocument = customerDocument ?? "",
            SellerName = sellerName ?? "",
            CreatedAt = DateTime.UtcNow,
            Items = []
        };

        foreach (var it in items)
        {
            var p = productsDb.First(x => x.Id == it.productId);
            p.Stock -= it.qty;

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = p.Id,
                Quantity = it.qty,
                UnitPrice = p.Price
            });
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task DeductsStock_WhenOrderIsConfirmed()
    {
        // arrange
        using var db = NewMemDb(nameof(DeductsStock_WhenOrderIsConfirmed));

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Bola Pro",
            Description = "Futebol",
            Price = 199.90m,
            Stock = 10
        };
        db.Products.Add(prod);
        await db.SaveChangesAsync();

        // act
        var order = await PlaceOrderAsync(db, "123.456.789-00", "Carlos",
            new[] { (prod.Id, 3) });

        // assert
        var updated = await db.Products.FindAsync(prod.Id);
        Assert.NotNull(updated);
        Assert.Equal(7, updated!.Stock); // 10 - 3

        Assert.Single(order.Items);
        Assert.Equal(3, order.Items[0].Quantity);
        Assert.Equal(prod.Id, order.Items[0].ProductId);
        Assert.Equal(199.90m, order.Items[0].UnitPrice);
    }

    [Fact]
    public async Task Throws_WhenStockIsInsufficient()
    {
        // arrange
        using var db = NewMemDb(nameof(Throws_WhenStockIsInsufficient));

        var prod = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Chuteira",
            Description = "Campo",
            Price = 349.00m,
            Stock = 2
        };
        db.Products.Add(prod);
        await db.SaveChangesAsync();

        // act + assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PlaceOrderAsync(db, "999.999.999-99", "Vendedor",
                new[] { (prod.Id, 5) })); // pede mais do que há

        Assert.Contains("Insufficient stock", ex.Message);

        // estoque deve permanecer inalterado
        var unchanged = await db.Products.FindAsync(prod.Id);
        Assert.Equal(2, unchanged!.Stock);
    }
}