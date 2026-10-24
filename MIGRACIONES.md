# Migración, recuperación e informes — v0.13.0

## Antes de ejecutar

Preparar el plan y pulsar **Iniciar migración**. ATR consulta SAM antes de mostrar la confirmación. El resumen incluye juego/AppID de Steam, historial y archivo elegido, cuenta Steam detectada, seleccionados, ya desbloqueados que se omitirán, pendientes y códigos ausentes. También muestra duración restante, espera inicial, modo reparación y registro de tiempo de juego. La cuenta se identifica mediante la configuración local de Steam; si no puede identificarse, se indica expresamente.

Cada fila muestra nombre, código Steam, acción y detalle. Si hay códigos ausentes en el esquema actual, la ejecución queda bloqueada hasta revisar el plan o las correspondencias. En modo normal se omiten los logros ya desbloqueados sin alterar la selección del borrador. Si todos los seleccionados ya están desbloqueados, se guarda un informe **Sin cambios** y no se ejecutan operaciones de desbloqueo.

Después de confirmar, el CLI moderno se consulta otra vez. Si cambió qué logros se aplicarían u omitirían, ATR vuelve a mostrar el resumen actualizado para confirmar. Un cambio de cuenta detectado durante la revisión impide iniciar.

El modo **reparación** sigue siendo una elección explícita: bloquea los logros pendientes del plan y después los vuelve a desbloquear. En este modo no se omiten los que ya están desbloqueados. El resumen lo advierte. No se cambian las fechas del historial de origen; los nuevos desbloqueos en Steam tienen la fecha actual.

## Reanudar

1. Seleccionar el mismo juego e historial en Biblioteca.
2. Ir a Ejecución y pulsar **Reanudar última migración…**.
3. Confirmar la carga del plan. Se recuperan selección, orden y parámetros; los planes guardados explícitamente no se sobrescriben.
4. El modo reparación se desactiva al recuperar para evitar volver a bloquear lo aplicado.
5. Pulsar **Iniciar migración** para consultar Steam y revisar los pendientes antes de ejecutar.

La recuperación requiere el CLI moderno y el mismo historial. Si se había identificado la cuenta Steam, debe coincidir con la cuenta actualmente detectada. No se usa el diario local como prueba de que un logro está desbloqueado: manda la nueva consulta a Steam. Si faltan logros del plan en el historial actual, se pide actualizar la biblioteca.

El plan completo conserva sus tiempos. Para ejecutar solo los pendientes, se toma el calendario calculado del plan, se eliminan de la ejecución los ya desbloqueados y se lleva el primer pendiente al inicio. Los intervalos entre los pendientes conservan las diferencias del calendario; se vuelve a añadir la espera inicial configurada. No se repiten las pausas anteriores al primer pendiente ni se reconstruye el tiempo transcurrido mientras ATR estuvo cerrado.

Solo hay una migración activa. Se puede seguir preparando otros juegos mientras corre; el botón de reanudar queda deshabilitado durante esa ejecución. El cierre normal sigue pidiendo terminar/cancelar la operación en curso. Una terminación forzada o un corte de energía deja un informe recuperable como **Interrumpida**.

## Informe

La pestaña Ejecución muestra el informe y permite **Copiar informe**, con nombre y código de cada logro:

| Resultado | Significado |
|---|---|
| Ya desbloqueado | Steam ya lo tenía desbloqueado; no se necesita aplicarlo. También puede indicarse un cambio externo durante la ejecución. |
| Pendiente | No se llegó a intentar el desbloqueo. |
| En curso / Sin verificar | El proceso se interrumpió alrededor de una operación; se debe consultar Steam. |
| Aplicado sin verificar | SAM aceptó la operación, pero falta confirmación mediante lectura final. |
| Aplicado | Después de una operación aceptada o interrumpida, Steam confirmó el estado desbloqueado. |
| Fallido | SAM rechazó el intento o la consulta final sigue informando bloqueado. |

La consulta final también se intenta después de cancelar o de un error. Si no se puede completar, el informe lo explica y no presenta las operaciones sin verificar como éxitos confirmados. Si SAM rechazó un intento pero Steam informa desbloqueado, se muestra el estado y se aclara que el cambio no se atribuye a ATR. No hay reintentos automáticos de escritura ante una respuesta ambigua.

El CLI legacy permite la ejecución con un aviso de estado sin verificar; no ofrece omisión fiable ni recuperación verificada. Para esas funciones debe usarse el CLI moderno.

## Guardado y privacidad

Antes de ejecutar se guarda el plan completo y el informe en `%LOCALAPPDATA%\AchievementTimelineReplay\Runs\<identidad-del-historial>`. Cada intento se registra antes de llamar a SAM y después de su respuesta. Si falla el guardado previo, la operación no continúa. Las escrituras de archivos usan reemplazo mediante un temporal y vaciado a disco.

Hay un JSON por ejecución y `latest.json` para recuperar la última. Los informes contienen nombres, códigos, rutas, identidad del historial y cuenta detectada; son información personal. No incluyen credenciales Xbox ni se publican con el programa. El actualizador conserva Runs; borrar el catálogo de biblioteca no borra informes, planes ni snapshots.

## Progreso del escaneo

La barra global cuenta **etapas**, no una estimación de tiempo restante ni un total ficticio de archivos. Incluye las fuentes activadas (Steam, Goldberg/GSE, CODEX/RUNE, PA y Xbox Live), consolidación y guardado. El detalle indica la etapa actual y, cuando existe, el contador local de esa fuente. El progreso es creciente y solo llega al 100 % cuando termina el guardado. Cancelación, errores de escaneo o fallo al guardar no simulan una finalización correcta.

## Validación

Pruebas automatizadas del resumen, códigos ausentes, cambios de estado, recuperación, verificación final, cancelación y progreso por etapas. La integración del motor se prueba con un ejecutable SAM simulado, sin modificar cuentas reales. La interfaz WPF, la migración real en Steam y una terminación forzada requieren validación manual en Windows.
