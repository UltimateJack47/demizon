# Patches (gzip+base64) for feat/stardust-disk-optimization cleanup

**URGENT:** `Demizon.Mvc/Pages/Admin/Dance/Detail.razor` on this branch tip is corrupted (literal path string from a failed MCP push). Restore via the Detail patch below.

Decode and install from repo root:

```bash
python3 - <<'PY'
import gzip, base64, pathlib
files = {
  'docs/patches/Detail.razor.gz.b64': 'Demizon.Mvc/Pages/Admin/Dance/Detail.razor',
  'docs/patches/DemizonContextModelSnapshot.cs.gz.b64': 'Demizon.Dal/Migrations/DemizonContextModelSnapshot.cs',
  'docs/patches/20260905121900_AddAuditLogTimestampIndex.Designer.cs.gz.b64': 'Demizon.Dal/Migrations/20260905121900_AddAuditLogTimestampIndex.Designer.cs',
}
for src, dst in files.items():
    data = gzip.decompress(base64.b64decode(pathlib.Path(src).read_text().strip()))
    pathlib.Path(dst).parent.mkdir(parents=True, exist_ok=True)
    pathlib.Path(dst).write_bytes(data)
    print('wrote', dst, len(data))
PY
git add -A && git commit -m 'fix(disk): apply Detail MaxFileBytes + EF Timestamp index artifacts'
```

Also sync `docs/hosting-optimization-plan.md` Priority 2 checkboxes (date 2026-09-05) — see agent box `/workspace/fixed_hosting.md` or decode hosting patch if present.
