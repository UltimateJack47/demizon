# Stardust — disk optimalizace (status branche)

**Branch:** `feat/stardust-disk-optimization`
**Updated:** 2026-09-08
**Stav:** připraveno k merge — `dotnet build Demizon.Backend.slnf -c Release` čistý,
231 testů zelených (76 unit + 155 integration).

## Priorita 2 — disk

1. **Audit whitelist** — `AuditSaveChangesInterceptor` přeskakuje `RefreshToken`,
   `SentNotification`, `DeviceToken`, `File` (~90 % objemu bez auditní hodnoty).
2. **Purge + WAL** — `DiskMaintenanceHostedService` (1×/hod): AuditLog 90 d,
   revokované/expirované RefreshTokeny, SentNotifications 180 d;
   `wal_checkpoint(TRUNCATE)` + `incremental_vacuum`.
3. **EF** — migrace `20260905121900_AddAuditLogTimestampIndex` (+ Designer);
   `DemizonContext` i snapshot mají `HasIndex(Timestamp)`.
4. **SQLite** — `auto_vacuum=INCREMENTAL` v `SqliteBusyTimeoutInterceptor`.
5. **Kvóty na uploady** — `UploadSettings`: MaxFileBytes 25 MB,
   MaxTotalStorageBytes 2 GB, MaxFileCount 2 000; `StorageQuotaService` + kontroly
   ve `FileService` / `FileUploadService`; UI (ListPhotos, MemberForm, Dance `Detail.razor`)
   čte `MaxFileBytes` z konfigurace místo zadrátované hodnoty.
6. **BLOBy mimo seznamy** — list/detail dotazy už neincludují `Files`, takže se
   nenačítají fotky do RAM; `GetOneAsync` je metadata-only, `GetContentAsync` tahá
   jeden sloupec.
7. **`GET /api/database/backup` odstraněn** — zálohy volume řeší Scaleway; endpoint
   tahal celou SQLite včetně fotek do RAM/tmp.

## Priorita 3 — co z ní bylo dotaženo v téže branchi

8. **Docker image 62 → 30 MB** — publish s `-r linux-x64 --self-contained false`
   (odpadl adresář `runtimes/` s 22 kopiemi `libe_sqlite3` pro cizí platformy).
   `PublishReadyToRun` záměrně nezapnuto, vrátil by 26 MB zpět — měření v plánu.
9. **`demizon.sqlite` odtrackován** a odstraněn z `CopyToOutputDirectory` — dev DB
   s hashi hesel se přestala vozit do image. Blob ale zůstává v git historii.
10. **Railway `DATABASE_URL`** parsování odstraněno z `Program.cs`.
11. **Mrtvý kód** — `UploadImageAsync` (filesystémový upload, nula volajících),
    `UploadSettings.Resize` / `ResizeSettings` / `ImagesDirectory` / `AllowedFileExtensions`,
    `AttendanceReminderBackgroundService`, `NotificationHostedService`, `docker-entrypoint.sh`.
12. **`.dockerignore`** rozšířen; duplicitní `dotnet build` z Dockerfile odstraněn.
13. **DataProtection klíče** persistované (`/data/keys` v produkci) — bez toho každý
    restart shodil auth cookies.
14. **Úklid branche** — `docs/patches/*.gz.b64` (havarijní gzip+base64 backupy z 5. 9.)
    smazány; HEAD ověřen buildem i testy, takže nejsou k čemu.

## Zbývá

**Vyžaduje běžící prostředí:**
- Naměřit RSS na jeden odpojený Blazor okruh a podle toho nastavit
  `DisconnectedCircuitMaxRetained` (dnes 10, odhad 1–3 MB/okruh je nepodložený).
- Jednorázový plný `VACUUM` na produkční DB po zapnutí `auto_vacuum=INCREMENTAL`
  (ops krok, potřebuje ~2× volného místa):
  `sqlite3 /data/demizon.sqlite "PRAGMA auto_vacuum=INCREMENTAL; VACUUM;"`
- ~~Docker image build neověřen~~ — **ověřeno 2026-09-08**: image 261 MB (z 509 MB),
  kontejner s `--memory=768m` vrací `/health` `Healthy` včetně database checku,
  homepage HTTP 200, `/data/keys` se plní, RSS v klidu 81 MB. Povinná proměnná
  `Jwt__SecretKey` doplněna do `docker run` receptu v plánu.

**Vyžaduje ruční rozhodnutí:**
- VAPID privátní klíč commitnutý v `appsettings.Production.json` — vygenerovat nové
  a předat přes secrets. Stejně jako u `demizon.sqlite` zůstává starý v git historii.
- Vizuální QA MudBlazor 9.9.0.
- Odložená rozhodnutí o nasazení (doména, HTTPS, OAuth redirect).

**Nedotčeno záměrně:**
- 7 ze 42 endpointů v `Demizon.Maui/Services/IApiClient.cs` — `Demizon.Maui` je
  nahrazován Flutter klientem a `.dockerignore` ho z image vylučuje.
- `AddDbContextFactory` a per-page render mode — architektonické změny, viz plán.

Detaily a zdůvodnění: `docs/hosting-optimization-plan.md`.
