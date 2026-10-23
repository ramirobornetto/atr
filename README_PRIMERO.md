# Achievement Timeline Replay — WPF v0.6.0 (integración funcional, PRUEBA)

**La v0.5.3 original incluida sigue intacta, sin cambios en un solo byte de su código.**

Esta versión sustituye el prototipo WPF de solo lectura: tiene escaneo asíncrono con barra de progreso y cancelación, nombres públicos (SAM → caché v0.5.3 → catálogo Steam → nombre interno), selección de cuenta más reciente, biblioteca desplazable con filtro, snapshots originales (leídos sin sobrescribir), plan de 10 columnas, selección, duración, reordenamiento, edición de esperas y momentos, consulta a SAM y ejecución con idle/cancelación/logs.

## Para probar en Windows

1. Descomprimí el ZIP en una carpeta nueva (no pises tu instalación funcional v0.5.3).
2. Dentro de `WpfPrototype`, ejecutá `build-wpf.cmd` con el .NET 8 SDK que ya instalaste.
3. Si compila, abrí `run-wpf.cmd` (o el ejecutable en `bin\Release\net8.0-windows`).
4. Elegí el mismo `samrewritten-cli.exe` y carpeta de Steam que usabas antes. Validá SAM.
5. Probá escaneo, biblioteca y plan. Verificá nombre, cantidad y fechas de uno o dos juegos contra v0.5.3.
6. Para ejecución real, verificá primero los datos anteriores y probá un juego/plan pequeño y conocido; el proceso altera tu cuenta Steam al confirmar.

**Advertencia de verificación:** el código WPF se preparó fuera de Windows y no pudo compilarse/ejecutarse en este entorno. Hay validación estática de XAML y de los enlaces de eventos, pero la paridad funcional en tu equipo necesita pruebas. No sustituyas tu v0.5.3 todavía.

## Carpeta original

`Legacy_v0.5.3` es una copia literal del último ZIP funcional que compartiste. Compilación con `build.cmd` y ejecución como antes, independiente de WPF.

## División funcional

- `WpfPrototype/Core/SteamHistory.cs`: **modelos, lector Binary KeyValues e historial** extraídos del fuente de v0.5.3.
- `WpfPrototype/Core/LegacyEngine.cs`: servicios adaptados a .NET 8/WPF (escáner, caché, snapshots, planificación, SAM, replay). Los métodos de cálculo y el orden de ejecución conservan el comportamiento de v0.5.3; se sustituyeron dependencias de Windows Forms y `System.Web.Script.Serialization`.
- `WpfPrototype/MainWindow.xaml` y `.xaml.cs`: solo presentación, estado y controles WPF.

## Límites actuales

- Solo Steam local: importadores Goldberg/GBE/RUNE/CODEX pendientes.
- No se han realizado pruebas automatizadas contra datos reales de tu cuenta ni smoke tests de Windows.
- Las fechas en Steam dependen de cuándo el servicio registre cada desbloqueo; la herramienta intenta reproducir sus intervalos históricos, no sobrescribir marcas temporales del servidor.
- La opción de reparar bloqueando logros seleccionados **modifica Steam**. Por defecto queda desactivada.
- El modo `SAM idle` se reinicia después de los desbloqueos intermedios, como en 0.5.3. La plataforma no garantiza que Steam compute cada minuto de juego.

## Problemas

Si aparece un error de compilación, copiá el primer mensaje `error CS...` junto con su archivo y línea, no solo la última línea. Si compila, anotá resultados del escaneo, plan y consultas a SAM antes de migrar logros reales.
