using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public class StockAndOrdersTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    public StockAndOrdersTests(CustomWebAppFactory factory) => _factory = factory;

    private static async Task<string> LoginAs(HttpClient client, UserRole role, string email)
    {
        await client.PostAsJsonAsync("/auth/signup", new SignupRequest { Name = role.ToString(), Email = email, Password = "pass123", Role = role });
        var r = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = "pass123" });
        var data = await r.Content.ReadFromJsonAsync<LoginResponse>();
        return data!.Token;
    }

    [Fact]
    public async Task Add_Stock_Then_Create_Order_Deducts_Stock()
    {
        var client = _factory.CreateClient();

        var adminToken = await LoginAs(client, UserRole.Admin, "admin2@local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createProduct = new ProductCreateRequest { Name = "Raquete", Description = "Tênis", Price = 300m };
        var rp = await client.PostAsJsonAsync("/products", createProduct);
        rp.EnsureSuccessStatusCode();
        var prod = await rp.Content.ReadFromJsonAsync<Product>();

        var stockReq = new StockEntryRequest { ProductId = prod!.Id, Quantity = 10, InvoiceNumber = "NF-123" };
        var rs = await client.PostAsJsonAsync("/stock/entries", stockReq);
        Assert.Equal(HttpStatusCode.Created, rs.StatusCode);

        var sellerToken = await LoginAs(client, UserRole.Seller, "seller@local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        var order = new CreateOrderRequest
        {
            CustomerDocument = "00011122233",
            SellerName = "Vendedor 1",
            Items = new() { new CreateOrderItemRequest { ProductId = prod.Id, Quantity = 3 } }
        };
        var ro = await client.PostAsJsonAsync("/orders", order);
        Assert.Equal(HttpStatusCode.Created, ro.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var rGet = await client.GetAsync($"/products/{prod.Id}");
        rGet.EnsureSuccessStatusCode();
        var after = await rGet.Content.ReadFromJsonAsync<Product>();
        Assert.Equal(7, after!.Stock);
    }

    [Fact]
    public async Task Create_Order_Fails_When_Insufficient_Stock()
    {
        var client = _factory.CreateClient();
        var adminToken = await LoginAs(client, UserRole.Admin, "admin3@local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var rp = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "Bola Vôlei", Price = 150m });
        rp.EnsureSuccessStatusCode();
        var prod = await rp.Content.ReadFromJsonAsync<Product>();

        var sellerToken = await LoginAs(client, UserRole.Seller, "seller2@local");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        var order = new CreateOrderRequest
        {
            CustomerDocument = "99988877766",
            SellerName = "Seller 2",
            Items = new() { new CreateOrderItemRequest { ProductId = prod!.Id, Quantity = 1 } }
        };
        var ro = await client.PostAsJsonAsync("/orders", order);
        Assert.Equal(HttpStatusCode.BadRequest, ro.StatusCode);
    }
}
