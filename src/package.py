"""Create a reproducible installation payload. Never package user data or auth."""
from pathlib import Path
import hashlib
import zipfile

root = Path(__file__).resolve().parents[1]
app = root / 'build' / 'app'
files = [app / name for name in ('海洛塔帝桌宠.exe', '卸载海洛塔帝桌宠.exe', '使用说明.md', 'LICENSE', 'NOTICE.md')]
for directory in ('assets',):
    files.extend(sorted(path for path in (app / directory).rglob('*') if path.is_file()))
relative = [path.relative_to(app).as_posix() for path in files]
assert not any(path in relative for path in ('settings.json', 'auth.json', 'history.json', 'placement.txt', 'runtime.json'))
assert not any('checks/' in path or 'ai-test-user/' in path or '.codex/' in path for path in relative)
assert not any(path.startswith('runtime/') for path in relative), 'Codex must not be bundled'
manifest = '\n'.join(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.relative_to(app).as_posix() for path in files) + '\n'
(app / 'payload.sha256').write_text(manifest, encoding='utf-8')
files.append(app / 'payload.sha256')
archive = root / 'build' / 'payload.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
    for path in files:
        bundle.write(path, path.relative_to(app).as_posix())
with zipfile.ZipFile(archive) as bundle:
    assert bundle.testzip() is None
    (root / 'checks').mkdir(exist_ok=True)
    (root / 'checks' / 'installer-payload.txt').write_text('\n'.join(bundle.namelist()), encoding='utf-8')
print('Payload:', archive, archive.stat().st_size, 'bytes;', len(files), 'files; no user credentials/history/settings.')
