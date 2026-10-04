import sys,glob,os,struct
PAL=None
def load(p): return bytes(b^0xFF for b in open(p,'rb').read())
def W(d,o): return struct.unpack_from('<H',d,o)[0]
def dec(d,start,width,height):
    end=start+W(d,start)
    px=bytearray(width*height)
    for s in range(width//8):
        pos=start+W(d,start+2+s*2); x=y=0
        while x<8:
            c=d[pos];pos+=1
            copy=(c&0xC0)==0x80; alt=(c&0xC0)==0xC0
            run=(c&0x3F) if c&0x80 else c>>4
            pair=0
            if alt: pair=d[pos];pos+=1
            if run==0: run=d[pos];pos+=1
            for z in range(run):
                X=s*8+x
                px[y*width+X]=(px[y*width+X-1] if X>0 else 0) if copy else ((pair>>4 if z%2==0 else pair&15) if alt else c&15)
                y+=1
                if y==height: y=0;x+=1
    return px
root=sys.argv[1]
for p in sorted(glob.glob(os.path.join(root,'[0-9][0-9].LFL'))):
    rid=os.path.basename(p)[:2]
    if rid=='00': continue
    try:
        d=load(p); end=W(d,0); w=W(d,4); h=W(d,6); n=d[20]; ims=W(d,10)
        bg=dec(d,ims,w,h)
    except Exception as e: print(rid,'ERR',e); continue
    recs=[]
    for i in range(n):
        im=W(d,29+i*2); cd=W(d,29+n*2+i*2)
        if cd+16>end: continue
        oid=W(d,cd+4); x=d[cd+7]*8; y=(d[cd+8]&0x7f)*8; ow=d[cd+9]*8; oh=d[cd+15]&0xF8
        shares = i+1<n and W(d,29+(i+1)*2)==im
        px=None
        if not shares and im>=29+n*4 and im!=cd and ow and oh and x+ow<=w and y+oh<=h:
            try: px=dec(d,im,ow,oh)
            except Exception: px=None
        recs.append((i+1,oid,x,y,ow,oh,d[cd+10],px))
    imgs=[r for r in recs if r[7] is not None]
    for a in imgs:
        for b in imgs:
            if a is b: continue
            if (a[2],a[3],a[4],a[5])==(b[2],b[3],b[4],b[5]): continue
            # b inside a
            if b[2]>=a[2] and b[3]>=a[3] and b[2]+b[4]<=a[2]+a[4] and b[3]+b[5]<=a[3]+a[5]:
                zeros=sum(1 for yy in range(b[5]) for xx in range(b[4]) if a[7][(b[3]-a[3]+yy)*a[4]+(b[2]-a[2]+xx)]==0)
                print(f"room {rid}: obj {b[1]} (#{b[0]} {b[4]}x{b[5]}@{b[2]},{b[3]}) inside obj {a[1]} (#{a[0]}); container black under it {zeros/(b[4]*b[5]):.0%}; order {'container first' if a[0]<b[0] else 'overlay first'}")
    # background holes under objects
    seen=set()
    for r in imgs:
        key=(r[2],r[3],r[4],r[5])
        if key in seen: continue
        seen.add(key)
        z=sum(1 for yy in range(r[5]) for xx in range(r[4]) if bg[(r[3]+yy)*w+r[2]+xx]==0)/(r[4]*r[5])
        nz=sum(1 for yy in range(r[5]) for xx in range(r[4]) if r[7][yy*r[4]+xx]==0)/(r[4]*r[5])
        if z>0.5 and nz<0.3: print(f"room {rid}: bg hole {z:.0%} black under rect {key} (obj {r[1]} black {nz:.0%})")
