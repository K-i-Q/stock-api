using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;
using Microsoft.AspNetCore.Mvc;

public class BranchCoverage_Extras_Tests : IClassFixture<CustomWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BranchCoverage_Extras_Tests(CustomWebAppFactory factory)
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
    public async Task Orders_Post_Sucesso_DeveBaixarEstoque()
    {
        var client = _factory.CreateClient();

        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(adminToken);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Produto Pedido OK",
            Price = 100m,
            Description = "Teste"
        });
        cres.EnsureSuccessStatusCode();
        var p = await cres.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(p);

        var sres = await client.PostAsJsonAsync("/stock/entries", new StockEntryRequest
        {
            ProductId = p!.Id,
            Quantity = 10,
            InvoiceNumber = "NF-123"
        });
        sres.EnsureSuccessStatusCode();

        var sellerToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(sellerToken);

        var ores = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "000.111.222-33",
            SellerName = "Vendedor X",
            Items = new() { new CreateOrderItemRequest { ProductId = p.Id, Quantity = 3 } }
        });
        Assert.Equal(HttpStatusCode.Created, ores.StatusCode);

        client.UseBearer(adminToken);
        var get = await client.GetAsync($"/products/{p.Id}");
        get.EnsureSuccessStatusCode();

        var atualizado = await get.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(atualizado);
        Assert.Equal(7, atualizado!.Stock);
    }

    [Fact]
    public async Task Root_DeveResponder_OK_ou_RedirecionarParaSwagger()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/");
        Assert.True(
            res.StatusCode == HttpStatusCode.OK ||
            res.StatusCode == HttpStatusCode.Redirect ||
            res.StatusCode == HttpStatusCode.Found,
            $"Esperado 200 ou 302/Found, mas veio: {res.StatusCode}"
        );
    }

    [Fact]
    public async Task Products_Get_Autenticado_Deve200_E_RetornarLista()
    {
        var client = _factory.CreateClient();
        var adminToken = await SignupAndLoginAsync(client, "admin-list@local", "admin123", UserRole.Admin);
        UseBearer(client, adminToken);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Produto Teste",
            Price = 10m,
            Description = "ok"
        });
        Assert.Equal(HttpStatusCode.Created, cres.StatusCode);

        var res = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<Product[]>();
        Assert.NotNull(list);
        Assert.True(list!.Length >= 1);
        Assert.Contains(list, p => p.Name == "Produto Teste");
    }

    [Fact]
    public async Task Auth_Signup_EmailJaExiste_Deve400()
    {
        var client = _factory.CreateClient();

        var payload = new SignupRequest
        {
            Name = "User",
            Email = "duplicado@local",
            Password = "abc123",
            Role = UserRole.Seller
        };

        var s1 = await client.PostAsJsonAsync("/auth/signup", payload);
        Assert.True(
            s1.StatusCode == HttpStatusCode.Created || s1.StatusCode == HttpStatusCode.BadRequest,
            $"Esperado 201 ou 400 na primeira tentativa, veio {s1.StatusCode}"
        );

        var s2 = await client.PostAsJsonAsync("/auth/signup", payload);
        Assert.Equal(HttpStatusCode.BadRequest, s2.StatusCode);
    }

    [Fact]
    public async Task StockEntries_Post_ProductInexistente_400()
    {
        var client = _factory.CreateClient();

        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        UseBearer(client, adminToken);

        var res = await client.PostAsJsonAsync("/stock/entries", new StockEntryRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            InvoiceNumber = "NF-123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Product not found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Auth_Signup_SenhaCurta_400()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/signup", new SignupRequest
        {
            Name = "Curta",
            Email = "curta@local",
            Password = "123", // < 6
            Role = UserRole.Seller
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("at least 6", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Products_Post_PrecoZero_400_ProblemDetails()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        UseBearer(client, adminToken);

        var res = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Produto Zero",
            Description = "desc",
            Price = 0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var pd = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.True(pd!.Errors.ContainsKey("Price"));
    }

    [Fact]
    public async Task Orders_Get_Inexistente_404()
    {
        var client = _factory.CreateClient();
        var sellerToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        UseBearer(client, sellerToken);

        var res = await client.GetAsync($"/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
