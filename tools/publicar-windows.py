"""Build and package ATR plus its automatic updater and synthetic test data."""
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,xml.etree.ElementTree as ET,zipfile
parser=argparse.ArgumentParser();parser.add_argument('--sin-compilar',action='store_true',help='Empaquetar publicaciones ya generadas y verificadas');args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
version=ET.parse(root/'WpfPrototype/AchievementTimelineReplay.Wpf.csproj').getroot().findtext('PropertyGroup/Version')
app=root/'artifacts'/f'Windows-x64-v{version}';helper=root/'artifacts/Updater-win-x64';downloads=root/'downloads';downloads.mkdir(exist_ok=True)
if not args.sin_compilar:
 dotnet=os.environ.get('DOTNET','dotnet')
 for project,out,extra in [('Updater/ATR.Updater.csproj',helper,[]),('WpfPrototype/AchievementTimelineReplay.Wpf.csproj',app,['-p:EnableWindowsTargeting=true','-p:PublishReadyToRun=false'])]:
  subprocess.run([dotnet,'publish',str(root/project),'-c','Release','-r','win-x64','--self-contained','true',f'-p:Version={version}','-o',str(out),*extra],check=True)
def digest(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
# Both executables share Core runtime files. The Desktop pack supplies three compatible
# facades which take precedence over their unused Core counterparts in the helper.
for p in helper.rglob('*'):
 if not p.is_file() or p.suffix=='.pdb':continue
 relative=p.relative_to(helper);dest=app/relative;dest.parent.mkdir(parents=True,exist_ok=True)
 if p.name.startswith('ATR.Updater.') or not dest.exists():shutil.copy2(p,dest)
 elif digest(p)!=digest(dest) and p.name not in {'Microsoft.VisualBasic.dll','WindowsBase.dll','System.Drawing.dll'}:raise RuntimeError(f'Runtime incompatible: {relative}')
for name in ['AchievementTimelineReplay.Wpf.exe','ATR.Updater.exe','ATR.Updater.dll','ATR.Updater.deps.json','ATR.Updater.runtimeconfig.json']:
 assert (app/name).is_file(),name
archive=downloads/f'ATR_v{version}_Windows_x64.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in app.rglob('*'):
  if p.is_file() and p.suffix!='.pdb':z.write(p,p.relative_to(app))
 for p in (root/'muestras').rglob('*'):
  if p.is_file():z.write(p,p.relative_to(root))
 for name in ['README.md','GOLDBERG.md','CODEX_RUNE.md','ACTUALIZACIONES.md']:z.write(root/name,name)
fixtures=downloads/f'ATR_Muestras_AAA_v{version}.zip'
with zipfile.ZipFile(fixtures,'w',zipfile.ZIP_DEFLATED) as z:
 for p in (root/'muestras/MuestrasAAA').rglob('*'):
  if p.is_file():z.write(p,p.relative_to(root/'muestras'))
for package in [archive,fixtures]:
 with zipfile.ZipFile(package) as z:assert z.testzip() is None
with zipfile.ZipFile(archive) as z:
 assert z.read('AchievementTimelineReplay.Wpf.dll')==(app/'AchievementTimelineReplay.Wpf.dll').read_bytes()
 assert z.read('ATR.Updater.dll')==(helper/'ATR.Updater.dll').read_bytes()
 assert len([p for p in z.namelist() if p.startswith('muestras/MuestrasAAA/') and p.endswith('/atr-demo.json')])==20
manifest={'Version':version,'DownloadUrl':f'https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/{archive.name}','Sha256':digest(archive),'SizeBytes':archive.stat().st_size}
(downloads/'latest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
sums=downloads/'SHA256SUMS.txt';names={archive.name,fixtures.name};lines=[l for l in sums.read_text().splitlines() if l.split()[-1] not in names]
lines.extend(digest(p)+'  '+p.name for p in [archive,fixtures]);sums.write_text('\n'.join(lines)+'\n')
print('Paquetes y manifiesto verificados:',archive.name,fixtures.name)
