# Achievement Timeline Replay v0.5.3

Migrador de historiales locales de logros hacia una cuenta de Steam usando SamRewritten como motor.

## Flujo pensado

1. Jugaste anteriormente y quedó un historial local de logros.
2. Comprás el juego oficialmente.
3. **No lo ejecutás todavía.**
4. Abrís Achievement Timeline Replay.
5. Escaneás manualmente la biblioteca.
6. La app protege el historial en un snapshot que no vuelve a sobrescribir automáticamente.
7. Revisás orden y tiempos.
8. La app va desbloqueando los logros con SamRewritten.
9. Recién después abrís el juego oficial.

## Novedades v0.2

- El programa ya **no escanea automáticamente al iniciar**.
- Botón **Escanear biblioteca**.
- Barra de progreso y estado del escaneo.
- Cruce de:
  - biblioteca real de la cuenta mediante `samrewritten-cli list-apps`;
  - historiales en `Steam\appcache\stats`.
- Por defecto se ocultan historiales de AppIDs que no pertenecen a la biblioteca.
- Opción **Mostrar historiales fuera de mi biblioteca**.
- Opción **Mostrar juegos sin historial local**.
- Juegos sin historial:
  - no son gestionados por el scheduler;
  - muestran **Abrir en SAM** para gestionarlos directamente allí.
- Columna **Descripción**.
- Snapshot automático protegido:
  `%LOCALAPPDATA%\AchievementTimelineReplay\Snapshots\<cuenta>_<appid>\`
- El snapshot incluye:
  - `snapshot.json`
  - copia del `UserGameStats`
  - copia del `UserGameStatsSchema`
- Una vez creado, el snapshot se usa como fuente de verdad en escaneos posteriores.
- Botón **Consultar estado Steam** para comparar los logros históricos con el estado oficial actual.
- “Bloquear antes” pasa a llamarse **Modo reparación** y viene desmarcado por defecto.

## Edición individual de tiempos

Hay dos columnas editables:

### Espera nueva
Tiempo transcurrido desde el logro anterior.

Ejemplo: si figura:

`3d 22:23:38`

podés reemplazarlo por:

`00:30:00`

Los logros posteriores se corren automáticamente, manteniendo sus propias esperas.

### Momento nuevo
Momento acumulado desde el inicio del schedule.

Ejemplo:

`01:15:00`

significa que ese logro debe desbloquearse 1 h 15 min después del comienzo.

Al editarlo, la app calcula automáticamente la espera necesaria desde el logro anterior.

### Formatos aceptados

- `00:30:00`
- `3d 22:24:20`
- `3.22:24:20`
- `90m`
- `1h 15m`
- `30s`

Las celdas con tiempos manuales quedan resaltadas.

**Limpiar tiempos manuales** vuelve al escalado automático.

## Escalado automático

La duración total sigue funcionando como antes:
las diferencias temporales originales se escalan proporcionalmente.

Los overrides manuales tienen prioridad sobre el valor automático.

Por eso el programa muestra dos duraciones:

- **Duración total automática**: objetivo usado para escalar los intervalos no editados.
- **Reproducción efectiva**: duración real final luego de aplicar tus cambios manuales.

## Juegos sin historial local

La aplicación no inventa una timeline.

Marcá:

`Mostrar juegos sin historial local`

Seleccioná el juego y usá:

`Abrir en SAM`

La app intenta localizar la GUI de SamRewritten junto a `samrewritten-cli.exe` y abrirla directamente en ese AppID.

## Requisitos

- Windows.
- Steam abierto e iniciado en sesión para consultar/modificar logros.
- SamRewritten moderno recomendado.
- .NET Framework 4.x.

La v0.2 usa el CLI moderno de SamRewritten para:
- `list-apps`
- `list-achievements`
- `unlock`
- `lock`

## Compilar

1. Extraé el ZIP.
2. Doble clic en `build.cmd`.
3. Se genera `AchievementTimelineReplay.exe`.
4. Después podés abrir el `.exe` directamente.

## Importante

- Esta herramienta no cambia timestamps históricos directamente en el backend de Steam.
- Reproduce el patrón temporal desbloqueando los logros en tiempo real.
- El modo normal **no bloquea primero** los logros.
- “Modo reparación” existe para casos donde ya quedaron desbloqueados oficialmente y querés reconstruir la secuencia.
- SamRewritten desaconseja modificar logros de juegos multijugador.
- Los snapshots no se sobrescriben automáticamente, justamente para proteger el historial original.

## Próximos pasos previstos

Todavía no está implementado en esta v0.2:
- reconstrucción de sesiones exactas desde `Steam\logs\gameprocess_log.txt`;
- detección/compresión automática de pausas entre sesiones;
- importadores Goldberg/GBE Fork y otros formatos;
- drag & drop de filas.

Por ahora, las pausas largas se pueden corregir cómodamente editando **Espera nueva** o **Momento nuevo**.


## Cambios v0.3 — nombres públicos de Steam

- Un historial local válido **ya no se oculta por ownership**.
- Si existe `UserGameStats_<cuenta>_<appid>.bin` + schema y tiene al menos un
  `AchievementTimes > 0`, se considera un historial útil y aparece en la lista.
- Esto es intencional: un juego jugado con LumaCore/SteaMidra puede tener historial
  antes de que el usuario lo compre oficialmente.
- Los juegos de la biblioteca que no tienen historial local siguen siendo opcionales
  mediante **Mostrar juegos sin historial local** y se delegan a SAM.

### Nombre visible

La app ya no usa el `gamename` interno del schema como nombre principal.

Prioridad:

1. nombre público devuelto por SamRewritten;
2. caché local de nombres públicos;
3. catálogo público `AppID -> name` de Steam;
4. nombre interno del schema solamente como fallback si Steam no puede resolverlo.

Esto evita mostrar codenames como `Azura`, `Galileo`, `Project Lake`, etc. cuando
Steam conoce el nombre público correspondiente.

Los nombres resueltos se cachean en:

`%LOCALAPPDATA%\AchievementTimelineReplay\steam_public_names.tsv`

Los snapshots antiguos también actualizan su campo `GameName` cuando se vuelve a
resolver correctamente el nombre público. Los timestamps y logros del snapshot no se
sobrescriben.


## Corrección v0.3.1 — codificación Unicode

- La salida de SamRewritten se lee explícitamente como UTF-8.
- Se corrigen nombres que antes aparecían como:
  - `â„¢` en lugar de `™`
  - `â€“` en lugar de `–`
  - `Â®` en lugar de `®`
- El caché de nombres de la v0.3 se repara automáticamente al volver a escanear.
- No hace falta borrar `%LOCALAPPDATA%\AchievementTimelineReplay\steam_public_names.tsv`.


## Cambios v0.4

### Registrar tiempo de juego durante la migración

Nueva opción, activada por defecto:

`Registrar tiempo de juego durante la migración (SAM idle)`

Cuando empieza una migración la app ejecuta:

`samrewritten-cli.exe idle <APPID>`

y mantiene ese proceso activo durante toda la migración. Steam debería considerar ese
AppID como “en ejecución” y sumar tiempo de juego mientras se reproducen los logros.
El proceso idle se detiene al completar, cancelar o producirse un error.

### Drag & drop

Ya no es obligatorio usar `↑ Subir` / `↓ Bajar`.

Arrastrá una fila y soltala en otra posición. La app activa automáticamente
**Orden personalizado**.

### Tabla más grande y redimensionable

La tabla de logros y la zona inferior ahora están separadas por una barra horizontal.
Arrastrá esa barra hacia arriba o abajo para decidir cuánto espacio querés dar a:

- tabla de logros;
- controles + log.

La tabla recibe bastante más espacio por defecto.

### API ID

La columna visible **API ID** fue eliminada. La app sigue conservando internamente
los IDs necesarios para llamar a SamRewritten.


## Corrección v0.4.1

- Corrige un cierre inmediato al iniciar causado por configurar `SplitterDistance`
  antes de que WinForms hubiera calculado el tamaño real de la ventana.
- El tamaño inicial del divisor ahora se aplica en el evento `Shown`.
- Se agregaron manejadores globales de excepciones para que futuros errores de inicio
  se muestren en pantalla en vez de cerrar silenciosamente.


## Cambios v0.5

### SAM idle corregido
La app ahora lanza `samrewritten-cli idle <APPID>` a través de `cmd.exe`,
replicando la ejecución manual que sí registra el juego como activo en Steam.
Al finalizar/cancelar, se usa `taskkill /T` para cerrar `cmd.exe` y su proceso hijo.

### Duración según selección
Al marcar o desmarcar logros, **Duración total automática** se recalcula usando
solamente el rango entre el primer y el último logro actualmente seleccionados.

Ejemplo: si seleccionás del quinto logro al décimo, los cuatro primeros dejan de
formar parte del cálculo de duración y del factor de escalado.

### Barra de acciones siempre visible
`Iniciar migración`, `Cancelar`, la barra de progreso y el estado ahora están
fuera del panel redimensionable, en una fila fija inferior. Deberían seguir
visibles con la ventana en tamaño normal.

### Drag & drop vivo
- cursor de mano al pasar por una fila;
- cursor de movimiento mientras mantenés el click;
- las filas se reorganizan **durante el arrastre**, antes de soltar el botón;
- los tiempos/orden se recalculan en vivo.


## Ajustes v0.5.1

- El comando `idle` usa exactamente el patrón de comillas de la prueba manual en CMD.
- El recálculo automático de duración evita eventos intermedios mientras cambia horas/minutos/segundos.
- El drag vivo usa la mitad superior/inferior de cada fila para decidir dónde insertar.
- Se bloquean eventos de selección durante el refresco visual del drag para evitar recursión.


## Cambios v0.5.2

### SAM idle después de cada logro
SamRewritten abre una sesión corta de Steam para cada comando `unlock` y la cierra al
terminar. En Windows esa salida también puede hacer que Steam deje de considerar activo
el proceso `idle` que ya estaba abierto.

La migración ahora:
1. inicia `idle` antes de la espera inicial;
2. desbloquea un logro;
3. reinicia inmediatamente `idle`;
4. continúa esperando el siguiente logro;
5. al último logro termina la sesión.

En modo reparación, el bloqueo inicial se ejecuta antes de iniciar `idle`.

### Solo juegos con historial local
Se eliminó la casilla **Mostrar juegos sin historial local**. El selector muestra
únicamente juegos para los que se encontró historial local de logros aprovechable.
SAM sigue usándose para nombres públicos, consulta de estado y modificación de logros.


## Corrección v0.5.3

- Restaura `OutputContainsFailure(...)`.
- Restaura `FinishReplay(...)`.
- Corrige los errores CS0103 introducidos accidentalmente en v0.5.2.
- Mantiene todos los cambios de v0.5.2: reinicio de SAM idle tras cada logro,
  solo juegos con historial local, drag & drop vivo, duración por selección y
  barra de acciones fija.
