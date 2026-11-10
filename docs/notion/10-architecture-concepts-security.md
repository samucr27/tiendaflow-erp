# 10 — Conceptos de arquitectura: seguridad y roles

## 10.1 Conceptos

- **Autenticación:** autenticación por cookie de ASP.NET Core (`TiendaFlow.Auth`, HttpOnly, SameSite Lax, expiración deslizante de 8 horas). Las contraseñas se guardan solo como hash detrás de `IPasswordHasher`; nunca en texto plano.
- **Roles y permisos:** cada usuario tiene exactamente uno de 7 roles. Cada función del ERP tiene un código de permiso (`Permissions`) y la matriz rol-permiso vive en código (`RolePermissions`), de modo que la autorización es determinista entre despliegues. La base de datos solo guarda usuarios, roles y su asignación.
- **Autorización en tres niveles (defensa en profundidad):**
  1. **Página o endpoint:** una política de autorización por permiso, aplicada con `[Authorize(Policy=...)]` o `RequireAuthorization(permission)`.
  2. **Interfaz:** las entradas de menú y los botones se muestran solo cuando el usuario tiene el permiso. Es comodidad, nunca el control real.
  3. **Servicio:** `IAccessControl.Demand(permission)` lanza `AccessDeniedException` dentro de los servicios de aplicación.
- **Auditoría:** el registro de auditoría guarda el id real del usuario, el módulo, la acción y la IP. Los accesos denegados se registran tanto en la capa de página como en la de servicio. Las escrituras de RR. HH. usan el módulo `hr` y registran solo ids, conteos y acciones; los nombres, documentos y montos de los empleados nunca aparecen en el detalle.
- **Datos personales (Ley 1581 de 2012):** se recolecta el mínimo de datos, no se registran datos personales y los datos de nómina quedan restringidos al Oficial de RR. HH. En el listado de empleados, el salario solo se devuelve cuando quien consulta tiene `hr.employees.manage`; un usuario con solo `hr.employees.view` recibe `null` desde `EmployeeService` y la página muestra "—". Es un control de protección de datos, no una regla visual, porque el valor nunca sale del servicio.
- **Separación de funciones:** el Administrador gestiona usuarios, roles y parámetros y puede leer la mayoría de los módulos, pero no tiene permisos operativos (ni punto de venta, ni órdenes de compra, ni nómina).

## 10.2 Matriz de roles y permisos

Roles: **ADM** Administrator, **CM** Corporate Manager, **SM** Store Manager, **CSH** Cashier, **SC** Stock Clerk, **LC** Logistics Coordinator, **HR** HR Officer.

| Permiso | ADM | CM | SM | CSH | SC | LC | HR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| security.users.view | ✓ | | | | | | |
| security.users.manage | ✓ | | | | | | |
| security.roles.view | ✓ | | | | | | |
| security.roles.manage | ✓ | | | | | | |
| security.audit.view | ✓ | | | | | | |
| security.parameters.manage | ✓ | | | | | | |
| security.reports.view | ✓ | ✓ | | | | | |
| inventory.products.view | ✓ | ✓ | ✓ | | ✓ | ✓ | |
| inventory.products.manage | | | ✓ | | | | |
| inventory.price.change | | | ✓ | | | | |
| inventory.stock.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | |
| inventory.stock.receive | | | ✓ | | ✓ | ✓ | |
| inventory.stock.transfer | | | ✓ | | ✓ | | |
| inventory.stock.count | | | ✓ | | ✓ | | |
| inventory.alerts.view | ✓ | ✓ | ✓ | | ✓ | ✓ | |
| inventory.reports.view | ✓ | ✓ | ✓ | | | | |
| sales.pos.use | | | ✓ | ✓ | | | |
| sales.returns.authorize | | | ✓ | | | | |
| sales.reports.view | ✓ | ✓ | ✓ | | | | |
| purchasing.suppliers.view | ✓ | ✓ | ✓ | | | ✓ | |
| purchasing.suppliers.manage | | | | | | ✓ | |
| purchasing.orders.view | ✓ | ✓ | ✓ | | | ✓ | |
| purchasing.orders.create | | | | | | ✓ | |
| purchasing.orders.approve | | | ✓ | | | | |
| purchasing.orders.send | | | | | | ✓ | |
| purchasing.orders.receive | | | ✓ | | | ✓ | |
| purchasing.orders.cancel | | | | | | ✓ | |
| purchasing.evaluations.manage | | | | | | ✓ | |
| purchasing.reports.view | ✓ | ✓ | ✓ | | | ✓ | |
| hr.employees.view | ✓ | ✓ | ✓ | | | | ✓ |
| hr.employees.manage | | | | | | | ✓ |
| hr.payroll.manage | | | | | | | ✓ |
| hr.contracts.manage | | | | | | | ✓ |
| hr.attendance.manage | | | ✓ | | | | ✓ |
| hr.shifts.manage | | | ✓ | | | | ✓ |
| crm.customers.view | ✓ | ✓ | ✓ | ✓ | | | |
| crm.customers.manage | | | | ✓ | | | |
| crm.points.manage | | | | ✓ | | | |
| crm.pqrs.manage | | | ✓ | | | | |
| crm.campaigns.manage | | ✓ | | | | | |
| crm.reports.view | ✓ | ✓ | | | | | |

## 10.3 Aplicación de los permisos en las tres capas

| Pantalla o endpoint | Capa 1: página o endpoint | Capa 2: interfaz | Capa 3: servicio |
|---|---|---|---|
| `/rrhh/empleados` | `hr.employees.view` | Alta y nómina ocultas sin `hr.employees.manage` o `hr.payroll.manage` | `EmployeeService` exige vista; el salario solo se expone con `hr.employees.manage` |
| `/rrhh/empleados/nuevo` y `/{id}` | `hr.employees.manage` | Formulario completo solo para quien administra | `Create` y `Update` exigen `hr.employees.manage` |
| `/rrhh/nomina` | `hr.payroll.manage` | Página completa solo con el permiso | `PayrollRunService.Calculate` exige `hr.payroll.manage` |
| `POST /api/nomina/calculate` | `hr.payroll.manage` | No aplica: es un endpoint | La autorización se exige en el borde; el motor no tiene permisos propios |
| `/seguridad/usuarios` | `security.users.view` | Alta y edición ocultas sin `security.users.manage` | `UserService` exige `security.users.manage` en cada escritura |
| `/seguridad/roles` | `security.roles.view` | Matriz de solo lectura | `RoleCatalogService.GetMatrix` exige `security.roles.view` |
| `/seguridad/auditoria` | `security.audit.view` | Sin acciones de escritura | `AuditTrailService.Recent` exige `security.audit.view` |
| `/inventario/alertas` | `inventory.alerts.view` | Acciones según `inventory.stock.receive` | `InventoryDashboardService.Get` exige `inventory.stock.view` |

## 10.4 Asignación de roles (HU-06)

El administrador asigna un rol desde `/seguridad/usuarios`. La matriz se muestra en solo lectura en `/seguridad/roles`. En el siguiente inicio de sesión, los permisos del usuario se construyen a partir de la matriz, de modo que solo alcanza las páginas, entradas de menú y servicios permitidos.

## 10.5 Auditoría y datos personales

| Operación | Contenido auditado | ¿Datos personales? |
|---|---|---|
| Crear o actualizar empleado | id del empleado, estado activo y tienda | No |
| Corrida de nómina | cantidad de empleados activos y días trabajados | No |
| Crear, actualizar o desactivar usuario | id del usuario, rol y estado | No |
| Inicio de sesión correcto | id del usuario y texto fijo | No |
| Inicio de sesión fallido | correo intentado como `EntityId` | Sí, decisión pendiente |
| Acceso denegado | id del usuario, permiso o política y ruta | No |

## 10.6 Coherencia con la Sección 5

El bloque de nómina de la Sección 5 queda protegido por esta matriz: `hr.payroll.manage` solo está marcado para **HR Officer**, y el endpoint y la pantalla de nómina exigen ese permiso. El Administrador puede leer el listado de empleados con `hr.employees.view`, pero no ve salarios ni ejecuta la nómina. Cada rechazo se audita sin nombres, documentos ni montos.
