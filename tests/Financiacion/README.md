# Financiación de insumos

Pruebas de integración con PostgreSQL local en `127.0.0.1:55439`, usuario `postgres`.
`CAJA_TEST_PORT` permite cambiar el puerto. Cada ejecución crea una base nueva; no lee
secretos ni configuración de Supabase/Railway.

```powershell
dotnet build tests/Financiacion/Financiacion.csproj -m:1
dotnet run --project tests/Financiacion --no-build
dotnet run --project tests/Caja
node --test tests/compra-envio.test.cjs
```

El usuario local necesita crear bases y, para simular Supabase, roles locales
`anon`, `authenticated` y `service_role` si todavía no existen. Los grants por defecto
de prueba se aplican exclusivamente a la base nueva.

Se comprueban migraciones de ida/vuelta, conservación de compras anteriores, RLS,
revocación de grants heredados, permisos del propietario, compras con envío y tres
orígenes, stock, asignaciones entre varias compras y lotes, límites de costo,
adelantos por aportante, ventas cobradas/entregadas, consumo FIFO, vistas previas sin
mutaciones, pagos parciales y concurrentes, protección del historial, cierre sin doble
descuento y recuperación completa sin superar el aporte original.

La conexión ficticia se guarda en `.artifacts/financiacion-checks/connection.txt`
para revisar las pantallas localmente.
