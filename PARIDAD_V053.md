# Lista de paridad contra v0.5.3

El **origen de verdad** es el archivo `Legacy_v0.5.3/AchievementTimelineReplay.cs`, idéntico al ZIP compartido. Esta lista separa *implementado* de *validado*.

| Función v0.5.3 | Implementación WPF | Validación Windows |
|---|---|---|
| Parseo UserGameStats/Schema y fechas | Mismo `HistoryLoader` / `BinaryKvReader` | Pendiente |
| Historiales con timestamps, cuenta reciente | `LegacyScanner` | Pendiente |
| Nombres públicos SAM → caché → catálogo → interno | `LegacyScanner`, `LegacyTools.RepairMojibake` | Pendiente |
| Snapshots persistentes originales | `LegacyScanner.LoadOrCreateSnapshot` (lectura compatible, no reescritura) | Pendiente |
| Escaneo en segundo plano y cancelable | `ScanClick` + servicio + `CancellationToken` | Pendiente |
| Biblioteca y selección de juego | `GamesGrid`, `GameChanged` | Pendiente |
| Columnas de plan originales | `AchievementsGrid` con 10 columnas | Pendiente |
| Intervalos de reproducción, escala, selección | `LegacyPlanner` | Pendiente |
| Tiempos editables, orden personalizado | `AchievementCellEditEnding`, arrastre, subir/bajar | Pendiente |
| Estado del logro en Steam | `SamRunner.States`, `QuerySteamClick` | Pendiente |
| Abrir interfaz SAM | `OpenSamClick` | Pendiente |
| Unlock/lock, confirmación previa | `LegacyReplayer`, `StartReplayClick` | Pendiente |
| Reiniciar SAM idle después de unlock | `LegacyReplayer` | Pendiente |
| Cancelación y logs de reproducción | `CancellationToken`, `ReplayEvents`, panel Logs | Pendiente |
| Modo minimizar → bandeja | **No migrado**, minimiza normalmente a barra de tareas | Pendiente |

**Garantía:** el archivo legacy no se edita. **No garantía:** hasta compilar/probar en Windows no podemos asegurar paridad de ejecución. Mantener backups de snapshots y v0.5.3 estable.
