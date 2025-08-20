using System.Net;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class BindingFailuresTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public BindingFailuresTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_Get_ComGuidInvalido_Deve404()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/products/nao-e-um-guid");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Orders_Get_ComGuidInvalido_Deve404()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, StockApi.Models.UserRole.Seller);
        client.UseBearer(token);

        var res = await client.GetAsync("/orders/xxx-guid");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
