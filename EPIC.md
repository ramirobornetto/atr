# Epic Games directo — v0.14.0

La fuente opcional **Epic (perfil)** consulta la biblioteca de tu cuenta y los desbloqueos disponibles en los servicios de Epic Games. Es independiente de PA y está desactivada inicialmente. Solo se muestran juegos con al menos un logro desbloqueado; un desbloqueo sin fecha válida también cuenta.

## Conectar

1. En **Escaneo**, abrir **Conectar Epic…**.
2. Pulsar **1. Iniciar sesión en Epic**. La contraseña, la autenticación en dos pasos y cualquier comprobación se completan en el navegador, en el sitio oficial de Epic.
3. Después, pulsar **2. Obtener código**. Epic debe mostrar una respuesta que contiene `authorizationCode`.
4. Copiar el valor de ese campo y pegarlo en ATR. También se admite pegar el JSON que contiene `authorizationCode`. No pegar la contraseña, un access token ni archivos de sesión de otro programa.
5. Pulsar **Conectar** y comprobar el nombre de cuenta que muestra ATR.
6. Activar **Epic (perfil)** y escanear. Usar el filtro **Epic** en Biblioteca.

El código es temporal y de un solo uso. Si venció, obtener uno nuevo. Si el navegador tiene abierta otra cuenta, cambiar de cuenta en Epic antes de obtener el código. Desconectar en ATR no cierra la sesión del navegador ni del launcher.

No requiere Entra ni un registro Microsoft. Es un flujo compatible con **Epic Games Launcher**, como el utilizado por PlayniteAchievements, con constantes de cliente launcher públicas; no es un registro OAuth propio de ATR. No se oculta esa identidad ni se extraen las sesiones de PA, Heroic o del launcher. La disponibilidad del flujo y de sus permisos depende de Epic y puede cambiar.

## Qué se consulta

Se pagina la biblioteca Windows del servicio Epic. Para cada título se consulta el catálogo de logros por sandbox y el progreso de la cuenta conectada por producto de logros. Los alias de biblioteca que resuelven al mismo sandbox/producto usan la misma identidad y se guarda una sola fuente. Si falta un título legible se intenta obtenerlo del catálogo; si no está disponible, se muestra un identificador explícito para que el usuario elija el destino por nombre manualmente.

La integración cubre los logros que expone ese servicio de Epic Games. **No garantiza recuperar todos los logros EOS de todos los juegos**, logros de emuladores ni progreso que el servicio no haya sincronizado. No convierte una compra en un historial de logros. Un juego sin un producto de logros disponible se omite; una respuesta incompleta, un error GraphQL o un perfil inaccesible no se interpretan como cero desbloqueos.

El estado `unlocked: true` determina el desbloqueo. Las fechas ausentes o inválidas se conservan como **Sin fecha**. Una fecha aislada no transforma un logro bloqueado en desbloqueado. Si un código histórico ya no aparece en el esquema, se conserva como identificador original, sin inventar un equivalente Steam.

Los errores se registran y los historiales anteriores se conservan. Un rechazo de sesión o un límite de consultas detiene la fase Epic. Un error aislado por título permite continuar con los demás. La barra global incluye esta fase y el guardado final.

## Correspondencias con Steam

Elegir el historial Epic, preparar equivalencias y seleccionar el juego/edición correctos de Steam. Se conservan los códigos Epic y Steam en columnas separadas. Los logros del desplegable de Steam están ordenados alfabéticamente por nombre; la columna Fecha se quitó solo de esa ventana. Las fechas originales siguen disponibles en Plan y en los snapshots.

Solo las correspondencias confirmadas preparan un historial **Epic → Steam**. La ejecución sigue pasando por SAM y el resumen de migración. No se escribe ni se desbloquea nada en Epic.

Los perfiles conocidos diferentes permanecen separados. No se unen listas por parecido de nombres. Una entrada de PA y otra de Epic directo pueden permanecer separadas si sus identificadores de juego no permiten demostrar que corresponden al mismo producto. Se mantienen los datos de cada fuente.

## Sesión y datos locales

`%LOCALAPPDATA%\AchievementTimelineReplay\Epic\session.dat` guarda el refresh token y el contexto de cuenta cifrados con **DPAPI CurrentUser**. El access token solo permanece en memoria. La renovación comprueba que siga siendo la misma cuenta y guarda la rotación del refresh token. El código de autorización se limpia del campo y no se guarda.

Las copias normalizadas de los logros están en `Epic\Histories\<perfil>`, con identificadores de archivo derivados por hash. Los snapshots, planes e informes usan las carpetas habituales. Desconectar elimina solo la sesión local de ATR. El actualizador conserva la carpeta Epic; borrar la biblioteca no elimina sesiones, snapshots ni planes.

No compartir códigos ni `session.dat`. Los historiales también son datos personales. No se imprimen códigos, tokens ni cuerpos privados de errores de Epic en los logs. El código de ATR solo usa autenticación y consultas de lectura; esto no significa que la credencial del cliente launcher tenga un permiso OAuth limitado técnicamente a esas consultas.

## Estado y validación

Se implementaron la conexión, renovación, consultas paginadas, lectura de logros, persistencia, perfiles separados y correspondencias con Steam. Las pruebas automatizadas usan respuestas simuladas. **El acceso real a Epic y la interfaz de conexión todavía deben validarse en Windows con una cuenta del usuario.** La compatibilidad con todos los títulos no está verificada.

Se tomó como referencia el protocolo de PlayniteAchievements, disponible bajo MIT; ver [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
