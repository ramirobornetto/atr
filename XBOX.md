# Xbox Live — conexión desde v0.15.0

El registro de ATR en Microsoft ya está preconfigurado en el programa. Los usuarios no necesitan Azure, Entra, crear directorios ni introducir un Client ID. No hay un campo ni una sección avanzada para mostrar ese identificador en la app.

## Conectar una cuenta

1. Abrir **Escaneo → Conectar Xbox…**.
2. Pulsar **Conectar cuenta Microsoft**. ATR solicita un código temporal y abre la página oficial de Microsoft.
3. Usar **Copiar código**, pegarlo en Microsoft e iniciar sesión con la cuenta Xbox correspondiente. Si el navegador no se abrió, usar **Abrir Microsoft**.
4. Esperar a que ATR indique que la cuenta quedó conectada. Al cerrar la ventana, la fuente Xbox Live queda seleccionada para el próximo escaneo.
5. Pulsar **Escanear biblioteca**.

El código visible es temporal y autoriza la cuenta del usuario; no es el identificador de desarrollo de la aplicación. Cancelar detiene la espera. La contraseña y el consentimiento se completan en Microsoft, no en ATR.

Las sesiones anteriores se conservan. Por compatibilidad, si una instalación ya tenía un registro de cliente asociado a su sesión, se conserva ese vínculo internamente; no se expone en la interfaz. El identificador público predeterminado está en el código de ATR. El responsable del proyecto mantiene el registro Microsoft; los usuarios finales solo autorizan sus propias cuentas.

## Datos y escaneo

La fuente consulta el historial del perfil Xbox Live, no una lista completa de compras Microsoft Store. Solo muestra juegos con al menos un logro desbloqueado, incluidos los que no tengan una fecha válida. Las entradas sin desbloqueos no se importan. Los códigos Xbox y los AppID/API names Steam se mantienen separados.

El resumen distingue recuperados, sin catálogo disponible, sin desbloqueos y errores reales. Si falla la conexión, se conservan los historiales anteriores y se indica la fase detenida. No se mezclan fuentes durante el escaneo.

Para transferir a Steam se preparan correspondencias confirmadas, un historial Xbox Live → Steam y un plan ejecutado mediante SAM. ATR no modifica Xbox. Consultar MIGRACIONES.md para revisión previa, recuperación e informes.

## Sesión y desconexión

La sesión renovable se protege con DPAPI CurrentUser bajo `%LOCALAPPDATA%\AchievementTimelineReplay\Xbox`. Desconectar elimina la sesión local de ATR y desactiva esa fuente de escaneo al cerrar el diálogo; conserva historiales, snapshots y planes. No desconecta otras aplicaciones ni revoca todos los consentimientos de la cuenta Microsoft.

El actualizador conserva estos datos. No compartir session.dat ni códigos temporales. El registro interno de cliente es un identificador público, no una contraseña ni un Client Secret.

## Validación

El usuario ya confirmó lectura Xbox y migración a Steam en versiones anteriores. Se probaron automáticamente configuración predeterminada, renovación simulada, persistencia y preservación durante actualizaciones. El nuevo flujo sin configuración y una segunda cuenta Microsoft requieren una prueba manual real en Windows. No se afirma haber probado cuentas ajenas desde el entorno de desarrollo.
