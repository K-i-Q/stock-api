using System.Net;
using System.Net.Http.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

public class AuthEndpointsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public AuthEndpointsTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Signup_Then_Login_Works()
    {
        var client = _factory.CreateClient();

        var signup = new SignupRequest
        {
            Name = "Alice",
            Email = "alice@test.local",
            Password = "secret123",
            Role = UserRole.Admin
        };
        var r1 = await client.PostAsJsonAsync("/auth/signup", signup);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var login = new LoginRequest { Email = signup.Email, Password = signup.Password };
        var r2 = await client.PostAsJsonAsync("/auth/login", login);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        var data = await r2.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(data!.Token));
    }
}
