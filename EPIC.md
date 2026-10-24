# Epic Games — conexión y resultados desde v0.15.0

La fuente **Epic (perfil)** es independiente de PA y consulta los logros disponibles del servicio Epic Games para la cuenta conectada. Solo importa juegos con al menos un desbloqueo, también sin fecha válida.

## Conexión habitual

1. Abrir **Escaneo → Conectar Epic…**.
2. Pulsar **Iniciar sesión en Epic**. Se abre el sitio de Epic en una ventana de navegador integrada.
3. Ingresar con Epic y completar las verificaciones que solicite. ATR no recibe la contraseña: la página oficial la envía a Epic.
4. La autorización se captura automáticamente al completarse. Si el sitio terminó de ingresar pero la ventana sigue abierta, usar **Continuar después de ingresar**.
5. ATR muestra la cuenta conectada. Al cerrar el diálogo, Epic (perfil) queda seleccionado para el siguiente escaneo.
6. Pulsar **Escanear biblioteca** y usar el filtro Epic en Biblioteca.

La ventana integrada usa Microsoft Edge WebView2. El paquete incluye su SDK y cargador, pero no el navegador Evergreen completo. Si el runtime no está disponible o un proveedor de ingreso no funciona dentro de esa ventana, se ofrece la alternativa externa. No es necesario instalar el runtime para usar la alternativa.

## Alternativa externa

Elegir **Usar navegador externo**. Ingresar en Epic; si hace falta, usar **Obtener código en Epic**. Copiar el valor authorizationCode o el JSON que lo contiene y usar **Pegar código y conectar**. También se puede pegar manualmente y pulsar **Conectar con el código**. El portapapeles solo se lee al pulsar ese botón.

No requiere Entra ni un registro Microsoft. Es un flujo compatible con Epic Games Launcher, como el utilizado por PA, con constantes públicas de ese cliente. No es un registro OAuth propio de ATR ni utiliza sesiones de otras aplicaciones. Los códigos son temporales y de un solo uso.

## Qué significan los resultados

El resumen separa:

- **Recuperados:** se leyó al menos un desbloqueo y se guardó su snapshot.
- **Sin catálogo:** Epic no devolvió un producto/catálogo de logros utilizable para esa entrada. Puede corresponder a un juego sin soporte, un complemento o un catálogo no disponible.
- **Sin progreso disponible:** no se recibió el bloque de progreso esperado. No equivale a cero desbloqueos ni demuestra un problema de privacidad; no se reemplaza un historial anterior por cero.
- **Sin desbloqueos:** se recibió un progreso explícito sin logros desbloqueados.
- **Repetidos:** otra entrada de biblioteca resolvió a la misma identidad de producto ya guardada.
- **Errores:** rechazo HTTP, límite de consultas, contenido no reconocido, códigos duplicados o fallos reales al guardar/leer. Un fallo de fase previo a enumerar se cuenta aparte de los títulos revisados.

Así, 69 elementos con 2 recuperados, 36 sin catálogo y 31 sin progreso disponible no se presentan como 67 errores. El resumen también indica si una fase se detuvo.

Los errores identifican operación y HTTP. Un 401 intenta renovar una sola vez. Un 403 de un título no detiene otros títulos, y un nombre de catálogo denegado no descarta los desbloqueos ya leídos. Un 429 detiene sin reintentos. Nunca se imprimen tokens ni cuerpos completos de errores.

## Límites y separación

Solo se recuperan los logros que expone el servicio Epic Games consultado: no se garantiza todo el progreso EOS de todos los títulos ni logros de emuladores. Un estado unlocked verdadero determina el desbloqueo; una fecha aislada no lo activa. Los desbloqueos sin fecha se conservan como Sin fecha.

Biblioteca se pagina y después se consulta esquema por sandbox y progreso por producto de logros/cuenta. Los alias del mismo sandbox/producto comparten identidad. Los perfiles conocidos distintos se mantienen separados. PA y Epic directo pueden conservar entradas distintas si no hay identidad suficiente para demostrar que son el mismo producto; no se agrupan por parecido de nombres ni se unen sus listas.

Las correspondencias confirmadas preparan un historial Epic → Steam separado y conservan las fechas de origen. Las acciones sobre Steam siguen pasando por SAM y su resumen previo. No se escribe en Epic.

## Datos locales

La sesión renovable se guarda cifrada con DPAPI CurrentUser en `%LOCALAPPDATA%\AchievementTimelineReplay\Epic\session.dat`; el access token no se persiste. La renovación verifica que siga siendo la misma cuenta. Los snapshots y copias normalizadas de desbloqueos no contienen tokens.

El navegador integrado tiene su propio perfil en Epic/Browser. Al iniciar una conexión integrada se limpian sus cookies para que el usuario elija la cuenta; no se toca el navegador externo, el launcher ni PA. La captura automática solo acepta respuestas del endpoint HTTPS oficial esperado y el callback local exacto de una autorización en curso. No lee campos de contraseña ni acepta códigos enviados por otras páginas.

Desconectar elimina la sesión local de ATR y desactiva su fuente de escaneo al cerrar el diálogo. No elimina historiales ni planes. El actualizador conserva Epic, sesiones y perfil de navegador. No compartir códigos, session.dat ni carpetas completas de datos.

## Validación

El usuario confirmó en v0.14.1 la recuperación de DREDGE 60/60 y KILL KNIGHT 12/47, y la migración real de DREDGE a Steam. Las pruebas automáticas cubren lectura, renovación, clasificación, captura restringida de códigos, perfiles y preservación de datos. El nuevo navegador integrado y sus proveedores de ingreso requieren validación manual en Windows; no se presentan las pruebas simuladas como accesos reales a una cuenta.

Referencias de terceros: THIRD_PARTY_NOTICES.md.
