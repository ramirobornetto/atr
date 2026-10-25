"""Build the current compact Windows package and its update manifest."""
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,xml.etree.ElementTree as ET,zipfile
parser=argparse.ArgumentParser()
parser.add_argument('--sin-compilar',action='store_true',help='Empaquetar una publicación ya generada')
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
version=ET.parse(root/'WpfPrototype/AchievementTimelineReplay.Wpf.csproj').getroot().findtext('PropertyGroup/Version')
app=root/'artifacts'/f'Windows-x64-v{version}';downloads=root/'downloads';downloads.mkdir(exist_ok=True)
if not args.sin_compilar:
 if app.exists():shutil.rmtree(app)
 subprocess.run([os.environ.get('DOTNET','dotnet'),'publish',str(root/'WpfPrototype/AchievementTimelineReplay.Wpf.csproj'),'-c','Release','-r','win-x64','--self-contained','true',f'-p:Version={version}','-p:EnableWindowsTargeting=true','-p:PublishReadyToRun=false','-o',str(app)],check=True)
def digest(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
executable=app/'AchievementTimelineReplay.exe';assert executable.is_file()
loose=[p.relative_to(app).as_posix() for p in app.rglob('*') if p.is_file() and p.suffix.lower() in {'.dll','.json','.exe'} and p!=executable]
assert not loose,f'Dependencias sin integrar: {loose}'
# Retain only hashes for safe retirement of previously distributed files, rather
# than requiring obsolete binary packages to remain in the repository.
inventory=json.loads((root/'tools/legacy-file-hashes.json').read_text())
metadata={'Layout':2,'Version':version,'LegacyFiles':inventory}
docs=['DESCRIPCION.md','es-howto.md','en-description.md','en-howto.md']
licenses=['THIRD_PARTY_NOTICES.md','third_party/WebView2-LICENSE.txt','third_party/Octicons-LICENSE.txt','third_party/DotNet-LICENSE.txt','third_party/DotNet-THIRD-PARTY-NOTICES.txt','third_party/WindowsDesktop-LICENSE.txt']
archive=downloads/f'ATR_v{version}_Windows_x64.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 z.write(executable,executable.name)
 z.writestr('Support/package.json',json.dumps(metadata,indent=2)+'\n')
 for name in docs:z.write(root/name,'Support/Documentation/'+name)
 for name in licenses:z.write(root/name,'Support/Licenses/'+name)
 z.write(root/'LICENSE','Support/Licenses/LICENSE')
 for p in app.iterdir():
  if p.is_file() and p.suffix.lower()=='.txt':z.write(p,'Support/Licenses/'+p.name)
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 assert {n.split('/')[0] for n in z.namelist()}=={'AchievementTimelineReplay.exe','Support'}
 assert not any(n.startswith('muestras/') for n in z.namelist())
manifest={'Version':version,'DownloadUrl':f'https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/{archive.name}','Sha256':digest(archive),'SizeBytes':archive.stat().st_size}
assert manifest['SizeBytes']<100*1024*1024,'El ZIP supera el límite por archivo de GitHub.'
(downloads/'latest-compact.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(downloads/'SHA256SUMS.txt').write_text(digest(archive)+'  '+archive.name+'\n')
print('Paquete y manifiesto verificados:',archive.name,archive.stat().st_size)
