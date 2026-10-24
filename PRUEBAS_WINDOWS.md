# Pruebas de regresión contra la versión 0.5.3

Probar primero sin iniciar migraciones reales:

1. Ejecutar `WpfPrototype/build-wpf.cmd` con .NET 8 SDK; registrar cualquier `error CS...`.
2. Iniciar `WpfPrototype/run-wpf.cmd`. Verificar orden: Escaneo → Biblioteca → Plan de desbloqueo → Ejecución → Logs.
3. En Escaneo, elegir la misma ruta de Steam y el mismo `samrewritten-cli.exe` que v0.5.3. Validar SAM y escanear. Debe verse avance y permitir cancelar.
4. En Biblioteca, comprobar scroll de mouse, búsqueda por nombre y AppID, selección. Comparar nombres públicos y cantidad de logros de 3 juegos contra 0.5.3.
5. Confirmar que el directorio `%LOCALAPPDATA%\AchievementTimelineReplay\Snapshots` conserva los snapshots anteriores (copiar backup antes de las pruebas).
6. Seleccionar un juego; en Plan comprobar 10 columnas, fechas originales, descripción, detalle, orden, fecha prevista. Cambiar una selección y observar ajuste de duración.
7. Probar un intervalo manual `90m` y un momento nuevo `01:30:00`; limpiar. Probar subir/bajar, arrastrar en modo orden personalizado y restaurar orden.
8. En Plan, consultar estado en Steam con SAM y verificar estados en la última columna.
9. Para una eventual prueba **real**, confirmar primero que el CLI es válido, cuenta correcta, tiempos razonables y que existe un historial guardado. Al pulsar Iniciar, confirmar solo después de revisar el resumen. Probar cancelar en un entorno controlado; los desbloqueos ya aplicados no se revierten.

Si hay errores, adjuntar captura de consola y primer error + una captura del área donde sucedió. No modificar datos originales para solucionar errores de UI.

## Regresión 0.8.2

- Importar una muestra, cerrar ATR y renombrar su carpeta. Al abrir aparece «Archivo no encontrado»; completar un reescaneo y comprobar que desaparece, también al reiniciar. Restaurar la ruta e importar: el snapshot y el plan guardado siguen disponibles. Cancelar un escaneo no debe quitar entradas.
- Para probar nombres, usar una copia nueva del estado Goldberg fuera de las rutas conocidas y sin steam_settings ni atr-demo.json. Importar sin definición: aparecen IDs y el botón del plan. Comprobar el globo «?», cancelar el diálogo y luego elegir la definición original de esa muestra. Debe completar nombres manteniendo selección, orden y tiempos manuales; al reiniciar siguen los nombres.
- Elegir el JSON de estado como definición o una definición de IDs ajenos: debe avisar y conservar los datos anteriores. Con nombres resueltos automáticamente no aparece el botón. Las muestras siguen bloqueando las acciones de Steam.

## Regresión 0.8.3

1. Escanear solo SteamIdra/LuaTools: solo entran BIN cuyo AppID tenga `<AppID>.lua` en Steam/config/stplug-in. Un LUA sin historial fechado no crea una fila. Steam solo excluye esos AppID; ambos habilitados no duplican los mismos historiales.
2. Con las muestras, activar CODEX dejando RUNE apagado; repetir a la inversa y hacer lo mismo con Goldberg/GSE. Desactivar un origen no debe quitar historiales previos; borrando la biblioteca antes de cada prueba se comprueba qué se importa de cero. Los INI de rutas ambiguas ya importados se controlan con CODEX/RUNE compatible.
3. En Biblioteca, alternar cada origen, combinar varios con la búsqueda, revisar cantidades y la flecha de orden. Activar un origen sin juegos no escanea ni crea entradas. Las preferencias sobreviven al reinicio. Cambiar manualmente un origen desde el plan debe mover el juego al filtro correspondiente.
4. Comprobar papelera roja, ayuda «?» y cancelación de la confirmación. Guardar un plan y un origen antes de borrar; confirmar y reiniciar: la lista sigue vacía. Reimportar la misma ruta: snapshot, plan guardado y origen siguen disponibles. Los originales no se modifican.
5. Durante una ejecución o escaneo la papelera queda deshabilitada. Los filtros de Biblioteca siguen siendo de visualización y no modifican el plan en ejecución.
