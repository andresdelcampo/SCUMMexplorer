import struct,sys,os
def open_img(path):
    f=open(path,'rb'); 
    return f
def sector(f,n,raw=True):
    if raw:
        f.seek(n*2352+16); return f.read(2048)   # MODE1: 12 sync+4 header, 2048 user
    f.seek(n*2048); return f.read(2048)
def walk(f,lba,size,path,out):
    data=b''.join(sector(f,lba+i) for i in range((size+2047)//2048))[:size]
    pos=0
    while pos<len(data):
        l=data[pos]
        if l==0:
            pos=(pos//2048+1)*2048; continue
        rec=data[pos:pos+l]
        elba=struct.unpack_from('<I',rec,2)[0]; esize=struct.unpack_from('<I',rec,10)[0]; flags=rec[25]; nl=rec[32]; name=rec[33:33+nl].decode('latin1')
        pos+=l
        if name in('\x00','\x01'): continue
        out.append((path+'/'+name,elba,esize,flags))
        if flags&2: walk(f,elba,esize,path+'/'+name,out)
if __name__=='__main__':
    f=open_img(sys.argv[1]); pvd=sector(f,16)
    print(pvd[0],pvd[1:6],pvd[8:40].decode('latin1').strip(),pvd[40:72].decode('latin1').strip(),'sectors',struct.unpack_from('<I',pvd,80)[0])
    root=pvd[156:190]; out=[]; walk(f,struct.unpack_from('<I',root,2)[0],struct.unpack_from('<I',root,10)[0],'',out)
    for p,l,s,fl in out: print(p,l,s,'dir' if fl&2 else '')
    if len(sys.argv)>2:
        for p,l,s,fl in out:
            if not fl&2:
                n=p.strip('/').split(';')[0]; open(os.path.join(sys.argv[2],n),'wb').write(b''.join(sector(f,l+i) for i in range((s+2047)//2048))[:s])
