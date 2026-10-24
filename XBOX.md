# Xbox directo — v0.12.0

ATR incluye una fuente opcional **Xbox (perfil)**, independiente de PlayniteAchievements. Consulta el historial de juegos jugados y los logros del perfil Microsoft conectado. No obtiene un catálogo de licencias compradas ni escribe desbloqueos en Xbox. Desde v0.12.2 solo se muestran juegos con al menos un logro desbloqueado. Se omiten 0/0, 0/35 y cualquier título sin desbloqueos, también al cargar la biblioteca guardada. Un desbloqueo sin fecha válida sí cuenta. No se borran archivos ni se aplica este filtro a otras fuentes.

## Estado de la integración

El lector, la conexión, la renovación de sesión, la paginación, la persistencia y la preparación de correspondencias con Steam están implementados. Las pruebas automatizadas usan respuestas simuladas. **El usuario confirmó el inicio de sesión con un registro propio y la lectura de su perfil en v0.12.0, incluyendo Cuphead 7/42.** Esto no valida todas las cuentas, títulos ni el contrato Xbox 360. Las consultas de red a Microsoft desde el entorno de desarrollo están bloqueadas. No se incluye un Client ID de otra aplicación ni se importan las sesiones de Playnite.

El primer uso requiere registrar ATR y comprobar que Microsoft acepta el permiso XboxLive.signin para esa aplicación. Si Microsoft restringe ese permiso, no alcanza con obtener un identificador: habrá que resolver la habilitación con Microsoft antes de considerar verificada la integración. No se promete acceso a todas las cuentas ni todos los títulos.

## Configuración inicial del responsable del proyecto

El **Client ID** es un identificador público de la aplicación, no el usuario de Xbox, una contraseña ni un Client Secret. Obtenerlo mediante el registro de una aplicación normalmente es gratuito; ATR no necesita desplegar servidores ni contratar recursos pagos de Azure. La disponibilidad del registro puede depender de la cuenta/directorio Microsoft.

1. Abrir [Microsoft Entra — registros de aplicaciones](https://entra.microsoft.com/) y crear un registro para **Achievement Timeline Replay (ATR)**.
2. Elegir un tipo de cuentas que admita **cuentas Microsoft personales**. El código usa el endpoint OAuth `consumers`, no cuentas exclusivamente corporativas.
3. Habilitar los **flujos de cliente público** en la configuración de autenticación de la aplicación. El flujo de código de dispositivo no necesita un Client Secret ni un redirect URI.
4. Copiar el **Id. de aplicación (cliente)**, un GUID, desde el resumen del registro.
5. En ATR, abrir **Escaneo → Conectar Xbox…**, pegar ese identificador e iniciar la conexión. No pegar secretos ni contraseñas en ese campo.

ATR solicita `XboxLive.signin offline_access`. La aceptación de esos permisos debe comprobarse para cada registro; no se debe resolver un rechazo sustituyendo el identificador por el de otro programa. La configuración actual permite introducir el identificador en cada instalación; una vez validado el registro propio de ATR, podrá distribuirse preconfigurado en una versión posterior.

Referencias del protocolo: [flujo OAuth de código de dispositivo](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code) y [registro de aplicaciones](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app). Los nombres de las opciones del portal pueden cambiar.

## Uso

1. En **Conectar Xbox…**, pulsar **Conectar cuenta Microsoft**.
2. Abrir la página oficial con **Abrir Microsoft** e ingresar el código que muestra ATR. La contraseña y el consentimiento se gestionan en Microsoft, no en ATR. Ingresar únicamente un código generado por esta instancia de ATR.
3. Esperar a que ATR indique que la cuenta Xbox quedó conectada.
4. Activar **Xbox (perfil)** y pulsar **Escanear biblioteca**. PA sigue teniendo una sola opción de escaneo y no es necesario activarla para consultar Xbox.
5. En Biblioteca, usar el filtro **Xbox**. El origen del historial original es **Xbox**. Un plan convertido muestra **Xbox → Steam**, conserva el AppID de destino y sigue perteneciendo al filtro Xbox.
6. Preparar equivalencias con Steam, revisar los códigos/nombres/descripciones y confirmar las correspondencias. Las acciones de Steam siguen pasando por SAM.

Se consulta el historial de títulos con paginación y después los logros por título. Hay lectores de contrato 2 (Xbox One/Series/PC) y contrato 1 para títulos identificados como Xbox 360. La compatibilidad real de esos servicios/títulos debe probarse. Un fallo de autenticación o un límite de consultas detiene la fase Xbox; un fallo aislado por título se registra. Los historiales anteriores se conservan. La lista visible puede incluir entradas guardadas que no se pudieron actualizar: ver su estado y Logs.

## Fechas, perfiles y biblioteca

El estado **Achieved**, o `unlocked` en el contrato antiguo, determina el desbloqueo. Una fecha por sí sola no lo desbloquea. Un desbloqueo con fecha ausente, inválida o fuera de rango se conserva como **Sin fecha**. Los códigos Xbox se muestran tal como los devuelve el servicio; no se interpretan como códigos de Steam.

Los perfiles se almacenan por separado mediante una identidad derivada del XUID. Cuando el historial PA contiene la misma identidad de perfil y título, ambas fuentes pueden quedar en el mismo grupo y el modo automático prioriza la cantidad de desbloqueos. No se combinan los logros de varias fuentes ni se agrupan perfiles conocidos distintos. Si PA utiliza un identificador de perfil diferente, puede conservarse otra entrada hasta contar con evidencia suficiente.

La copia local permite reabrir la biblioteca y preparar planes sin consultar Xbox. Un nuevo escaneo solo consulta Xbox si está activado. Desconectar la cuenta conserva los historiales y planes; no permite volver a consultar el perfil hasta conectar de nuevo.

## Datos y sesión

Bajo `%LOCALAPPDATA%\AchievementTimelineReplay\Xbox` se guardan:

- `connection.json`: Client ID público.
- `session.dat`: refresh token protegido con **DPAPI CurrentUser**, legible únicamente por el mismo usuario de Windows. No se guarda la contraseña, el access token ni el token XSTS en archivos.
- `Histories\<perfil>\<TitleID>.json`: copia de los desbloqueos leídos del servicio, con nombres, descripciones y fechas normalizadas. No contiene tokens.

Los snapshots y los planes mantienen las ubicaciones habituales de ATR. El actualizador conserva la carpeta Xbox. **Desconectar** elimina la sesión local de ATR; no cierra otras aplicaciones ni revoca todos los permisos en Microsoft. Para revocar el consentimiento, usar la administración de aplicaciones de la cuenta Microsoft.

No compartir `session.dat`, carpetas completas de datos ni códigos de acceso. Los historiales también son información personal, aunque no contengan credenciales. ATR no imprime tokens, códigos de dispositivo privados ni cuerpos de errores del servicio en el log.


## Etiquetas y filtrado desde v0.12.1

La interfaz muestra **Xbox Live (perfil)** en Escaneo, **Xbox Live** como origen y **Xbox Live → Steam** en historiales convertidos. Internamente se conserva la clave Xbox para no invalidar preferencias, identidades ni correspondencias. Las entradas originales de Steam continúan separadas.

El filtro se aplica después de consultar los logros del título: no se decide únicamente por el total anunciado en el historial de títulos. Por eso se conserva un desbloqueo aunque el total inicial sea cero. Desde v0.12.2 la visibilidad requiere al menos un desbloqueo leído del snapshot o del servicio; un total de catálogo positivo ya no alcanza.


## Criterio vigente desde v0.12.2

Solo juegos con uno o más desbloqueos. Reemplaza el filtro 0/0 de v0.12.1: 0/35 también se omite. No exige una fecha válida ni prueba propiedad. Los archivos de origen, snapshots y planes se conservan aunque una entrada deje de mostrarse.
