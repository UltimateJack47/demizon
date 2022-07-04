using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Demizon.Common.Configuration;
using Demizon.Core.Extensions;
using Demizon.Dal.Extensions;
using Demizon.Mvc.Services.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Local.json", true, true)
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile("appsettings.Development.json", true, true);
DefaultConnectionString.DbConnectionString = builder.Configuration.GetConnectionString("Default");

// Connect with mobile on local network setting:
var ipAddress = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .FirstOrDefault(ni =>
                        ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                        && ni.OperationalStatus == OperationalStatus.Up
                        && ni.GetIPProperties().GatewayAddresses.FirstOrDefault() != null
                        && ni.GetIPProperties().UnicastAddresses.FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork) != null
                    )
                    ?.GetIPProperties()
                    .UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    ?.Address.ToString()
                ?? string.Empty;

builder.WebHost.UseKestrel(options => { 
    options.Listen(IPAddress.Parse(ipAddress), 7272, listenOptions => { listenOptions.UseHttps(); });
    options.Listen(IPAddress.Loopback, 7272, listenOptions => { listenOptions.UseHttps(); });
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

builder.Services.AddCoreServices();
builder.Services.AddMvcServices();

builder.Services.AddDatabase(DefaultConnectionString.DbConnectionString);

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
