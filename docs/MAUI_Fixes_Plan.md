# Plán oprav Demizon.Maui aplikace

## Problematika

Uživatel hlásí čtyři hlavní problémy v mobilní MAUI aplikaci:

1. **Logo se nezobrazuje na login stránce** — zobrazuje se pouze bílá stránka (zejména na mobilu s dark theme)
2. **Aplikace padá při kliknutí na detaily** — kliknutí na akci, tanec nebo docházku způsobuje crash
3. **Dark theme se nerespektuje** — App.xaml.cs příkazuje Light theme, ignorující systémové nastavení
4. **Chybí editace cizí docházky** — na přehledové stránce (AllMembersAttendance) uživatel není schopen editovat docházku jiného člena (ideálně s viditelností data, osoby a stavu účasti)

Postupujeme podle standardu v `docs/MAUI_Routing_Guide.md`.

---

## 1. Logo na Login Stránce — Analýza & Oprava

### Problém
- LoginPage.xaml.cs hledá logo v `FileSystem.OpenAppPackageFileAsync("demizon_logo.jpg")`
- Logo je ale v `Resources/Raw/demizon_logo.jpg`, ne v AppPackage
- V Raw resources se načítají přes `ResourceManager` nebo ImageSource

### Řešení
- Změnit LoginPage.xaml.cs: místo `OpenAppPackageFileAsync` použít `ImageSource.FromFile("demizon_logo.jpg")` nebo StreamImageSource
- Ověřit, že XAML deklarace je správná (měla by být)

---

## 2. Dark Theme — App.xaml.cs

### Problém
```csharp
UserAppTheme = AppTheme.Light;  // Ignoruje systémové nastavení
```

### Řešení
- Změnit na `UserAppTheme = AppTheme.Unspecified;` aby se respektoval systém

---

## 3. Routovací Chyby — Prošetření & Opravy

### Zjištěné problémy (podle hlášení)
- Kliknutí na akci v AllMembersAttendancePage (sloupec header nebo cell) → crash
- Kliknutí na tanec → crash
- Kliknutí na docházku (v AttendancePage) → crash
- Opakované kliknutí (3x) na cizí docházku v AllMembersAttendancePage → crash

### Pravděpodobná příčina
1. **TapGestureRecognizer bez error handling** — nemá try-catch, navigace selhává tiše
2. **QueryProperty setter nemusí běžet před LoadAsync()** — `RehearsalDateString` vs `EventId` parametry
3. **Null reference v navigaci** — `_vm` by mohl být null v edge case

### Řešení
1. Přidat error handling do všech `tap.Tapped` event handlery
2. Ověřit pořadí inicializace QueryProperty vs LoadAsync v DetailPage
3. Přidat null checks na `_vm` reference
4. Testovat navigaci s příslušnými parametry

---

## 4. Editace Cizí Docházky — Nová Funkce

### Požadavek
Na stránce `AllMembersAttendancePage`:
- Kliknutí na cizí docházku (MemberCell) by mělo otevřít editační view
- Editační view bude zobrazovat:
  - Které datum se upravuje
  - Které člověka se upravuje
  - Participaci (checkbox yes/no) — jako na normální AttendanceDetail

### Implementace
1. Vytvořit nový typ navigace pro "MemberAttendanceDetail":
   - Route: `member-attd-detail` (nová konstanta v AppRoutes.cs)
   - Parametry: `?eventId={id}&memberId={id}` nebo `?rehearsalDate={date}&memberId={id}`

2. Vytvořit nový ViewModel: `MemberAttendanceDetailViewModel`
   - Přijme `IApiClient` a `INavigationService`
   - QueryProperty: `EventId`, `MemberId`, `RehearsalDateString`
   - Logika: načíst data cizího člena pro danou akci/zkoušku
   - API call: např. `GET /api/attendances/{eventId}/member/{memberId}` (ověřit v API)

3. Vytvořit novou Page: `MemberAttendanceDetailPage`
   - Layout: stejný jako EventDetailPage (radios + save/cancel)
   - Data binding na nový ViewModel

4. Registrovat v `AppShell.xaml.cs` a `MauiProgram.cs`

5. Změnit `AllMembersAttendancePage.xaml.cs` — v `AddAttendanceCell`:
   - Rozlišit: jde-li o "můj" řádek (název = current user) → naviguj na `EventDetail`
   - Jinak → naviguj na `MemberAttendanceDetail` s dalšími parametry

---

## Soubory k úpravě

| # | Soubor | Typ změny | Důvod |
|---|--------|-----------|-------|
| 1 | `Pages/LoginPage.xaml.cs` | Edit | Oprava načítání loga z Raw resources |
| 2 | `App.xaml.cs` | Edit | Dark theme support + error handling |
| 3 | `Pages/Attendance/AllMembersAttendancePage.xaml.cs` | Edit | Error handling v tap handlers + logika pro cizí docházku |
| 4 | `Pages/EventDetailPage.xaml.cs` | Edit | Error handling + ověření LoadAsync pořadí |
| 5 | `Pages/DanceDetailPage.xaml.cs` | Edit | Error handling + ověření LoadAsync pořadí |
| 6 | `AppRoutes.cs` | Edit | Přidat novou konstantu pro MemberAttendanceDetail |
| 7 | `AppShell.xaml.cs` | Edit | Registrovat novou stránku |
| 8 | `MauiProgram.cs` | Edit | Registrovat nový ViewModel a Page |
| 9 | `ViewModels/Attendance/MemberAttendanceDetailViewModel.cs` | Create | Nový ViewModel |
| 10 | `Pages/Attendance/MemberAttendanceDetailPage.xaml` | Create | Nová Page (XAML) |
| 11 | `Pages/Attendance/MemberAttendanceDetailPage.xaml.cs` | Create | Nová Page (code-behind) |
| 12 | `ViewModels/EventDetailViewModel.cs` | Edit | Error handling + ověření LoadAsync |
| 13 | `ViewModels/DanceDetailViewModel.cs` | Edit | Error handling + ověření LoadAsync |

---

## Poznámky

- Pokud API endpoint pro čtení cizí docházky (`GET /api/attendances/{eventId}/member/{memberId}`) neexistuje, bude potřeba jej implementovat na backendu
- Ověřit, že `OnAppearing()` → `LoadCommand.Execute(null)` běží POTÉ, co jsou QueryProperty hodnoty nastaveny
- Při testování: klikat opakovaně, měnit taby, jít zpět a znovu vpřed
- Dark theme je jednoducha změna — testovat na Android emulatoru s dark mode zapnutým
