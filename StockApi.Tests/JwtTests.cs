using StockApi.Models;

namespace StockApi.Tests;

public class JwtTests
{
    [Fact]
    public void Jwt_Generated_WithMinimum256bitKey()
    {
        var jwt = TestHelpers.CreateJwt("0123456789abcdef0123456789abcdef0123456789abcdef");
        var token = jwt.Generate(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@local",
            PasswordHash = "hash",
            Role = UserRole.Admin
        });

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}
