"""A plain PNG reader (8-bit RGBA, no libraries): rd(path) -> (w, h, rows). Run on files to count their colours.
Used by skin_recipes.py."""
import sys,zlib,struct,collections
def rd(p):
    d=open(p,'rb').read(); i=8; idat=b''
    while i<len(d):
        n,=struct.unpack('>I',d[i:i+4]); t=d[i+4:i+8]; c=d[i+8:i+8+n]
        if t==b'IHDR': w,h=struct.unpack('>II',c[:8])
        if t==b'IDAT': idat+=c
        i+=12+n
    raw=zlib.decompress(idat); bpp=4; stride=w*bpp; out=[]; prev=bytearray(stride); p=0
    for y in range(h):
        f=raw[p]; line=bytearray(raw[p+1:p+1+stride]); p+=1+stride
        for x in range(stride):
            a=line[x-bpp] if x>=bpp else 0; b=prev[x]; c=prev[x-bpp] if x>=bpp else 0
            if f==1: line[x]=(line[x]+a)&255
            elif f==2: line[x]=(line[x]+b)&255
            elif f==3: line[x]=(line[x]+(a+b)//2)&255
            elif f==4:
                pa,pb,pc=abs(b-c),abs(a-c),abs(a+b-2*c); pr=a if pa<=pb and pa<=pc else b if pb<=pc else c
                line[x]=(line[x]+pr)&255
        out.append(bytes(line)); prev=line
    return w,h,out
if __name__ == "__main__":
  for f in sys.argv[1:]:
        w,h,rows=rd(f); cnt=collections.Counter()
        for r in rows:
            for x in range(w):
                px=tuple(r[x*4:x*4+4])
                if px[3]: cnt[px]+=1
        print(f.split('/')[-1], 'opaque', sum(cnt.values()), 'colours', len(cnt), cnt.most_common(8))
