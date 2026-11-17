# 10 — Conceptos de arquitectura: seguridad y roles

## 10.1 Conceptos

- **Autenticación:** autenticación por cookie de ASP.NET Core (`TiendaFlow.Auth`, HttpOnly, SameSite Lax, expiración deslizante de 8 horas). Las contraseñas se guardan solo como hash detrás de `IPasswordHasher`; nunca en texto plano.
- **Roles y permisos:** cada usuario tiene exactamente uno de 7 roles. Cada función del ERP tiene un código de permiso (`Permissions`) y la matriz rol-permiso por defecto vive en código (`RolePermissions`). El administrador puede reemplazar los permisos de un rol desde la interfaz; esos cambios se guardan en `role_permission` y se aplican en el siguiente inicio de sesión.
- **Autorización en tres niveles (defensa en profundidad):**
  1. **Página o endpoint:** una política de autorización por permiso, aplicada con `[Authorize(Policy=...)]` o `RequireAuthorization(permission)`.
  2. **Interfaz:** las entradas de menú y los botones se muestran solo cuando el usuario tiene el permiso. Es comodidad, nunca el control real.
  3. **Servicio:** `IAccessControl.Demand(permission)` lanza `AccessDeniedException` dentro de los servicios de aplicación.
- **Auditoría:** el registro de auditoría guarda el id real del usuario, el módulo, la acción y la IP. Los accesos denegados se registran tanto en la capa de página como en la de servicio. Las escrituras de RR. HH. usan el módulo `hr` y registran solo ids, conteos y acciones; los nombres, documentos y montos de los empleados nunca aparecen en el detalle.
- **Datos personales (Ley 1581 de 2012):** se recolecta el mínimo de datos, no se registran datos personales y los datos de nómina quedan restringidos al Oficial de RR. HH. En el listado de empleados, el salario solo se devuelve cuando quien consulta tiene `hr.employees.manage`; un usuario con solo `hr.employees.view` recibe `null` desde `EmployeeService` y la página muestra "—". Es un control de protección de datos, no una regla visual, porque el valor nunca sale del servicio.
- **Protección del acceso:** `POST /login` limita los intentos por IP (`Security:LoginAttemptsPerMinute`, 10 por minuto) y responde con un mensaje en la pantalla de acceso. `UserService` impide quitar el rol o desactivar al último administrador activo, para que el sistema nunca quede sin quien gestione usuarios.
- **Separación de funciones:** el Administrador gestiona usuarios, roles y parámetros y puede leer la mayoría de los módulos, pero no tiene permisos operativos (ni punto de venta, ni órdenes de compra, ni nómina).

## 10.2 Matriz de roles y permisos

Roles: **ADM** Administrator, **CM** Corporate Manager, **SM** Store Manager, **CSH** Cashier, **SC** Stock Clerk, **LC** Logistics Coordinator, **HR** HR Officer.

La tabla se genera a partir de `Permissions` y `RolePermissions` de la versión final: 45 permisos. Permisos por rol: ADM 23, CM 17, SM 25, CSH 7, SC 6, LC 13, HR 6.

| Permiso | ADM | CM | SM | CSH | SC | LC | HR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| security.users.view | ✓ |   |   |   |   |   |   |
| security.users.manage | ✓ |   |   |   |   |   |   |
| security.roles.view | ✓ |   |   |   |   |   |   |
| security.roles.manage | ✓ |   |   |   |   |   |   |
| security.audit.view | ✓ |   |   |   |   |   |   |
| security.parameters.manage | ✓ |   |   |   |   |   |   |
| security.reports.view | ✓ | ✓ |   |   |   |   |   |
| security.stores.view | ✓ | ✓ |   |   |   |   |   |
| security.stores.manage | ✓ |   |   |   |   |   |   |
| inventory.products.view | ✓ | ✓ | ✓ |   | ✓ | ✓ |   |
| inventory.products.manage |   |   | ✓ |   |   |   |   |
| inventory.price.change |   |   | ✓ |   |   |   |   |
| inventory.stock.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |   |
| inventory.stock.receive |   |   | ✓ |   | ✓ | ✓ |   |
| inventory.stock.transfer |   |   | ✓ |   | ✓ |   |   |
| inventory.stock.count |   |   | ✓ |   | ✓ |   |   |
| inventory.alerts.view | ✓ | ✓ | ✓ |   | ✓ | ✓ |   |
| inventory.reports.view | ✓ | ✓ | ✓ |   |   |   |   |
| sales.pos.use |   |   | ✓ | ✓ |   |   |   |
| sales.returns.authorize |   |   | ✓ |   |   |   |   |
| sales.reports.view | ✓ | ✓ | ✓ |   |   |   |   |
| sales.invoices.view | ✓ | ✓ | ✓ | ✓ |   |   |   |
| sales.invoices.manage | ✓ | ✓ | ✓ | ✓ |   |   |   |
| purchasing.suppliers.view | ✓ | ✓ | ✓ |   |   | ✓ |   |
| purchasing.suppliers.manage |   |   |   |   |   | ✓ |   |
| purchasing.orders.view | ✓ | ✓ | ✓ |   |   | ✓ |   |
| purchasing.orders.create |   |   |   |   |   | ✓ |   |
| purchasing.orders.approve |   |   | ✓ |   |   |   |   |
| purchasing.orders.send |   |   |   |   |   | ✓ |   |
| purchasing.orders.receive |   |   | ✓ |   |   | ✓ |   |
| purchasing.orders.cancel |   |   |   |   |   | ✓ |   |
| purchasing.evaluations.manage |   |   |   |   |   | ✓ |   |
| purchasing.reports.view | ✓ | ✓ | ✓ |   |   | ✓ |   |
| hr.employees.view | ✓ | ✓ | ✓ |   |   |   | ✓ |
| hr.employees.manage |   |   |   |   |   |   | ✓ |
| hr.payroll.manage |   |   |   |   |   |   | ✓ |
| hr.contracts.manage |   |   |   |   |   |   | ✓ |
| hr.attendance.manage |   |   | ✓ |   |   |   | ✓ |
| hr.shifts.manage |   |   | ✓ |   |   |   | ✓ |
| crm.customers.view | ✓ | ✓ | ✓ | ✓ |   |   |   |
| crm.customers.manage | ✓ | ✓ | ✓ | ✓ |   |   |   |
| crm.points.manage |   |   |   | ✓ |   |   |   |
| crm.pqrs.manage |   |   | ✓ |   |   |   |   |
| crm.campaigns.manage |   | ✓ |   |   |   |   |   |
| crm.reports.view | ✓ | ✓ |   |   |   |   |   |

## 10.3 Aplicación de los permisos en las tres capas

| Pantalla o endpoint | Capa 1: página o endpoint | Capa 2: interfaz | Capa 3: servicio |
|---|---|---|---|
| `/rrhh/empleados` | `hr.employees.view` | Alta y nómina ocultas sin `hr.employees.manage` o `hr.payroll.manage` | `EmployeeService` exige vista; el salario solo se expone con `hr.employees.manage` |
| `/rrhh/empleados/nuevo` y `/{id}` | `hr.employees.manage` | Formulario completo solo para quien administra | `Create` y `Update` exigen `hr.employees.manage` |
| `/rrhh/nomina` | `hr.payroll.manage` | Página completa solo con el permiso | `PayrollRunService.Calculate` exige `hr.payroll.manage` |
| `POST /api/nomina/calculate` | `hr.payroll.manage` | No aplica: es un endpoint | La autorización se exige en el borde; el motor no tiene permisos propios |
| `/seguridad/usuarios` | `security.users.view` | Alta y edición ocultas sin `security.users.manage` | `UserService` exige `security.users.manage` en cada escritura |
| `/seguridad/roles` | `security.roles.view` | Edición por rol solo con `security.roles.manage`; el resto la ve en solo lectura | `RoleCatalogService.GetMatrix` exige `security.roles.view` |
| `/seguridad/auditoria` | `security.audit.view` | Sin acciones de escritura | `AuditTrailService.Recent` exige `security.audit.view` |
| `/inventario/alertas` | `inventory.alerts.view` | Acciones según `inventory.stock.receive` | `InventoryDashboardService.Get` exige `inventory.stock.view` |
| `/proveedores` | `purchasing.suppliers.view` | Alta, edición y desactivación ocultas sin `purchasing.suppliers.manage` | `SupplierService.List` exige vista; `Create`, `Update` y el cambio de estado exigen `purchasing.suppliers.manage` |
| `/proveedores/nuevo` y `/{id}` | `purchasing.suppliers.manage` | Formulario completo solo para quien administra | `SupplierService.Create` y `Update` exigen `purchasing.suppliers.manage` |
| `/facturacion` | `sales.invoices.view` | Nueva factura y anulación ocultas sin `sales.invoices.manage` | `SalesInvoiceService.List` exige vista; `Create` y `Cancel` exigen `sales.invoices.manage` |
| `/facturacion/{id}` | `sales.invoices.view` | Detalle de solo lectura: una factura emitida no se edita | `SalesInvoiceService.Get` exige `sales.invoices.view` |
| `/facturacion/nueva` | `sales.invoices.manage` | Formulario solo para quien administra | `SalesInvoiceService.Create` exige `sales.invoices.manage` |
| `/inventario/ventas` | `sales.pos.use` | Entrada **Registrar venta** solo para SM y CSH | `PosSaleService.Register` exige `sales.pos.use` |
| `/inventario/conteos` y `/{id}` | `inventory.stock.count` | Acciones de cierre según el estado del conteo | `InventoryCountService` exige `inventory.stock.count` |
| `/inventario/traslados` y `/{id}` | `inventory.stock.transfer` | Acciones de envío y recepción según el estado | `InventoryTransferService` exige `inventory.stock.transfer` |
| `/compras/ordenes` y `/{id}` | `purchasing.orders.view` | Aprobar, enviar, recibir y cancelar según `purchasing.orders.*` | `PurchaseOrderService` exige el permiso de cada transición |
| `/compras/ordenes/nueva` y `/compras/reposicion` | `purchasing.orders.create` | Generación de borradores solo para LC | `PurchaseOrderService.Create` y `ReplenishmentService` exigen `purchasing.orders.create` |
| `/proveedores/ranking` | `purchasing.reports.view` | Exportación CSV con el mismo permiso | La consulta del ranking exige `purchasing.reports.view` |
| `/crm/segmentacion` | `crm.reports.view` | Solo lectura | `CustomerSegmentationService` exige `crm.reports.view` |
| `/reportes/inventario`, `/reportes/ventas`, `/reportes/nomina` | `inventory.reports.view`, `sales.reports.view`, `hr.payroll.manage` | Exportación CSV con el mismo permiso | Cada servicio de reporte exige su permiso |
| `/seguridad/tiendas` y `/{id}` | `security.stores.view` | Alta y desactivación ocultas sin `security.stores.manage` | `StoreService` exige `security.stores.manage` en cada escritura |
| `/seguridad/parametros` y `/{clave}` | `security.parameters.manage` | Solo ADM | `ParameterService` exige `security.parameters.manage` |
| `/reportes/seguridad` | `security.reports.view` | Exportación CSV con el mismo permiso | `SecurityReportService` exige `security.reports.view` |
| `/rrhh/contratos`, `/rrhh/asistencia`, `/rrhh/turnos` | `hr.contracts.manage`, `hr.attendance.manage`, `hr.shifts.manage` | Entradas de menú según el permiso | `HumanResourcesRecordsService` exige el permiso de cada operación |
| `/crm/puntos`, `/crm/pqrs`, `/crm/campanas` | `crm.points.manage`, `crm.pqrs.manage`, `crm.campaigns.manage` | Entradas de menú según el permiso | `LoyaltyService`, `PqrsService` y `CampaignService` exigen su permiso |
| Devoluciones en `/facturacion/{id}` | `sales.invoices.view` | Formulario visible solo con `sales.returns.authorize` | `SalesReturnService.Register` exige `sales.returns.authorize` |
| `/api/*` | El permiso del endpoint | No aplica | Sin sesión responde `401` y sin permiso `403`, en lugar de redirigir |
| `/crm/clientes` | `crm.customers.view` | Alta, edición y desactivación ocultas sin `crm.customers.manage` | `CustomerService.List` exige vista; `Create` y `Update` exigen `crm.customers.manage` |
| `/crm/clientes/nuevo` y `/{id}` | `crm.customers.manage` | Formulario completo solo para quien administra | `CustomerService.Create` y `Update` exigen `crm.customers.manage` |

## 10.4 Asignación de roles (HU-06)

El administrador asigna un rol desde `/seguridad/usuarios`. En `/seguridad/roles` puede además editar los permisos de cada rol: los cambios se guardan en la tabla `role_permission`, reemplazan la matriz por defecto de ese rol y se auditan; el rol Administrador conserva siempre `security.users.*` y `security.roles.*`, y "Restablecer por defecto" vuelve a la matriz del código. En el siguiente inicio de sesión, los permisos del usuario se construyen a partir de la matriz, de modo que solo alcanza las páginas, entradas de menú y servicios permitidos.

## 10.5 Auditoría y datos personales

| Operación | Contenido auditado | ¿Datos personales? |
|---|---|---|
| Crear o actualizar empleado | id del empleado, estado activo y tienda | No |
| Corrida de nómina | cantidad de empleados activos y días trabajados | No |
| Crear, actualizar o desactivar usuario | id del usuario, rol y estado | No |
| Crear o actualizar proveedor | id del proveedor, NIT y estado | No |
| Emitir o anular factura | id de la factura, cliente, total y unidades descontadas o devueltas | Sí, nombre del cliente en el detalle; decisión pendiente |
| Venta del punto de venta | id de la venta, código, productos, unidades y total | No |
| Devolución (nota crédito) | id de la devolución, factura, unidades y valor | No |
| Contratos, asistencia, turnos y corrida guardada | id del registro, tipo, fecha y periodo | No |
| Canje de puntos, PQRS y campañas | id del registro, puntos, radicado o segmento | No |
| Cambio de permisos de un rol | id del rol y permisos agregados y quitados | No |
| Borradores de reposición | id de la orden, proveedor y total | No |
| Crear o actualizar cliente CRM | id del cliente y estado | No; el detalle usa el nombre, no el documento |
| Inicio de sesión correcto | id del usuario y texto fijo | No |
| Inicio de sesión fallido | correo intentado como `EntityId` | Sí, decisión pendiente |
| Acceso denegado | id del usuario, permiso o política y ruta | No |

## 10.6 Coherencia con la Sección 5

El bloque de nómina de la Sección 5 queda protegido por esta matriz: `hr.payroll.manage` solo está marcado para **HR Officer**, y el endpoint y la pantalla de nómina exigen ese permiso. El Administrador puede leer el listado de empleados con `hr.employees.view`, pero no ve salarios ni ejecuta la nómina. Cada rechazo se audita sin nombres, documentos ni montos.

Los módulos del Sprint 2 repiten el patrón: la fila de permisos de la tabla 10.2 coincide con la política de la página, con la visibilidad del menú y con el `Demand` del servicio. Proveedores queda administrado solo por LC; Facturación y CRM clientes quedan disponibles para ADM, CM, SM y CSH; el punto de venta, para SM y CSH; la reposición y las órdenes de compra, para LC, con aprobación de SM. La prueba `WebSmokeTests` inicia sesión con los siete roles y abre cada ruta que la matriz permite, y `Sprint2ScreenAuthorizationTests` comprueba que cada página declare la política de la matriz.
