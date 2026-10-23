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
