# CODEX/RUNE compatible — ATR 0.8.0

El soporte inicial lee `achievements.ini` con una sección por ID API, `Achieved=0/1` y `UnlockTime` Unix decimal en segundos. Se admiten UTF-8/ASCII y UTF-16 con BOM, claves sin distinción de mayúsculas y comentarios `;` / `#` de línea completa. Una fecha positiva no convierte un logro bloqueado en desbloqueado. El progreso completo sin `Achieved=1` tampoco se convierte por conjetura. Los desbloqueos sin fecha válida se explican en Logs y no entran al plan.

```ini
[EXAMPLE_API_ID]
Achieved=1
CurProgress=0
MaxProgress=0
UnlockTime=1704067200
[SteamAchievements]
Count=1
00000=EXAMPLE_API_ID
```

`SteamAchievements` es un índice; no se cuenta como otro logro. Se rechazan secciones o campos duplicados/conflictivos y variantes que no contienen estados explícitos admitidos. No se interpretan fechas hexadecimales, milisegundos ni formatos de otros proveedores mediante aproximaciones.

## Importación

**Escaneo → Importar CODEX/RUNE…** permite elegir `<AppID>/achievements.ini` o `<AppID>/remote/achievements.ini`. No requiere tener la emulación instalada. Se buscan también esas dos disposiciones bajo:

- `%PUBLIC%\Documents\Steam\CODEX`
- `%PUBLIC%\Documents\Steam\RUNE`
- `%APPDATA%\Steam\CODEX`
- `%APPDATA%\Steam\RUNE`

La búsqueda se limita a esas carpetas, sus hijos y `remote`; no recorre todo el disco. No se admiten todavía disposiciones arbitrarias con varios niveles de usuarios ni variantes binarias, otros nombres de archivo o inferencia de desbloqueos por progreso. Una variante desconocida requiere examinar un archivo real antes de añadirla.

Biblioteca informa `achievements.ini (CODEX/RUNE)` como Fuente. Origen se sugiere por un componente CODEX/RUNE en la ruta; sin ese indicio se muestra `CODEX/RUNE compatible`. El formato compartido no certifica qué ejecutable escribió el archivo. Las asignaciones manuales tienen prioridad.

Se usan las definiciones del esquema Steam local si existe; sin ellas se conservan los IDs API. El nombre del juego se resuelve por caché pública/Steam Store, sin usar nombres internos del esquema. Los datos de cada archivo se aíslan por ruta, independientemente de Steam, Goldberg y otras cuentas locales. Los snapshots conservan el primer historial y los bytes del INI original. Planes y origen se guardan por esa identidad; cambiar la ruta crea otra identidad. No se ejecutan ni modifican emuladores ni sus archivos de configuración.

## Veinte muestras AAA

`muestras/MuestrasAAA` contiene cinco carpetas por origen. Los juegos y AppIDs son reales; los logros `ATR_DEMO_*`, sus fechas y estados son totalmente ficticios. Cada carpeta debe mostrar **4 / 6**, el título con indicación de muestra y acciones de Steam bloqueadas. Los nombres de muestra funcionan sin conexión.

Goldberg/GSE incluyen `achievements.json` y su definición de nombres. CODEX/RUNE incluyen `achievements.ini` y `referencia-logros.json` con los mismos estados para inspección. El JSON de referencia no es un guardado nativo ni se importa con el lector Goldberg. `atr-demo.json` es un descriptor de prueba, no un archivo de estado.

El catálogo JSON/CSV enumera los 20 títulos y los archivos a importar. `LEEME.txt` explica cómo copiarlos a rutas conocidas. Copiar únicamente a AppIDs que no tengan ya guardados; no reemplazar archivos de un juego real. Mantener los marcadores de demostración. Se pueden probar orden, casillas, tiempos, límites, guardar/cargar/restaurar y abrir ubicación; no desbloquear logros reales.

Formato contrastado con la [muestra INI pública de Achievement Watcher](https://github.com/xan105/Achievement-Watcher/blob/d8609019353d66f0cdcebe36c7e548be375df14d/README.md) y su [lector de estados y rutas](https://github.com/xan105/Achievement-Watcher/blob/d8609019353d66f0cdcebe36c7e548be375df14d/service/watchdog/monitor.js). Implementación propia, con pruebas sintéticas; pendiente contraste con guardados CODEX/RUNE producidos por un juego real y validación visual en Windows.
