using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace StockApi.Tests.Infra;

public sealed class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestConfig.JwtKey,
                ["Jwt:Issuer"] = TestConfig.JwtIssuer,
                ["Jwt:Audience"] = TestConfig.JwtAudience,
                ["Jwt:TokenExpirationMinutes"] = TestConfig.TokenMinutes.ToString(),
                ["RabbitMq:Disabled"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(o =>
            {
                var keyBytes = Encoding.UTF8.GetBytes(TestConfig.JwtKey);
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Jwt__Key", TestConfig.JwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestConfig.JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestConfig.JwtAudience);
        Environment.SetEnvironmentVariable("Jwt__TokenExpirationMinutes", TestConfig.TokenMinutes.ToString());
        Environment.SetEnvironmentVariable("RabbitMq__Disabled", "true");
        return base.CreateHost(builder);
    }
}
