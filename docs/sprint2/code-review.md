# Revisión de código — Sprint 2

> **Revisión automatizada preparada con asistente de código. No sustituye la aprobación de un integrante del equipo en GitHub.**

**Rama revisada:** `feature/opencode-erp-ui` frente a `origin/develop`
**Fecha:** 9 de noviembre de 2026
**Autor de la revisión:** asistente de código (OpenCode)
**Alcance temporal:** trabajo del Sprint 2 (nómina, empleados, Compras, seguridad/auditoría, documentación, CI/Terraform y Fase 1)
**Aprobación en GitHub:** no emitida. Esta revisión no aprueba, comenta ni fusiona el PR.

---

## 1. Alcance revisado

`git diff origin/develop...HEAD` muestra 255 archivos porque la rama arrastra historia anterior no relacionada con el Sprint 2 (`wwwroot/lib/bootstrap/**`, migraciones base, páginas del Sprint 1). Esta revisión **no** evalúa esos 238 archivos antiguos; se concentra en los siguientes grupos:

| Grupo | Archivos revisados |
|---|---|
| Nómina (Application) | `Payroll/PayrollEngine.cs`, `PayrollContracts.cs`, `PayrollModels.cs`, `PayrollParameters.cs`, `EmploymentPayrollStrategy.cs`, `ServiceProvisionPayrollStrategy.cs`, `WithholdingCalculator.cs`, `PayrollRunService.cs`, `ContractTypeLabels.cs` |
| Empleados | `Application/EmployeeService.cs`, `Domain/Employee.cs`, `Domain/EmployeeExceptions.cs`, páginas `Rrhh/Empleados.razor`, `Rrhh/EmpleadoForm.razor`, `Rrhh/Nomina.razor` |
| Seguridad y auditoría | `Application/Security/**`, `Application/AuditEntry.cs`, `Application/IAuditLog.cs`, `Infrastructure/Persistence/EfAuditLog.cs`, `Web/Program.cs`, páginas `Seguridad/**`, `AccesoDenegado.razor` |
| Compras | `Application/PurchaseOrderService.cs`, `Application/SupplierService.cs`, `Domain/PurchaseOrder*.cs`, `Domain/Supplier*.cs` |
| Inventario relacionado | `Application/StockService.cs`, `Application/InventoryDashboardService.cs`, `Components/Pages/Alertas.razor`, `Panel.razor`, `Ventas.razor` |
| Pruebas | `tests/TiendaFlow.UnitTests/**` (`EmployeeServiceTests`, `PayrollEngineTests`, `PayrollRunServiceTests`, `UserServiceTests`, `AuthenticationServiceTests`) |
| Documentación y CI | `docs/arc42/05`, `07`, `10`, `docs/sprint2/**`, `docs/uml/payroll_*.puml`, `.github/workflows/ci.yml`, `.github/workflows/terraform-plan.yml`, `infra/terraform/main.tf` |

**Fuera de alcance (al momento de esta revisión):** migraciones y esquema de base de datos (no se modificaron en esa revisión), infraestructura real (Terraform es un *placeholder* declarado), contenido del Sprint 1 y archivos generados (`tests/TiendaFlow.AcceptanceTests/Features/ConsultStock.feature.cs`, que no se versiona).

### Evidencia de compilación y pruebas al momento de la revisión

| Comando | Resultado |
|---|---|
| `dotnet build TiendaFlow.slnx` | 0 advertencias, 0 errores |
| `dotnet test TiendaFlow.slnx` | 244 unitarias correctas, 6 de aceptación correctas, 17 de integración omitidas, 0 fallos |
| Cobertura Application + Domain | 87,6 % de líneas al cierre (85,6 % en la línea base) |

> Estas cifras son la foto de esta revisión. El cierre del Sprint 2, con los módulos de Proveedores, Facturación y CRM, se reporta en `docs/sprint2/retrospective.md`: 88,4 % de líneas y 73,6 % de ramas.

---

## 2. Resumen ejecutivo

- **No hay hallazgos bloqueantes para el alcance del Sprint 2.** La solución compila sin advertencias, las pruebas pasan y las tres capas de permisos están presentes en las páginas y servicios nuevos.
- **Sí hay tres discrepancias legales en los parámetros de nómina** que son **bloqueantes para uso productivo**, pero la Fase 3 ordenó no cambiar los valores en código: quedan documentadas y con propuesta en `docs/sprint2/payroll-parameters-validation.md`.
- Se corrigieron **dos errores claros y pequeños**, cada uno en su propio commit: correos en el detalle de auditoría de cuentas (`5f8719f`) y verificaciones desalineadas del guion de demo (`3f90d73`).
- El resto son hallazgos **importantes o menores** que se recomienda atender en el Sprint 3, no en este PR.

---

## 3. Hallazgos por severidad

### 3.1 Bloqueante

**Ninguno para el alcance del Sprint 2.** No se encontraron fallos de compilación, pruebas rojas, referencias a EF Core en Domain/Application, ausencia de permisos en servicios nuevos ni datos personales en los detalles de auditoría de recursos humanos.

> Aclaración: las tres discrepancias de nómina de la sección 3.2 son **bloqueantes para producción**, no para la entrega del Sprint 2, porque la Fase 3 pidió explícitamente documentarlas sin alterar el código.

### 3.2 Importante

#### I-01 — Fondo de solidaridad con tarifa plana del 1 %

- **Archivos:** `src/TiendaFlow.Application/Payroll/PayrollParameters.cs:36-37`; `src/TiendaFlow.Application/Payroll/EmploymentPayrollStrategy.cs:34-36`.
- **Qué ocurre:** el código aplica `SolidarityFundRate = 0.01` a toda base de cotización desde 4 SMMLV, sin escalón adicional.
- **Impacto:** la norma vigente escala del 1 % al 2 % por encima de 16 SMMLV. La nómina subestima la deducción en salarios altos.
- **Estado:** documentado en `docs/sprint2/payroll-parameters-validation.md`; no se modifica por la restricción de la Fase 3.
- **Recomendación:** modelar el umbral de 16 SMMLV como parámetro y una tarifa adicional (1 % / 2 %) antes de usar el motor en producción.

#### I-02 — Retención de prestación de servicios con umbral y tarifa desactualizados

- **Archivos:** `src/TiendaFlow.Application/Payroll/PayrollParameters.cs:61-62`; `src/TiendaFlow.Application/Payroll/ServiceProvisionPayrollStrategy.cs:16-18`.
- **Qué ocurre:** se retiene 4 % desde 4 UVT, con una tarifa única.
- **Impacto:** desde el 1 de julio de 2026 el umbral es 2 UVT y la tarifa depende de la condición del contratista: 4 % para declarantes y 6 % para no declarantes. El cálculo actual sobreestima el umbral y subestima la retención en no declarantes.
- **Estado:** documentado; no se modifica por la restricción de la Fase 3.
- **Recomendación:** agregar un dato de “declarante / no declarante” al contrato de entrada y dos parámetros de tarifa, con el umbral de 2 UVT.

#### I-03 — La base de retención en la fuente incluye el auxilio de transporte

- **Archivos:** `src/TiendaFlow.Application/Payroll/EmploymentPayrollStrategy.cs:26` y `:37-38`.
- **Qué ocurre:** `gross = salaryIncome + transport` y ese `gross` se pasa a `CalculateMonthlyEmployeeWithholding`.
- **Impacto:** el auxilio de transporte no es ingreso gravable; incluirlo infla la base y puede generar retención donde no corresponde.
- **Recomendación:** pasar `salaryIncome` (sin auxilio) como ingreso bruto a la retención, dejando el auxilio solo en `GrossEarnings` y en la base de prestaciones. Añadir un caso de prueba con salario cercano al umbral.

#### I-04 — El correo intentado en un inicio de sesión fallido queda en `EntityId`

- **Archivo:** `src/TiendaFlow.Application/Security/AuthenticationService.cs:79-95` (en especial `:81-86` y `:90`).
- **Qué ocurre:** cuando el usuario no existe o la contraseña es incorrecta, se guarda el correo tecleado como identificador de entidad de la auditoría.
- **Impacto:** el correo es dato personal y puede ser un correo de alguien ajeno a la organización. No hay contraseña, pero sí un identificador personal en un evento de seguridad.
- **Mitigación actual:** el detalle ya no repite el correo; solo queda en `EntityId`.
- **Recomendación:** decidir con el equipo si se conserva para investigación de intentos de acceso o se sustituye por un identificador seudónimo (por ejemplo, hash con sal). Registrarlo en `docs/sprint2/open-questions.md` como decisión de privacidad pendiente.

#### I-05 — `POST /api/nomina/calculate` confía en el salario y el empleado que envía el cliente

- **Archivo:** `src/TiendaFlow.Web/Program.cs:139-150`.
- **Qué ocurre:** el endpoint recibe `PayrollInput` completo (`employeeId`, `contractType`, `baseSalary`, `daysWorked`, etc.) y lo pasa al motor. La autorización exige `HrPayrollManage`, pero el cálculo no vuelve a leer al empleado desde la base.
- **Impacto:** un usuario con permiso de nómina puede calcular cualquier salario arbitrario para cualquier identificador. No hay fuga de datos porque la respuesta solo contiene cifras, pero el resultado no es trazable al empleado real.
- **Recomendación:** en el endpoint, cargar el empleado por `employeeId`, validar que esté activo y construir el `PayrollInput` con su salario y contrato; o documentar que el endpoint es una calculadora de simulación y no un registro de nómina.

#### I-06 — La recepción de una orden de compra crea filas de stock con `minStock: 0`

- **Archivos:** `src/TiendaFlow.Application/PurchaseOrderService.cs:148-154` (`minStock: 0`); `src/TiendaFlow.Application/StockService.cs:53-59`.
- **Qué ocurre:** si el SKU no tiene fila de stock en la tienda, `ReceiveStock` crea una con el mínimo recibido (0). El mínimo real del producto no se copia.
- **Impacto:** la nueva fila nunca genera alerta de stock bajo hasta que alguien edite el mínimo a mano.
- **Recomendación:** resolver el mínimo desde el producto o desde una fila de referencia de la tienda antes de crear el stock; si no existe, exigir el mínimo como parte de la recepción.

### 3.3 Menor

#### M-01 — Auditoría de compras guarda el total de la orden

- **Archivo:** `src/TiendaFlow.Application/PurchaseOrderService.cs:93`.
- **Qué ocurre:** el detalle `"Orden creada con N líneas y total {order.Total:0}."` incluye un monto.
- **Impacto:** no es dato personal, pero mezcla importes en un registro pensado para trazabilidad y dificulta una política uniforme de “sin montos en auditoría”.
- **Recomendación:** mantener solo el identificador, el número de líneas y el estado; el total ya vive en la orden.

#### M-02 — La matriz de roles muestra códigos crudos de permiso

- **Archivo:** `src/TiendaFlow.Web/Components/Pages/Seguridad/Roles.razor:49`.
- **Qué ocurre:** la primera columna muestra `sales.pos.use`, `inventory.alerts.view`, etc.
- **Impacto:** es correcto para el equipo técnico, pero poco legible para el usuario de negocio que ve la pantalla en la demo.
- **Recomendación:** añadir una etiqueta en español por permiso (o mostrar el código como texto secundario) sin cambiar la fuente de verdad, que sigue siendo `Permissions`.

#### M-03 — No hay pruebas de componentes Razor (bUnit)

- **Evidencia:** `tests/TiendaFlow.UnitTests/TiendaFlow.UnitTests.csproj` y `tests/TiendaFlow.AcceptanceTests/TiendaFlow.AcceptanceTests.csproj` no referencian bUnit.
- **Impacto:** el ocultamiento de botones con `AccessControl.Has`, el enmascaramiento del salario en la tabla y los mensajes de error de las páginas solo se validan manualmente en la demo.
- **Recomendación:** agregar un proyecto de pruebas de componentes en el Sprint 3 para las páginas de RR. HH. y Seguridad, empezando por los casos “sin permiso no se ve la acción”.

#### M-04 — Colecciones estáticas mutables en los parámetros de nómina

- **Archivo:** `src/TiendaFlow.Application/Payroll/PayrollParameters.cs:10-13`, `:15-24`, `:42`, `:58`.
- **Qué ocurre:** `DefaultArlRates` es un `Dictionary` y `DefaultBrackets` un arreglo, expuestos a través de propiedades `init`.
- **Impacto:** un consumidor podría mutar la instancia compartida y alterar los valores por defecto de toda la aplicación.
- **Recomendación:** exponerlos como `IReadOnlyDictionary` / `IReadOnlyList` sobre copias inmutables (`ReadOnlyDictionary`, `Array.AsReadOnly`) o crear una instancia nueva por proveedor.

#### M-05 — CI depende del descubrimiento automático de la solución

- **Archivo:** `.github/workflows/ci.yml:14-16`.
- **Qué ocurre:** los pasos usan `dotnet build --no-restore` y `dotnet test --no-build` sin indicar `TiendaFlow.slnx`.
- **Impacto:** funciona hoy porque hay una sola solución en la raíz, pero un archivo `.sln` adicional o un cambio de estructura puede hacer que el CI compile otro conjunto de proyectos.
- **Recomendación:** fijar explícitamente `dotnet build TiendaFlow.slnx` y `dotnet test TiendaFlow.slnx` para que el CI y la validación local usen exactamente el mismo objetivo.

#### M-06 — Desalineación latente entre la política de `/inventario/alertas` y el servicio que la alimenta

- **Archivos:** `src/TiendaFlow.Web/Components/Pages/Alertas.razor:3` (`InventoryAlertsView`); `src/TiendaFlow.Application/InventoryDashboardService.cs:23` (`Demand(InventoryStockView)`).
- **Qué ocurre:** la página exige un permiso y el servicio exige otro distinto. Hoy todos los roles que tienen `InventoryAlertsView` también tienen `InventoryStockView`, por lo que no se manifiesta.
- **Impacto:** si en el futuro se crea un rol con alertas pero sin panel de inventario, la página cargará y el servicio lanzará `AccessDeniedException`.
- **Recomendación:** decidir si las alertas son una vista del panel (entonces la página debería exigir `InventoryStockView`) o una función independiente (entonces el servicio debería aceptar `InventoryAlertsView`). Documentarlo en la matriz.

#### M-07 — `UserService` valida que la tienda exista, pero no que esté activa

- **Archivo:** `src/TiendaFlow.Application/Security/UserService.cs:70-71` y `:119-120`.
- **Qué ocurre:** se comprueba `GetById(...) is not null`, no `store.Active`.
- **Impacto:** se puede asignar un usuario a una tienda inactiva. El formulario solo lista tiendas activas (`:261`), así que requiere una llamada directa al servicio.
- **Recomendación:** validar `store.Active` en el servicio, no solo en las opciones del formulario.

---

## 4. Verificaciones obligatorias

### 4.1 SRP y DIP en `PayrollEngine`

| Criterio | Resultado | Evidencia |
|---|---|---|
| Responsabilidad única | **Cumple.** `PayrollEngine` solo valida la entrada, elige la estrategia por tipo de contrato y delega. No calcula reglas de negocio ni conoce la persistencia. | `PayrollEngine.cs:16-25` |
| Abierto/cerrado | **Cumple.** Un nuevo tipo de contrato se agrega como nueva `IPayrollStrategy`, sin editar el motor. | `PayrollEngine.cs:21-24`; `EmploymentPayrollStrategy.cs:12-13`; `ServiceProvisionPayrollStrategy.cs:9` |
| Inversión de dependencias | **Cumple.** El motor depende de `IPayrollParametersProvider` e `IEnumerable<IPayrollStrategy>`, no de implementaciones concretas. | `PayrollEngine.cs:7-14`; registro en `Program.cs:31-36` |
| Estrategias con responsabilidad acotada | **Cumple.** Empleo y prestación de servicios tienen clases separadas; la retención mensual vive en `WithholdingCalculator`. | `EmploymentPayrollStrategy.cs`, `ServiceProvisionPayrollStrategy.cs`, `WithholdingCalculator.cs` |
| Validación centralizada | **Cumple.** Salario mayor que cero, días entre 1 y 30 y otros devengos no negativos se validan antes de delegar. | `PayrollEngine.cs:30-38` |
| Orquestación resiliente | **Cumple.** `PayrollRunService` convierte un empleado inválido en una línea con `Problem` en español y no tumba la corrida. | `PayrollRunService.cs:79-86`, `:88-125` |

**Observación de diseño:** la retención recibe `gross` (con auxilio) en `EmploymentPayrollStrategy.cs:37-38`; es un defecto de regla, no de SRP/DIP, y se registra como I-03.

### 4.2 Domain y Application libres de EF Core

| Verificación | Resultado |
|---|---|
| `grep` de `EntityFrameworkCore`, `DbContext`, `DbSet`, `Microsoft.EntityFrameworkCore` en `src/TiendaFlow.Domain` | **Sin coincidencias** |
| `grep` equivalente en `src/TiendaFlow.Application` | **Sin coincidencias** |
| `TiendaFlow.Domain.csproj` | Sin `PackageReference`; solo framework |
| `TiendaFlow.Application.csproj` | Solo referencia a `TiendaFlow.Domain`; sin EF Core |
| `TiendaFlow.Infrastructure.csproj` | Referencia a Application y `Microsoft.EntityFrameworkCore.SqlServer` |
| Repositorios | `EfEmployeeRepository`, `EfAuditLog`, configuraciones y `TiendaFlowDbContext` viven en Infrastructure |
| Dominio | Entidades como `Employee`, `PurchaseOrder`, `StockItem` no exponen anotaciones ni tipos de EF Core |

**Conclusión:** la regla de aislamiento se cumple. La única dependencia de EF Core está en Infrastructure, que es el adaptador de persistencia.

### 4.3 Auditoría sin datos personales

| Operación | Archivo y línea | Contenido auditado | ¿Datos personales? |
|---|---|---|---|
| Crear empleado | `EmployeeService.cs:122` | id del empleado, id de tienda | No |
| Actualizar empleado | `EmployeeService.cs:162-169` | id, estado activo, tienda; sin nombre, documento ni salario | No |
| Corrida de nómina | `PayrollRunService.cs:83` | cantidad de empleados activos y días | No |
| Crear usuario | `UserService.cs:106` | id del usuario y nombre del rol | No (corregido en `5f8719f`) |
| Actualizar usuario | `UserService.cs` (`UserUpdated`) | id, rol y estado | No (corregido en `5f8719f`) |
| Desactivar usuario | `UserService.cs` (`UserDeactivated`) | id | No (corregido en `5f8719f`) |
| Restablecer contraseña | `UserService.cs` (`PasswordReset`) | id | No (corregido en `5f8719f`) |
| Inicio de sesión correcto | `AuthenticationService.cs:67-74` | id y texto fijo | No (corregido en `5f8719f`) |
| Inicio de sesión fallido | `AuthenticationService.cs:79-95` | correo intentado como `EntityId` | **Sí**, pendiente (I-04) |
| Acceso denegado | `AccessControl.cs:29-36`; `Program.cs:166-196` | id, permiso o política, ruta | No |
| Compras | `PurchaseOrderService.cs:93`, `:160-163`, `:177` | id, líneas, estado; el alta incluye total | No personal; ver M-01 |

**Conclusión:** los recursos humanos no registran nombres, documentos, salarios ni montos. La única deuda de privacidad es el correo del intento fallido de inicio de sesión, que queda como decisión abierta.

### 4.4 Permisos en las tres capas

| Pantalla / endpoint | Capa 1: página o endpoint | Capa 2: UI (`AccessControl.Has`) | Capa 3: servicio (`Demand`) |
|---|---|---|---|
| `/rrhh/empleados` | `[Authorize(Policy = HrEmployeesView)]` — `Empleados.razor:3` | Botones de alta y nómina ocultos sin `HrEmployeesManage` / `HrPayrollManage` — `:15-22` | `List` exige `HrEmployeesView`; `Get` exige vista y solo devuelve salario con `HrEmployeesManage` — `EmployeeService.cs` |
| `/rrhh/empleados/nuevo` y `/{id}` | `[Authorize(Policy = HrEmployeesManage)]` — `EmpleadoForm.razor:4` | Formulario completo solo para quien administra | `Create` / `Update` exigen `HrEmployeesManage` — `EmployeeService.cs` |
| `/rrhh/nomina` | `[Authorize(Policy = HrPayrollManage)]` — `Nomina.razor:3` | Página completa solo con el permiso | `Calculate` exige `HrPayrollManage` — `PayrollRunService.cs:67` |
| `POST /api/nomina/calculate` | `.RequireAuthorization(Permissions.HrPayrollManage)` — `Program.cs:150` | N/A (endpoint) | Motor sin permisos propios; la autorización es del endpoint. Ver I-05 |
| `/seguridad/usuarios` | `[Authorize(Policy = SecurityUsersView)]` — `Usuarios.razor:3` | Alta y edición ocultas sin `SecurityUsersManage` — `:14`, `:54`, `:74` | `UserService` exige `SecurityUsersManage` en cada escritura |
| `/seguridad/roles` | `[Authorize(Policy = SecurityRolesView)]` — `Roles.razor:3` | Matriz de solo lectura | `RoleCatalogService.GetMatrix` exige `SecurityRolesView` — `UserService.cs:295` |
| `/seguridad/auditoria` | `[Authorize(Policy = SecurityAuditView)]` — `Auditoria.razor:3` | Sin acciones de escritura | `AuditTrailService.Recent` exige `SecurityAuditView` — `UserService.cs:282` |
| `/inventario/alertas` | `[Authorize(Policy = InventoryAlertsView)]` — `Alertas.razor:3` | Acciones según `InventoryStockReceive` — `:15` | `InventoryDashboardService.Get` exige `InventoryStockView` — desalineación M-06 |

**Conclusión:** las páginas nuevas de RR. HH., el endpoint de nómina y las pantallas de Seguridad cumplen las tres capas. La única desalineación es la de alertas (M-06), latente y sin impacto con los roles actuales.

### 4.5 Otras verificaciones

| Verificación | Resultado |
|---|---|
| Fuga de salario a quien no administra | **Cumple.** `EmployeeRow.Salary` es `decimal?`; `EmployeeService` lo deja en `null` sin `HrEmployeesManage`; la UI muestra `—` |
| Datos personales en respuestas de nómina | **Cumple.** `PayrollResult` solo lleva `EmployeeId` y cifras |
| Contraseñas en auditoría o logs | **Cumple.** No se registra la contraseña ni su hash |
| Persistencia de la corrida de nómina | **No persistida por diseño.** Documentado como limitación; requeriría tablas y migración, prohibidas en este sprint |
| Pruebas sensibles a zona horaria | **Cumple.** Las pruebas usan instantes UTC y `UtcTimestamp`; no se encontraron aserciones de hora local |
| Migraciones nuevas | **No se crearon.** El esquema del Sprint 2 ya existía en la migración `Phase2Security` |
| Secretos en el repositorio | **Cumple.** No se encontraron contraseñas ni cadenas de conexión versionadas |
| Workflows e infraestructura | Revisados, no modificados por esta revisión. Terraform es un *placeholder* declarado y el CI usa un token de HCP Terraform por secreto |

---

## 5. Correcciones aplicadas en esta revisión

Cada corrección va en su propio commit, con compilación y pruebas verdes antes de confirmar.

| Commit | Tipo | Archivos | Qué corrige | Pruebas |
|---|---|---|---|---|
| `5f8719f` | `fix(security)` | `UserService.cs`, `AuthenticationService.cs`, `UserServiceTests.cs`, `AuthenticationServiceTests.cs` | Elimina el correo del detalle de auditoría en alta, edición, desactivación, restablecimiento e inicio de sesión correcto; agrega aserciones `DoesNotContain` sobre el correo | 244 unitarias + 6 aceptación, 0 fallos |
| `3f90d73` | `docs(sprint2)` | `docs/sprint2/demo-script.md` | Alinea la demo con la matriz real (códigos crudos, fila `sales.pos.use`, `inventory.alerts.view`, `inventory.stock.receive`) y con el texto del comprobante de venta | No aplica (documentación); sin cambios de código |

**No se modificaron valores de nómina**, conforme a la Fase 3. Las discrepancias I-01, I-02 e I-03 permanecen documentadas y con propuesta.

---

## 6. Recomendaciones priorizadas

1. **Antes de producción:** resolver I-01, I-02 e I-03 con validación legal y pruebas de cálculo. Son las únicas que cambian dinero.
2. **Sprint 3, prioridad alta:** decidir I-04 (correo en `EntityId`) y I-05 (endpoint que confía en el cliente). Ambas son de privacidad y trazabilidad.
3. **Sprint 3, prioridad media:** corregir I-06 (`minStock: 0` en recepción) y M-06 (permiso de alertas), porque afectan el comportamiento del inventario y la coherencia de permisos.
4. **Sprint 3, prioridad media:** agregar pruebas de componentes (M-03) para el ocultamiento de acciones y el enmascaramiento del salario.
5. **Mejora continua:** M-01, M-02, M-04, M-05 y M-07 son ajustes acotados que pueden agruparse en un solo PR de “deuda técnica del Sprint 2”.

---

## 7. Limitaciones de esta revisión

- Es una revisión **automatizada**: no reemplaza la lectura de un integrante del equipo ni la aprobación formal en GitHub.
- No se ejecutó la aplicación contra SQL Server ni se recorrió la demo; la validación funcional de rutas se apoya en el código y en el guion de `docs/sprint2/demo-script.md`.
- No se evaluó el contenido de los 238 archivos del Sprint 1 que arrastra el diff.
- No se ejecutó `terraform plan` real ni los workflows de GitHub Actions; solo se revisó su definición.
- La cobertura (87,6 % al cierre, 85,6 % en la línea base) corresponde a Application + Domain; no mide Razor ni Infrastructure.

---

## 8. Conclusión

El trabajo del Sprint 2 es **coherente con la arquitectura y las reglas de seguridad del proyecto**: el motor de nómina respeta SRP/DIP, Domain y Application no conocen EF Core, la auditoría de RR. HH. no contiene datos personales ni montos, y las pantallas nuevas aplican permisos en página, UI y servicio. Los riesgos relevantes están identificados y acotados: tres discrepancias legales que no se tocaron por instrucción expresa, dos decisiones de privacidad/trazabilidad para el Sprint 3 y un conjunto de mejoras menores.

**Esta revisión no aprueba el PR.** La aprobación debe hacerla un integrante del equipo en GitHub tras leer este informe y el diff.
