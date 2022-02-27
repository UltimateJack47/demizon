using DomProject.Common.Configuration;
using DomProject.Core.Extensions;
using MudBlazor.Services;
using DomProject.Dal;
using DomProject.Dal.Extensions;
using DomProject.Mvc.Services.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Connect with mobile on local network setting:
builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Parse("192.168.0.3"), 7272));
builder.WebHost.UseKestrel();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Local.json", true, true);
DefaultConnectionString.DbConnectionString = builder.Configuration.GetConnectionString("Default");
    
// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDatabase(DefaultConnectionString.DbConnectionString);
builder.Services.AddTransient<DomProjectContext>();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddCoreServices();
builder.Services.AddMvcServices();

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
