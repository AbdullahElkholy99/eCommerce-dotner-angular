using System.ComponentModel.DataAnnotations;

namespace Ecom.Core.DTO.Account;

public record LoginDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record RegisterDTO : LoginDTO
{
    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

}

public record RestPasswordDTO : LoginDTO
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

public record ActiveAccountDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}