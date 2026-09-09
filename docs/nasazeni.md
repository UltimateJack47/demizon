# Nasazení na Scaleway Stardust

> **Živý dokument.** Poslední aktualizace: 2026-09-09.
>
> Runbook, ne záznam práce: co je potřeba udělat a jak, až aplikace poleze na
> veřejnou IP. Proč je server nastavený takhle, vysvětluje
> [`features/disk-optimalizace/`](features/disk-optimalizace/README.md).
>
> **Aplikace zatím není v produkci.** Stav a priority: [`STATUS.md`](STATUS.md).
> Flutter na telefonu: [`features/flutter-prepis/`](features/flutter-prepis/README.md).

## Až budeš nasazovat — pořadí

Nic z toho nehoří, dokud není doména. Až bude, jdi shora dolů. Recepty
jsou v sekcích pod tím.

1. [ ] **Doména.** Dnes `AllowedHosts` v `appsettings.Production.json` i
       `GoogleCalendar.RedirectUri` míří na Railway — na Scalewayu by každý
       request skončil HTTP 400. (Základní `appsettings.json` už `demizon.cz`
       obsahuje.) DNS A/AAAA na IP Stardustu.
2. [ ] **HTTPS přes Caddy** (Let's Encrypt). Kestrel jen HTTP na localhost.
       Ne `dotnet dev-certs`.
3. [ ] **Google OAuth redirect URI** 1:1 s Google Cloud Console
       (`https://<domena>/google/callback`).
4. [ ] **Tajemství** mimo repo, předat jako `-e` při `docker run`:
       `Jwt__SecretKey`, `Vapid__*`, `FIREBASE_CREDENTIAL_JSON`,
       jednorázově `Bootstrap__SeedToken`.
5. [ ] **První `docker run`** (volume, `--memory=768m`, log-opt) + seed
       prvního admina + **restart bez seed tokenu**.
6. [ ] **Jednorázový `VACUUM`** na produkční SQLite (chce ~2× volného místa).
7. [ ] **Cron** na `ops/backup-demizon.sh` a **vyzkoušet obnovu**, ne jen záloh.
8. [ ] **`/etc/docker/daemon.json`** s rotací logů.
9. [ ] **Registry + CI:** `build.yml` (`latest` + `sha-<commit>`), `deploy.yml`
       (`workflow_dispatch` / release, SSH `docker pull` + restart + prune).
       Zůstat u Docker Hubu (`jackeq/demizon-mvc`) nebo GHCR.

Dokud bod 1 není, body 2–9 nedělej. Flutter (`flutterfire`, ikony, telefon)
je jiná větev — viz flutter-prepis, sekce *Až budeš mít Firebase a telefon*.

---

## Doména, HTTPS, OAuth

- [ ] **Doména a HTTPS.** Dnes `appsettings.Production.json:8` má
      `AllowedHosts: "demizon-production.up.railway.app;localhost"` — na Scalewayu by
      **každý request skončil HTTP 400**, protože hlavička `Host` v seznamu není.
      Stejně tak `GoogleCalendar.RedirectUri` (`:13`) míří na Railway.
      Při `docker run` přebij `AllowedHosts` a `GoogleCalendar__RedirectUri`
      na ostrou doménu (env overlay v `Program.cs` sedí až za json soubory).
- [ ] **HTTPS řešit reverzní proxy**, ne `dotnet dev-certs` (dev certifikát prohlížeč
      na veřejné doméně odmítne). Caddy stačí takto:
      ```
      demizon.cz {
          reverse_proxy 127.0.0.1:8083
      }
      ```
      Let's Encrypt si vyřídí sám. Kestrel pak jede prostý HTTP na localhostu.
- [ ] **Google OAuth redirect URI** musí přesně odpovídat tomu, co je zaregistrované
      v Google Cloud Console.

### Správný `docker run` (až se bude nasazovat)

```bash
docker volume create demizon-data      # jednou

docker pull <image>:latest
docker stop demizon 2>/dev/null; docker rm demizon 2>/dev/null
docker run -d --name demizon --restart unless-stopped \
  -p 127.0.0.1:8083:8080 \
  -v demizon-data:/data \
  -e ASPNETCORE_URLS="http://+:8080" \
  -e AllowedHosts="<domena>" \
  -e Jwt__SecretKey="<nahodny retezec, min. 32 znaku>" \
  -e GoogleCalendar__RedirectUri="https://<domena>/google/callback" \
  -e Vapid__PublicKey="<public>" \
  -e Vapid__PrivateKey="<private>" \
  -e Vapid__Subject="mailto:info@demizon.cz" \
  -e FIREBASE_CREDENTIAL_JSON='<json>' \
  --memory=768m --memory-swap=768m \
  --log-opt max-size=10m --log-opt max-file=3 \
  <image>:latest
docker image prune -f
```

Na co si dát pozor:
- **Přepínače patří PŘED jméno image.** V `docker run [OPTIONS] IMAGE [COMMAND]` je
  všechno za jménem image příkaz pro kontejner. Původní zápis
  `docker run -p 8083:8080 image -e ASPNETCORE_URLS=...` proměnnou nikdy nenastavil.
- **`-p 127.0.0.1:8083:8080`**, ne `-p 8083:8080` — Docker si píše vlastní pravidla
  do iptables a obchází tím UFW; bez prefixu je port otevřený do internetu.
- **`--memory=768m`** je lepší páka než `DOTNET_GCHeapHardLimit` — .NET čte cgroup limit
  a sám si nastaví heap hard limit na ~75 % z něj.
- **`Jwt__SecretKey` je povinná, jinak kontejner nenastartuje.** Ověřeno smoke testem:
  bez ní `ValidateOnStart` shodí start s `OptionsValidationException: DataAnnotation
  validation failed for 'JwtSettings' members: 'SecretKey'`. Sekce `Jwt` v
  `appsettings.json` **je**, ale drží jen `Issuer`, `Audience` a `ExpirationMinutes` —
  `SecretKey` v ní schválně chybí, protože je to tajemství. Recept ji tedy musí předat.
  Dvojité podtržítko je oddělovač sekcí — `Jwt__SecretKey` = `Jwt:SecretKey`.
  `Issuer` a `Audience` mají v `JwtSettings` výchozí hodnoty, takže je předávat netřeba;
  s jedinou `Jwt__SecretKey` naběhne appka do `Healthy`. Firebase je volitelná (bez ní
  jen varování a vypnuté FCM push). `Vapid__*` a `GoogleCalendar__RedirectUri` jsou
  povinné, ale dnes jsou v `appsettings.Production.json` — až se VAPID klíče přesunou
  do secrets (Priorita 3), přidají se sem taky.

Na hostiteli ještě `/etc/docker/daemon.json`:
```json
{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
```

### Jak vznikne první admin

Databáze se migruje při startu, ale **žádného člena neseeduje** — jediné seed data
v modelu je jeden `Setting`. Bez prvního admina se do administrace nikdo nepřihlásí,
takže bootstrap je součást nasazení, ne něco, co se vyřeší samo.

`POST /api/database/seed` je na to jediná podporovaná cesta a je zamčená třemi
pojistkami (viz `DatabaseController.SeedAdmin`):

1. **Bez `Bootstrap__SeedToken` endpoint vrací 404.** Nenastavený token = feature
   vypnutá. Tohle je cílový stav běžící instance.
2. **Špatná hlavička `X-Seed-Token` vrací 401.** Porovnává se v konstantním čase.
3. **Jakýkoli existující člen — včetně soft-smazaného — vrací 409.** Endpoint se tím
   po prvním úspěchu sám vypne.

Postup:

```bash
# 1) jednorázově dokládat token při startu kontejneru
TOKEN=$(openssl rand -hex 32)
docker run -d --name demizon ... -e Bootstrap__SeedToken="$TOKEN" <image>:latest

# 2) založit admina; login i heslo si volíš (heslo min. 12 znaků)
curl -sS -X POST https://<domena>/api/database/seed   -H "Content-Type: application/json"   -H "X-Seed-Token: $TOKEN"   -d '{"login":"jack","password":"<silne-heslo>","name":"Jméno","surname":"Příjmení","email":"ty@demizon.cz"}'
# -> {"id":1,"login":"jack","role":"Admin"}

# 3) restartovat kontejner BEZ Bootstrap__SeedToken
docker stop demizon && docker rm demizon
docker run -d --name demizon ... <image>:latest   # bez -e Bootstrap__SeedToken
```

Heslo se v odpovědi nevrací a v kódu není žádné výchozí — zná ho jen ten, kdo request
poslal. Krok 3 není nutný pro bezpečnost (409 stačí), ale je to hygiena: token, který
už nemá co dělat, nemá v prostředí kontejneru co zůstávat.

> Pro lokální vývoj patří `Bootstrap:SeedToken` do `appsettings.Local.json`
> (je gitignorovaný), ne do `appsettings.json`.

---

## Záloha `/data`

Fotky jsou BLOBy uvnitř SQLite, takže **ten jeden soubor je celý obsah aplikace**.
`GET /api/database/backup` je pryč (tahal celou databázi do RAM), takže zálohu musí
dělat hostitel. „Řeší to infrastruktura“ není plán — konkrétní job je
[`ops/backup-demizon.sh`](../ops/backup-demizon.sh).

**Proč ne `cp`.** Databáze jede v režimu WAL, takže potvrzené transakce mohou být
ještě jen v `demizon.sqlite-wal`, ne v hlavním souboru. Prostá kopie dá nekonzistentní
snapshot. Skript proto používá `sqlite3 ".backup"`, což je online backup API: projde
za běhu appky a kontejner se nemusí zastavovat. `sqlite3` musí být na hostiteli
(`apt install sqlite3`) — v runtime image není.

**Co skript dělá:**

1. `.backup` do `$DEMIZON_BACKUP_DIR/demizon-<UTC stamp>.sqlite`
2. `PRAGMA integrity_check` **nad kopií** — záloha, kterou nejde otevřít, není záloha
   a chce se to zjistit teď, ne při obnově. Při chybě kopii smaže a skončí nenulově.
3. `gzip -9`
4. `keys/` (DataProtection) do samostatného `tar.gz` — bez nich se po obnově odhlásí
   všichni přihlášení
5. smaže lokální kopie starší než `DEMIZON_BACKUP_KEEP_DAYS` (default **3**)
6. `rclone copy` na `$DEMIZON_BACKUP_REMOTE`, pokud je nastavené; jinak vypíše varování

> **Proč jen 3 dny lokálně:** disk má 10 GB a `MaxTotalStorageBytes` připouští 2 GB dat.
> Čtrnáct lokálních kopií se tam nevejde. Delší historie patří mimo stroj — jeden mrtvý
> disk jinak znamená konec dat a lokální záloha je pak jen ochrana proti překlepu,
> ne proti hardwaru.

**Cron na hostiteli:**

```cron
15 3 * * * DEMIZON_BACKUP_REMOTE=scaleway-s3:demizon-backup /usr/local/bin/backup-demizon.sh >> /var/log/demizon-backup.log 2>&1
```

**Obnova:**

```bash
docker stop demizon
DATA=/var/lib/docker/volumes/demizon-data/_data
gunzip -c demizon-<stamp>.sqlite.gz > "$DATA/demizon.sqlite"
# KRITICKÉ: starý WAL/SHM musí zmizet, jinak nad obnovenou databází přehraje
# zastaralý žurnál a snapshot se tím rozbije.
rm -f "$DATA/demizon.sqlite-wal" "$DATA/demizon.sqlite-shm"
tar -xzf demizon-keys-<stamp>.tar.gz -C "$DATA"
docker start demizon
curl -sf http://127.0.0.1:8083/health
```

**Ověřeno (2026-09-09)** v Alpine kontejneru proti databázi v režimu WAL s BLOBy:
záloha proběhla, `integrity_check` vrátil `ok`, obnovená kopie měla všechny 3 řádky
a 450 kB dat, `keys/` se rozbalily. Varování o chybějícím off-site cíli se vypíše.

---

## Plán CI/CD (GitHub Actions)

Repozitář: `github.com/UltimateJack47/demizon`.

**Cena:** veřejné repo = neomezené minuty zdarma. Privátní repo na plánu Free =
**2 000 Linux minut měsíčně**. Build .NET image trvá ~3–5 min, takže i privátně
vychází ~400 buildů měsíčně zdarma. Pro tenhle projekt bohatě stačí.

**Navržený tvar:**

1. **`test.yml`** — ✅ založeno. Trigger `push`/`pull_request` na `master`,
   `dotnet test Demizon.Backend.slnf` (251 testů). **`Demizon.slnx` v CI stavět nelze**,
   `Demizon.Maui` vyžaduje workload `maui-android` — proto solution filter.
2. **`build.yml`** — ještě ne. Po testech Docker image do registry se dvěma tagy:
   `latest` a `sha-<commit>`. Čeká na rozhodnutí o registry.
3. **`deploy.yml`** — trigger `workflow_dispatch` (ruční spuštění) nebo `release`.
   Přes SSH na server udělá `docker pull` + restart kontejneru + `docker image prune -f`.

Testy běží na každý push. Image a nasazení až po rozhodnutí o registry / doméně.

**Registry:** dnes se používá Docker Hub (`jackeq/demizon-mvc`). Free plán tam dává
1 privátní repozitář, což stačí. Alternativa GHCR (`ghcr.io`) je těsněji integrovaná
s Actions (autentizace přes `GITHUB_TOKEN`, není potřeba spravovat heslo), ale u privátních
balíčků platí kvóta GitHub Packages (500 MB storage na Free plánu) — image má dnes ~60 MB,
takže by se vešlo jen pár tagů. Doporučení: **zůstat u Docker Hubu**, nebo na GHCR mazat
staré tagy.

**Tajemství** (`Settings → Secrets and variables → Actions`): přihlašovací údaje do registry,
SSH klíč na server, VAPID klíče, Firebase service account, Google OAuth. Do image nepatří
nic z toho — všechno se předá jako `-e` při `docker run`.

⚠️ `test.yml` běží. `build.yml` / `deploy.yml` čekají na rozhodnutí o doméně a registry.

---

## VAPID klíče (web push)

Hodnoty v `appsettings.Production.json` jsou slepené GUIDy, ne platné P-256
klíče — web push proto **zatím nikdy nefungoval**. Historii kvůli nim
není třeba přepisovat. Vygenerovat **až při nasazení**, mimo repo:

```bash
npx web-push generate-vapid-keys
```

Předat kontejneru (a vymazat je z `appsettings.Production.json`):

```
-e Vapid__PublicKey="<public>"
-e Vapid__PrivateKey="<private>"
-e Vapid__Subject="mailto:info@demizon.cz"
```

Kdo má privátní klíč, může posílat push odběratelům. Rotace později odhlásí
všechny prohlížeče — tenhle pár vznikne jednou.

---

## Firebase v kontejneru (FCM)

Bez credentials `FcmService` jen zaloguje warning a mlčí. Inicializace čte
nejdřív env, pak soubor (`FcmService.Initialize`):

```
-e FIREBASE_CREDENTIAL_JSON='<celý JSON service account>'
```

Fallback: `Firebase:CredentialFile` na cestu uvnitř kontejneru. Do image
soubor nepatří — buď env, nebo volume. Stejný Firebase projekt, který
později použije `flutterfire configure` na telefonu.

---

## Jednorázový `VACUUM`

`auto_vacuum=INCREMENTAL` na už existující databázi (vznikla s `NONE`)
**nic neudělá**, dokud soubor jednou nepřepíšeš. Periodický
`incremental_vacuum` v `DiskMaintenanceService` už běží, ale uvolní místo
až po tomhle kroku. Chce ~2× velikosti DB volného místa. Appku zastav:

```bash
docker stop demizon
DATA=/var/lib/docker/volumes/demizon-data/_data
sqlite3 "$DATA/demizon.sqlite" "PRAGMA auto_vacuum=INCREMENTAL; VACUUM;"
docker start demizon
```

Nová prázdná DB z migrace tohle nepotřebuje — interceptor nastaví
`INCREMENTAL` před vznikem tabulek. Tenhle krok je jen pro soubor, který
už data má.
