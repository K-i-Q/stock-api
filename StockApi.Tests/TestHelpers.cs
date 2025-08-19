using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StockApi.Data;
using StockApi.Services;
using System.Text;

namespace StockApi.Tests;

public static class TestHelpers
{
    public static AppDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static JwtTokenService CreateJwt(string key = "THIS_IS_A_LONG_TEST_KEY_32+CHARS_MIN_1234567890")
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        return new JwtTokenService(signingKey);
    }
}
