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
