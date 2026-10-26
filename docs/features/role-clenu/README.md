# Role členů: víc oprávnění na jednoho člověka

> Založeno a hotové: 2026-10-26. Stav: ✅ **hotové v kódu**, testy zelené.
> Vzniklo jako krok 0 [krojárky](../krojarka/README.md), která potřebuje roli
> správce vedle admina.

## Zadání

- Člen měl jedinou `Member.Role` (`Standard` / `Admin`), takže nešlo být
  zároveň admin a správce krojárky.
- Nová role **správce obsahu webu** (`ContentManager`): spravuje to, co se
  ukazuje na veřejném webu, ale není admin.
- Vůdce souboru (`Leader`) a správce krojárky (`CostumeKeeper`) přibudou
  s krojárkou. Mimo krojárku vůdce nic navíc nedostává (rozhodnuto 2026-10-26).
- Aplikace ještě není v produkci, takže přestavba je teď levná.

## Jak to funguje dnes

**Model.** Tabulka `MemberRoles (Id, MemberId, Role)` s unikátním indexem
`(MemberId, Role)` a kaskádovým mazáním. `Role` je enum `UserRole` uložený jako
text. **Běžný člen nemá žádný řádek**, hodnota `Standard` zmizela.
`Member.HasRole(UserRole)` je pomocník nad načtenou kolekcí.

Surrogate `Id` je tam schválně: audit (`AuditSaveChangesInterceptor`) zapisuje
jen první sloupec klíče. Se složeným klíčem by šlo u odebrané role dohledat
člena, ale ne kterou roli ztratil.

**Co role nejsou.** `IsDancer`, `IsMusician` a `IsExternal` popisují, co člověk
v souboru dělá, a řídí docházku a veřejný seznam. Nejsou to oprávnění, proto
zůstávají sloupci na `Member`.

**Claimy.** JWT i cookie nesou **jeden `ClaimTypes.Role` claim za každou roli**.
Atributy `[Authorize(Roles = ...)]` a `AuthorizeView Roles=...` tedy fungují beze
změny. Řetězce rolí jsou v `RoleNames` (`Demizon.Mvc/Services/Authentication/RoleNames.cs`),
odvozené přes `nameof(UserRole.X)`.

**Kdo co smí:**

| Oblast | Člen | `ContentManager` | `Admin` |
|---|---|---|---|
| Docházka, akce, tance, profil (čtení, vlastní docházka) | ✔ | ✔ | ✔ |
| Tance: úpravy, dokumenty, videa u tance | — | ✔ | ✔ |
| Video odkazy (`/Admin/Videos`, `/api/videos` zápis) | — | ✔ | ✔ |
| Galerie (`/Admin/Photos`, `/api/gallery`) | — | ✔ | ✔ |
| Akce: úpravy, mazání, rušení, veřejnost, připomínky | — | — | ✔ |
| Zkoušky (rušení), docházka za ostatní | — | — | ✔ |
| Členové a jejich role, `/api/database` | — | — | ✔ |

Akce zůstaly jen adminovi, i když se ukazují na webu. Řídí totiž docházku,
Google Calendar a push notifikace, takže to není jen obsah. Kdyby je měl
`ContentManager` dostat, stačí změnit atributy v `EventsController` a stránkách
`Admin/Event` na `RoleNames.ContentEditors`.

Místa, kde bylo `Roles = "Admin, Standard"` (znamenalo „kdokoli přihlášený“),
jsou teď prosté `[Authorize]`. Jinak by člen bez role, nebo jen s rolí `Leader`,
z aplikace vypadl.

**Nastavení rolí.** Ve formuláři člena (`MemberForm.razor`) má admin sekci
„Oprávnění“ se zaškrtávátky podle `UserRole`. Ukládá je
`IMemberService.SetRolesAsync`:

- nastaví přesně zadaný seznam,
- **posledního admina nejde odebrat** — vrátí `Rejected` s hláškou
  „Soubor musí mít aspoň jednoho admina.“ Soft-smazaný admin se nepočítá,
- při úpravě se role ukládají **před** ostatními poli, aby odmítnutí nenechalo
  napůl uložený formulář. Nový člen dostane role rovnou v entitě (jeden `SaveChanges`).

**Kontrakt (breaking change).**

| Kde | Dřív | Teď |
|---|---|---|
| `TokenResponse` (`/api/auth/token`, `/api/auth/refresh`) | `role: "Admin"` / `"Standard"` | `roles: ["Admin"]` / `[]` |
| `MemberProfileDto` | `role: string` | `roles: string[]` |
| `POST /api/database/seed` odpověď | `role` | `roles` |

Flutter je přepnutý (`TokenStorage.roles`, `Authenticated.isAdmin`,
`rolesLabel` v profilu). Hodnoty v úložišti telefonu se po aktualizaci
přepíšou až dalším přihlášením nebo refreshem. Do té doby má appka prázdné
role, admin tedy dočasně nevidí admin akce. Aplikace v produkci není, proto se
to neřeší.

**MAUI se neudržuje** (rozhodnuto 2026-10-26). Novým kontraktem by se nezkompilovala
(`TokenStorage.cs:57` čte `response.Role`). Proto je vyřazená z `Demizon.slnx`,
složka zůstává jen jako historická reference. Rozdaná MAUI build by proti
novému backendu přihlášení nezvládla (`role` v odpovědi chybí).

**Změna role platí okamžitě (2026-10-26).** Role jsou zapečené v cookie (30 dní,
klouzavě) i v JWT. `MemberSessionValidator` proto porovná role v session s DB a
ověří, že člen existuje a není externista:

| Kde | Kdy se kontroluje | Když nesedí |
|---|---|---|
| Cookie (web) | `OnValidatePrincipal`, každý HTTP požadavek | odmítnutí + smazání cookie = **odhlášení** |
| Otevřený Blazor okruh | `MemberRevalidatingAuthenticationStateProvider`, každou minutu | okruh přejde na nepřihlášeného |
| JWT (mobil) | `JwtBearerEvents.OnTokenValidated`, každý požadavek | **401** → Flutter obnoví token a dostane aktuální role, uživatel nic nepozná |

Spouští se při jakékoli změně rolí, tedy odebrání i přidání. Navíc smazaný člen nebo
externista přijde o přístup hned, ne až s vypršením session. Cookie a tokeny vydané
před nasazením nesou roli `Standard`, takže se každý jednou odhlásí.
Stojí to jeden malý dotaz do SQLite na přihlášený požadavek.

Zvažované alternativy: zneplatnit jen refresh tokeny (řeší jen mobil, web by
držel 30 dní), sloupec „verze účtu“ na členovi (migrace navíc a neumí nic víc),
událost v paměti (nedosáhne na jiný prohlížeč ani telefon).

**Migrace `AddMemberRoles`.** Vytvoří tabulku, převede `Members.Role = 'Admin'`
na řádek `Admin` a sloupec zahodí. `Down` je ztrátový: starý sloupec unese jen
jednu roli, takže se zachová jen `Admin`.

## Známá omezení

- Otevřená stránka se odhlásí až při další kontrole (do minuty). Automaticky
  to testované není, E2E pokrývá jen cestu přes cookie (nový požadavek).
- `AuthenticationService.IssueToken` nemá v kódu volajícího (endpoint není
  namapovaný). Upravený je jen kvůli kompilaci, mrtvý kód zůstává.

## Testy

| Co | Kde |
|---|---|
| role → claimy (víc rolí, žádná role) | `TokenServiceTests` |
| `roles` v odpovědi tokenu pro člena i admina | `AuthApiTests` |
| `ContentManager` smí videa, nesmí zkoušky; člen bez role nesmí obsah, ale vidí běžné endpointy | `RoleAuthorizationApiTests` |
| token s odebranou / přidanou rolí a token smazaného člena → 401 | `RoleAuthorizationApiTests` |
| odebraná role ukončí webovou session (cookie zmizí) | `AuthFlowTests.Odebrani_role_ukonci_webovou_session` (E2E) |
| `SetRolesAsync`: přidání/odebrání, poslední admin, smazaný admin, neexistující člen | `MemberServiceTests` |
| migrace převede adminy a běžným členům roli nepřidá | `ModelAndMigrationsTests.Migrace_roli_prevede_adminy_a_beznym_clenum_roli_neprida` |
| menu podle role v prohlížeči (člen, správce obsahu) | `AuthFlowTests` (E2E) |
| `TokenResponse.roles`, `rolesLabel`, `isAdmin` | `demizon_flutter/test/contract_test.dart` |

Výsledek 2026-10-26: 201 unit + 190 integration + 59 E2E + 51 Flutter, vše zelené.
