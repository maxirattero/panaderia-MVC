# Masa Viva (Panadería MVC)

App de gestión de panadería que reemplaza un flujo de 2 años en Google Sheets. Desarrollador único: Maxi (también operador de la panadería). No hay equipo — priorizar pragmatismo y confirmar decisiones de trade-off antes de generar código.

## Stack

- ASP.NET Core MVC, **.NET 10**
- Entity Framework Core 10 + Npgsql 10 (PostgreSQL)
- PostgreSQL hosteado en **Supabase** — **Session pooler, puerto 5432**. NUNCA usar el Transaction pooler (puerto 6543): causa timeouts en migraciones de EF Core.
- ASP.NET Core Identity (`AddDefaultIdentity` + `AddRoles<IdentityRole>`), roles **Admin** / **Revendedor**
- Data Protection persistido en la DB (`PersistKeysToDbContext<PanaderiaContext>`), `SetApplicationName("MasaViva")`
- Bootstrap 5 + Material Symbols icons. Design system propio "Masa Viva"
- Deploy en **Railway** vía Dockerfile multi-stage (build con `dotnet/sdk:10.0`, runtime con `dotnet/aspnet:10.0`); Railway inyecta `PORT` en runtime
- `UseRequestLocalization` fijado a `CultureInfo.InvariantCulture` (evita errores de parseo decimal bajo locale es-AR del SO)

## Estructura de la solución

Tres proyectos (`panaderia-MVC.slnx`):

- **`Panaderia.Models`** — entidades EF Core (`Entities/`), DbContext y migraciones (`Data/`, `Migrations/`), DTOs como `record` (`DTOs/`), enums (`Enums/`)
- **`Panaderia.Services`** — lógica de negocio e interfaces (`Interfaces/` / `Implementations/`), un servicio por módulo (ej. `IPedidoService`/`PedidoService`), registrados como `Scoped` en `Program.cs`
- **`Panaderia.MVC`** — Controllers, Views (Razor), ViewModels en `Models/` (no confundir con `Panaderia.Models`), `wwwroot/`

Referencias: `Panaderia.MVC` → `Panaderia.Services` → (implícito) `Panaderia.Models`.

## Módulos

**Completos y deployados:** Productos, Configuración, Clientes, Proveedores, Pedidos, Insumos, Compras, Recetas, SubRecetas, Producción (dashboard + Planificador de Amasadas), ReporteCaja (con cierre semanal), Identity (roles Admin/Revendedor).

**Pendientes:** Panel de gestión de usuarios (admin), Historial de Pedidos (entregados), Storefront para revendedores, análisis de consumo/ML.

## Convenciones y principios clave (seguir siempre)

- **EF Core tracking en Update:** cargar la entidad trackeada con `FirstOrDefaultAsync` + `Include`, después mapear propiedades directamente. Nunca llamar `Update()` sobre una entidad detached. Para colecciones hijas: `RemoveRange` + `Clear` + reinsert.
- **Antiforgery:** formularios estáticos en el DOM → `@Html.AntiForgeryToken()` nativo dentro del `<form>`. Formularios armados dinámicamente por JS → pasar token vía `data-token` + `@inject IAntiforgery`. Si un 400 es por antiforgery, Kestrel loguea un Warning; comentar `[ValidateAntiForgeryToken]` aísla la causa.
- **Soft delete:** campo `bool Anulado`. Query filter global en EF (`!p.Anulado` en `Pedido`, y también `!d.Pedido.Anulado` en `DetallePedido` — este segundo filtro silencia el warning EF10622 y no requiere migración nueva). `AnularAsync` usa `FindAsync` para bypassear los query filters. Sin GET Delete actions; modal reutilizable `_DeleteModal.cshtml` en `Views/Shared/`, incluido desde `_Layout.cshtml`.
- **PostgreSQL/Npgsql — fechas:** siempre `DateTime.UtcNow`. Inputs de `<input type="date">` llegan como `Unspecified` → envolver con `DateTime.SpecifyKind(value, DateTimeKind.Utc)` antes de guardar.
- **PostgreSQL/Npgsql — decimales:** precios/decimales en atributos `data-*` del HTML deben usar `.ToString(CultureInfo.InvariantCulture)` (evita choque con locale es-AR en el parseo JS).
- **PostgreSQL MVCC ordering:** sin `ORDER BY` explícito, los `UPDATE` pueden alterar el orden de scan de filas. Usar siempre `OrderBy` explícito en campos de contenido. Para propiedades computadas en C# (ej. `NombreVisible`), ordenar en memoria con `AsEnumerable()` después del `Include`, con comparador `CultureInfo("es-AR")` para acentos y ñ.
- **Model validation:** `[ValidateNever]` en navigation properties (ej. `Categoria`, `Formato`, `Tamano`) para evitar fallos silenciosos de ModelState en POST.
- **Precisión de datos:** `(18,4)` para campos de costo; atributos `data-*` con precisión `F10` para minimizar diferencias de redondeo float (JS) vs decimal (C#).
- **FK cascade risk:** revisar cuidadosamente `DeleteBehavior` en las relaciones. `DetallePedido → Producto` es `Restrict` (no `Cascade`) para no borrar silenciosamente historial de pedidos — al tocar FKs nuevas, replicar ese criterio.
- **Single responsibility en mutaciones de datos:** un solo camino de deducción de stock, sin duplicar lógica entre services. Mantener el `SaveChanges` dentro de una sola unit of work cuando sea posible.
- **Diseño pragmático:** preferir reutilizar infraestructura existente antes que crear tablas nuevas (ej. `Insumo` reutilizado para packaging vía enum `TipoInsumo`). Si una tarea implica una decisión de trade-off no trivial, discutirla y confirmarla antes de implementar.

## Convenciones de diseño (CSS/UI)

Paleta cálida definida en `wwwroot/css/site.css` (`--mv-*` custom properties): `--mv-background: #e7ddd6`, `--mv-primary-dark: #855330`, `--mv-tertiary: #802b0d`, `--mv-surface`/`--mv-surface-variant` para cards, `--mv-success`/`--mv-error` para estados.

Clases clave: `btn-mv-primary`, `btn-mv-secondary` (variante outline), `btn-mv-tertiary`, `card-custom`, `status-badge` (+ `status-active`/`status-inactive`), `.titulo-pagina`, `.pill-filtro` / `.pill-filtro.activo`.

Layout: sidebar (`_SidebarNav.cshtml`) + top app bar (`_TopAppBar.cshtml`) dentro de `_Layout.cshtml`. `container-fluid p-4 p-md-5` es el wrapper único definido en `_Layout.cshtml` — las vistas individuales no deben agregar su propio wrapper. Forms de Create/Edit: `max-width: 1060px`.

## Convenciones de dropdowns

- `ViewBag.Categorias` → `SelectList("Id","Nombre")`
- `ViewBag.Formatos` / `ViewBag.Tamanos` → `SelectList("Id","Descripcion")`
- `ViewBag.Clientes` / `ViewBag.Productos` → `IEnumerable` raw (las views arman el `<select>` manualmente con atributos `data-*`)
- `CargarDropdowns()` privado en el controller, llamado en los 4 caminos del form (GET/POST Create, GET/POST Edit)

## Convenciones generales

- `FechaCreacion = DateTime.UtcNow` se setea en el POST Create del controller (no dentro del service). `FechaModificacion` se actualiza dentro de `UpdateAsync`.
- Decimal/costo: `(18,4)` en el modelo de datos.
