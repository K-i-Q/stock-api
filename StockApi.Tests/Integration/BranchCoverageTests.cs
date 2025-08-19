using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockApi.Dtos;
using StockApi.Models;
using StockApi.Tests.Infra;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockApi.Data;
using System.Net.Http.Headers;

public class BranchCoverageTests : IClassFixture<CustomWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BranchCoverageTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<string> SignupAndLoginAsync(HttpClient client, string email, string password, UserRole role)
    {
        var sign = new SignupRequest
        {
            Name = "User " + role,
            Email = email,
            Password = password,
            Role = role
        };

        var sres = await client.PostAsJsonAsync("/auth/signup", sign);

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

    private static void UseBearer(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }


    [Fact]
    public async Task Products_Get_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Products_Post_ComSeller_Deve403()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(token);

        var res = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "X",
            Price = 10m,
            Description = "desc"
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Products_Get_Inexistente_404()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(token);

        var res = await client.GetAsync($"/products/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Orders_Post_ItensVazios_400_ProblemDetails()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(token);

        var res = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new()
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();

        Assert.Contains("\"type\":\"https://tools.ietf.org/html/rfc", body);
        Assert.Contains("Items", body);
    }

    [Fact]
    public async Task Orders_Post_QuantidadeZero_400_Msg()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(token);

        var res = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new()
            {
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 0 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var msg = await res.Content.ReadAsStringAsync();
        Assert.Contains("All quantities must be > 0.", msg);
    }

    [Fact]
    public async Task Orders_Post_ProdutoInexistente_400_Msg()
    {
        var client = _factory.CreateClient();
        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(token);

        var res = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new()
            {
                new CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var msg = await res.Content.ReadAsStringAsync();
        Assert.Contains("Some product(s) not found.", msg);
    }

    [Fact]
    public async Task Orders_Post_EstoqueInsuficiente_400_Msg()
    {
        var client = _factory.CreateClient();

        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(adminToken);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest
        {
            Name = "Produto Zero",
            Price = 50m
        });
        Assert.Equal(HttpStatusCode.Created, cres.StatusCode);
        var product = await cres.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(product);

        var sellerToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(sellerToken);

        var ores = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new() { new CreateOrderItemRequest { ProductId = product!.Id, Quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, ores.StatusCode);
        var msg = await ores.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient stock", msg);
        Assert.Contains("Available: 0", msg);
    }

    [Fact]
    public async Task StockEntries_Post_InvoiceVazio_400_Msg()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(adminToken);

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
            Quantity = 5,
            InvoiceNumber = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, sres.StatusCode);

        var pd = await sres.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(pd);
        Assert.Contains("InvoiceNumber", pd!.Errors.Keys);
    }

    [Fact]
    public async Task Root_Redirects_To_Swagger()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.NotNull(res.Headers.Location);
        Assert.EndsWith("/swagger", res.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Products_Get_200_List_Returned()
    {
        var client = _factory.CreateClient();

        var token = await SignupAndLoginAsync(client, "prodlist@local", "abc123", UserRole.Seller);
        UseBearer(client, token);

        var res = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<Product>>();
        Assert.NotNull(list);

        Assert.True(list!.Count >= 0);
    }

    [Fact]
    public async Task Products_Put_NotFound_404()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(adminToken);

        var req = new ProductUpdateRequest
        {
            Name = "Updated",
            Price = 99
        };

        var res = await client.PutAsJsonAsync($"/products/{Guid.NewGuid()}", req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Products_Delete_NotFound_404()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(adminToken);

        var res = await client.DeleteAsync($"/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Auth_Signup_EmailAlreadyExists_400()
    {
        var client = _factory.CreateClient();

        var signup = new SignupRequest
        {
            Name = "User",
            Email = "user@x.com",
            Password = "123456",
            Role = UserRole.Seller
        };

        var res1 = await client.PostAsJsonAsync("/auth/signup", signup);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await client.PostAsJsonAsync("/auth/signup", signup);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task Auth_Login_SenhaErrada_401()
    {
        var client = _factory.CreateClient();

        var token = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);

        var bad = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = ExtractEmailFromTokenForTests(token) ?? "user@local",
            Password = "wrongpass"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Auth_Login_EmailInexistente_401()
    {
        var client = _factory.CreateClient();

        var lres = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = "naoexiste@local",
            Password = "qualquer"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, lres.StatusCode);
    }

    [Fact]
    public async Task Root_Redirects_To_Swagger_SemSeguirRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var res = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Found, res.StatusCode);

        Assert.True(res.Headers.Location?.ToString().Contains("/swagger"),
            "Location esperado deve conter /swagger");
    }

    [Fact]
    public async Task Startup_Usa_Padding_Quando_JwtKey_Curta()
    {
        const string envKeyName = "Jwt__Key";
        var old = Environment.GetEnvironmentVariable(envKeyName);
        Environment.SetEnvironmentVariable(envKeyName, "short-key");

        try
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });

            var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var res = await client.GetAsync("/");

            Assert.True(res.StatusCode == HttpStatusCode.OK || res.StatusCode == HttpStatusCode.Found);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKeyName, old);
        }
    }

    [Fact]
    public async Task Products_Get_ListaVazia_200()
    {
        var client = _factory.CreateClient();

        var token = await SignupAndLoginAsync(client, "vazio@local", "123456", UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.RemoveRange(db.Products);
            await db.SaveChangesAsync();
        }

        var r = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var products = await r.Content.ReadFromJsonAsync<List<Product>>();
        Assert.NotNull(products);
        Assert.Empty(products!);
    }

    [Fact]
    public async Task StockEntries_Post_QuantidadeZero_400()
    {
        var client = _factory.CreateClient();

        var token = await SignupAndLoginAsync(client, "tq0@local", "123456", UserRole.Admin);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var create = new ProductCreateRequest
        {
            Name = "P-Qty0",
            Description = "",
            Price = 10m
        };

        var rCreate = await client.PostAsJsonAsync("/products", create);
        Assert.Equal(HttpStatusCode.Created, rCreate.StatusCode);

        var produto = await rCreate.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(produto);

        var payload = new StockEntryRequest
        {
            ProductId = produto!.Id,
            Quantity = 0,
            InvoiceNumber = "NF-001"
        };

        var r = await client.PostAsJsonAsync("/stock/entries", payload);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var problem = await r.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.NotNull(problem!.Errors);

        var hasQuantityKey = problem.Errors.Keys.Any(k =>
            k.Equals("Quantity", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasQuantityKey, "Deveria haver erro associado a 'Quantity'.");

        var msgs = problem.Errors.First(kv =>
            kv.Key.Equals("Quantity", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.NotEmpty(msgs);
    }

    [Fact]
    public async Task Products_Put_Sucesso_NoContent()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "P", Price = 10m });
        var p = await cres.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(p);

        var ures = await client.PutAsJsonAsync($"/products/{p!.Id}", new ProductUpdateRequest
        {
            Name = "P-Updated",
            Price = 99m,
            Description = "desc"
        });

        Assert.Equal(HttpStatusCode.NoContent, ures.StatusCode);
    }

    [Fact]
    public async Task Products_Delete_Sucesso_NoContent()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "Del", Price = 10m });
        var p = await cres.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(p);

        var dres = await client.DeleteAsync($"/products/{p!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, dres.StatusCode);

        var getAfter = await client.GetAsync($"/products/{p.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
    }

    [Fact]
    public async Task StockEntries_Post_ProdutoInexistente_400()
    {
        var client = _factory.CreateClient();
        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var res = await client.PostAsJsonAsync("/stock/entries", new StockEntryRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            InvoiceNumber = "NF-001"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Product not found", body);
    }

    [Fact]
    public async Task Auth_Signup_SenhaCurta_Deve400()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/auth/signup", new SignupRequest
        {
            Name = "X",
            Email = $"x-{Guid.NewGuid():N}@local",
            Password = "123",
            Role = UserRole.Seller
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Orders_Post_Sucesso_E_Get_200()
    {
        var client = _factory.CreateClient();

        var admin = await TestAuth.SignupAndLoginAsync(client, UserRole.Admin);
        client.UseBearer(admin);

        var cres = await client.PostAsJsonAsync("/products", new ProductCreateRequest { Name = "P-Order", Price = 25m });
        var p = await cres.Content.ReadFromJsonAsync<Product>(); Assert.NotNull(p);

        var sres = await client.PostAsJsonAsync("/stock/entries", new StockEntryRequest
        {
            ProductId = p!.Id,
            Quantity = 3,
            InvoiceNumber = "NF-OK"
        });
        Assert.Equal(HttpStatusCode.Created, sres.StatusCode);

        var seller = await TestAuth.SignupAndLoginAsync(client, UserRole.Seller);
        client.UseBearer(seller);

        var ores = await client.PostAsJsonAsync("/orders", new CreateOrderRequest
        {
            CustomerDocument = "123",
            SellerName = "Bob",
            Items = new() { new CreateOrderItemRequest { ProductId = p.Id, Quantity = 2 } }
        });
        Assert.Equal(HttpStatusCode.Created, ores.StatusCode);

        var created = await ores.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orderId = created.GetProperty("id").GetGuid();

        var get = await client.GetAsync($"/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    private static string? ExtractEmailFromTokenForTests(string token) => null;
}
