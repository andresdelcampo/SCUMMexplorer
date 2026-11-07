# Usage: python lengths.py [frames.tsv] - the replays whose scene length differs from the catalog's (less its 32 idle frames) by more than 10%.
import sys
rows=[l.rstrip('\n').split('\t') for l in open(sys.argv[1] if len(sys.argv)>1 else 'frames-loom-session.tsv',encoding='utf-8')]
w5=w10=0;off=[]
for c,e,lines,id in rows:
    c=int(c)-32;e=int(e)
    d=abs(c-e)
    if d<=5: w5+=1
    elif d<=0.1*max(c,1): w10+=1
    else: off.append((id,c,e,lines))
print(f"{len(rows)} replays: within 5 frames {w5}, within 10% {w10}; {len(rows)-len(off)} of {len(rows)} ({100*(len(rows)-len(off))/len(rows):.1f}%)")
for o in off: print(*o,sep='\t')
