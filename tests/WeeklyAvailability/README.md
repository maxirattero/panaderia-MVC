# Disponibilidad semanal de panes

Desde la raíz del proyecto, con PostgreSQL local disponible en `127.0.0.1:55449`
(usuario `postgres`, autenticación de pruebas sin contraseña):

```powershell
dotnet run --project tests/WeeklyAvailability
```

Se puede cambiar el puerto mediante `WEEKLY_TEST_PORT`. Las pruebas crean una base
con nombre aleatorio `weekly_checks_*` y la eliminan al finalizar. Nunca cargan
la configuración ni las credenciales de producción.

Cubren los límites horarios en Argentina, productos sin bandera, stock, carritos
anteriores al cierre, altas individuales y múltiples, cambios de cantidad,
confirmación, ampliación de pedidos, rollback, migración y edición/duplicación
de la bandera. La migración activa únicamente categorías `Pan`/`Panes`, ignorando
mayúsculas y espacios extremos; los productos nuevos tienen la bandera apagada
por defecto y se configuran desde Crear/Editar producto.

Para revisar las vistas Razor reales con productos ficticios y reloj controlado,
sin base de datos ni envíos externos:

```powershell
dotnet run --project tests/WeeklyAvailability -- --preview
```

Abrir `http://127.0.0.1:5079/preview`. Elegir jueves, agregar pan y pizza, cambiar
a viernes y abrir el carrito: debe conservar ambos productos, bloquear Continuar
y permitir quitar el pan. Al quitarlo debe habilitarse Continuar. Elegir sábado
a las 12:00 debe volver a habilitar la compra de pan. Esta vista previa pertenece
solo al proyecto de pruebas; no agrega rutas ni opciones de reloj a producción.
