# Implementační plán – Demizon Web App

> Aplikace slouží jako veřejná prezentace FS Demižón ze Strážnice + interní správa souboru pro členy.
>
> **Stack:** .NET 10, Blazor Server, MudBlazor 9, SQLite, Entity Framework Core 10

---

## Stav projektu (výchozí bod)

- Funkční CRUD pro entity: Member, Dance, Event, VideoLink, File, Attendance
- Cookie-based autentizace (Admin / Standard role)
- Základní public stránky bez propracovaného designu
- Docházka – základní grid s navigací datumů

---

## Přehled fází

| # | Fáze | Priorita | Stav |
|---|------|----------|------|
| 1 | Datový model – rozšíření entit | Kritická | ✅ |
| 2 | Design redesign – mobile-first | Vysoká | ✅ |
| 3 | Admin reorganizace – role-based přístup | Vysoká | ✅ |
| 4 | Docházka – měsíční pohled, mobil UX | Vysoká | ✅ |
| 5 | Fotogalerie – lightbox | Střední | ✅ |
| 6 | JWT Auth upgrade | Střední | ✅ |
| 7 | Push notifikace | Bonus | ✅ |
| 8 | CI/CD + deployment | Odloženo | 🔲 |

---

## FÁZE 1 – Datový model ✅ HOTOVO

### Provedené změny (28.3.2026)

| Soubor | Změna |
|--------|-------|
| `Dance.cs` | `+ Region` (string), `+ Description` (string), `+ Numbers` (nav. prop.) |
| `Member.cs` | `+ IsAttendanceVisible` (bool, default `true`) |
| `VideoLink.cs` | `+ IsInternal` (bool, default `false`) |
| `DanceNumber.cs` | Nová entita – Id, Title, Description, Lyrics, DanceId |
| `DemizonContext.cs` | Registrace DanceNumbers, konfigurace relací a defaultů |
| Migrace `ExtendDataModel` | Aplikováno na SQLite |

### Původní plán – Změny entit

#### Dance (existující)
- `+ Region` (string) – region tance (Slovácko, Haná, Valašsko…)
- `+ Description` (string) – popis tance

#### Member (existující)
- `+ IsAttendanceVisible` (bool, default `true`) – zda zobrazovat člena v docházce

#### DanceNumber (nová entita)
- `Id`, `Title`, `Description`, `Lyrics` (text písničky), `DanceId` (FK)
- Relace: Dance 1→N DanceNumbers
- Účel: popis čísel repertoáru – texty písniček, popis choreografie

#### VideoLink (existující)
- `+ IsInternal` (bool, default `false`) – interní videa vidí jen přihlášení členové

### Migrace
1. `AddDanceRegionDescriptionMemberAttendanceVisible`
2. `AddDanceNumberEntity`

### Kritické soubory
- `Demizon.Dal/Entities/Dance.cs`
- `Demizon.Dal/Entities/Member.cs`
- `Demizon.Dal/Entities/DanceNumber.cs` *(nový)*
- `Demizon.Dal/Entities/VideoLink.cs`
- `Demizon.Dal/DemizonContext.cs`

---

## FÁZE 2 – Design redesign (Mobile-First) ✅ HOTOVO

### Provedené změny (28.3.2026)

| Soubor | Změna |
|--------|-------|
| `App.razor` | Nové MudBlazor téma – folklorní paletta (červená #9B1A1A, zlatá #C8912A, Georgia serif) |
| `MainLayout.razor` | Drawer výchozí zavřen, `DrawerVariant.Temporary`, logo v drawer headeru s gradientem, footer |
| `site.css` | Hero sekce, member karty, dance karty, responzivní video embed (16:9), join-us kroky |
| `Index.razor` | Hero banner + dvě info karty „O nás" + „Proč Demižón" + CTA tlačítka |
| `Members.razor` | MudGrid (xs=6, sm=4, md=3), avatar placeholder, opraveno zobrazení příjmení mužů |
| `Dances.razor` | Grid karet s MudChip pro region a popisem |
| `Events.razor` | Vylepšený timeline s kartami, ikonky místa a data |
| `Videos.razor` | Responzivní embed (aspect-ratio 16:9), filtr `!IsInternal`, limit 12 |
| `Contact.razor` | Kontaktní karty s avatarem, klikatelné tel/email linky |
| `JoinUs.razor` | Dvousloupcový layout, kroky, CTA tlačítko |
| `DanceViewModel.cs` | `+ Region`, `+ Description` |
| `VideoLinkViewModel.cs` | `+ IsInternal` |

### Původní plán – Téma
- Folklorní barevná paletta – zemité tóny, červená/modrá Slovácka
- MudThemeProvider konfigurace v `App.razor`

### Veřejné stránky
| Stránka | Plánovaný layout |
|---------|-----------------|
| Index | Hero banner + O nás + nejbližší akce + repertoár preview |
| Members | Kartičky s fotkou místo tabulky |
| Events | Card/timeline layout, filtrace minulé/budoucí |
| Dances | Grid karet s názvem, regionem, popisem |
| Photos | Skutečná galerie s lightbox efektem |
| Videos | Grid s YouTube thumbnail preview |
| Contact | Mapa + kontaktní info |
| JoinUs | Kroky jak se přidat |

### Obsah
- Texty importovat z demizon.straznice.cz (O nás, Kontakt, Přidej se)

### Kritické soubory
- `Demizon.Mvc/Layout/MainLayout.razor`
- `Demizon.Mvc/Pages/Public/*.razor`
- `Demizon.Mvc/wwwroot/css/site.css`
- `Demizon.Mvc/App.razor`

---

## FÁZE 3 – Admin reorganizace ✅ HOTOVO

### Provedené změny (28.3.2026)

| Soubor | Změna |
|--------|-------|
| `ListEvents.razor` | Role `Admin, Standard`; Create/Edit/Delete jen pro Admin přes `<AuthorizeView>` |
| `ListVideoLinks.razor` | Beze změny – zůstává Admin only; odkaz v menu skrytý pro Standard |
| `ListMembers.razor` | Přidán `[Authorize(Roles = "Admin, Standard")]`; Create skryt pro Standard; nový sloupec Docházka |
| `MemberForm.razor` | Přidán `IsAttendanceVisible` checkbox (admin only); opraven bug při update bez hesla |
| `MemberViewModel.cs` | `+ IsAttendanceVisible` |
| `ListDances.razor` | Create/Edit/Delete jen pro Admin; přidán sloupec Region s MudChip |
| `DanceForm.razor` | Přidána pole Region a Description |
| `Dance/Detail.razor` | Přepsán na záložkový layout (Základní info / Čísla tance / Videa) |
| `DanceNumberForm.razor` | Nová komponenta – dialog pro přidání/editaci čísla tance |
| `DanceNumberViewModel.cs` | Nový ViewModel |
| `IDanceNumberService.cs` | Nový service interface |
| `DanceNumberService.cs` | Nová implementace service |
| `CoreServicesRegistrationExtension.cs` | Registrace `IDanceNumberService` |
| `AdminNavMenu.razor` | Events přesunuto pro všechny role; Videos jen Admin |

### Původní plán – Role-based přístup

| Sekce | Admin | Standard |
|-------|-------|---------|
| Events | Plné CRUD | Jen výpis |
| VideoLinks | Plné CRUD | Žádný přístup |
| Members | Plné CRUD + role managment | Editace vlastního profilu |
| Dances | Plné CRUD + DanceNumbers | Žádný přístup |
| Attendance | Plné | Plné |

### Nové stránky
- `/Admin/Profile` – Standard uživatel edituje svůj profil
- `/Admin/Dances/Detail/{id}` – záložky: Základní info | Čísla tance | Videa | Soubory

### Nové komponenty
- `DanceNumberForm.razor`
- `DanceNumbers.razor`

### Kritické soubory
- `Demizon.Mvc/Pages/Admin/ListEvents.razor`
- `Demizon.Mvc/Pages/Admin/ListVideoLinks.razor`
- `Demizon.Mvc/Pages/Admin/ListMembers.razor`
- `Demizon.Mvc/Pages/Admin/Dances/Detail.razor`
- `Demizon.Mvc/Pages/Admin/Profile.razor` *(nový)*

---

## FÁZE 4 – Docházka – vylepšení ✅ HOTOVO

### Provedené změny (28.3.2026)

| Soubor | Změna |
|--------|-------|
| `MemberAttendance.razor.cs` | Měsíční navigace (`PreviousMonth`/`NextMonth`), `StartDate`/`EndDate` computed properties, `GenderFilter`, `ShowAttendanceHidden`, `FilteredFemales`/`FilteredMales`, `CountAttending()`, nová `SetAttendanceMember()` pro editaci buňky tabulky |
| `MemberAttendance.razor` | Nový layout: navigace ← MMMM yyyy →, barevné chipy akcí, MudSelect filtr pohlaví, MudCheckbox skrytí, `<table>` s CSS sticky levým sloupcem, součtové řádky Σ ženy / Σ muži / Σ celkem |
| `site.css` | `.demi-att-table-wrap` (overflow-x scroll), `.demi-att-sticky` (position: sticky), `.demi-att-cell` (min 44px touch target), `.demi-att-row-hidden` (opacity 0.45), barevné součtové řádky |

### Původní plán – Funkce
1. **Měsíční pohled** – výběr měsíce/roku místo date range
2. **Mobil UX** – velké touch targets, fixní levý sloupec při scrollu
3. **Filtr** – Ženy / Muži / Všichni, zobrazit/skrýt neaktivní
4. **IsAttendanceVisible** – skryje člena s `false` z výpisu docházky

### Kritické soubory
- `Demizon.Mvc/Pages/Admin/MemberAttendance/MemberAttendance.razor`
- `Demizon.Mvc/Pages/Admin/MemberAttendance/MemberAttendance.razor.cs`
- `Demizon.Mvc/Components/AttendanceForm.razor`

---

## FÁZE 5 – Fotogalerie ✅ HOTOVO

### Provedené změny (29.3.2026)

| Soubor | Změna |
|--------|-------|
| `Photos.razor` | Grid galerie (`auto-fill minmax(160px)`), čistý Blazor lightbox bez JS interop (MudOverlay nahrazen vlastním `<div>`), klávesová navigace (← → Escape), counter „1 / N", prázdný stav |
| `site.css` | `.demi-gallery-grid` (CSS grid, aspect-ratio 1:1, hover zoom), `.demi-lightbox` (fixed overlay, max-width 90vw/90vh), navigační šipky |

### Původní plán – Funkce
- Grid layout fotek s lazy loading
- Lightbox po kliknutí (JS interop – Fancybox.js nebo Viewer.js)
- Thumbnail generování přes ImageMagick (již v projektu – `Magick.NET`)
- Admin: upload přiřazený k tanci nebo obecný

### Kritické soubory
- `Demizon.Mvc/Pages/Public/Photos.razor`
- `Demizon.Mvc/wwwroot/js/lightbox-interop.js` *(nový)*

---

## FÁZE 6 – JWT Auth upgrade ✅ HOTOVO

### Provedené změny (28.3.2026)

> **Implementovaný přístup: Hybrid** – stávající cookie auth pro Blazor Server (SignalR circuit) zůstala nezměněna. JWT Bearer bylo přidáno jako druhé auth schéma pro budoucí REST API endpointy.

| Soubor | Změna |
|--------|-------|
| `appsettings.json` | `+ Jwt` sekce (Issuer, Audience, ExpirationMinutes) |
| `appsettings.Local.json` | `+ Jwt.SecretKey` (gitignored – tajný klíč mimo repozitář) |
| `JwtSettings.cs` | Nová konfigurační třída v `Demizon.Common` |
| `TokenService.cs` | Nová service – generování a validace JWT tokenů (HMAC-SHA256) |
| `IMyAuthenticationService.cs` | `+ IssueToken(HttpContext)` metoda |
| `MyAuthenticationService.cs` | Implementace `IssueToken` – přijímá JSON nebo form, vrací `{ token, expiresIn, role }` |
| `MvcAuthenticationServicesRegistrationExtension.cs` | Přidán `.AddJwtBearer(...)`, registrace `TokenService`, přijímá `IConfiguration` |
| `Program.cs` | Aktualizováno volání `AddAuthenticationServices(builder.Configuration)`, přidán `POST /api/auth/token` endpoint |

### Původní plán – Přechod z cookie → JWT

> **Poznámka:** Blazor Server používá SignalR (WebSockets). JWT musí být předáno jako query parametr při WS handshake nebo přes cookie-backed token store. Doporučený přístup: **hybrid** – JWT pro případné API endpointy, stávající cookie pro Blazor Server circuit.

### API endpoint
`POST /api/auth/token` – přijme `{ "login": "...", "password": "..." }` (JSON nebo form), vrátí:
```json
{ "token": "eyJ...", "expiresIn": 3600, "role": "Admin" }
```

### Kritické soubory
- `Demizon.Mvc/Services/Authentication/MyAuthenticationService.cs`
- `Demizon.Mvc/Services/Authentication/TokenService.cs` *(nový)*
- `Demizon.Mvc/Services/Authentication/MvcAuthenticationServicesRegistrationExtension.cs`
- `Demizon.Common/Configuration/JwtSettings.cs` *(nový)*
- `Demizon.Mvc/Program.cs`

---

## FÁZE 7 – Browser Push Notifikace ✅ HOTOVO

### Provedené změny (29.3.2026)

> **Implementace:** Hybridní přístup – `Append.Blazor.Notifications` (in-browser) + plná Web Push infrastruktura (VAPID, service-worker.js, `WebPush` library pro server-side odesílání).

| Soubor | Změna |
|--------|-------|
| `PushSubscription.cs` | Nová entita – Endpoint, P256dh, Auth, MemberId FK, CreatedAt |
| `Event.cs` | `+ NotifyBeforeDays` (int?, nullable) |
| `Member.cs` | `+ PushSubscriptions` navigační vlastnost |
| `DemizonContext.cs` | Registrace PushSubscriptions DbSet + modelBuilder konfigurace |
| Migrace `AddPushNotifications` | SQLite schema update |
| `VapidSettings.cs` | Konfigurační třída v Demizon.Common |
| `appsettings.json` | Vapid sekce (Subject), Jwt sekce |
| `appsettings.Local.json` | Vapid.PublicKey + Vapid.PrivateKey (gitignored) |
| `IPushSubscriptionService.cs` | Interface service |
| `PushSubscriptionService.cs` | Implementace – GetByMember, Add (deduplication), Remove, GetAll |
| `NotificationHostedService.cs` | BackgroundService – denní check v 8:00, odesílání přes WebPush library, auto-cleanup expired subscriptions |
| `Profile.razor` | Nová stránka `/Admin/Profile` – editace jména/emailu/hesla + push notifikace toggle |
| `AdminNavMenu.razor` | `+ Profil` odkaz |
| `EventViewModel.cs` | `+ NotifyBeforeDays` |
| `EventForm.razor` | `+ MudNumericField` pro NotifyBeforeDays |
| `service-worker.js` | Zpracování `push` event, `notificationclick` pro otevření URL |
| `push-notifications.js` | JS helper – subscribe/unsubscribe/getSubscription, VAPID base64url konverze |
| `_Layout.cshtml` | Načtení `push-notifications.js` |
| `Program.cs` | Registrace `VapidSettings`, `AddNotifications()`, `AddHostedService<NotificationHostedService>()` |
| `CoreServicesRegistrationExtension.cs` | Registrace `IPushSubscriptionService` |

### VAPID klíče
- Vygenerovány pomocí `WebPush.VapidHelper.GenerateVapidKeys()`
- Public key v `appsettings.Local.json` (gitignored) – **v produkci použít proměnné prostředí `Vapid__PublicKey` a `Vapid__PrivateKey`**

### Kritické soubory
- `Demizon.Dal/Entities/PushSubscription.cs` *(nový)*
- `Demizon.Mvc/Services/Notification/NotificationHostedService.cs` *(nový)*
- `Demizon.Mvc/Pages/Admin/Profile/Profile.razor` *(nový)*
- `Demizon.Mvc/wwwroot/service-worker.js` *(nový)*
- `Demizon.Mvc/wwwroot/js/push-notifications.js` *(nový)*

---

## FÁZE 8 – CI/CD + Deployment (odloženo)

> Odloženo – zatím není vybrána hostingová platforma.

### Plánované kroky (až bude server)
1. Opravit `Dockerfile` – změnit .NET 8.0 → .NET 10.0 image
2. GitHub Actions workflow – build, test, Docker push, deploy
3. SQLite záloha – bash skript s rotací posledních 7 dní

---

## Mobilní testování (bez serveru)

- **Chrome DevTools** – F12 → Toggle Device Toolbar (Ctrl+Shift+M)
- **dotnet dev-certs** – `dotnet dev-certs https --trust` pro lokální HTTPS
- **Ngrok** – dočasný HTTPS tunel na reálné zařízení (volitelné)

---

## Insights a architektonické poznámky

Technické poznatky nasbírané při implementaci – věci, které nejsou zřejmé z kódu na první pohled.

### EF Core – `defaultValue` vs C# inicializátor vlastnosti
EF Core při generování migrace ignoruje C# inicializátor (`= true`) a použije výchozí hodnotu pro daný .NET typ (`bool` → `false`). Pokud chceš skutečný `DEFAULT TRUE` v SQLite, musíš:
1. V `OnModelCreating` nastavit `.HasDefaultValue(true)`
2. Ručně opravit vygenerovanou migraci: změnit `defaultValue: false` → `defaultValue: true` **v obou souborech** (`Migration.cs` i `Migration.Designer.cs`)

### MudBlazor 9 – DrawerVariant.Temporary
Pro mobile-first přístup je správná volba `DrawerVariant.Temporary` (overlay drawer, zavírá se po kliknutí na odkaz). `DrawerVariant.Responsive` ponechá drawer otevřený na desktopu, což není vhodné pro tento design. Výchozí stav `DrawerOpen = false` zajistí, že mobilní uživatelé neuvidí drawer při načtení.

### MudBlazor 9 – Title vs title
V MudBlazor 9 je `title` HTML atribut, nikoliv Blazor parametr. Proto `title="text"` (lowercase) – jinak kompilátor generuje MUD0002 warning.

### CSS – Responzivní 16:9 video embed bez JS
Technika `padding-bottom: 56.25%` (= 9/16 × 100%) s `height: 0` a absolutně pozicovaným `<iframe>` zajistí správný poměr stran na jakémkoliv zařízení. Alternativou v moderních prohlížečích je `aspect-ratio: 16/9`, ale padding-bottom trick funguje ve všech.

### CSS – Sticky levý sloupec tabulky
`position: sticky; left: 0` funguje, ale sticky element musí mít **explicitní `background` barvu** – jinak obsah tabulky "prosvítá" přes přilepený sloupec při horizontálním scrollu. Každý stav řádku (normální, hidden, sum, total) potřebuje vlastní `background` na `.demi-att-sticky`.

### Blazor – Lightbox bez JS Interop
Čistý Blazor lightbox je možný pomocí:
- `<div @onkeydown="OnKeyDown" tabindex="0" @ref="_lightboxRef">` pro zachycení klávesových událostí
- `await _lightboxRef.FocusAsync()` po otevření – overlay musí být focusovaný, aby přijímal keydown
- `@onclick:stopPropagation="true"` na vnitřním obsahu zabraňuje zavření při kliknutí na fotku

### AutoMapper – Hesla a podmíněné mapování
Při mapování `MemberViewModel → Member` (update existujícího záznamu) nesmí automaticky dojít k přepsání hashe hesla. Správné řešení v profilu:
```csharp
.ForMember(x => x.PasswordHash,
    opt => opt.MapFrom(y => string.IsNullOrWhiteSpace(y.Password)
        ? y.PasswordHash
        : Crypto.HashPassword(y.Password)));
```
`MemberViewModel.PasswordHash` je při editaci naplněn z DB, `Password` je prázdné – takže hash zůstane. Pokud uživatel zadá nové heslo, přepíše se.

### EF Core – SetValues() nekopíruje navigační vlastnosti
`context.Entry(entity).SetValues(source)` kopíruje **pouze skalární vlastnosti** (primitivní typy). Navigační vlastnosti (kolekce, reference) zůstávají nezměněny. Proto se `DanceNumber` spravuje přes vlastní `DanceNumberService` s přímými CRUD operacemi, nikoliv přes `Dance.UpdateAsync`.

### Blazor – AuthorizeView vs @if pro role-based UI
`<AuthorizeView Roles="Admin">` je správná cesta pro podmíněné zobrazení UI elementů. Alternativa `@if (role == "Admin")` vyžaduje ruční přístup k `AuthenticationState` a je náchylná na chyby. `AuthorizeView` navíc funguje reaktivně – přerenduje se při změně auth stavu.

### Docházka – Deduplikace pátků s akcemi
Při generování sloupců tabulky docházky (pátky + akce) může dojít k duplicitě, pokud akce připadne na pátek. Řešení: filtrovat pátky pomocí `.Where(date => !events.Any(e => e.DateFrom.Date == date.Date))` – pátek se vynechá a nahradí ho akce.

### Blazor – Conflict mezi názvem komponenty a vlastností
Třída komponenty `Photos` nemůže mít vlastnost `Photos` (CS0542: member name same as its enclosing type). Pojmenuj seznam položek jinak – např. `GalleryItems`.

### Web Push – VAPID klíče a bezpečnost
VAPID (Voluntary Application Server Identification) používá asymetrické klíče P-256 ECDH. Public key se posílá do prohlížeče při subscripci (URL base64 formát). Private key zůstává na serveru a slouží k podpisu push requestů. Klíče jsou specifické pro doménu aplikace – při změně domény nebo klíčů se musí všichni uživatelé znovu přihlásit k odběru. Nikdy necommitovat private key do gitu – použít `appsettings.Local.json` (gitignored) nebo proměnné prostředí `Vapid__PrivateKey`.

### Web Push – Expired subscriptions (HTTP 410 Gone)
Prohlížeč může subscription zneplatnit (uživatel odebral povolení, reinstalace prohlížeče). Push server vrátí `410 Gone` nebo `404 Not Found`. Je potřeba tyto subscriptions automaticky odstraňovat z DB. `NotificationHostedService` to řeší catch blokem pro `WebPushException` s těmito status kódy.

### BackgroundService – GetDelay pattern
`BackgroundService.ExecuteAsync` běží po celou dobu životnosti aplikace. Namísto `Task.Delay(TimeSpan.FromHours(24))` je lepší vypočítat zpoždění do konkrétního času (`GetDelayUntilNextCheck`) – zajistí konzistentní čas spuštění bez driftu.

### SQLite + EF Core Lazy Loading
Projekt používá `UseLazyLoadingProxies()`. To vyžaduje, aby všechny navigační vlastnosti byly `virtual`. EF Core pak generuje proxy třídy, které načítají data při prvním přístupu. Výhoda: jednodušší kód. Nevýhoda: riziko N+1 problémů u větších dotazů – při výpisu seznamu entit vždy zvažuj `Include()` nebo projekci do ViewModel.
