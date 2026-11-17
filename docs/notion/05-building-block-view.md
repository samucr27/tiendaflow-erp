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

**Corridas guardadas:** `PayrollHistoryService` guarda la corrida de cada mes (tablas `payroll_run` y `payroll_run_line`) con una copia del resultado de cada empleado. Las ausencias registradas en asistencia se descuentan de los días trabajados. *Texto anterior:* las corridas de nómina **no se persistían**. `PayrollRunService` calcula y devuelve un resultado en memoria; al cerrar o recalcular la página se descarta. Persistir corridas, sus líneas y un cierre de período queda para un sprint posterior y requeriría tablas nuevas (y por tanto una migración).

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
| `SalesInvoice` / `InvoiceLine` (Domain) | Factura con nombre y documento del cliente guardados como **copia histórica**, tienda que la emite, fecha de emisión, estado (`Issued`/`Cancelled`) y líneas. Cada línea copia SKU, descripción, cantidad y precio unitario; el total se deriva. Una factura emitida **no se edita**: el dominio no ofrece métodos de edición y la corrección es anularla y emitir otra. | El agregado protege sus líneas; un documento emitido no depende del catálogo actual. |
| `SalesInvoiceService` (Application) | Lista y lee facturas (`sales.invoices.view`), las emite y anula (`sales.invoices.manage`). Emitir descuenta las unidades del stock de la tienda del usuario y anular las devuelve con un movimiento `Return`, en la misma transacción que el documento. Cada escritura se audita con módulo `sales`. | SRP; auditoría; DIP; unidad de trabajo. |
| `IInvoiceRepository` / `IInvoiceQuery`, `InvoiceRow` / `InvoiceDetail` (Application) | La escritura carga el agregado con sus líneas; la lectura proyecta filas y detalle sin tracking. | Separación de lecturas y escrituras. |
| `EfInvoiceRepository`, `EfInvoiceQuery`, `SalesInvoiceConfiguration`, `InvoiceLineConfiguration` (Infrastructure) | Mapeo EF Core de `sales_invoice` y `sales_invoice_line`, migración `20261111005733_Phase4Invoicing`. La identidad de línea y un índice no único `(invoice_id, product_sku)` permiten reemplazar líneas al editar. | El detalle de persistencia queda fuera del dominio. |
| Páginas `/facturacion`, `/facturacion/nueva`, `/facturacion/{id:int}` (Web) | Lista con búsqueda y anulación, formulario de emisión que elige productos del catálogo y detalle de solo lectura. | Política de página + visibilidad en la interfaz + `Demand` en el servicio. |

**Limitación explícita:** la factura electrónica real ante la DIAN, los pagos y el cálculo de impuestos quedan **fuera de alcance**. `ElectronicInvoiceService` genera un XML **simulado** con un resumen SHA-256, sin CUFE ni firma certificada.

## 5.6 Vista de nivel 2: registro de clientes para CRM (Sprint 2)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `Customer` (Domain) | Documento único, nombre completo, correo, teléfono y dirección opcionales, y estado activo. Recorta y convierte en `null` los campos opcionales vacíos; rechaza documento o nombre ausentes. | Un agregado, un conjunto de invariantes. |
| `CustomerService` (Application) | Lista y lee clientes (`crm.customers.view`), los crea y actualiza (`crm.customers.manage`), verifica que el documento sea único y audita cada escritura con módulo `crm`. | SRP; defensa en profundidad. |
| `ICustomerRepository` / `ICustomerQuery`, `CustomerRow` (Application) | Separación entre escritura y lectura; la fila expone un valor `Contact` formateado para la tabla. | DIP; modelos de lectura. |
| `EfCustomerRepository`, `EfCustomerQuery`, `CustomerConfiguration` (Infrastructure) | Mapeo EF Core de `customer` con índice único en `document_number`; migración `20261111010125_Phase5Crm`. | Ignorancia de la persistencia. |
| Páginas `/crm/clientes`, `/crm/clientes/nuevo`, `/crm/clientes/{id:int}` (Web) | Lista con búsqueda y acción de desactivación, formulario de alta/edición con datos de contacto y estado activo. | Política de página + visibilidad en la interfaz + `Demand` en el servicio. |

**Segmentación A/B/C:** `CustomerSegmentationService` ordena a los clientes por valor facturado y asigna A al 20 % superior, B al 30 % siguiente y C al resto (pantalla `/crm/segmentacion`, permiso `crm.reports.view`). Puntos de fidelización, PQRS y campañas se describen en la sección 5.6.3.

## 5.6.1 Vista de nivel 2: punto de venta (HU-05)

| Componente | Responsabilidad | Principio |
|---|---|---|
| `PosSale` / `PosSaleLine` (Domain) | Venta de mostrador con código, tienda, cajero, fecha y copia del nombre y precio de cada producto. | Documento inmutable; no depende del catálogo actual. |
| `StockItem` (Domain) | Cantidad disponible, mínimo y último movimiento (`Sale`, `Receipt`, `Return`, `Adjustment`, traslados). `IsBelowMinimum` define la alerta de HU-05. Columna `row_version` para control de concurrencia. | Las reglas de stock viven en la entidad. |
| `PosSaleService` (Application) | Descuenta el stock y guarda la venta en una sola transacción; devuelve el comprobante con el stock restante y el mínimo, que la pantalla usa para mostrar la alerta de stock crítico. Exige `sales.pos.use`. | SRP; unidad de trabajo. |
| `EfPosSaleRepository`, `EfPosSaleQuery` (Infrastructure) | Tablas `pos_sale` y `pos_sale_line` (migración `Phase11PosSalesAndInvoiceStock`); la consulta alimenta el panel, el reporte de ventas y el consumo de la reposición. | DIP; modelos de lectura. |
| `FreshTracking` (Infrastructure) | Antes de modificar una entidad, los repositorios descartan la copia en caché del `DbContext` de la pestaña de Blazor y leen el valor actual. | Evita escrituras con datos viejos entre sesiones. |

## 5.6.2 Vista de nivel 2: compras, reposición y documentos de inventario

| Componente | Responsabilidad |
|---|---|
| `ReplenishmentService` | Toma los productos en stock crítico, calcula la cantidad sugerida con el mínimo y el consumo promedio, y crea borradores de orden agrupados por proveedor predeterminado (`/compras/reposicion`). |
| `ProductSupplierService` | Relación producto · proveedor con último costo y proveedor predeterminado. |
| Ranking de proveedores | Calificación, puntualidad y cumplimiento calculados con las recepciones reales (`/proveedores/ranking`). |
| `InventoryCountService` / `InventoryTransferService` | Conteos físicos que ajustan el stock al cerrarse y traslados entre tiendas con salida y entrada. |
| `StoreService` / `ParameterService` | Administración de tiendas con desactivación protegida y parámetros versionados con fecha de vigencia. |
| Servicios de `Reporting` | Reportes de inventario, ventas (facturas, ventas POS y devoluciones), nómina y seguridad con exportación CSV. |

## 5.6.3 Vista de nivel 2: módulos de cierre del proyecto

| Componente | Responsabilidad | Permiso |
|---|---|---|
| `SalesReturn` / `SalesReturnService` | Devolución parcial de una factura emitida (nota crédito); limita las unidades a lo facturado menos lo ya devuelto y las regresa al stock. | `sales.returns.authorize` |
| `PosSaleService.RegisterCart` | Venta POS con varios productos en un solo documento y una sola transacción. | `sales.pos.use` |
| `EmploymentContract`, `AttendanceRecord`, `WorkShift` / `HumanResourcesRecordsService` | Contratos con vigencia y prórrogas, asistencia y novedades, y turnos sin cruces. | `hr.contracts.manage`, `hr.attendance.manage`, `hr.shifts.manage` |
| `PayrollRunRecord` / `PayrollHistoryService` | Corrida de nómina guardada por mes. | `hr.payroll.manage` |
| `LoyaltyPointsEntry` / `LoyaltyService` | Libro de puntos: un punto por cada $ 1.000 facturado a un cliente registrado; la anulación los revierte; canje. | `crm.points.manage` |
| `PqrsCase` / `PqrsService` | PQRS con radicado, responsable y plazo de 15 días. | `crm.pqrs.manage` |
| `MarketingCampaign` / `CampaignService` | Campañas por segmento A/B/C con su resultado en facturas. | `crm.campaigns.manage` |
| `RolePermissionGrant` / `RoleEditorService` | Matriz de roles editable que reemplaza la configuración por defecto en el siguiente inicio de sesión. | `security.roles.manage` |
| `SecurityReportService` | Conteos de inicios de sesión, accesos denegados y cambios de usuarios y roles. | `security.reports.view` |
| `IEntityStore<T>` / `EfEntityStore<T>` | Puerto genérico de persistencia de los módulos de cierre (migración `Phase12CloseoutModules`). | — |

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
| Facturación | `SalesInvoiceService`, páginas `/facturacion` | `SalesInvoiceTests`, `SalesInvoiceServiceTests`, `SalesDocumentsIntegrationTests` | `docs/final/testing.md` |
| Punto de venta | `PosSaleService`, página `/inventario/ventas` | `PosSaleServiceTests`, `LongLivedContextStockTests` | `docs/final/testing.md` |
| Reposición y compras | `ReplenishmentService`, `PurchaseOrderService`, páginas `/compras/*` | `ReplenishmentServiceTests`, `PurchaseOrderServiceTests` | `docs/final/implemented-modules.md` |
| CRM clientes | `CustomerService`, páginas `/crm/clientes` | `CustomerTests`, `CustomerServiceTests` | `docs/sprint2/demo-script.md` |
