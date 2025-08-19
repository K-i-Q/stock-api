namespace StockApi.Dtos;

public record SignupRequest(string Name, string Email, string Password, string Role);
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token);