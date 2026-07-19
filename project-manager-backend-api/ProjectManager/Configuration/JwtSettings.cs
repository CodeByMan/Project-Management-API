using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ProjectManager.Configuration
{
    public sealed class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 30;

        public static JwtSettings Load(IConfiguration configuration)
        {
            var settings = configuration.GetSection(SectionName).Get<JwtSettings>()
                ?? throw new InvalidOperationException("JWT configuration is missing.");

            if (string.IsNullOrWhiteSpace(settings.Key) || Encoding.UTF8.GetByteCount(settings.Key) < 64)
            {
                throw new InvalidOperationException("JWT Key must be configured and contain at least 64 bytes.");
            }

            if (string.IsNullOrWhiteSpace(settings.Issuer))
            {
                throw new InvalidOperationException("JWT Issuer must be configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.Audience))
            {
                throw new InvalidOperationException("JWT Audience must be configured.");
            }

            if (settings.AccessTokenMinutes is < 5 or > 120)
            {
                throw new InvalidOperationException("JWT AccessTokenMinutes must be between 5 and 120 minutes.");
            }

            return settings;
        }

        public TokenValidationParameters CreateValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        }
    }
}

