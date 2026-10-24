# Biblioteca persistente

Desde 0.8.1, después de escanear o importar un historial se guarda el catálogo en `%LOCALAPPDATA%\AchievementTimelineReplay\Library\catalog.json`. Al abrir ATR se carga esa lista sin volver a leer todos los BIN/JSON/INI ni consultar nombres públicos. Los logros originales se obtienen de sus snapshots protegidos. Las fuentes importadas manualmente también se recuerdan.

La biblioteca muestra cuándo se actualizó el catálogo, la fecha del último escaneo y la disponibilidad de cada archivo. Los contadores que se cargan corresponden al historial protegido y su total conocido. Consultar Steam vuelve a obtener los estados oficiales actuales; esa respuesta no se reutiliza como estado actual en el próximo arranque.

**Primera instalación de 0.8.1:** hacer un escaneo o importar los archivos una vez. Las versiones anteriores no guardaban un catálogo de rutas, por lo que la lista no puede reconstruirse completa automáticamente desde sus snapshots. Los planes/snapshots previos se conservan.

## Actualizar y quitar

**Actualizar biblioteca** revisa las carpetas habituales habilitadas y las fuentes locales recordadas. No es una vigilancia permanente ni una búsqueda incremental de todo el disco. La carga al inicio usa el catálogo; el refresco explícito vuelve a leer las fuentes disponibles. También funciona con fuentes locales aunque Steam/appcache/stats no esté disponible.

Desde 0.8.2, un archivo desaparecido se muestra como «Archivo no encontrado» al abrir el catálogo, pero su entrada se quita tras completar un reescaneo exitoso. Se conservan sus planes, origen y snapshots; al volver a importar la misma ruta se pueden recuperar. Los archivos que todavía existen pero no se pudieron reimportar permanecen identificados como guardados para no perderlos por un error de lectura. Un snapshot inexistente o ilegible deja la fila marcada, sin habilitar la ejecución. Cambiar una ruta implica otra identidad para los formatos locales; la referencia anterior puede quitarse.

**Quitar de biblioteca** elimina únicamente la referencia del catálogo. No elimina el archivo original, el snapshot, la etiqueta de origen ni el plan guardado. Si el archivo sigue en una ruta de búsqueda conocida, puede reaparecer al actualizar. No es una lista de exclusiones permanentes.

Al actualizar la biblioteca, los borradores de los juegos que ya estaban abiertos se conservan y se relacionan por ID API; los nuevos logros se incorporan desmarcados cuando se restaura un borrador anterior. Cancelar o fallar un escaneo no vacía la lista que ya estaba disponible. Guardar plan sigue siendo necesario para conservar voluntariamente esas ediciones entre cierres normales.

## Estado actual y protección

El catálogo no comprueba propiedad de juegos y no identifica por sí solo la cuenta conectada a SAM. Si la cuenta conocida en loginusers.vdf difiere de la de un historial Steam guardado, se señala y se pide actualizar antes de ejecutar. Las fuentes de emulación son neutrales respecto a la cuenta de destino. Si no se puede determinar la cuenta desde ese archivo, no se inventa una identidad.

Antes de ejecutar se consulta SAM como en las versiones anteriores. Con el CLI moderno se exige que todos los IDs seleccionados existan en su respuesta actual; los logros retirados del esquema no se envían a desbloquear, tampoco en reparación. Las muestras guardadas siguen sin habilitar acciones de Steam. Los datos del catálogo no sustituyen esa verificación.

Los metadatos se escriben mediante un archivo temporal y reemplazo final. Se mantiene `catalog.previous.json`; si el catálogo principal no puede leerse, se intenta esa copia. Los registros corruptos o con identidades incompatibles se señalan sin acceder a snapshots arbitrarios. Un guardado explícito posterior conserva el catálogo corrupto con otro nombre antes de reemplazarlo. No se reescriben snapshots al cargar la biblioteca.

La actualización del programa conserva este directorio y los borradores de actualización independientes. Un borrador recuperado tras actualizar aparece al seleccionar el historial, que ahora normalmente ya estará disponible en el catálogo al arrancar.
