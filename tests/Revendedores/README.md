# Pruebas de revendedores

Requieren PostgreSQL local en `127.0.0.1:55459`, usuario `postgres` con autenticación de confianza para pruebas.

Desde la raíz del repositorio:

```powershell
dotnet run --project tests/Revendedores
```

Crea una base descartable con nombre aleatorio, ejecuta migraciones y prueba los formularios HTTP reales con cookies y antiforgery. No lee secretos ni accede a producción. Comprueba edición de cuentas, revocación de sesiones, confirmación directa, importes, reservas y devolución de stock, y conservación del checkout minorista. El servidor temporal usa el puerto 5096.

Para revisar las pantallas después de las pruebas, agregá `-- --preview`. Las credenciales locales se muestran en la salida. Al cerrar normalmente, se elimina la base temporal.
