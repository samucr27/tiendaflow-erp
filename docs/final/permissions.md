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
| Seguridad: tiendas | `security.stores.view` | `security.stores.manage` |
| Seguridad: parámetros | `security.parameters.manage` | `security.parameters.manage` |
| Seguridad: reportes | `security.reports.view` (declarado, sin pantalla) | — |
| Inventario: productos y precios | `inventory.products.view` | `inventory.products.manage`, `inventory.price.change` |
| Inventario: stock | `inventory.stock.view` | `inventory.stock.receive`, `inventory.stock.transfer`, `inventory.stock.count` |
| Inventario: alertas y reportes | `inventory.alerts.view`, `inventory.reports.view` | — |
| Ventas: POS | `sales.pos.use` | — |
| Ventas: facturas | `sales.invoices.view` | `sales.invoices.manage` |
| Ventas: reportes | `sales.reports.view` | — |
| Ventas: devoluciones | — | `sales.returns.authorize` |
| Compras: proveedores | `purchasing.suppliers.view` | `purchasing.suppliers.manage` |
| Compras: órdenes | `purchasing.orders.view` | `.create`, `.approve`, `.send`, `.receive`, `.cancel` |
| Compras: evaluación y reportes | `purchasing.reports.view` | `purchasing.evaluations.manage` |
| RR. HH.: empleados | `hr.employees.view` | `hr.employees.manage` |
| RR. HH.: nómina | `hr.payroll.manage` | `hr.payroll.manage` |
| RR. HH.: contratos | — | `hr.contracts.manage` |
| RR. HH.: asistencia y turnos | — | `hr.attendance.manage`*, `hr.shifts.manage`* |
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

## Cambios en la matriz por la administración de tiendas

La fase de tiendas añadió dos permisos y conservó el resto de asignaciones:

| Permiso | Roles | Justificación |
| --- | --- | --- |
| `security.stores.view` | Administrador, Gerente corporativo | El gerente corporativo supervisa la red sin modificarla; el administrador necesita verla para gestionarla. |
| `security.stores.manage` | Administrador | Crear, renombrar, activar, desactivar y reasignar personal altera la operación de una sucursal; queda reservado al rol administrativo. |

La pantalla `/seguridad/tiendas` es de solo lectura para el Gerente corporativo:
el botón de creación, el formulario de edición, la activación/desactivación y la
reasignación de usuarios o empleados solo aparecen con
`security.stores.manage`, y `StoreService` vuelve a exigirlo antes de tocar
cualquier repositorio.

## Cambios en la matriz por los parámetros

La administración de parámetros no cambió la matriz: el permiso
`security.parameters.manage` ya existía y solo lo tiene el Administrador. La
pantalla `/seguridad/parametros` y su historial exigen ese permiso en la página
y en `ParameterService`, porque un valor mal configurado afecta a todas las
tiendas. Los demás roles consultan el valor efectivo a través de
`IParameterValues` sin poder modificarlo.

## Cambios en la matriz por los KPIs y la reposición

Las compras con KPIs y reposición tampoco cambiaron la matriz; reutilizan
permisos existentes:

| Ruta o acción | Permiso | Justificación |
| --- | --- | --- |
| `/compras/reposicion` y generación de borradores | `purchasing.orders.create` | Crear borradores es una acción de compra; el borrador no se envía solo. |
| Ver o editar la relación producto · proveedor | `purchasing.suppliers.view` / `purchasing.suppliers.manage` | El último costo y el proveedor predeterminado son datos maestros de proveedores. |
| Ranking, KPIs y exportación CSV | `purchasing.reports.view` | Es un reporte de compras, no una acción transaccional. |
| Fecha prometida y recepción con fecha | `purchasing.orders.create` / `purchasing.orders.receive` | Siguen el mismo permiso que la creación y la recepción de la orden. |

El Coordinador de logística conserva `purchasing.orders.create` y
`purchasing.suppliers.manage`, así que puede mantener la relación producto ·
proveedor y generar borradores. El Jefe de tienda conserva la aprobación y la
recepción, y el Administrador mantiene todas las acciones. Ningún rol nuevo
recibió permisos de administración de proveedores.

## Cambios en la matriz por los conteos y los traslados

Los conteos físicos y los traslados no cambiaron la matriz: usan permisos que ya
existían y siguen asignados a los mismos roles.

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/inventario/conteos` y `/inventario/conteos/{id}` | `inventory.stock.count` | Administrador, Jefe de tienda, Auxiliar de bodega |
| `/inventario/traslados` y `/inventario/traslados/{id}` | `inventory.stock.transfer` | Administrador, Jefe de tienda, Auxiliar de bodega |

El Coordinador de logística conserva la recepción de mercancía
(`inventory.stock.receive`) pero no crea conteos ni traslados: su papel es
abastecer, no ajustar el inventario físico de una tienda. El Cajero solo consulta
el stock y no puede abrir ninguno de los dos documentos. Cada pantalla declara su
política, el menú oculta el enlace y `InventoryCountService` /
`InventoryTransferService` vuelven a exigir el permiso antes de leer o escribir.

## Cambios en la matriz por las devoluciones

Las devoluciones y notas crédito siguen usando un único permiso. En la revisión
de alcance se amplió a los roles corporativos que ya administran ventas, para
que puedan resolver una devolución de cualquier tienda:

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/ventas/devoluciones`, `/ventas/devoluciones/{id}`, autorizar y rechazar | `sales.returns.authorize` | Administrador, Gerente corporativo, Jefe de tienda |

El Cajero registra ventas, pero no autoriza devoluciones: la nota crédito afecta
el ingreso y repone inventario, así que la decisión queda en el responsable de la
tienda o en un rol corporativo. La pantalla declara la política, el menú oculta el
enlace y `SalesReturnService` vuelve a exigir el permiso antes de leer o
escribir.

## Cambios en la matriz por los contratos

Los contratos laborales usan `hr.contracts.manage` y no crearon permisos
nuevos. La lectura también exige ese permiso porque la pantalla muestra salario
base y documentos firmados, que son datos personales y salariales:

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/rrhh/contratos`, `/rrhh/contratos/nuevo`, `/rrhh/contratos/{id}` | `hr.contracts.manage` | Gestor de recursos humanos |
| Descargar un adjunto (`/rrhh/contratos/{id}/adjunto/{attachmentId}`) | `hr.contracts.manage` | Gestor de recursos humanos |

El permiso queda concentrado en el Gestor de recursos humanos. El Administrador
conserva la vista de empleados (`hr.employees.view`) pero no administra
contratos: la decisión es intencional para separar la operación de RR. HH. del
rol técnico y se documenta como decisión de diseño en
[pending-items.md](pending-items.md). El Jefe de tienda tampoco lo tiene: su
alcance de asistencia y turnos llegará en la siguiente fase y no incluye
salarios ni contratos.

Las tres capas se cumplen: las páginas declaran
`[Authorize(Policy = Permissions.HrContractsManage)]`, el menú oculta el enlace
sin el permiso y `EmploymentContractService` llama
`IAccessControl.Demand(Permissions.HrContractsManage)` antes de cualquier
repositorio. La descarga del adjunto es un endpoint mínimo con la misma
política y cada descarga deja una entrada de auditoría.

## Alcance por tienda

Los permisos deciden **qué** puede hacer un rol; el alcance decide **sobre qué
tienda**. `StoreScope` aplica la segunda regla dentro de los servicios de
conteos, traslados y devoluciones, antes de consultar cualquier repositorio.

| Rol | Alcance |
| --- | --- |
| Administrador | Todas las tiendas |
| Gerente corporativo | Todas las tiendas |
| Jefe de tienda | Solo su tienda |
| Auxiliar de bodega | Solo su tienda |

Los roles con alcance limitado no pueden crear, listar, enviar, recibir, anular,
autorizar ni rechazar documentos de otra tienda, aunque llamen al servicio
directamente. Si el usuario no tiene tienda asignada, la operación se deniega.
El Administrador y el Gerente corporativo también deben tener el permiso del
módulo: el alcance global no concede permisos por sí solo.

## Comprobaciones automáticas

- Las pruebas unitarias de cada servicio verifican que sin permiso se lanza
  `AccessDeniedException` y no se consultan los repositorios.
- `FinalScreenAuthorizationTests` lee los archivos `.razor` y comprueba que cada
  ruta nueva declara la política esperada.
- Las mismas pruebas revisan que el menú asocia cada enlace nuevo al permiso
  correcto.
