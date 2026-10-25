<div align="center">

**Español** · [English](en-description.md)

<img width="256" height="256" alt="image" src="https://github.com/user-attachments/assets/87711d51-5cc5-47b7-8c29-2f6c6a58d22d" />

# Achievement Timeline Replay — ATR

**[Descargar v0.19.1 para Windows x64](https://github.com/ramirobornetto/atr/releases/download/v0.19.1/ATR_v0.19.1_Windows_x64.zip)**

**Recuperá tus logros y prepará su migración a tu copia oficial en Steam.**

¿Alguna vez te pasó de tener una copia "no oficial" de un juego y luego decidiste comprarlo? Si estuviste en esa situación, seguro te encontraste con el escenario de querer desbloquear los logros que ya habías desbloqueado anteriormente para poder lucirlos en tu perfil de Steam. Ya existen aplicaciones que cumplen esa función, pero tener que elegir manualmente cuál desbloquear y cuál no, ingresar manualmente horas random para cada uno, checkear y re-checkear qué se va a desbloquear (ojo, piratas pero honestos) puede ser una tarea muy tediosa.

Soy una persona simple: tenía un problema que quería resolver y no encontraba una herramienta que lo hiciera. Frente a mi falta de habilidades y conocimiento de programación, trabajé 100% con CODEX para desarrollar esta solución. Repito: NO SOY PROGRAMADOR.

Achievement Timeline Replay es una aplicación para Windows que reúne historiales de logros, permite revisarlos y prepara su desbloqueo en Steam mediante [SAM Rewritten](https://github.com/PaulCombal/SamRewritten). Su objetivo principal es recuperar el progreso obtenido en copias anteriores de tus juegos, incluidos historiales de emulación, y trasladar los logros compatibles a una licencia oficial. **NO DESBLOQUEA LOGROS QUE NO HAYAN SIDO DESBLOQUEADOS PREVIAMENTE.** Para eso ya existen otras soluciones.

En lugar de desbloquear todos los logros de golpe, ATR permite elegir cuáles aplicar, conservar o modificar su orden y distribuir los desbloqueos a lo largo de un plan de ejecución.



## Historiales compatibles

**Steam:** lectura de historiales locales y sus definiciones de logros.

**SteamIdra/LuaTools:** clasificación de historiales de Steam según los archivos LUA locales asociados al juego.

**Goldberg y GSE:** lectura e importación de archivos `achievements.json` compatibles.

**CODEX y RUNE:** lectura e importación de variantes compatibles de `achievements.ini`.

**Playnite Achievements:** importación de historiales desde su caché local.

**Xbox Live y Epic Games:** conexión de cuenta y consulta de juegos con logros desbloqueados.


Los historiales se mantienen separados por fuente e identidad. Cuando un juego tiene varias alternativas compatibles, podés elegir cuál usar; sus logros no se suman indiscriminadamente. Las listas de Xbox y Epic representan los historiales que ATR puede recuperar, no un inventario completo de juegos comprados.




## Herramientas para preparar la migración

<img width="1426" height="893" alt="image" src="https://github.com/user-attachments/assets/aaa1e229-c93d-4965-9b43-a0df8bfb1b88" />



-Biblioteca persistente con búsqueda, filtros por origen y selección del historial de partida.

-Correspondencias asistidas entre logros de otra plataforma y sus equivalentes de Steam, con búsqueda de juegos y textos en español o inglés cuando estén disponibles.

-Confirmación de equivalencias antes de incorporarlas al plan.

-Selección individual de logros, orden personalizado con arrastre en vivo y edición de tiempos.

-Duración automática, límite de esperas largas y espera inicial configurables.

-Consulta del estado actual en Steam para identificar logros ya desbloqueados.

-Guardado de planes, copias protegidas del historial importado y recuperación de ejecuciones interrumpidas.

-Progreso e informes por logro, con distinción entre operaciones verificadas, pendientes y fallidas.

-Actualizador integrado y distribución para Windows x64 con .NET incluido.




**Interfaz en español, inglés y portugués de Brasil**, con elección de idioma guardada entre sesiones.

## Cómo funciona

**Escaneá o importá → elegí el historial → revisá las equivalencias si hacen falta → prepará el plan → comprobá Steam → confirmá y ejecutá.**

ATR consulta Xbox y Epic sin modificar sus logros. Steam es el destino de ejecución y SAM Rewritten realiza las consultas y operaciones de desbloqueo. Playnite es opcional: solo hace falta si querés importar sus historiales.

Las fechas del historial sirven para construir el orden y los intervalos del plan. **Los logros desbloqueados en Steam reciben la fecha real de ejecución:** ATR no puede asignarles sus fechas históricas originales. Las equivalencias entre plataformas o ediciones requieren revisión del usuario.

Para empezar, consultá el [tutorial de uso](es-howto.md).

<a href='https://cafecito.app/ramirobornetto' rel='noopener' target='_blank'><img srcset='https://cdn.cafecito.app/imgs/buttons/button_1.png 1x, https://cdn.cafecito.app/imgs/buttons/button_1_2x.png 2x, https://cdn.cafecito.app/imgs/buttons/button_1_3.75x.png 3.75x' src='https://cdn.cafecito.app/imgs/buttons/button_1.png' alt='Buy me a coffee' /></a>

</div>
