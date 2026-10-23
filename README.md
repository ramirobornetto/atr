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

## v0.6.1 — ajustes de uso

- Arrastrar desde una columna de texto activa el orden personalizado. Soltar en la mitad superior/inferior inserta antes/después. Las flechas se conservan; las casillas y las celdas de tiempos no inician arrastre. Arrastrar cerca del borde permite desplazar la tabla.
- Casillas de inclusión de un clic, conservando acceso con teclado.
- Botones con acento eléctrico, texto oscuro al pasar el cursor y estado de presión propio.
- Separadores arrastrables para ajustar el ancho entre tabla y detalles y la altura de la tabla; los detalles tienen scroll.
- Biblioteca: históricos / total del esquema local. Al consultar Steam se actualiza a desbloqueados actuales / total devuelto por SAM. Los históricos del snapshot no se reemplazan por el estado de Steam.

Compilación cruzada y pruebas de datos sintéticos:

```sh
dotnet build WpfPrototype/AchievementTimelineReplay.Wpf.csproj -c Release -p:EnableWindowsTargeting=true
bash tests/run-core-checks.sh
```

Las pruebas comprueban conteos parciales/completos/vacíos, grupos sin caché, exclusión de estadísticas normales y conservación de snapshots existentes. El script de pruebas usa un directorio temporal aislado y requiere Bash/Linux y .NET 8. Los gestos y colores requieren comprobación manual en Windows.

### Comprobación manual de v0.6.1 en Windows

1. Compilar y abrir con los `.cmd` habituales, escanear y seleccionar un juego.
2. Con orden personalizado desmarcado, arrastrar una fila desde su nombre arriba/abajo. Verificar inserción antes/después y las flechas; restaurar orden.
3. Marcar/desmarcar una casilla de una fila sin seleccionar con un solo clic. Verificar el contador de selección y comprobar también con la barra espaciadora.
4. Editar una espera o momento: no debe iniciar un arrastre. Verificar que las consultas Steam conservan el plan, la selección y los tiempos manuales.
5. Pasar el cursor, presionar y navegar con teclado por los botones: texto legible en todos los estados.
6. Arrastrar el separador entre tabla y detalles. Reducir/ampliar la ventana y comprobar scroll y columnas.
7. Comparar el contador histórico con el esquema y, después de Consultar Steam, con el estado actual. No iniciar migraciones reales para estas comprobaciones.

### Límite opcional de esperas largas

En el plan, activar **Limitar esperas automáticas largas** y elegir el máximo en minutos (valor inicial 30; opción desactivada por defecto). El límite se aplica a cada intervalo automático después del escalado de duración, incluyendo intervalos entre días diferentes. Los tiempos manuales tienen prioridad y pueden superar el máximo. La duración efectiva y las horas estimadas reflejan el límite; la confirmación muestra el máximo elegido. Un máximo inválido impide iniciar la migración. El historial y los snapshots originales no cambian.

El signo **?** explica la diferencia entre tiempo calendario y tiempo jugado. No se infieren sesiones reales ni se cambian timestamps históricos en Steam. No se agregan funciones para repartir logros entre días.

Prueba manual adicional: comparar el mismo plan con el límite desactivado/activado, probar una espera manual superior al límite y comprobar que los valores inválidos impiden iniciar. Desactivar el límite debe recuperar el escalado automático completo. Las pruebas del núcleo cubren estas reglas con intervalos de 18 horas y 3 días.
