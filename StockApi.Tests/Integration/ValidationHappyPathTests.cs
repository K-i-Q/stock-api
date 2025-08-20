using System.Net;
using System.Net.Http.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class ValidationHappyPathTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public ValidationHappyPathTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_Post_RequestValido_Deve201()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var res = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Produto Valido",
            Description = "Ok",
            Price = 12.34m
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }
}
