using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class RouteBodyMismatchTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public RouteBodyMismatchTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_Put_RouteIdDiferente_DoBody_DeveNoContent()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var create = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "X", Price = 10m });
        create.EnsureSuccessStatusCode();

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var body = new ProductUpdateRequest { Id = Guid.NewGuid(), Name = "Y", Price = 11m, Description = "d" };
        var res = await client.PutAsJsonAsync($"/products/{id}", body);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
