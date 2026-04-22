# Plán oprav a vylepšení – mobilní aplikace + MVC

> Vytvořeno: 2026-04-23  
> Aktualizováno: 2026-04-23 (iterace 3 – oprava crash, NavBar, swipe, status bar)  
> Stav: ✅ Tři iterace dokončeny – čeká se na testování

---

## Přehled úkolů

| # | Úkol | Oblast | Složitost | Stav |
|---|------|--------|-----------|------|
| 1 | Změna hesla v profilu (MAUI) | MAUI + API | Střední | ✅ Hotovo |
| 2 | Fotogalerie u jednotlivých tanců | MAUI + API + MVC | Vysoká | ✅ Hotovo (MAUI + MVC správa) |
| 3 | Odstranit pole Opakování (MAUI + MVC) | MAUI + API | Nízká | ✅ Hotovo |
| 4 | Odstranit duplicitní tlačítko Zrušit akci | MAUI | Nízká | ✅ Hotovo |
| 5 | Odstranit "Demižón" lištu (Shell NavBar) | MAUI/Android | Nízká | ✅ Iter3: globální + programatické skrytí NavBar, světlý status bar |
| 6 | Opravit swipe gesta na docházce | MAUI | Střední | ✅ Iter3: SwipeGestureRecognizer místo PanGestureRecognizer |
| 7 | Dlouhý stisk pro zobrazení poznámky | MAUI | Střední | ✅ Iter3: opravený binding (x:Reference místo RelativeSource) |
| 8 | Swipe na přehledu docházky (tabulka) | MAUI | Nízká | ✅ Iter3: SwipeGestureRecognizer na RootGrid |
| 9 | Zlepšit plynulost animací (back navigace) | MAUI | Nízká | ✅ Opraveno (vypnuta back animace) |
| 10 | Refresh indikátor se zasekne na stránce Akce | MAUI | Nízká | ✅ Hotovo |

---

## 1. Změna hesla v profilu (MAUI)

### Současný stav
- `EditProfilePage` umožňuje editaci jména, příjmení a emailu.
- **Neexistuje** API endpoint pro změnu hesla.
- `Member.PasswordHash` v DB existuje, heslo se hashuje přes `Crypto.VerifyHashedPassword`.

### Návrh řešení

#### A) Nový API endpoint
**Soubor:** `Demizon.Mvc/Controllers/Api/MembersController.cs`
- Přidat `[HttpPut("me/password")]` endpoint.
- Přijímá `ChangePasswordRequest(string CurrentPassword, string NewPassword)`.
- Ověří stávající heslo pomocí `Crypto.VerifyHashedPassword`, poté nastaví nový hash.
- Vrací `200 OK` nebo `400 BadRequest` při špatném aktuálním heslu.

**Nový soubor:** `Demizon.Contracts/Members/ChangePasswordRequest.cs`
```csharp
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
```

#### B) Refit definice
**Soubor:** `Demizon.Maui/Services/IApiClient.cs`
- Přidat: `[Put("/api/members/me/password")] Task ChangePasswordAsync([Body] ChangePasswordRequest request);`

#### C) Nová MAUI stránka
- **Route:** `AppRoutes.ChangePassword = "change-password"` (plochý název dle konvence)
- **Soubory:**
  - `Demizon.Maui/Pages/ChangePasswordPage.xaml` – formulář se 3 poli: Aktuální heslo, Nové heslo, Potvrzení nového hesla.
  - `Demizon.Maui/ViewModels/ChangePasswordViewModel.cs` – validace (min. délka, shoda hesel), volání API.
- **Navigace z profilu:** Na `ProfilePage.xaml` přidat tlačítko "Změnit heslo" vedle "Upravit profil".

---

## 2. Fotogalerie u jednotlivých tanců

### Současný stav
- Globální `GalleryPage` zobrazuje všechny veřejné fotky (`IsPublic == true`).
- DB entita `File` již má `DanceId` (nullable FK na `Dance`) a `IsPublic` flag.
- `DanceDetailPage` aktuálně zobrazuje jen popis, text písně a videa – **žádné fotky**.
- API endpoint `GET /api/files/gallery` vrací `GalleryPhotoDto(Id, DanceName)` – globální galerie.
- MVC admin stránka `Dances.razor` zobrazuje fotky u tanců (`.Include(x => x.Files)`).

### Návrh řešení

#### A) Nový API endpoint pro fotky tance
**Soubor:** `Demizon.Mvc/Controllers/Api/DancesController.cs`
- Přidat `[HttpGet("{id:int}/photos")]` – vrátí seznam fotek (`GalleryPhotoDto`) napojených na daný tanec.
- Přidat `[HttpPut("{danceId:int}/photos/{photoId:int}/visibility")]` – změní `IsPublic` flag na konkrétní fotce (Admin only).

#### B) Rozšířit DanceDto o fotky
**Soubor:** `Demizon.Contracts/Dances/DanceDto.cs`
- Přidat property `List<DancePhotoDto> Photos` (nebo znovupoužít `GalleryPhotoDto`).
- Alternativně: API vrátí fotky zvlášť přes nový endpoint (lepší pro lazy loading).

#### C) MAUI – Fotogalerie v DanceDetailPage
**Soubor:** `Demizon.Maui/Pages/DanceDetailPage.xaml`
- Přidat sekci "Fotogalerie" pod Videa (CollectionView s 3-column GridItemsLayout).
- Tap na fotku otevře existující `PhotoViewerPage` s filtrem na fotky tohoto tance.

**Soubor:** `Demizon.Maui/ViewModels/DanceDetailViewModel.cs`
- Přidat `ObservableCollection<GalleryPhotoItem> Photos` property.
- Načíst fotky z nového API endpointu při `LoadAsync()`.

#### D) MAUI – Tlačítko galerie na DancesPage
- Zvážit odebrání globálního tlačítka "📷 Fotogalerie" z `DancesPage.xaml` (řádek 748-755), pokud globální galerie ztrácí smysl. Nebo ho zachovat jako "všechny fotky".

#### E) Správa IsPublic flagu
- V MVC admin nebo přes API endpoint umožnit nastavení viditelnosti fotky u tance.
- Na public webu (`Dances.razor`) se zobrazí jen fotky s `IsPublic == true`.

---

## 3. Odstranit pole Opakování (MAUI + MVC)

### Současný stav
- **MAUI CreateEventPage.xaml** (řádky 61-69): Picker "Opakování" s možnostmi `["Jednorázová", "Týdně", "Měsíčně"]`.
- **MAUI EditEventPage.xaml** (řádky 60-68): Totéž.
- **MAUI CreateEventViewModel.cs**: `RecurrenceOptions`, `RecurrenceIndex`, `RecurrenceMap`.
- **MAUI EditEventViewModel.cs**: Totéž + načítání z API.
- **API kontrakty**: `CreateEventRequest.Recurrence`, `UpdateEventRequest.Recurrence`.
- **DB**: `Event.Recurrence` (enum), `Event.RecurrenceEndDate`.
- **MVC EventForm.razor**: Recurrence pole tu **již chybí** (formulář ho neobsahuje).
- **API controller**: Stále přijímá a mapuje Recurrence, ale s `_ => RecurrenceType.None` jako default.

### Návrh řešení

#### A) MAUI – Odstranit UI
- **CreateEventPage.xaml**: Smazat Picker pro Opakování.
- **EditEventPage.xaml**: Smazat Picker pro Opakování.
- **CreateEventViewModel.cs**: Smazat `RecurrenceOptions`, `RecurrenceIndex`, `RecurrenceMap`. Vždy posílat `"None"`.
- **EditEventViewModel.cs**: Smazat totéž.

#### B) API kontrakty – zachovat zpětnou kompatibilitu
- `CreateEventRequest.Recurrence` a `UpdateEventRequest.Recurrence` nechat v kontraktech (nullable), ale MAUI je nebude nastavovat nebo pošle `"None"`.
- Controller bude stále fungovat jako dosud (default `None`).

#### C) DB
- `Event.Recurrence` a `Event.RecurrenceEndDate` ponechat v DB – nebudu mazat existující sloupce. Žádná nová migrace potřeba není.

---

## 4. Odstranit duplicitní tlačítko Zrušit akci

### Současný stav
- **EventDetailPage.xaml** (řádky 233-240): Samostatné tlačítko `{Binding CancelButtonText}` v admin sekci "Správa akce".
- **EditEventPage.xaml** (řádky 83-94): Switch "Zrušená akce" (`IsCancelled` toggle).
- Obojí dělá totéž – přepíná `IsCancelled` flag.

### Návrh řešení
- **Smazat** z `EventDetailPage.xaml` tlačítko pro zrušení akce (řádky 233-240).
- **Smazat** z `EventDetailViewModel.cs` command `ToggleCancelCommand`, `ToggleCancelAsync()` metodu a property `CancelButtonText`.
- Zachovat Switch v `EditEventPage.xaml` jako jediný způsob zrušení/obnovení akce.

---

## 5. Android status bar zatmavení

### Současný stav
- `MainActivity.cs` používá výchozí `Maui.SplashTheme` bez úprav.
- Neexistují žádné soubory `Platforms/Android/Resources/values/styles.xml`.
- Aplikace má světlý motiv (`PageBackground: #FEFBF5`), nav bar `Accent: #9A7450`.
- Systémový status bar není explicitně stylován – závisí na výchozím chování Androidu.

### Návrh řešení

#### A) Android theme – styles.xml
**Nový soubor:** `Demizon.Maui/Platforms/Android/Resources/values/styles.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <style name="Maui.MainTheme" parent="Maui.SplashTheme">
        <item name="android:statusBarColor">@color/statusBarColor</item>
        <item name="android:windowLightStatusBar">false</item>
    </style>
    <color name="statusBarColor">#7A5A38</color>
</resources>
```

#### B) MainActivity.cs
- Změnit `Theme = "@style/Maui.MainTheme"` v atributu `[Activity(…)]`.
- Alternativně: programaticky nastavit barvu status baru v `OnCreate`:
```csharp
Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#7A5A38"));
```

#### C) Zvážit edge-to-edge mód
- Na novějších Androidech (API 35+) se status bar automaticky řídí systémovým tématem. Pokud uživatel vidí "zatmavení", může jít o semi-transparentní overlay. Potřeba nastavit `android:windowTranslucentStatus = false` a explicitní barvu.

---

## 6. Opravit swipe gesta na docházce

### Současný stav
- **AttendancePage.xaml**: `SwipeGestureRecognizer` je na 3 úrovních:
  1. Root `Grid` (řádky 11-14)
  2. Vnitřní `Grid` uvnitř `RefreshView` (řádky 79-82)
  3. Jednotlivé karty eventů (řádky 103-106, threshold=30)
- **Problém**: `RefreshView` zachytává vertikální gesta a MAUI `SwipeGestureRecognizer` bojuje s `RefreshView` o horizontální swipe. RefreshView má přednost u vertikálních gest, čímž „krade" i šikmé tahy.

### Návrh řešení

#### A) Nahradit SwipeGestureRecognizer za PanGestureRecognizer
- Stejný přístup jako v `AllMembersAttendancePage.xaml.cs`, kde `PanGestureRecognizer` funguje spolehlivěji.
- Přidat `PanGestureRecognizer` na `CollectionView` nebo na root `Grid`.
- Detekovat horizontální swipe s prahovými hodnotami: `dx > 70px` a `dx > dy * 2.5`.
- Odebrat všechny `SwipeGestureRecognizer` z AttendancePage.

#### B) Kód v code-behind
**Soubor:** `Demizon.Maui/Pages/Attendance/AttendancePage.xaml.cs`
- Přidat PanGestureRecognizer s logikou z AllMembersAttendancePage.
- Napojit na `NextMonthCommand` / `PreviousMonthCommand`.

---

## 7. Dlouhý stisk pro zobrazení poznámky na docházce

### Současný stav
- **AttendancePage.xaml** (řádek 175-177): Ikona 📝 se zobrazuje u záznamů s komentářem.
- Na hlavní docházkové stránce (`AttendancePage`) **neexistuje** žádná interakce pro zobrazení poznámky bez rozkliknutí detailu.
- Na `AllMembersAttendancePage` existuje tap → DisplayAlert pro non-admin uživatele.

### Návrh řešení

#### A) Přidat dlouhý stisk na AttendancePage
- Na každou kartu eventu (Border s `TapGestureRecognizer`) přidat detekci dlouhého stisku.
- MAUI nemá nativní `LongPressGestureRecognizer`, ale existují 2 přístupy:
  1. **Platform behavior** – napsat custom `LongPressBehavior` (Android `OnLongClick` + iOS `UILongPressGestureRecognizer`).
  2. **Workaround s PointerGestureRecognizer + Timer** – na `PointerPressed` spustit timer (~500ms), na `PointerReleased` zrušit.
  3. **TapGestureRecognizer** – MAUI 9+ podporuje `NumberOfTapsRequired` ale ne long press.
- **Doporučení**: Custom attached behavior `LongPressBehavior` – nejčistší řešení.

#### B) Zobrazit popup/modal
- Při dlouhém stisku zavolat `Shell.Current.DisplayAlert("Poznámka", comment, "OK")`.
- Alternativně: `CommunityToolkit.Maui` nabízí `Popup` pro lepší UX.

---

## 8. Swipe na přehledu docházky (tabulka)

### Současný stav
- **AllMembersAttendancePage.xaml.cs**: `PanGestureRecognizer` přidán na `TableScrollView` (řádek 121).
- Detekuje horizontální swipe (`dx > 70, dx > dy * 2.5`).
- Uživatel hlásí, že to nefunguje na celé ploše tabulky.

### Návrh řešení
- `PanGestureRecognizer` je přidán pouze na ScrollView. Pokud ScrollView zachytí pan pro vlastní scrolling, gesture recognizer nemusí dostat šanci.
- **Řešení**: Přidat PanGestureRecognizer i na header border (navigace měsíce) a případně na celý root Grid.
- Snížit threshold z `70` na `50` pro citlivější detekci.
- Alternativně: Přidat SwipeGestureRecognizer na root Grid stránky (funguje lépe mimo ScrollView).

---

## 9. Zlepšit plynulost animací

### Současný stav
- Žádné explicitní animace v kódu neexistují.
- Hardware akcelerace je zapnutá (`android:hardwareAccelerated="true"`).
- Jankiness je pravděpodobně způsobena:
  1. MAUI frameworkovými přechody stránek (Shell navigation transitions).
  2. Layout thrashing při sestavování `CollectionView` s komplexními šablonami.
  3. Alokace `ObservableCollection` při každém přechodu měsíce.

### Návrh řešení

#### A) Optimalizace Shell navigace
- Nastavit `Shell.PresentationMode="ModalAnimated"` nebo `NotAnimated` pro pomalé přechody.
- Použít `await Shell.Current.GoToAsync(route, false)` (animate=false) pro kritické přechody.

#### B) Optimalizace layout
- Na AttendancePage: zjednodušit DataTemplate – méně vnořených layoutů.
- `CollectionView.ItemSizingStrategy="MeasureFirstItem"` pro konzistentní výšku řádků.
- Předejít re-measure celé kolekce při změně dat.

#### C) AllMembersAttendancePage – custom drawn tabulka
- Tato stránka generuje UI programaticky (Grid s buňkami). Na velký počet členů × událostí to může být pomalé.
- Zvážit `VirtualizeLayout` nebo `CollectionView` místo ručního `Grid.Add()`.

#### D) Reduce allocations
- Místo `new ObservableCollection<>()` při každém `LoadAsync()` zvážit `.Clear()` + `.Add()`.

---

## 10. Refresh indikátor se zasekne na stránce Akce

### Současný stav
- **EventsPage.xaml** (řádek 12): `<RefreshView Command="{Binding LoadCommand}" IsRefreshing="{Binding IsBusy}">`
- **EventsViewModel.cs**: `LoadAsync()` nastavuje `IsBusy = true` na začátku, `IsBusy = false` ve `finally`.
- Problém: `RefreshView.IsRefreshing` je navázán na `IsBusy`, ale `RefreshView` interně nastavuje `IsRefreshing = true` při pull-down. Pokud `IsBusy` guard clause (`if (IsBusy) return;`) zabrání provedení metody, `IsBusy` zůstane `true` navždy.
- Dalši scénář: `Receive(EventsChangedMessage)` volá `LoadCommand.Execute(null)` – pokud se spustí během jiného načítání, guard clause vrátí a `IsRefreshing` se neresetuje.

### Návrh řešení

#### A) Separovat IsRefreshing od IsBusy
**Soubor:** `Demizon.Maui/ViewModels/EventsViewModel.cs`
- Přidat separátní `[ObservableProperty] private bool _isRefreshing;` property.
- `RefreshView.IsRefreshing` bindovat na `IsRefreshing` (ne `IsBusy`).
- V `LoadAsync()` nastavit `IsRefreshing = false` ve `finally`.

#### B) Přidat RefreshCommand
- Vytvořit separátní `[RelayCommand] RefreshAsync()` metodu pro RefreshView.
- Tato metoda vždy zavolá `LoadAsync()` a poté nastaví `IsRefreshing = false`.
- Nebo: Přidat vždy `IsRefreshing = false` na konec `LoadAsync()` (i při early return z guard clause).

#### C) Opravit guard clause
```csharp
[RelayCommand]
public async Task LoadAsync()
{
    if (IsBusy)
    {
        IsRefreshing = false; // Reset spinner i při early return
        return;
    }
    // ... rest of method
}
```

---

## Pořadí implementace (doporučené)

1. **#10 Refresh indikátor** – rychlý fix, okamžitý efekt
2. **#3 Odstranit Opakování** – čistá práce, smazání kódu
3. **#4 Odstranit duplicitní Cancel tlačítko** – čistá práce, smazání kódu
4. **#5 Android status bar** – malý konfigurační fix
5. **#6 Swipe gesta na docházce** – středně náročné, velký UX dopad
6. **#8 Swipe na přehledu docházky** – navazuje na #6
7. **#7 Dlouhý stisk pro poznámku** – nový gesture handling
8. **#1 Změna hesla** – nový endpoint + stránka
9. **#9 Animace** – investigace + optimalizace
10. **#2 Fotogalerie u tanců** – největší scope, nové API + UI

---

## Poznámky k implementaci

### #10 Refresh indikátor
- Přidána separátní property `IsRefreshing` do `EventsViewModel`
- `RefreshView` bindován na `IsRefreshing` místo `IsBusy`
- Reset v guard clause i ve finally bloku

### #3 Odstranit Opakování
- Odstraněn Picker z `CreateEventPage.xaml` i `EditEventPage.xaml`
- Odstraněny `RecurrenceOptions`, `RecurrenceIndex`, `RecurrenceMap` z obou ViewModelů
- API kontrakty zachovány pro zpětnou kompatibilitu, MAUI posílá vždy `"None"`

### #4 Odstranit duplicitní Cancel tlačítko
- Odstraněno standalone toggle tlačítko ze sekce "Správa akce" v `EventDetailPage.xaml`
- Odstraněn `ToggleCancelCommand`, `ToggleCancelAsync()` a `CancelButtonText` z ViewModelu
- Zrušení akce zůstává jen v editačním formuláři (Switch)

### #5 Android status bar
- Vytvořen `Platforms/Android/Resources/values/styles.xml` s `Maui.MainTheme`
- Status bar barva: `#7A5A38` (PrimaryDark), bílé ikony (`windowLightStatusBar=false`)
- `MainActivity.cs` přepnut na nový theme

### #6 Swipe gesta na docházce
- Všechny `SwipeGestureRecognizer` nahrazeny `PanGestureRecognizer`
- Prahové hodnoty: dx > 60px, dx > dy * 2 (rozlišení od pull-to-refresh)
- PanGestureRecognizer na `MainRefreshView` pro celoplošnou detekci

### #8 Swipe na přehledu docházky
- PanGestureRecognizer přidán na `RootGrid` i `TableScrollView`
- Snížen threshold z 70 na 50px pro citlivější detekci

### #7 Zobrazení poznámky
- Místo long-press (MAUI nemá nativní podporu) implementováno jako tap na 📝 ikonu
- `ShowNoteCommand` v `AttendanceViewModel` zobrazí DisplayAlert s obsahem poznámky

### #1 Změna hesla
- Nový API endpoint `PUT /api/members/me/password` v `MembersController`
- Nový DTO `ChangePasswordRequest` v `Demizon.Contracts`
- Nová MAUI stránka `ChangePasswordPage` s validací (min. 6 znaků, shoda hesel)
- Navigace z `ProfilePage` tlačítkem "Změnit heslo"

### #9 Animace
- Přidán `ItemSizingStrategy="MeasureFirstItem"` na hlavní `CollectionView` (AttendancePage, EventsPage, DancesPage)
- Redukuje layout thrashing při scrollování a změně dat

### #2 Fotogalerie u tanců
- Nový API endpoint `GET /api/dances/{id}/photos` v `DancesController` (vrací všechny fotky tance)
- Aktualizován `FilesController.GetImage` – autentizovaní uživatelé vidí i ne-veřejné fotky
- Přidána sekce "Fotogalerie" do `DanceDetailPage` (3-sloupcový grid s thumbnaily)
- Tap na fotku otevře existující `PhotoViewerPage`
- Odstraněno globální tlačítko "📷 Fotogalerie" z `DancesPage` (nedávalo smysl dle požadavku)
- Refit definice `GetDancePhotosAsync` přidána do `IApiClient`

---

## Iterace 2 – Opravy po testování (2026-04-23)

### #2v2 – MVC správa fotek u tanců

**Problém:** MAUI stránka hotová, ale v MVC admin neexistuje způsob jak přiřadit fotky k tancům.

**Současný stav MVC:**
- `Pages/Admin/Photo/ListPhotos.razor` – upload, toggle veřejnost, smazání. **Chybí** přiřazení k tanci.
- `Pages/Admin/Dance/Detail.razor` – zobrazuje info, videa, texty. **Chybí** sekce fotek.
- `POST /api/gallery/upload` nepřijímá `danceId` parametr.
- DB entita `File.DanceId` (FK) existuje, ale není nikde v admin UI nastavitelná.

**Návrh řešení:**
1. Rozšířit `POST /api/gallery/upload` o volitelný `danceId` query parametr
2. Přidat `PUT /api/gallery/{id}` endpoint pro úpravu fotky (změna DanceId, IsPublic)
3. Přidat sekci "Fotografie" do `Dance/Detail.razor` (analog k sekci Videa)
4. V `ListPhotos.razor` přidat sloupec "Tanec" s možností přiřazení

### #5v2 – Odstranit "Demižón" lištu (Shell NavBar)

**Problém:** Předchozí fix (styles.xml s barvou status baru) problém nevyřešil. Uživatel chce odstranit celý tmavý pás "Demižón" pod Android status barem.

**Analýza:** Tmavý pás je **Shell Navigation Bar** definovaný v:
- `AppShell.xaml`: `Shell.BackgroundColor="#9A7450"` (řádky 8-10)
- `Styles.xaml`: Style pro Shell s `Accent` barvou
- `AndroidManifest.xml`: `android:label="Demižón"` (zobrazuje se jako titulek)

**Návrh řešení:**
- Přidat `Shell.NavBarIsVisible="False"` na `<Shell>` element v `AppShell.xaml`
- Tím zmizí celý tmavý pás "Demižón" globálně
- Stránky typu detail (kde je back button) budou potřebovat vlastní navigaci zpět, ale to řeší Android systémové gesto / hardware back button
- Revertovat styles.xml pokud už není potřeba

### #6v2 – Swipe gesta na docházce (oprava)

**Problém:** PanGestureRecognizer je připojen na `MainRefreshView` (RefreshView). RefreshView zachytává a konzumuje pan gesta pro pull-to-refresh, takže custom PanGestureRecognizer se nikdy nespustí.

**Návrh řešení:**
- Přesunout PanGestureRecognizer z RefreshView na **Grid uvnitř RefreshView** (`ContentGrid`)
- Přidat `x:Name="ContentGrid"` na vnitřní Grid v AttendancePage.xaml
- V code-behind připojit pan na `ContentGrid` místo `MainRefreshView`

### #7v2 – Dlouhý stisk pro poznámku (oprava)

**Problém:** TapGestureRecognizer na 📝 ikoně koliduje s TapGestureRecognizer na kartě (Border), takže tap na ikonu vždy otevře detail místo poznámky.

**Analýza:** CommunityToolkit.Maui není v projektu (jen CommunityToolkit.Mvvm). Toolkit nabízí `TouchBehavior` s `LongPressCommand`.

**Návrh řešení:**
1. Přidat NuGet `CommunityToolkit.Maui` do `Demizon.Maui.csproj`
2. Zaregistrovat `.UseMauiCommunityToolkit()` v `MauiProgram.cs`
3. Nahradit TapGestureRecognizer na 📝 za `TouchBehavior` s `LongPressCommand`
4. Uživatel podrží prst ~500ms na kartě → zobrazí se poznámka v DisplayAlert
5. Krátký tap stále otevře detail (bez konfliktu)

### #8v2 – Swipe na přehledu docházky (oprava)

**Problém:** Stejný jako #6v2 – PanGestureRecognizer na ScrollView nefunguje, protože ScrollView konzumuje pan gesta pro scrolling.

**Návrh řešení:**
- Odebrat PanGestureRecognizer z `TableScrollView`
- Ponechat pouze na `RootGrid` (to by mělo fungovat, protože Grid nekonzumuje gesta)
- Alternativně: obalit obsah ScrollView do ContentView a připojit pan tam

### #9v2 – Back navigace animace (oprava)

**Problém:** Shell back navigace (`GoToAsync("..", true)`) je sekaná na 120Hz Android displejích. Známý problém MAUI frameworku.

**Analýza:** `ShellNavigationService.GoBackAsync()` volá `Shell.Current.GoToAsync("..", true)` – parametr `true` vynucuje animaci.

**Návrh řešení:**
- Změnit `animate: true` na `animate: false` v `ShellNavigationService.GoBackAsync()`
- Navigace zpět bude okamžitá (bez animace), ale bez sekání
- Standardní řešení pro MAUI na 120Hz Android zařízeních

### Pořadí implementace iterace 2

1. **#5v2** – Shell NavBar (1 řádek)
2. **#9v2** – Back animace (1 řádek)
3. **#6v2** – Swipe attendance (přesun gesture na správný element)
4. **#8v2** – Swipe overview (přesun gesture na správný element)
5. **#7v2** – Long press (přidání CommunityToolkit.Maui + TouchBehavior)
6. **#2v2** – MVC správa fotek (největší scope – nové UI komponenty)

---

## Iterace 3 – Oprava crash a dalších problémů (2026-04-23)

### Crash po přihlášení

**Příčina (z maui-log.txt):**
```
FATAL EXCEPTION: Cannot apply relative binding to CommunityToolkit.Maui.Behaviors.TouchBehavior
because it is not a superclass of Element.
```

**Problém:** `TouchBehavior` je `Behavior`, nikoliv vizuální `Element`. Binding `{Binding Source={RelativeSource AncestorType={x:Type vm:AttendanceViewModel}}, ...}` nefunguje u Behaviors, protože `RelativeSource AncestorType` prochází jen vizuální strom (Visual Tree).

**Oprava:** Nahrazeno za `{Binding Source={x:Reference ThisPage}, Path=BindingContext.ShowNoteCommand}` — přidáno `x:Name="ThisPage"` na ContentPage a použit `x:Reference` místo `RelativeSource`.

### NavBar stále viditelný

**Problém:** `Shell.NavBarIsVisible="False"` na jednotlivých `ShellContent` nefungovalo pro pushed (detail) stránky.

**Oprava:**
1. Přidáno `Shell.NavBarIsVisible="False"` globálně na `<Shell>` element
2. Přidán `Navigated` event handler v `AppShell.xaml.cs` – při každé navigaci programaticky nastaví `SetNavBarIsVisible(CurrentPage, false)` pro pushed stránky

### Status bar barva

**Problém:** Tmavý status bar (#7A5A38) vypadal špatně po odstranění NavBar.

**Oprava:** Změna na světlý status bar (#FAF3E8 = barva pozadí stránky) s `windowLightStatusBar=true` (tmavé ikony na světlém pozadí).

### Swipe gesta nefungují (#6, #8)

**Problém:** `PanGestureRecognizer` nefunguje uvnitř `RefreshView` ani `ScrollView` – tyto kontejnery konzumují všechna pan gesta.

**Oprava:** Kompletní nahrazení `PanGestureRecognizer` za `SwipeGestureRecognizer` (dedikovaný left/right), který koexistuje s vertikálním pull-to-refresh i scrollováním. Implementováno na obou stránkách:
- `AttendancePage` – SwipeGestureRecognizer na ContentGrid
- `AllMembersAttendancePage` – SwipeGestureRecognizer na RootGrid

### Změněné soubory (iterace 3)

| Soubor | Změna |
|--------|-------|
| `AttendancePage.xaml` | `x:Name="ThisPage"`, binding oprava TouchBehavior |
| `AttendancePage.xaml.cs` | SwipeGestureRecognizer místo PanGestureRecognizer |
| `AllMembersAttendancePage.xaml.cs` | SwipeGestureRecognizer místo PanGestureRecognizer |
| `AppShell.xaml` | Globální `Shell.NavBarIsVisible="False"` |
| `AppShell.xaml.cs` | `Navigated` handler pro programatické skrytí NavBar |
| `styles.xml` | Světlý status bar (#FAF3E8, dark icons) |
