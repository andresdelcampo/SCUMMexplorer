# Compares the RoomAudit report before and after the interface-image and
# reached-setState rules: paint order, fallback and estimate lines per room.
import re
def parse(f):
    rooms={};cur=None
    for l in open(f,encoding='utf-8'):
        m=re.match(r'== Room (\d+)',l)
        if m: cur=m.group(1); rooms[cur]={'fb':[],'est':[],'paint':'','extra':[]}; continue
        if cur is None: continue
        l=l.strip()
        if l.startswith('fallback '): rooms[cur]['fb'].append(l[9:])
        elif l.startswith('estimate '): rooms[cur]['est'].append(l[9:])
        elif l.startswith('paint order:'): rooms[cur]['paint']=l[12:].strip()
        elif l.startswith(('interface','state set','nothing visible after')): rooms[cur]['extra'].append(l)
    return rooms
b=parse('room-audit-before.txt'); a=parse('room-audit-after.txt')
first='first stored image'
def count(rs): return sorted(r for r in rs if any(first in x for x in rs[r]['fb']))
print('rooms using the first-stored-image fallback: before', len(count(b)), count(b))
print('                                             after ', len(count(a)), count(a))
print()
for r in sorted(a):
    if b[r]['paint']!=a[r]['paint'] or b[r]['fb']!=a[r]['fb'] or b[r]['est']!=a[r]['est']:
        print(f"Room {r}: paint {b[r]['paint'] or '-'}  ->  {a[r]['paint'] or '(background only)'}")
        for x in a[r]['extra']: print('    ',x)
        for x in a[r]['fb']: print('     fallback',x)
        for x in a[r]['est']: print('     estimate',x)
