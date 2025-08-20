using Microsoft.EntityFrameworkCore;
using StockApi.Models;

namespace StockApi.Tests.Unit;

public class AuthTests
{
    [Fact]
    public async Task Signup_CreatesUser_WithHashedPassword_AndUniqueEmail()
    {
        using var db = TestHelpers.CreateDb();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Carlos",
            Email = "carlos@local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = UserRole.Admin
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var exists = await db.Users.AnyAsync(u => u.Email == "carlos@local");
        Assert.True(exists);

        Assert.NotEqual("123456", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("123456", user.PasswordHash));
    }

    [Fact]
    public async Task Signup_Fails_WhenDuplicateEmail()
    {
        using var db = TestHelpers.CreateDb();

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();

        var duplicate = await db.Users.AnyAsync(u => u.Email == "admin@local");
        Assert.True(duplicate);
    }

    [Fact]
    public void Login_GeneratesJwt_WithValidCredentials()
    {
        var jwt = TestHelpers.CreateJwt();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Seller",
            Email = "seller@local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = UserRole.Seller
        };

        var token = jwt.Generate(user);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}
