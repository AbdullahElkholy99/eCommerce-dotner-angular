using Ecom.Core.Entity.Account;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ecom.Infrastructure.Repositories.Service;

public class GenerateToken : IGenerateToken
{
    private readonly IConfiguration configuration;
    public GenerateToken(IConfiguration configuration)
    {
        this.configuration = configuration;
    }
    public string GetAndCreateToken(User user)
    {

        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        string secret = configuration["Token:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT configuration is missing. Configure 'Token:Secret' via .NET User Secrets or an environment variable.");
        }

        byte[] key = Encoding.UTF8.GetBytes(secret);
        if (key.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT configuration is invalid. 'Token:Secret' must be at least 32 bytes (256 bits) for HMAC-SHA256 signing.");
        }

        SigningCredentials credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature);

        SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.Now.AddHours(30), // Adjust token expiration time as needed
            Issuer = configuration["Token:Issuer"],
            SigningCredentials = credentials,
            NotBefore = DateTime.UtcNow
        };

        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

        string tokenString = tokenHandler.WriteToken(token);

        // Log the token to ensure it looks correct
        Console.WriteLine("Generated JWT: " + tokenString);

        return tokenString;
    }

}