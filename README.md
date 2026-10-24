# Achievement Timeline Replay

## Descargar v0.17.2 para Windows x64

[Descargar ZIP con ejecutable y .NET incluido](downloads/ATR_v0.17.2_Windows_x64.zip)

Extraer **todo el ZIP** en una carpeta nueva y abrir `AchievementTimelineReplay.Wpf.exe`. No se necesita compilar ni instalar .NET. Mantener las DLL y demás archivos junto al ejecutable. Steam y SamRewritten se seleccionan en la aplicación, como antes; no vienen incluidos.

Aplicación Windows para revisar y reproducir historiales locales de logros de Steam mediante SamRewritten.

## Compilar y abrir en Windows

1. Instalar el SDK de .NET 8.
2. Ejecutar `WpfPrototype\build-wpf.cmd`.
3. Ejecutar `WpfPrototype\run-wpf.cmd`.

Steam y SamRewritten son necesarios para consultar o modificar logros; no son necesarios para compilar.

## Corrección de compilación

Se corrigió CS0136 en `WpfPrototype/Core/LegacyEngine.cs`: la variable del nombre público del juego ahora se llama `appName`, evitando el conflicto con el contador `n` del mismo método. El resto de la lógica no cambia.

Validado con SDK 8.0.426 en Linux:

```sh
dotnet build WpfPrototype/AchievementTimelineReplay.Wpf.csproj -c Release -p:EnableWindowsTargeting=true
```

Resultado: 0 errores, 2 advertencias existentes (CS0169 y CS0649). La interfaz WPF y la integración con Steam/SamRewritten requieren validación en Windows; no se ejecutaron migraciones ni se modificaron cuentas.

`Legacy_v0.5.3` conserva los cinco archivos originales byte a byte. Las instrucciones históricas están en `README_PRIMERO.md`, `PARIDAD_V053.md` y `PRUEBAS_WINDOWS.md`; sus notas sobre compilación corresponden al paquete recibido, antes de esta corrección.

La captura de referencia visual no representa la interfaz actual ni cambios realizados en esta corrección.

## v0.6.1 — ajustes de uso

- Arrastrar desde una columna de texto activa el orden personalizado. Soltar en la mitad superior/inferior inserta antes/después. Las flechas se conservan; las casillas y las celdas de tiempos no inician arrastre. Arrastrar cerca del borde permite desplazar la tabla.
- Casillas de inclusión de un clic, conservando acceso con teclado.
- Botones con acento eléctrico, texto oscuro al pasar el cursor y estado de presión propio.
- Separadores arrastrables para ajustar el ancho entre tabla y detalles y la altura de la tabla; los detalles tienen scroll.
- Biblioteca: históricos / total del esquema local. Al consultar Steam se actualiza a desbloqueados actuales / total devuelto por SAM. Los históricos del snapshot no se reemplazan por el estado de Steam.

Compilación cruzada y pruebas de datos sintéticos:

```sh
dotnet build WpfPrototype/AchievementTimelineReplay.Wpf.csproj -c Release -p:EnableWindowsTargeting=true
bash tests/run-core-checks.sh
```

Las pruebas comprueban conteos parciales/completos/vacíos, grupos sin caché, exclusión de estadísticas normales y conservación de snapshots existentes. El script de pruebas usa un directorio temporal aislado y requiere Bash/Linux y .NET 8. Los gestos y colores requieren comprobación manual en Windows.

### Comprobación manual de v0.6.1 en Windows

1. Compilar y abrir con los `.cmd` habituales, escanear y seleccionar un juego.
2. Con orden personalizado desmarcado, arrastrar una fila desde su nombre arriba/abajo. Verificar inserción antes/después y las flechas; restaurar orden.
3. Marcar/desmarcar una casilla de una fila sin seleccionar con un solo clic. Verificar el contador de selección y comprobar también con la barra espaciadora.
4. Editar una espera o momento: no debe iniciar un arrastre. Verificar que las consultas Steam conservan el plan, la selección y los tiempos manuales.
5. Pasar el cursor, presionar y navegar con teclado por los botones: texto legible en todos los estados.
6. Arrastrar el separador entre tabla y detalles. Reducir/ampliar la ventana y comprobar scroll y columnas.
7. Comparar el contador histórico con el esquema y, después de Consultar Steam, con el estado actual. No iniciar migraciones reales para estas comprobaciones.

### Límite opcional de esperas largas

En el plan, activar **Limitar esperas automáticas largas** y elegir el máximo en minutos (valor inicial 30; opción desactivada por defecto). El límite se aplica a cada intervalo automático después del escalado de duración, incluyendo intervalos entre días diferentes. Los tiempos manuales tienen prioridad y pueden superar el máximo. La duración efectiva y las horas estimadas reflejan el límite; la confirmación muestra el máximo elegido. Un máximo inválido impide iniciar la migración. El historial y los snapshots originales no cambian.

El signo **?** explica la diferencia entre tiempo calendario y tiempo jugado. No se infieren sesiones reales ni se cambian timestamps históricos en Steam. No se agregan funciones para repartir logros entre días.

Prueba manual adicional: comparar el mismo plan con el límite desactivado/activado, probar una espera manual superior al límite y comprobar que los valores inválidos impiden iniciar. Desactivar el límite debe recuperar el escalado automático completo. Las pruebas del núcleo cubren estas reglas con intervalos de 18 horas y 3 días.

## v0.6.2 — consulta Steam, ayudas y navegación durante la ejecución

- Consultar Steam desmarca los logros seleccionados que SAM informa como desbloqueados, con o sin fecha. Los demás conservan su selección. No se cambian las fechas históricas, los tiempos manuales ni los snapshots.
- En modo normal, se comprueba de nuevo antes de ejecutar. Si se detectan logros ya desbloqueados, se desmarcan y se devuelve al plan para revisarlo y confirmar nuevamente. Si la respuesta no se puede interpretar, no se inicia. El modo reparación permite bloquear y volver a desbloquear logros que se hayan seleccionado explícitamente; puede cambiar su fecha oficial.
- Ayudas **?** para espera inicial, modo reparación y SAM idle.
- Durante una migración se puede buscar y seleccionar otros juegos, desplazar tablas y seleccionar logros para leer sus detalles. Las casillas, tiempos y orden quedan bloqueados hasta terminar o cancelar. Se conserva en memoria el plan de cada juego al navegar; el plan en marcha, sus fechas previstas y la pantalla de ejecución siguen asociados al juego original. No es guardado persistente entre cierres.
- Paquete Windows x64 autónomo con runtime .NET 8 incluido. El código fuente sigue disponible y `WpfPrototype/publish-windows.cmd` permite regenerar la carpeta distribuible desde Windows con el SDK de .NET 8.

### Verificación de v0.6.2

Las pruebas del núcleo incluyen desbloqueados con y sin fecha, conservación de selecciones previas y tiempos, consultas repetidas, reparación explícita, límites de espera y conservación de snapshots. El ejecutable Windows se genera por compilación cruzada; su inicio y los gestos WPF requieren comprobación en Windows.

Pruebas manuales adicionales:

1. Consultar Steam: los ya desbloqueados deben quedar desmarcados, los bloqueados conservar su selección y los tiempos manuales mantenerse. Repetir la consulta.
2. Pasar el cursor por cada **?** de las tres opciones y leer su explicación.
3. Durante una espera del plan, desplazar la biblioteca, filtrar, seleccionar otro juego y leer sus logros. Volver al juego original: conservar selección, orden y tiempos. La pantalla Ejecución debe conservar el juego original, progreso y cancelación.
4. Comprobar que se puede seleccionar una fila y desplazarse, pero no editar casillas, tiempos ni orden durante la ejecución.
5. Extraer el paquete autónomo completo en una carpeta nueva y abrir el .exe directamente, sin ejecutar build-wpf.cmd.

## v0.6.3 — preparar otros juegos y guardar planes

Mientras un juego se ejecuta, se puede consultar Steam y editar selección, orden, duración, tiempos manuales, límite de esperas, espera inicial, reparación e idle de **otros juegos**. El juego activo sigue en modo lectura y no se puede iniciar una segunda migración. Las opciones son independientes para cada juego al navegar.

- **Guardar plan** conserva un plan por cuenta y juego en `%LOCALAPPDATA%\AchievementTimelineReplay\Plans\<cuenta>_<appid>.json`. Guardar de nuevo reemplaza ese plan mediante escritura temporal y reemplazo del archivo. No escribe snapshots ni modifica Steam.
- **Cargar guardado** recupera el plan después de cerrar/reabrir el programa y escanear. Pide confirmación antes de reemplazar el borrador. Los logros se relacionan por ID: si hay diferencias con el historial actual, avisa, omite los desaparecidos y agrega los nuevos desmarcados. Revisar y consultar Steam antes de ejecutar.
- **Restaurar original** devuelve selección, orden e intervalos del historial importado, limpiando tiempos manuales y restaurando opciones por defecto. Pide confirmación. No borra el plan guardado; para reemplazarlo con el original hay que pulsar Guardar plan después.
- El indicador de guardado permite ver si el borrador coincide con el archivo guardado. Las modificaciones se conservan en memoria al navegar, pero necesitan Guardar plan para sobrevivir al cierre.

Los planes guardan intervalos relativos y las fechas originales proceden siempre del historial protegido. No guardan fechas futuras absolutas ni el progreso de una ejecución para reanudarla. Solo se conserva un plan guardado por cuenta/juego en esta versión.

Las consultas de otros juegos se coordinan con los comandos de desbloqueo y restauran SAM idle para el juego activo después de consultar. Una consulta puede demorar brevemente un desbloqueo o la cancelación si coinciden; su proceso tiene un timeout de 15 segundos, además de la restauración de idle. La contabilización real de Steam debe validarse en Windows.

Pruebas adicionales: guardar un plan con orden personalizado y todos los parámetros, cerrar/reabrir, escanear y cargarlo; restaurar el original sin borrar el guardado; preparar/consultar B mientras A está en marcha, volver a A y verificar que conserva su plan. Comprobar también que Cancelar detiene A aunque se esté viendo B. Las pruebas automáticas usan historiales y un CLI SAM simulado; no modifican cuentas reales.

## v0.6.4 — total de logros de Biblioteca

La columna Logros conserva el total conocido del juego al consultar Steam. Una respuesta parcial de 16 logros desbloqueados mantiene **16 / 40** si el esquema contiene 40, y no cambia a 16 / 16. Si Steam informa una lista más grande que el esquema local, se recuerda ese total para las siguientes consultas. El cuadro flotante de toda la tabla de Biblioteca se eliminó.

Fuente identifica dónde se leyó el historial (actualmente Steam local), no acredita la licencia ni identifica la herramienta usada para obtener el juego. Los archivos UserGameStats/Schema y la aparición en list-apps no permiten distinguir de forma fiable una instalación oficial de Steamidra. Los importadores futuros identificarán el formato/proveedor cuando existan pruebas suficientes; los formatos compartidos necesitarán una etiqueta genérica.

## v0.6.5 — origen manual en el plan

En **Plan de desbloqueo**, el campo **Origen (manual)** permite elegir Steam oficial, Steamidra, Goldberg, CODEX, RUNE u OnlineFix, además de Otro y Sin clasificar. El desplegable está junto al nombre del juego, que reemplaza el título genérico del plan. La elección se guarda automáticamente por cuenta y juego, independientemente de Guardar plan. Se almacena en `%LOCALAPPDATA%\AchievementTimelineReplay\Origins\<cuenta>_<appid>.json` con la indicación explícita `manual`. Elegir Sin clasificar elimina esa asignación. No modifica historiales, snapshots, planes guardados ni Steam. Restaurar original o cargar un plan no borra el origen.

Biblioteca muestra la columna **Origen** en modo lectura; solo se edita desde el desplegable en Plan de desbloqueo y se actualiza al elegir el origen. La columna **Fuente** describe el archivo leído: actualmente **UserGameStats (.bin)**. Esa etiqueta no pretende verificar una licencia ni afirmar qué herramienta obtuvo el juego. Las opciones Goldberg/CODEX del origen son etiquetas manuales y no habilitan importadores de esos formatos.

En el futuro, reglas de directorios conocidos podrán sugerir un origen detectado, separado de las asignaciones manuales y sin reemplazarlas. Esta versión no detecta orígenes automáticamente ni incorpora importadores nuevos.

Comprobación manual: elegir origen para A, cambiar a B y comprobar que no se hereda; volver a A y reiniciar/escanear para verificar persistencia. Restaurar el original y cargar un plan deben conservar ese origen. El campo queda bloqueado en el juego activo durante una migración, igual que las demás ediciones del plan.


## v0.7.0 — importación Goldberg / GBE

- Lectura de `achievements.json` de usuario: solo desbloqueos con fecha Unix válida, sin ejecutar ni modificar emuladores.
- Búsqueda opcional en las carpetas habituales de Goldberg y GBE, más importación manual de archivos en otras rutas, independiente del escaneo Steam.
- Definiciones opcionales de `steam_settings/achievements.json`, con nombres y descripciones localizados. Alternativa: esquema local Steam o IDs API.
- Fuente por archivo y origen detectado corregible manualmente. Historiales, snapshots, planes y etiquetas independientes por fuente, incluso para un mismo AppID.
- Muestra sintética incluida: `muestras/Goldberg/480/achievements.json`, cuatro logros con fecha de seis conocidos. Sus acciones de Steam están bloqueadas.

Instrucciones, formato, persistencia y limitaciones: [GOLDBERG.md](GOLDBERG.md). No hace falta tener una emulación para probar la muestra. Compilación WPF y publicación autónoma Windows x64 verificadas; 68 comprobaciones automáticas pasadas, incluyendo 21 de Goldberg y la muestra distribuida. Pendiente prueba visual en Windows y validación con archivos de una instalación real.


## v0.7.1 — diagnóstico de escaneo y biblioteca

El resultado del escaneo desglosa Steam y Goldberg/GBE: revisados, recuperados, sin logros con fecha válida, sin esquema, errores de lectura, errores de snapshot, duplicados e IDs inválidos. Las categorías suman los candidatos revisados; los archivos de otras cuentas filtrados previamente se informan aparte. Logs identifica cada descarte. Esto permite investigar diferencias como 272 candidatos / 112 recuperados sin atribuirlas todas a falta de logros.

Biblioteca muestra flechas ascendentes/descendentes en las columnas que ordenan la vista; comienza por nombre ascendente. Se conserva el orden y el redimensionado nativos de WPF. Bajo el nombre del juego, el plan muestra en cursiva la ruta completa del archivo leído, con acceso al texto completo al pasar el cursor.

Origen se completa como Steam o Goldberg por la fuente detectada. Para historiales Steam, un archivo coincidente `Steam/config/stplug-in/<AppID>.lua` produce **Steamidra (indicio LUA)**. No se ejecuta el LUA ni se interpretan IDs internos como procedencia probada; un juego oficial también puede tener uno. La explicación aparece en el plan. Las asignaciones manuales guardadas siempre tienen prioridad; quitarlas vuelve a la detección automática.

Goldberg resuelve nombres mediante la caché de nombres públicos (incluidos los obtenidos con SAM) y consultas a Steam Store por AppID. No usa el `gamename` interno del esquema como título público: puede ser un nombre de desarrollo como Steel Magnolia. Si no hay nombre disponible, muestra AppID y registra el motivo. Las consultas tienen cancelación, timeout y se detienen ante bloqueo o límite HTTP.

La muestra sigue siendo reconocida como sintética al copiarla bajo otro AppID, aunque no se copie `.atr-demo`, porque sus IDs `ATR_DEMO_*` la identifican. Mantiene sus seis logros y no incorpora el total de un esquema real ajeno. Los snapshots sintéticos de 0.7.0 se pueden abrir manteniendo el modo muestra, sin reescribirlos. Las acciones de Steam permanecen bloqueadas. AppID 480 está admitido por el lector y por la búsqueda habitual; cualquier omisión se puede investigar con el nuevo desglose y Logs.


Validación de 0.7.1: 83 comprobaciones automáticas pasadas con datos aislados, incluidos contadores reconciliados, inferencia por LUA, nombres públicos por AppID y copias de la muestra. Compilación y publicación autónoma Windows x64 verificadas. La presentación visual de WPF y las consultas reales de nombres requieren comprobación en Windows; las pruebas no modifican cuentas de Steam.


## v0.8.0 — CODEX/RUNE, muestras AAA y actualizador automático

- Importación manual y búsqueda en carpetas conocidas de `achievements.ini` compatible con CODEX/RUNE, con estados y fechas explícitos y snapshots/planes separados por archivo.
- Veinte carpetas AAA sintéticas, cinco para Goldberg, cinco GSE, cinco CODEX y cinco RUNE. Cada una muestra 4 / 6 y bloquea acciones de Steam; incluye nombres sin conexión, catálogo y JSON de referencia para los INI. [ZIP solo de muestras](downloads/ATR_Muestras_AAA_v0.8.0.zip).
- Clic en la ruta del plan abre el Explorador seleccionando el archivo; si falta, abre su carpeta si todavía existe.
- Fechas históricas sin definición en el esquema se informan por separado, incluso cuando se recupera parte del historial. No se generan destinos `bit_N` sin ID API. Comprobado con los dos archivos de CS:GO/CS2 enviados por el usuario: 22 fechas sin correspondencia y un logro actual definido. Esos archivos privados no forman parte de la publicación.
- Origen Steam oficial retirado; sus asignaciones se muestran como Steam. Steamidra y su indicio LUA pasan a SteamIdra/LuaTools, conservando compatibilidad con etiquetas guardadas.
- Botón **Actualizar ATR**: consulta GitHub, descarga/verifica el ZIP, conserva borradores, cierra, instala y reinicia; incluye copia anterior y restauración ante fallos de instalación. La primera instalación de esta versión sigue requiriendo extraer el ZIP completo.

Instrucciones y límites: [CODEX_RUNE.md](CODEX_RUNE.md) y [ACTUALIZACIONES.md](ACTUALIZACIONES.md). Pendiente validar visualmente WPF, guardados INI reales y una actualización completa entre versiones en Windows; las pruebas automáticas usan archivos aislados y HTTP simulado, sin modificar cuentas de Steam.


Verificación de 0.8.0: 188 comprobaciones automáticas pasadas. Se probaron descarga con SHA256, rechazo de ZIPs inválidos, extracción aislada, instalación con preservación de archivos ajenos, rollback tras fallo intermedio y borradores separados de planes explícitos. ATR y el auxiliar se publican para Windows x64 con runtime incluido. El cierre/reinicio real, UAC y Explorer siguen pendientes de prueba en Windows.


## v0.8.1 — biblioteca persistente y categoría Lua

Origen ofrece una única categoría **SteamIdra/LuaTools**, tanto para detección por archivo LUA coincidente como para asignación manual. Las etiquetas guardadas Steamidra (indicio LUA) y SteamIdra/LuaTools (indicio LUA) se leen como esa misma categoría; no se pierden sus asociaciones por cuenta/historial. La explicación sobre la incertidumbre del origen permanece en el texto del plan. Las muestras y las otras categorías no cambian.

Disponible mediante **Actualizar ATR** desde 0.8.0 o mediante el ZIP completo.

La biblioteca se guarda automáticamente después de un escaneo o importación y se carga al iniciar sin volver a escanear ni consultar Steam. El catálogo vive en `%LOCALAPPDATA%\AchievementTimelineReplay\Library\catalog.json`, con una copia anterior y escritura mediante archivo temporal. Contiene los metadatos/rutas; los logros originales se leen de sus snapshots protegidos. Se conservan los totales conocidos, la fecha del último escaneo y sus contadores, pero no se reutilizan estados oficiales de Steam como datos actuales.

**Actualizar biblioteca** revisa las rutas habituales y los archivos locales importados manualmente que recuerda el catálogo. Si falta una fuente o no puede reimportarse, conserva su historial con una indicación de disponibilidad; si el snapshot falta o está dañado, la fila queda marcada y no permite ejecutar el plan. Los borradores en memoria se conservan durante un refresco y un refresco cancelado no vacía la biblioteca anterior. **Quitar de biblioteca** elimina solo la referencia: no borra snapshots, planes, etiquetas ni archivos originales, y un escaneo puede volver a encontrar la fuente.

Antes de ejecutar se reevalúa la cuenta Steam conocida y se comprueba el estado actual con SAM. En el CLI moderno, los IDs seleccionados que ya no existan en el esquema actual bloquean la ejecución, incluso en modo reparación. Las muestras conservan sus bloqueos al cargarse desde el catálogo.

Después de instalar esta versión por primera vez, escanear o importar una vez para crear el catálogo; los reinicios siguientes lo cargan automáticamente. Los planes continúan guardándose explícitamente con Guardar plan. Más detalles en [BIBLIOTECA.md](BIBLIOTECA.md).


Verificación de 0.8.1: 212 comprobaciones automáticas pasadas, incluyendo catálogo persistente, fuentes ausentes, snapshots dañados, recuperación de copia anterior, cambio de cuenta y validación de IDs actuales. WPF y el auxiliar se publican para Windows x64. El arranque visual y una actualización real entre versiones siguen requiriendo comprobación en Windows.

## v0.8.2 — refresco de biblioteca y nombres

Un reescaneo completado quita de la biblioteca las fuentes cuyo archivo ya no existe, conservando planes, orígenes y snapshots. Cancelar o fallar el escaneo mantiene la biblioteca anterior. Los errores de lectura de archivos existentes conservan su entrada marcada.

Goldberg/GSE busca nombres automáticamente; se elimina la casilla previa a la importación. Si faltan nombres, el plan ofrece «Buscar archivo de definición…» con ayuda «?». La selección manual se valida por IDs y no cambia fechas ni parámetros del plan. Las etiquetas se guardan en el catálogo sin modificar los snapshots originales.

## v0.8.3 — fuentes y filtros independientes

Escaneo permite activar Steam, SteamIdra/LuaTools, Goldberg, GSE, CODEX y RUNE por separado. Los BIN se clasifican por presencia de `<AppID>.lua` en `Steam/config/stplug-in`: el LUA solo filtra, no aporta logros ni certifica origen o propiedad. Activar ambos incluye los BIN en una sola pasada. CODEX/RUNE compatible permite refrescar importaciones INI recordadas sin proveedor identificable por ruta. Desactivar un origen conserva su catálogo previo, incluso si su archivo no está disponible; no lo reimporta ni lo purga.

Biblioteca añade interruptores por origen con cantidades, búsqueda combinada y contador «Mostrando X de Y». Estos filtros usan el origen visible/manual y no escanean, importan ni borran. Las preferencias de escaneo y visibilidad se guardan por separado en `source-options.json`.

La papelera roja borra el catálogo y su respaldo tras confirmar. Su «?» explica que conserva archivos de logros, snapshots, planes y orígenes. Se deshabilita mientras hay operaciones activas. Los borradores sin guardar se descartan; las preferencias se mantienen.

## v0.9.0 — PlayniteAchievements e icono

Nuevo lector PA de Steam, Manual con fuente Steam y Local. Detección de la instalación habitual, importación de bases portables y rutas recordadas. Se elige el historial con más desbloqueos y se conservan logros sin fecha o con fecha inválida: «Sin fecha», con intervalos sugeridos editables en el nuevo plan. Las fechas originales no se inventan.

La comparación con archivos directos exige evidencia de historial coincidente y respeta perfiles ambiguos. Los primeros snapshots y planes se conservan; PA y las fuentes directas que pasan a seguimiento actual usan vistas de snapshot separadas. Nuevo icono del ejecutable y ventana proporcionado por el usuario. Ver [PA.md](PA.md) para criterios, formatos y pruebas de Windows.

## v0.10.0 — fuentes seleccionables y plataformas PA

Biblioteca muestra una fila por juego/perfil confirmado y conserva todas sus fuentes. Historial de origen permite selección automática por desbloqueos o manual, con cantidades y ayuda en Fuente. Se guardan preferencias, borradores y planes por fuente. Se corrige el caso Steam directo + PA/Steam + PA/Local equivalentes con GUIDs diferentes.

PA añade lectura de Xbox, Epic, GOG y PlayStation, bajo una única opción PA en Escaneo y con filtros por plataforma en Biblioteca. Los IDs externos nunca se envían como AppID Steam. Preparar equivalencias con Steam permite revisar sugerencias, confirmar asociaciones y guardarlas antes de crear el plan. Los destinos duplicados o ausentes se rechazan y los logros sin fecha se conservan. SAM sigue siendo quien acepta o rechaza operaciones; sus errores detienen el plan sin anunciar éxito.

Ver [PLATAFORMAS.md](PLATAFORMAS.md) para uso, agrupación, correspondencias y límites de validación.

## v0.11.0 — búsqueda de destino y comparación asistida

La ventana de correspondencias propone juegos Steam por nombre desde cachés locales y la tienda pública; se puede elegir la edición sin escribir el AppID. Se añaden textos localizados, sugerencias es/en con comprobación de cantidades/negaciones, motivo de cada propuesta y confirmación por lotes de coincidencias exactas o filas seleccionadas. Se respetan las elecciones guardadas y las exclusiones explícitas. Los IDs del esquema y la confirmación del usuario siguen siendo necesarios. Ver PLATAFORMAS.md.


## v0.12.0 — códigos visibles y Xbox directo

La tabla de correspondencias agrega **Código de origen** y **Código Steam**. La biblioteca distingue el historial original **Xbox** del preparado **Xbox → Steam**, manteniendo el filtro por origen.

Nuevo **Conectar Xbox…** y fuente opcional **Xbox (perfil)**, independiente de PA: conexión Microsoft por código de dispositivo, sesión protegida por Windows, lectura paginada de juegos/logros, copia persistente y correspondencias hacia Steam. Los desbloqueos sin fecha se conservan; los perfiles conocidos distintos se separan. El escaneo de PA sigue teniendo una sola opción.

**La conexión real de Xbox está pendiente de validar con un registro Microsoft propio de ATR.** No se entrega un Client ID ajeno. La configuración inicial, las limitaciones y las pruebas están en [XBOX.md](XBOX.md). No se necesita una suscripción de Azure para los recursos que usa este código; la aceptación de los permisos Xbox depende de Microsoft.


## v0.12.1 — historial Xbox Live sin entradas vacías

El escaneo Xbox omite títulos que, tras consultar sus logros, siguen con **0 desbloqueados y 0 de total**. Se conservan 0/35 y otros títulos con catálogo, además de cualquier desbloqueo real, incluso sin fecha. Las entradas Xbox directo 0/0 guardadas en versiones anteriores también se ocultan al cargar/actualizar la biblioteca; no se borran sus archivos, snapshots ni planes. Las fuentes Steam y PA no se filtran por esta regla.

La etiqueta visible pasa a **Xbox Live** y los planes convertidos a **Xbox Live → Steam**. Se mantienen las identidades, las correspondencias y los filtros existentes. No se infiere propiedad ni tienda de compra y no se mezclan logros Steam/Xbox durante el escaneo.


## v0.12.2 — Xbox Live solo con desbloqueos

Por preferencia del usuario, Xbox Live directo ahora incluye únicamente juegos con al menos un logro desbloqueado. Se omiten tanto 0/0 como 0/35 y similares, también al cargar el catálogo guardado. Los desbloqueos sin fecha válida cuentan y se conservan. No se borran archivos ni planes y esta regla no cambia las otras fuentes. Reemplaza el criterio de v0.12.1 que admitía títulos sin desbloqueos si tenían catálogo.


## v0.13.0 — revisión, reanudación e informes

Antes de ejecutar se consulta Steam y se muestra un resumen con juego/AppID, fuente, archivo, cuenta detectada, seleccionados, ya desbloqueados, pendientes y códigos ausentes. El CLI moderno vuelve a consultar después de confirmar; si cambian las acciones previstas se revisa el resumen otra vez.

Las ejecuciones conservan el plan y registran cada intento en Runs. **Reanudar última migración…** recupera el plan, desactiva reparación y consulta Steam antes de aplicar pendientes. El informe final muestra nombres/códigos, aplicados confirmados, omitidos, fallidos, pendientes y operaciones sin verificar; se puede copiar. Los planes guardados explícitamente se conservan.

La barra de escaneo ahora avanza por fuentes activadas, consolidación y guardado, y no se llena al terminar solo Steam. Detalles y límites: [MIGRACIONES.md](MIGRACIONES.md). Epic directo queda fuera de esta actualización.


## v0.13.1 — búsqueda de esquema Steam desde Xbox

Se corrige la búsqueda de definiciones para correspondencias: el JSON de un historial Xbox ya no se intenta abrir como base PA. Solo se consultan cachés achievement_cache.db de PA conocidas, detectadas o configuradas. Un esquema local ilegible o una caché corrupta se registran y permiten probar otras fuentes antes de SAM. Un error de conexión/licencia de SAM sigue siendo un error independiente; no se infiere su causa a partir del mensaje genérico.


## v0.14.0 — Epic directo y tabla de correspondencias

Los logros del desplegable de Steam se ordenan alfabéticamente por nombre. Se elimina la columna Fecha únicamente de esa ventana; las fechas originales siguen en Plan y en los snapshots. Las casillas de confirmación y las correspondencias guardadas se conservan.

Nuevo **Conectar Epic…** y fuente independiente **Epic (perfil)**, desactivada inicialmente. Inicio de sesión en el navegador oficial, código temporal y sesión protegida con Windows; lectura paginada de biblioteca, catálogo de logros y desbloqueos de la cuenta. Solo incluye títulos con desbloqueos, también sin fecha; mantiene perfiles separados y permite preparar correspondencias Steam. No modifica Epic ni lee las sesiones de otras aplicaciones.

La conexión utiliza un flujo compatible con el launcher, no requiere Entra y debe validarse con una cuenta real. No garantiza todos los logros EOS. Instrucciones y límites: [EPIC.md](EPIC.md). Las pruebas de servicios remotos usan respuestas simuladas.


## v0.14.1 — consultas Epic y diagnóstico de acceso

Las consultas Epic usan cabeceras compatibles con las del lector PA (cliente launcher, Accept JSON e idioma). Un 403 de un título no detiene los demás; un nombre de catálogo denegado no descarta desbloqueos ya leídos. Un 401 permite renovar la sesión una sola vez y repetir esa consulta; no hay reintentos automáticos para 403 ni límites 429.

Los mensajes identifican operación, código HTTP y, cuando existe, un código público de error Epic. No muestran cuerpos privados ni tokens. El resumen indica incidencias/fase detenida en lugar de presentar cero juegos como una consulta limpia. El flujo de navegador/código temporal no cambia. La validación real de logros sigue pendiente con la cuenta del usuario; la vinculación y lectura de biblioteca ya fueron confirmadas por él.


## v0.15.0 — conexiones simplificadas y resultados claros

Xbox usa el registro de ATR preconfigurado, sin mostrar Client ID ni pedir configuración al usuario. Abre Microsoft y permite copiar el código temporal. Epic incorpora una ventana WebView2 para capturar la autorización automáticamente; conserva la alternativa externa si el runtime o un proveedor no están disponibles. Al completar una nueva conexión se selecciona su fuente para el próximo escaneo; desconectar desactiva esa fuente sin borrar historiales.

Los resúmenes Xbox/Epic separan recuperados, sin catálogo, sin progreso disponible, sin desbloqueos, repetidos y errores reales. La ausencia de progreso Epic deja de recomendar cambios de privacidad sin evidencia. Los fallos de PA se informan como errores de lectura aparte de los historiales importados.

Se amplían pruebas de cancelación, reanudación ligada a cuenta, bloqueo de escrituras ante un fallo de checkpoint y conservación de biblioteca, sesiones, navegador, snapshots y planes al actualizar/volver atrás. Los cambios visuales generales quedan fuera de esta versión. Los nuevos flujos de ingreso aún requieren prueba real en Windows; ver XBOX.md, EPIC.md y PRUEBAS_WINDOWS.md.


## v0.16.0 — limpieza y distribución de la interfaz

Escaneo se organiza en dos columnas, con fuentes a la izquierda y botones de importar/conectar a la derecha. Se quitan los párrafos explicativos, las instrucciones de muestras y el bloque de estadísticas final; el progreso de la etapa se muestra mientras se escanea y el detalle queda en Logs. Los avisos de Xbox/Epic no conectados permanecen visibles; esas fuentes se omiten durante ese escaneo y las demás continúan.

Plan distribuye los controles inferiores en dos columnas, compacta el encabezado y separa la ruta de la tabla. El botón pasa a **Consultar estado en Steam** y la columna a **Estado en Steam**. Correspondencias unifica alturas de campos/botones y compacta los selectores de la tabla. La casilla del encabezado Confirmar permite marcar/desmarcar todas las equivalencias válidas; filas sin destino o con destinos repetidos quedan pendientes.

La interfaz usa **Playnite Achievements** en lugar de PA y elimina (perfil) de las etiquetas. Los filtros GOG, PlayStation y OnlineFix se retiran; sus datos siguen accesibles mediante Otro. Las claves de preferencias, identidades, sesiones y correspondencias se conservan. Se eliminan las leyendas del motor/snapshots de la barra lateral y del pie. La validación de distribución visual requiere Windows; las pruebas automáticas cubren compatibilidad de claves y selección masiva sin conflictos.

## Personalizar la interfaz

Los ajustes visuales comunes están comentados al comienzo de [`WpfPrototype/Appearance.xaml`](WpfPrototype/Appearance.xaml). Ver [la guía de apariencia](APARIENCIA.md) para cambiar medidas, colores y propiedades de un solo control desde Visual Studio.

## v0.16.1 — ajustes de Escaneo y Biblioteca

- Selección de carpeta de SAM Rewritten con resolución automática de `samrewritten-cli.exe`.
- Rótulos de directorios, botones más breves y ayudas al pasar el mouse.
- Acciones de Biblioteca junto al encabezado, contador debajo de filtros y búsqueda más compacta.
- Fuente sin el prefijo «Automático:».
- Apariencia compartida en `Appearance.xaml`, con altura normal de 34 y búsqueda de Biblioteca de 30; guía en `APARIENCIA.md`.

La mejora de los mensajes sobre cachés vacías y fechas de logros permanece pendiente.

## v0.16.2 — distribución de Biblioteca y Plan

- Actualizar y borrar biblioteca junto al título, sin alineación al extremo derecho.
- Encabezado sin subtítulo y logo de ATR en la parte superior de la barra lateral.
- Nombre y datos del juego en el encabezado solo en Biblioteca, Plan y Ejecución.
- Consultar estado en Steam y Abrir en SAM junto al nombre del juego en Plan.
- Selectores de origen e historial en la misma fila, más angostos y con altura 28.
- Opciones de origen tomadas de los mismos filtros de Biblioteca; categorías antiguas se muestran como Otro sin modificar datos guardados.
- Ayudas con demora de aparición de 300 ms en toda la aplicación.

## v0.16.3 — paneles compactos y logo nítido

- Escaneo: fuentes en dos columnas y acciones en otras dos, con bloque ajustado a su contenido y botón Goldberg con ancho natural.
- Plan: se ocultan las notas de borrador y origen guardado; los avisos de ejecución y errores de origen siguen disponibles. Campos numéricos más pequeños (alto 28).
- Notas explicativas dentro de las ventanas en cursiva; etiquetas, datos, títulos y ayudas emergentes conservan su formato.
- Logo lateral cargado desde PNG de 256 × 256, conservando los píxeles originales del icono en alta resolución.
- Título de ventana sin el sufijo WPF.

## v0.16.4 — orden y espaciado de controles

- Importaciones en la columna izquierda (Goldberg, Codex/Rune, Playnite Achievements) y conexiones a la derecha (Epic, Xbox).
- Campos de tiempos de alto 24 y menor ancho; espacio entre Espera inicial y su campo.
- Modo reparación y Registrar tiempo de juego en renglones propios debajo de Espera inicial.
- Logo lateral centrado y ampliado a 144 × 144.

## v0.17.0 — apariencia neón y paneles translúcidos

- Fondo de paisaje desenfocado, oscurecimiento suave y paneles oscuros semitransparentes sin reducir la opacidad de textos ni controles.
- Navegación lateral con iconos vectoriales más grandes, letra 15 y franja luminosa en la pestaña activa.
- Botones redondeados con transición de glow cian al pasar el mouse; acciones de borrar, quitar, limpiar, cancelar y desconectar con glow rojo.
- Desplegables oscuros con bordes redondeados y selección resaltada, incluidos los de correspondencias.
- Casillas delineadas con marca clara y estado parcial en los encabezados de selección múltiple.
- Importaciones con ancho compartido por columna; Epic y Xbox comparten su propio ancho.

Las funciones de importación, correspondencias y migración mantienen su comportamiento. La distribución y los efectos requieren revisión visual en Windows.

## v0.17.1 — cristal negro y tablas más suaves

- Contorno luminoso completo y uniforme en la pestaña activa, en lugar de una franja más intensa a la izquierda.
- Navegación con iconos de 20 y letra 14 para que entren los rótulos completos.
- Paneles negros translúcidos y oscurecimiento neutro del fondo, sin tinte azul añadido.
- Todas las tablas con filas negras/grises transparentes de contraste sutil, sin rejilla ni borde exterior claro.
- Contenido de las celdas centrado verticalmente, incluidos textos y controles de correspondencias.

- Escaneo: fuentes ordenadas de arriba abajo por columna; Xbox Live y Epic cierran la segunda columna.
- Acciones rojas: gradiente de rojo intenso a rojo oscuro, sin extremo blanco/rosado claro y con texto blanco.
- Fila seleccionada con contorno cian y glow, manteniendo textos y controles sin desenfoque.
- Plan: fila flotante que acompaña al cursor, reordenamiento inmediato de las demás filas y desplazamiento automático en los bordes. Escape o soltar fuera del Plan restaura el orden y el modo previos. Los tiempos se recalculan al soltar, conservando fechas, selección y esperas manuales.

## v0.17.2 — transparencia y separadores finos

- Bloques principales con 40 % de transparencia (60 % de opacidad), conservando el negro y la opacidad de su contenido.
- Separadores del Plan con línea visible de 1 unidad WPF y glow cian suave, tanto vertical como horizontal. La zona transparente de agarre conserva su ancho de 6 para facilitar el arrastre.
