# Corrección de compilación CS0051

En `WpfPrototype/MainWindow.xaml.cs` el constructor público `AchievementView(AchievementRecord, Action)` exponía un tipo interno. Se cambió solamente la accesibilidad del constructor de `public` a `internal`.

El resto del código de la integración funcional WPF v0.6.0 y el archivo original `Legacy_v0.5.3/AchievementTimelineReplay.cs` se conservan sin modificaciones.

## Probar en Windows

1. Extraer el ZIP en una carpeta nueva.
2. Ejecutar `WpfPrototype\build-wpf.cmd`.
3. Si compila, ejecutar `WpfPrototype\run-wpf.cmd`.
4. No hacer migraciones reales hasta validar la paridad con v0.5.3.

No se pudo ejecutar `dotnet build` en el entorno de generación (no dispone del SDK Windows/.NET).
