using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StockApi.Models;
using StockApi.Services;

namespace StockApi.Tests.Unit;

public class JwtTokenServiceTests
{
    private static JwtTokenService MakeService()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 32)));
        return new JwtTokenService(key);
    }

    [Fact]
    public void Generate_DeveRetornarTokenEmFormatoJwt()
    {
        var svc = MakeService();
        var user = new User { Id = Guid.NewGuid(), Email = "a@b", Name = "Alice", Role = UserRole.Admin };

        var token = svc.Generate(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void Generate_DeveConterClaims_Sub_Name_Email_Role()
    {
        var svc = MakeService();
        var user = new User { Id = Guid.NewGuid(), Email = "seller@local", Name = "Bob", Role = UserRole.Seller };

        var token = svc.Generate(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Name, jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal(UserRole.Seller.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Generate_DeveExpirarAproximadamenteEm8Horas()
    {
        var svc = MakeService();
        var user = new User { Id = Guid.NewGuid(), Email = "x@y", Name = "Zed", Role = UserRole.Admin };

        var before = DateTime.UtcNow;
        var token = svc.Generate(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var after = DateTime.UtcNow;

        var expectedMin = before.AddHours(8).AddMinutes(-1);
        var expectedMax = after.AddHours(8).AddMinutes(+1);

        Assert.InRange(jwt.ValidTo, expectedMin, expectedMax);
    }
}
