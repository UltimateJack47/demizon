# Krojárka: inventář a předávky souborových krojů

> Založeno: 2026-10-25. Stav: 🔨 **probíhá** — kroky 0, A, B hotové (2026-10-26),
> další je C (API). Co se při implementaci rozhodlo jinak než v návrhu: [Implementace](#implementace).
>
> Sekce *Současný stav* popisuje repozitář **před** implementací.

## Zadání

Krojárka je místnost, kde soubor skladuje kroje, které si členové půjčují.
Funkčně jde o malý sklad:

1. Správce krojárky (1, nejvýš 2 lidé) nadefinuje, co v krojárce je: kroje,
   jejich součástky a konkrétní kusy (velikost, barva, specifika, hodnota).
   Admini / vůdci souboru se tam mohou dívat.
2. **Evidence po kusech** (každý kus má vlastní QR) je hlavní režim. Má ale jít
   i **evidence počtem** (drobnosti, nebo soubor, který to tak chce) — obojí.
3. Kusy se předávají správce → člen, člen → správce i **člen → člen** napřímo,
   bez cesty přes správce.
4. Při každé předávce se **každý kus fotí zvlášť** a **příjemce stav potvrdí**
   na svém zařízení. Smysl: příjemce svým potvrzením přebírá odpovědnost za
   stav, ve kterém kus dostal. Nemůže se pak vymlouvat na předchozího držitele
   ani na starou fotku od správce.
5. Párování jako na pokladně: naskenovat QR kusů, pak QR z profilu příjemce →
   systém ví všechno sám, nic se nevybírá ručně.
6. Vracet jde i **po kusech** (půjčil 3, vrací 1).
7. **Žádná vazba na akci ani termín vrácení** — půjčuje se klidně na celou sezónu.
8. Velikost je **volný text** podle toho, jak to krojařka značí (sukně `1`–`6`,
   nohavice v mm `100`, `106`, `120`). Žádné S/M/L.
9. Přehledy (kdo co má, co je volné), **připomínky pushem spouští správce**
   ručně (jednomu, více, všem), údržba (praní, oprava), inventura, hodnota kusu.

**Objem:** ~5 krojů × 3–5 součástek × ~12 kusů (6 mužských + 6 ženských
variant) ≈ **250 kusů**. Půjčuje se ~6 párům, hlavně na sezónu → předávek
jsou řádově stovky ročně, ne tisíce.

## Současný stav (2026-10-25)

Co se dá převzít:

- **Komprese fotek.** `FileUploadService.OptimizeImage`
  (`Demizon.Core/Services/FileUpload/FileUploadService.cs:140`): AutoOrient,
  zahození EXIF/IPTC/XMP, 1200 px JPEG q80 + náhled 200 px, BLOB v SQLite.
- **Push jednomu členovi.** `AttendanceReminderService.SendToMemberAsync`
  (`Demizon.Mvc/Services/Notification/AttendanceReminderService.cs:143`) — web
  push i FCM, uklízí neplatné tokeny.
- **Audit.** `AuditSaveChangesInterceptor` zapíše změny sám.
- **Příznaky na členovi.** `Member.IsDancer` / `IsMusician` + checkboxy
  v `MemberForm.razor:125` — vzor pro příznak správce.

Co chybí nebo překáží:

- **Role jsou jen `Standard` a `Admin`** (`Member.UserRole`).
- **Kvóta úložiště je společná pro tabulku `Files`**: 2 GB a **2 000 řádků**
  (`StorageQuotaService`). Fotky z předávek by sdílely limit s galerií.
- **Flutter nemá kameru ani skener** (v `pubspec.yaml` není `image_picker`
  ani `mobile_scanner`) a na fyzickém telefonu ještě neběží (STATUS B).
- **Blazor nemá skenování QR** a v `wwwroot/js` není žádná knihovna na kameru.
  Upload jde přes SignalR (`OpenReadStream(MaxFileBytes)`), fotka z mobilu
  o 4–8 MB tedy poputuje celá.
- **Žádné generování QR** na serveru ani v klientech.

## Návrh

### Datový model

Tři úrovně podle toho, jak krojárka skutečně vypadá, plus držení a předávky.

| Tabulka | K čemu | Hlavní pole |
|---|---|---|
| `Costume` | kroj („Kopanický“) | název, popis, pořadí |
| `CostumePart` | součástka / druh („Kopanická vesta“) | kroj (nullable — „ostatní“), název, pro koho (muž/žena/oboje), popis, režim evidence `Individual`/`Bulk` |
| `CostumeItem` | evidenční položka: **jeden kus** (Individual) nebo **šarže N stejných kusů** (Bulk) | součástka, inventární kód (unikátní, nese ho QR), velikost (text), barva (text), poznámka, hodnota v Kč (int), počet (Individual = 1), stav `Available`/`InMaintenance`/`Lost`/`Retired` |
| `CostumeHolding` | kdo kolik čeho drží | položka, člen, počet. **Krojárka nemá řádek** — v krojárce je `Quantity − Σ holdings` |
| `CostumeTransfer` | jedna předávka | od (člen / krojárka), komu (člen / krojárka), kdo ji založil, stav `Draft`/`Pending`/`Confirmed`/`Rejected`/`Cancelled`, časy, poznámka, důvod odmítnutí |
| `CostumeTransferLine` | kus v předávce | předávka, položka, počet, poznámka ke stavu |
| `CostumePhoto` | fotka | data + náhled, nahrál, `UploadedAt` (server), `CapturedAt` (z EXIF před jeho zahozením), vazba na řádek předávky **nebo** vzorová fotka součástky |
| `CostumeMaintenance` | údržba | položka, druh (praní, čištění, oprava, jiné), od–do, poznámka, cena |
| `CostumeInventoryCheck` + `…Entry` | inventura | kdo, kdy; naskenované položky a spočtené počty |

Na `Member` přibude sloupec `CostumeQrToken` (náhodný, jde přegenerovat).
Správce krojárky je **role** `CostumeKeeper`, ne sloupec — viz krok 0.

Hodnota v **celých Kč jako `int`**, protože SQLite `decimal` neumí
v EF řadit ani sčítat na straně DB.

### Předávka — stavový automat

```
Draft ──(fotka u každého řádku)──► Pending ──(příjemce potvrdí)──► Confirmed
  │                                   ├──(příjemce odmítne + důvod)──► Rejected
  └──────────(zakladatel zruší)───────┴──────────────────────────────► Cancelled
```

- **Holdings se mění až při `Confirmed`.** Do té doby kus drží a odpovídá za
  něj předávající. Kus v `Draft`/`Pending` nejde dát do jiné předávky.
- **Bez fotky u každého řádku nejde předávku odeslat** (platí i pro šarži —
  fotí se těch N kusů).
- **Potvrzuje vždy příjemce, sám, svým přihlášením.** QR z profilu jen
  identifikuje, koho se předávka týká. Potvrzení dává teprve přihlášený
  příjemce — kdo by si QR vyfotil, předávku za něj nepotvrdí.
- Kdo může předávku založit:
  - **z krojárky** — správce,
  - **ode člena** — ten člen (předání kolegovi), nebo správce,
  - **vrácení do krojárky** zakládá správce, který je zároveň příjemcem, takže
    potvrzuje rovnou. Vracející člen dostane notifikaci s fotkami.
- **Proti staré fotce:** server ukládá čas nahrání. Z EXIF se před zahozením
  přečte `DateTimeOriginal` a uloží se jako `CapturedAt`. Příjemce vidí
  varování, když je fotka starší než ~1 h. Flutter při předávce nabízí jen
  fotoaparát, ne galerii. Skutečná pojistka je ale samotné potvrzení příjemce.
- Odmítnutá předávka: kusy zůstávají předávajícímu, správce dostane notifikaci
  s důvodem.

### QR kódy

- **Kus:** QR obsahuje `https://<PublicBaseUrl>/k/<kód>`, pod ním je kód
  vytištěný čitelně (když se QR ošoupe nebo seprá, kód se napíše ručně).
  Obyčejný foťák otevře kartu kusu. Parser v aplikaci bere jen poslední
  segment, takže **štítky přežijí i změnu domény**.
- **Člen:** `…/m/<CostumeQrToken>`, zobrazený v profilu (Flutter i web).
- **Tisk štítků:** stránka s mřížkou štítků pro A4, `@media print`, výběr
  kusů nebo celého kroje.
- Generování na serveru (`QRCoder` → SVG pro web, PNG pro Flutter). Flutter
  tak nepotřebuje balíček na kreslení QR.
- **Ruční pole pro kód** je na každé obrazovce se skenováním. Navíc tím za
  ~500 Kč funguje i bluetooth/USB čtečka „jako na pokladně“, protože ta píše
  kód + Enter jako klávesnice.

### Kde v aplikaci

**Web (Blazor), nová položka menu „Krojárka“** pro všechny přihlášené. Obsah
podle oprávnění:

Admin = `Admin` i `Leader` (vůdce souboru). 👁 = jen čtení.

| Obrazovka | Člen | Správce | Admin |
|---|---|---|---|
| Moje kroje (co držím, čekající potvrzení, historie, můj QR, předat kolegovi) | ✔ | ✔ | ✔ |
| Karta kusu (z QR) — název, velikost, stav, kdo drží | ✔ | ✔ | ✔ |
| Katalog: kroje → součástky → kusy, dostupnost podle velikostí | — | ✔ | 👁 |
| Předávka: výdej / příjem (mobilní web, skenování) | — | ✔ | — |
| Kdo co má + připomínka pushem (jeden / vybraní / všichni držitelé) | — | ✔ | 👁 |
| Historie kusu se všemi fotkami | — | ✔ | 👁 |
| Údržba, inventura, tisk štítků, hodnota držená členy | — | ✔ | 👁 |

**Flutter:** „Moje kroje“ v Profilu (to samé co člen na webu, včetně předání
kolegovi a potvrzení). Správce dostane **pátý tab „Krojárka“**: sken → výdej /
příjem, kdo co má, připomínky. Katalog, štítky, inventura a údržba zůstávají
na webu. Inventuru jde dělat i z mobilního webu.

**Notifikace:** automatické jsou jen transakční — „potvrď převzetí“ příjemci,
„předávka odmítnuta“ správci, „vráceno“ vracejícímu. Připomínky „vrať kroj“
spouští správce ručně. FCM `data` nese `type=costumeTransfer` + `transferId`,
tap otevře potvrzení.

### Fotky a úložiště

- Vlastní tabulka `CostumePhoto`, **ne `Files`**: nesdílí limit 2 000 řádků
  s galerií a nemůže se omylem objevit ve veřejných dotazech.
- Komprese stejným pipeline. `OptimizeImage` se vytáhne z `FileUploadService`
  do sdílené třídy, aby šla použít bez kvóty `Files`.
- Vlastní limit `Costumes:MaxPhotoStorageBytes` (návrh 500 MB). Odhad:
  ~600 fotek ročně × ~200 kB ≈ 120 MB ročně.
- Retence zatím žádná. Jako důkaz mají smysl jen fotky posledních předávek
  kusu; promazávání starších se dá doplnit, až to bude potřeba.
- Klient fotku zmenší už před odesláním (Flutter `image_picker` `maxWidth`,
  web canvas v JS), aby se po mobilních datech a SignalR netahalo 8 MB.

## Rozhodnutí

Rozhodnuto uživatelem 2026-10-25: body 1–9 v [Zadání](#zadání).

Návrhová rozhodnutí (alternativy a důvod):

| # | Rozhodnutí | Alternativy | Proč |
|---|---|---|---|
| 1 | Jedna `CostumeItem` pro kus i šarži, držení v `CostumeHolding` | a) `HolderMemberId` na kusu + zvlášť tabulka pro šarže; b) jen kusy | Jedna cesta pro předávku, přehledy i inventuru. Kus je šarže s počtem 1. |
| 2 | Správce = role `CostumeKeeper` ve více-rolovém modelu (krok 0) | a) příznak `IsCostumeKeeper` na členovi; b) další hodnota jednoho `UserRole` | a) je obejití autorizace mimo role; b) neumí člověka, který je admin i správce. |
| 3 | Předávka jako řetěz `Transfer`ů, ne „výpůjčka + vrácení“ | `Loan` s `ReturnedAt` | Předávka člen → člen a vracení po kusech z modelu výpůjčky nevycházejí. |
| 4 | Server-side `Draft` | poslat všechny fotky najednou při odeslání | Výpadek signálu v krojárce neztratí rozfocenou předávku. |
| 5 | Vlastní `CostumePhoto` + vlastní limit | rozšířit `File` o FK | Viz *Fotky a úložiště*. |
| 6 | QR nese URL, parser bere poslední segment | holý kód bez URL | Funguje i obyčejný foťák a doména jde změnit. |

**Rozhodnuto 2026-10-25:**

- **O2.** Běžný člen katalog ani volné kusy nevidí, jen co právě drží (a kartu kusu z QR).
- **O3.** Vrácení do krojárky potvrzuje správce, vracející člen dostane notifikaci s fotkami.
- **O4.** Předávka člen → člen: správce dostane notifikaci, neschvaluje.
- **O5.** Závislosti schválené: `QRCoder`, `qr-scanner` (vendorovaný), `mobile_scanner`, `image_picker`.
- **O6.** Schéma schválené.

- **O1.** Role (2026-10-26): víc rolí na člena ([`role-clenu`](../role-clenu/README.md)). Vůdce
  souboru = role `Leader`, mimo krojárku nic navíc. Správce krojárky = `CostumeKeeper`.

## Postup

Commity po uzavřených částech, každá s testy.

| Krok | Obsah | Testy |
|---|---|---|
| 0 | ✅ Více rolí na člena — [`role-clenu`](../role-clenu/README.md) (2026-10-26) | tam |
| A | ✅ Model, migrace `AddCostumeStore`, QR token na členovi, `UserRole.Leader` + `CostumeKeeper` | `CostumeModelTests` |
| B | ✅ Core: `CostumeCatalogService`, `CostumeTransferService`, `CostumePhotoProcessor`, `CostumeQrService` | `CostumeTransferServiceTests`, `CostumeCatalogServiceTests`, `CostumeQrServiceTests` (integrační nad SQLite) |
| C | API + DTO (`/api/costumes/...`), QR endpointy | HTTP přes `WebApplicationFactory`: 401/403 podle role, kontrakt |
| D | Blazor: menu, katalog, tisk štítků, Moje kroje, potvrzení, karta kusu | E2E |
| E | Blazor mobilní web: předávka se skenem (kamera + ruční kód), fotky s kompresí v prohlížeči | E2E přes ruční zadání kódu (headless kamera neumí) |
| F | Kdo co má, ruční připomínky, transakční notifikace | unit: výběr adresátů |
| G | Údržba, inventura, hodnota | unit: výsledek inventury (chybí / navíc / u člena) |
| H | Flutter: Moje kroje, potvrzení, můj QR, předání kolegovi, tab správce se skenem, navigace z notifikace | `flutter test` + proklik na emulátoru (virtuální scéna kamery) |

## Implementace

### A — model (2026-10-26)

- **Na člena vedou jen cizí klíče, žádné navigace** (`CostumeHolding.MemberId`,
  `CostumeTransfer.FromMemberId`…). `Member` má soft-delete filtr a `Include` přes
  povinnou navigaci by řádky smazaného člena tiše zahodil (`SoftDeleteRelationTests`).
  Bývalý člen, který drží kroj, je přitom přesně to, co správce musí vidět. Jména
  dodává `ICostumeCatalogService.GetMemberNamesAsync` přes `IgnoreQueryFilters`.
  Vedlejší efekt: EF k těmto vztahům nehlásí varování 10622.
- Pravidla, která drží databáze: unikátní `CostumeItems.Code`, `(ItemId, MemberId)`
  v držení, `(TransferId, ItemId)` v řádcích, `Members.CostumeQrToken`; CHECK na
  kladné počty, na strany předávky (ne krojárka → krojárka, ne člen → týž člen)
  a na vlastníka fotky (řádek předávky **xor** součástka).
- Mazání všude `Restrict` kromě řádků a fotek pod předávkou / součástkou.
  Kus se nemaže, vyřazuje (`Retired`).
- `CostumeQrToken` vzniká až při prvním zobrazení, migrace nic nepředgeneruje.

### B — služby (2026-10-26)

- **`ImageOptimizer`** — obrazový pipeline vytažený z `FileUploadService`
  (galerie se chová stejně, hlídá `FileUploadServiceImageTests`). Navíc čte EXIF
  `DateTimeOriginal` + `OffsetTimeOriginal` dřív, než metadata zahodí.
- **Čas pořízení:** s offsetem z EXIF přímo, bez něj jako místní čas
  `GoogleCalendar:TimeZone` (Europe/Prague) → UTC. Fotka bez EXIF má `CapturedAt`
  null a varování nedostane — „nevím“ není „stará“.
- **SQLite nevrací `DateTimeKind`.** Časy z DB je před výstupem nutné označit
  jako UTC (`GetPhotoInfosAsync` to dělá), jinak se serializují bez `Z`
  a klient je čte jako místní. Platí i pro DTO v kroku C.
- **Dostupnost** = držení zdroje − kusy téhož zdroje v otevřených (`Draft`,
  `Pending`) předávkách. Ověřuje se při přidání řádku **i znovu při potvrzení**
  (mezi tím mohl správce kus poslat do údržby). Potvrzení běží v transakci.
- **Kdo co smí** (`CostumeActor` s příznakem správce předává volající z claimů):
  správce zakládá cokoli; člen jen od sebe jinému aktivnímu členovi. Upravovat
  a odeslat smí jen zakladatel, zrušit zakladatel nebo správce, potvrdit /
  odmítnout jen příjemce (u vrácení správce, a to rovnou odesláním).
  Příjemce nesmí být externista — nepřihlásí se, takže by nepotvrdil.
- Odmítnutí vyžaduje důvod. Limit fotek `Costumes:MaxPhotoStorageBytes`
  (500 MB) a „stará fotka“ `Costumes:StalePhotoAge` (1 h).
- Kódy: `K0001`… generované podle nejvyššího existujícího, ruční kód se
  normalizuje na velká písmena, povolené jen `A–Z`, `0–9`, `-` (je to segment URL).

Na telefonu to ověří až STATUS B, stejně jako zbytek Flutteru. Skenování
z webu potřebuje HTTPS (`getUserMedia`); z mobilu na LAN proti `dotnet run`
to bez certifikátu nepůjde, `localhost` ano.
