"""Package app and editable sources without user settings, logs or test images."""
from pathlib import Path
import zipfile
import sys

root = Path(__file__).resolve().parents[1]
files = [root / name for name in ('海洛塔帝桌宠.exe', '使用说明.md', '默认提示词.txt', 'README.md', 'LICENSE', 'NOTICE.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'SECURITY.md')]
files += list(root.glob('*.cmd'))
for name in ('assets', 'src', 'docs', 'ui-preview'):
    files += [p for p in (root / name).rglob('*') if p.is_file() and '__pycache__' not in p.parts and 'screenshots' not in p.parts]
target = Path(sys.argv[1]) if len(sys.argv)>1 else root.parent / '海洛塔帝桌宠-便携版.zip'
target.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
    for path in sorted(set(files)):
        bundle.write(path, (Path(root.name) / path.relative_to(root)).as_posix())
with zipfile.ZipFile(target) as bundle:
    assert bundle.testzip() is None
    assert not any('/checks/' in name or name.endswith(('settings.json', 'history.json', 'auth.json')) for name in bundle.namelist())
print('Portable ZIP:', target.stat().st_size, 'bytes')
