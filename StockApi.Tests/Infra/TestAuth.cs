using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using StockApi.Dtos;
using StockApi.Models;

namespace StockApi.Tests.Infra;

public static class TestAuth
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task<string> SignupAndLoginAsync(HttpClient client, UserRole role)
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        const string password = "p4ssw0rd!";
        return await SignupAndLoginAsync(client, email, password, role);
    }

    public static async Task<string> SignupAndLoginAsync(HttpClient client, string email, string password, UserRole role)
    {
        var signup = new SignupRequest
        {
            Name = "Test",
            Email = email,
            Password = password,
            Role = role
        };

        var sres = await client.PostAsJsonAsync("/auth/signup", signup, JsonOpts);
        Assert.True(
            sres.StatusCode is System.Net.HttpStatusCode.Created or System.Net.HttpStatusCode.BadRequest,
            $"Signup expected 201/400, got {(int)sres.StatusCode} {sres.StatusCode}. Body: {await sres.Content.ReadAsStringAsync()}"
        );

        var login = new LoginRequest { Email = email, Password = password };
        var lres = await client.PostAsJsonAsync("/auth/login", login, JsonOpts);

        if (!lres.IsSuccessStatusCode)
        {
            var body = await lres.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Login failed: {(int)lres.StatusCode} {lres.StatusCode}\nBody: {body}");
        }

        var data = await lres.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts);
        Assert.NotNull(data);
        Assert.False(string.IsNullOrWhiteSpace(data!.Token));
        return data.Token!;
    }

    public static void UseBearer(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new("Bearer", token);
}
