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
| Seguridad: reportes | `security.reports.view` | — |
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
| RR. HH.: asistencia | `hr.attendance.manage` | `hr.attendance.manage` |
| RR. HH.: turnos y reporte | `hr.shifts.manage` | `hr.shifts.manage` |
| CRM: clientes | `crm.customers.view` | `crm.customers.manage` |
| CRM: segmentación | `crm.reports.view` | — |
| CRM: puntos | `crm.points.manage` | `crm.points.manage` |
| CRM: PQRS | `crm.pqrs.manage` | `crm.pqrs.manage` |
| CRM: campañas | `crm.campaigns.manage` | `crm.campaigns.manage` |

## Roles de la matriz

La matriz define siete roles: Administrador, Gerente corporativo, Jefe de
tienda, Cajero, Auxiliar de bodega, Coordinador de logística y Gestor de
recursos humanos. Los nombres viven en `RoleNames` y la asignación exacta se
puede consultar en `/seguridad/roles` o en `RolePermissions.cs`.

Los roles no se editan desde la interfaz: la pantalla `/seguridad/roles` es una
matriz informativa. Esto es intencional para que la autorización sea
determinista entre despliegues y no dependa de datos que alguien pueda cambiar
por error. `security.roles.manage` está declarado en el catálogo, pero no se
implementó su caso de uso; la decisión y sus condiciones para reconsiderarla
están en [pending-items.md](pending-items.md).

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
alcance de asistencia y turnos no incluye salarios ni contratos.

Las tres capas se cumplen: las páginas declaran
`[Authorize(Policy = Permissions.HrContractsManage)]`, el menú oculta el enlace
sin el permiso y `EmploymentContractService` llama
`IAccessControl.Demand(Permissions.HrContractsManage)` antes de cualquier
repositorio. La descarga del adjunto es un endpoint mínimo con la misma
política y cada descarga deja una entrada de auditoría.

## Cambios en la matriz por la asistencia y los turnos

Los dos permisos ya estaban declarados y asignados; la fase no añadió roles.
La lectura y la escritura comparten permiso porque las dos operaciones muestran
identificadores, jornada y novedades del personal:

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/rrhh/asistencia` y `/rrhh/asistencia/export.csv` | `hr.attendance.manage` | Jefe de tienda, Gestor de recursos humanos |
| `/rrhh/turnos`, `/rrhh/turnos/reporte` y `/rrhh/turnos/reporte/export.csv` | `hr.shifts.manage` | Jefe de tienda, Gestor de recursos humanos |

El Administrador no recibió ninguno de los dos permisos: la decisión es la misma
que para los contratos. Puede administrar usuarios y tiendas, pero no consultar
jornadas ni novedades laborales sin una necesidad funcional. El Jefe de tienda
ve solo su sucursal; el Gestor de recursos humanos ve todas. `HrStoreScope`
aplica esa regla dentro de los servicios y de los endpoints CSV antes de leer
cualquier repositorio, con el código `hr.store.scope` para la negación.

Las tres capas se cumplen: las páginas declaran
`[Authorize(Policy = Permissions.HrAttendanceManage)]` o
`Permissions.HrShiftsManage`, el menú comprueba `AccessControl.Has(...)` y los
servicios vuelven a exigir el permiso con `IAccessControl.Demand(...)`. Los CSV
son endpoints mínimos con la misma política de autorización que la pantalla y
usan el escritor seguro `CsvReport`.

## Cambios en la matriz por los puntos, PQRS y campañas

La fase de CRM no creó permisos nuevos: los tres ya estaban declarados y
asignados. La lectura y la escritura comparten permiso porque la información que
muestra cada pantalla es la misma que se modifica. La asignación es
deliberadamente exclusiva:

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/crm/puntos`, `/crm/puntos/export.csv` | `crm.points.manage` | Cajero |
| `/crm/pqrs`, `/crm/pqrs/{Id:int}`, `/crm/pqrs/export.csv` | `crm.pqrs.manage` | Jefe de tienda |
| `/crm/campanas`, `/crm/campanas/export.csv` | `crm.campaigns.manage` | Gerente corporativo |

El Administrador **no** recibió ninguno de los tres permisos: se separa la
operación de punto de venta, la atención de solicitudes en tienda y la gestión
corporativa de campañas del rol técnico. Es una decisión de diseño documentada
en [pending-items.md](pending-items.md). Las campañas son un recurso global: no
tienen alcance por tienda, sino permiso por rol.

Puntos y PQRS sí tienen alcance por tienda: `CrmStoreScope` concede acceso global
al Administrador y al Gerente corporativo, y limita al Cajero y al Jefe de tienda
a su propia sucursal. El código de negación es `crm.store.scope`. Como el
Administrador no posee los permisos de estas pantallas, en la práctica el alcance
global lo aprovecha solo el Gerente corporativo.

Las tres capas se cumplen: las páginas declaran
`[Authorize(Policy = Permissions.CrmPointsManage)]`, `CrmPqrsManage` o
`CrmCampaignsManage`, el menú comprueba `AccessControl.Has(...)` y los servicios
vuelven a exigir el permiso con `IAccessControl.Demand(...)` antes de tocar un
repositorio. Los CSV son endpoints mínimos con la misma política y usan el
escritor seguro `CsvReport`.

## Cambios en la matriz por los reportes de seguridad

La fase 9 no creó permisos: `security.reports.view` ya estaba declarado y
asignado al Administrador y al Gerente corporativo. La pantalla
`/seguridad/reportes` y el CSV `/seguridad/reportes/export.csv` comparten esa
política, y `SecurityReportService` la vuelve a exigir con
`IAccessControl.Demand(...)` antes de consultar `audit_log`. El menú solo muestra
el enlace cuando `AccessControl.Has(...)` devuelve verdadero.

| Ruta o acción | Permiso | Roles |
| --- | --- | --- |
| `/seguridad/reportes` y `/seguridad/reportes/export.csv` | `security.reports.view` | Administrador, Gerente corporativo |

A diferencia de inventario, RR. HH. y CRM, este reporte no tiene alcance por
tienda: resume la auditoría de toda la organización. Por eso el Gerente
corporativo puede verlo, mientras que el Jefe de tienda y el Cajero no. La
consulta se limita a 5000 eventos, como se explica en
[pending-items.md](pending-items.md).

## Alcance por tienda

Los permisos deciden **qué** puede hacer un rol; el alcance decide **sobre qué
tienda**. `StoreScope` aplica la segunda regla dentro de los servicios de
conteos, traslados y devoluciones, `HrStoreScope` dentro de asistencia y turnos,
y `CrmStoreScope` dentro de puntos y PQRS, antes de consultar cualquier
repositorio.

| Rol | Alcance |
| --- | --- |
| Administrador | Todas las tiendas |
| Gerente corporativo | Todas las tiendas |
| Jefe de tienda | Solo su tienda (también en PQRS) |
| Cajero | Solo su tienda (puntos) |
| Auxiliar de bodega | Solo su tienda |
| Gestor de recursos humanos | Todas las tiendas en asistencia y turnos |

Los roles con alcance limitado no pueden crear, listar, enviar, recibir, anular,
autorizar ni rechazar documentos de otra tienda, aunque llamen al servicio
directamente. Si el usuario no tiene tienda asignada, la operación se deniega.
El Administrador y el Gerente corporativo también deben tener el permiso del
módulo: el alcance global no concede permisos por sí solo. Las campañas no
aparecen en esta tabla porque son globales y se gobiernan solo por permiso.

## Comprobaciones automáticas

- Las pruebas unitarias de cada servicio verifican que sin permiso se lanza
  `AccessDeniedException` y no se consultan los repositorios.
- `FinalScreenAuthorizationTests` lee los archivos `.razor` y comprueba que cada
  ruta nueva declara la política esperada.
- Las mismas pruebas revisan que el menú asocia cada enlace nuevo al permiso
  correcto y que los dos endpoints CSV declaran la política de su pantalla.
- `WebSmokeTests.WorkforceCsvDownloadsRequireTheirPermission` comprueba con el
  pipeline HTTP real que el Gestor de recursos humanos descarga ambos CSV y que
  un Cajero recibe la redirección a acceso denegado sin el archivo.
- `WebSmokeTests.CrmCsvDownloadsRequireTheirPermission` hace lo mismo para los
  tres CSV de CRM: el Cajero, el Jefe de tienda y el Gerente corporativo descargan
  el suyo, y un rol sin permiso recibe la redirección a acceso denegado sin el
  archivo. Usa un host propio para no agotar el límite de intentos de inicio de
  sesión que comparten las demás pruebas HTTP.
