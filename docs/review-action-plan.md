# Akční plán – Opravy a vylepšení po code review

> Vytvořeno: 10.4.2026 na základě code review commitu `6f190e5` (60 souborů, ~3800 řádků).
> Review provedeno ve 4 paralelních auditech: backend, security, frontend, architektura.

---

## Přehled fází

| # | Fáze | Priorita | Stav | Rozsah |
|---|------|----------|------|--------|
| R1 | Kritické opravy – blokující produkci | Kritická | ✅ `afd5995` | ~8 úprav |
| R2 | Bezpečnostní hardening | Vysoká | ✅ `3a25475` | ~6 úprav |
| R3 | Datová vrstva – integrita a výkon | Vysoká | ✅ `1fb2bfd` | ~7 úprav |
| R4 | Frontend – kvalita a UX | Střední | ✅ `a0f0897` | ~9 úprav |
| R5 | Architektura – čistota kódu | Střední | ✅ `f844b60` | ~6 úprav |
| R6 | Lokalizace – CZ/EN public stránky | Střední | ✅ `22ca424` | ~4 úprav |
| R7 | Nové funkce | Nízká | ✅ `d60a4d6` | ~8 návrhů |

---

## FÁZE R1 – Kritické opravy ✅

> Tyto problémy brání správnému buildu nebo mohou způsobit runtime chyby.

### R1.1 Dockerfile – .NET 8 → .NET 10

**Problém:** `Dockerfile` používá `aspnet:8.0` a `sdk:8.0`, ale `.csproj` cílí na `net10.0`. Docker build selže.

**Soubory:**
- `Demizon.Mvc/Dockerfile`

**Úpravy:**
1. Změnit `FROM mcr.microsoft.com/dotnet/aspnet:8.0` → `aspnet:10.0` (řádky 1, 9)
2. Změnit `ENV ASPNETCORE_ENVIRONMENT Development` → `Production` (řádek 7)
3. Přidat `HEALTHCHECK` instrukci
4. Zvážit non-root user (`USER app` – .NET 10 images mají uživatele `app` předvytvořeného)

---

### R1.2 MemberViewModel.Birthdate – naming mismatch

**Problém:** Property `Birthdate` v `MemberViewModel` vs `BirthDate` v entitě `Member`. AutoMapper `ReverseMap()` nenamapuje property s odlišným case → datum narození se neukládá.

**Soubory:**
- `Demizon.Mvc/ViewModels/MemberViewModel.cs`

**Úpravy:**
1. Přejmenovat `Birthdate` → `BirthDate` ve ViewModelu (aby odpovídalo entitě)

---

### R1.3 Profile.razor – synchronní přístup k AuthenticationState

**Problém:** V `OnAfterRenderAsync()` se přistupuje k `AuthenticationState?.Result.User.Claims` – to je synchronní blokující přístup k `Task<AuthenticationState>`. Může způsobit deadlock.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Profile/Profile.razor`

**Úpravy:**
1. Nahradit `AuthenticationState?.Result` za `await AuthenticationState!`
2. Přesunout načítání subscriptions do `OnInitializedAsync()` místo `OnAfterRenderAsync()`

---

### R1.4 MemberAttendance.razor.cs – pořadí inicializace

**Problém:** `OnInitialized()` (synchronní) přistupuje k `AuthenticationState` dříve než `OnInitializedAsync()`. Potenciální `NullReferenceException`.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor.cs`

**Úpravy:**
1. Přesunout inicializaci `LoggedUser` do `OnInitializedAsync()`
2. Zajistit, že `LoadData()` se volá až po inicializaci `LoggedUser`

---

### R1.5 NotificationHostedService – chybí global try-catch

**Problém:** `ExecuteAsync` nemá outer try-catch. Neočekávaná výjimka ukončí celý BackgroundService bez možnosti recovery.

**Soubory:**
- `Demizon.Mvc/Services/Notification/NotificationHostedService.cs`

**Úpravy:**
1. Obalit `while` loop try-catch blokem s logováním
2. Zachytit `OperationCanceledException` separátně (graceful shutdown)

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    logger.LogInformation("NotificationHostedService spuštěna.");
    try
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // ...existující kód...
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* ok */ }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "NotificationHostedService selhala s nepředvídanou chybou.");
        throw;
    }
}
```

---

### R1.6 push-notifications.js – chybí try-catch

**Problém:** Funkce `subscribe()` nemá error handling. Selhání registrace service workeru vyhodí unhandled promise rejection do Blazor JS interop.

**Soubory:**
- `Demizon.Mvc/wwwroot/js/push-notifications.js`

**Úpravy:**
1. Obalit tělo `subscribe()` try-catch blokem
2. Vrátit `null` při chybě (konzistentní s existujícím early-return pattern)
3. Logovat chybu do `console.error`

---

### R1.7 DanceNumberViewModel – chybí validační atributy

**Problém:** ViewModel nemá `[Required]`, `[StringLength]` atributy. Formulář může odeslat nevalidovaná data.

**Soubory:**
- `Demizon.Mvc/ViewModels/DanceNumberViewModel.cs`

**Úpravy:**
1. Přidat `[Required]` na `Title`
2. Přidat `[StringLength(200)]` na `Title`
3. Přidat `[StringLength(5000)]` na `Description` a `Lyrics`
4. Přidat `MaxLength` do `DanceNumberForm.razor` inputů

---

### R1.8 DefaultConnectionString – odstranit static class

**Problém:** `DefaultConnectionString` je mutable static state. Connection string se předává do `AddDatabase()`, ale přes statickou třídu → skrytá dependence, ztěžuje testování.

**Soubory:**
- `Demizon.Common/Configuration/DefaultConnectionString.cs`
- `Demizon.Mvc/Program.cs`
- `Demizon.Dal/Extensions/DalServicesRegistrationExtension.cs`

**Úpravy:**
1. Předat connection string přímo jako parametr do `AddDatabase(connectionString)`
2. Odstranit statickou třídu `DefaultConnectionString`

---

## FÁZE R2 – Bezpečnostní hardening ✅

> Ochrana před útoky a best practices pro produkční nasazení.

### R2.1 Rate limiting na auth endpointy

**Problém:** `/api/auth/token` a `/ProcessLogin` nemají rate limiting. Umožňuje brute-force útok na hesla.

**Soubory:**
- `Demizon.Mvc/Program.cs`

**Úpravy:**
1. Přidat `Microsoft.AspNetCore.RateLimiting` middleware
2. Nastavit `FixedWindowLimiter` na auth endpointy (5 pokusů / minuta)
3. Vrátit `429 Too Many Requests` při překročení

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});
// ...
app.UseRateLimiter();
app.MapPost("/api/auth/token", ...).RequireRateLimiting("auth");
app.MapPost("/ProcessLogin", ...).RequireRateLimiting("auth");
```

---

### R2.2 Timing attack na login – dummy hash

**Problém:** Když uživatel neexistuje, `Crypto.VerifyHashedPassword(null, ...)` se vrátí mnohem rychleji než s reálným hashem. Útočník může měřením response time zjistit existenci uživatele.

**Soubory:**
- `Demizon.Mvc/Services/Authentication/MyAuthenticationService.cs`

**Úpravy:**
1. Přidat konstantní dummy hash pro neexistující uživatele
2. Vždy provést hash verifikaci bez ohledu na existenci uživatele

```csharp
private static readonly string DummyHash = Crypto.HashPassword("dummy-timing-defense");

// V Login() i IssueToken():
var userAccount = MemberService.GetOneByLogin(login ?? string.Empty);
var isPasswordCorrect = Crypto.VerifyHashedPassword(
    userAccount?.PasswordHash ?? DummyHash, password ?? string.Empty);
```

---

### R2.3 Service Worker – URL validace

**Problém:** `notificationclick` handler otevírá `data.url` bez validace. Potenciální XSS přes `javascript:` URL.

**Soubory:**
- `Demizon.Mvc/wwwroot/service-worker.js`

**Úpravy:**
1. Validovat URL – povolit jen relativní cesty začínající `/`
2. Fallback na `/` pro nevalidní URL

```javascript
const raw = event.notification.data?.url || '/';
const url = (typeof raw === 'string' && raw.startsWith('/')) ? raw : '/';
event.waitUntil(clients.openWindow(url));
```

---

### R2.4 AllowedHosts – specifikovat domény

**Problém:** `"AllowedHosts": "*"` v `appsettings.json` deaktivuje Host header check → riziko host-header injection.

**Soubory:**
- `Demizon.Mvc/appsettings.json`

**Úpravy:**
1. Změnit `"AllowedHosts": "*"` na `"AllowedHosts": "localhost;demizon.cz;www.demizon.cz"` (nebo přes env proměnnou)
2. V development nechat `*` přes `appsettings.Development.json`

---

### R2.5 IOptions validace při startu

**Problém:** `JwtSettings` a `VapidSettings` nejsou validovány při startu aplikace. Chybějící SecretKey způsobí runtime crash až při prvním pokusu o login.

**Soubory:**
- `Demizon.Mvc/Program.cs`
- `Demizon.Common/Configuration/JwtSettings.cs`
- `Demizon.Common/Configuration/VapidSettings.cs`

**Úpravy:**
1. Přidat `[Required]` atributy na povinné properties v konfiguračních třídách
2. Přidat `.ValidateDataAnnotations().ValidateOnStart()` v Program.cs

```csharp
// JwtSettings.cs
public class JwtSettings
{
    [Required, MinLength(32)]
    public string SecretKey { get; set; } = null!;
    [Required]
    public string Issuer { get; set; } = null!;
    [Required]
    public string Audience { get; set; } = null!;
    public int ExpirationMinutes { get; set; } = 60;
}

// Program.cs
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

### R2.6 appsettings.Local.json.example

**Problém:** Nový vývojář neví, jaké klíče potřebuje nastavit v `appsettings.Local.json`.

**Soubory:**
- `Demizon.Mvc/appsettings.Local.json.example` (nový)

**Úpravy:**
1. Vytvořit `.example` soubor s placeholdery (bez skutečných klíčů)
2. Zdokumentovat v README

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=demizon.sqlite"
  },
  "Jwt": {
    "SecretKey": "<min-32-znaku-tajny-klic>"
  },
  "Vapid": {
    "PublicKey": "<vapid-public-key>",
    "PrivateKey": "<vapid-private-key>"
  }
}
```

---

## FÁZE R3 – Datová vrstva – integrita a výkon ✅

> Opravy v EF Core konfiguraci, migracích a service vrstvě.

### R3.1 Unique constraint na PushSubscription (MemberId, Endpoint)

**Problém:** Race condition v `PushSubscriptionService.AddAsync()` – check-then-act bez DB constraintu.

**Soubory:**
- `Demizon.Dal/DemizonContext.cs`
- Nová migrace

**Úpravy:**
1. Přidat v `OnModelCreating`:
   ```csharp
   b.HasIndex(x => new { x.MemberId, x.Endpoint }).IsUnique();
   ```
2. Vytvořit migraci `AddPushSubscriptionUniqueIndex`
3. V `PushSubscriptionService.AddAsync()` přidat try-catch na `DbUpdateException`

---

### R3.2 Cascade delete pro Files a VideoLinks u Dance

**Problém:** Smazání Dance zanechá osiřelé Files a VideoLinks v DB.

**Soubory:**
- `Demizon.Dal/DemizonContext.cs`
- Nová migrace

**Úpravy:**
1. Přidat `.OnDelete(DeleteBehavior.Cascade)` na oba vztahy:
   ```csharp
   b.HasMany<File>(x => x.Files)
       .WithOne(y => y.Dance)
       .HasForeignKey(x => x.DanceId)
       .OnDelete(DeleteBehavior.Cascade);
   
   b.HasMany<VideoLink>(x => x.Videos)
       .WithOne(y => y.Dance)
       .HasForeignKey(x => x.DanceId)
       .OnDelete(DeleteBehavior.Cascade);
   ```
2. Vytvořit migraci

---

### R3.3 HasMaxLength pro PushSubscription stringy

**Problém:** Endpoint, P256dh, Auth nemají délkové omezení → potenciální fat rows / DoS.

**Soubory:**
- `Demizon.Dal/DemizonContext.cs`
- Nová migrace

**Úpravy:**
1. Přidat v `OnModelCreating`:
   ```csharp
   b.Property(x => x.Endpoint).HasMaxLength(500).IsRequired();
   b.Property(x => x.P256dh).HasMaxLength(128).IsRequired();
   b.Property(x => x.Auth).HasMaxLength(48).IsRequired();
   ```

---

### R3.4 Indexy na Files.DanceId a VideoLinks.DanceId

**Problém:** Dotazy přes `DanceId` nemají index → full table scan.

**Soubory:**
- `Demizon.Dal/DemizonContext.cs`
- Nová migrace

**Úpravy:**
1. Přidat indexy:
   ```csharp
   modelBuilder.Entity<File>(b =>
   {
       b.HasIndex(x => x.DanceId);
   });
   modelBuilder.Entity<VideoLink>(b =>
   {
       b.HasIndex(x => x.DanceId);
   });
   ```

> **Poznámka:** R3.1, R3.2, R3.3, R3.4 lze sloučit do jedné migrace `ImproveDataIntegrity`.

---

### R3.5 DanceNumberService.UpdateAsync – optimistic concurrency

**Problém:** `SetValues()` přepíše hodnoty bez detekce souběžných změn (lost update problem).

**Soubory:**
- `Demizon.Dal/Entities/DanceNumber.cs`
- `Demizon.Core/Services/DanceNumber/DanceNumberService.cs`

**Úpravy (volitelné):**
1. Přidat `[Timestamp] public byte[] RowVersion { get; set; }` do entity
2. V `OnModelCreating`: `.Property(x => x.RowVersion).IsRowVersion()`
3. Zachytit `DbUpdateConcurrencyException` v service
4. **Alternativa (jednodušší):** Toto je low-traffic admin operace – ponechat a řešit až při problémech

---

### R3.6 Services – error handling (swallowed exceptions)

**Problém:** Některé service metody (EventService, MemberService) vrací `bool` a v `catch` vrátí `false` bez logování. Chyby se tiše ztrácí.

**Soubory:**
- `Demizon.Core/Services/Event/EventService.cs`
- `Demizon.Core/Services/Member/MemberService.cs`
- (případně další services)

**Úpravy:**
1. Přidat `ILogger` do services
2. Logovat výjimky v catch blocích
3. Zvážit přechod na Result pattern (viz R7.1)

---

### R3.7 NotificationHostedService – N+1 optimalizace

**Problém:** `eventService.GetAll().ToList()` materializuje všechny eventy do paměti, pak filtruje v C#.

**Soubory:**
- `Demizon.Mvc/Services/Notification/NotificationHostedService.cs`

**Úpravy:**
1. Optimalizovat dotaz – přenést co nejvíc logiky do SQL:
   ```csharp
   var upcomingEvents = await eventService.GetAll()
       .Where(e => e.NotifyBeforeDays.HasValue && e.DateFrom > today)
       .ToListAsync();
   // Pak filtrovat v C# jen finální podmínku (DateFrom.Date == ...)
   ```
2. Přidat `AsNoTracking()` pro read-only dotazy

---

## FÁZE R4 – Frontend – kvalita a UX ✅

> Vylepšení UI kódu, přístupnosti a CSS.

### R4.1 CSS custom properties místo hardcoded barev

**Problém:** Barvy (#9B1A1A, #C8912A atd.) hardcoded na 30+ místech.

**Soubory:**
- `Demizon.Mvc/wwwroot/css/site.css`

**Úpravy:**
1. Definovat `:root` proměnné:
   ```css
   :root {
       --demi-primary: #9B1A1A;
       --demi-primary-dark: #6B0000;
       --demi-primary-light: #C84B4B;
       --demi-accent: #C8912A;
       --demi-accent-light: #E8B94A;
       --demi-bg-warm: #f5f0eb;
       --demi-shadow: rgba(0, 0, 0, 0.08);
   }
   ```
2. Nahradit všechny hardcoded hodnoty `var(--demi-*)` referencemi
3. Bonus: připravit dark mode variantu v `@media (prefers-color-scheme: dark)` bloku

---

### R4.2 MainLayout – aria-label na menu tlačítku

**Problém:** `MudIconButton` pro otevření draweru nemá `aria-label` → nepřístupné pro screen readery.

**Soubory:**
- `Demizon.Mvc/Shared/MainLayout.razor`

**Úpravy:**
1. Přidat `aria-label="Otevřít navigační menu"` nebo `Title="Menu"` na MudIconButton

---

### R4.3 Sjednocení formátování datumů

**Problém:** Datumy jsou formátovány nekonzistentně – některé `d.M.`, jiné `d.M.yyyy`, žádné nezobrazují rok při přechodu přes rok.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor`
- (případně další Razor stránky)

**Úpravy:**
1. Vytvořit extension metodu `DateRange.Format()` nebo helper
2. Použít konzistentně na všech místech

```csharp
public static string FormatRange(this (DateTime From, DateTime To) range)
{
    if (range.From.Date == range.To.Date)
        return range.From.ToString("d. M. yyyy");
    if (range.From.Year == range.To.Year)
        return $"{range.From:d. M.}–{range.To:d. M. yyyy}";
    return $"{range.From:d. M. yyyy}–{range.To:d. M. yyyy}";
}
```

---

### R4.4 MemberAttendance – deduplikace SetAttendance logiky

**Problém:** `SetAttendance()` a `SetAttendanceMember()` obsahují téměř shodný try-catch-refresh kód.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor.cs`

**Úpravy:**
1. Extrahovat společnou logiku do privátní metody `SaveAttendanceAsync()`

---

### R4.5 App.razor – duplicitní font definice

**Problém:** Font `Georgia, serif` se nastavuje v MudTheme Typography I v site.css na `html, body`. Dvojitá definice.

**Soubory:**
- `Demizon.Mvc/App.razor`
- `Demizon.Mvc/wwwroot/css/site.css`

**Úpravy:**
1. Odebrat font definici z `site.css` (nechat jen v MudTheme – to je autoritativní zdroj)

---

### R4.6 App.razor – nekompletní PaletteDark

**Problém:** `PaletteDark` má jen 2 barvy z ~15 potřebných. Dark mode by vypadal rozbitě.

**Soubory:**
- `Demizon.Mvc/App.razor`

**Úpravy:**
1. Buď kompletně doplnit PaletteDark (Surface, Background, TextPrimary, DrawerBackground, ...)
2. Nebo odebrat PaletteDark úplně, dokud nebude dark mode prioritou
3. Přidat toggle dark/light do UI (MudSwitch v draweru)

---

### R4.7 Responsive design – tabulka docházky na mobilu

**Problém:** Jen jeden media query `@media (max-width: 600px)` pro hero sekci. Tabulka docházky se jen scrolluje.

**Soubory:**
- `Demizon.Mvc/wwwroot/css/site.css`

**Úpravy:**
1. Přidat responsive breakpointy pro attendance tabulku:
   ```css
   @media (max-width: 768px) {
       .demi-att-table { font-size: 0.75rem; }
       .demi-att-sticky { min-width: 100px; }
   }
   ```

---

### R4.8 Detail.razor – redundantní video UI

**Problém:** Stránka Dance Detail zobrazuje videa v embedded formátu, ale říká "spravujte v sekci Video Links". Matoucí pro uživatele.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Dance/Detail.razor`

**Úpravy:**
1. Buď přidat inline CRUD pro videa přímo na Dance Detail
2. Nebo zobrazit videa jen jako read-only seznam s odkazem na Admin/Videos

---

### R4.9 AdminNavMenu – hardcoded "Profil"

**Problém:** Text "Profil" není lokalizovaný na rozdíl od ostatních položek menu.

**Soubory:**
- `Demizon.Mvc/Shared/AdminNavMenu.razor`

**Úpravy:**
1. Protože admin stránky zůstanou jen v češtině (viz R6), stačí nechat hardcoded
2. Ale pro konzistenci použít `@Localizer` pattern jako ostatní položky

---

## FÁZE R5 – Architektura – čistota kódu ✅

> Vylepšení patterns, DI a struktury projektu.

### R5.1 PageService – Scoped místo Transient

**Problém:** `PageService` nastavuje titulek stránky – měl by žít po dobu Blazor circuitu (Scoped), ne být Transient.

**Soubory:**
- `Demizon.Mvc/Services/Extensions/MvcServicesRegistrationExtension.cs`

**Úpravy:**
1. Změnit `AddTransient<PageService>()` → `AddScoped<PageService>()`

---

### R5.2 AutoMapper registrace – přesunout do Core

**Problém:** AutoMapper je registrován v MVC vrstvě, ale ViewModely se používají i jinde.

**Soubory:**
- `Demizon.Mvc/Services/Extensions/MvcServicesRegistrationExtension.cs`
- `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs`

**Úpravy:**
1. Přesunout `cfg.AddMaps(...)` do `AddCoreServices()` nebo vytvořit separátní extension

---

### R5.3 IMyAuthenticationService – přejmenovat interface

**Problém:** Prefix `My` v `IMyAuthenticationService` není konvenční. Interface by měl popisovat roli, ne vlastnictví.

**Soubory:**
- `Demizon.Mvc/Services/Authentication/IMyAuthenticationService.cs`
- `Demizon.Mvc/Services/Authentication/MyAuthenticationService.cs`
- `Demizon.Mvc/Program.cs`

**Úpravy:**
1. Přejmenovat na `IAuthenticationService` / `AuthenticationService`
2. Nebo `IAccountService` / `AccountService` (pokud bude rozšířen o profil management)

---

### R5.4 TokenService.ValidateToken() – mrtvý kód?

**Problém:** Metoda `ValidateToken()` existuje ale nikde se nepoužívá.

**Soubory:**
- `Demizon.Mvc/Services/Authentication/TokenService.cs`

**Úpravy:**
1. Ověřit, zda je plánované využití (např. v budoucím API middleware)
2. Pokud ne – odebrat nebo přidat `// TODO: bude využito v API middleware`

---

### R5.5 appsettings.Production.json

**Problém:** Neexistuje produkční konfigurace. HSTS default je 30 dní (doporučení: 2 roky).

**Soubory:**
- `Demizon.Mvc/appsettings.Production.json` (nový)

**Úpravy:**
1. Vytvořit soubor s produkčním nastavením:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Warning",
         "Microsoft.AspNetCore": "Warning"
       }
     }
   }
   ```

---

### R5.6 Global exception handler

**Problém:** Neošetřené výjimky v endpointech vrátí generickou chybu bez logování.

**Soubory:**
- `Demizon.Mvc/Program.cs`

**Úpravy:**
1. Přidat `app.UseExceptionHandler()` s custom handlerem pro API endpointy
2. Logovat všechny neošetřené výjimky

---

## FÁZE R6 – Lokalizace ✅

> Strategie: čeština/angličtina jen pro **veřejné stránky**. Admin sekce zůstane výhradně v češtině.

### R6.1 Lokalizační strategie

**Princip:**
- **Public stránky** (`/`, `/Members`, `/Events`, `/Dances`, `/Photos`, `/Videos`, `/Contact`, `/JoinUs`): CZ + EN
- **Admin stránky** (`/Admin/*`): pouze CZ – není potřeba překlad
- **Chybové zprávy (Snackbar)**: CZ pro admin, lokalizované pro public
- Přepínač jazyka existující (`/SetLanguage/{culture}`) – zobrazit jen na public stránkách

### R6.2 Vyčistit existující lokalizaci

**Problém:** Aktuální stav je nekonzistentní – mix anglických a českých hardcoded textů na admin stránkách.

**Soubory:**
- `Demizon.Mvc/Pages/Admin/Dance/ListDances.razor` – anglické Snackbar zprávy
- `Demizon.Mvc/Pages/Admin/Event/ListEvents.razor` – anglické zprávy
- `Demizon.Mvc/Pages/Admin/Member/ListMembers.razor` – anglické zprávy

**Úpravy:**
1. Nahradit všechny anglické zprávy v admin sekcích českými (hardcoded, bez Localizer)
2. Odstranit `@Localizer` volání z admin stránek (zbytečná režie)

### R6.3 Doplnit lokalizaci public stránek

**Soubory:**
- `Demizon.Mvc/Pages/Public/*.razor`
- `Demizon.Mvc/Resources/` (resource soubory)

**Úpravy:**
1. Vytvořit `.resx` soubory pro CZ a EN (nebo rozšířit stávající `DemizonLocales`)
2. Zajistit, aby všechny texty na public stránkách procházely přes `@Localizer[...]`
3. Přeložit obsah: O nás, Kontakt, Přidej se, popisy sekcí

### R6.4 Přepínač jazyka – jen na public stránkách

**Soubory:**
- `Demizon.Mvc/Shared/MainLayout.razor`

**Úpravy:**
1. Zobrazit přepínač CZ/EN jen když URL není `/Admin/*`
2. Zvážit uložení preference do cookie (stávající implementace to dělá přes `CookieRequestCultureProvider`)

---

## FÁZE R7 – Nové funkce ✅ `d60a4d6`

> Implementováno. Code review provedeno, kritické nálezy opraveny před commitem.

### R7.1 Result\<T\> pattern ✅

**Implementováno:** `Demizon.Common/Result.cs` – sealed `Result<T>` a `Result` třídy s `Ok()`/`Fail()` factory metodami. Připraveno pro postupný refaktoring services.

---

### R7.2 Refresh token pro JWT ✅

**Implementováno:**
- `RefreshToken` entita s hashovaným tokenem (CryptoHelper BCrypt)
- `RefreshTokenService` – atomická rotace tokenů (transakce), validace v paměti (nutné kvůli BCrypt)
- Endpoint `POST /api/auth/refresh` s rate limitingem
- `POST /api/auth/token` nyní vrací i `refreshToken` v odpovědi
- `JwtSettings.RefreshTokenExpirationDays` (výchozí 30 dní)

---

### R7.3 Soft delete pro členy ✅

**Implementováno:**
- `Member.DeletedAt` (nullable DateTime)
- Global query filter `b.HasQueryFilter(m => m.DeletedAt == null)`
- `MemberService.DeleteAsync()` nastavuje `DeletedAt = DateTime.UtcNow` místo `Remove()`
- Historická docházka zůstane zachována

---

### R7.4 Audit log ✅

**Implementováno:**
- `AuditLog` entita (EntityType, EntityId, Action, UserId, Timestamp, OldValues, NewValues JSON)
- `AuditSaveChangesInterceptor` – Scoped SaveChangesInterceptor, vylučuje citlivá pole (PasswordHash, TokenHash)
- `ICurrentUserAccessor` / `CurrentUserAccessor` – přístup k přihlášenému uživateli bez závislosti na ASP.NET Core v DAL vrstvě
- Přechod `AddDbContextPool` → `AddDbContext` (nutné pro Scoped interceptor)

---

### R7.5 Statistiky docházky ✅

**Implementováno:**
- `IAttendanceReportService` / `AttendanceReportService` – agreguje docházku per člen, vrací `MemberAttendanceStat` záznamy
- `/Admin/AttendanceStats` – tabulka s MudProgressLinear vizualizací (% docházky), filtr dle období
- Odkaz v `AdminNavMenu` (ikona BarChart)

---

### R7.6 Opakující se události (recurrence) ✅

**Implementováno:**
- `RecurrenceType` enum (`None`, `Weekly`, `Monthly`) přidán do `Event` entity
- `Event.RecurrenceEndDate` (nullable DateTime)
- EF Core konfigurace s `HasConversion<string>()`
- UI zatím ukazuje pole, logika generování instancí není implementována (komplexní scope)

---

### R7.7 Health check endpoint ✅

**Implementováno:**
- `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` NuGet
- `builder.Services.AddHealthChecks().AddDbContextCheck<DemizonContext>("database")`
- `app.MapHealthChecks("/health")` s JSON response (status + jednotlivé checks)

---

### R7.8 API dokumentace ✅ (částečná)

**Implementováno:** Endpoint `GET /api/endpoints` (development only) vrací JSON seznam API endpointů.

**Poznámka:** `Swashbuckle.AspNetCore 10.x` a `Microsoft.AspNetCore.OpenApi` jsou inkompatibilní s nastavením `UseRazorSourceGenerator=false` (source generator konflikt). Plnohodnotný Swagger UI vyžaduje buď odebrání tohoto nastavení, nebo downgrade na Swashbuckle 6.x – ponecháno pro budoucí sprint.

---

## Doporučené pořadí implementace

```
R1 (kritické opravy)           ← Týden 1
  └→ R1.1 Dockerfile
  └→ R1.2 Birthdate naming
  └→ R1.3 Profile async
  └→ R1.4 Attendance init
  └→ R1.5 NotificationHostedService try-catch
  └→ R1.6 push-notifications.js try-catch
  └→ R1.7 DanceNumber validace
  └→ R1.8 DefaultConnectionString removal

R2 (security)                   ← Týden 1-2
  └→ R2.1 Rate limiting
  └→ R2.2 Timing attack fix
  └→ R2.3 SW URL validace
  └→ R2.4 AllowedHosts
  └→ R2.5 IOptions validace
  └→ R2.6 .example soubor

R3 (data integrita) – 1 migrace ← Týden 2
  └→ R3.1 Unique constraint
  └→ R3.2 Cascade delete
  └→ R3.3 MaxLength
  └→ R3.4 Indexy
  └→ R3.6 Error handling v services

R4 + R5 (frontend + architektura) ← Týden 3
R6 (lokalizace)                    ← Týden 3-4
R7 (nové funkce)                   ← Průběžně dle potřeby
```

---

## Poznámky

- R3.1–R3.4 **sloučit do jedné migrace** `ImproveDataIntegrity` – méně migrací = méně problémů
- R3.5 (optimistic concurrency) je **volitelné** – pro low-traffic admin app nemá velký praktický dopad
- R5.3 (přejmenování interface) je **kosmetické** – dělat jen pokud se v blízkosti bude refaktorovat auth
- R7.* jsou **nezávislé** – implementovat v libovolném pořadí dle priority
