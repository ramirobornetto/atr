# Pendientes para la próxima actualización

## Aclarar las omisiones de logros en el log de escaneo

Solicitud del usuario: distinguir una caché vacía de un historial sin fechas utilizables. No cambiar todavía el comportamiento de importación.

Caso comprobado: BIN del AppID 611500 de 38 bytes, con únicamente `cache: { crc: 0, PendingChanges: 0 }`. No contiene estados ni fechas de logros. No publicar el archivo del usuario ni usarlo como prueba de que su cuenta no tiene desbloqueos. `PendingChanges` no es un contador de logros.

Mensajes propuestos, según evidencia disponible:

- Caché sin registros: «Omitido AppID 611500: caché local sin registros de logros».
- Estados explícitos que indiquen que todos están bloqueados: «Todos los logros figuran bloqueados».
- Desbloqueo explícito sin fecha utilizable: «Logros desbloqueados sin fecha».
- Fallo de lectura: «No se pudo leer el archivo», con la causa.
- Si el formato no permite determinar el estado, indicarlo sin inferir cero desbloqueos en la cuenta.

Conservar por separado los registros históricos sin correspondencia con el esquema actual. Al implementar, revisar los contadores del resumen para que reflejen estas categorías y no presenten omisiones normales como errores.
