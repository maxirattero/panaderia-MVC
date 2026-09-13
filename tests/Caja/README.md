# Comprobaciones del cierre de caja

Con PostgreSQL local disponible en `127.0.0.1:55439` (usuario `postgres`), ejecutar desde la raíz:

```powershell
dotnet run --project tests/Caja
```

`CAJA_TEST_PORT` permite elegir otro puerto. El ejecutable crea una base nueva por ejecución, sin consultar secretos ni conexiones de producción. El acceso local debe estar configurado previamente. Guarda la conexión de prueba en `.artifacts/caja-checks/connection.txt` para una eventual revisión visual.

Comprueba migración desde el esquema anterior, conservación de cierres históricos, cobros combinados, duplicación de compras y cobros parciales, reparto y centavos, compras desde reserva, saldos, transferencias concurrentes, pagos parciales a cada persona, devoluciones y protección de períodos cerrados.

El costo mostrado es una estimación por receta y empaque. Los pedidos nuevos congelan el costo de ingredientes al entregarse; los pedidos anteriores se reconstruyen con los precios disponibles al cerrar. La fotografía del cierre no cambia después. No se mide consumo real de producción ni desperdicio.

Al comenzar a usar la versión nueva, el operador debe registrar una única apertura con saldos comprobados y clasificar los movimientos antiguos de las semanas que desee cerrar. Los cobros y compras posteriores guardan su cuenta desde el formulario. Guardar un cierre no ejecuta ni registra por sí solo transferencias o retiros; cada operación realizada se registra contra su pendiente.
