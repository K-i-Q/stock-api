using System.ComponentModel.DataAnnotations;

namespace StockApi.Models;

public class User
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = default!;

    [Required, MinLength(60)]
    public string PasswordHash { get; set; } = default!;

    [Required, StringLength(20)]
    public UserRole Role { get; set; } = default!;
}
