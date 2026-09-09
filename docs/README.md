# Rozcestník dokumentace

> Poslední aktualizace: 2026-09-09.

## Kde začít

| Chci… | Otevři |
|---|---|
| **vědět, co se dělá dál** | [`STATUS.md`](STATUS.md) — stav projektu a jediný dopředný plán |
| pochopit architekturu a kontrakty | [`../AGENTS.md`](../AGENTS.md) |
| psát nebo spouštět testy | [`testing-plan.md`](testing-plan.md) |
| nasadit to na server | [`nasazeni.md`](nasazeni.md) — checklist od domény po CI |
| dělat na mobilní appce | [`features/flutter-prepis/`](features/flutter-prepis/README.md) |
| rozumět notifikacím | [`notifications.md`](notifications.md) |
| vědět, **proč** je něco jak je | složka té feature ve `features/` |

Když si nejsi jistý, jdi do `STATUS.md`.

---

## Struktura

```
docs/
  README.md          tenhle rozcestník
  STATUS.md          stav + co dál          <- vstupní bod
  testing-plan.md    konvence testů         (napříč featurami)
  nasazeni.md        runbook nasazení       (napříč featurami)
  notifications.md   kanály notifikací      (napříč featurami)
  features/
    <název>/
      README.md      zadání, stav, co se udělalo   <- povinné
      pruzkum.md     analýza, když je samostatná
      review.md      nálezy code review, když je jich dost
    archiv/
      <název>/       feature, jejíž stav už neplatí
```

**Průřezové dokumenty zůstávají v korenu.** Konvence testů, runbook nasazení
a notifikační kanály nejsou featury — platí přes všechny. Kdyby se rozsypaly po
složkách, příští test se napíše podle toho, kam kdo zabloudí.

**V každé feature je README povinné, ostatní soubory teprve když si to velikost
vyžádá.** Pevná šablona pěti souborů vyrobí jednořádkové soubory a prázdný
soubor vypadá jako chybějící práce — což je horší než žádný.

---

## Živé featury

| Feature | Stav |
|---|---|
| [`features/flutter-prepis/`](features/flutter-prepis/README.md) | 🔨 probíhá — kód bez telefonu je hotový; zbývá Firebase + fyzické zařízení (**odloženo**). |

## Uzavřené featury

Záznamy hotové práce — zdůvodnění, naměřená čísla, poučení. **Nejsou to úkoly.**

| Feature | Co v ní je |
|---|---|
| [`features/disk-optimalizace/`](features/disk-optimalizace/README.md) | Hostování na 1 vCPU / 1 GB / 10 GB. ImageSharp místo Magick.NET, kvóty, purge, WAL, RID publish. Samostatný [`pruzkum.md`](features/disk-optimalizace/pruzkum.md) říká, proč disk i paměť docházely. |
| [`features/kvalita-result-testy/`](features/kvalita-result-testy/README.md) | `Result` místo `bool` napříč službami, E2E sada v Playwrightu, vizuální QA MudBlazoru. Nálezy dvou kol review v [`review.md`](features/kvalita-result-testy/review.md). |

## Archiv

Stav, který už neplatí, nebo plány, které byly nahrazené. Nechává se kvůli
kontextu — proč něco vypadá, jak vypadá. Cesty na `Demizon.Api/` uvnitř
jsou historické: ten host se sjednotil do `Demizon.Mvc` (`71d9916`)
a v solution už není.

| Složka | Poznámka |
|---|---|
| `features/archiv/maui-klient/` | MAUI klient, kterého nahrazuje Flutter. **Zdroj pravdy o chování, které se má přepsat** — hlavně notifikace a navigace. |
| [`features/archiv/notifikace-navrh/`](features/archiv/notifikace-navrh/README.md) | Původní návrh notifikací; skutečný stav je v [`notifications.md`](notifications.md) |
| [`features/archiv/code-review-starsi/`](features/archiv/code-review-starsi/README.md) | Starší kola review; část nálezů je bezpředmětná (soubory smazané) |
| `features/archiv/dochazka/` | Zavedení stavu „nevím“ a návrh stavového pruhu |
| [`features/archiv/souhrn-etapy/`](features/archiv/souhrn-etapy/README.md) | Souhrn dřívější etapy |

---

## Konvence

- **Živý dokument nese v hlavičce datum poslední aktualizace** a odškrtává
  položky, jak se dokončují. Uzavřená feature má v hlavičce, že je uzavřená.
- **Zapisuje se i to, co se rozhodlo neudělat, a proč.** Bez toho příští
  session „opraví“ něco, co je záměr — a naopak. Půlka hodnoty těch dokumentů
  je právě v tomhle.
- **Naměřená čísla patří dovnitř**, ne do commit message: velikosti image,
  RSS, počty testů. Commit se hledá špatně, dokument se čte.
- **Stav napříč vším je jen v `STATUS.md`.** Feature složka odpovídá na „co se
  stalo v X“, ne na „co teď“. Nezakládat druhý plán.
- **Když feature přestane platit, přesune se do `archiv/`** — nemaže se.
