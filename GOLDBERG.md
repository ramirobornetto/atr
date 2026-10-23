# Soporte Goldberg / GBE — ATR 0.7.0

ATR lee el estado de usuario `achievements.json` sin ejecutar emuladores ni modificar archivos. Es posible desarrollar y probar este adaptador con JSON sintéticos sin tener un juego que use Goldberg.

## Probar sin un juego

El ZIP de Windows incluye `muestras/Goldberg/480/achievements.json`. Abrir **Escaneo → Importar Goldberg…** y seleccionar ese archivo, no el de `steam_settings`. Se esperan cuatro logros con fecha de seis conocidos y nombres en español. La muestra incluye una separación de 17 h 50 min para probar el límite de esperas largas.

El encabezado identifica la demostración. Se pueden editar y guardar planes, cargar el guardado, restaurar el original y corregir el origen. Para esta muestra están bloqueados Consultar Steam, Abrir en SAM e Iniciar migración. Conservar el marcador `.atr-demo`; la protección del snapshot comprueba también que el modo de demostración no haya cambiado.

## Importar un historial real

En el escaneo habitual se buscan, además de Steam, `%APPDATA%\Goldberg SteamEmu Saves\<AppID>\achievements.json` y `%APPDATA%\GSE Saves\<AppID>\achievements.json`. Se puede desactivar esa búsqueda. El escaneo de biblioteca sigue requiriendo la ruta de Steam; el botón de importación manual funciona por separado sin Steam ni SAM.

Para guardados de una ubicación personalizada, usar **Importar Goldberg…** y seleccionar el estado dentro de una carpeta cuyo nombre sea el AppID. Se valida la carpeta inmediata; no se adivina el AppID ni se recorre todo el disco. La búsqueda habitual no recorre perfiles ni rutas anidadas arbitrarias.

Los dos archivos llamados `achievements.json` tienen funciones distintas:

- **Estado**: objeto con claves API y valores `earned` booleano / `earned_time` Unix en segundos. Solo los logros con `earned: true` y fecha válida entran al plan.
- **Definición**: array con `name`, `displayName`, `description`, etc., normalmente en `steam_settings` de la instalación del juego. No acredita ningún desbloqueo. Marcar **Elegir también definición de nombres** antes de importar para seleccionarla. Cancelar ese segundo diálogo continúa sin ella.

Sin definición, ATR usa el esquema local de Steam si está disponible. Si tampoco existe, muestra los IDs API; no inventa nombres ni fechas. El total cuenta IDs distintos conocidos por el estado o la definición; puede ser incompleto si ambos archivos lo son. Un desbloqueo sin fecha válida se omite y se explica en Logs. No se convierten milisegundos o cadenas de texto por conjetura. Los empates de fecha se ordenan por ID API.

Biblioteca muestra **Fuente: achievements.json (Goldberg/GBE)** y **Origen: Goldberg**, corregible desde el plan. Ese origen indica compatibilidad del formato; no certifica qué ejecutable escribió el JSON, una licencia ni la procedencia de una instalación.

## Protección y persistencia

Goldberg y Steam se conservan como historiales separados, aunque compartan AppID. No se fusionan automáticamente. Cada ruta Goldberg tiene identidad propia (hash de la ruta absoluta); se usa cuenta 0 como espacio local, sin atribuir el historial a una cuenta Steam oficial. La cuenta conectada a Steam sigue siendo el destino de una migración real, como en las versiones anteriores.

Snapshots, planes y orígenes quedan separados de Steam y de otros perfiles por esa identidad. Se guardan los bytes originales y un snapshot JSON; un snapshot existente nunca se reemplaza. Restaurar original vuelve a ese primer historial protegido, no a cambios posteriores en los guardados. Una corrupción o identidad incorrecta impide cargar ese snapshot. Mover la carpeta de origen crea una identidad diferente. Para volver a cargar una ruta personalizada después de reiniciar o de un escaneo completo, importar el mismo archivo y cargar el plan guardado.

No cambia la ruta ni el formato de planes, orígenes o snapshots de Steam existentes. Los planes previos sin HistoryId siguen siendo compatibles con Steam. No se importa progreso parcial de logros aún bloqueados, imágenes, estadísticas ni tiempo de juego de Goldberg.

## Evidencia y límites de validación

Formato contrastado con el código público de GBE Fork, commit `f7bbd18130d184048fcb1784a622ed846dec589b`:

- [Carga de estado y definiciones](https://github.com/Detanup01/gbe_fork/blob/f7bbd18130d184048fcb1784a622ed846dec589b/dll/steam_user_stats_achievements.cpp)
- [Inicialización de earned y earned_time y localización](https://github.com/Detanup01/gbe_fork/blob/f7bbd18130d184048fcb1784a622ed846dec589b/dll/steam_user_stats.cpp)
- [Nombre GSE Saves](https://github.com/Detanup01/gbe_fork/blob/f7bbd18130d184048fcb1784a622ed846dec589b/dll/local_storage.cpp)

Las comprobaciones automáticas usan estados y definiciones sintéticos y un SAM simulado; no modifican cuentas reales. Se verifica compilación WPF y publicación para Windows x64 desde Linux. Falta validación visual de WPF en Windows y contraste con archivos producidos por una instalación real. Esto es soporte inicial para el formato descrito, no una garantía para todos los forks y configuraciones.


## Cambios de 0.7.1

Se detectan las muestras copiadas por sus IDs ficticios aunque falte el marcador `.atr-demo`; no se mezclan sus totales con los de esquemas de juegos reales. Si existe un nombre público, se muestra con la indicación «muestra Goldberg». De lo contrario, se conserva «Muestra Goldberg · AppID …».

El título de un juego real se resuelve por caché pública y Steam Store. El `gamename` del esquema se usa únicamente como información interna y ya no como título Goldberg. Un AppID no disponible o una consulta fallida conserva su identificador numérico. La importación funciona aunque el servicio de nombres no esté disponible; puede esperar hasta el timeout de la consulta.

La búsqueda presenta contadores por fuente y motivos de omisión, incluidos errores de snapshots. Los historiales de Steam reciben el origen Steam; un LUA coincidente en config/stplug-in solo indica una posible relación con Steamidra. Las correcciones manuales de origen prevalecen sobre ambas detecciones.
