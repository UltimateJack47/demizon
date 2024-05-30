using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Demizon.Common.Configuration;
using Demizon.Core.Extensions;
using Demizon.Core.Services.S3;
using Demizon.Dal.Extensions;
using Demizon.Mvc.Services.Authentication;
using Demizon.Mvc.Services.Extensions;
using Imagekit.Sdk;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Local.json", true, true)
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile("appsettings.Development.json", true, true);
var defaultConnectionString = builder.Configuration.GetConnectionString("Default");

//builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection("Upload"));
builder.Services.AddOptions<UploadSettings>()
    .BindConfiguration("Upload");

builder.Services.AddOptions<ImagekitSettings>()
    .BindConfiguration("ImageKit")
    .Validate(x => !string.IsNullOrWhiteSpace(x.PrivateKey))
    .Validate(x => !string.IsNullOrWhiteSpace(x.PublicKey))
    .Validate(x => !string.IsNullOrWhiteSpace(x.UrlEndpoint));

if (defaultConnectionString != null) DefaultConnectionString.DbConnectionString = defaultConnectionString;

// Connect with mobile on local network setting:
var ipAddress = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .FirstOrDefault(ni =>
                        ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                        && ni.OperationalStatus == OperationalStatus.Up
                        && ni.GetIPProperties().GatewayAddresses.FirstOrDefault() != null
                        && ni.GetIPProperties().UnicastAddresses
                            .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork) != null
                    )
                    ?.GetIPProperties()
                    .UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString()
                ?? string.Empty;

builder.WebHost.UseKestrel(options =>
{
    if (!string.IsNullOrWhiteSpace(ipAddress))
    {
        options.Listen(IPAddress.Parse(ipAddress), 7272, listenOptions => { listenOptions.UseHttps(); });
    }

    options.Listen(IPAddress.Loopback, 7272, listenOptions => { listenOptions.UseHttps(); });
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.AddCoreServices();
builder.Services.AddMvcServices();
builder.Services.AddAuthenticationServices();

builder.Services.AddAppLocalizationServices();

builder.Services.AddDatabase(DefaultConnectionString.DbConnectionString);

var imagekitSettings = builder.Configuration.GetSection("ImageKit").Get<ImagekitSettings>();
if (!string.IsNullOrWhiteSpace(imagekitSettings?.PrivateKey))
{
    var imageKit = new ImagekitClient(imagekitSettings!.PublicKey, imagekitSettings.PrivateKey,
        imagekitSettings.UrlEndpoint);
    builder.Services.AddSingleton(imageKit);
    builder.Services.AddTransient<IS3Service, S3Service>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/ProcessLogin",
    async (HttpContext context, IMyAuthenticationService service) => await service.Login(context));
app.MapGet("/Logout", async (HttpContext context, IMyAuthenticationService service) => await service.Logout(context));
app.MapGet("/SetLanguage/{culture}", (HttpContext context, string culture) =>
{
    context.Response.Cookies
        .Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions {Expires = DateTimeOffset.UtcNow.AddYears(1)}
        );
    return Task.FromResult(Results.Redirect(context.Request.Headers.Referer.ToString()));
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.ApplyDbMigrations();

app.Run();
