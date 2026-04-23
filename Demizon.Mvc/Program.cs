using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Append.Blazor.Notifications;
using Demizon.Common.Configuration;
using Demizon.Core.Extensions;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Demizon.Core.Services.Authentication;
using Demizon.Mvc.Services;
using Demizon.Mvc.Services.Authentication;
using Demizon.Mvc.Services.Extensions;
using Demizon.Mvc.Services.Notification;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway volume mount: počkat až bude /data dostupné a writable
if (builder.Environment.IsProduction())
{
    var dataDir = "/data";
    var maxWait = 60;
    for (int i = 0; i < maxWait; i++)
    {
        try
        {
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
            var probe = Path.Combine(dataDir, ".probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            Console.WriteLine($"/data is writable after {i}s");
            break;
        }
        catch
        {
            if (i == maxWait - 1)
                Console.WriteLine($"WARNING: /data not writable after {maxWait}s");
            else
                Thread.Sleep(1000);
        }
    }
}

builder.Services.AddLocalization();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Local.json", true, true)
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true);

// Railway PostgreSQL: automaticky parsuj DATABASE_URL environment variable
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl) && builder.Environment.IsProduction())
{
    // Parse DATABASE_URL: postgres://user:password@host:port/database
    var databaseUri = new Uri(databaseUrl);
    var userInfo = databaseUri.UserInfo.Split(':');
    var connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};";
    builder.Configuration["ConnectionStrings:Default"] = connectionString;
}

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
builder.Services.AddHostedService<UnifiedNotificationService>();

builder.Services.AddDatabase(defaultConnectionString);

builder.Services.AddControllers();
builder.Services.AddSingleton<FcmService>();
builder.Services.AddScoped<Demizon.Mvc.Services.Notification.WebPushSender>();
builder.Services.AddScoped<Demizon.Mvc.Services.Notification.AttendanceReminderService>();

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
        new { method = "POST", path = "/api/auth/token",           description = "Vydá JWT token (login/heslo)" },
        new { method = "POST", path = "/api/auth/refresh",         description = "Vydá nový JWT token z refresh tokenu" },
        new { method = "GET",  path = "/api/events/upcoming",      description = "Seznam nadcházejících akcí s docházkou přihlášeného člena" },
        new { method = "GET",  path = "/api/events/{id}",          description = "Detail akce" },
        new { method = "GET",  path = "/api/attendances/me",       description = "Docházka přihlášeného člena" },
        new { method = "PUT",  path = "/api/attendances/{eventId}", description = "Upsert docházky na akci (+ Google Calendar sync)" },
        new { method = "GET",  path = "/api/dances",               description = "Seznam viditelných tanců" },
        new { method = "GET",  path = "/api/dances/{id}",          description = "Detail tance" },
        new { method = "POST", path = "/api/notifications/device", description = "Registrace FCM device tokenu" },
        new { method = "DELETE", path = "/api/notifications/device", description = "Odregistrování FCM device tokenu" },
        new { method = "POST", path = "/api/notifications/test",   description = "Odešle testovací FCM notifikaci na vlastní zařízení" },
        new { method = "GET",  path = "/health",                   description = "Health check stav databáze" },
    }).ExcludeFromDescription();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
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
// Cookie provider first (manual user choice), then Slovak-to-Czech remap, then Accept-Language header (auto-detect browser locale)
localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new CookieRequestCultureProvider(),
    new CustomRequestCultureProvider(context =>
    {
        var header = context.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(header))
            return Task.FromResult<ProviderCultureResult?>(null);

        var prefersSlovak = header
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(';')[0].Trim())
            .Any(lang => lang.StartsWith("sk", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<ProviderCultureResult?>(
            prefersSlovak ? new ProviderCultureResult("cs-CZ", "cs-CZ") : null);
    }),
    new AcceptLanguageHeaderRequestCultureProvider()
};
app.UseRequestLocalization(localizationOptions);

app.Services.EnableWalMode();

app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

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
    if (!s.IsConfigured)
        return Results.Problem("Google Calendar integration is not configured on this server.", statusCode: 501);

    var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(s.ClientId!)}" +
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
    if (!opts.Value.IsConfigured)
        return Results.Problem("Google Calendar integration is not configured on this server.", statusCode: 501);

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

    await memberService.ConnectGoogleCalendarAsync(member.Id, refreshToken, opts.Value.DefaultCalendarId);

    logger.LogInformation("Google Calendar úspěšně propojen pro člena {Login}.", login);
    return Results.Redirect("/Admin/Profile?gcal=connected");
}).RequireAuthorization();

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

FcmService.Initialize(app.Configuration, app.Logger);

app.Run();
