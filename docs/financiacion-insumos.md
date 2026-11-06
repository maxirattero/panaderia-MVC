# Compras, aportes personales y reintegros

## Uso

1. En **Compras → Registrar compra**, cargar insumos y envío. Indicar cuánto pagaron
   Maxi y Ani. El resto corresponde a Masa Viva y usa la cuenta seleccionada.
   Solo esa parte genera un egreso de caja. Un aporte personal crea deuda, sin
   inventar una entrada o salida de dinero de la panadería.
2. En **Inversiones personales**, crear un lote por producto con fecha y unidades
   obtenidas. Asignar a ese lote el importe de cada compra que se destinó a él.
   Una compra puede distribuirse entre varios productos/lotes y un lote reunir varias
   compras. El costo asignado incluye el envío que corresponda y se distribuye entre
   los orígenes en proporción a lo pagado en la compra. No es una asignación de stock
   físico: el stock se incorpora una única vez al registrar la compra.
3. Las entregas reales totalmente cobradas generan reintegros según el costo
   financiado por unidad. Para un producto con varios lotes se consume primero la
   fecha más antigua, con desempate por identificador. Una unidad nunca consume dos
   lotes. Los anticipos de clientes y los pedidos sin entregar no generan reintegros.
4. Un adelanto puede confirmarse antes de vender, por compra y aportante. Los pagos
   anticipados compensan también las sugerencias de otras compras de esa misma
   persona. Nunca compensan aportes de la otra persona.
5. El cierre guarda el consumo de lotes y separa los reintegros sugeridos, descontando
   pagos y reservas anteriores. El límite es la base positiva de cobros menos
   devoluciones y gastos elegidos. Si no alcanza, el saldo conserva su sugerencia
   para otro cierre. Las deudas se atienden por identificador de aporte.
6. **Guardar cierre no confirma pagos.** Confirmar un reintegro permite pagos parciales
   desde efectivo o banco, comprueba fondos y reduce la misma deuda. Los reintegros
   reservados se pagan desde su pendiente. Los adelantos no pueden consumir importes
   que ya estén reservados en un cierre.

## Integración con reserva y reparto

La base del cierre es:

`cobros - devoluciones - gastos elegidos - reintegros separados en ese cierre`.

Un adelanto manual es un gasto elegido en la semana en que se paga. La confirmación
de un reintegro ya separado no vuelve a descontarse del reparto. La compra pagada
desde la reserva solo reduce esa cuenta; el costo por receta sigue siendo informativo.
Sobre la base restante se aplica el porcentaje actual de reserva y después se divide
el resto entre Ani y Maxi, conservando los centavos.

Los cierres conservan generado nuevo, adelantos de su período, reintegros separados,
porcentaje y reparto. Los saldos de deuda mostrados junto a un cierre guardado son
actuales. Al cerrar una semana pasada se consideran los pagos personales ya
confirmados, incluso si ocurrieron después de su corte, para no reservar una deuda
ya cancelada. Los adelantos posteriores se descuentan del reparto de su propia semana.

Las asignaciones quedan congeladas al consumir el lote en un cierre. Con lotes
financiados, las semanas deben cerrarse cronológicamente. Las devoluciones posteriores
conservan el costo del insumo de una entrega ya registrada; el reintegro corresponde
al insumo consumido, no a la ganancia. No se revierte automáticamente una compra o un
pago confirmado desde movimientos manuales de caja.

## Esquema y seguridad

La migración `20261106085224_IncorporarFinanciacionInsumos` agrega `AportesCompra`,
`LotesFinanciados`, `AsignacionesCompraLote`, `VentasLote`, `ReservasReintegro` y
`PagosReintegro`, con claves externas `Restrict`, índices únicos de operación y
precisión de costos `(18,4)`. Los pagos de caja admiten centavos y el cálculo
proporcional usa acumulados para no repartir un mismo centavo dos veces.

Todas las mutaciones comparten la transacción y el bloqueo de caja existentes.
Una clave de operación persistida impide duplicar también una compra completamente
personal, que no tiene movimiento de caja asociado. Los detalles vendidos y las
reservas por aporte/cierre tienen índices únicos adicionales.

Son tablas internas del servidor MVC con autorización Admin mediante Identity.
La migración habilita RLS y da grants explícitos de tabla y secuencia al usuario
PostgreSQL que ejecuta la migración y opera EF Core. Revoca permisos de `PUBLIC`,
`anon`, `authenticated` y `service_role` si esos roles existen, incluidos grants
heredados por defecto de Supabase. No agrega políticas de Data API porque la
aplicación no la usa para estos datos. El propietario accede directamente; una sesión
Supabase no equivale a un usuario Admin MVC. Una instalación con usuario de ejecución
distinto del propietario deberá provisionar ese rol explícitamente.

La migración reconstruye únicamente aportes de Masa Viva para compras históricas
con un pago único, cuenta conocida y total coincidente. No presume aportes personales
ni deudas históricas cuando falta esa evidencia. En producción se ejecuta por el
circuito habitual `MigrateAsync` al arrancar el despliegue.
