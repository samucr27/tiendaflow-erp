# 10. Architecture Concepts - Security and Roles (HU-06, HU-08)

## 10.1 Concepts

- **Authentication:** ASP.NET Core cookie authentication (`TiendaFlow.Auth`,
  HttpOnly, SameSite Lax, 8 hours sliding expiration). Passwords are stored
  only as hashes behind `IPasswordHasher`; never in plain text.
- **Roles and permissions:** each user has exactly one of 7 roles. Every ERP
  function has one permission code (`Permissions`), and the role-permission
  matrix lives in code (`RolePermissions`) so authorization is deterministic
  across deployments. The database stores users and roles only.
- **Authorization at three levels (defence in depth):**
  1. **Page / endpoint:** one authorization policy per permission, applied with
     `[Authorize(Policy=...)]` or `RequireAuthorization(permission)`.
  2. **UI:** menu entries and buttons are shown only when the user has the
     permission. This is convenience, never the real control.
  3. **Service:** `IAccessControl.Demand(permission)` throws
     `AccessDeniedException` inside the application services.
- **Audit:** the audit log stores the real user id, module, action and IP.
  Denied access attempts are recorded both at page level and at service level.
- **Personal data (Ley 1581 de 2012):** minimum data is collected, personal
  data is not logged, and payroll data is restricted to the HR Officer.
- **Separation of duties:** the Administrator manages users, roles and
  parameters and can read most modules, but holds no operational permissions
  (no POS, no purchase orders, no payroll).

## 10.2 Role-permission matrix

Roles: **ADM** Administrator, **CM** Corporate Manager, **SM** Store Manager,
**CSH** Cashier, **SC** Stock Clerk, **LC** Logistics Coordinator,
**HR** HR Officer.

| Permission | ADM | CM | SM | CSH | SC | LC | HR |
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

## 10.3 Role assignment (HU-06)

The administrator assigns a role from `/seguridad/usuarios`. The role matrix
is shown read-only in `/seguridad/roles`. On the next sign-in the user's
permission claims are built from the matrix, so the user can reach only the
allowed pages, menu entries and services.