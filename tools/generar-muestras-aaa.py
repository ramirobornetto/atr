"""Generate synthetic read-only importer fixtures, never actual game achievements."""
from pathlib import Path
import json,csv
root=Path(__file__).resolve().parents[1]/'muestras'/'MuestrasAAA'
groups={
 'Goldberg SteamEmu Saves':[(1245620,'ELDEN RING'),(1593500,'God of War'),(1091500,'Cyberpunk 2077'),(1174180,'Red Dead Redemption 2'),(1888930,'The Last of Us Part I')],
 'GSE Saves':[(2322010,'God of War Ragnarok'),(990080,'Hogwarts Legacy'),(1151640,'Horizon Zero Dawn'),(1817070,'Marvel\'s Spider-Man Remastered'),(2050650,'Resident Evil 4')],
 'CODEX':[(292030,'The Witcher 3: Wild Hunt'),(883710,'Resident Evil 2'),(782330,'DOOM Eternal'),(750920,'Shadow of the Tomb Raider'),(412020,'Metro Exodus')],
 'RUNE':[(1817190,'Marvel\'s Spider-Man: Miles Morales'),(2420110,'Horizon Forbidden West'),(1680880,'Forspoken'),(534380,'Dying Light 2 Stay Human'),(1259420,'Days Gone')]
}
scenarios=[[0,90,5400,86400],[0,0,120,121],[0,3600,86400,172800],[0,45,180,3600],[0,600,64800,65400]]
manifest=[]
for provider,games in groups.items():
 for i,(app_id,title) in enumerate(games):
  folder=root/provider/str(app_id);folder.mkdir(parents=True,exist_ok=True)
  ini=provider in ('CODEX','RUNE');prefix=('CODEX' if ini else 'JSON')
  ids=[f'ATR_DEMO_{prefix}_{app_id}_{n+1:02}' for n in range(6)]
  base=1704067200+i*86400
  states={api:{'earned':n<4,'earned_time':base+scenarios[i][n] if n<4 else 0} for n,api in enumerate(ids)}
  labels=['Inicio de prueba','Exploración de prueba','Otra sesión ficticia','Final de prueba','Pendiente de prueba','Segundo pendiente']
  definitions=[{'name':api,'displayName':{'spanish':labels[n],'english':labels[n]},'description':{'spanish':'Logro ficticio de ATR. No existe en el juego real.'},'hidden':0} for n,api in enumerate(ids)]
  def save(name,data):
   path=folder/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
  (folder/'.atr-demo').write_text('MUESTRA SINTETICA ATR. NO USAR EN STEAM. Mantener este marcador.\n',encoding='utf-8')
  save('atr-demo.json',{'AppId':app_id,'GameName':title,'Synthetic':True,'Achievements':{api:{'DisplayName':labels[n],'Description':'Logro ficticio de ATR; solo para probar el importador.'} for n,api in enumerate(ids)}})
  if ini:
   save('referencia-logros.json',states)
   lines=['; Muestra sintetica ATR. Los IDs no pertenecen al juego real.']
   for api,state in states.items():lines.extend([f'[{api}]',f'Achieved={int(state["earned"])}','CurProgress=0','MaxProgress=0',f'UnlockTime={state["earned_time"]}',''])
   lines.extend(['[SteamAchievements]','Count=6']+[f'{n:05}={api}' for n,api in enumerate(ids)])
   (folder/'achievements.ini').write_text('\n'.join(lines)+'\n',encoding='utf-8')
   file='achievements.ini'
  else:
   save('achievements.json',states);save('steam_settings/achievements.json',definitions);file='achievements.json'
  manifest.append({'Origen':provider,'AppID':app_id,'Juego':title,'Archivo':str((folder/file).relative_to(root)),'Esperado':'4 / 6','Escenario':i+1})
root.mkdir(parents=True,exist_ok=True)
(root/'catalogo.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
with (root/'catalogo.csv').open('w',encoding='utf-8-sig',newline='') as f:
 writer=csv.DictWriter(f,fieldnames=list(manifest[0]),delimiter=';');writer.writeheader();writer.writerows(manifest)
(root/'LEEME.txt').write_text('''20 CARPETAS DE PRUEBA AAA — LOGROS FICTICIOS, NO HISTORIALES REALES

Hay cinco carpetas numéricas en cada grupo: Goldberg SteamEmu Saves, GSE Saves, CODEX y RUNE.
Los nombres y AppIDs identifican los títulos elegidos; los IDs ATR_DEMO_* y todos sus estados/fechas son inventados. No reemplazar archivos de un juego real.

IMPORTACION MANUAL (sin instalar emulaciones):
- Goldberg/GSE: elegir achievements.json desde Importar Goldberg.
- CODEX/RUNE: elegir achievements.ini desde Importar CODEX/RUNE.
- referencia-logros.json de CODEX/RUNE sirve para inspeccionar los mismos estados en JSON. No es un guardado nativo y no debe importarse con el lector Goldberg.
- atr-demo.json identifica la muestra y sus nombres; no es el archivo de estado.

BUSQUEDA AUTOMATICA: copiar cada carpeta numérica completa a su ubicación correspondiente, solo si no existe ya una carpeta con ese AppID:
Goldberg: %APPDATA%\\Goldberg SteamEmu Saves
GSE: %APPDATA%\\GSE Saves
CODEX: %PUBLIC%\\Documents\\Steam\\CODEX o %APPDATA%\\Steam\\CODEX
RUNE: %PUBLIC%\\Documents\\Steam\\RUNE o %APPDATA%\\Steam\\RUNE
No copiar el grupo CODEX dentro de Goldberg ni el JSON de referencia como achievements.json.

RESULTADO: cada carpeta debe dar 4 / 6, nombre del título con indicación de muestra y acciones Steam bloqueadas. Hay cinco patrones de intervalos, incluidos empates y pausas entre días. Se pueden probar casillas, orden, límites, guardar/cargar/restaurar y abrir ubicación.
Mantener .atr-demo y atr-demo.json. Si se renombra la carpeta AppID, el nombre del descriptor se ignora y se conserva la indicación de muestra por los IDs ficticios.
El catálogo enumera los 20 títulos, archivos y resultados esperados.
''',encoding='utf-8')
print('Generadas',len(manifest),'carpetas de juegos ficticios.')
