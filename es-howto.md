# Cómo usar Achievement Timeline Replay

Esta guía explica cómo recuperar un historial de logros y preparar su migración a Steam. No necesitás programar ni compilar el proyecto.

## 1. Prepará lo necesario

- El paquete de ATR, descargado desde [el repositorio del proyecto](https://github.com/ramirobornetto/atr/releases/latest).
- **Steam**, con la cuenta de destino iniciada.
- [**SAM Rewritten**](https://github.com/PaulCombal/SamRewritten), con el archivo `samrewritten-cli.exe` disponible.
- Un historial compatible: archivos locales de logros, una caché de Playnite Achievements o una cuenta de Xbox/Epic con desbloqueos.

Extraé **todo el ZIP de ATR** en una carpeta y ejecutá `AchievementTimelineReplay.exe`. La carpeta principal contiene ese ejecutable y `Support`, con la documentación y las licencias. Las dependencias están integradas; no se incluye la carpeta de muestras.

Si usás ATR 0.18.3 o anterior, descargá el ZIP actual y extraelo en una carpeta nueva. El paquete de transición ya no se distribuye. Tu biblioteca y tus planes permanecen en el perfil de Windows. Desde 0.18.4 y las versiones compactas podés usar **Actualizar ATR**.

## 2. Configurá Steam y SAM

En la pestaña **Escaneo**:

1. En **Directorio Steam**, pulsá **Elegir carpeta…** y seleccioná la carpeta donde está instalado Steam.
2. En **Directorio SAM Rewritten**, seleccioná la carpeta que contiene `samrewritten-cli.exe`. ATR elige ese ejecutable automáticamente.
3. Pulsá **Validar** y comprobá que SAM se reconozca.

No selecciones la carpeta de un juego como directorio de Steam, ni la carpeta superior que contiene varias versiones de SAM: elegí la que tenga directamente el archivo CLI.

## 3. Elegí de dónde importar

Las casillas de **Fuentes disponibles** determinan qué se consulta durante el escaneo. Al pasar el mouse por una fuente local podés ver sus rutas predeterminadas.

| Fuente | Cuándo usarla |
| --- | --- |
| Steam | Para leer historiales locales de Steam. |
| SteamIdra/LuaTools | Para historiales cuyo AppID tenga un LUA asociado en la configuración local. |
| Goldberg / GSE | Para historiales `achievements.json` de esos formatos. |
| CODEX / RUNE | Para historiales `achievements.ini` compatibles. |
| CODEX/RUNE compatible | Para archivos compatibles importados manualmente cuya ruta no identifica CODEX ni RUNE. |
| Playnite Achievements | Para leer sus historiales guardados. |
| Xbox Live / Epic | Para consultar la cuenta conectada. |

**Desmarcar una fuente no borra lo que ya importaste.**

### Importar un archivo manualmente

Los botones de la derecha permiten elegir archivos fuera de las rutas habituales:

- **Goldberg:** seleccioná el `achievements.json` de estado, normalmente dentro de una carpeta con el AppID. El archivo homónimo de `steam_settings` contiene definiciones y se usa para completar nombres, no como historial de desbloqueos.
- **Codex/Rune:** seleccioná el `achievements.ini`, normalmente dentro de una carpeta con el AppID o su subcarpeta `remote`.
- **Playnite Achievements:** cerrá Playnite y seleccioná `achievement_cache.db`. Si usás seguimiento manual, conservá `game_custom_data.db` junto a esa caché.


### Conectar Xbox Live

1. Pulsá **Conectar Xbox** y después **Conectar cuenta Microsoft**.
2. Completá el ingreso y la autorización en la página oficial de Microsoft. Si se solicita un código, usá el que muestra ATR.
3. Volvé a ATR y esperá la confirmación de conexión.
4. Dejá marcada **Xbox Live** y realizá un escaneo.

Solo se recuperan juegos con desbloqueos accesibles para la consulta de ATR. Un juego sin información disponible puede omitirse aunque figure en tu biblioteca de Xbox.

### Conectar Epic Games

1. Pulsá **Conectar Epic** y después **Iniciar sesión en Epic**.
2. Ingresá en la página oficial que se abre y completá las verificaciones de tu cuenta.
3. La conexión se completa automáticamente. Si el ingreso terminó y la ventana sigue esperando, usá **Continuar después de ingresar**.
4. Dejá marcada **Epic** y realizá un escaneo.

Solo se recuperan juegos con desbloqueos accesibles para la consulta de ATR. Un juego sin información disponible puede omitirse aunque figure en tu biblioteca de Epic.


## 4. Escaneá y elegí un historial

Pulsá **Escanear biblioteca**. Al terminar, abrí **Biblioteca**.


<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/d338de72-4cc7-47e6-b9b5-53bd35604e51" />





- Buscá por nombre o ID del juego.
- Usá los filtros de origen para reducir la lista. Podés activar varios.
- Revisá **Fuente**, **Logros**, disponibilidad del archivo y estado del snapshot.
- Hacé doble clic en un juego para abrir su **Plan de desbloqueo**.

Si hay varias fuentes para un mismo juego, **Historial de origen** permite elegir una. La opción automática prioriza el historial con más desbloqueos. **No mezcla los logros de todas las fuentes.**

<img width="405" height="138" alt="image" src="https://github.com/user-attachments/assets/5e56d204-e0e6-4634-91d3-bf35aaaba918" />


Cambiar el campo **Origen** corrige una etiqueta de clasificación; no convierte el historial a otra plataforma ni busca equivalencias.

<img width="250" height="387" alt="image" src="https://github.com/user-attachments/assets/c6bfd083-1d3e-4e63-9537-43372abcf08c" />


**Actualizar biblioteca** vuelve a escanear. **Quitar de la biblioteca** retira la entrada seleccionada del catálogo; el botón de papelera borra el catálogo guardado completo. Esas acciones no borran los archivos originales de logros ni los planes y snapshots de ATR. Un nuevo escaneo puede volver a mostrar las fuentes que sigan disponibles.

## 5. Si el historial viene de Xbox o Epic, prepará las equivalencias

**SI NO POSEES UNA COPIA DEL JUEGO EN STEAM, SAM DARÁ UN ERROR**
1. En el Plan del juego de origen, pulsá **Preparar equivalencias con Steam…**.
2. Buscá el juego por nombre y elegí la edición correcta. También podés introducir su **AppID Steam**, el número que aparece en su URL de la tienda.
3. Elegí el idioma y pulsá **Cargar logros Steam**.
4. Compará cada logro de origen con el destino propuesto: nombre, descripción, requisitos y edición/DLC.
5. En **Equivalente Steam**, corregí la propuesta si hace falta o dejá **Sin equivalente**.
6. Marcá **Confirmar** en las asociaciones revisadas. La casilla del encabezado permite marcar o desmarcar todas las asociaciones válidas y sin destino repetido: revisalas antes de usarla.
7. Marcá la confirmación de juego/edición y pulsá **Preparar plan Steam**.

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/88425bbc-d694-4412-a34a-e521e122a367" />



Solo pasan al plan las correspondencias confirmadas y válidas. **Guardar correspondencias** permite conservar el trabajo para continuarlo después. Una sugerencia no garantiza que dos logros pidan lo mismo, y dos logros de origen no pueden apuntar al mismo logro de destino.

Los historiales que ya contienen códigos de logros de Steam compatibles pueden pasar directamente a la preparación del plan, sin esta conversión entre plataformas.

## 6. Ajustá el Plan de desbloqueo

### Selección y estado de Steam

Marcá los logros que querés incluir. Pulsá **Consultar estado en Steam** para ver su estado actual: los que ya estén desbloqueados se desmarcan. El historial importado conserva sus datos originales.


### Tiempos

| Control | Qué hace |
| --- | --- |
| Duración automática | Ajusta los intervalos automáticos del plan a la duración elegida. |
| Limitar esperas automáticas largas | Pone un máximo a cada espera automática; los tiempos manuales tienen prioridad. |
| Espera nueva | Permite definir la espera de ese logro respecto del anterior seleccionado. |
| Momento nuevo | Permite situar un logro en un momento relativo del plan. |
| Espera inicial | Añade una pausa antes de comenzar. |
| Limpiar tiempos manuales | Elimina los ajustes manuales de intervalos del borrador. |

Podés escribir tiempos como `00:30:00`, `90m` o `1h 15m`. Revisá siempre la **duración efectiva**, porque los límites y las esperas manuales pueden hacer que difiera de la duración automática indicada.

<img width="551" height="175" alt="image" src="https://github.com/user-attachments/assets/0800aabe-c3f6-45c5-ac2e-bfd5b2f843cd" />


<img width="548" height="179" alt="image" src="https://github.com/user-attachments/assets/10fc9d8e-e580-4b62-b3b4-2f1ff1d450d3" />


<img width="537" height="179" alt="image" src="https://github.com/user-attachments/assets/73f54ea0-8586-428c-8f4c-b09e54655d5f" />



Las pausas históricas pueden incluir días sin jugar. No representan horas jugadas. Los logros sin fecha se conservan en los formatos que permiten identificar su desbloqueo; los tiempos sugeridos del plan no son fechas históricas recuperadas.

### Opciones adicionales

- **Modo reparación: bloquear antes:** bloquea los logros seleccionados antes de volver a desbloquearlos. Para una migración normal, dejalo desactivado. Puede cambiar el estado y la fecha oficial de logros ya obtenidos.
- **Registrar tiempo de juego (SAM idle):** intenta mantener el juego activo durante la migración para que Steam contabilice tiempo. No reconstruye horas históricas ni garantiza cómo las contará Steam.


### Guardá tu trabajo

**Guardar plan** conserva selección, orden, tiempos y opciones. **Cargar guardado** recupera esa configuración. **Restaurar original** descarta los cambios del borrador y vuelve al historial importado; no modifica Steam ni borra el plan guardado.

## 7. Revisá y ejecutá

**SI NO POSEES UNA COPIA DEL JUEGO EN STEAM, SAM DARÁ UN ERROR**
1. Entrá en **Ejecución** y pulsá **Iniciar migración**.
2. ATR consulta Steam y presenta un resumen. Revisá juego, AppID, cuenta detectada, historial, pendientes, ya desbloqueados y parámetros.
3. Si hay códigos de logros ausentes, volvé al plan y corregí la selección o las correspondencias.
4. Confirmá el resumen para comenzar. Si el estado cambió durante la revisión, ATR puede pedir una nueva confirmación.
5. Seguí el progreso en Ejecución. **Cancelar migración** detiene los siguientes pasos; no revierte los logros ya aplicados.

Solo puede ejecutarse una migración por vez. Mientras corre, podés preparar otros juegos.


<img width="1170" height="673" alt="image" src="https://github.com/user-attachments/assets/6b4d5b97-9fbf-4199-b63c-0d6693d972df" />


**Los desbloqueos de Steam reciben la fecha real en que se ejecutan.** La duración del plan organiza cuándo se realizan, pero **no permite escribir fechas del pasado.**

## 8. Revisá el resultado o retomá una ejecución

El informe diferencia logros ya desbloqueados, aplicados, pendientes, fallidos y operaciones sin verificación final. Usá **Copiar informe** para conservar o compartir el detalle.

Si hubo una interrupción:

1. Seleccioná el mismo juego e historial.
2. Abrí Ejecución y pulsá **Reanudar última migración…**.
3. Revisá el plan recuperado y pulsá **Iniciar migración**.
4. Confirmá los pendientes después de la nueva consulta a Steam.

La recuperación verificada usa el CLI moderno de SAM. ATR vuelve a consultar Steam: el informe local por sí solo no prueba que un logro se haya aplicado. Al recuperar se desactiva reparación para evitar volver a bloquear lo obtenido.

## 9. Entendé las omisiones y errores

| Mensaje o situación | Cómo interpretarlo |
| --- | --- |
| Sin logros desbloqueados con fecha válida | El archivo local no aportó fechas utilizables para importar. Puede ser una caché vacía; no demuestra por sí solo que la cuenta no tenga logros. |
| Sin catálogo | El servicio no proporcionó un catálogo de logros utilizable para esa entrada. |
| Sin progreso disponible | No se recibió el progreso; no equivale a cero desbloqueos. |
| Sin desbloqueos | La respuesta consultada no contenía desbloqueos para importar. |
| Historial o fechas sin correspondencia | Hay registros que no se pudieron asociar con las definiciones actuales; no se envían al plan automáticamente. |
| Variante INI no admitida | El archivo no coincide con los formatos que ATR sabe interpretar. |
| `Steam connection failed` | SAM no pudo completar la consulta. Por sí solo, el mensaje no identifica la causa ni prueba que falte una licencia. |

En **Logs** podés consultar el detalle. Si pedís ayuda, indicá la versión de ATR, el juego, la fuente y el error exacto. No compartas códigos de acceso ni archivos de sesión de las cuentas.

## 10. Otros

Los datos del usuario se guardan en:

```text
%LOCALAPPDATA%\AchievementTimelineReplay
```

Conservá una copia de esa carpeta para respaldar biblioteca, planes, correspondencias, snapshots y ejecuciones. Los archivos de origen que estén en otras carpetas se respaldan por separado. Las sesiones protegidas de Xbox/Epic están asociadas al usuario de Windows y pueden requerir una nueva conexión al cambiar de PC.



## Idioma de la interfaz

En la barra inferior, junto a la versión, podés elegir **Español**, **English** o **Português (Brasil)**. La elección se guarda y un aviso permite **Reiniciar ahora** o **Después**. El inglés es el idioma predeterminado cuando no existe una elección guardada. No interrumpe una migración ni cambia los nombres y descripciones de tus logros. El idioma de los textos de Steam se elige por separado en la ventana de correspondencias.
