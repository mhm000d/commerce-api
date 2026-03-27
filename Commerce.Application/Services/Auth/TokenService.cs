using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Commerce.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Application.Services.Auth;

public class TokenService(IConfiguration config) : ITokenService
{
    private readonly string _key = config["Jwt:Key"]
                                   ?? throw new InvalidOperationException("key is not configured.");

    private readonly string _issuer = config["Jwt:Issuer"]
                                      ?? throw new InvalidOperationException("Issuer is not configured.");

    private readonly string _audience = config["Jwt:Audience"]
                                        ?? throw new InvalidOperationException("Audience is not configured.");

    private readonly int _accessTokenExpirationMinutes = int.Parse(config["Jwt:AccessTokenExpirationMinutes"]
                                                                   ?? throw new InvalidOperationException(
                                                                       "AccessTokenExpirationMinutes is not configured.")
    );

    private readonly int _refreshTokenExpirationDays = int.Parse(config["Jwt:RefreshTokenExpirationDays"]
                                                                 ?? throw new InvalidOperationException(
                                                                     "RefreshTokenExpirationDays is not configured.")
    );

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string rawToken, string tokenHash) GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = TokenHasher.Hash(rawToken);

        return (rawToken, tokenHash);
    }

    public DateTimeOffset RefreshTokenExpiresAt()
        => DateTimeOffset.UtcNow.AddDays(_refreshTokenExpirationDays);

    public DateTimeOffset AccessTokenExpiresAt()
        => DateTimeOffset.UtcNow.AddMinutes(_accessTokenExpirationMinutes);
}

/// <summary>
/// Input:  raw Base64 string (~44 chars)
/// Output: lowercase hex string (always 64 chars)
/// </summary>
public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}