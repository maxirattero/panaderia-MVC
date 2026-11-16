# Materiales y presentación de productos

Desde **Productos → Detalle → Editar materiales y presentación** se configura una
lista opcional de insumos y cantidades por unidad: moldes, moños, cintas y envoltorios.
También aparece al crear o editar un producto. Los materiales deben existir en
Insumos como Empaque, Consumible o Etiqueta, estar activos y tener rendimiento válido.

La bolsa principal se mantiene en su selector habitual y puede cambiarse por papel,
sellada u otro empaque al cargar el pedido. Elegir otra bolsa la reemplaza. Si ese
mismo insumo figura como adicional, una unidad ya está incluida en la bolsa y no se
cuenta dos veces. Dos envoltorios indicados expresamente conservan dos unidades.

La lista admite una etiqueta específica con su cantidad. Se aplica únicamente si
se marca Incluir etiqueta. Sin una específica se utiliza la etiqueta activa habitual
(menor identificador), conservando el funcionamiento de los productos anteriores.
Las etiquetas comerciales de la tienda —Vegano, Masa madre, etc.— son independientes.

El detalle muestra elaboración base, materiales, bolsa y etiqueta. La calculadora
de CMV y margen usa el total de la presentación habitual y se actualiza al cambiar
los materiales; editar cantidades o costos no modifica por sí solo los precios de
venta. Los clientes a precio de costo conservan la regla existente de solo ingredientes
o costo base manual.

Los pedidos nuevos guardan una fotografía de insumos, nombres, cantidades y costos
por unidad. Cambiar después Insumos o la presentación del producto no altera pedidos
anteriores. Al editar una línea conservando producto, bolsa y etiqueta se preserva
su costo; cambiar bolsa o etiqueta cotiza únicamente el componente cambiado.
Una ampliación de tienda con presentación distinta queda en una línea separada.

Los pedidos antiguos conservan CostoEmpaque y MaterialesJson vacío, sin reconstruir
ni atribuirles adicionales actuales. El cierre suma los materiales a los costos
informativos de empaque existentes una sola vez. Esta configuración no genera pagos,
egresos, reintegros personales ni deducciones adicionales de stock de insumos.

La migración `20261116135057_AgregarMaterialesPresentacionProducto` agrega la tabla
interna ProductosMateriales con cantidades (18,4), índice único por producto/insumo,
FK Restrict a Insumos y Cascade desde Producto (solo configuración; el historial
permanece en el pedido). Agrega MaterialesJson como jsonb nullable en DetallesPedido.
ProductosMateriales tiene RLS, grants explícitos de tabla/secuencia al propietario y
revocación de permisos heredados de PUBLIC, anon, authenticated y service_role.
