using System.Globalization;
using System.Security.Claims;
using System.Text;
using Common.DTOs.Auth;
using Common.Interfaces;
using Common.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Services.Interfaces;

namespace API.Auth;

/// <summary>Issues HS256-signed access tokens (spec 6.1, 15 minutes).</summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        _signingCredentials = new SigningCredentials(CreateSigningKey(_options.SigningKey), SecurityAlgorithms.HmacSha256);
    }

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    public AccessToken Issue(AccessTokenSubject subject)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.UserId, subject.UserId.ToString(CultureInfo.InvariantCulture)),
            new(AuthClaimTypes.OrganizationId, subject.OrganizationId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            new(AuthClaimTypes.DepartmentId, subject.DepartmentId.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            new(AuthClaimTypes.MustChangePassword, subject.MustChangePassword ? "true" : "false", ClaimValueTypes.Boolean),
            new(AuthClaimTypes.TokenId, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(subject.Roles.Select(role => new Claim(AuthClaimTypes.Role, role.ToString())));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials,
        });

        return new AccessToken(token, expiresAt);
    }
}
