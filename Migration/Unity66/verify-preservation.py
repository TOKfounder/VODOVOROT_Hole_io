#!/usr/bin/env python3
"""Compare the migration against the original working tree, including uncommitted files."""
import difflib
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[2]
backup = root.parent / '_MigrationBackups/VODOVOROT_Hole_io-20261106-pre-unity66'
original = backup / 'Project'
out = root / 'Migration/Unity66'
hashes = json.loads((backup / 'sha256-source.json').read_text())

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None

def guid(path):
    match = re.search(r'^guid: (\w+)', path.read_text(errors='replace'), re.M) if path.is_file() else None
    return match.group(1) if match else None

sources = [p for p in hashes if p.startswith(('Assets/', 'Packages/', 'ProjectSettings/', 'Tests/', 'Tools/'))]
changed = []
patch = []
for p in sorted(sources):
    current_hash = digest(root / p)
    if current_hash == hashes[p]:
        continue
    changed.append(dict(path=p, status='changed' if current_hash else 'missing',
                        beforeSha256=hashes[p], afterSha256=current_hash))
    try:
        before = (original / p).read_text(encoding='utf-8').splitlines(True)
        after = (root / p).read_text(encoding='utf-8').splitlines(True) if current_hash else []
        patch.extend(difflib.unified_diff(before, after, fromfile='original/' + p, tofile='migrated/' + p))
    except UnicodeError:
        patch.append('Binary changed: ' + p + '\n')

metas = [p for p in sources if p.endswith('.meta')]
scenes = [p for p in sources if p.endswith('.unity')]
prefabs = [p for p in sources if p.endswith('.prefab')]
save = 'Assets/PluginYourGames/Editor/SavesEditorYG2.json'
settings = list(original.glob('Assets/**/SettingsYG2.asset'))
mcp = json.loads((out / 'mcp-provenance.json').read_text())
mcp_mismatches = [p for p, h in mcp['files'].items() if digest(root / 'LocalPackages/MCPForUnity' / p) != h]
result = dict(finishedUtc=datetime.now(timezone.utc).isoformat(), originalMetaCount=len(metas),
    changedOrMissingGuids=[p for p in metas if guid(root / p) != guid(original / p)],
    sceneCount=len(scenes), changedScenes=[p for p in scenes if digest(root / p) != hashes[p]],
    prefabCount=len(prefabs), changedPrefabs=[p for p in prefabs if digest(root / p) != hashes[p]],
    missingOriginalSourceFiles=[x['path'] for x in changed if x['status'] == 'missing'],
    editorSaveByteExact=digest(root / save) == hashes[save],
    sdkSettingsByteExact=all(digest(root / p.relative_to(original)) == digest(p) for p in settings),
    vendoredMcpProvenanceMismatches=mcp_mismatches)
assert not any(result[k] for k in ('changedOrMissingGuids', 'changedScenes', 'changedPrefabs',
                                  'missingOriginalSourceFiles', 'vendoredMcpProvenanceMismatches'))
assert result['editorSaveByteExact'] and result['sdkSettingsByteExact']
(out / 'preservation-final.json').write_text(json.dumps(result, indent=2))
(out / 'changed-original-files.json').write_text(json.dumps(changed, indent=2))
(out / 'migration-only.patch').write_text(''.join(patch))
before = json.loads((original / 'Packages/packages-lock.json').read_text())['dependencies']
after = json.loads((root / 'Packages/packages-lock.json').read_text())['dependencies']
(out / 'package-changes-final.json').write_text(json.dumps({k: dict(before=before.get(k), after=after.get(k))
    for k in sorted(before.keys() | after.keys()) if before.get(k) != after.get(k)}, indent=2))
print(json.dumps(result, indent=2))
