using System.ComponentModel.DataAnnotations;
using StockApi.Models;

namespace StockApi.Dtos;

public class SignupRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public UserRole Role { get; set; }
}
