using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Badri.TradeFlow;

// The Entra directory ID is the identity provider tenant, NOT the trader's tenant ID.
public sealed class TraderMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string EntraObjectId { get; set; } = "";
    public TraderRole Role { get; set; }
    public bool Active { get; set; } = true;
}

public static class TradeIdentity
{
    public static void AddIdentity(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization();
        if (builder.Environment.IsDevelopment()) return;

        var directoryId = builder.Configuration["AzureAd:TenantId"];
        var apiClientId = builder.Configuration["AzureAd:ClientId"];
        var spaClientId = builder.Configuration["AzureAd:SpaClientId"];
        if (!Guid.TryParse(directoryId, out var directory) || !Guid.TryParse(apiClientId, out var audience)
            || !Guid.TryParse(spaClientId, out _))
            throw new InvalidOperationException("Production requires valid AzureAd:TenantId, AzureAd:ClientId and AzureAd:SpaClientId.");

        var authority = $"https://login.microsoftonline.com/{directory:D}/v2.0";
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience.ToString("D");
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = audience.ToString("D"),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });
    }

    public static async Task<Actor?> ResolveActorAsync(HttpContext context, TradeDb db,
        IConfiguration config, bool development)
    {
        if (development)
        {
            // Disabled by default even in development. Explicit local opt-in only.
            if (!config.GetValue<bool>("DevOnly:EnableIdentityHeaders")) return null;
            var headers = context.Request.Headers;
            if (!Guid.TryParse(headers["X-Dev-Tenant"], out var tenantId)
                || string.IsNullOrWhiteSpace(headers["X-Dev-User"])
                || !Enum.TryParse<TraderRole>(headers["X-Dev-Role"], true, out var role))
                return null;
            return new Actor(tenantId, headers["X-Dev-User"].ToString(), role);
        }

        if (context.User.Identity?.IsAuthenticated != true) return null;
        var objectId = context.User.FindFirst("oid")?.Value
                       ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var tokenDirectory = context.User.FindFirst("tid")?.Value;
        var scopes = context.User.FindFirst("scp")?.Value;
        if (!Guid.TryParse(objectId, out var userId)
            || !Guid.TryParse(tokenDirectory, out var tokenTenant)
            || !Guid.TryParse(config["AzureAd:TenantId"], out var configuredTenant)
            || tokenTenant != configuredTenant
            || !(scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("access_as_user") ?? false))
            return null;

        // R1 is intentionally one-trader-membership-per-identity. Do not infer the
        // business tenant from a user-provided header or an arbitrary token claim.
        var memberships = await db.TraderMemberships.AsNoTracking()
            .Where(x => x.EntraObjectId == userId.ToString("D") && x.Active)
            .Take(2).ToListAsync(context.RequestAborted);
        if (memberships.Count != 1) return null;
        var membership = memberships[0];
        return new Actor(membership.TenantId, userId.ToString("D"), membership.Role);
    }
}
