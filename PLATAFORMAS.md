# Historiales y plataformas — ATR 0.10.0

## Una fila y varias fuentes

La biblioteca conserva todos los historiales importados en su catálogo y muestra una fila por grupo confirmado de juego/perfil. No se eliminan las alternativas al elegir una fuente. La columna Fuente indica `Automático: Steam directo · 3 fuentes` o `Manual: PA / Local · 3 fuentes`. Su globo detalla las cantidades de cada fuente.

El selector **Historial de origen** del plan permite automático o una fuente concreta. Automático elige la mayor cantidad de IDs desbloqueados entre los historiales utilizables, después más fechas válidas y, en empate, el archivo directo. La selección manual se guarda por fuente en `history-choices.json`, separado del catálogo. Si ya no está disponible la fuente elegida, se vuelve al automático.

Se conservan borradores por fuente y los planes guardados. Cambiar de fuente pide confirmación si hay ediciones o un plan guardado; regresar a la fuente anterior recupera su borrador. Un cambio automático tras actualizar se señala en el plan. Cuando el plan de una fuente equivalente sirve como alternativa, puede cargarse por IDs; sus archivos no se sobrescriben al guardar el de otra fuente.

Los identificadores de Playnite distintos ya no bastan para separar historiales equivalentes. Se agrupa por plataforma y juego, respetando cuentas/perfiles conocidos diferentes. Una misma cuenta Steam conocida o un mismo vínculo PA permiten reconocer el grupo. Cuando no hay esa información, se exige evidencia histórica: IDs con fechas idénticas y sin contradicciones entre los fechados compartidos. Los perfiles ambiguos, o historiales sin evidencia suficiente, se mantienen separados. No se suman listas ni mezclan fechas. Las cuentas Steam distintas no se unen mediante una fuente intermedia sin cuenta.

Las fuentes con cero desbloqueos pueden conservarse como alternativa de un vínculo PA que sí tiene otro historial desbloqueado. No habilitan la ejecución de un plan vacío. Los juegos sin desbloqueos en ninguna fuente no llenan la biblioteca.

El catálogo guarda todas las fuentes, no solo la ganadora, y al reiniciar reconstruye los grupos. **Quitar de biblioteca** quita el grupo y sus fuentes del catálogo; **Borrar biblioteca** quita todo el catálogo y su respaldo. Ninguno elimina originales, snapshots, planes, correspondencias ni preferencias.

## PA: Xbox, Epic, GOG y PlayStation hacia Steam

Los proveedores nativos Xbox, Epic, GOG y PSN/PlayStation de PA se leen desde las mismas bases locales de PA. Una sola casilla **PA** en Escaneo incluye todas las plataformas compatibles de sus bases. Los filtros de Xbox, Epic, GOG y PlayStation están separados únicamente en Biblioteca. Importar PA manualmente sigue siendo una petición explícita de lectura de todas las plataformas compatibles.

Los IDs de esas plataformas se conservan como identificadores externos. **No se usan como AppID Steam.** Un historial externo puede verse y editarse, pero no habilita Consultar Steam, Abrir SAM ni Iniciar migración. No se añaden conexiones ni escrituras directas a Xbox, Epic, GOG o PlayStation; PA debe haber obtenido y guardado el seguimiento.

Para preparar una migración:

1. Refrescar el proveedor de origen en PA, comprobar sus desbloqueos, guardar y cerrar Playnite.
2. Escanear el origen deseado o importar `achievement_cache.db`, con `game_custom_data.db` al lado para seguimiento manual.
3. Seleccionar el juego externo y pulsar **Preparar equivalencias con Steam…**.
4. Introducir el AppID correcto de Steam y pulsar **Cargar logros Steam**. ATR obtiene definiciones del esquema local, de la caché PA de Steam/Local o del CLI moderno de SAM. El AppID puede verse en la URL de la tienda Steam. Se muestra el nombre del destino cuando se dispone de él.
5. Revisar la edición, DLC, los nombres y ambas descripciones. Las sugerencias por nombre o descripción única quedan **Por revisar**, nunca confirmadas automáticamente. Se puede elegir un equivalente o dejarlo **Sin equivalente**.
6. Marcar **Confirmar** para cada asociación válida. **Guardar correspondencias** permite conservar trabajo pendiente; al abrir el mismo origen y cargar el mismo AppID se recuperan las asociaciones.
7. Confirmar que el AppID y la edición de destino son los correctos y pulsar **Preparar plan Steam**. Solo pasan las correspondencias confirmadas y existentes. Dos logros de origen no pueden apuntar al mismo destino. Si cambió el AppID, se debe cargar nuevamente el esquema.
8. Revisar el nuevo plan, sus tiempos y los estados actuales con Consultar Steam. Guardar plan conserva su configuración. Iniciar usa las mismas verificaciones y confirmación de ejecución de ATR.

La elección del plan recién preparado se marca como manual. Si hay una sola cuenta Steam compatible, ese plan puede ser otra fuente del juego de destino: la correspondencia confirmada constituye la asociación explícita. Con cuentas ambiguas se conserva separado. El historial externo de Xbox/GOG/etc. permanece en su propia plataforma; no se lo confunde con el juego Steam.

Las correspondencias se guardan en `%LOCALAPPDATA%\AchievementTimelineReplay\Mappings`, vinculadas a la identidad del historial de origen y AppID de destino. El plan convertido tiene sus propios snapshots e identidad. No se renombra ni altera el historial original. Los logros sin fecha siguen como Sin fecha; las esperas sugeridas son editables y no inventan fechas históricas.

Los desbloqueos de Steam reciben la fecha real de ejecución. La herramienta no puede escribir la fecha histórica de Xbox ni garantizar equivalencia entre ediciones: las asociaciones requieren revisión del usuario.

## Errores de SAM

No se añade una comprobación de licencia propia. ATR delega la operación en SAM y detiene el plan si SAM rechaza la consulta o modificación. Se reconocen códigos de salida, errores en texto y rechazos JSON, incluidos resultados individuales con código cero. Un rechazo no incrementa el progreso ni anuncia migración completada. Las respuestas de listado que indican que un logro está bloqueado no se tratan como errores.

## Validación y límites

Pruebas automáticas con bases sintéticas para Xbox, Epic, GOG y PlayStation, IDs numéricos y opacos, fechas vacías, correspondencias parciales, destinos duplicados y guardado/restauración. Se comprueban rechazos con un SAM simulado; no se modifican cuentas reales.

Las bases privadas del usuario confirman el caso PA Steam/Local de Age of Empires con distintos GUIDs y datos equivalentes. En esas bases, Xbox tiene definiciones pero ningún desbloqueo guardado: no prueban una migración real Xbox→Steam. El lector de datos y la compilación se verifican desde Linux; el diseño WPF, la biblioteca SQLite de Windows y una ejecución real con SAM requieren las pruebas de Windows del usuario. No se publican sus bases privadas.

## Búsqueda y comparación asistida (0.11.0)

La ventana de correspondencias busca por el nombre del juego al abrirse. Combina candidatos de ATR, de PA (incluidos juegos Steam sin logros desbloqueados), de la caché de nombres y de la búsqueda pública de la tienda Steam. Una coincidencia exacta única puede rellenar el AppID; el usuario puede elegir otra edición en la lista. El AppID manual sigue disponible. La búsqueda no depende de SAM ni demuestra que una cuenta tenga licencia.

**Cargar logros Steam** sigue obteniendo los IDs de logros del esquema local, PA o SAM. La búsqueda pública de juegos no sustituye ese esquema y no concede acceso al juego. `Steam connection failed` informa un fallo de conexión/consulta y, por sí solo, no identifica su causa.

El selector Idioma permite comparar con textos públicos en español o inglés. Cuando hacen falta, se buscan las páginas públicas de logros de Steam en ambos idiomas y se relacionan mediante imágenes únicas y textos del esquema ya identificado. Se conservan los IDs existentes: nunca se inventan IDs por la posición de una fila, su nombre o una imagen. Los textos se almacenan durante siete días en `SteamAchievementTexts`; si falla la consulta, se conservan los datos locales y continúa la comparación disponible. No hace falta proporcionar una clave API para esa consulta de textos.

La columna Motivo distingue nombre/descripción exactos y descripciones semejantes. Las sugerencias bilingües es/en usan vocabulario limitado de condiciones de juego, cantidades, negaciones y algunos números romanos; no son una traducción universal ni una garantía de equivalencia. Por ejemplo, no se trata 20 paradas como 100 ni una condición sin daño como otra con daño. Los candidatos empatados quedan sin sugerencia automática.

**Confirmar coincidencias exactas** confirma solo propuestas con descripción exacta y destino único. **Confirmar seleccionadas** permite aprobar conjuntamente las filas revisadas y seleccionadas. Ninguna confirma destinos repetidos ni filas sin destino. Las sugerencias no se aplican sin una acción de confirmación del usuario; la confirmación de juego/edición y las verificaciones de Steam siguen vigentes.

Una fila sin propuesta se muestra como Sin sugerencia; no significa que el logro carezca de equivalente. Elegir explícitamente Sin equivalente se conserva al guardar. Las filas vacías de archivos de correspondencias anteriores pueden recibir nuevas sugerencias, sin sustituir destinos previamente elegidos ni trasladar su confirmación a otro ID. Los borradores de distintos destinos se conservan en memoria mientras la ventana permanezca abierta.

Validación: casos sintéticos de los siete tipos de logro mostrados para Cuphead, conflictos numéricos/negación, ambigüedades, localización pública simulada, búsqueda sin progreso desbloqueado y caché. Las solicitudes reales a Steam Store/Community fueron bloqueadas por el proxy de este entorno de desarrollo; su funcionamiento en Windows queda pendiente de prueba. No se afirma una validación en vivo de esas páginas ni una identificación automática perfecta para todos los juegos.


## v0.12.0

- La tabla de correspondencias muestra los identificadores reales de origen y Steam en columnas separadas. No se supone que los códigos coincidan entre plataformas.
- Los planes convertidos muestran «Xbox → Steam» (o el origen correspondiente) en Biblioteca; el historial original mantiene su etiqueta. La etiqueta visual no modifica el filtro, las identidades, los IDs ni las correspondencias guardadas.
- Xbox directo se implementa como fuente opcional independiente de PA. Detalles y configuración pendientes de validación real: [XBOX.md](XBOX.md).
- Los perfiles Xbox conocidos distintos se mantienen separados, incluso si sus fechas coinciden. PA/Xbox directo pueden compartir grupo cuando título y perfil coinciden. No se mezclan listas.
