using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class ProductsEndpointsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public ProductsEndpointsTests(CustomWebAppFactory factory) => _factory = factory;

    private static async Task<string> LoginAsAdmin(HttpClient client)
    {
        var rSignup = await client.PostAsJsonAsync("/auth/signup", new SignupRequest
        {
            Name = "Admin1",
            Email = "admin1@local",
            Password = "admin123",
            Role = UserRole.Admin
        });

        Assert.True(rSignup.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest);

        var r = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = "admin1@local", Password = "admin123" });
        var body = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var data = JsonSerializer.Deserialize<LoginResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(data);
        Assert.False(string.IsNullOrWhiteSpace(data!.Token));
        return data.Token;
    }

    [Fact]
    public async Task Create_Update_Get_Delete_Product_Flow()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsAdmin(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = new ProductCreateRequest { Name = "Bola", Description = "Futebol", Price = 99.90m };
        var rCreate = await client.PostAsJsonAsync("/products", create);
        Assert.Equal(HttpStatusCode.Created, rCreate.StatusCode);

        var created = await rCreate.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(created);
        Assert.Equal("Bola", created!.Name);

        var rList = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, rList.StatusCode);

        var update = new ProductUpdateRequest { Id = created.Id, Name = "Bola Pro", Description = "Top", Price = 129.90m };
        var rUpdate = await client.PutAsJsonAsync($"/products/{created.Id}", update);
        Assert.Equal(HttpStatusCode.NoContent, rUpdate.StatusCode);

        var rGet = await client.GetAsync($"/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, rGet.StatusCode);

        var rDel = await client.DeleteAsync($"/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, rDel.StatusCode);
    }
}
