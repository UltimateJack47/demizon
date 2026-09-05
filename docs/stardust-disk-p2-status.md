# Stardust Priority 2 — disk (status)

**Updated:** 2026-09-05 (PT)

## Done on `feat/stardust-disk-optimization`

1. **Audit whitelist** — `AuditSaveChangesInterceptor` skips `RefreshToken`, `SentNotification`, `DeviceToken`, `File`.
2. **Purge + WAL** — `DiskMaintenanceHostedService` (hourly): AuditLog 90d, revoked/expired RefreshTokens, SentNotifications 180d; `wal_checkpoint(TRUNCATE)` + `incremental_vacuum`.
3. **EF** — migration `20260905121900_AddAuditLogTimestampIndex`; `DemizonContext` has `HasIndex(Timestamp)`. Snapshot/Designer alignment may still need a local `dotnet ef` refresh.
4. **SQLite** — `auto_vacuum=INCREMENTAL` in `SqliteBusyTimeoutInterceptor` (one-time `VACUUM` still an ops note).
5. **Upload quotas** — `UploadSettings` MaxFileBytes 25MB, MaxTotalStorageBytes 2GB, MaxFileCount 2000; `StorageQuotaService` + gates in `FileService` / `FileUploadService`; UI: ListPhotos + MemberForm use `MaxFileBytes`.
6. **DatabaseController** — Admin role + try/finally for `/tmp` ZIP.
7. **Docker** — `.dockerignore` expanded; redundant `dotnet build` removed; `docker-entrypoint.sh` deleted.
8. **Tests** — `RefreshToken_se_neaudituje` expects empty audit.

## Leftover / follow-ups

- Dance `Detail.razor` may still hardcode `25 * 1024 * 1024` for `OpenReadStream` (ListPhotos/MemberForm fixed).
- EF `DemizonContextModelSnapshot` / Designer for Timestamp index — verify with local `dotnet ef`.
- One-time `VACUUM` after enabling incremental auto_vacuum.
- Circuit RSS measurement (Priority 2 non-disk leftover from plan).
- Run `dotnet test` locally (no clone/VM in this agent).

See also: `docs/hosting-optimization-plan.md` (full plan; checkbox sync may lag this status file).
