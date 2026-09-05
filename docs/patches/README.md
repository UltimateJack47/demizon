# Patches for feat/stardust-disk-optimization cleanup

**Detail.razor was deleted from tip** (corrupted by failed MCP ~32KB write). Restore Detail + EF + hosting docs:

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
b64 = ''.join(pathlib.Path(f'docs/patches/hosting-optimization-plan.md.gz.b64.part{i}').read_text().strip() for i in range(3))
pathlib.Path('docs/hosting-optimization-plan.md').write_bytes(gzip.decompress(base64.b64decode(b64)))
print('wrote hosting-optimization-plan.md')
PY
git add -A && git commit -m 'fix(disk): restore Detail MaxFileBytes + EF Timestamp index + P2 docs'
```
