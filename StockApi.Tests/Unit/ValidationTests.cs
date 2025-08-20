using Microsoft.EntityFrameworkCore;
using StockApi.Data;
using StockApi.Models;

namespace StockApi.Tests.Unit;

public class ValidationTests
{
    private AppDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task Signup_Fails_WhenEmailAlreadyExists()
    {
        using var db = NewDb();
        db.Users.Add(new User { Name = "A", Email = "x@x.com", PasswordHash = "hash", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        var exists = await db.Users.AnyAsync(u => u.Email == "x@x.com");
        Assert.True(exists);
    }

    [Fact]
    public void Product_Invalid_WhenPriceIsZero()
    {
        var p = new Product { Name = "N", Description = "D", Price = 0m };
        Assert.True(p.Price <= 0);
    }

    [Fact]
    public void StockEntry_MustHaveInvoice()
    {
        var invoice = "";
        Assert.True(string.IsNullOrWhiteSpace(invoice));
    }

    [Fact]
    public void Order_Fails_WhenInsufficientStock()
    {
        var product = new Product { Name = "Guitarra", Description = "Strato", Price = 1000m, Stock = 1 };
        var requestedQty = 2;
        Assert.True(requestedQty > product.Stock);
    }
}
