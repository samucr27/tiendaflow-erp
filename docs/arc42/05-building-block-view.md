# 5. Building Block View

## 5.1 Level 1 - Whitebox: TiendaFlow ERP (modular monolith)

TiendaFlow is a single deployable Blazor Web App organised in four layers with
a strict dependency direction: **Domain <- Application <- Infrastructure <- Web**.
Domain and Application never reference EF Core.

| Building block | Responsibility |
|---|---|
| **TiendaFlow.Domain** | Entities and aggregates (`Product`, `StockItem`, `Supplier`, `PurchaseOrder`, `SalesInvoice`/`InvoiceLine`, `Customer`, `Employee`), value rules, enums (for example `ContractType`, `InvoiceStatus`) and domain exceptions. No framework dependencies. |
| **TiendaFlow.Application** | Use-case services (`StockService`, `InventoryService`, `SupplierService`, `PurchaseOrderService`, `SalesInvoiceService`, `CustomerService`, `EmployeeService`, `AuthenticationService`), read models and repository/query abstractions, the permission catalog (`Permissions`), the role matrix (`RolePermissions`), `IAccessControl`, `IAuditLog`, and the **Payroll module** (`PayrollEngine`, `PayrollRunService`, `ContractTypeLabels`). Depends only on abstractions. |
| **TiendaFlow.Infrastructure** | EF Core 9 persistence for SQL Server (`TiendaFlowDbContext`, fluent configurations, repositories, queries, unit of work, migrations). |
| **TiendaFlow.Web** | Blazor Web App (interactive server), cookie authentication, one authorization policy per permission, Minimal API endpoints, formatting and Spanish error translation. Inventory under `/inventario/*`, purchasing under `/proveedores`, invoicing under `/facturacion`, CRM under `/crm/clientes`, HR under `/rrhh/*`. |

## 5.2 Level 2 - Whitebox: Payroll module (HU-04)

> **Architecture decision.** The lab guide refers to a "Payroll microservice".
> We implemented payroll as a **module inside the modular monolith**, with its
> own engine, its own contracts and its own HTTP endpoint. Reason: one
> developer, one deployment unit and a very short sprint make a separate
> service add operational cost (network, deployment, distributed
> transactions) without benefit. The module is isolated behind
> `IPayrollEngine`, so it can be extracted into a service later without
> changing its callers.

| Component | Responsibility | Principle |
|---|---|---|
| `PayrollEngine` (`IPayrollEngine`) | Validates the input, selects the strategy for the contract type and delegates. Offers `Calculate` and `CalculateBatch`. | SRP: orchestration only. DIP: depends only on abstractions. |
| `IPayrollStrategy` | One implementation per family of contract types: `EmploymentPayrollStrategy` (indefinite, fixed term, work or service) and `ServiceProvisionPayrollStrategy` (withholding only). | Open/closed: a new contract type means a new strategy. |
| `IWithholdingCalculator` (`WithholdingCalculator`) | Employee income-tax withholding with the simplified UVT table. | SRP: one rule, one class. |
| `IPayrollParametersProvider` (`DefaultPayrollParametersProvider`) | Supplies `PayrollParameters` (minimum wage, transport subsidy, UVT, contribution rates, provisions). Parameters change by law, so they are configurable and never hard-coded in the logic. | DIP |
| `PayrollInput` / `PayrollResult` | Immutable records exchanged with callers. | - |
| Endpoint `POST /api/nomina/calculate` (Web) | Minimal API; requires permission `hr.payroll.manage`; maps domain exceptions to `400 { error }`. | - |

Relationships: `Endpoint -> IPayrollEngine -> IPayrollStrategy -> (IWithholdingCalculator, IPayrollParametersProvider)`.
The sequence diagram is in `docs/uml/payroll_sequence.puml`.

## 5.3 Level 2 - Whitebox: Employee management and payroll run (HU-04)

| Component | Responsibility | Principle |
|---|---|---|
| `EmployeeService` (Application) | Lists and edits employees for the HR screens. Reading requires `hr.employees.view`; only callers with `hr.employees.manage` receive the salary, otherwise the row exposes `null` and the UI shows "—". Validates the contract type, store, salary and duplicate document, and audits every write with module `hr` without names or amounts. | SRP; Ley 1581 de 2012 data minimisation. |
| `PayrollRunService` (Application) | Runs the monthly payroll for every **active** employee. Validates days worked (1-30) and ARL risk class (1-5), then delegates each employee to `IPayrollEngine`. A missing salary, unknown contract or engine rejection becomes a per-line `Problem` in Spanish instead of failing the whole run. Returns totals only over calculated lines and audits the employee count and days. | SRP: orchestration and resilience. Depends on abstractions (DIP). |
| `ContractTypeLabels` (Application) | Translates between the `ContractType` enum and the Spanish labels stored in `Employees.ContractType` (`Indefinido`, `Término fijo`, `Obra o labor`, `Prestación de servicios`). Accepts unaccented variants and enum names when parsing. | Keeps persistence labels and payroll enum decoupled. |
| Pages `/rrhh/empleados`, `/rrhh/empleados/nuevo`, `/rrhh/empleados/{id}`, `/rrhh/nomina` (Web) | Employee list, create/edit form and monthly run screen. Policies: `hr.employees.view`, `hr.employees.manage` and `hr.payroll.manage`. The run screen shows a visible warning that the parameters are reference values pending legal validation. | Defence in depth: page policy + UI visibility + service `Demand`. |

**Explicit limitation:** payroll runs are **not persisted**. `PayrollRunService`
calculates and returns an in-memory result; closing or recalculating the page
discards it. Persisting runs, their lines and a period close is left for a
later sprint and would require new tables (and therefore a migration).

## 5.4 Level 2 - Whitebox: Supplier registry and purchase orders (Sprint 2)

| Component | Responsibility | Principle |
|---|---|---|
| `Supplier` (Domain) | NIT (business key), business name, address, rating (0-100) and active flag. Rejects invalid data and exposes the rating label used by the screen. | One aggregate, one set of invariants. |
| `PurchaseOrder` / `PurchaseOrderLine` (Domain) | Draft, approval, send, confirm, receive and cancel flow; total is derived from its lines. Cancelled orders keep their history. | State changes only through intention-revealing methods. |
| `SupplierService` (Application) | Lists, creates, updates and deactivates suppliers. Reading demands `purchasing.suppliers.view`; writing demands `purchasing.suppliers.manage`; every write is audited with module `purchasing`. | Defence in depth; SRP. |
| `PurchaseOrderService` (Application) | Creates and advances the order flow with `purchasing.orders.*` permissions and audited transitions. | One service per use-case family. |
| `ISupplierRepository` / `ISupplierQuery` (Application) | Write side and read side separated; the query returns `SupplierRow` instead of entities. | DIP; read models. |
| `EfSupplierRepository`, `EfSupplierQuery`, `SupplierConfiguration` (Infrastructure) | EF Core mapping for `supplier` and `purchase_order*` tables, generated by migration `20261109011701_Phase3Purchasing`. | Persistence ignorance in Domain/Application. |
| Pages `/proveedores`, `/proveedores/nuevo`, `/proveedores/{id:int}` (Web) | Spanish list with search, create form, edit form and deactivate action. `[Authorize]` uses the same permission codes as the service. | Page policy + UI visibility + service `Demand`. |

## 5.5 Level 2 - Whitebox: Invoicing (Sprint 2)

| Component | Responsibility | Principle |
|---|---|---|
| `SalesInvoice` / `InvoiceLine` (Domain) | Invoice with customer name and document stored as a **snapshot**, issue date, status (`Issued`/`Cancelled`) and lines. Each line copies the SKU, description, quantity and unit price; the total is derived. A cancelled invoice cannot be edited or cancelled again. | Aggregate root guards its lines; issued documents never depend on the current catalog. |
| `SalesInvoiceService` (Application) | Lists and reads invoices (`sales.invoices.view`), creates, edits and cancels them (`sales.invoices.manage`). Cancellation replaces physical deletion so numbering and history stay intact. Every write is audited with module `sales`. | SRP; audit; DIP. |
| `IInvoiceRepository` / `IInvoiceQuery`, `InvoiceRow` / `InvoiceDetail` (Application) | Write side loads the aggregate with its lines; the read side projects rows and detail without tracking. | Separation of reads and writes. |
| `EfInvoiceRepository`, `EfInvoiceQuery`, `SalesInvoiceConfiguration`, `InvoiceLineConfiguration` (Infrastructure) | EF Core mapping for `sales_invoice` and `sales_invoice_line`, migration `20261111005733_Phase4Invoicing`. Line identity plus a non-unique `(invoice_id, product_sku)` index allows replacing lines on edit. | Persistence detail stays outside the domain. |
| Pages `/facturacion`, `/facturacion/nueva`, `/facturacion/{id:int}` (Web) | List with search and cancel action, create/edit form with dynamic lines and read-only view for cancelled invoices. | Page policy + UI visibility + service `Demand`. |

**Explicit limitation:** electronic invoicing (DIAN), payments and tax calculation are
**out of scope** for this sprint. The document stores the data needed to display and
audit a sale, not a fiscal document.

## 5.6 Level 2 - Whitebox: CRM customer registry (Sprint 2)

| Component | Responsibility | Principle |
|---|---|---|
| `Customer` (Domain) | Unique document number, full name, optional email/phone/address and active flag. Trims and nulls blank optional values; rejects a missing document or name. | One aggregate, one set of invariants. |
| `CustomerService` (Application) | Lists and reads customers (`crm.customers.view`), creates and updates them (`crm.customers.manage`), checks the document is unique and audits every write with module `crm`. | SRP; defence in depth. |
| `ICustomerRepository` / `ICustomerQuery`, `CustomerRow` (Application) | Write side and read side separated; the row exposes a formatted `Contact` value for the table. | DIP; read models. |
| `EfCustomerRepository`, `EfCustomerQuery`, `CustomerConfiguration` (Infrastructure) | EF Core mapping for `customer` with a unique index on `document_number`; migration `20261111010125_Phase5Crm`. | Persistence ignorance. |
| Pages `/crm/clientes`, `/crm/clientes/nuevo`, `/crm/clientes/{id:int}` (Web) | List with search and deactivate action, create/edit form with contact data and active flag. | Page policy + UI visibility + service `Demand`. |

**Explicit limitation:** segmentation, campaigns, points and PQRS are **out of scope**
for this sprint. The module covers the customer registry the other screens need.

## 5.7 Cross-reference with Section 10

The payroll endpoint is protected by `hr.payroll.manage`, which the role matrix
(Section 10.2) grants only to the **HR Officer**. Payroll data is therefore
reachable by one role, and every denied attempt is audited.

The Sprint 2 modules follow the same rule:

- Suppliers: `purchasing.suppliers.view` is granted to ADM, CM, SM and LC;
  `purchasing.suppliers.manage` only to LC. The page hides the create, edit and
  deactivate controls from the other roles and the service rejects the call.
- Invoicing: `sales.invoices.view` and `sales.invoices.manage` are granted to
  ADM, CM, SM and CSH, so the store can issue and cancel invoices while SC and
  HR cannot reach the page.
- CRM customers: `crm.customers.view` is granted to ADM, CM, SM and CSH;
  `crm.customers.manage` to the same four roles. The seed users for SC, LC and
  HR therefore see no "CRM" menu entry.