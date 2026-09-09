# Rozcestník dokumentace

> Poslední aktualizace: 2026-09-09.
>
> V `docs/` je **šest živých dokumentů**; zbytek je záznam hotové práce
> v `waves/` a `archive/`. Tenhle soubor říká, do kterého se dívat.
>
> Bylo jich osmnáct v jedné rovině — restrukturalizováno 2026-09-09, protože
> se v tom nedalo poznat, co ještě platí.

## Kde začít

| Chci… | Otevři |
|---|---|
| **vědět, co se dělá dál** | [`next-wave-plan.md`](next-wave-plan.md) — **jediný dopředný plán** |
| pochopit architekturu a kontrakty | [`../AGENTS.md`](../AGENTS.md) |
| psát nebo spouštět testy | [`testing-plan.md`](testing-plan.md) |
| nasadit to na server | [`hosting-optimization-plan.md`](hosting-optimization-plan.md) |
| dělat na mobilní appce | [`flutter-rewrite-plan.md`](flutter-rewrite-plan.md) |
| rozumět notifikacím | [`notifications.md`](notifications.md) |

Když si nejsi jistý, jdi do `next-wave-plan.md`. Odkazuje na všechno ostatní
a drží stav.

---

## Živé dokumenty

Průběžně se aktualizují, platí teď.

| Dokument | Co v něm je |
|---|---|
| [`next-wave-plan.md`](next-wave-plan.md) | **Co je hotové, co zbývá a v jakém pořadí.** Vstupní bod pro každou další session. |
| [`testing-plan.md`](testing-plan.md) | Struktura testů, co která sada hlídá, konvence, chyby, které testy našly, a co ještě pokryté není. |
| [`hosting-optimization-plan.md`](hosting-optimization-plan.md) | Diagnóza disku a RAM na Scaleway Stardust, priority 1–3, `docker run` recept, bootstrap prvního admina, záloha `/data`, odložená rozhodnutí o nasazení. |
| [`flutter-rewrite-plan.md`](flutter-rewrite-plan.md) | Přepis mobilního klienta z MAUI do Flutteru. |
| [`notifications.md`](notifications.md) | Milníky notifikací, kanály FCM a Web Push, které service jsou registrované. |

## Záznamy uzavřených vln

Popisují, co a proč se udělalo. Nejsou to úkoly — **neber je jako TODO**.
Cenné jsou kvůli zdůvodněním a naměřeným číslům.

| Dokument | Vlna |
|---|---|
| [`waves/2026-09-quality.md`](waves/2026-09-quality.md) | Result refactor, E2E sada, vizuální QA, code review (2026-09-09) |

Disková vlna vlastní záznam nemá — její stav i naměřená čísla jsou přímo
v [`hosting-optimization-plan.md`](hosting-optimization-plan.md) (samostatný
`stardust-disk-p2-status.md` byl 2026-09-09 sloučen tam, byl to duplikát).

## Historie

Záznamy z dřívějších etap. Popisují stav, který už neplatí, nebo plány, které
byly nahrazené. Nechávají se kvůli kontextu — proč něco vypadá, jak vypadá.

| Dokument | Poznámka |
|---|---|
| [`archive/implementation-plan.md`](archive/implementation-plan.md) | Původní návrh notifikací; skutečný stav je v `notifications.md` |
| [`archive/review-action-plan.md`](archive/review-action-plan.md) | Starší kola code review; část nálezů je bezpředmětná (soubory smazané) |
| [`archive/IMPLEMENTATION_SUMMARY.md`](archive/IMPLEMENTATION_SUMMARY.md) | Souhrn dřívější etapy |
| [`archive/ATTENDANCE_MAYBE_STATUS_PLAN.md`](archive/ATTENDANCE_MAYBE_STATUS_PLAN.md) | Zavedení stavu „nevím“ do docházky |
| [`archive/plan-attendance-statusbar-attendees.md`](archive/plan-attendance-statusbar-attendees.md) | Návrh stavového pruhu docházky |
| [`archive/maui/mobile-fixes-plan.md`](archive/maui/mobile-fixes-plan.md), [`archive/maui/maui-api-plan.md`](archive/maui/maui-api-plan.md), [`archive/maui/MAUI_Enhancement_Plan.md`](archive/maui/MAUI_Enhancement_Plan.md), [`archive/maui/MAUI_Fixes_Plan.md`](archive/maui/MAUI_Fixes_Plan.md), [`archive/maui/MAUI_Routing_Guide.md`](archive/maui/MAUI_Routing_Guide.md), [`archive/maui/MAUI_Technical_Specification.md`](archive/maui/MAUI_Technical_Specification.md) | MAUI klient, kterého nahrazuje Flutter. Zdroj pravdy o chování, které se má přepsat — hlavně u notifikací a navigace. |

---

## Konvence pro tyhle dokumenty

- **Živý dokument nese v hlavičce datum poslední aktualizace** a odškrtává
  položky, jak se dokončují. Uzavřená vlna má v hlavičce, že je uzavřená.
- **Zapisuje se i to, co se rozhodlo neudělat, a proč.** Bez toho příští
  session „opraví“ něco, co je záměr — a naopak.
- **Naměřená čísla patří dovnitř**, ne do commit message: velikosti image,
  RSS, počty testů. Commit se hledá špatně, dokument se čte.
- **Nezakládat nový plán na každou vlnu.** Osmnáct souborů v jedné rovině bylo
  varování: stav patří do `next-wave-plan.md`, samostatný soubor v `waves/` si
  zaslouží jen vlna, která má vlastní postup a poučení k zapsání.
- **Struktura:** živé dokumenty v `docs/`, uzavřené vlny v `docs/waves/`,
  historie v `docs/archive/`. Když dokument přestane platit, přesune se do
  `archive/` — nemaže se, kontext „proč to tak je“ je cenný.
