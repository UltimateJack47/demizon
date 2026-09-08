using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CryptoHelper;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.E2E.Infrastructure;

/// <summary>
/// Spustí aplikaci jako <b>samostatný proces</b> na skutečném Kestrelu.
/// </summary>
/// <remarks>
/// Ne <c>WebApplicationFactory</c>: ta staví <c>TestServer</c>, který nemá
/// otevřený socket, takže se na něj prohlížeč nemá jak připojit. Vedlejší
/// výhoda je, že takhle se testuje i to, co se v in-process hostu obchází —
/// statické soubory z výstupního adresáře, načtení konfigurace z pracovního
/// adresáře i Blazor SignalR přes reálné WebSockets.
/// </remarks>
public sealed class AppHost : IAsyncDisposable
{
    public const string AdminLogin = "e2e-admin";
    public const string AdminPassword = "e2e-admin-heslo-1";
    public const string MemberLogin = "e2e-clen";
    public const string MemberPassword = "e2e-clen-heslo-1";

    private readonly string _dbPath;
    private Process? _process;
    private readonly List<string> _output = [];

    public string BaseUrl { get; }

    public AppHost()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"demizon-e2e-{Guid.NewGuid():N}.sqlite");
        BaseUrl = $"http://127.0.0.1:{FreePort()}";
    }

    private static int FreePort()
    {
        // Port se drží jen do zavření listeneru — mezi tím a startem Kestrelu je
        // teoreticky okno na kolizi, ale v praxi ho OS znovu nepřidělí hned.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task StartAsync()
    {
        var dll = AppDll();
        var startInfo = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            // Program.cs čte konfiguraci z Directory.GetCurrentDirectory(),
            // takže appsettings.json musí být vidět z pracovního adresáře.
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var env = startInfo.Environment;
        env["ASPNETCORE_ENVIRONMENT"] = "Development";
        env["ASPNETCORE_URLS"] = BaseUrl;
        env["ConnectionStrings__Default"] = $"Data Source={_dbPath}";
        env["Jwt__SecretKey"] = "e2e-jwt-secret-key-32-chars-min!!";
        env["Vapid__PublicKey"] = "e2e-vapid-public";
        env["Vapid__PrivateKey"] = "e2e-vapid-private";
        env["Vapid__Subject"] = "mailto:e2e@demizon.test";
        env["AllowedHosts"] = "*";
        // Bez zvednutého limitu by se přihlašování ve více testech trefilo do 429.
        env["RateLimiting__AuthPermitLimit"] = "10000";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Nepodařilo se spustit hosta.");

        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.Add(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (_output) _output.Add(e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForHealthAsync();
        SeedMembers();
    }

    private static string AppDll()
    {
        // Test běží z .../Demizon.Tests.E2E/bin/<cfg>/net10.0/; host je vedle,
        // ve stejné konfiguraci. Cesta se skládá relativně, aby fungovala
        // v Debugu i Release a v CI.
        var testDir = AppContext.BaseDirectory;
        var config = new DirectoryInfo(testDir).Parent!.Name;
        var repoRoot = new DirectoryInfo(testDir).Parent!.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(repoRoot, "Demizon.Mvc", "bin", config, "net10.0", "Demizon.Mvc.dll");
        if (!System.IO.File.Exists(dll))
            throw new FileNotFoundException($"Host nenalezen: {dll}. Postav Demizon.Mvc ve konfiguraci {config}.");
        return dll;
    }

    private async Task WaitForHealthAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_process!.HasExited)
                throw new InvalidOperationException($"Host spadl při startu.{Environment.NewLine}{Output()}");

            try
            {
                var response = await http.GetAsync($"{BaseUrl}/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException) { /* ještě neposlouchá */ }
            catch (TaskCanceledException) { /* timeout requestu */ }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Host nenaběhl do 90 s.{Environment.NewLine}{Output()}");
    }

    /// <summary>
    /// Data se sázejí přímo do SQLite. Přes API by to nešlo: založit člena může
    /// jen admin a prvního admina dělá bootstrap endpoint, který po sobě zamkne.
    /// </summary>
    private void SeedMembers()
    {
        using var db = NewContext();
        if (db.Members.IgnoreQueryFilters().Any()) return;

        db.Members.AddRange(
            NewMember(AdminLogin, AdminPassword, UserRole.Admin),
            NewMember(MemberLogin, MemberPassword));
        db.SaveChanges();
    }

    private static Member NewMember(string login, string password, UserRole role = UserRole.Standard) => new()
    {
        Name = login,
        Surname = "Testovaci",
        Login = login,
        Email = $"{login}@demizon.test",
        PasswordHash = PasswordHasher.HashPassword(password),
        Role = role,
        Gender = Gender.Male,
        IsVisible = true,
        IsAttendanceVisible = true,
    };

    /// <summary>Kontext nad tou samou databází, se kterou jede běžící host.</summary>
    public DemizonContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DemizonContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new DemizonContext(options);
    }

    public string Output()
    {
        lock (_output) return string.Join(Environment.NewLine, _output.TakeLast(60));
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();

        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { System.IO.File.Delete(path); }
            catch (IOException) { /* uvolní se s temp adresářem */ }
        }
    }
}
