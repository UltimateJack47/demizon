# Notifikační systém – Demizon

## Přehled

Notifikace jsou plně automatické. Odesílají se pomocí `UnifiedNotificationService` (background hosted service v `Demizon.Mvc`), který každou hodinu zkontroluje podmínky a odešle notifikace oběma kanály:

- **Web Push** (VAPID) – do prohlížeče na desktopu/mobilu
- **FCM** (Firebase Cloud Messaging) – nativní mobilní push přes MAUI app

Každý kanál je nezávislý. Povolení notifikací v prohlížeči nespojuje ani neruší mobilní notifikace a naopak.

---

## Typy notifikací

### 1. Nová akce (po zadání do systému)
- **Kdy**: 1 hodinu po vytvoření akce (`Event.CreatedAt + 1h`)
- **Komu**: všem členům
- **Obsah**: název akce + datum + místo (pokud uvedeno)
- **Deduplikace**: jeden záznam na akci (`SentNotification` s `MemberId = null`)

> Akce zadané před nasazením nové verze (s `CreatedAt = DateTime.MinValue`) jsou přeskočeny, aby nedošlo ke spamu.

### 2. Připomínky akce (milníkové)
Odesílají se pro každou **budoucí, nezrušenou** akci:

| Milník | Zpráva |
|--------|--------|
| 60 dní před akcí | „Připomínka akce za 2 měsíce" |
| 30 dní před akcí | „Připomínka akce za měsíc" |
| 14 dní před akcí | „Připomínka akce za 2 týdny" |

- **Komu**: pouze členům, kteří **nemají vyplněnou docházku** na danou akci (Attendance s `EventId = ev.Id`). Pokud se člen v 60d neozval, dostane notifikaci znovu v 30d i 14d. Jakmile si docházku vyplní, na další milník už ji nedostane.
- **Deduplikace**: jeden záznam na akci + milník + člen (`SentNotification` s konkrétním `MemberId`).
- **Legacy kompatibilita**: pokud pro daný milník existuje starý broadcast záznam (`MemberId = null`) z doby před per-member režimem, milník se přeskočí jako již doručený (nezasílá se znovu každému).

#### Inteligentní přeskakování milníků
Pokud je akce zadána pozdě (blíže než daný milník), starší milníky se přeskočí.
Příklad: akce zadána 20 dní před konáním → milník 60d i 30d se přeskočí, odešle se pouze 14d.

### 3. Manuální připomínka docházky (admin, akce)
- **Kdo spouští**: admin ručně přes tlačítko „🔔 Upozornit na docházku" v detailu akce v mobilní aplikaci
- **Endpoint**: `POST /api/events/{id}/notify-missing-attendance` (`Demizon.Mvc/Controllers/Api/EventsController.cs`), `[Authorize(Roles = "Admin")]`
- **Kdy**: jen pro budoucí, nezrušené akce
- **Komu**: členům, kteří **nemají vyplněnou docházku** na danou akci (stejný filtr jako automatické milníky)
- **Kanály**: FCM + Web Push paralelně
- **Deduplikace**: žádná – je to explicitní akce admina. Nezapisuje se do `SentNotification`, takže nezasahuje do milníkové logiky.
- **Odpověď**: `NotifyMissingAttendanceResponse { NotifiedCount, SkippedWithAttendance }` pro zobrazení v UI.

### 4. Připomínky zkoušky (nevyplněná docházka)
- **Kdy**: 5 dní, 3 dny a 1 den před každým **pátekem** (zkouška)
- **Výjimka**: pátek, na který připadá akce (`Event.DateFrom.Date == pátek`), není považován za zkoušku
- **Komu**: pouze členům, kteří **nemají vyplněnou docházku** na daný pátek
- **Obsah**: „Zkouška DD.MM.YYYY – nezapomeň vyplnit docházku!"
- **Deduplikace**: jeden záznam na člena + pátek + milník

---

## Technická architektura

### Soubory
| Soubor | Popis |
|--------|-------|
| `Demizon.Mvc/Services/Notification/UnifiedNotificationService.cs` | Hlavní background service (spouštění každou hodinu) |
| `Demizon.Dal/Entities/SentNotification.cs` | Entita pro sledování odeslaných notifikací (deduplication) |
| `Demizon.Dal/Entities/Event.cs` | Pole `CreatedAt` pro detekci nových akcí |
| `Demizon.Mvc/Services/FcmService.cs` | FCM odesílání (Firebase) |
| `Demizon.Mvc/Services/Notification/NotificationHostedService.cs` | **Starý service** – neregistrován, ponechán pro referenci |
| `Demizon.Mvc/Services/AttendanceReminderBackgroundService.cs` | **Starý service** – neregistrován, ponechán pro referenci |

### SentNotification entita
```
SentNotification
├── Id (PK)
├── NotificationType (enum)
├── EventId? (FK → Event) – pro akce
├── MemberId? (FK → Member) – pro individuální (zkoušky), null = broadcast
├── RehearsalDate? (DateTime) – pro zkoušky
└── SentAt (DateTime)
```

### NotificationType enum
| Hodnota | Popis |
|---------|-------|
| `NewEvent` | Notifikace o nové akci |
| `EventReminder60Days` | Připomínka 60 dní předem |
| `EventReminder30Days` | Připomínka 30 dní předem |
| `EventReminder14Days` | Připomínka 14 dní předem |
| `RehearsalReminder5Days` | Připomínka nevyplněné docházky 5 dní předem |
| `RehearsalReminder3Days` | Připomínka nevyplněné docházky 3 dny předem |
| `RehearsalReminder1Day` | Připomínka nevyplněné docházky 1 den předem |

---

## Konfigurace

### Web Push (VAPID)
Nastavení v `appsettings.json` pod klíčem `Vapid`:
```json
{
  "Vapid": {
    "Subject": "mailto:...",
    "PublicKey": "...",
    "PrivateKey": "..."
  }
}
```

### FCM
Firebase konfiguraci zajišťuje `FcmService` – podporuje jak environment variable (`FIREBASE_CONFIG`), tak soubor `firebase-config.json`.

### Perioda
Service se spouští každou hodinu (`PeriodicTimer(TimeSpan.FromHours(1))`).
Po startu aplikace proběhne první kontrola s 30sekundovým zpožděním.

---

## Web Push vs. FCM – nezávislost

Push notifikace jsou dva zcela samostatné systémy:
- **Web Push**: subscription je uložena v `PushSubscriptions` tabulce (koncový bod prohlížeče)
- **FCM**: device token je uložen v `DeviceTokens` tabulce (zařízení s MAUI appkou)

Uživatel může mít aktivní oba, jen jeden, nebo žádný. Nemají vliv na sebe navzájem.
Admin může v profilu vidět stav web push subscriptions. Mobilní device tokeny spravuje MAUI app automaticky při přihlášení.

---

## Pole `NotifyBeforeDays` na entitě Event

Toto pole existuje v DB schématu z původní implementace (kde se notifikace nastavovaly manuálně per-akci), ale **nový systém ho ignoruje**. Nezobrazuje se ve formulářích ani se nenastavuje při vytváření/úpravě akcí. Může být v budoucnu odstraněno migrací.
