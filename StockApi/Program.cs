using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockApi.Data;
using StockApi.Dtos;
using StockApi.Filters;
using StockApi.Models;
using StockApi.Services;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService("StockApi"))
        .SetSampler(new AlwaysOnSampler())
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSource("StockApi")
        .AddConsoleExporter());

builder.Services.AddProblemDetails();

// DB
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (builder.Environment.IsEnvironment("Testing"))
        opt.UseInMemoryDatabase("StockApiTests");
    else
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

// Auth (JWT)
var key = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
{
    key = (key ?? string.Empty).PadRight(32, '0');
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
builder.Services.AddSingleton(signingKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(nameof(UserRole.Admin)));
    options.AddPolicy("SellerOrAdmin", p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Seller)));
});

builder.Services.AddScoped<JwtTokenService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "StockApi", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "StockApi v1");
    c.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

#region AUTH
var auth = app.MapGroup("/auth");

auth.MapPost("/signup", async (SignupRequest req, AppDbContext db) =>
{
    if (req.Password.Length < 6) return Results.BadRequest("Password must be at least 6 characters.");

    var exists = await db.Users.AnyAsync(u => u.Email == req.Email);
    if (exists) return Results.BadRequest("E-mail already registered.");

    var user = new User
    {
        Id = Guid.NewGuid(),
        Name = req.Name.Trim(),
        Email = req.Email.Trim().ToLowerInvariant(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        Role = req.Role
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", new { user.Id, user.Name, user.Email, user.Role });
})
.AddEndpointFilter(new ValidationFilter<SignupRequest>())
.WithOpenApi(op =>
{
    op.Summary = "Add user (Admin/Seller)";
    return op;
});

auth.MapPost("/login", async (LoginRequest req, AppDbContext db, JwtTokenService tokens) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
    if (user is null) return Results.Unauthorized();

    if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = tokens.Generate(user);
    return Results.Ok(new LoginResponse
    {
        Token = token,
        Email = user.Email,
        Role = user.Role.ToString()
    });
})
.AddEndpointFilter(new ValidationFilter<LoginRequest>());
#endregion

#region PRODUCTS (CRUD) - Admin only for write; read open to authenticated
var products = app.MapGroup("/products");

products.MapGet("/", async (AppDbContext db) =>
    await db.Products.AsNoTracking().ToListAsync())
    .RequireAuthorization();

products.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    return p is null ? Results.NotFound() : Results.Ok(p);
})
.RequireAuthorization();

products.MapPost("/", async (ProductCreateRequest req, AppDbContext db) =>
{
    var p = new Product
    {
        Id = Guid.NewGuid(),
        Name = req.Name.Trim(),
        Description = req.Description ?? "",
        Price = req.Price,
        Stock = 0
    };
    db.Products.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{p.Id}", p);
})
.RequireAuthorization("AdminOnly")
.AddEndpointFilter(new ValidationFilter<ProductCreateRequest>());

products.MapPut("/{id:guid}", async (Guid id, ProductUpdateRequest req, AppDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();

    p.Name = req.Name.Trim();
    p.Description = req.Description ?? "";
    p.Price = req.Price;

    await db.SaveChangesAsync();
    return Results.NoContent();
})
.RequireAuthorization("AdminOnly")
.AddEndpointFilter(new ValidationFilter<ProductUpdateRequest>());

products.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Products.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.RequireAuthorization("AdminOnly");
#endregion

#region STOCK - Admin only
var stock = app.MapGroup("/stock").RequireAuthorization("AdminOnly");

stock.MapPost("/entries", async (StockEntryRequest req, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(req.ProductId);
    if (product is null) return Results.BadRequest("Product not found.");
    if (req.Quantity <= 0) return Results.BadRequest("Quantity must be > 0.");
    if (string.IsNullOrWhiteSpace(req.InvoiceNumber)) return Results.BadRequest("Invoice number required.");

    var entry = new StockEntry
    {
        Id = Guid.NewGuid(),
        ProductId = req.ProductId,
        Quantity = req.Quantity,
        InvoiceNumber = req.InvoiceNumber.Trim()
    };
    db.StockEntries.Add(entry);
    product.Stock += req.Quantity;

    await db.SaveChangesAsync();
    return Results.Created($"/stock/entries/{entry.Id}", new { entry.Id });
})
.AddEndpointFilter(new ValidationFilter<StockEntryRequest>());
#endregion

#region ORDERS - Seller or Admin
var orders = app.MapGroup("/orders").RequireAuthorization("SellerOrAdmin");

orders.MapPost("/", async (CreateOrderRequest req, AppDbContext db) =>
{
    if (req.Items is null || req.Items.Count == 0) return Results.BadRequest("No items.");
    if (req.Items.Any(i => i.Quantity <= 0)) return Results.BadRequest("All quantities must be > 0.");

    var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
    var productsDb = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();

    if (productsDb.Count != productIds.Count) return Results.BadRequest("Some product(s) not found.");

    // Validar estoque
    foreach (var it in req.Items)
    {
        var p = productsDb.First(x => x.Id == it.ProductId);
        if (p.Stock < it.Quantity)
            return Results.BadRequest($"Insufficient stock for product '{p.Name}'. Available: {p.Stock}, required: {it.Quantity}");
    }

    var order = new Order
    {
        Id = Guid.NewGuid(),
        CustomerDocument = req.CustomerDocument?.Trim() ?? "",
        SellerName = req.SellerName?.Trim() ?? "",
        CreatedAt = DateTime.UtcNow,
        Items = new()
    };

    foreach (var it in req.Items)
    {
        var p = productsDb.First(x => x.Id == it.ProductId);
        p.Stock -= it.Quantity;

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = p.Id,
            Quantity = it.Quantity,
            UnitPrice = p.Price
        });
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    return Results.Created($"/orders/{order.Id}", new
    {
        order.Id,
        order.CustomerDocument,
        order.SellerName,
        Items = order.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })
    });
})
.AddEndpointFilter(new ValidationFilter<CreateOrderRequest>());

orders.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var o = await db.Orders
        .Include(x => x.Items)
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    return o is null ? Results.NotFound() : Results.Ok(o);
});
#endregion

using (var scope = app.Services.CreateScope())
{
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (env.IsEnvironment("Testing"))
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();
    }
}

app.Run();
public partial class Program { }
