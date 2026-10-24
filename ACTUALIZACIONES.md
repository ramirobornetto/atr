# Actualización automática de ATR

El botón **Actualizar ATR** consulta `downloads/latest.json` del repositorio `ramirobornetto/atr`. Si la publicación no es más nueva, informa que ya está actualizado. Si hay una versión nueva, descarga el ZIP, verifica tamaño y SHA256, guarda los borradores de los juegos abiertos sin reemplazar los planes guardados explícitamente y cierra ATR. Un instalador auxiliar instala los archivos y vuelve a abrir ATR. No hace falta extraer el ZIP manualmente.

Mientras se comprueba/descarga no se puede iniciar una migración ni consultar/modificar un plan. El botón de actualizar queda bloqueado si ya existe una operación activa. Se puede cerrar la ventana para cancelar antes de entregar la instalación al auxiliar; la versión actual permanece intacta. Si Windows solicita permisos para una carpeta protegida, se requiere aceptarlos en el diálogo del sistema.

El auxiliar se ejecuta desde una copia temporal del runtime, fuera de la instalación. Espera el cierre del proceso original, comprueba que no quede otra instancia en esa carpeta, valida otra vez el ZIP y su versión, rechaza rutas de extracción que salgan de la carpeta temporal o intenten sustituir directorios de datos y conserva una copia anterior de cada archivo reemplazado. Un fallo de copia restaura esos archivos y elimina los nuevos que llegó a instalar. También intenta volver a la versión anterior si la nueva no se puede iniciar o termina inmediatamente con error. No es una garantía de recuperación ante cortes de energía, procesos terminados externamente o fallos posteriores al arranque.

Biblioteca, planes, snapshots, orígenes y ajustes del perfil permanecen en `%LOCALAPPDATA%\AchievementTimelineReplay`. Los borradores recuperables quedan en `UpdateDrafts`, separados de `Plans`, y se recuperan al seleccionar el mismo historial (desde 0.8.1 puede estar ya disponible en la biblioteca persistente). Guardar explícitamente sigue siendo necesario para conservar un plan como configuración elegida. La actualización no ejecuta escaneos ni migraciones automáticamente después del reinicio.

Los ZIP, el detalle `update.log`, los archivos temporales y la copia anterior se conservan bajo `Updates\<version>_<identificador>`. No se eliminan automáticamente las copias anteriores. Si una restauración no puede completarse por permisos/bloqueos, el mensaje señala esa copia y el log. Las carpetas de origen de historiales reales no se tocan.

## Publicar una versión nueva

1. Cambiar `Version` del proyecto WPF y los textos de versión de la interfaz.
2. Ejecutar las pruebas aisladas y `python tools/publicar-windows.py` (requiere SDK .NET 8; `DOTNET` permite indicar el ejecutable).
3. Revisar y publicar en el mismo commit el ZIP de Windows, el ZIP de muestras, `downloads/latest.json` y `downloads/SHA256SUMS.txt`.

El script publica ATR y el auxiliar, comparte el mismo runtime incluido, comprueba el contenido y genera el manifiesto a partir del ZIP terminado. El manifiesto no se incluye dentro del propio ZIP para evitar una dependencia circular del SHA256. No cambiar solo el número de `latest.json`: el archivo y su hash deben corresponder a la compilación real.

La URL permitida es el ZIP versionado de este repositorio, por HTTPS. No se ejecutan órdenes ni URLs de instalación arbitrarias procedentes del manifiesto. El SHA256 comprueba integridad respecto de la publicación; no sustituye una firma de código.

El motor de descarga, extracción, instalación y rollback se prueba con HTTP simulado y archivos aislados. La actualización completa con cierre, permisos de Windows y reinicio requiere validación real en Windows entre dos versiones publicadas. La primera instalación de 0.8.0 se hace desde el ZIP: las versiones anteriores aún no tienen este mecanismo.
