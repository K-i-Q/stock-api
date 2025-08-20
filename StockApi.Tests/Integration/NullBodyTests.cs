using System.Net;
using System.Net.Http.Json;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class NullBodyTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public NullBodyTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_Post_BodyNulo_Deve400()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, StockApi.Models.UserRole.Admin);
        client.UseBearer(token);

        var res = await client.PostAsync("/products", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Orders_Post_BodyNulo_Deve400()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, StockApi.Models.UserRole.Seller);
        client.UseBearer(token);

        var res = await client.PostAsync("/orders", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
