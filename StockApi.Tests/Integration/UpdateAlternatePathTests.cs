using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class UpdateAlternatePathTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public UpdateAlternatePathTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Products_Put_SemDescription_ContinuaValido_NoContent()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var create = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "P", Price = 10m });
        create.EnsureSuccessStatusCode();

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var res = await client.PutAsJsonAsync($"/products/{id}", new ProductUpdateRequest
        {
            Id = id,
            Name = "P2",
            Price = 20m
        });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
