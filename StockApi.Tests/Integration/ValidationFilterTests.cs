using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;

public class ValidationFilterTests : IClassFixture<CustomWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ValidationFilterTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    private static void UseBearer(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> SignupAndLoginAsync(HttpClient client, string email, string password, UserRole role)
    {
        var sres = await client.PostAsJsonAsync("/auth/signup", new SignupRequest
        {
            Name = "User " + role,
            Email = email,
            Password = password,
            Role = role
        });

        if (sres.StatusCode != HttpStatusCode.Created && sres.StatusCode != HttpStatusCode.BadRequest)
            sres.EnsureSuccessStatusCode();

        var lres = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, lres.StatusCode);

        var login = await lres.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        return login.Token;
    }

    [Fact]
    public async Task Signup_ComPasswordVazio_Deve400_ValidationProblemDetails()
    {
        var client = _factory.CreateClient();

        var sres = await client.PostAsJsonAsync("/auth/signup", new SignupRequest
        {
            Name = "Sem senha",
            Email = "sem-senha@local",
            Password = "",
            Role = UserRole.Seller
        });

        Assert.Equal(HttpStatusCode.BadRequest, sres.StatusCode);

        var pd = await sres.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.Contains("Password", pd!.Errors.Keys);
    }

    [Fact]
    public async Task ProductCreate_ComNomeEmBranco_Deve400_ValidationProblemDetails()
    {
        var client = _factory.CreateClient();
        var adminToken = await SignupAndLoginAsync(client, "admin-val@local", "admin123", UserRole.Admin);
        UseBearer(client, adminToken);

        var res = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "   ",
            Price = 10m,
            Description = "teste"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var pd = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.Contains("Name", pd!.Errors.Keys);
    }

    [Fact]
    public async Task ProductCreate_ComPrecoZero_Deve400_ValidationProblemDetails()
    {
        var client = _factory.CreateClient();
        var adminToken = await SignupAndLoginAsync(client, "admin-val2@local", "admin123", UserRole.Admin);
        UseBearer(client, adminToken);

        var res = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Mouse",
            Price = 0m,
            Description = "abc"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var pd = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.Contains("Price", pd!.Errors.Keys);
    }

    [Fact]
    public async Task StockEntry_ComQuantityZero_Deve400_ValidationProblemDetails()
    {
        var client = _factory.CreateClient();
        var adminToken = await SignupAndLoginAsync(client, "admin-val3@local", "admin123", UserRole.Admin);
        UseBearer(client, adminToken);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "P1",
            Price = 10m
        });
        Assert.Equal(HttpStatusCode.Created, cres.StatusCode);
        var product = await cres.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(product);

        var sres = await client.PostAsJsonAsync("/stock/entries", new StockEntryRequest
        {
            ProductId = product!.Id,
            Quantity = 0,
            InvoiceNumber = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, sres.StatusCode);

        var pd = await sres.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.Contains("Quantity", pd!.Errors.Keys);
        Assert.Contains("InvoiceNumber", pd!.Errors.Keys);
    }

    [Fact]
    public async Task Order_ComItensNulos_Deve400()
    {
        var client = _factory.CreateClient();
        var sellerToken = await SignupAndLoginAsync(client, "seller-val@local", "seller123", UserRole.Seller);
        UseBearer(client, sellerToken);

        var ores = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = null!
        });

        Assert.Equal(HttpStatusCode.BadRequest, ores.StatusCode);

        var body = await ores.Content.ReadAsStringAsync();
        Assert.Contains("No items.", body);
    }

    [Fact]
    public async Task Order_ItemComQuantidadeNegativa_Deve400()
    {
        var client = _factory.CreateClient();
        var sellerToken = await SignupAndLoginAsync(client, "seller-val2@local", "seller123", UserRole.Seller);
        UseBearer(client, sellerToken);

        var ores = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new()
        {
            new CreateOrderItemRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = -1
            }
        }
        });

        Assert.Equal(HttpStatusCode.BadRequest, ores.StatusCode);

        var body = await ores.Content.ReadAsStringAsync();
        Assert.Contains("All quantities must be > 0.", body);
    }
}
