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

## Regresión 0.9.0 — PA

1. Cerrar Playnite y actualizar ATR. Comprobar el icono del EXE/ventana/barra de tareas. Escanear solo PA o seleccionar achievement_cache.db con Importar PA; game_custom_data.db y los WAL/SHM existentes deben quedar junto a su base. No se necesita config.json.
2. Confirmar que Resident Evil elige Manual 1/44 frente al registro Steam 0/44 en las bases de prueba privadas. Probar también un juego con más desbloqueos en Steam que en Manual: debe ganar Steam. Revisar Fuente y el log. Los logros de amigos no deben importarse.
3. En PA, quitar la fecha de un logro manual manteniéndolo desbloqueado, guardar y cerrar Playnite. Reimportar: debe conservarlo y mostrar Sin fecha, sin 1970 ni esperas gigantes. Si la lista con más desbloqueos tiene menos fechas, debe seguir ganando. Guardar plan, reiniciar y comprobar selección/tiempos/estado sin fecha.
4. Escanear un archivo Goldberg y el seguimiento Local de ese mismo archivo en PA: si hay evidencia fechada coincidente, debe verse una entrada con el historial más completo. Si faltan todas las fechas, o hay perfiles ambiguos/contradictorios, deben conservarse separados. El AppID solo no basta para fusionar.
5. Guardar un plan de la fuente directa antes de elegir PA más completo. Cargar el guardado desde la nueva entrada: se recuperan los IDs comunes con selección y tiempos; los nuevos quedan desmarcados. Confirmar que el plan anterior y snapshot original siguen en disco.
6. Con una instalación portable, importar una vez y repetir escaneo: se recuerda la ruta. Desactivar PA no debe vaciar su catálogo; filtro PA no inicia escaneos. Borrar biblioteca no elimina sus bases ni los planes/snapshots. Las muestras sintéticas siguen sin permitir operaciones Steam.

## Regresión 0.10.0

1. Abrir el catálogo anterior y escanear de nuevo: Age of Empires II DE Steam directo, PA/Steam y PA/Local equivalentes deben aparecer en una fila con tres fuentes. Revisar el tooltip y las cantidades. Cuentas distintas o historiales sin evidencia suficiente deben conservarse separados.
2. Elegir una fuente manual, editar selección/orden/tiempos, cambiar a otra y volver: se pide confirmación si hay ediciones y se conserva el borrador. Guardar plan, cerrar/reabrir y comprobar fuente elegida, cargar plan y alternativas completas. El modo automático vuelve a priorizar más desbloqueos.
3. Ocultar orígenes y buscar por nombre/AppID, sin reimportaciones. Quitar un grupo y borrar biblioteca deben afectar solo el catálogo; escanear permite recuperar fuentes y planes.
4. Refrescar Xbox en PA hasta que su caché tenga desbloqueos y cerrar Playnite. Importar: ID externo visible, ejecución Steam deshabilitada. Abrir equivalencias, cargar el AppID Steam correcto, revisar ambas descripciones y confirmar solo las equivalentes. Guardar, cerrar y recargar el mismo AppID: asociaciones recuperadas.
5. Confirmar destinos repetidos debe impedir preparar el plan. Cambiar AppID invalida la edición cargada hasta consultar de nuevo. Asociaciones pendientes no deben entrar al plan; los logros sin fecha sí se conservan cuando su asociación está confirmada. Revisar Steam para omitir ya desbloqueados.
6. Probar Epic/GOG/PlayStation con desbloqueos disponibles en PA. Todos se incluyen mediante la misma casilla PA; sus filtros de Biblioteca son independientes. No se conectan directamente a esas plataformas ni se les escriben logros. Validación real pendiente de cada formato/proveedor.
7. Con SAM rechazando un AppID u operación, comprobar mensaje de error, progreso sin avanzar para el logro fallido y ausencia de Migración completada. Sin validación propia de propiedad ni modificación de archivos PA.

## Regresión 0.11.0

1. Abrir correspondencias de Cuphead desde Xbox. Buscar por nombre, comprobar Cuphead AppID 268910 y distinguir DLC/ediciones. Probar también sin red: debe conservar candidatos locales cuando existan y permitir AppID manual. La propiedad del juego no se deduce de la búsqueda.
2. Con esquema inglés, comparar los siete logros de la captura en español: revisar propuestas, cantidades 20/100, negación sin daño, nombres/descripciones y motivo. La consulta de textos públicos en el Windows del usuario está pendiente de validación real, ya que el entorno de desarrollo bloqueó esas URLs.
3. Probar Confirmar coincidencias exactas y Confirmar seleccionadas tras revisar; destinos repetidos y filas sin propuesta no deben confirmarse en bloque. Cambiar manualmente un destino debe quitar su confirmación y motivo automático.
4. Cargar un archivo anterior con filas vacías: se proponen candidatos sin reemplazar destinos guardados válidos. Marcar Sin equivalente, guardar y volver: debe conservar esa exclusión. Cambiar destino o idioma y volver debe conservar borradores de esa ventana, sin confirmar automáticamente nuevos IDs.
5. Cambiar AppID después de cargar invalida la edición hasta volver a cargar. El plan preparado debe llevar el nombre elegido en la búsqueda y solo las asociaciones confirmadas. Los datos de origen y fechas no se modifican.

Pendiente anotado: la barra de progreso del escaneo completo debe cubrir todas las fuentes; no se modifica en 0.11.0.


## v0.12.0 — códigos y Xbox directo

1. Abrir un historial PA/Xbox y Preparar equivalencias: comprobar las columnas Código de origen/Código Steam y que el código Steam cambie al elegir otra opción. Confirmar que siguen funcionando las casillas y el desplegable después de agregar columnas.
2. Preparar un plan Steam: en Biblioteca, original Xbox y convertido Xbox → Steam; ambos siguen el filtro Xbox. Reabrir la app y comprobar persistencia de etiquetas y correspondencias.
3. Registrar ATR según XBOX.md y validar con una cuenta real. Este paso no se pudo ejecutar desde el entorno de desarrollo: no hay un Client ID propio registrado y las conexiones Microsoft están bloqueadas. La interfaz WPF y DPAPI también requieren prueba en Windows.
4. Conectar por código de dispositivo, cancelar antes de completar, denegar consentimiento y dejar vencer el código. No deben aparecer tokens ni cuerpos de respuesta privados en Logs. Un GUID inválido no debe iniciar una solicitud.
5. Con una sesión válida: activar solo Xbox (perfil), escanear y comparar Cuphead contra Xbox/PA, incluyendo total, estado, código y fechas. Probar perfil con más de una página de títulos y juegos de PC/One/Series; contrato Xbox 360 pendiente de prueba real.
6. Reabrir sin conexión: biblioteca/planes disponibles desde snapshots; reescanear con Xbox desactivado no debe consultar su servicio. Una sesión vencida o un error conserva el historial anterior y se registra en Logs.
7. Desconectar y conectar otro perfil: conservar historiales anteriores separados. Si PA y Xbox directo tienen la misma identidad, una entrada agrupada con fuentes seleccionables y prioridad por más desbloqueos.
8. Desbloqueo sin fecha válida: mantenerlo como Sin fecha. No importar bloqueados aunque tengan una fecha engañosa. Preparar equivalencias y plan Steam sin cambiar el archivo Xbox.
9. Actualizar ATR: conservar Xbox/connection.json, Xbox/session.dat, historiales, snapshots y planes. Borrar biblioteca sigue borrando solo los índices; desconectar elimina solo la sesión local.

La barra de progreso global de las demás fuentes sigue siendo un pendiente anotado; esta versión no rediseña ese cálculo.


## v0.12.1 — Xbox Live 0/0

- Actualizar y abrir la biblioteca guardada: las entradas Xbox directo 0/0 deben dejar de mostrarse. No se eliminan archivos de origen, snapshots ni planes.
- Escanear Xbox Live de nuevo: omitir entradas vacías como Sekiro 0/0; conservar Cuphead 7/42 y títulos con catálogo pero sin desbloqueos (0/35). Logs indica cada omisión.
- Escanear Steam: su entrada de Sekiro debe conservar sus logros, sin recibir datos de la entrada Xbox ni ser filtrada por esta regla.
- Comprobar etiquetas Xbox Live / Xbox Live → Steam y que el filtro Xbox Live conserve su estado anterior y afecte ambos tipos de entrada. Verificar también la selección de origen en Plan.
