# Permisos y roles

La fuente de verdad es código, no base de datos:

- `src/TiendaFlow.Application/Security/Permissions.cs`: catálogo de permisos.
- `src/TiendaFlow.Application/Security/RolePermissions.cs`: matriz rol → permisos.
- `src/TiendaFlow.Web/Components/Pages/Seguridad/Roles.razor`: vista de solo
  lectura de la matriz en `/seguridad/roles`.

Cada página declara `@attribute [Authorize(Policy = Permissions.X)]`, el menú
oculta los enlaces que el usuario no puede abrir y el servicio de aplicación
vuelve a llamar `IAccessControl.Demand(...)`. Así una función queda protegida en
las tres capas y no depende de que el enlace esté oculto.

## Módulos y permisos

| Módulo | Lectura | Escritura / acciones |
| --- | --- | --- |
| Seguridad: usuarios | `security.users.view` | `security.users.manage` |
| Seguridad: roles | `security.roles.view` | `security.roles.manage` (declarado; la matriz es de solo lectura) |
| Seguridad: auditoría | `security.audit.view` | — |
| Seguridad: parámetros y reportes | `security.parameters.manage`, `security.reports.view` (declarados, sin pantalla) | — |
| Inventario: productos y precios | `inventory.products.view` | `inventory.products.manage`, `inventory.price.change` |
| Inventario: stock | `inventory.stock.view` | `inventory.stock.receive`, `inventory.stock.transfer`*, `inventory.stock.count`* |
| Inventario: alertas y reportes | `inventory.alerts.view`, `inventory.reports.view` | — |
| Ventas: POS | `sales.pos.use` | — |
| Ventas: facturas | `sales.invoices.view` | `sales.invoices.manage` |
| Ventas: reportes y devoluciones | `sales.reports.view` | `sales.returns.authorize`* |
| Compras: proveedores | `purchasing.suppliers.view` | `purchasing.suppliers.manage` |
| Compras: órdenes | `purchasing.orders.view` | `.create`, `.approve`, `.send`, `.receive`, `.cancel` |
| Compras: evaluación y reportes | `purchasing.reports.view` | `purchasing.evaluations.manage` |
| RR. HH.: empleados | `hr.employees.view` | `hr.employees.manage` |
| RR. HH.: nómina | `hr.payroll.manage` | `hr.payroll.manage` |
| RR. HH.: contratos, asistencia y turnos | — | `hr.contracts.manage`*, `hr.attendance.manage`*, `hr.shifts.manage`* |
| CRM: clientes | `crm.customers.view` | `crm.customers.manage` |
| CRM: segmentación | `crm.reports.view` | — |
| CRM: puntos, PQRS y campañas | — | `crm.points.manage`*, `crm.pqrs.manage`*, `crm.campaigns.manage`* |

`*` El permiso existe en la matriz y se asigna a algún rol, pero todavía no
tiene pantalla ni caso de uso. El detalle está en
[pending-items.md](pending-items.md).

## Roles de la matriz

La matriz define siete roles: Administrador, Gerente corporativo, Jefe de
tienda, Cajero, Auxiliar de bodega, Coordinador de logística y Gestor de
recursos humanos. Los nombres viven en `RoleNames` y la asignación exacta se
puede consultar en `/seguridad/roles` o en `RolePermissions.cs`.

Los roles no se editan desde la interfaz: la pantalla `/seguridad/roles` es una
matriz informativa. Esto es intencional para que la autorización sea
determinista entre despliegues y no dependa de datos que alguien pueda cambiar
por error.

## Comprobaciones automáticas

- Las pruebas unitarias de cada servicio verifican que sin permiso se lanza
  `AccessDeniedException` y no se consultan los repositorios.
- `FinalScreenAuthorizationTests` lee los archivos `.razor` y comprueba que cada
  ruta nueva declara la política esperada.
- Las mismas pruebas revisan que el menú asocia cada enlace nuevo al permiso
  correcto.
