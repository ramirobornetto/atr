# PlayniteAchievements (PA) — ATR 0.9.0

ATR lee `achievement_cache.db` y, para seguimiento manual, `game_custom_data.db` de la misma carpeta. Es una lectura SQLite de solo consulta; no ejecuta Playnite ni modifica sus bases. Usa la biblioteca SQLite del sistema de Windows 10/11; no requiere instalar Python ni SQLite.

## Importar y escanear

La casilla **PA** en Escaneo busca bases en `%APPDATA%\Playnite\ExtensionsData\*`; no presupone el nombre o GUID de la extensión. **Importar PA…** permite seleccionar `achievement_cache.db` de otra instalación, incluida Playnite portable. Esa ubicación queda recordada para escaneos posteriores, aunque todavía no tenga desbloqueos. Las preferencias y rutas están en `%LOCALAPPDATA%\AchievementTimelineReplay\source-options.json`.

Cerrar Playnite por completo antes de importar o copiar las bases es lo recomendado para obtener un conjunto estable. Si se usan copias y quedan archivos `-wal`/`-shm`, conservarlos junto a su `.db` con sus nombres originales: el lector ve transacciones confirmadas que aún no llegaron al archivo principal. No eliminar esos archivos de la instalación de Playnite.

Se admiten proveedores **Steam**, **Manual con fuente Steam** y **Local** (Santodan) con AppID numérico y juego de Playnite identificable. Se leen estados del usuario actual; no se importan logros de amigos. Local admite el usuario interno local que el fork usa en su caché. No se adivinan AppID por títulos, no se convierten IDs de Xbox/Exophase a Steam ni se accede a config.json.

## Fuente más completa

Dentro de un mismo juego de Playnite y AppID, se elige el historial con más IDs únicos desbloqueados, sin sumar listas de fuentes distintas. La cantidad de fechas válidas solo desempata; si también coincide, se prefiere Manual. Si la caché identifica varios perfiles Steam actuales para el mismo juego, quedan separados. Un cambio de proveedor elegido mantiene la identidad PA del juego.

Las personalizaciones por logro de `AchievementOverrides` completan nombres y descripciones y respetan fechas corregidas o borradas explícitamente (`ClearUnlockTime`). Nunca convierten por sí solas un logro bloqueado en desbloqueado.

El seguimiento manual usa `ManualLink.UnlockStates` y `UnlockTimes` de `game_custom_data.db`; un estado explícito bloqueado tiene prioridad sobre la fecha o la caché. Un ID desbloqueado del vínculo que no tenga definición se conserva con su ID como nombre. Sin vínculo Steam verificable, los registros Manual se omiten con explicación; Steam y Local no requieren ese vínculo.

Cuando se escanea junto a archivos directos, ATR puede mostrar una sola entrada si hay evidencia de que son el mismo historial: AppID coincidente, al menos un ID con fecha idéntica y ninguna fecha contradictoria entre los IDs fechados compartidos. Las cuentas Steam conocidas diferentes y las coincidencias ambiguas se mantienen separadas. **AppID coincidente por sí solo no permite fusionar perfiles.** Si todas las fechas están vacías, se conservan las entradas separadas; no se descartan por ello.

Entre historiales cuya coincidencia sí se confirmó, gana el de más desbloqueos; luego, más fechas válidas; en empate exacto, el archivo directo. Origen indica el proveedor confirmado si se pudo relacionar con el directo; Fuente identifica la base PA y su proveedor o el archivo directo realmente elegido. El historial completo de la fuente ganadora se usa sin mezclar fechas de fuentes distintas.

Los planes anteriores de una fuente coincidente se conservan. Si no existe plan propio de la nueva fuente, se puede cargar el de la fuente relacionada. Se restaura por IDs, manteniendo selección y tiempos; los logros nuevos quedan desmarcados. El plan viejo no se sobrescribe al guardar uno de la nueva fuente.

## Fechas vacías o inválidas

**No se pierde un desbloqueo PA por falta de fecha.** Se conserva con `OriginalUnix = 0` y se muestra **Sin fecha**; no se interpreta como 1970 ni se inventa una fecha histórica. Tampoco pierde prioridad frente a una lista más pequeña con fechas completas.

Para preparar el nuevo plan se ordenan primero los logros fechados y después los no fechados por ID. La duración sugerida añade 60 segundos por intervalo desconocido. El aviso del plan lo explica; se puede cambiar Duración automática, Espera nueva, Momento nuevo, orden y selección. Estas esperas son del plan futuro y no convierten el historial en una supuesta sesión real. Para un único logro, se aplica la espera inicial como siempre. Guardar/cargar el plan y reiniciar conservan los logros sin fecha.

Antes de ejecutar, la verificación actual de IDs con SAM sigue aplicándose. Un logro personalizado, retirado o de otra plataforma puede conservarse y mostrarse, pero un ID seleccionado ausente del esquema actual no se envía a Steam.

## Snapshots y biblioteca

PA conserva un primer `snapshot.json` y actualiza por separado `pa-current.json` al importar o escanear. Así una primera importación con pocos desbloqueos no limita los escaneos futuros. El catálogo carga la vista actual sin consultar PA en cada arranque.

Si se confirma continuidad con el progreso actual de un archivo directo que creció, se conserva su snapshot original y se crea una vista separada `source-current.json` para comparar el progreso actual. Los siguientes escaneos de esa fuente actualizan esa vista; no reescriben el original. Restaurar original en el editor restablece el historial importado que sirve de base al borrador actual, sin tocar ninguno de los archivos de origen.

La casilla PA controla lectura, no visibilidad. Los filtros de Biblioteca usan el Origen visible/manual; una entrada PA identificada como Goldberg aparece bajo Goldberg. Desactivar PA no borra lo que ya se importó. Borrar biblioteca elimina el catálogo, no bases de Playnite, snapshots, planes, orígenes ni rutas recordadas.

## Compatibilidad comprobada

- Original Justin Delano 4.1.1, commit `002eaa8b2cdcc5df4c6f4bf2d886c5fbbc2390b2`: tablas relevantes y vínculo Manual compatibles.
- Santodan 4.1.1.1, commit `4af6fb93367fe959868a79b077c09cca6695ef39`: mismas tablas relevantes; datos adicionales del fork se ignoran.
- Pruebas automáticas con bases sintéticas, payloads de esquemas 8 y 9 y transacciones en WAL.
- Prueba local del lector C# con bases reales proporcionadas por el usuario, sin publicarlas. La interfaz WPF y SQLite nativo de Windows requieren prueba final en Windows; no se ha probado una base real independiente del original de Justin Delano.
