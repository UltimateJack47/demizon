using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Append.Blazor.Notifications;
using Demizon.Common.Configuration;
using Demizon.Core.Extensions;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Microsoft.Extensions.Options;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Demizon.Mvc.Services.Authentication;
using Demizon.Mvc.Services.Extensions;
using Demizon.Mvc.Services.Notification;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Local.json", true, true)
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);
var defaultConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddOptions<UploadSettings>()
    .BindConfiguration("Upload");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.AddCoreServices();
builder.Services.AddMvcServices();
builder.Services.AddAuthenticationServices(builder.Configuration, builder.Environment);

builder.Services.AddOptions<VapidSettings>()
    .BindConfiguration("Vapid")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<GoogleCalendarSettings>()
    .BindConfiguration("GoogleCalendar")
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddNotifications();
builder.Services.AddHostedService<NotificationHostedService>();

builder.Services.AddDatabase(defaultConnectionString);

// Health check – ověří dostupnost DB
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DemizonContext>("database");


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// R7.8: API endpointy jsou zdokumentovány jako Minimal API – strojově čitelný popis
// Swagger UI vyžaduje Microsoft.AspNetCore.OpenApi, které je v konfliktu s UseRazorSourceGenerator=false.
// Vygenerovaná dokumentace přístupná přes /api/endpoints (development only).
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/endpoints", () => new[]
    {
        new { method = "POST", path = "/api/auth/token", description = "Vydá JWT token (login/heslo)" },
        new { method = "POST", path = "/api/auth/refresh", description = "Vydá nový JWT token z refresh tokenu" },
        new { method = "GET",  path = "/health", description = "Health check stav databáze" },
    }).ExcludeFromDescription();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exceptionFeature is not null)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(exceptionFeature.Error, "Neošetřená výjimka v požadavku {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            }
            else
            {
                context.Response.Redirect("/Error");
            }
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Position of localization culture switching is crucial between UseStaticFiles and UseRouting 
var supportedCultures = new[] {"en-US", "cs-CZ"};
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPost("/ProcessLogin",
    async (HttpContext context, IAuthenticationService service) => await service.Login(context))
    .RequireRateLimiting("auth");
app.MapGet("/Logout", async (HttpContext context, IAuthenticationService service) => await service.Logout(context));

// Google Calendar OAuth – inicializace propojení
app.MapGet("/google/connect", (HttpContext ctx, IOptions<GoogleCalendarSettings> opts) =>
{
    var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    ctx.Response.Cookies.Append("gcal_oauth_state", state, new CookieOptions
    {
        HttpOnly = true,
        Secure = !ctx.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromMinutes(10)
    });

    var s = opts.Value;
    var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(s.ClientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(s.RedirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/calendar")}" +
                  $"&access_type=offline" +
                  $"&prompt=consent" +
                  $"&state={Uri.EscapeDataString(state)}";

    return Results.Redirect(authUrl);
}).RequireAuthorization();

// Google Calendar OAuth – callback po autorizaci
app.MapGet("/google/callback", async (
    HttpContext ctx,
    IGoogleCalendarService gcalService,
    IMemberService memberService,
    IOptions<GoogleCalendarSettings> opts,
    ILogger<Program> logger,
    string? code,
    string? state,
    string? error) =>
{
    if (!string.IsNullOrEmpty(error))
    {
        logger.LogWarning("Google OAuth zamítnut uživatelem: {Error}", error);
        return Results.Redirect("/Admin/Profile?gcal=denied");
    }

    var cookieState = ctx.Request.Cookies["gcal_oauth_state"];
    ctx.Response.Cookies.Delete("gcal_oauth_state");

    if (string.IsNullOrEmpty(state) || state != cookieState)
    {
        logger.LogWarning("Google OAuth neplatný state parametr (CSRF ochrana).");
        return Results.Redirect("/Admin/Profile?gcal=error");
    }

    if (string.IsNullOrEmpty(code))
        return Results.Redirect("/Admin/Profile?gcal=error");

    var login = ctx.User.FindFirstValue(ClaimTypes.Name);
    if (login is null)
        return Results.Redirect("/Login");

    var member = memberService.GetOneByLogin(login);
    if (member is null)
        return Results.Redirect("/Admin/Profile?gcal=error");

    var refreshToken = await gcalService.ExchangeCodeForRefreshTokenAsync(code);
    if (refreshToken is null)
    {
        logger.LogError("Selhala výměna kódu za token pro člena {Login}.", login);
        return Results.Redirect("/Admin/Profile?gcal=error");
    }

    member.GoogleRefreshToken = refreshToken;
    member.GoogleCalendarId = opts.Value.DefaultCalendarId;
    member.GoogleConnectedAt = DateTime.UtcNow;
    await memberService.UpdateAsync(member.Id, member);

    logger.LogInformation("Google Calendar úspěšně propojen pro člena {Login}.", login);
    return Results.Redirect("/Admin/Profile?gcal=connected");
}).RequireAuthorization();

// JWT token endpoint – pro budoucí API integraci (mobilní klient, externí nástroje)
app.MapPost("/api/auth/token", async (HttpContext context, IAuthenticationService service) =>
    await service.IssueToken(context))
    .RequireRateLimiting("auth");

// Refresh token endpoint – vydá nový access token na základě platného refresh tokenu
app.MapPost("/api/auth/refresh", async (HttpContext context, RefreshTokenService refreshService,
    TokenService tokenService, Demizon.Core.Services.Member.IMemberService memberService) =>
{
    string? rawRefreshToken = null;

    if (context.Request.HasJsonContentType())
    {
        var body = await context.Request.ReadFromJsonAsync<RefreshRequest>();
        rawRefreshToken = body?.RefreshToken;
    }
    else
    {
        rawRefreshToken = context.Request.Form["refreshToken"].ToString();
    }

    if (string.IsNullOrWhiteSpace(rawRefreshToken))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Missing refresh token." });
        return;
    }

    var memberId = await refreshService.ValidateAsync(rawRefreshToken);
    if (memberId is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired refresh token." });
        return;
    }

    var member = await memberService.GetOneAsync(memberId.Value);
    var newAccessToken = tokenService.GenerateToken(member);
    var jwtSettings = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Demizon.Common.Configuration.JwtSettings>>().Value;
    var newRefreshToken = await refreshService.CreateAsync(member.Id, jwtSettings.RefreshTokenExpirationDays);

    await context.Response.WriteAsJsonAsync(new
    {
        token = newAccessToken,
        refreshToken = newRefreshToken,
        expiresIn = tokenService.ExpirationMinutes * 60
    });
}).RequireRateLimiting("auth");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
    }
});
app.MapGet("/SetLanguage/{culture}", (HttpContext context, string culture) =>
{
    var cultureInfo = culture == "cs" ? new CultureInfo("cs-CZ") : new CultureInfo("en-US");

    var requestCulture = new RequestCulture(cultureInfo, cultureInfo);
    var cookieName = CookieRequestCultureProvider.DefaultCookieName;
    var cookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);

    context.Response.Cookies.Append(cookieName, cookieValue);
    return Task.FromResult(Results.Redirect(context.Request.Headers.Referer.ToString()));
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.ApplyDbMigrations();

app.Run();

// Lokální record pro deserializaci těla refresh požadavku
internal sealed record RefreshRequest(string RefreshToken);
