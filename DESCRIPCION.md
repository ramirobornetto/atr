# Achievement Timeline Replay — ATR

**Recuperá tus logros y prepará su migración a tu copia oficial en Steam.**

Achievement Timeline Replay es una aplicación para Windows que reúne historiales de logros, permite revisarlos y prepara su desbloqueo en Steam mediante SAM Rewritten. Su objetivo principal es recuperar el progreso obtenido en copias anteriores de tus juegos, incluidos historiales de emulación, y trasladar los logros compatibles a una licencia oficial.

En lugar de marcar todos los logros de golpe, ATR permite elegir cuáles aplicar, conservar o modificar su orden y distribuir los desbloqueos a lo largo de un plan de ejecución.

## Historiales compatibles

- **Steam:** lectura de historiales locales y sus definiciones de logros.
- **SteamIdra/LuaTools:** clasificación de historiales de Steam según los archivos LUA locales asociados al juego.
- **Goldberg y GSE:** lectura e importación de archivos `achievements.json` compatibles.
- **CODEX y RUNE:** lectura e importación de variantes compatibles de `achievements.ini`.
- **Playnite Achievements:** importación de historiales desde su caché local.
- **Xbox Live y Epic Games:** conexión de cuenta y consulta de juegos con logros desbloqueados.

Los historiales se mantienen separados por fuente e identidad. Cuando un juego tiene varias alternativas compatibles, podés elegir cuál usar; sus logros no se suman indiscriminadamente. Las listas de Xbox y Epic representan los historiales que ATR puede recuperar, no un inventario completo de juegos comprados.

## Herramientas para preparar la migración

- Biblioteca persistente con búsqueda, filtros por origen y selección del historial de partida.
- Correspondencias asistidas entre logros de otra plataforma y sus equivalentes de Steam, con búsqueda de juegos y textos en español o inglés cuando estén disponibles.
- Confirmación de equivalencias antes de incorporarlas al plan.
- Selección individual de logros, orden personalizado con arrastre en vivo y edición de tiempos.
- Duración automática, límite de esperas largas y espera inicial configurables.
- Consulta del estado actual en Steam para identificar logros ya desbloqueados.
- Guardado de planes, copias protegidas del historial importado y recuperación de ejecuciones interrumpidas.
- Progreso e informes por logro, con distinción entre operaciones verificadas, pendientes y fallidas.
- Actualizador integrado y distribución para Windows x64 con .NET incluido.

## Cómo funciona

**Escaneá o importá → elegí el historial → revisá las equivalencias si hacen falta → prepará el plan → comprobá Steam → confirmá y ejecutá.**

ATR consulta Xbox y Epic sin modificar sus logros. Steam es el destino de ejecución y SAM Rewritten realiza las consultas y operaciones de desbloqueo. Playnite es opcional: solo hace falta si querés importar sus historiales.

Las fechas del historial sirven para construir el orden y los intervalos del plan. **Los logros desbloqueados en Steam reciben la fecha real de ejecución:** ATR no puede asignarles sus fechas históricas originales. Las equivalencias entre plataformas o ediciones requieren revisión del usuario.

Para empezar, consultá el [tutorial de uso](TUTORIAL.md).
