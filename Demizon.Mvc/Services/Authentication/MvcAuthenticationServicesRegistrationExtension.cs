using System.Text;
using Demizon.Common.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.IdentityModel.Tokens;

namespace Demizon.Mvc.Services.Authentication;

public static class MvcAuthenticationServicesRegistrationExtension
{
    /// <summary>
    /// Collection of used services in the Api
    /// </summary>
    /// <param name="services">Collection of used services</param>
    /// <returns>Services that are used in the Api</returns>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOptions<JwtSettings>()
            .BindConfiguration("Jwt")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
                          ?? throw new InvalidOperationException("JWT configuration ('Jwt' section) is missing.");

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.Secure = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.HttpOnly = HttpOnlyPolicy.Always;
        });

        // Cookie = výchozí schéma pro Blazor Server (SignalR circuit)
        // JWT Bearer = přidáno jako druhé schéma pro budoucí API endpointy
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                // Changed roles or a deleted member end the web session: signing in again issues current roles.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var validator = context.HttpContext.RequestServices.GetRequiredService<MemberSessionValidator>();
                    if (context.Principal is not null && await validator.IsCurrentAsync(context.Principal))
                        return;

                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
                // A 401 here makes the mobile client refresh, and the refreshed token carries current roles.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices.GetRequiredService<MemberSessionValidator>();
                        if (context.Principal is null || !await validator.IsCurrentAsync(context.Principal))
                            context.Fail("Token no longer matches the member's roles.");
                    }
                };
            });

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<MemberSessionValidator>();
        services.AddScoped<AuthenticationStateProvider, MemberRevalidatingAuthenticationStateProvider>();

        return services;
    }
}
