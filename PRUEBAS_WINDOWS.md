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


## v0.12.2 — Xbox Live solo juegos con desbloqueos

Actualizar y abrir la biblioteca guardada: deben desaparecer también los títulos Xbox Live 0/35, 0/175 y 0/200. Reescanear: conservar Cuphead 7/42 y todos los títulos con al menos un desbloqueo, incluidos los que no tengan fecha válida. Omitir juegos sin desbloqueos e indicar el motivo en Logs. Esta regla es solo para Xbox directo; los archivos, snapshots, planes y otras fuentes se conservan.


## v0.13.0 — flujo completo de migración

1. Preparar un historial Goldberg/GSE o CODEX/RUNE para un juego correcto: iniciar y comprobar el resumen (nombre, AppID, fuente, ruta, cuenta detectada, seleccionados, omitidos, pendientes y duración). Volver al plan no debe escribir logros.
2. Incluir un logro ya desbloqueado en Steam: debe figurar como Ya desbloqueado y no ejecutarse. Si todos están desbloqueados, se guarda Sin cambios. Seleccionar un código ausente debe impedir confirmar.
3. Cambiar el estado de un logro desde otra interfaz mientras el resumen está abierto: al confirmar, la nueva consulta debe actualizar el resumen y pedir otra revisión si cambió el conjunto de acciones.
4. Ejecutar unos pocos logros con intervalos breves. Cancelar después del primero: el informe debe distinguir aplicado, pendientes y error si lo hubiera. Copiar informe debe incluir nombres y códigos. No debe etiquetar como aplicado confirmado si la lectura final falla.
5. Cerrar ATR tras cancelar y reabrir. Seleccionar el mismo historial, Reanudar última migración y cargar el plan. Se conserva orden/tiempos, se desactiva reparación y al iniciar se consultan los estados actuales para omitir lo aplicado. Probar también terminar forzadamente ATR en una muestra controlada; ningún logro real se revierte.
6. Cambiar de cuenta Steam: no reanudar un informe ligado a una cuenta conocida distinta. Probar guardado sin permisos: no iniciar escrituras si no puede guardarse el plan/diario previo.
7. Durante una migración, preparar otro juego y consultarlo con el comportamiento existente; confirmar que no cambie el plan activo ni el informe del proceso. Reanudar queda deshabilitado mientras hay una migración activa.
8. Escanear Steam junto con Goldberg/GSE, CODEX/RUNE, PA y/o Xbox Live. La barra debe indicar etapa actual y permanecer por debajo de 100 % hasta guardar el catálogo. Cancelar en una etapa posterior a Steam no debe dejar una barra de éxito completa.
9. Actualizar ATR y verificar que Runs, planes, snapshots y sesiones sigan presentes. La integración Epic directa se probará en una actualización posterior.

Las pruebas automáticas usan SAM simulado y no sustituyen estas verificaciones de WPF/Steam en Windows.


## v0.13.1 — destinos Steam de historiales Xbox

- Desde Brotato/Xbox y otro título sin esquema Steam local, cargar logros Steam: no debe aparecer «PA: file is not a database» por abrir un JSON Xbox. Cuphead debe seguir funcionando.
- Probar con PA cerrado y también sin cachés PA: debe consultar SAM si no hay definiciones locales.
- Una caché PA corrupta o un esquema local ilegible se registran, pero no bloquean otras cachés ni la consulta SAM.
- Si SAM rechaza el destino, mostrar su propio error. No afirmar automáticamente que falta una licencia.


## v0.14.0 — desplegable y Epic

1. Abrir correspondencias de Brotato: desplegable de Steam ordenado por nombre en el idioma elegido, con Sin equivalente primero. No debe haber columna Fecha; los códigos, descripciones, casillas y decisiones guardadas deben seguir funcionando. Las fechas del Plan no cambian.
2. Conectar Epic con los botones del navegador y un código temporal propio. Comprobar la cuenta conectada, código vencido/inválido, cancelación y desconexión. No debe pedir Entra ni recibir la contraseña de Epic en ATR.
3. Activar solo Epic (perfil): comparar los desbloqueos con Epic y PA en uno o más títulos compatibles. Conservar fechas ausentes y omitir juegos con cero desbloqueos o sin producto de logros disponible. Comprobar nombres legibles y DLC/alias del mismo producto.
4. Reabrir sin conexión: biblioteca, snapshots y planes disponibles. Reescanear con sesión vencida o error de privacidad debe conservar el historial anterior y mostrar el error en Logs, sin sustituirlo por un cero ficticio.
5. Conectar otro perfil: conservar entradas separadas. No agrupar juegos solo por nombres similares ni mezclar con Steam. PA sigue teniendo una sola opción de escaneo; Epic directo tiene la suya.
6. Preparar correspondencias Epic → Steam y revisar antes de ejecutar. Solo las confirmadas pasan al plan. Probar recuperación de una migración desde ese plan con el flujo de v0.13.
7. Actualizar ATR: preservar Epic/session.dat, copias de logros, snapshots, planes e informes. Desconectar solo elimina la sesión de ATR.

Pendiente de validación manual real: inicio de sesión y respuestas de Epic en Windows. Las pruebas automatizadas cubren respuestas simuladas, no una cuenta real.


## v0.14.1 — acceso Epic

- Con la sesión ya vinculada, actualizar y reescanear Epic sin volver a conectar inicialmente. Comprobar si se recupera DREDGE.
- Si falla, verificar que Logs indique operación y HTTP 401/403/429; compartir solo esas líneas y códigos públicos, sin códigos de acceso ni session.dat.
- Un título 403 no debe detener los demás. Un 401 renueva una sola vez y, si persiste, detiene. Un 429 detiene sin reintentos.
- Un fallo de nombre en el catálogo conserva los desbloqueos recuperados. El resumen debe indicar incidencias cuando corresponda.
- La validación de acceso real a logros sigue pendiente; las nuevas pruebas de errores y renovación usan respuestas simuladas.


## v0.15.0 — validación pendiente en Windows

- Xbox en una instalación sin configuración previa: Conectar cuenta Microsoft sin campo Client ID ni pasos de Azure/Entra. Probar con otra cuenta Microsoft real además de la cuenta ya usada. Código temporal, copia, apertura automática, cancelación y expiración. Las sesiones anteriores deben seguir funcionando.
- Epic: Iniciar sesión en la ventana integrada, verificar identidad y cierre automático al obtener autorización. Probar contraseña/2FA y los proveedores sociales que se utilicen. Continuar después de ingresar como alternativa. No debe mostrar códigos ni tokens en logs.
- Probar Windows sin runtime WebView2 o elegir navegador externo: ingreso anterior con código sigue disponible. Cancelar/cerrar no debe guardar una sesión incompleta ni dejar controles bloqueados.
- Nueva conexión selecciona su fuente al cerrar el diálogo. Desconectar la desactiva, conservando la biblioteca. No deben cambiar preferencias si solo se abre y cierra el estado de conexión.
- Escanear Epic: comprobar que recuperados/sin catálogo/sin progreso/sin desbloqueos/repetidos/errores se distingan y que los contadores concilien. Sin progreso no equivale a cero ni se etiqueta automáticamente como error de privacidad.
- Cancelar una migración después del primer logro; reabrir y reanudar en el mismo historial/cuenta. Debe omitir lo aplicado tras consultar Steam, mantener los pendientes y desactivar reparación. No repetir sobre logros valiosos solo para probar: elegir pocos desbloqueos pendientes y revisar el resumen.
- Actualizar conservando biblioteca, sesiones Xbox/Epic, perfil del navegador, snapshots, correspondencias, planes e informes. Verificar recuperación de un borrador guardado antes de actualizar.

La compilación y las pruebas de servicios/SAM simulado se ejecutan en el entorno de desarrollo. Este entorno no dispone de una sesión gráfica Windows ni de otra cuenta Microsoft del usuario; esos pasos manuales no se declaran aprobados automáticamente.


## v0.16.0 — revisión visual y compatibilidad

- Escaneo: fuentes a la izquierda, botones a la derecha. Sin párrafos explicativos ni resultados multilínea al terminar; etapa visible mientras corre y detalle disponible en Logs.
- Seleccionar Epic o Xbox Live sin sesión vinculada: aviso global visible con el botón de conexión indicado. Si hay otras fuentes, continuarán; si solo están seleccionadas cuentas no vinculadas, no iniciar un escaneo vacío. No eliminar historiales guardados de esas fuentes.
- Verificar Playnite Achievements en botones, filtros, selector de origen, mensajes y logs, conservando las preferencias de la versión anterior. Las rutas físicas e identificadores guardados no se renombran.
- Biblioteca: retirar filtros GOG/PlayStation/OnlineFix. Si hay datos antiguos de esos orígenes, mostrarlos a través de Otro y permitir conservar sus planes/snapshots.
- Plan: dos columnas inferiores, botones y selector de historial compactos, mayor margen entre ruta y tabla. Consultar estado en Steam y Estado en Steam. Revisar tamaño habitual y ventana mínima para que no se superpongan controles.
- Correspondencias: mismo alto de nombre/AppID/selectores/botones; selectores de fila compactos. Confirmar del encabezado marca todas las equivalencias únicas con destino válido, omite conflictos y permite desmarcar todo. Cambiar una equivalencia actualiza la casilla de encabezado; comprobar también correspondencias guardadas y estado parcial.
- Guardar/cargar planes, reordenar y calcular duración deben mantener el comportamiento anterior; los cambios son de presentación y selección, no de fechas ni identidad de archivos.

El entorno de desarrollo compila WPF pero no ejecuta una sesión gráfica Windows; la distribución final de píxeles requiere esa revisión manual.


## Revisión visual 0.16.2

- Biblioteca: actualizar y borrar inmediatamente después del título; detalle debajo.
- Barra lateral: logo visible; encabezado sin subtítulo.
- Cambiar entre las cinco pestañas: datos del juego solo visibles en Biblioteca, Plan y Ejecución.
- Plan: botones junto al título; Origen a la izquierda de Historial de origen; selectores de alto 28 y anchos 185/260.
- Origen: mismas opciones que los filtros de Biblioteca. Un origen antiguo oculto se presenta como Otro sin guardar cambios hasta una edición explícita.
- Pasar por varias ayudas, incluidas casillas creadas dinámicamente y ventanas secundarias: demora de 0,3 s.
- Revisar títulos largos, ventana mínima y escalas de Windows 100/125/150 %; las filas pueden envolver controles cuando falta espacio.


## Revisión visual 0.16.3

- Escaneo: dos columnas de fuentes y dos de botones; fondo del bloque ajustado al contenido, sin ocupar el espacio vacío inferior. Goldberg no se estira.
- Plan: notas de borrador y origen guardado ocultas, campos de duración/esperas de alto 28 y menor ancho. Verificar valores largos y edición normal.
- Notas explicativas en cursiva, sin cambiar rótulos, títulos, tablas ni texto de los globos.
- Logo lateral nítido usando el PNG de 256 × 256 en escalas 100/125/150 %.
- Barra de título sin WPF. Revisar distribución a ventana mínima.


## Revisión visual 0.16.4

- Importaciones a la izquierda en orden Goldberg / Codex-Rune / Playnite Achievements; conexiones Epic / Xbox a la derecha.
- Campos de tiempos de alto 24: comprobar edición, valores largos y separación de Espera inicial.
- Reparación y registro de tiempo, cada uno en su renglón debajo de Espera inicial, con sus ayudas.
- Logo centrado de 144 × 144 y navegación accesible a tamaño mínimo de ventana y escalas 100/125/150 %.


## Revisión visual y de interacción 0.17.0

- Fondo desenfocado visible en las cinco pestañas. Textos legibles sobre zonas claras y oscuras, sin transparencia en el propio texto.
- Navegación: iconos de 22, letra 15 solo en pestañas; franja cian sigue a la pestaña activa. Probar clic y Tab/Enter.
- Botones normales y filtros: glow cian al entrar, salida suave al retirar el mouse. Borrar, quitar, limpiar, cancelar y desconectar: glow rojo. Controles desactivados siguen sin ejecutar acciones.
- Goldberg/Codex-Rune/Playnite Achievements con igual ancho entre sí; Epic/Xbox con su propio ancho común.
- Desplegables: abrir/cerrar con mouse y Alt+Abajo/Escape; elegir con teclado, rueda y mouse; comprobar desplazamiento en listas largas y selección visible en correspondencias.
- Casillas: marcar/desmarcar con clic y Espacio; encabezado de Confirmar con estado parcial cuando corresponde; selección de filas no reemplaza la confirmación.
- Conectar Epic y Xbox: ayudas, botones y selectores legibles en ventanas secundarias; no es necesario ejecutar una migración para revisar estos estilos.
- Revisar ventana mínima y escalas 100/125/150 %, particularmente los textos largos de origen y los filtros.


## Revisión visual 0.17.1

- Navegación: contorno cian completo y uniforme en la pestaña activa; el título Plan de desbloqueo entra completo. Iconos 20, letra 14.
- Paneles negros transparentes sobre el mismo fondo, sin tinte azul de los rellenos.
- Biblioteca, Plan, correspondencias e informe: filas alternas sutiles, sin líneas claras; textos y controles centrados verticalmente.
- Verificar selección de filas, selección de casillas, edición de tiempos, menús desplegables y redimensionamiento de columnas. La plantilla de celda mantiene el contenido y los enlaces originales.

- Escaneo: leer las fuentes de arriba abajo, primero izquierda y luego derecha. Xbox Live y Epic son las dos últimas; marcar y escanear conserva las mismas preferencias.
- Botones de cancelar/borrar/desconectar: hover de rojo a rojo oscuro, texto blanco y glow rojo.
- Selección de filas: halo cian y borde fino sin modificar altura ni posición de las columnas; comprobar también scroll horizontal, edición, casillas y selección con teclado.

- Arrastre de Plan: iniciar desde una celda de solo lectura, mover despacio y con saltos largos en ambas direcciones. La fila sigue al cursor, las otras se recolocan y los números cambian en vivo. Probar filas fuera de pantalla con desplazamiento automático y saltos al primer/último lugar.
- Escape y soltar fuera de la tabla: restaurar el orden exacto y el estado anterior de Orden personalizado, tanto con orden cronológico como con uno manual guardado.
- No iniciar arrastres desde casillas, campos editables, encabezados ni barras. Las fechas y esperas manuales siguen perteneciendo a sus logros; no debe haber duplicados ni pérdidas. Al soltar, revisar los tiempos recalculados y Guardar/Cargar plan.
- Con la ventana escalada al 125/150 %, comprobar que la vista flotante conserve tamaño y texto legible. Las filas virtualizadas deben conservar el estilo al desplazarse.


## Revisión visual 0.17.2

- Paneles principales más transparentes (40 %); textos y controles sólidos.
- Separadores vertical y horizontal del Plan: línea fina con glow leve, cursor correcto y ajuste de dimensiones arrastrando dentro de la zona de 6 unidades. Verificar también escalas 125/150 %.


## Revisión visual 0.17.3

- Ajustes horizontal y vertical del Plan sin línea ni glow, incluso al pasar el mouse. El cursor cambia y se conserva el arrastre de las dimensiones.


## Revisión visual 0.17.4

- Principales al 55 % e interiores al 45 % de transparencia; revisar legibilidad sobre zonas claras y oscuras del fondo. Textos y controles mantienen su opacidad.


## Actualizador 0.17.5

- Cada búsqueda de actualización debe usar una consulta nueva al manifiesto, con cabeceras que eviten caché. Comprobar en Windows que detecte una publicación posterior.
- La consulta cambia; la validación del paquete no cambia. Las instalaciones antiguas pueden necesitar reintentar o actualizar manualmente para recibir esta corrección.


## Revisión visual 0.17.6

- Paneles principales e interiores sin contorno gris; misma transparencia y fondo. Los botones, casillas y selección conservan sus bordes funcionales.
- DESCRIPCION.md y TUTORIAL.md presentes en el ZIP y accesibles desde los enlaces del README.
- Fondo un 10 % más oscuro aproximadamente; revisar legibilidad y conservación de transparencias 55/45.


## Revisión visual 0.17.7

- Cuadro de Logs azul oscuro al 60 % de transparencia, con texto sólido y legible.
