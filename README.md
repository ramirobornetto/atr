# Achievement Timeline Replay

Aplicación Windows para revisar y reproducir historiales locales de logros de Steam mediante SamRewritten.

## Compilar y abrir en Windows

1. Instalar el SDK de .NET 8.
2. Ejecutar `WpfPrototype\build-wpf.cmd`.
3. Ejecutar `WpfPrototype\run-wpf.cmd`.

Steam y SamRewritten son necesarios para consultar o modificar logros; no son necesarios para compilar.

## Corrección de compilación

Se corrigió CS0136 en `WpfPrototype/Core/LegacyEngine.cs`: la variable del nombre público del juego ahora se llama `appName`, evitando el conflicto con el contador `n` del mismo método. El resto de la lógica no cambia.

Validado con SDK 8.0.426 en Linux:

```sh
dotnet build WpfPrototype/AchievementTimelineReplay.Wpf.csproj -c Release -p:EnableWindowsTargeting=true
```

Resultado: 0 errores, 2 advertencias existentes (CS0169 y CS0649). La interfaz WPF y la integración con Steam/SamRewritten requieren validación en Windows; no se ejecutaron migraciones ni se modificaron cuentas.

`Legacy_v0.5.3` conserva los cinco archivos originales byte a byte. Las instrucciones históricas están en `README_PRIMERO.md`, `PARIDAD_V053.md` y `PRUEBAS_WINDOWS.md`; sus notas sobre compilación corresponden al paquete recibido, antes de esta corrección.

La captura de referencia visual no representa la interfaz actual ni cambios realizados en esta corrección.
