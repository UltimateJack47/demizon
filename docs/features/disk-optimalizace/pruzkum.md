# Průzkum: proč docházel disk a paměť

> ✅ **Uzavřená analýza** z 2026-09-01, na které stojí celá disková optimalizace.
> Co se z ní udělalo: [`README.md`](README.md). Jak se to nasazuje:
> [`../../nasazeni.md`](../../nasazeni.md).

Předchozí pokus o nasazení skončil zaplněním disku a kolapsem serveru. Tenhle
dokument pojmenovává příčiny — bez nich by se optimalizace dělala podle dojmu.

## Diagnóza

### Proč docházel disk

Build na serveru to nebyl — na server jde jen `docker pull`. Skutečné příčiny:

| # | Příčina | Důkaz |
|---|---|---|
| 1 | `docker run` **bez `-v` pro `/data`** → SQLite DB včetně všech fotek se psala do zapisovatelné vrstvy kontejneru | `Program.cs:34-35` si adresář vytvoří sám (`Directory.CreateDirectory`), takže probe projde a appka pokračuje |
| 2 | `docker run` **bez `--rm`** → zastavené kontejnery se hromadí, každý drží svou vrstvu v `/var/lib/docker/overlay2` | poznámky k nasazení |
| 3 | Opakovaný `docker pull :latest` → staré image zůstávají jako dangling, ~284 MB každý | nikdy se nespouštěl `docker image prune` |
| 4 | `docker logs` bez rotace — výchozí `json-file` driver nemá limit | na hostiteli chybí `/etc/docker/daemon.json` |
| 5 | SQLite nikdy neuvolní smazaná data — v repu **nula** výskytů `VACUUM`/`auto_vacuum` | `Demizon.Dal` |
| 6 | `AuditLog` bez retence, ~200–300 MB/rok (hlavně z refresh tokenů, JWT expirace 60 min) | `AuditSaveChangesInterceptor.cs:21-71`, žádný purge |
| 7 | WAL se nikdy nezkrátí — chybí `journal_size_limit`; navíc `AddDbContext` je v Blazor Serveru scoped na **celý okruh**, takže checkpoint nemůže doběhnout | `DatabaseServiceConfigurationExtension.cs:33,59` |

**Vedlejší efekt bodů 1+2: tichá ztráta dat.** Každý nový kontejner startoval s prázdnou DB.

### Proč docházela paměť

| # | Riziko | Stav |
|---|---|---|
| 1 | **Magick.NET Q16** — 8 B/px, alokace mimo GC haldu, žádné `ResourceLimits` | ✅ **vyřešeno** (viz níže) |
| 2 | Blazor Server circuity bez konfigurace — `ServerPrerendered` dává circuit **i anonymnímu návštěvníkovi**, default `DisconnectedCircuitMaxRetained = 100` × 3 min | ⚠️ **částečně** (retence snížena, viz ✅ 5; per-page render mode zablokovaný) |
| 3 | Server GC zapnutý defaultně, bez heap limitu — nevrací paměť OS | ✅ **vyřešeno** (Workstation GC, viz ✅ 5) |
| 4 | BLOBy v SQLite se načítají celé do paměti, bez streamování | ✅ **vyřešeno** (seznamy jen metadata; `GetContentAsync` tahá jeden sloupec) |

---
