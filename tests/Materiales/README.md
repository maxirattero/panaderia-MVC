# Pruebas de materiales y presentación

Requiere PostgreSQL local en `127.0.0.1:55439` con usuario `postgres`. Cada ejecución
crea una base nueva; no lee secretos ni usa datos de producción. `CAJA_TEST_PORT`
permite cambiar el puerto. El usuario necesita crear bases y los roles locales de
prueba anon/authenticated/service_role si todavía no existen, para simular grants
por defecto de Supabase.

```powershell
dotnet build tests/Materiales/Materiales.csproj -m:1
dotnet run --project tests/Materiales --no-build
```

Comprueba migración de ida/vuelta, RLS y grants/secuencia; conservación de pedidos
antiguos; materiales y cantidades fraccionarias; etiqueta específica/habitual;
reemplazo de bolsa y prevención de duplicados; copia de productos; validaciones;
fotografía histórica; cambios de precios y nombres; edición de pedidos y cambios
de bolsa/etiqueta; ampliaciones web; costo del cierre y ausencia de movimientos de
caja/reintegros nuevos. La conexión ficticia queda en
`.artifacts/materiales-checks/connection.txt` para revisión visual local.

Regresiones relacionadas: tests/Caja, tests/Financiacion, tests/Costos,
tests/StoreSettings y las pruebas JavaScript de presentación, envío, stock y checkout:

```powershell
node --test tests/product-materials.test.cjs tests/compra-envio.test.cjs tests/order-stock.test.cjs tests/store-checkout.test.cjs
```
