"""Retain every inherited assertion and require all latest primary records exactly."""
from pathlib import Path
import hashlib
import json
import sys

source = Path('.verification/runtime-object-correspondence-20261002/analyze_correspondence.py').read_text(encoding='utf-8')
source = source.replace('.verification/runtime-object-correspondence-20261002', '.verification/runtime-restoration-20261002')
exec(compile(source, 'preserved-primary-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

out = Path(sys.argv[1])
prior = Path('Exe/RuntimeObjectCorrespondenceFinalSession2-20261002/final-evidence')
reports = []
primary_camera_waits = 0
native_message_fields = 0
for folder in ('room41-1', 'egg-first-1', 'dye-last-1', 'preserved-1'):
    for packet in sorted((out/folder).glob('*.json')):
        assert json.loads(packet.read_text(encoding='utf-8-sig')) == json.loads((prior/folder/packet.name).read_text(encoding='utf-8-sig'))
        for suffix in ('-instructions.jsonl', '-cancellations.jsonl', '-access.jsonl', '-relations.jsonl',
                       '-completion.jsonl', '-expressions.jsonl', '-calls.jsonl', '-object-selections.jsonl', '-correspondence.jsonl'):
            path = packet.with_name(packet.stem+suffix)
            old = prior/folder/path.name
            # UTF-8 jsonl contents retain exact chronology, IDs, original estimates and barriers.
            assert path.read_bytes() == old.read_bytes(), str(path)
            if suffix == '-access.jsonl':
                for line in path.open(encoding='utf-8'):
                    row = json.loads(line)
                    primary_camera_waits += int(row['Site']['Operation'] == 'waitFor' and row['Site']['Operands'] == 'camera')
                    native_message_fields += sum(d['Domain'] in ('message-have-msg', 'message-talk-delay', 'message-talking-actor')
                                                 for d in row['access'].get('NativeDependencies') or [])
            reports.append(dict(path=path.as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
assert primary_camera_waits == 0 and native_message_fields == 15
summary = dict(primaryWholePackets=13, primaryEntireLatestStreamsExact=True, actualBindings=366,
    structuralTriples=55, callerResumptions=0, originalWriterTerminals=76, newOriginalWriterResumedChains=0,
    nativeMessageFields=native_message_fields, nativeMessageProvenanceUnknown=True, primaryCameraWaits=primary_camera_waits, reports=reports)
Path(__file__).with_name('preservation-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('All13 latest primary packets/entire call/access/correspondence/completion streams exact;366/55/0 and76 original terminals retained.')
