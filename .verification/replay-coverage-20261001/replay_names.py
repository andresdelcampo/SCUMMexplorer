"""Source-backed replay labels. Never infer a target name from artwork/dialogue."""
import collections, pathlib, re, struct

class ReplayNames:
    def __init__(self, game, code):
        self.local = {}
        self.shared = collections.defaultdict(set)
        self.renames = collections.defaultdict(set)
        for path in sorted(pathlib.Path(game).glob('*.LFL')):
            data = bytes(b ^ 255 for b in path.read_bytes())
            # Room directory, as LoomRoomObjectDirectory. Non-room resources fail
            # the room length/header and bounded object-code checks.
            if len(data) < 29: continue
            room_end = struct.unpack_from('<H', data)[0]
            width, height = struct.unpack_from('<HH', data, 4)
            if room_end > len(data) or not 8 <= width <= 1280 or not 1 <= height <= 200: continue
            count = data[20]
            if 29 + count * 4 > room_end: continue
            for i in range(count):
                code_offset = struct.unpack_from('<H', data, 29 + count * 2 + i * 2)[0]
                if code_offset < 29 + count * 4 or code_offset + 17 > room_end: continue
                size, obj = struct.unpack_from('<H', data, code_offset)[0], struct.unpack_from('<H', data, code_offset + 4)[0]
                relative = data[code_offset + 16]
                end = code_offset + size
                if size < 18 or end > room_end or relative < 17 or relative >= size: continue
                start = code_offset + relative
                terminator = data.find(b'\0', start, end)
                if terminator <= start: continue
                raw = data[start:terminator]
                # Dynamic name/control substitutions cannot establish a plain name.
                if b'\xff' in raw: continue
                name = raw.decode('cp437').replace('@', '').strip()
                if not any(c.isalnum() for c in name): continue
                self.local[int(path.stem), obj] = name
                self.shared[obj].add(name)
        for line in open(code, encoding='utf-8-sig'):
            cols = line.rstrip('\n').split('\t', 3)
            if len(cols) != 4 or cols[2] != 'setObjectName': continue
            match = re.fullmatch(r'(0x[\dA-Fa-f]+|\d+), "([^"\\]*)"', cols[3])
            if match:
                self.renames[int(match[1], 0)].add(match[2].replace('@', '').strip())

    def describe(self, recipe):
        room, obj, verb = recipe['room'], recipe['objectId'], recipe['verb']
        if not obj: return f'Arrival in Room {room:02d}', 'Room entry; no object name required.'
        name = self.local.get((room, obj))
        evidence = 'Stored name in the trigger room.'
        if not name and len(self.shared[obj]) == 1:
            name = next(iter(self.shared[obj]))
            evidence = 'Unambiguous stored name for the same object ID in shared room definitions.'
        # Renames are conditional; static operands alone do not establish which
        # name was reached. Keep stored labels, and record the alternatives.
        alternatives = sorted(self.renames[obj])
        if alternatives: evidence += ' Conditional naming operands (not validated live names): ' + ', '.join(alternatives) + '.'
        action = {0x36: 'Look at', 0x37: 'Walk to', 0x38: 'Use', 0x56: 'Other draft response at'}.get(verb, f'Draft response {verb - 0x39} at')
        if name: return f'Room {room:02d} · {action} {name}', evidence
        response = {0x36: 'Look response', 0x37: 'Walk response', 0x38: 'Use response', 0x56: 'Other draft response'}.get(verb, f'Draft response {verb - 0x39}')
        reached = sorted(set(recipe['rooms']) - {room})
        if reached: response += ' · Reached ' + ('Room ' if len(reached) == 1 else 'Rooms ') + ', '.join(f'{r:02d}' for r in reached)
        gap = 'Naming gap: no useful stored name in any supplied room definition; no validated naming instruction supplies one. Label uses the decoded action and validated reached rooms.'
        if alternatives: gap += ' Unresolved conditional naming operands: ' + ', '.join(alternatives) + '.'
        return f'Room {room:02d} · {response}', gap
