using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModularMonolith.Modules.Identity.Application.Domain.Users;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class JwtTokenService : Application.Ports.Outbound.ITokenService
{
    private readonly JwtOptions _opts;
    public JwtTokenService(IOptions<JwtOptions> opts) => _opts = opts.Value;

    public (string AccessToken, DateTimeOffset ExpiresAt) GenerateAccessToken(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_opts.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        foreach (var role in user.Roles) claims.Add(new Claim(ClaimTypes.Role, role));

        var token = CreateToken(claims, expires);
        return (token, expires);
    }

    public (string RefreshToken, DateTimeOffset ExpiresAt) GenerateRefreshToken(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddDays(_opts.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("rt", "true"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = CreateToken(claims, expires);
        return (token, expires);
    }

    public bool ValidateRefreshToken(string token, out Guid userId)
    {
        userId = Guid.Empty;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = _opts.Issuer, ValidAudience = _opts.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            var principal = handler.ValidateToken(token, parameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var isRefresh = principal.FindFirst("rt")?.Value == "true";
            return isRefresh && Guid.TryParse(sub, out userId);
        }
        catch { return false; }
    }

    private string CreateToken(List<Claim> claims, DateTimeOffset expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opts.Issuer,
            Audience = _opts.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires.UtcDateTime,
            SigningCredentials = creds,
        };
        var handler = new JwtSecurityTokenHandler();
        var stoken = handler.CreateToken(descriptor);
        return handler.WriteToken(stoken);
    }
}
