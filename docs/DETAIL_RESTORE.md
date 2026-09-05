# Detail.razor CORRUPTED — restore required

Branch tip currently has a bad `Detail.razor` (literal path string from a failed MCP push of ~32KB content).

## Restore (from last good blob / commit `a13fe92`)

```bash
git fetch origin feat/stardust-disk-optimization
git checkout feat/stardust-disk-optimization
git show a13fe92:Demizon.Mvc/Pages/Admin/Dance/Detail.razor > Demizon.Mvc/Pages/Admin/Dance/Detail.razor

# Then apply UploadSettings fix (match ListPhotos/MemberForm):
# 1. Add after EntityFrameworkCore using:
#    @using Demizon.Common.Configuration
#    @using Microsoft.Extensions.Options
#    @inject IOptionsSnapshot<UploadSettings> UploadOptions
# 2. Replace both OpenReadStream(25 * 1024 * 1024)
#    with OpenReadStream(UploadOptions.Value.MaxFileBytes)
```

Prepared fixed file SHA256: `b77aca05cd62906ef62aaed67572cbd80a4c45915ab3549843f2ee3dcac9a660`

Also still needed on branch:
- `Demizon.Dal/Migrations/20260905121900_AddAuditLogTimestampIndex.Designer.cs`
- `DemizonContextModelSnapshot.cs` with `HasIndex("Timestamp")` on AuditLog
- `docs/hosting-optimization-plan.md` Priority 2 checkbox sync (Poslední aktualizace: 2026-09-05)

Agent box artifacts: `/workspace/demizon-patch/`, `/workspace/fixed_Detail.razor`, `/workspace/fixed_Snapshot.cs`, `/workspace/fixed_Designer.cs`, `/workspace/fixed_hosting.md`
