# 05 — Vista de bloques de construcción

## 5.1 Vista de nivel 1: TiendaFlow ERP (monolito modular)

TiendaFlow es una única aplicación Blazor Web App organizada en cuatro capas con una dirección de dependencia estricta: **Domain <- Application <- Infrastructure <- Web**. Domain y Application no referencian EF Core.

| Bloque | Responsabilidad |
|---|---|
| **TiendaFlow.Domain** | Entidades y agregados (`Product`, `StockItem`, `Supplier`, `PurchaseOrder`, `SalesInvoice`/`InvoiceLine`, `Customer`, `Employee`), reglas, enums (por ejemplo `ContractType`, `InvoiceStatus`) y excepciones de dominio. Sin dependencias de framework. |
| **TiendaFlow.Application** | Servicios de caso de uso (`StockService`, `InventoryService`, `SupplierService`, `PurchaseOrderService`, `SalesInvoiceService`, `CustomerService`, `EmployeeService`, `AuthenticationService`), modelos de lectura, abstracciones de repositorio y consulta, catálogo de permisos (`Permissions`), matriz de roles (`RolePermissions`), `IAccessControl`, `IAuditLog` y el **módulo de nómina** (`PayrollEngine`, `PayrollRunService`, `ContractTypeLabels`). Depende solo de abstracciones. |
| **TiendaFlow.Infrastructure** | Persistencia con EF Core 9 sobre SQL Server (`TiendaFlowDbContext`, configuraciones fluent, repositorios, consultas, unidad de trabajo, migraciones). |
| **TiendaFlow.Web** | Blazor Web App con renderizado interactivo en servidor, autenticación por cookie, una política de autorización por permiso, endpoints Minimal API, formateo y traducción de errores al español. Inventario bajo `/inventario/*`; Compras en `/proveedores`; Facturación en `/facturacion`; CRM en `/crm/clientes`; RR. HH. bajo `/rrhh/*`. |

## 5.2 Vista de nivel 2: módulo de nómina (HU-04)

> **Decisión de arquitectura.** La guía del laboratorio habla de un "microservicio de nómina". Se implementó como un **módulo dentro del monolito modular**, con su propio motor, sus propios contratos y su propio endpoint HTTP. Razón: un solo desarrollador, una sola unidad de despliegue y un sprint muy corto hacen que un servicio separado agregue costo operativo (red, despliegue, transacciones distribuidas) sin beneficio. El módulo está aislado detrás de `IPayrollEngine`, por lo que puede extraerse como servicio más adelante sin cambiar a sus consumidores.

| Componente | Responsabilidad | Principio |
|---|---|---|
| `PayrollEngine` (`IPayrollEngine`) | Valida la entrada, selecciona la estrategia según el tipo de contrato y delega. Ofrece `Calculate` y `CalculateBatch`. | SRP: solo orquesta. DIP: depende solo de abstracciones. |
| `IPayrollStrategy` | Una implementación por familia de contratos: `EmploymentPayrollStrategy` (indefinido, término fijo, obra o labor) y `ServiceProvisionPayrollStrategy` (solo retención). | Abierto/cerrado: un tipo de contrato nuevo implica una estrategia nueva. |
| `IWithholdingCalculator` (`WithholdingCalculator`) | Retención mensual en la fuente con la tabla simplificada de UVT. | SRP: una regla, una clase. |
| `IPayrollParametersProvider` (`DefaultPayrollParametersProvider`) | Entrega `PayrollParameters` (salario mínimo, auxilio de transporte, UVT, tarifas de aportes, provisiones). Los parámetros cambian por ley, así que son configurables y nunca están embebidos en la lógica. | DIP |
| `PayrollInput` / `PayrollResult` | Registros inmutables que se intercambian con los llamadores. | Contratos estables. |
| Endpoint `POST /api/nomina/calculate` (Web) | Minimal API; exige el permiso `hr.payroll.manage`; traduce excepciones de dominio a `400 { error }`. | Autorización en el borde. |

**Relaciones:** `Endpoint → IPayrollEngine → IPayrollStrategy → (IWithholdingCalculator, IPayrollParametersProvider)`.

**Diagramas:** el diagrama de secuencia está en `docs/uml/payroll_sequence.puml` y el de la corrida mensual en `docs/uml/payroll_run_sequence.puml`. En `docs/notion/README.md` se explica cómo convertirlos a PNG si se quieren insertar en Notion.

## 5.3 Vista de nivel 2: empleados y corrida de nómina (HU-04)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `EmployeeService` (Application) | Lista y edita empleados para las pantallas de RR. HH. La lectura exige `hr.employees.view`; solo quien tiene `hr.employees.manage` recibe el salario; en caso contrario la fila expone `null` y la interfaz muestra "—". Valida el tipo de contrato, la tienda, el salario y el documento duplicado. Audita cada escritura con módulo `hr` sin nombres ni montos. | SRP; minimización de datos de la Ley 1581 de 2012. |
| `PayrollRunService` (Application) | Calcula la nómina mensual de todos los empleados **activos**. Valida los días trabajados (1 a 30) y la clase de riesgo ARL (1 a 5), y delega cada empleado a `IPayrollEngine`. Un salario faltante, un contrato desconocido o un rechazo del motor se convierten en una línea con `Problem` en español, sin tumbar la corrida. Suma totales solo sobre las líneas calculadas y audita la cantidad de empleados y los días. | SRP: orquestación y resiliencia. DIP: depende de abstracciones. |
| `ContractTypeLabels` (Application) | Traduce entre el enum `ContractType` y las etiquetas en español guardadas en `Employees.ContractType` (`Indefinido`, `Término fijo`, `Obra o labor`, `Prestación de servicios`). Al parsear acepta variantes sin tilde y nombres del enum. | Mantiene desacopladas las etiquetas de persistencia y el enum de nómina. |
| Páginas `/rrhh/empleados`, `/rrhh/empleados/nuevo`, `/rrhh/empleados/{id}` y `/rrhh/nomina` (Web) | Listado de empleados, formulario de alta/edición y pantalla de corrida mensual. Políticas: `hr.employees.view`, `hr.employees.manage` y `hr.payroll.manage`. La pantalla de nómina muestra un aviso visible de que los parámetros son de referencia y están pendientes de validación legal. | Defensa en profundidad: política de página, visibilidad en la interfaz y `Demand` en el servicio. |

**Limitación explícita:** las corridas de nómina **no se persisten**. `PayrollRunService` calcula y devuelve un resultado en memoria; al cerrar o recalcular la página se descarta. Persistir corridas, sus líneas y un cierre de período queda para un sprint posterior y requeriría tablas nuevas (y por tanto una migración).

## 5.4 Vista de nivel 2: proveedores y órdenes de compra (Sprint 2)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `Supplier` (Domain) | NIT como clave de negocio, razón social, dirección, calificación de 0 a 100 y estado activo. Rechaza datos inválidos y expone la etiqueta de calificación que usa la pantalla. | Un agregado, un conjunto de invariantes. |
| `PurchaseOrder` / `PurchaseOrderLine` (Domain) | Flujo de borrador, aprobación, envío, confirmación, recepción y cancelación; el total se deriva de las líneas. Una orden cancelada conserva su historial. | Los cambios de estado solo ocurren por métodos con intención. |
| `SupplierService` (Application) | Lista, crea, actualiza y desactiva proveedores. La lectura exige `purchasing.suppliers.view`; la escritura exige `purchasing.suppliers.manage`; cada escritura se audita con módulo `purchasing`. | Defensa en profundidad; SRP. |
| `PurchaseOrderService` (Application) | Crea y avanza el flujo de órdenes con permisos `purchasing.orders.*` y transiciones auditadas. | Un servicio por familia de casos de uso. |
| `ISupplierRepository` / `ISupplierQuery` (Application) | Separación entre escritura y lectura; la consulta devuelve `SupplierRow` en lugar de entidades. | DIP; modelos de lectura. |
| `EfSupplierRepository`, `EfSupplierQuery`, `SupplierConfiguration` (Infrastructure) | Mapeo EF Core de las tablas `supplier` y `purchase_order*`, generado por la migración `20261109011701_Phase3Purchasing`. | Ignorancia de la persistencia en Domain/Application. |
| Páginas `/proveedores`, `/proveedores/nuevo`, `/proveedores/{id:int}` (Web) | Lista en español con búsqueda, formulario de alta, formulario de edición y acción de desactivación. `[Authorize]` usa los mismos códigos de permiso que el servicio. | Política de página + visibilidad en la interfaz + `Demand` en el servicio. |

## 5.5 Vista de nivel 2: facturación (Sprint 2)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `SalesInvoice` / `InvoiceLine` (Domain) | Factura con nombre y documento del cliente guardados como **copia histórica**, fecha de emisión, estado (`Issued`/`Cancelled`) y líneas. Cada línea copia SKU, descripción, cantidad y precio unitario; el total se deriva. Una factura anulada no se puede editar ni volver a anular. | El agregado protege sus líneas; un documento emitido no depende del catálogo actual. |
| `SalesInvoiceService` (Application) | Lista y lee facturas (`sales.invoices.view`), las crea, edita y anula (`sales.invoices.manage`). La anulación reemplaza el borrado físico para conservar numeración e historial. Cada escritura se audita con módulo `sales`. | SRP; auditoría; DIP. |
| `IInvoiceRepository` / `IInvoiceQuery`, `InvoiceRow` / `InvoiceDetail` (Application) | La escritura carga el agregado con sus líneas; la lectura proyecta filas y detalle sin tracking. | Separación de lecturas y escrituras. |
| `EfInvoiceRepository`, `EfInvoiceQuery`, `SalesInvoiceConfiguration`, `InvoiceLineConfiguration` (Infrastructure) | Mapeo EF Core de `sales_invoice` y `sales_invoice_line`, migración `20261111005733_Phase4Invoicing`. La identidad de línea y un índice no único `(invoice_id, product_sku)` permiten reemplazar líneas al editar. | El detalle de persistencia queda fuera del dominio. |
| Páginas `/facturacion`, `/facturacion/nueva`, `/facturacion/{id:int}` (Web) | Lista con búsqueda y acción de anulación, formulario de alta/edición con líneas dinámicas y vista de solo lectura para facturas anuladas. | Política de página + visibilidad en la interfaz + `Demand` en el servicio. |

**Limitación explícita:** la facturación electrónica (DIAN), los pagos y el cálculo de impuestos quedan **fuera de alcance** en este sprint. El documento guarda los datos necesarios para mostrar y auditar una venta, no un documento fiscal.

## 5.6 Vista de nivel 2: registro de clientes para CRM (Sprint 2)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `Customer` (Domain) | Documento único, nombre completo, correo, teléfono y dirección opcionales, y estado activo. Recorta y convierte en `null` los campos opcionales vacíos; rechaza documento o nombre ausentes. | Un agregado, un conjunto de invariantes. |
| `CustomerService` (Application) | Lista y lee clientes (`crm.customers.view`), los crea y actualiza (`crm.customers.manage`), verifica que el documento sea único y audita cada escritura con módulo `crm`. | SRP; defensa en profundidad. |
| `ICustomerRepository` / `ICustomerQuery`, `CustomerRow` (Application) | Separación entre escritura y lectura; la fila expone un valor `Contact` formateado para la tabla. | DIP; modelos de lectura. |
| `EfCustomerRepository`, `EfCustomerQuery`, `CustomerConfiguration` (Infrastructure) | Mapeo EF Core de `customer` con índice único en `document_number`; migración `20261111010125_Phase5Crm`. | Ignorancia de la persistencia. |
| Páginas `/crm/clientes`, `/crm/clientes/nuevo`, `/crm/clientes/{id:int}` (Web) | Lista con búsqueda y acción de desactivación, formulario de alta/edición con datos de contacto y estado activo. | Política de página + visibilidad en la interfaz + `Demand` en el servicio. |

**Limitación explícita:** segmentación, campañas, puntos y PQRS quedan **fuera de alcance** en este sprint. El módulo cubre el registro de clientes que necesitan las demás pantallas.

## 5.7 Coherencia con la Sección 10

El endpoint de nómina está protegido por `hr.payroll.manage`, que la matriz de la Sección 10.2 concede únicamente al **Oficial de RR. HH.** Los datos de nómina solo son alcanzables por un rol y cada intento denegado queda auditado. La misma matriz protege las pantallas de empleados: el Administrador puede ver el listado con `hr.employees.view`, pero no administra salarios ni ejecuta la nómina porque no tiene `hr.employees.manage` ni `hr.payroll.manage`.

Los módulos del Sprint 2 siguen la misma regla:

- **Proveedores:** `purchasing.suppliers.view` se concede a ADM, CM, SM y LC; `purchasing.suppliers.manage` solo a LC. La página oculta los controles de alta, edición y desactivación a los demás roles y el servicio rechaza la llamada.
- **Facturación:** `sales.invoices.view` y `sales.invoices.manage` se conceden a ADM, CM, SM y CSH, de modo que la tienda puede emitir y anular facturas mientras SC y HR no alcanzan la página.
- **CRM clientes:** `crm.customers.view` y `crm.customers.manage` se conceden a ADM, CM, SM y CSH. Los usuarios sembrados para SC, LC y HR no ven la entrada **CRM** en el menú.

## 5.8 Trazabilidad de la entrega

| Historia o módulo | Componente principal | Pruebas | Documento de referencia |
|---|---|---|---|
| HU-04 | `PayrollEngine`, `PayrollRunService`, `EmployeeService`, páginas `/rrhh/*` | `PayrollEngineTests`, `PayrollRunServiceTests`, `EmployeeServiceTests` | `docs/sprint2/payroll-parameters-validation.md` |
| HU-05 | `StockService`, `InventoryDashboardService`, páginas `/inventario/*` | `InventoryDashboardServiceTests`, escenario de aceptación de stock | `docs/sprint2/demo-script.md` |
| HU-06 | `UserService`, `RoleCatalogService`, `AuditTrailService`, páginas `/seguridad/*` | `UserServiceTests`, `AuthenticationServiceTests` | `docs/sprint2/code-review.md` |
| Proveedores | `SupplierService`, `PurchaseOrderService`, páginas `/proveedores` | `SupplierServiceTests`, `PurchaseOrderServiceTests` | `docs/sprint2/demo-script.md` |
| Facturación | `SalesInvoiceService`, páginas `/facturacion` | `SalesInvoiceTests`, `SalesInvoiceServiceTests` | `docs/sprint2/demo-script.md` |
| CRM clientes | `CustomerService`, páginas `/crm/clientes` | `CustomerTests`, `CustomerServiceTests` | `docs/sprint2/demo-script.md` |
