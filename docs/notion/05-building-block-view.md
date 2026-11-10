# 05 — Vista de bloques de construcción

## 5.1 Vista de nivel 1: TiendaFlow ERP (monolito modular)

TiendaFlow es una única aplicación Blazor Web App organizada en cuatro capas con una dirección de dependencia estricta: **Domain <- Application <- Infrastructure <- Web**. Domain y Application no referencian EF Core.

| Bloque | Responsabilidad |
|---|---|
| **TiendaFlow.Domain** | Entidades, enums (por ejemplo `ContractType`) y excepciones de dominio. Sin dependencias de framework. |
| **TiendaFlow.Application** | Servicios de caso de uso (`StockService`, `InventoryService`, `SupplierService`, `PurchaseOrderService`, `EmployeeService`, `AuthenticationService`), catálogo de permisos (`Permissions`), matriz de roles (`RolePermissions`), `IAccessControl`, `IAuditLog` y el **módulo de nómina** (`PayrollEngine`, `PayrollRunService`, `ContractTypeLabels`). Depende solo de abstracciones. |
| **TiendaFlow.Infrastructure** | Persistencia con EF Core 9 sobre SQL Server (`TiendaFlowDbContext`, configuraciones fluent, repositorios, unidad de trabajo, migraciones). |
| **TiendaFlow.Web** | Blazor Web App con renderizado interactivo en servidor, autenticación por cookie, una política de autorización por permiso, endpoints Minimal API, formateo y traducción de errores al español. Las pantallas de RR. HH. viven bajo `/rrhh/*`. |

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

## 5.4 Coherencia con la Sección 10

El endpoint de nómina está protegido por `hr.payroll.manage`, que la matriz de la Sección 10.2 concede únicamente al **Oficial de RR. HH.** Los datos de nómina solo son alcanzables por un rol y cada intento denegado queda auditado. La misma matriz protege las pantallas de empleados: el Administrador puede ver el listado con `hr.employees.view`, pero no administra salarios ni ejecuta la nómina porque no tiene `hr.employees.manage` ni `hr.payroll.manage`.

## 5.5 Trazabilidad de la entrega

| Historia | Componente principal | Pruebas | Documento de referencia |
|---|---|---|---|
| HU-04 | `PayrollEngine`, `PayrollRunService`, `EmployeeService`, páginas `/rrhh/*` | `PayrollEngineTests`, `PayrollRunServiceTests`, `EmployeeServiceTests` | `docs/sprint2/payroll-parameters-validation.md` |
| HU-05 | `StockService`, `InventoryDashboardService`, páginas `/inventario/*` | `InventoryDashboardServiceTests`, escenario de aceptación de stock | `docs/sprint2/demo-script.md` |
| HU-06 | `UserService`, `RoleCatalogService`, `AuditTrailService`, páginas `/seguridad/*` | `UserServiceTests`, `AuthenticationServiceTests` | `docs/sprint2/code-review.md` |
