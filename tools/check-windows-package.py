"""Inspect a Windows single-file bundle without executing Windows binaries."""
from pathlib import Path
import argparse,io,json,struct,zipfile
parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);args=parser.parse_args()
with zipfile.ZipFile(args.package) as z:
 names=z.namelist()
 assert {n.split('/')[0] for n in names}=={'AchievementTimelineReplay.exe','Support'}
 assert not any(n.startswith('muestras/') for n in names)
 data=z.read('AchievementTimelineReplay.exe');metadata=json.loads(z.read('Support/package.json'))
 assert metadata['Layout']==2
 for name in ['DESCRIPCION.md','es-howto.md','en-description.md','en-howto.md']:
  assert z.read('Support/Documentation/'+name)==(Path(__file__).resolve().parents[1]/name).read_bytes(),name
 assert data[:2]==b'MZ'
 pe=struct.unpack_from('<I',data,60)[0];assert data[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',data,pe+4)[0]==0x8664
 signature=bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')
 marker=data.find(signature);assert marker>=8
 header=struct.unpack_from('<q',data,marker-8)[0];assert 0<header<len(data)
 stream=io.BytesIO(data);stream.seek(header)
 def read(fmt):return struct.unpack(fmt,stream.read(struct.calcsize(fmt)))
 def string():
  size=0;shift=0
  while True:
   byte=stream.read(1)[0];size|=(byte&127)<<shift
   if byte<128:break
   shift+=7
  return stream.read(size).decode('utf-8')
 major,minor,count=read('<IIi');assert (major,minor)==(6,0) and 0<count<20000
 bundle_id=string();deps_offset,deps_size,config_offset,config_size,flags=read('<qqqqQ')
 assert flags&1,'All-content extraction is required by the Windows desktop/browser libraries.'
 entries={}
 for _ in range(count):
  offset,size,compressed=read('<qqq');kind=read('<B')[0];name=string()
  assert name not in entries and offset>=0 and size>=0 and 0<=compressed and offset+(compressed or size)<=header
  entries[name]=(offset,size,compressed,kind)
 for required in ['AchievementTimelineReplay.dll','System.Private.CoreLib.dll','PresentationFramework.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll','PresentationNative_cor3.dll','wpfgfx_cor3.dll']:
  assert any(n==required or n.endswith('/'+required) for n in entries),required
 assert '--atr-update'.encode('utf-16le') in data,'Integrated updater mode is absent.'
 config=json.loads(data[config_offset:config_offset+config_size])
 assert 'includedFrameworks' in config['runtimeOptions'],'The package must include .NET rather than require it to be installed.'
 print(f'Paquete compacto {metadata["Version"]}: 2 elementos en raíz; {len(entries)} componentes integrados; x64, .NET, WPF, WebView2, instalador y documentación verificados.')
