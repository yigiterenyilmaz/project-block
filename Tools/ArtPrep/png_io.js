// PURPOSE: Minimal PNG read/write for the art prep scripts (8-bit gray/RGB/RGBA in, RGBA out).
// Node only - this machine has no Python.
const fs=require('fs'),zlib=require('zlib');
function decode(file){const b=fs.readFileSync(file);let p=8,w,h,ct,bd,idat=[];
while(p<b.length){const len=b.readUInt32BE(p),type=b.toString('ascii',p+4,p+8),d=b.slice(p+8,p+8+len);
if(type==='IHDR'){w=d.readUInt32BE(0);h=d.readUInt32BE(4);bd=d[8];ct=d[9];}if(type==='IDAT')idat.push(d);p+=12+len;}
if(bd!==8)throw new Error('bitdepth '+bd);const ch={6:4,2:3,0:1,4:2}[ct];const raw=zlib.inflateSync(Buffer.concat(idat));
const stride=w*ch,out=Buffer.alloc(w*h*4),prev=Buffer.alloc(stride),cur=Buffer.alloc(stride);
for(let y=0;y<h;y++){const f=raw[y*(stride+1)];raw.copy(cur,0,y*(stride+1)+1,(y+1)*(stride+1));
for(let i=0;i<stride;i++){const a=i>=ch?cur[i-ch]:0,up=prev[i],c=i>=ch?prev[i-ch]:0;let v=cur[i];
if(f===1)v+=a;else if(f===2)v+=up;else if(f===3)v+=(a+up)>>1;else if(f===4){const pp=a+up-c,pa=Math.abs(pp-a),pb=Math.abs(pp-up),pc=Math.abs(pp-c);v+=(pa<=pb&&pa<=pc)?a:(pb<=pc?up:c);}cur[i]=v&255;}
for(let x=0;x<w;x++){const o=(y*w+x)*4;if(ch===4){for(let k=0;k<4;k++)out[o+k]=cur[x*4+k];}else if(ch===3){out[o]=cur[x*3];out[o+1]=cur[x*3+1];out[o+2]=cur[x*3+2];out[o+3]=255;}else{out[o]=out[o+1]=out[o+2]=cur[x*ch];out[o+3]=ch===2?cur[x*2+1]:255;}}
cur.copy(prev);}return{w,h,ct,data:out};}
function crc32(buf){let c,crc=0xFFFFFFFF;for(let n=0;n<buf.length;n++){c=(crc^buf[n])&0xFF;for(let k=0;k<8;k++)c=c&1?0xEDB88320^(c>>>1):c>>>1;crc=(crc>>>8)^c;}return(crc^0xFFFFFFFF)>>>0;}
function chunk(t,d){const l=Buffer.alloc(4);l.writeUInt32BE(d.length);const body=Buffer.concat([Buffer.from(t,'ascii'),d]);const c=Buffer.alloc(4);c.writeUInt32BE(crc32(body));return Buffer.concat([l,body,c]);}
function encode(file,w,h,data){const raw=Buffer.alloc(h*(w*4+1));for(let y=0;y<h;y++){raw[y*(w*4+1)]=0;data.copy(raw,y*(w*4+1)+1,y*w*4,(y+1)*w*4);}
const ih=Buffer.alloc(13);ih.writeUInt32BE(w,0);ih.writeUInt32BE(h,4);ih[8]=8;ih[9]=6;
fs.writeFileSync(file,Buffer.concat([Buffer.from([0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A]),chunk('IHDR',ih),chunk('IDAT',zlib.deflateSync(raw,{level:9})),chunk('IEND',Buffer.alloc(0))]));}
module.exports={decode,encode};
