# 5. Building Block View

## 5.1 Level 1 - Whitebox: TiendaFlow ERP (modular monolith)

TiendaFlow is a single deployable Blazor Web App organised in four layers with
a strict dependency direction: **Domain <- Application <- Infrastructure <- Web**.
Domain and Application never reference EF Core.

| Building block | Responsibility |
|---|---|
| **TiendaFlow.Domain** | Entities, enums (for example `ContractType`) and domain exceptions. No framework dependencies. |
| **TiendaFlow.Application** | Use-case services (`StockService`, `InventoryService`, `SupplierService`, `PurchaseOrderService`, `AuthenticationService`), the permission catalog (`Permissions`), the role matrix (`RolePermissions`), `IAccessControl`, `IAuditLog`, and the **Payroll module**. Depends only on abstractions. |
| **TiendaFlow.Infrastructure** | EF Core 9 persistence for SQL Server (`TiendaFlowDbContext`, fluent configurations, repositories, unit of work, migrations). |
| **TiendaFlow.Web** | Blazor Web App (interactive server), cookie authentication, one authorization policy per permission, Minimal API endpoints, formatting and Spanish error translation. |

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

## 5.3 Cross-reference with Section 10

The payroll endpoint is protected by `hr.payroll.manage`, which the role matrix
(Section 10.2) grants only to the **HR Officer**. Payroll data is therefore
reachable by one role, and every denied attempt is audited.