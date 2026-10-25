"""Validate translation coverage and format placeholders without changing files."""
from pathlib import Path
import json,re,sys
root=Path(__file__).resolve().parents[1]
folder=root/'WpfPrototype/Localization'
catalogs={lang:json.loads((folder/(lang+'.json')).read_text()) for lang in ['es','en','pt-BR']}
source=catalogs['es'];errors=[]
slots=lambda text:re.findall(r'\{\d+(?:,[^}:]+)?(?::[^}]+)?\}',text)
for lang,catalog in catalogs.items():
 if catalog.keys()!=source.keys():errors.append(f'{lang}: different catalog keys')
 for key,value in catalog.items():
  if not isinstance(value,str) or not value:errors.append(f'{lang}: empty translation for {key!r}')
  elif slots(key)!=slots(value):errors.append(f'{lang}: changed placeholders in {key!r}')
paths=list((root/'WpfPrototype').rglob('*.cs'))+[root/'Updater/Program.cs']
for path in paths:
 if any(part in path.parts for part in ['obj','bin']):continue
 for match in re.finditer(r'UiText\.Text\(("(?:\\.|[^"\\])*")\)',path.read_text()):
  key=json.loads(match[1])
  if key not in source:errors.append(f'{path.relative_to(root)}: missing {key!r}')
for value in json.loads((folder/'Resources.json').read_text()).values():
 if value not in source:errors.append(f'XAML resource missing: {value!r}')
if errors:print('\n'.join(errors));sys.exit(1)
print(f'Localization verified: {len(source)} entries in Spanish, English and Brazilian Portuguese.')
