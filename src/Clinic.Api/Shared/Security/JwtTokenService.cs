using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Clinic.Api.Modules.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Api.Shared.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var expiresUtc = DateTimeOffset.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(CreateSecurityKey(options), SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresUtc);
    }

    public RefreshToken CreateRefreshToken(Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshToken(userId, token, DateTimeOffset.UtcNow.AddDays(options.RefreshTokenDays));
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = CreateSecurityKey(options),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

    private static SymmetricSecurityKey CreateSecurityKey(JwtOptions options)
    {
        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresUtc);
