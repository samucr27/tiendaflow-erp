# Guion del video: tour de arquitectura (máximo 1 minuto)

Este guion coincide con `docs/sprint2/video-guide.md`. Duración objetivo: 60 segundos.
Palabras habladas: 137 (tope: 140). Lo graba una persona del equipo.

**Páginas de Notion:** `05 — Vista de bloques de construcción` y `10 — Conceptos de arquitectura: seguridad y roles`.

## 1. Guion

| Tiempo | Página y punto a señalar | Texto |
|---|---|---|
| 0:00–0:10 | Página 05, tabla **5.1**, cuatro filas de capas. | "Hola. TiendaFlow es un monolito modular en cuatro capas: Domain, Application, Infrastructure y Web. Domain y Application no conocen EF Core." |
| 0:10–0:30 | Página 05, sección **5.2**, filas `PayrollEngine` e `IPayrollStrategy`; luego secciones **5.4 a 5.6**. | "En la sección 5 están los módulos. Nómina usa PayrollEngine: valida, elige la estrategia según el contrato y delega en interfaces, así que cumple SRP y DIP. Compras, Facturación y CRM siguen el mismo patrón: entidad en Domain, servicio de caso de uso en Application, repositorio y migración en Infrastructure, y páginas Blazor en Web." |
| 0:30–0:50 | Página 10, tabla **10.2**, filas `hr.payroll.manage` y `sales.invoices.manage`; luego tabla **10.3**. | "En la sección 10, una sola matriz de permisos asigna cada función a los siete roles. Por ejemplo, hr.payroll.manage es solo para HR Officer; sales.invoices.manage cubre Administrador, Gerente Corporativo, Jefe de Tienda y Cajero." |
| 0:50–1:00 | Volver a página 05, sección **5.7**. | "La coherencia es esta: cada módulo de la Sección 5 exige en página, interfaz y servicio el permiso que la matriz de la Sección 10 le asigna." |

**Frase de coherencia:** "La coherencia es esta: cada módulo de la Sección 5 exige en página, interfaz y servicio el permiso que la matriz de la Sección 10 le asigna."

La lista de verificación previa a grabar está en `docs/sprint2/video-guide.md`.

## 2. Tabla de verificación: módulo, código y permiso

Cada fila se puede comprobar abriendo el archivo indicado. La columna de permisos
se contrasta con `src/TiendaFlow.Application/Security/RolePermissions.cs`, que es la
única fuente de la matriz.

| Módulo (Sección 5) | Componente de aplicación | Persistencia | Páginas | Permiso exigido | Roles con acceso |
|---|---|---|---|---|---|
| Nómina (5.2) | `src/TiendaFlow.Application/Payroll/PayrollEngine.cs` | Sin tablas propias: la corrida no se persiste | `/rrhh/nomina` | `hr.payroll.manage` | HR Officer |
| Empleados (5.3) | `src/TiendaFlow.Application/EmployeeService.cs` | `Employees` (migración `20261109010416_Phase2Security`) | `/rrhh/empleados` | `hr.employees.view` / `hr.employees.manage` | ADM, CM, SM, HR (ver) / HR (editar) |
| Proveedores (5.4) | `src/TiendaFlow.Application/SupplierService.cs` | `supplier` (migración `20261109011701_Phase3Purchasing`) | `/proveedores` | `purchasing.suppliers.view` / `purchasing.suppliers.manage` | ADM, CM, SM, LC (ver) / LC (editar) |
| Facturación (5.5) | `src/TiendaFlow.Application/SalesInvoiceService.cs` | `sales_invoice`, `sales_invoice_line` (migración `20261111005733_Phase4Invoicing`) | `/facturacion` | `sales.invoices.view` / `sales.invoices.manage` | ADM, CM, SM, CSH |
| CRM clientes (5.6) | `src/TiendaFlow.Application/CustomerService.cs` | `customer` (migración `20261111010125_Phase5Crm`) | `/crm/clientes` | `crm.customers.view` / `crm.customers.manage` | ADM, CM, SM, CSH |
| Seguridad (10) | `src/TiendaFlow.Application/Security/RolePermissions.cs` | `Roles`, `AppUsers` | `/seguridad/roles`, `/seguridad/usuarios` | `security.*` | ADM |

**Comprobación de coherencia:** las tres páginas nuevas llevan
`[Authorize(Policy = ...)]` con el mismo permiso que el servicio exige con
`IAccessControl.Demand`, y el menú (`NavMenu.razor`) solo muestra la sección cuando
el usuario tiene el permiso de lectura. Un rol sin el permiso recibe el redirect a
`/acceso-denegado` y el intento queda auditado.
